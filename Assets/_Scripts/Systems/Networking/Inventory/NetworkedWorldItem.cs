using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using TimeGame.Systems.GridPlacement;
using TimeGame.Systems.Inventory;
using TimeGame.Systems.Networking.Inventory;

/// <summary>
/// Phase 3 — NetworkedWorldItem
///
/// The networked equivalent of WorldItem.  Attach this to every world-item prefab
/// that should exist in a multiplayer session.  It replaces WorldItem's solo-only
/// Destroy() / Spawn() calls with server-authoritative despawn / spawn.
///
/// KEY DESIGN POINTS
/// ─────────────────
/// • Server is the only machine that can Despawn this object.
/// • Client detects interaction raycast hits on this and sends SvrPickupItem to
///   its own NetworkedInventoryComponent.
/// • Uses SyncVar to broadcast item definition name + quantity so late-joining
///   clients see the correct label/mesh without extra RPCs.
/// • ContainerData and ItemInstance are NOT SyncVars — they are sent once inside
///   TgtPickupGranted via a NetPlacedItemData payload.
/// </summary>
namespace TimeGame.Systems.Networking.Inventory
{
    [RequireComponent(typeof(FishNet.Object.NetworkObject))]
    [RequireComponent(typeof(Collider))]
    public class NetworkedWorldItem : NetworkBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Item Data (set before spawning or via Initialize)")]
        [SerializeField] private InventoryItemRegistry itemRegistry;

        [Header("Physics")]
        [SerializeField] private bool usePhysics = true;
        [SerializeField] private Rigidbody rb;

        [Header("Interaction")]
        [SerializeField] private float interactionRange = 3f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // ─── SyncVars — visible to all clients immediately on spawn ───────────
        // FishNet V4: use SyncVar<T> generic field, NOT the [SyncVar] attribute.

        /// <summary>
        /// InventoryItemRegistry key.  All clients resolve the SO from this.
        /// </summary>
        private readonly SyncVar<string> _itemDefName = new SyncVar<string>(string.Empty);

        /// <summary>Stack / quantity of this world item.</summary>
        private readonly SyncVar<int> _quantity = new SyncVar<int>(1);

        // ─── Server-only data (not replicated) ───────────────────────────────

        /// <summary>
        /// The fully resolved item definition.  Server uses this.
        /// Clients resolve it via _itemDefName in OnItemDefNameChanged.
        /// </summary>
        private InventoryItemSO _itemDef;

        /// <summary>
        /// The tracked ItemInstance data for this world item (may be null for non-tracked items).
        /// Stored server-side only; sent to the picking-up client in TgtPickupGranted payload.
        /// </summary>
        private ItemInstance _itemInstance;

        /// <summary>
        /// Container contents for items that provide storage (backpacks, bags, etc.).
        /// Stored server-side only; sent to the picking-up client in TgtPickupGranted payload.
        /// </summary>
        private ContainerItemData _containerData;

        /// <summary>
        /// True once a pickup request has been ACK'd and despawn is imminent.
        /// Prevents a second simultaneous pickup on the server before Despawn() executes.
        /// </summary>
        private bool _claimedByPickup = false;

        // ─── Public accessors ────────────────────────────────────────────────

        public InventoryItemSO ItemDefinition => _itemDef;
        public int             Quantity       => _quantity.Value;
        public float           InteractionRange => interactionRange;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // FishNet V4: subscribe SyncVar OnChange callbacks here, not via attribute.
            _itemDefName.OnChange += OnItemDefNameChanged;
            _quantity.OnChange    += OnQuantityChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Initialization
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Call on the server immediately after ServerManager.Spawn() to set item data.
        /// </summary>
        [Server]
        public void ServerInitialize(
            InventoryItemSO  itemDef,
            int              quantity,
            ItemInstance     itemInstance   = null,
            ContainerItemData containerData = null)
        {
            if (itemRegistry == null)
            {
                itemRegistry = Resources.Load<InventoryItemRegistry>("InventoryItemRegistry");
                if (itemRegistry == null)
                    Debug.LogError("[NetworkedWorldItem] InventoryItemRegistry not in Resources!");
            }

            _itemDef             = itemDef;
            _itemDefName.Value   = itemDef != null ? itemDef.ItemName : string.Empty;
            _quantity.Value      = quantity;
            _itemInstance        = itemInstance;
            _containerData       = containerData;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  FishNet lifecycle
        // ─────────────────────────────────────────────────────────────────────

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Resolve item def on late-join or first visibility
            ResolveItemDef(_itemDefName.Value);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (itemRegistry == null)
                itemRegistry = Resources.Load<InventoryItemRegistry>("InventoryItemRegistry");

            // Scene-placed items are never runtime-spawned, so ServerInitialize() is never
            // called on them.  Auto-initialize from the sibling WorldItem component instead.
            if (_itemDef == null)
            {
                WorldItem worldItem = GetComponent<WorldItem>();
                if (worldItem != null && worldItem.ItemDefinition != null)
                {
                    ServerInitialize(
                        worldItem.ItemDefinition,
                        worldItem.Quantity,
                        worldItem.ItemInstance,
                        worldItem.ContainerData
                    );

                    if (debugMode)
                        Debug.Log($"[NetworkedWorldItem] Scene-placed auto-init: {worldItem.ItemDefinition.ItemName} x{worldItem.Quantity}");
                }
                else
                {
                    Debug.LogWarning($"[NetworkedWorldItem] '{gameObject.name}' has no sibling WorldItem or WorldItem.ItemDefinition is null — item will have no data on clients.", this);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SyncVar callbacks
        // ─────────────────────────────────────────────────────────────────────

        private void OnItemDefNameChanged(string prev, string next, bool asServer)
        {
            if (!asServer) ResolveItemDef(next);
        }

        private void OnQuantityChanged(int prev, int next, bool asServer) { }

        private void ResolveItemDef(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return;

            if (itemRegistry == null)
                itemRegistry = Resources.Load<InventoryItemRegistry>("InventoryItemRegistry");

            if (itemRegistry != null)
                _itemDef = itemRegistry.GetItem(defName);
            else
                Debug.LogError("[NetworkedWorldItem] Cannot resolve item — registry missing.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Server — pickup validation (called by NetworkedInventoryComponent)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Validate and execute a pickup request for the given connection.
        ///
        /// Called by NetworkedInventoryComponent.SvrPickupItem() after the server
        /// has located this NetworkObject via ObjectId.
        ///
        /// Returns a NetPickupResult describing success or the denial reason.
        /// </summary>
        [Server]
        public NetPickupResult ServerTryPickup(
            FishNet.Connection.NetworkConnection requestingConn,
            Vector3 playerPosition,
            float   playerReach,
            Guid    speculativeInstanceId)
        {
            // Guard: already claimed by another simultaneous pickup?
            if (_claimedByPickup)
                return NetPickupResult.Deny("Item already claimed.");

            // Guard: still alive / valid?
            if (_itemDef == null)
                return NetPickupResult.Deny("Item definition missing on server.");

            // Guard: distance check — cheating not a concern, but avoids teleport exploits
            float dist = Vector3.Distance(transform.position, playerPosition);
            if (dist > playerReach + 1.5f) // 1.5 m grace for latency
                return NetPickupResult.Deny($"Too far ({dist:F1} m).");

            // All good — claim the item so no other pickup can race us
            _claimedByPickup = true;

            // Build the payload the client needs to reconstruct the item in its grid
            NetItemInstance? netInstance = _itemInstance != null
                ? (NetItemInstance?)NetItemInstance.FromItemInstance(_itemInstance)
                : null;

            NetContainerSnapshot containerSnapshot = default;
            bool hasContainer = _containerData != null;
            if (hasContainer)
                containerSnapshot = InventoryNetConverter.ToNetSnapshot(_containerData);

            return NetPickupResult.Grant(
                itemDefName:         _itemDefName.Value,
                quantity:            _quantity.Value,
                instanceID:          _itemInstance?.InstanceID ?? speculativeInstanceId,
                netInstance:         netInstance,
                hasContainer:        hasContainer,
                containerSnapshot:   containerSnapshot
            );
        }

        /// <summary>
        /// Despawn this world item after a successful pickup.
        /// Must be called on the server.
        /// </summary>
        [Server]
        public void ServerDespawn()
        {
            ServerManager.Despawn(NetworkObject);
        }
    }

    // ─── Helper result struct (not serialised — stays server-side) ───────────

    public struct NetPickupResult
    {
        public bool   Success;
        public string DenyReason;

        // Granted payload fields
        public string              ItemDefName;
        public int                 Quantity;
        public Guid                InstanceID;
        public NetItemInstance?    NetInstance;
        public bool                HasContainer;
        public NetContainerSnapshot ContainerSnapshot;

        public static NetPickupResult Deny(string reason) =>
            new NetPickupResult { Success = false, DenyReason = reason };

        public static NetPickupResult Grant(
            string              itemDefName,
            int                 quantity,
            Guid                instanceID,
            NetItemInstance?    netInstance,
            bool                hasContainer,
            NetContainerSnapshot containerSnapshot) =>
            new NetPickupResult
            {
                Success           = true,
                ItemDefName       = itemDefName,
                Quantity          = quantity,
                InstanceID        = instanceID,
                NetInstance       = netInstance,
                HasContainer      = hasContainer,
                ContainerSnapshot = containerSnapshot
            };
    }
}
