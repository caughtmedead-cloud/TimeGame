using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using TimeGame.Systems.GridPlacement;
using TimeGame.Systems.Inventory;
using TimeGame.Systems.Inventory.UI;
using TimeGame.Systems.Networking.Inventory;

/// <summary>
/// Phase 2 — NetworkedInventoryComponent
///
/// AUTHORITY MODEL (recap)
/// ┌─────────────────────────────────────────────────────────────────────┐
/// │ Owner client  │ Maintains full visual + positional inventory state. │
/// │               │ Grid drag/split/merge = zero network traffic.        │
/// ├─────────────────────────────────────────────────────────────────────┤
/// │ Server        │ Keeps Dictionary<Guid, ServerItemRecord> per player. │
/// │               │ Knows WHAT items a player has (not WHERE in grid).   │
/// │               │ Validates every mutation RPC before ACKing it.       │
/// ├─────────────────────────────────────────────────────────────────────┤
/// │ Other clients │ No inventory state — only see equipped cosmetics     │
/// │               │ (handled separately in Phase 7+ if needed).          │
/// └─────────────────────────────────────────────────────────────────────┘
///
/// MESSAGE FLOW SUMMARY
///   Pickup  : owner → SvrPickupItem()         → server validates  → TgtPickupGranted / TgtPickupDenied
///   Drop    : owner → SvrDropItem()           → server validates  → TgtDropGranted / TgtDropDenied
///   Use     : owner → SvrUseItem()            → server validates  → TgtUseGranted  / TgtUseDenied
///   Loot    : owner → SvrRequestContainerOpen → server sends snapshot → TgtReceiveContainerSnapshot
///   TakeItem: owner → SvrTakeItemFromContainer→ server validates  → TgtTakeGranted / TgtTakeDenied
///
/// PHASES
///   Phase 2 (this file)  — Component skeleton + server manifest CRUD
///   Phase 3              — Pickup flow (SvrPickupItem + NetworkedWorldItem)
///   Phase 4              — Drop    flow
///   Phase 5              — Use/consume flow
///   Phase 6              — World loot container flow
/// </summary>
namespace TimeGame.Systems.Networking.Inventory
{
    /// <summary>
    /// Attach this to the player prefab alongside InventoryUIController.
    /// One instance per player.  On non-owner clients it does almost nothing.
    /// </summary>
    [RequireComponent(typeof(FishNet.Object.NetworkObject))]
    public class NetworkedInventoryComponent : NetworkBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("References — assign or auto-found on Awake")]
        [Tooltip("Item registry ScriptableObject (must be in a Resources folder)")]
        [SerializeField] private InventoryItemRegistry itemRegistry;

        [Tooltip("PlayerInventoryManager on this prefab")]
        [SerializeField] private PlayerInventoryManager playerInventoryManager;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // ─── Client-side singleton ────────────────────────────────────────────

        /// <summary>
        /// The local player's NetworkedInventoryComponent — set when IsOwner in OnStartClient.
        /// Lets UI components (e.g. FloatingContainerWindow) fire owner-only ServerRpcs
        /// without calling FindObjectOfType at runtime.
        /// </summary>
        public static NetworkedInventoryComponent LocalInstance { get; private set; }

        // ─── Server-side manifest  (only populated on the server) ─────────────

        /// <summary>
        /// Authoritative item set.  Only the server reads/writes this.
        /// Key = PlacedItem.InstanceID
        /// </summary>
        private Dictionary<Guid, ServerItemRecord> _serverManifest
            = new Dictionary<Guid, ServerItemRecord>();

        // ─── Client-side pending confirmations ────────────────────────────────

        /// <summary>
        /// Items speculatively added on the owner client while waiting for server ACK.
        /// If the server denies the request we roll back these.
        /// Key = InstanceID of the speculatively added item.
        /// </summary>
        private HashSet<Guid> _pendingPickups = new HashSet<Guid>();

        /// <summary>
        /// Container take requests that were issued speculatively (item already placed in
        /// the player's grid by the drag handler before the server confirms).
        /// TgtTakeItemGranted skips ClientAddItemToInventory for these to avoid duplicates.
        /// Key = InstanceID of the item being taken.
        /// </summary>
        private HashSet<Guid> _pendingContainerTakes = new HashSet<Guid>();

        /// <summary>
        /// Container put requests that were issued speculatively (item already placed in
        /// the container grid by the drag handler before the server confirms).
        /// TgtPutItemGranted is a no-op for these since the drag already moved the item.
        /// Key = InstanceID of the item being put into the container.
        /// </summary>
        private HashSet<Guid> _pendingContainerPuts = new HashSet<Guid>();

        // ─────────────────────────────────────────────────────────────────────
        //  Unity / FishNet lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Auto-find registry from Resources if not assigned
            if (itemRegistry == null)
            {
                itemRegistry = Resources.Load<InventoryItemRegistry>("InventoryItemRegistry");
                if (itemRegistry == null)
                    Debug.LogError("[NetworkedInventory] InventoryItemRegistry not found in Resources!");
            }

            // Auto-find PlayerInventoryManager
            if (playerInventoryManager == null)
                playerInventoryManager = GetComponentInChildren<PlayerInventoryManager>(true);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
            {
                LocalInstance = this;
                Log("Initialized for local player.");
                // Phase 3: register with world-item detection here

                // Subscribe so we know when the owner drags an item out of a container grid,
                // into a container grid, or repositions an item within a container grid.
                InventoryDragHandler.OnItemMovedBetweenGrids   += HandleItemMovedBetweenGrids;
                InventoryDragHandler.OnItemRepositionedInGrid  += HandleItemRepositionedInGrid;

                // Sync any items already in grids at startup (e.g., added by InventoryTestHarness
                // or loaded from save data) so SvrPutItemIntoContainer passes the ownership check.
                // We wait one frame so Start() methods on other components have a chance to
                // populate the grids before we scan them.
                StartCoroutine(SyncStartingInventoryNextFrame());
            }
        }

        private System.Collections.IEnumerator SyncStartingInventoryNextFrame()
        {
            yield return null; // wait one frame for Start() methods to run
            SyncInventoryWithServer();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (IsOwner)
            {
                if (LocalInstance == this) LocalInstance = null;
                InventoryDragHandler.OnItemMovedBetweenGrids  -= HandleItemMovedBetweenGrids;
                InventoryDragHandler.OnItemRepositionedInGrid -= HandleItemRepositionedInGrid;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Log($"Server tracking for connection {OwnerId}.");
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _serverManifest.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Server Manifest — CRUD  (server-only helpers)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Add an item to the server's authoritative manifest.
        /// Call this on the server whenever the player legitimately gains an item.
        /// </summary>
        [Server]
        public void ServerAddItem(PlacedItem item)
        {
            if (item == null) return;

            ServerItemRecord record = InventoryNetConverter.ToServerRecord(item);
            _serverManifest[record.InstanceID] = record;

            Log($"[Server] Manifest +ADD  {record.ItemDefName}  id={ShortGuid(record.InstanceID)}  " +
                $"count={record.StackCount}  manifest_size={_serverManifest.Count}");
        }

        /// <summary>
        /// Remove an item from the server manifest (drop / consume / destroy).
        /// Returns false if the item was not in the manifest.
        /// </summary>
        [Server]
        public bool ServerRemoveItem(Guid instanceID)
        {
            if (_serverManifest.TryGetValue(instanceID, out ServerItemRecord rec))
            {
                _serverManifest.Remove(instanceID);
                Log($"[Server] Manifest -REM  {rec.ItemDefName}  id={ShortGuid(instanceID)}  " +
                    $"manifest_size={_serverManifest.Count}");
                return true;
            }

            Log($"[Server] Manifest -REM  MISS  id={ShortGuid(instanceID)}");
            return false;
        }

        /// <summary>
        /// Check the server manifest for ownership.  Returns null if not found.
        /// </summary>
        [Server]
        public ServerItemRecord? ServerGetItem(Guid instanceID)
        {
            return _serverManifest.TryGetValue(instanceID, out ServerItemRecord rec) ? rec : (ServerItemRecord?)null;
        }

        /// <summary>
        /// Returns true if the player's manifest contains this item.
        /// </summary>
        [Server]
        public bool ServerOwnsItem(Guid instanceID) => _serverManifest.ContainsKey(instanceID);

        /// <summary>
        /// Bulk-replace the manifest (reconnect / full resync case).
        /// </summary>
        [Server]
        public void ServerReplaceManifest(IEnumerable<PlacedItem> items)
        {
            _serverManifest.Clear();
            foreach (PlacedItem item in items)
                ServerAddItem(item);

            Log($"[Server] Manifest replaced. Size={_serverManifest.Count}");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 3 — Pickup  (stubs — implemented in Phase 3)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Owner client calls this to request picking up a world item.
        ///
        /// FLOW:
        ///   1. Owner speculatively adds item to local inventory grid.
        ///   2. Owner sends this RPC with the world item's NetworkObject ID.
        ///   3. Server validates:  item still exists?  player in range?  no duplicate?
        ///   4a. Valid   → SvrPickupItem despawns world object, adds to manifest, calls TgtPickupGranted.
        ///   4b. Invalid → calls TgtPickupDenied; owner rolls back the speculative add.
        /// </summary>
        /// <param name="worldItemNetId">NetworkObject.ObjectId of the WorldItem</param>
        /// <param name="speculativeInstanceId">
        ///   The Guid the owner assigned to the speculative PlacedItem — so the server can
        ///   echo it back in the ACK so the owner promotes it from pending to confirmed.
        /// </param>
        /// <param name="playerPosition">Sender's world position — used for server-side range check.</param>
        [ServerRpc]
        public void SvrPickupItem(int worldItemNetId, Guid speculativeInstanceId, Vector3 playerPosition)
        {
            // ── Locate the networked world item ────────────────────────────────
            if (!ServerManager.Objects.Spawned.TryGetValue(worldItemNetId, out FishNet.Object.NetworkObject nob))
            {
                Log($"[Server] SvrPickupItem DENY — NetworkObject {worldItemNetId} not found.");
                TgtPickupDenied(Owner, speculativeInstanceId, "Item no longer exists.");
                return;
            }

            NetworkedWorldItem worldItem = nob.GetComponent<NetworkedWorldItem>();
            if (worldItem == null)
            {
                Log($"[Server] SvrPickupItem DENY — NetworkObject {worldItemNetId} has no NetworkedWorldItem.");
                TgtPickupDenied(Owner, speculativeInstanceId, "Invalid world item.");
                return;
            }

            // ── Delegate validation + claim to the world item ─────────────────
            float playerReach = 4f; // TODO: read from player stats / NetworkedInventoryComponent config
            NetPickupResult result = worldItem.ServerTryPickup(Owner, playerPosition, playerReach, speculativeInstanceId);

            if (!result.Success)
            {
                Log($"[Server] SvrPickupItem DENY — {result.DenyReason}");
                TgtPickupDenied(Owner, speculativeInstanceId, result.DenyReason);
                return;
            }

            // ── Grant: add to server manifest, despawn world item ─────────────
            // Build a transient PlacedItem so we can use ToServerRecord()
            // (server never adds it to a grid — manifest only)
            InventoryItemSO itemDef = itemRegistry.GetItem(result.ItemDefName);
            if (itemDef == null)
            {
                Log($"[Server] SvrPickupItem DENY — registry missing '{result.ItemDefName}'.");
                TgtPickupDenied(Owner, speculativeInstanceId, "Item definition not found on server.");
                return;
            }

            PlacedItem transient = new PlacedItem(
                result.InstanceID,
                itemDef,
                Vector2Int.zero,
                GridDirection.Down,
                result.Quantity
            );

            // Attach instance data for manifest record if tracked
            if (result.NetInstance.HasValue)
            {
                transient.AddInstances(new List<ItemInstance>
                {
                    result.NetInstance.Value.ToItemInstance()
                });
            }

            ServerAddItem(transient);
            worldItem.ServerDespawn();

            // ── Notify owner — include full item payload for client-side grid placement ──
            TgtPickupGranted(
                Owner,
                speculativeInstanceId,
                result.InstanceID,
                result.ItemDefName,
                result.Quantity,
                result.NetInstance ?? default,
                result.NetInstance.HasValue,
                result.HasContainer,
                result.ContainerSnapshot
            );

            Log($"[Server] SvrPickupItem GRANTED — {result.ItemDefName} x{result.Quantity}  id={ShortGuid(result.InstanceID)}");
        }

        /// <summary>
        /// Server → owner only: pickup was approved.
        /// Owner promotes the speculative item to confirmed, or adds it fresh if the
        /// speculative add wasn't done yet.
        /// </summary>
        [TargetRpc]
        private void TgtPickupGranted(
            NetworkConnection conn,
            Guid   speculativeInstanceId,
            Guid   confirmedInstanceId,
            string itemDefName,
            int    quantity,
            NetItemInstance netInstance,
            bool   hasNetInstance,
            bool   hasContainer,
            NetContainerSnapshot containerSnapshot)
        {
            _pendingPickups.Remove(speculativeInstanceId);

            Log($"[Client] TgtPickupGranted  item={itemDefName} x{quantity}  " +
                $"specId={ShortGuid(speculativeInstanceId)}  confirmedId={ShortGuid(confirmedInstanceId)}");

            // If the speculative ID differs from the confirmed ID, we need to reconcile.
            // For now we remove the speculative item and re-add with the confirmed ID.
            // (This only happens if the server reassigned the InstanceID — rare but safe to handle.)
            bool idMismatch = speculativeInstanceId != confirmedInstanceId;
            if (idMismatch)
            {
                Log($"[Client] ID mismatch — rolling speculative add and replacing with confirmed id.");
                RemoveSpeculativeItem(speculativeInstanceId);
            }

            // Resolve item def
            if (itemRegistry == null)
                itemRegistry = Resources.Load<InventoryItemRegistry>("InventoryItemRegistry");

            InventoryItemSO itemDef = itemRegistry != null ? itemRegistry.GetItem(itemDefName) : null;
            if (itemDef == null)
            {
                Debug.LogError($"[NetworkedInventory] TgtPickupGranted — cannot resolve '{itemDefName}'");
                return;
            }

            // Reconstruct ItemInstance if tracked
            ItemInstance itemInstance = hasNetInstance ? netInstance.ToItemInstance() : null;

            // Reconstruct ContainerItemData if present
            ContainerItemData containerData = null;
            if (hasContainer && itemRegistry != null)
                containerData = InventoryNetConverter.FromNetSnapshot(containerSnapshot, itemRegistry);

            // If the speculative add placed a correct-ID item already, just confirm it.
            // Otherwise add it now via PlayerItemInteraction's pickup path.
            if (!idMismatch)
            {
                ConfirmSpeculativeItem(confirmedInstanceId);
                return;
            }

            // Re-add with confirmed data (id mismatch path)
            if (playerInventoryManager != null)
            {
                ClientAddItemToInventory(itemDef, quantity, confirmedInstanceId, itemInstance, containerData);
            }
        }

        /// <summary>
        /// Server → owner only: pickup was denied.
        /// Owner removes the speculatively added item from the local grid.
        /// </summary>
        [TargetRpc]
        private void TgtPickupDenied(NetworkConnection conn, Guid speculativeInstanceId, string reason)
        {
            _pendingPickups.Remove(speculativeInstanceId);

            Log($"[Client] TgtPickupDenied  specId={ShortGuid(speculativeInstanceId)}  reason={reason}");

            // Roll back — remove the speculatively placed item from the local grid
            RemoveSpeculativeItem(speculativeInstanceId);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 4 — Drop flow
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Owner requests to drop an item from their inventory into the world.
        ///
        /// FLOW:
        ///   1. Owner removes item from its local grid immediately (speculative remove).
        ///   2. Owner sends SvrDropItem with the item's InstanceID + desired world position.
        ///   3. Server validates ownership via manifest.
        ///   4a. Valid   → removes from manifest, spawns NetworkedWorldItem, calls TgtDropGranted.
        ///   4b. Invalid → calls TgtDropDenied; owner must re-add item to its local grid.
        ///
        /// Drop position is server-clamped within ±2 m of the player's reported position to
        /// prevent teleport-drops.  The final spawn position is echoed back in TgtDropGranted.
        /// </summary>
        /// <param name="instanceId">InstanceID of the PlacedItem to drop</param>
        /// <param name="desiredWorldPosition">Drop position the owner requests</param>
        /// <param name="playerPosition">Owner's current world position — used for range clamp</param>
        [ServerRpc]
        public void SvrDropItem(Guid instanceId, Vector3 desiredWorldPosition, Vector3 playerPosition)
        {
            // ── Validate ownership ──────────────────────────────────────────────
            if (!ServerOwnsItem(instanceId))
            {
                Log($"[Server] SvrDropItem DENY — player does not own {ShortGuid(instanceId)}.");
                TgtDropDenied(Owner, instanceId, "Item not in your inventory.");
                return;
            }

            ServerItemRecord record = _serverManifest[instanceId];

            // ── Resolve item def ────────────────────────────────────────────────
            InventoryItemSO itemDef = itemRegistry != null ? itemRegistry.GetItem(record.ItemDefName) : null;
            if (itemDef == null)
            {
                Log($"[Server] SvrDropItem DENY — unknown item def '{record.ItemDefName}'.");
                TgtDropDenied(Owner, instanceId, "Item definition not found on server.");
                return;
            }

            // ── Clamp drop position to within 3 m of the player ────────────────
            const float MaxDropDist = 3f;
            Vector3 toDesired = desiredWorldPosition - playerPosition;
            if (toDesired.magnitude > MaxDropDist)
                toDesired = toDesired.normalized * MaxDropDist;
            Vector3 finalPos = playerPosition + toDesired;
            // Keep the drop slightly above ground to avoid z-fighting / embedding
            finalPos.y = desiredWorldPosition.y;

            // ── Spawn the NetworkedWorldItem ────────────────────────────────────
            if (itemDef.WorldItemPrefab == null)
            {
                Log($"[Server] SvrDropItem DENY — '{record.ItemDefName}' has no WorldItemPrefab.");
                TgtDropDenied(Owner, instanceId, "No world prefab for this item.");
                return;
            }

            // Remove from manifest before spawning (avoids brief double-ownership window)
            ServerRemoveItem(instanceId);

            // Instantiate + spawn on server
            // Note: the prefab must have a NetworkObject and NetworkedWorldItem component.
            GameObject spawned = Instantiate(itemDef.WorldItemPrefab, finalPos, Quaternion.identity);
            NetworkedWorldItem netItem = spawned.GetComponent<NetworkedWorldItem>();

            if (netItem == null)
            {
                // Prefab is not yet networked — destroy and deny gracefully
                Destroy(spawned);
                Log($"[Server] SvrDropItem ERROR — WorldItemPrefab for '{record.ItemDefName}' " +
                    "lacks NetworkedWorldItem component.  Add it to the prefab.");
                // Re-add to manifest so the client's speculative remove is also rolled back
                PlacedItem transient = new PlacedItem(
                    instanceId, itemDef, Vector2Int.zero, GridDirection.Down, record.StackCount);
                ServerAddItem(transient);
                TgtDropDenied(Owner, instanceId, "Server prefab error — drop cancelled.");
                return;
            }

            // Initialise item data on the world object before spawning so SyncVars
            // are set before clients receive the spawn message.
            netItem.ServerInitialize(itemDef, record.StackCount);
            ServerManager.Spawn(spawned);

            Log($"[Server] SvrDropItem GRANTED — {record.ItemDefName} x{record.StackCount} " +
                $"at {finalPos}  id={ShortGuid(instanceId)}");

            TgtDropGranted(Owner, instanceId, finalPos);
        }

        /// <summary>
        /// Server → owner: drop was accepted.
        /// The world item is already spawning across the network — nothing more to do on
        /// the client unless we want to play a drop animation or confirmation sound.
        /// </summary>
        [TargetRpc]
        private void TgtDropGranted(NetworkConnection conn, Guid instanceId, Vector3 finalPosition)
        {
            Log($"[Client] TgtDropGranted  id={ShortGuid(instanceId)}  pos={finalPosition}");
            // The speculative remove already happened before the RPC was sent.
            // Nothing to reconcile on the happy path.
            // Future: trigger drop SFX / VFX here.
        }

        /// <summary>
        /// Server → owner: drop was denied.
        /// Owner must restore the item back into its inventory grid.
        /// </summary>
        [TargetRpc]
        private void TgtDropDenied(NetworkConnection conn, Guid instanceId, string reason)
        {
            Log($"[Client] TgtDropDenied  id={ShortGuid(instanceId)}  reason={reason}");
            // TODO: restore the speculatively-removed item back into the grid.
            // This requires caching the item data before the speculative remove.
            // For now, log a warning so it's visible during testing.
            Debug.LogWarning($"[NetworkedInventory] Drop denied for {ShortGuid(instanceId)}: {reason}. " +
                             "Item should be restored — implement drop rollback cache in Phase 4b.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 5 — Use / Consume flow
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Owner requests to use or consume an item.
        ///
        /// FLOW:
        ///   1. Owner calls SvrUseItem with the item's InstanceID.
        ///   2. Server validates: does the player own it?  Does it have uses remaining?
        ///   3a. Valid + uses exhausted → item consumed: removed from manifest, TgtUseGranted(usesLeft=-1).
        ///   3b. Valid + uses remain   → decrements server record, TgtUseGranted(usesLeft=N).
        ///   3c. Invalid              → TgtUseDenied.
        ///
        /// The actual game-effect (heal, buff, etc.) is applied server-side by ItemEffectSystem
        /// (not yet implemented) and broadcast via ObserversRpc if visible to other players.
        /// The client only updates its local item state (use count decrement / removal).
        /// </summary>
        [ServerRpc]
        public void SvrUseItem(Guid instanceId)
        {
            // ── Validate ownership ──────────────────────────────────────────────
            if (!_serverManifest.TryGetValue(instanceId, out ServerItemRecord record))
            {
                Log($"[Server] SvrUseItem DENY — player does not own {ShortGuid(instanceId)}.");
                TgtUseDenied(Owner, instanceId, "Item not in your inventory.");
                return;
            }

            // ── Validate uses ───────────────────────────────────────────────────
            if (record.UsesRemaining == 0)
            {
                Log($"[Server] SvrUseItem DENY — {record.ItemDefName} has no uses remaining.");
                TgtUseDenied(Owner, instanceId, "No uses remaining.");
                return;
            }

            // ── Apply use on server manifest ────────────────────────────────────
            int newUses;
            if (record.UsesRemaining < 0)
            {
                // Infinite-use item (e.g. a tool) — never remove, just confirm
                newUses = -1;
                Log($"[Server] SvrUseItem — {record.ItemDefName} infinite-use, no decrement.");
            }
            else
            {
                record.UsesRemaining--;
                newUses = record.UsesRemaining;
                _serverManifest[instanceId] = record; // structs need re-assignment

                Log($"[Server] SvrUseItem — {record.ItemDefName}  uses_now={newUses}");

                // Consumed — remove from manifest
                if (newUses <= 0)
                {
                    ServerRemoveItem(instanceId);
                    newUses = 0; // signal "consumed" to client
                }
            }

            // TODO: Dispatch to ItemEffectSystem.Apply(record.ItemDefName, Owner) here.

            TgtUseGranted(Owner, instanceId, newUses);
        }

        /// <summary>
        /// Server → owner: use was accepted.
        /// <paramref name="newUsesRemaining"/> == 0 means the item was fully consumed.
        /// <paramref name="newUsesRemaining"/> == -1 means infinite-use (no change needed).
        /// </summary>
        [TargetRpc]
        private void TgtUseGranted(NetworkConnection conn, Guid instanceId, int newUsesRemaining)
        {
            Log($"[Client] TgtUseGranted  id={ShortGuid(instanceId)}  uses_left={newUsesRemaining}");

            if (playerInventoryManager == null) return;

            foreach (InventoryGridVisual grid in playerInventoryManager.GetAllGrids())
            {
                if (grid == null || grid.InventorySystem == null) continue;

                foreach (PlacedItem item in grid.InventorySystem.GetAllItems())
                {
                    if (item.InstanceID != instanceId) continue;

                    if (newUsesRemaining == 0)
                    {
                        // Item fully consumed — remove from grid
                        grid.InventorySystem.RemoveItem(instanceId);
                        grid.RefreshAllItemVisuals();
                        Log($"[Client] Consumed item removed from grid.");
                    }
                    else if (newUsesRemaining > 0 && item.IsInstanceTracked && item.ItemInstances != null)
                    {
                        // Decrement uses on the first (or only) tracked instance
                        if (item.ItemInstances.Count > 0)
                        {
                            item.ItemInstances[0].UseItem();
                            grid.RefreshAllItemVisuals();
                        }
                    }
                    // newUsesRemaining == -1: infinite use, nothing to update visually
                    return;
                }
            }

            Log($"[Client] TgtUseGranted — item {ShortGuid(instanceId)} not found in any local grid.");
        }

        /// <summary>
        /// Server → owner: use was denied.
        /// No local state was changed speculatively, so no rollback needed.
        /// </summary>
        [TargetRpc]
        private void TgtUseDenied(NetworkConnection conn, Guid instanceId, string reason)
        {
            Log($"[Client] TgtUseDenied  id={ShortGuid(instanceId)}  reason={reason}");
            // No speculative change was made before sending SvrUseItem, so nothing to roll back.
            // Display a "can't use" UI message here if needed.
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 6 — World loot container flow
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Owner requests to open a world loot container (chest, bag, crate, etc.).
        /// Server replies with the full ContainerSnapshot so the client can populate its UI.
        /// Multiple players may view simultaneously — no exclusive lock is held.
        /// </summary>
        [ServerRpc]
        public void SvrRequestContainerOpen(int containerNetId)
        {
            if (!ServerManager.Objects.Spawned.TryGetValue(containerNetId, out FishNet.Object.NetworkObject nob))
            {
                Log($"[Server] SvrRequestContainerOpen DENY — NetworkObject {containerNetId} not found.");
                return;
            }

            NetworkedWorldLootContainer netContainer = nob.GetComponent<NetworkedWorldLootContainer>();
            if (netContainer == null)
            {
                Log($"[Server] SvrRequestContainerOpen DENY — no NetworkedWorldLootContainer on {containerNetId}.");
                return;
            }

            NetContainerSnapshot snapshot = netContainer.ServerGetSnapshot();
            Log($"[Server] SvrRequestContainerOpen — sending snapshot  compartments={snapshot.Compartments?.Length}");
            TgtReceiveContainerSnapshot(Owner, containerNetId, snapshot);
        }

        /// <summary>
        /// Server → requesting client: full container snapshot.
        /// Client feeds this into ContainerInteractionManager to populate the UI left panel.
        /// </summary>
        [TargetRpc]
        private void TgtReceiveContainerSnapshot(
            NetworkConnection    conn,
            int                  containerNetId,
            NetContainerSnapshot snapshot)
        {
            Log($"[Client] TgtReceiveContainerSnapshot  containerNetId={containerNetId}  " +
                $"compartments={snapshot.Compartments?.Length}");

            if (itemRegistry == null)
                itemRegistry = Resources.Load<InventoryItemRegistry>("InventoryItemRegistry");

            ContainerItemData containerData = InventoryNetConverter.FromNetSnapshot(snapshot, itemRegistry);
            if (containerData == null) return;

            // Build a LootContainer that ContainerInteractionManager can display
            List<ContainerCompartment> compartmentList = new List<ContainerCompartment>();
            for (int c = 0; c < containerData.Compartments.Count; c++)
            {
                CompartmentData compData = containerData.Compartments[c];

                InventorySystem inv = new InventorySystem(
                    compData.GridSize.x, compData.GridSize.y,
                    64f, Vector3.zero, compData.MaxWeight);

                containerData.LoadIntoInventorySystem(inv, c);

                compartmentList.Add(new ContainerCompartment
                {
                    Label           = $"Compartment {c + 1}",
                    GridSize        = compData.GridSize,
                    MaxWeight       = compData.MaxWeight,
                    InventorySystem = inv
                });
            }

            LootContainer lootContainer = LootContainer.CreateMultiCompartment("Container", compartmentList);

            if (ContainerInteractionManager.Instance != null)
                ContainerInteractionManager.Instance.OpenContainer(lootContainer, null, containerNetId);
        }

        /// <summary>
        /// Owner requests to take a specific item from an open world container.
        ///
        /// PATH-BASED: containerPath chains InstanceIDs from the root compartment down to
        /// the direct parent of the target.  Empty path = item is directly in the compartment.
        ///
        /// This RPC is called speculatively — the drag handler has already placed the item
        /// in the player's grid.  TgtTakeItemGranted will confirm without re-adding.
        /// </summary>
        [ServerRpc]
        public void SvrTakeItemFromContainer(
            int    containerNetId,
            int    compartmentIndex,
            Guid[] containerPath,
            Guid   targetInstanceId,
            int    splitCount = -1)    // -1 = take the whole item; >0 = take only this many (stack split)
        {
            if (!ServerManager.Objects.Spawned.TryGetValue(containerNetId, out FishNet.Object.NetworkObject nob))
            {
                Log($"[Server] SvrTakeItemFromContainer DENY — NetworkObject {containerNetId} not found.");
                TgtTakeItemDenied(Owner, targetInstanceId, "Container no longer exists.");
                return;
            }

            NetworkedWorldLootContainer netContainer = nob.GetComponent<NetworkedWorldLootContainer>();
            if (netContainer == null)
            {
                TgtTakeItemDenied(Owner, targetInstanceId, "Invalid container.");
                return;
            }

            // PRE-VALIDATE: resolve the item definition BEFORE removing the item from the container.
            //
            // Previously, ServerTryTakeItem was called first (which removed the item), then the
            // registry lookup happened afterwards.  If the lookup returned null (empty name, missing
            // entry, or failed InventoryItemSO cast), ServerAddItem was silently skipped — the item
            // was gone from _serverCompartments but never registered in _serverManifest.  Every
            // subsequent SvrPutItemIntoContainer would then fail the ServerOwnsItem check, the
            // speculative UI move was never rolled back, and the item vanished from the container
            // on the next open.  This mirrors the early-exit pattern already used in SvrPickupItem.
            string preCheckName = netContainer.ServerGetItemDefName(containerPath, targetInstanceId, compartmentIndex);
            InventoryItemSO itemDef = !string.IsNullOrEmpty(preCheckName)
                ? itemRegistry?.GetItem(preCheckName)
                : null;

            if (itemDef == null)
            {
                Log($"[Server] SvrTakeItemFromContainer DENY — cannot resolve item def " +
                    $"'{preCheckName ?? "unknown"}' for target {ShortGuid(targetInstanceId)} " +
                    $"in container {containerNetId}.  Check InventoryItemRegistry.");
                TgtTakeItemDenied(Owner, targetInstanceId, "Item definition not found on server.");
                return;
            }

            // itemDef is guaranteed non-null — safe to commit the removal.
            NetTakeResult result = netContainer.ServerTryTakeItem(containerPath, targetInstanceId, compartmentIndex, splitCount);

            if (!result.Success)
            {
                Log($"[Server] SvrTakeItemFromContainer DENY — {result.DenyReason}");
                TgtTakeItemDenied(Owner, targetInstanceId, result.DenyReason);
                return;
            }

            // Add to the player's server manifest.
            // itemDef was resolved above — this call is unconditional on the success path.
            PlacedItem transient = new PlacedItem(
                targetInstanceId, itemDef, Vector2Int.zero, GridDirection.Down,
                result.ItemData.StackCount);
            ServerAddItem(transient);

            // ServerTryTakeItem calls PlacedItemData.FromPlacedItem() which recursively
            // captures ContainerInventory, so result.ItemData.HasContainer is true for
            // container items with contents.  Patch the manifest entry to preserve this
            // snapshot — it is needed when the player later puts the item into a world
            // container so SvrPutItemIntoContainer can call ServerTryPutItem with the
            // nested contents and the container on the server won't appear empty on reopen.
            if (result.ItemData.HasContainer && _serverManifest.ContainsKey(targetInstanceId))
            {
                ServerItemRecord withContainer = _serverManifest[targetInstanceId];
                withContainer.HasContainerSnapshot = true;
                withContainer.ContainerSnapshot    = result.ItemData.ContainerSnapshot;
                _serverManifest[targetInstanceId]  = withContainer;
            }

            Log($"[Server] SvrTakeItemFromContainer GRANTED — {result.ItemData.ItemDefName}  " +
                $"id={ShortGuid(targetInstanceId)}");

            TgtTakeItemGranted(Owner, targetInstanceId, result.ItemData);
        }

        /// <summary>
        /// Server → owner: take granted.
        ///
        /// SPECULATIVE PATH (normal drag flow):
        ///   The drag handler already placed the item in the player's grid before this RPC
        ///   arrives.  We just clear the pending marker — no second add needed.
        ///
        /// NON-SPECULATIVE PATH (future server-initiated grants):
        ///   If the item was not speculatively placed, add it now via ClientAddItemToInventory.
        /// </summary>
        [TargetRpc]
        private void TgtTakeItemGranted(
            NetworkConnection conn,
            Guid              targetInstanceId,
            NetPlacedItemData itemData)
        {
            Log($"[Client] TgtTakeItemGranted  id={ShortGuid(targetInstanceId)}  item={itemData.ItemDefName}");

            // SPECULATIVE PATH: item was already placed in the grid by the drag handler.
            // Just remove the pending marker and return — no need to re-add.
            if (_pendingContainerTakes.Remove(targetInstanceId))
            {
                Log($"[Client] TgtTakeItemGranted (speculative) — item already in grid, skipping re-add.");
                return;
            }

            // NON-SPECULATIVE PATH (future use):
            if (itemRegistry == null)
                itemRegistry = Resources.Load<InventoryItemRegistry>("InventoryItemRegistry");

            InventoryItemSO itemDef = itemRegistry != null ? itemRegistry.GetItem(itemData.ItemDefName) : null;
            if (itemDef == null)
            {
                Debug.LogError($"[NetworkedInventory] TgtTakeItemGranted — unknown item '{itemData.ItemDefName}'");
                return;
            }

            ItemInstance itemInstance = null;
            if (itemData.IsInstanceTracked && itemData.Instances != null && itemData.Instances.Length > 0)
                itemInstance = itemData.Instances[0].ToItemInstance();

            ContainerItemData containerData = itemData.HasContainer
                ? InventoryNetConverter.FromNetSnapshot(itemData.ContainerSnapshot, itemRegistry)
                : null;

            if (playerInventoryManager != null)
                ClientAddItemToInventory(itemDef, itemData.StackCount, targetInstanceId, itemInstance, containerData);
        }

        /// <summary>
        /// Server → owner: take denied (item was taken by someone else, race condition, etc.).
        /// The drag already placed the item speculatively, so ideally we'd roll it back.
        /// For now we log a warning — full drag rollback can be implemented in a future phase.
        /// </summary>
        [TargetRpc]
        private void TgtTakeItemDenied(
            NetworkConnection conn,
            Guid   targetInstanceId,
            string reason)
        {
            _pendingContainerTakes.Remove(targetInstanceId);

            Log($"[Client] TgtTakeItemDenied  id={ShortGuid(targetInstanceId)}  reason={reason}");

            // TODO Phase 6b: roll back the speculative item add (remove from player grid and
            // restore it to the container's InventorySystem + refresh container UI).
            Debug.LogWarning($"[NetworkedInventory] Container take denied for {ShortGuid(targetInstanceId)}: {reason}. " +
                             "Speculative item should be rolled back — implement in Phase 6b.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 6 — Container intra-reposition (item moved within the container)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Owner requests to reposition an item that is already inside an open world container.
        ///
        /// Handles two sub-cases that previously had no server sync:
        ///
        ///   SAME-COMPARTMENT DRAG  (sourceCompartmentIndex == targetCompartmentIndex):
        ///     The drag handler fires OnItemRepositionedInGrid; this RPC syncs the new
        ///     position to _serverCompartments so that the next snapshot reflects it.
        ///
        ///   CROSS-COMPARTMENT DRAG within the same container:
        ///     The drag handler fires OnItemMovedBetweenGrids with both source and target
        ///     being grids of the same container.  HandleItemMovedBetweenGrids detects this
        ///     and calls this RPC instead of TAKE + (missing) PUT — keeping the item in
        ///     the container without any manifest change.
        ///
        /// The speculative drag has already updated the local InventorySystems on the owner
        /// client.  The server validates placement and broadcasts RpcItemMovedInContainer
        /// so every other viewer's UI stays in sync.
        /// </summary>
        [ServerRpc]
        public void SvrMoveItemInContainer(
            int           containerNetId,
            int           sourceCompartmentIndex,
            int           targetCompartmentIndex,
            Guid          itemInstanceId,
            Vector2Int    newGridPosition,
            GridDirection newRotation,
            Guid[]        containerPath = null)   // path from root compartment to the container holding the item
        {
            if (!ServerManager.Objects.Spawned.TryGetValue(containerNetId, out FishNet.Object.NetworkObject nob))
            {
                Log($"[Server] SvrMoveItemInContainer DENY — NetworkObject {containerNetId} not found.");
                return;
            }

            NetworkedWorldLootContainer netContainer = nob.GetComponent<NetworkedWorldLootContainer>();
            if (netContainer == null)
            {
                Log($"[Server] SvrMoveItemInContainer DENY — no NetworkedWorldLootContainer on {containerNetId}.");
                return;
            }

            // Items being repositioned WITHIN the container must NOT be in a player's manifest.
            // If the server still records this player as owning the item, the earlier TAKE RPC
            // hasn't been processed yet or something went wrong — refuse silently.
            if (ServerOwnsItem(itemInstanceId))
            {
                Log($"[Server] SvrMoveItemInContainer DENY — item {ShortGuid(itemInstanceId)} " +
                    $"is in player manifest, not container.");
                return;
            }

            bool success = netContainer.ServerTryMoveItem(
                sourceCompartmentIndex, targetCompartmentIndex,
                itemInstanceId, newGridPosition, newRotation,
                containerPath);

            if (success)
            {
                Log($"[Server] SvrMoveItemInContainer GRANTED — {ShortGuid(itemInstanceId)} " +
                    $"comp {sourceCompartmentIndex}→{targetCompartmentIndex} pos={newGridPosition} " +
                    $"pathDepth={containerPath?.Length ?? 0}");
            }
            else
            {
                Log($"[Server] SvrMoveItemInContainer DENY — container rejected move " +
                    $"for {ShortGuid(itemInstanceId)} to pos={newGridPosition}.");
                // TODO Phase 6e: TgtMoveItemDenied — roll back the speculative reposition on owner.
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Root compartment → nested container move
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Owner requests to move an item from a root compartment grid directly into a
        /// nested container that is open as a floating window (e.g. root → backpack).
        ///
        /// This is distinct from SvrPutItemIntoNestedContainer (player inventory → floating
        /// window) because here the SOURCE is the world container itself, not the player's
        /// manifest. The item must be removed from the root compartment and placed at the
        /// nested path in one atomic server operation.
        /// </summary>
        [ServerRpc]
        private void SvrMoveItemFromRootToNested(
            int                  containerNetId,
            int                  sourceCompartmentIndex,
            int                  targetCompartmentIndex,
            Guid[]               targetContainerPath,
            Guid                 itemInstanceId,
            Vector2Int           newGridPosition,
            GridDirection        newRotation,
            int                  stackCount,
            bool                 hasContainerSnapshot,
            NetContainerSnapshot containerSnapshot)
        {
            if (!ServerManager.Objects.Spawned.TryGetValue(
                    containerNetId, out FishNet.Object.NetworkObject nob))
            {
                Log($"[Server] SvrMoveItemFromRootToNested DENY — NetworkObject {containerNetId} not found.");
                return;
            }

            NetworkedWorldLootContainer netContainer = nob.GetComponent<NetworkedWorldLootContainer>();
            if (netContainer == null)
            {
                Log("[Server] SvrMoveItemFromRootToNested DENY — no container component.");
                return;
            }

            netContainer.ServerMoveFromRootToNested(
                sourceCompartmentIndex,
                targetCompartmentIndex,
                targetContainerPath,
                itemInstanceId,
                newGridPosition,
                newRotation,
                stackCount,
                hasContainerSnapshot,
                containerSnapshot);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 6 — Cross-nested container move
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Owner requests to move an item from one nested container (floating window) to
        /// another nested container (floating window) that both live inside the same world
        /// loot container compartment.
        ///
        /// Example: dragging a bandage from a backpack into a pouch that is also inside
        /// the backpack — sourcePath=[backpackId], targetPath=[backpackId, pouchId].
        ///
        /// The drag handler already applied the move speculatively on the client.  This RPC
        /// tells the server to mirror it so that other clients and future reopens are correct.
        /// </summary>
        [ServerRpc]
        private void SvrMoveItemBetweenNestedContainers(
            int           containerNetId,
            int           compartmentIndex,
            Guid[]        sourceContainerPath,
            Guid[]        targetContainerPath,
            Guid          itemInstanceId,
            Vector2Int    newGridPosition,
            GridDirection newRotation,
            int           stackCount)
        {
            if (!ServerManager.Objects.Spawned.TryGetValue(
                    containerNetId, out FishNet.Object.NetworkObject nob))
            {
                Log($"[Server] SvrMoveItemBetweenNestedContainers DENY — " +
                    $"NetworkObject {containerNetId} not found.");
                return;
            }

            NetworkedWorldLootContainer netContainer =
                nob.GetComponent<NetworkedWorldLootContainer>();
            if (netContainer == null)
            {
                Log($"[Server] SvrMoveItemBetweenNestedContainers DENY — no container component.");
                return;
            }

            netContainer.ServerMoveItemBetweenPaths(
                compartmentIndex,
                sourceContainerPath,
                targetContainerPath,
                itemInstanceId,
                newGridPosition,
                newRotation,
                stackCount);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 6 — Container put (drag hook — inverse of take)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Owner requests to put an item from their inventory into an open world container.
        ///
        /// FLOW:
        ///   1. Owner drags item from player-inventory grid to container-compartment grid.
        ///   2. Drag handler speculatively places the item in the container grid locally.
        ///   3. Owner sends this RPC with the target container, compartment index, and grid position.
        ///   4a. Valid → server adds item to _serverCompartments, removes from manifest,
        ///               calls RpcItemAddedToContainer (all viewers update), TgtPutItemGranted.
        ///   4b. Invalid → TgtPutItemDenied; owner should roll back (TODO Phase 6d).
        /// </summary>
        [ServerRpc]
        public void SvrPutItemIntoContainer(
            int                  containerNetId,
            int                  compartmentIndex,
            Guid                 itemInstanceId,
            Vector2Int           gridPosition,
            GridDirection        rotation,
            int                  stackCount                  = -1,    // -1 = use full manifest count (whole-stack move)
            bool                 clientHasContainerSnapshot  = false,  // true when the item being put is a container with contents
            NetContainerSnapshot clientContainerSnapshot     = default) // live snapshot captured by the client just before put
        {
            // Locate the container
            if (!ServerManager.Objects.Spawned.TryGetValue(containerNetId, out FishNet.Object.NetworkObject nob))
            {
                Log($"[Server] SvrPutItemIntoContainer DENY — NetworkObject {containerNetId} not found.");
                TgtPutItemDenied(Owner, itemInstanceId, "Container no longer exists.");
                return;
            }

            NetworkedWorldLootContainer netContainer = nob.GetComponent<NetworkedWorldLootContainer>();
            if (netContainer == null)
            {
                TgtPutItemDenied(Owner, itemInstanceId, "Invalid container.");
                return;
            }

            // Verify the player actually owns this item
            if (!ServerOwnsItem(itemInstanceId))
            {
                Log($"[Server] SvrPutItemIntoContainer DENY — player does not own {ShortGuid(itemInstanceId)}.");
                TgtPutItemDenied(Owner, itemInstanceId, "Item not in your inventory.");
                return;
            }

            ServerItemRecord record = _serverManifest[itemInstanceId];

            // Resolve item definition
            InventoryItemSO itemDef = itemRegistry != null ? itemRegistry.GetItem(record.ItemDefName) : null;
            if (itemDef == null)
            {
                Log($"[Server] SvrPutItemIntoContainer DENY — unknown item def '{record.ItemDefName}'.");
                TgtPutItemDenied(Owner, itemInstanceId, "Item definition not found on server.");
                return;
            }

            // Determine how many items to place.
            // For whole-stack moves stackCount == -1 (or equals the full count) → use
            // the full manifest count and remove the item from the manifest entirely.
            // For split moves (0 < stackCount < record.StackCount) → place only that
            // many and reduce the manifest — the player keeps the remainder in their grid.
            int actualCount = (stackCount > 0 && stackCount <= record.StackCount)
                ? stackCount
                : record.StackCount;
            bool isPartialPut = actualCount < record.StackCount;

            // Delegate placement to the container (also fires RpcItemAddedToContainer).
            //
            // Use the LIVE snapshot that the client sent with this RPC rather than the
            // cached one in the server manifest.  The manifest snapshot is taken when the
            // item first enters the player's possession (pickup / take-from-container) and
            // is therefore STALE if the player has since modified the backpack's contents.
            // The client captures the current ContainerInventory state immediately before
            // calling this RPC, so clientContainerSnapshot always reflects the item as-is.
            //
            // Fallback: if the client didn't send a snapshot (non-container item, or empty
            // container) use the manifest snapshot so the server can at least try to
            // restore what it last knew about this item's contents.
            // Client snapshot takes priority; manifest snapshot is the fallback.
            bool hasSnapshot = clientHasContainerSnapshot || record.HasContainerSnapshot;
            NetContainerSnapshot snapshot = clientHasContainerSnapshot
                ? clientContainerSnapshot
                : record.ContainerSnapshot;

            bool success = netContainer.ServerTryPutItem(
                itemInstanceId, itemDef, actualCount, compartmentIndex, gridPosition, rotation,
                hasSnapshot, snapshot, Array.Empty<Guid>());

            if (!success)
            {
                Log($"[Server] SvrPutItemIntoContainer DENY — container rejected placement.");
                TgtPutItemDenied(Owner, itemInstanceId, "Container rejected the item (position conflict).");
                return;
            }

            if (isPartialPut)
            {
                // Partial put (stack split): reduce the manifest count rather than remove
                // entirely.  The player still owns the remaining portion in their local grid.
                ServerItemRecord updated = record;
                updated.StackCount -= actualCount;
                _serverManifest[itemInstanceId] = updated;

                Log($"[Server] SvrPutItemIntoContainer PARTIAL — {record.ItemDefName}  " +
                    $"id={ShortGuid(itemInstanceId)}  put={actualCount}  remaining={updated.StackCount}" +
                    $"  pos={gridPosition}");
            }
            else
            {
                // Full put: remove from this player's server manifest — item now belongs
                // to the container.
                ServerRemoveItem(itemInstanceId);

                Log($"[Server] SvrPutItemIntoContainer GRANTED — {record.ItemDefName}  " +
                    $"id={ShortGuid(itemInstanceId)}  pos={gridPosition}");
            }

            TgtPutItemGranted(Owner, itemInstanceId);
        }

        /// <summary>
        /// Server → owner: put was accepted.
        /// The drag handler already placed the item in the container grid speculatively,
        /// so nothing needs to be added.  Just clear the pending marker.
        /// </summary>
        [TargetRpc]
        private void TgtPutItemGranted(NetworkConnection conn, Guid itemInstanceId)
        {
            _pendingContainerPuts.Remove(itemInstanceId);
            Log($"[Client] TgtPutItemGranted  id={ShortGuid(itemInstanceId)} — item confirmed in container.");
        }

        /// <summary>
        /// Server → owner: put was denied (race condition, container full, etc.).
        /// The drag already moved the item speculatively — ideally we roll back, but that
        /// requires caching the original grid position before the drag (TODO Phase 6d).
        /// </summary>
        [TargetRpc]
        private void TgtPutItemDenied(NetworkConnection conn, Guid itemInstanceId, string reason)
        {
            _pendingContainerPuts.Remove(itemInstanceId);
            Log($"[Client] TgtPutItemDenied  id={ShortGuid(itemInstanceId)}  reason={reason}");

            // TODO Phase 6d: roll back — remove item from container grid, re-add to player inventory.
            Debug.LogWarning($"[NetworkedInventory] Container put denied for {ShortGuid(itemInstanceId)}: {reason}. " +
                             "Speculative place should be rolled back — implement in Phase 6d.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 6 — Nested container put (separate RPC — required Guid[] param)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Owner requests to put an item directly into a nested container (floating window).
        ///
        /// This is a SEPARATE RPC from SvrPutItemIntoContainer intentionally.
        /// FishNet's weaver corrupts optional Guid[] parameters, so containerPath
        /// must be a required parameter here rather than an optional one on the root RPC.
        ///
        /// FLOW: same as SvrPutItemIntoContainer but ServerTryPutItem walks containerPath
        /// to find the correct nested InventorySystem before placing the item.
        /// </summary>
        [ServerRpc]
        private void SvrPutItemIntoNestedContainer(
            int                  containerNetId,
            int                  compartmentIndex,
            Guid                 itemInstanceId,
            Vector2Int           gridPosition,
            GridDirection        rotation,
            Guid[]               containerPath,
            int                  stackCount,
            bool                 clientHasContainerSnapshot,
            NetContainerSnapshot clientContainerSnapshot)
        {
            if (!ServerManager.Objects.Spawned.TryGetValue(containerNetId, out FishNet.Object.NetworkObject nob))
            {
                Log($"[Server] SvrPutItemIntoNestedContainer DENY — NetworkObject {containerNetId} not found.");
                TgtPutItemDenied(Owner, itemInstanceId, "Container no longer exists.");
                return;
            }

            NetworkedWorldLootContainer netContainer = nob.GetComponent<NetworkedWorldLootContainer>();
            if (netContainer == null)
            {
                TgtPutItemDenied(Owner, itemInstanceId, "Invalid container.");
                return;
            }

            if (!ServerOwnsItem(itemInstanceId))
            {
                Log($"[Server] SvrPutItemIntoNestedContainer DENY — player does not own {ShortGuid(itemInstanceId)}.");
                TgtPutItemDenied(Owner, itemInstanceId, "Item not in your inventory.");
                return;
            }

            ServerItemRecord record = _serverManifest[itemInstanceId];

            InventoryItemSO itemDef = itemRegistry != null ? itemRegistry.GetItem(record.ItemDefName) : null;
            if (itemDef == null)
            {
                Log($"[Server] SvrPutItemIntoNestedContainer DENY — unknown item def '{record.ItemDefName}'.");
                TgtPutItemDenied(Owner, itemInstanceId, "Item definition not found on server.");
                return;
            }

            int  actualCount  = (stackCount > 0 && stackCount <= record.StackCount) ? stackCount : record.StackCount;
            bool isPartialPut = actualCount < record.StackCount;

            bool hasSnapshot = clientHasContainerSnapshot || record.HasContainerSnapshot;
            NetContainerSnapshot snapshot = clientHasContainerSnapshot ? clientContainerSnapshot : record.ContainerSnapshot;

            bool success = netContainer.ServerTryPutItem(
                itemInstanceId, itemDef, actualCount, compartmentIndex, gridPosition, rotation,
                hasSnapshot, snapshot, containerPath);

            if (!success)
            {
                Log($"[Server] SvrPutItemIntoNestedContainer DENY — container rejected placement.");
                TgtPutItemDenied(Owner, itemInstanceId, "Container rejected the item (position conflict).");
                return;
            }

            if (isPartialPut)
            {
                ServerItemRecord updated = record;
                updated.StackCount -= actualCount;
                _serverManifest[itemInstanceId] = updated;
            }
            else
            {
                ServerRemoveItem(itemInstanceId);
            }

            Log($"[Server] SvrPutItemIntoNestedContainer GRANTED — {record.ItemDefName} " +
                $"id={ShortGuid(itemInstanceId)} pathDepth={containerPath.Length} pos={gridPosition}");

            TgtPutItemGranted(Owner, itemInstanceId);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 6 — Inventory manifest sync (non-networked item registration)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Scan all owner inventory grids and register any untracked items with the server
        /// manifest so subsequent SvrPutItemIntoContainer calls pass the ownership check.
        ///
        /// Call this (on the owner client) after adding items to the player's inventory through
        /// non-networked paths — InventoryTestHarness key presses, save-data loads, etc.
        /// Already-known items (picked up via SvrPickupItem / taken from containers) are skipped.
        ///
        /// TRUST MODEL: client-reported.  Acceptable for a cooperative game.  For competitive
        /// play, move starting-loadout grants server-side so the server is always the authority.
        /// </summary>
        public void SyncInventoryWithServer()
        {
            if (!IsOwner || playerInventoryManager == null) return;

            List<NetPlacedItemData> netItems = new List<NetPlacedItemData>();

            foreach (InventoryGridVisual grid in playerInventoryManager.GetAllGrids())
            {
                if (grid?.InventorySystem == null) continue;

                foreach (PlacedItem item in grid.InventorySystem.GetAllItems())
                {
                    if (item?.ItemDefinition == null) continue;

                    PlacedItemData snapData = PlacedItemData.FromPlacedItem(item);
                    netItems.Add(InventoryNetConverter.ToNetPlacedItem(snapData));
                }
            }

            if (netItems.Count > 0)
            {
                Log($"[Client] SyncInventoryWithServer — syncing {netItems.Count} item(s) to server manifest.");
                SvrSyncInventory(netItems.ToArray());
            }
            else
            {
                Log("[Client] SyncInventoryWithServer — no items to sync.");
            }
        }

        /// <summary>
        /// Owner → Server: register items that exist locally but are not yet in the manifest.
        ///
        /// Items already tracked (InstanceId already in _serverManifest) are silently skipped
        /// so this RPC is safe to call multiple times — it only adds what is missing.
        /// </summary>
        [ServerRpc]
        public void SvrSyncInventory(NetPlacedItemData[] items)
        {
            if (items == null || items.Length == 0) return;

            int added = 0;
            foreach (NetPlacedItemData netItem in items)
            {
                // Already tracked — skip to avoid double-counting
                if (_serverManifest.ContainsKey(netItem.InstanceId)) continue;

                if (string.IsNullOrEmpty(netItem.ItemDefName)) continue;

                InventoryItemSO itemDef = itemRegistry?.GetItem(netItem.ItemDefName);
                if (itemDef == null)
                {
                    Log($"[Server] SvrSyncInventory — unknown item '{netItem.ItemDefName}', skipping.");
                    continue;
                }

                // Build a transient PlacedItem purely to reuse the existing ServerAddItem path.
                // The server never places this in a grid — manifest only.
                PlacedItem transient = new PlacedItem(
                    netItem.InstanceId, itemDef,
                    Vector2Int.zero, GridDirection.Down,
                    netItem.StackCount);

                ServerAddItem(transient);
                added++;
            }

            Log($"[Server] SvrSyncInventory — registered {added}/{items.Length} item(s) for connection {OwnerId}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Phase 6 — Container take (drag hook)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by InventoryDragHandler.OnItemMovedBetweenGrids whenever the local player
        /// successfully drags an item from one grid to another.
        ///
        /// Handles two directions:
        ///
        ///   TAKE (source = container grid, target = player grid):
        ///     Sends SvrTakeItemFromContainer so the server removes the item from
        ///     _serverCompartments, adds it to the player manifest, and broadcasts
        ///     RpcItemRemovedFromContainer.
        ///
        ///   PUT (source = player grid, target = container grid):
        ///     Sends SvrPutItemIntoContainer so the server adds the item to
        ///     _serverCompartments, removes it from the player manifest, and broadcasts
        ///     RpcItemAddedToContainer.
        ///
        /// Both calls are speculative — the drag already updated local grids, so the
        /// TargetRpc callbacks are no-ops on the happy path.
        /// </summary>
        private void HandleItemMovedBetweenGrids(
            InventoryGridVisual sourceGrid,
            InventoryGridVisual targetGrid,
            PlacedItem          movedItem)
        {
            if (!IsOwner) return;
            if (movedItem == null) return;

            ContainerInteractionManager cim = ContainerInteractionManager.Instance;

            // ── TAKE: player dragged an item OUT of the open container ────────
            if (cim != null && cim.TryGetContainerContext(
                    sourceGrid, out int takeNetId, out int takeCompartmentIndex))
            {
                // Before treating this as a TAKE, check whether the TARGET is also a grid
                // belonging to THE SAME container (cross-compartment intra-container drag).
                if (cim.TryGetContainerContext(
                        targetGrid, out int intraNetId, out int intraTargetCompartment)
                    && intraNetId == takeNetId)
                {
                    Log($"[Client] HandleItemMovedBetweenGrids INTRA-CONTAINER — " +
                        $"'{movedItem.ItemDefinition?.ItemName}' " +
                        $"comp {takeCompartmentIndex}→{intraTargetCompartment} in container {takeNetId}.");

                    SvrMoveItemInContainer(
                        takeNetId,
                        takeCompartmentIndex,
                        intraTargetCompartment,
                        movedItem.InstanceID,
                        movedItem.AnchorPosition,
                        movedItem.Rotation,
                        Array.Empty<Guid>());
                    return;
                }

                // Also check whether the TARGET is a floating window that lives INSIDE this
                // same world container.  Root compartment → floating window is a nested PUT,
                // not a TAKE to the player's inventory.
                if (FloatingContainerWindowManager.Instance != null)
                {
                    FloatingContainerWindow tgtWin =
                        FloatingContainerWindowManager.Instance.FindWindowByGrid(targetGrid);

                    Log($"[Client] HandleItemMovedBetweenGrids ROOT→NESTED check — " +
                        $"tgtWin={(tgtWin != null ? tgtWin.ContainerItem?.ItemDefinition?.name : "NULL")} " +
                        $"targetGrid={targetGrid?.name}");

                    if (tgtWin != null)
                    {
                        FloatingContainerWindow.ParentContainerContext tgtCtx = tgtWin.GetParentContext();

                        Log($"[Client] HandleItemMovedBetweenGrids ROOT→NESTED ctx — " +
                            $"IsValid={tgtCtx.IsValid} tgtNetId={tgtCtx.WorldContainerNetId} takeNetId={takeNetId}");

                        if (tgtCtx.IsValid && tgtCtx.WorldContainerNetId == takeNetId)
                        {
                            // Root compartment → nested floating window: treat as nested PUT.
                            Guid[] putPath = AppendGuid(tgtCtx.ContainerPath, tgtWin.ContainerItem.InstanceID);

                            Log($"[Client] HandleItemMovedBetweenGrids ROOT→NESTED PUT — " +
                                $"'{movedItem.ItemDefinition?.ItemName}' " +
                                $"from root comp {takeCompartmentIndex} " +
                                $"into container {tgtCtx.WorldContainerNetId} pathDepth={putPath.Length}.");

                            bool                hasContainerData  = false;
                            NetContainerSnapshot containerSnapshot = default;

                            InventoryItemSO putItemDef = movedItem.ItemDefinition as InventoryItemSO;
                            if (putItemDef != null && putItemDef.ProvidesStorage
                                && movedItem.ContainerInventory != null)
                            {
                                ContainerItemData currentData =
                                    ContainerItemData.FromInventorySystem(movedItem.ContainerInventory);
                                if (currentData != null && !currentData.IsEmpty())
                                {
                                    containerSnapshot = InventoryNetConverter.ToNetSnapshot(currentData);
                                    hasContainerData  = true;
                                }
                            }

                            // The item moves OUT of the root compartment on the server too,
                            // so first take it from the root, then put it into the nested path.
                            SvrMoveItemFromRootToNested(
                                takeNetId,
                                takeCompartmentIndex,
                                tgtCtx.CompartmentIndex,
                                putPath,
                                movedItem.InstanceID,
                                movedItem.AnchorPosition,
                                movedItem.Rotation,
                                movedItem.StackCount,
                                hasContainerData,
                                containerSnapshot);
                            return;
                        }
                    }
                }

                Log($"[Client] HandleItemMovedBetweenGrids TAKE — '{movedItem.ItemDefinition?.ItemName}' " +
                    $"from container {takeNetId} compartment {takeCompartmentIndex}.");

                _pendingContainerTakes.Add(movedItem.InstanceID);

                SvrTakeItemFromContainer(
                    takeNetId,
                    takeCompartmentIndex,
                    Array.Empty<Guid>(),
                    movedItem.InstanceID,
                    movedItem.StackCount);
                return;
            }

            // ── FLOATING → FLOATING: item dragged between two nested containers ──
            // MUST be checked BEFORE the NESTED TAKE block below.
            // When both grids are floating windows inside the SAME world container,
            // this is an intra-nested move — not a take to player inventory.
            // Sending NESTED TAKE here would remove the item from the source on the server
            // but never add it to the target, leaving the server state corrupt.
            if (FloatingContainerWindowManager.Instance != null)
            {
                FloatingContainerWindow srcWin =
                    FloatingContainerWindowManager.Instance.FindWindowByGrid(sourceGrid);
                FloatingContainerWindow tgtWin =
                    FloatingContainerWindowManager.Instance.FindWindowByGrid(targetGrid);

                if (srcWin != null && tgtWin != null)
                {
                    FloatingContainerWindow.ParentContainerContext srcCtx = srcWin.GetParentContext();
                    FloatingContainerWindow.ParentContainerContext tgtCtx = tgtWin.GetParentContext();

                    if (srcCtx.IsValid && tgtCtx.IsValid &&
                        srcCtx.WorldContainerNetId == tgtCtx.WorldContainerNetId &&
                        srcCtx.CompartmentIndex    == tgtCtx.CompartmentIndex)
                    {
                        Guid[] srcPath = AppendGuid(srcCtx.ContainerPath, srcWin.ContainerItem.InstanceID);
                        Guid[] tgtPath = AppendGuid(tgtCtx.ContainerPath, tgtWin.ContainerItem.InstanceID);

                        Log($"[Client] HandleItemMovedBetweenGrids FLOATING→FLOATING — " +
                            $"'{movedItem.ItemDefinition?.ItemName}' " +
                            $"container={srcCtx.WorldContainerNetId} comp={srcCtx.CompartmentIndex} " +
                            $"srcPathDepth={srcPath.Length} tgtPathDepth={tgtPath.Length}.");

                        SvrMoveItemBetweenNestedContainers(
                            srcCtx.WorldContainerNetId,
                            srcCtx.CompartmentIndex,
                            srcPath,
                            tgtPath,
                            movedItem.InstanceID,
                            movedItem.AnchorPosition,
                            movedItem.Rotation,
                            movedItem.StackCount);
                        return;
                    }
                }
            }

            // ── TAKE from floating window (nested container) ──────────────────
            // Handles dragging an item from a nested container (floating window) to any
            // other grid (typically player inventory).  The server already supports paths
            // in SvrTakeItemFromContainer so we just need to build the correct path here.
            if (FloatingContainerWindowManager.Instance != null)
            {
                FloatingContainerWindow sourceWindow =
                    FloatingContainerWindowManager.Instance.FindWindowByGrid(sourceGrid);

                if (sourceWindow != null)
                {
                    FloatingContainerWindow.ParentContainerContext ctx = sourceWindow.GetParentContext();
                    if (ctx.IsValid)
                    {
                        // Full path = ancestor chain + this window's own container ID.
                        Guid[] takePath = AppendGuid(ctx.ContainerPath, sourceWindow.ContainerItem.InstanceID);

                        Log($"[Client] HandleItemMovedBetweenGrids TAKE (nested) — " +
                            $"'{movedItem.ItemDefinition?.ItemName}' " +
                            $"from container {ctx.WorldContainerNetId} compartment {ctx.CompartmentIndex} " +
                            $"pathDepth={takePath.Length}.");

                        _pendingContainerTakes.Add(movedItem.InstanceID);

                        SvrTakeItemFromContainer(
                            ctx.WorldContainerNetId,
                            ctx.CompartmentIndex,
                            takePath,
                            movedItem.InstanceID,
                            movedItem.StackCount);
                        return;
                    }
                }
            }

            // ── PUT: player dragged an item INTO the open root container ──────
            if (cim != null && cim.TryGetContainerContext(
                    targetGrid, out int putNetId, out int putCompartmentIndex))
            {
                Log($"[Client] HandleItemMovedBetweenGrids PUT — '{movedItem.ItemDefinition?.ItemName}' " +
                    $"into container {putNetId} compartment {putCompartmentIndex} at {movedItem.AnchorPosition}.");

                _pendingContainerPuts.Add(movedItem.InstanceID);

                // Snapshot the CURRENT state of the item's nested inventory (if any) so the
                // server receives the live contents rather than the potentially stale copy
                // cached in the server manifest.
                bool                hasContainerData  = false;
                NetContainerSnapshot containerSnapshot = default;

                InventoryItemSO putItemDef = movedItem.ItemDefinition as InventoryItemSO;
                if (putItemDef != null && putItemDef.ProvidesStorage && movedItem.ContainerInventory != null)
                {
                    ContainerItemData currentData = ContainerItemData.FromInventorySystem(movedItem.ContainerInventory);
                    if (currentData != null && !currentData.IsEmpty())
                    {
                        containerSnapshot = InventoryNetConverter.ToNetSnapshot(currentData);
                        hasContainerData  = true;

                        Log($"[Client] HandleItemMovedBetweenGrids PUT — snapshotting " +
                            $"{currentData.GetTotalItemCount()} item(s) inside '{putItemDef.ItemName}'.");
                    }
                }

                SvrPutItemIntoContainer(
                    putNetId,
                    putCompartmentIndex,
                    movedItem.InstanceID,
                    movedItem.AnchorPosition,
                    movedItem.Rotation,
                    movedItem.StackCount,
                    hasContainerData,
                    containerSnapshot);
                return;
            }

            // ── NESTED PUT: player dragged an item INTO a floating window ─────
            // Handles dragging from player inventory (or another non-container grid)
            // directly into a nested container that is open as a floating window.
            // Without this block the item ends up only in the client's local floating-
            // window InventorySystem; the server never learns about the move, so the
            // change is lost when the container is closed and reopened.
            if (FloatingContainerWindowManager.Instance != null)
            {
                FloatingContainerWindow targetWindow =
                    FloatingContainerWindowManager.Instance.FindWindowByGrid(targetGrid);

                if (targetWindow != null)
                {
                    FloatingContainerWindow.ParentContainerContext ctx = targetWindow.GetParentContext();
                    if (ctx.IsValid)
                    {
                        // Full path = ancestors stored in the window + this window's container ID.
                        Guid[] putPath = AppendGuid(ctx.ContainerPath, targetWindow.ContainerItem.InstanceID);

                        // Snapshot the item's container inventory if it is itself a container
                        // (e.g. the player is dropping a pouch into a backpack).
                        bool                hasContainerData  = false;
                        NetContainerSnapshot containerSnapshot = default;

                        InventoryItemSO putItemDef = movedItem.ItemDefinition as InventoryItemSO;
                        if (putItemDef != null && putItemDef.ProvidesStorage &&
                            movedItem.ContainerInventory != null)
                        {
                            ContainerItemData currentData =
                                ContainerItemData.FromInventorySystem(movedItem.ContainerInventory);
                            if (currentData != null && !currentData.IsEmpty())
                            {
                                containerSnapshot = InventoryNetConverter.ToNetSnapshot(currentData);
                                hasContainerData  = true;

                                Log($"[Client] HandleItemMovedBetweenGrids NESTED PUT — snapshotting " +
                                    $"{currentData.GetTotalItemCount()} item(s) inside '{putItemDef?.ItemName}'.");
                            }
                        }

                        Log($"[Client] HandleItemMovedBetweenGrids NESTED PUT — " +
                            $"'{movedItem.ItemDefinition?.ItemName}' " +
                            $"into container {ctx.WorldContainerNetId} compartment {ctx.CompartmentIndex} " +
                            $"pathDepth={putPath.Length} at {movedItem.AnchorPosition}.");

                        _pendingContainerPuts.Add(movedItem.InstanceID);

                        SvrPutItemIntoNestedContainer(
                            ctx.WorldContainerNetId,
                            ctx.CompartmentIndex,
                            movedItem.InstanceID,
                            movedItem.AnchorPosition,
                            movedItem.Rotation,
                            putPath,
                            movedItem.StackCount,
                            hasContainerData,
                            containerSnapshot);
                    }
                }
            }
        }

        /// <summary>
        /// Called by InventoryDragHandler.OnItemRepositionedInGrid whenever the local
        /// player successfully drops an item onto a new cell within the SAME grid.
        ///
        /// Handles two cases:
        ///   ROOT CONTAINER GRID  — grid is one of the root compartment grids managed by
        ///     ContainerInteractionManager.  Path is empty (item is directly in the compartment).
        ///
        ///   FLOATING WINDOW GRID — grid belongs to a nested floating container window.
        ///     Path = the window's ancestor chain + the window's own container InstanceID.
        ///     Server walks this path to find the correct InventorySystem before moving.
        ///
        /// Player-inventory grids are ignored; the server manifest is position-agnostic.
        /// </summary>
        private void HandleItemRepositionedInGrid(
            InventoryGridVisual grid,
            PlacedItem          movedItem)
        {
            if (!IsOwner) return;
            if (movedItem == null) return;

            int    containerNetId    = -1;
            int    compartmentIndex  = -1;
            Guid[] containerPath     = null;

            // ── Case A: root compartment grid ─────────────────────────────────
            if (ContainerInteractionManager.Instance != null &&
                ContainerInteractionManager.Instance.TryGetContainerContext(
                    grid, out containerNetId, out compartmentIndex))
            {
                containerPath = Array.Empty<Guid>();
                Log($"[Client] HandleItemRepositionedInGrid — Case A ROOT grid detected, container={containerNetId} comp={compartmentIndex}.");
            }
            // ── Case B: floating window grid (nested container) ───────────────
            else if (FloatingContainerWindowManager.Instance != null)
            {
                FloatingContainerWindow window =
                    FloatingContainerWindowManager.Instance.FindWindowByGrid(grid);

                Log($"[Client] HandleItemRepositionedInGrid — Case B FLOATING: window={(window != null ? window.ContainerItem?.ItemDefinition?.name : "NULL")} grid={grid?.name}.");

                if (window != null)
                {
                    FloatingContainerWindow.ParentContainerContext ctx = window.GetParentContext();
                    Log($"[Client] HandleItemRepositionedInGrid — ctx.IsValid={ctx.IsValid} NetId={ctx.WorldContainerNetId} comp={ctx.CompartmentIndex} ancestorDepth={ctx.ContainerPath?.Length ?? -1}.");
                    if (ctx.IsValid)
                    {
                        containerNetId   = ctx.WorldContainerNetId;
                        compartmentIndex = ctx.CompartmentIndex;
                        // Full path = ancestors stored in window + this window's own container ID.
                        containerPath = AppendGuid(ctx.ContainerPath, window.ContainerItem.InstanceID);
                    }
                }
            }

            if (containerNetId < 0)
            {
                Log($"[Client] HandleItemRepositionedInGrid — player-inventory grid, skipping sync.");
                return;
            }

            Log($"[Client] HandleItemRepositionedInGrid — '{movedItem.ItemDefinition?.ItemName}' " +
                $"moved to {movedItem.AnchorPosition} rot={movedItem.Rotation} " +
                $"in container {containerNetId} compartment {compartmentIndex} " +
                $"pathDepth={containerPath?.Length ?? 0}.");

            SvrMoveItemInContainer(
                containerNetId,
                compartmentIndex,   // source = same compartment
                compartmentIndex,   // target = same compartment
                movedItem.InstanceID,
                movedItem.AnchorPosition,
                movedItem.Rotation,
                containerPath);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API  (called by other systems, e.g. InventoryUIController)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a read-only copy of the server manifest item names for debugging.
        /// Only meaningful when called on the server.
        /// </summary>
        [Server]
        public IReadOnlyDictionary<Guid, ServerItemRecord> GetServerManifest()
            => _serverManifest;

        /// <summary>
        /// Mark an item as pending (speculative add on owner client).
        /// Returns false if already pending.
        /// </summary>
        public bool MarkPendingPickup(Guid instanceId)
        {
            if (_pendingPickups.Contains(instanceId)) return false;
            _pendingPickups.Add(instanceId);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Client-side inventory mutation helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Walk every grid the owner has and remove the item with this InstanceID.
        /// Used for rollback (TgtPickupDenied) and ID-mismatch reconciliation.
        /// </summary>
        private void RemoveSpeculativeItem(Guid instanceId)
        {
            if (playerInventoryManager == null) return;

            foreach (InventoryGridVisual grid in playerInventoryManager.GetAllGrids())
            {
                if (grid == null || grid.InventorySystem == null) continue;

                if (grid.InventorySystem.RemoveItem(instanceId))
                {
                    grid.RefreshAllItemVisuals();
                    Log($"[Client] Removed speculative item {ShortGuid(instanceId)} from grid.");
                    return;
                }
            }

            Log($"[Client] RemoveSpeculativeItem — id {ShortGuid(instanceId)} not found in any grid (may not have been placed yet).");
        }

        /// <summary>
        /// The speculative item's ID matched the server-confirmed ID — nothing to change.
        /// Just log the confirmation so future debugging is clear.
        /// Called in TgtPickupGranted on the happy path (no ID mismatch).
        /// </summary>
        private void ConfirmSpeculativeItem(Guid confirmedInstanceId)
        {
            // In the non-mismatch path the item is already sitting in the grid with the
            // correct InstanceID.  Nothing needs to move.  If we ever add a "pending" visual
            // tint (greyed-out icon while waiting for server ACK) this is where we'd clear it.
            Log($"[Client] Confirmed speculative item {ShortGuid(confirmedInstanceId)} — already in grid.");
        }

        /// <summary>
        /// Add an item to the owner's inventory grid after a server-granted pickup.
        /// Called only when the speculative InstanceID differed from the confirmed one
        /// (i.e. the item was NOT already added speculatively before the RPC arrived).
        ///
        /// Mirrors the placement logic in PlayerItemInteraction.TryAddToAnyInventoryGrid
        /// but uses the server-confirmed InstanceID.
        /// </summary>
        private void ClientAddItemToInventory(
            InventoryItemSO   itemDef,
            int               quantity,
            Guid              confirmedInstanceId,
            ItemInstance      itemInstance,
            ContainerItemData containerData)
        {
            if (playerInventoryManager == null || itemDef == null) return;

            // Prefer merging into an existing stack
            if (itemDef.IsStackable)
            {
                foreach (InventoryGridVisual grid in playerInventoryManager.GetAllGrids())
                {
                    if (grid == null || grid.InventorySystem == null) continue;

                    foreach (PlacedItem existing in grid.InventorySystem.GetAllItems())
                    {
                        if (existing.ItemDefinition != itemDef) continue;
                        if (existing.StackCount >= itemDef.MaxStackSize) continue;

                        if (itemInstance != null && itemDef.TrackIndividualItems)
                            existing.AddInstances(new List<ItemInstance> { itemInstance });
                        else
                            existing.AddToStack(quantity);

                        grid.RefreshAllItemVisuals();
                        Log($"[Client] Merged confirmed item into existing stack.");
                        return;
                    }
                }
            }

            // No existing stack — find a free cell
            GridDirection[] rotations = itemDef.CanRotate
                ? new[] { GridDirection.Down, GridDirection.Right, GridDirection.Up, GridDirection.Left }
                : new[] { GridDirection.Down };

            foreach (InventoryGridVisual grid in playerInventoryManager.GetAllGrids())
            {
                if (grid == null || grid.InventorySystem == null) continue;

                foreach (GridDirection rotation in rotations)
                {
                    Vector2Int? pos = FindFirstAvailablePosition(grid, itemDef, rotation);
                    if (!pos.HasValue) continue;

                    int stackCountToCreate = (itemInstance != null && itemDef.TrackIndividualItems) ? 0 : quantity;

                    // Use the Guid-accepting overload so the server-confirmed InstanceID is
                    // stamped directly onto the new PlacedItem — no remove-and-re-add needed.
                    bool ok = grid.InventorySystem.TryAddItem(
                        confirmedInstanceId, itemDef, pos.Value, rotation,
                        out PlacedItem placed,
                        stackCountToCreate);

                    if (ok && placed != null)
                    {
                        if (itemInstance != null && itemDef.TrackIndividualItems)
                            placed.AddInstances(new List<ItemInstance> { itemInstance });

                        if (containerData != null && itemDef.ProvidesStorage)
                        {
                            InventorySystem containerInv = new InventorySystem(
                                itemDef.StorageGridSize.x, itemDef.StorageGridSize.y,
                                64f, Vector3.zero, itemDef.StorageMaxWeight);
                            containerData.LoadIntoInventorySystem(containerInv, 0);
                            placed.ContainerInventory = containerInv;
                        }

                        Log($"[Client] Added confirmed item {itemDef.ItemName} at {pos.Value} (id={ShortGuid(confirmedInstanceId)}).");
                        grid.RefreshAllItemVisuals();
                        return;
                    }
                }
            }

            Debug.LogWarning($"[NetworkedInventory] ClientAddItemToInventory — no space found for {itemDef.ItemName}!");
        }

        /// <summary>
        /// Find the first grid cell that can accept this item at the given rotation.
        /// Mirrors PlayerItemInteraction.FindFirstAvailablePosition.
        /// </summary>
        private static Vector2Int? FindFirstAvailablePosition(
            InventoryGridVisual grid,
            InventoryItemSO     item,
            GridDirection       rotation)
        {
            int width      = grid.InventorySystem.Width;
            int height     = grid.InventorySystem.Height;
            int itemWidth  = item.GetRotatedWidth(rotation);
            int itemHeight = item.GetRotatedHeight(rotation);

            for (int y = 0; y <= height - itemHeight; y++)
                for (int x = 0; x <= width - itemWidth; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (grid.InventorySystem.CanAddItem(item, pos, rotation))
                        return pos;
                }

            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Nested container reconciliation (close-time persistence snapshot)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Owner → Server: persist the current contents of a nested container by walking
        /// <paramref name="containerPath"/> from the root compartment to the target
        /// InventorySystem and replacing it with the provided snapshot.
        ///
        /// Called by FloatingContainerWindow.Close() as a reconciliation step.
        /// Real-time RPCs (SvrMoveItemInContainer, SvrPutItemIntoNestedContainer, etc.)
        /// keep the server authoritative during a session.  This snapshot acts as a
        /// safety net: it ensures that any operation not covered by a real-time RPC
        /// (e.g. a same-grid reposition that the event missed, or a merge) is still
        /// persisted when the window closes.
        ///
        /// <paramref name="containerPath"/> is the FULL path including the container
        /// being closed (ancestors + this container's own InstanceID), so the server
        /// can walk to the exact InventorySystem regardless of nesting depth.
        /// </summary>
        [ServerRpc]
        public void SvrSyncNestedContainer(
            int                  worldContainerNetId,
            int                  compartmentIndex,
            Guid[]               containerPath,
            bool                 hasSnapshot,
            NetContainerSnapshot snapshot)
        {
            if (!ServerManager.Objects.Spawned.TryGetValue(
                    worldContainerNetId, out FishNet.Object.NetworkObject nob))
            {
                Log($"[Server] SvrSyncNestedContainer DENY — NetworkObject {worldContainerNetId} not found.");
                return;
            }

            NetworkedWorldLootContainer netContainer = nob.GetComponent<NetworkedWorldLootContainer>();
            if (netContainer == null)
            {
                Log("[Server] SvrSyncNestedContainer DENY — no NetworkedWorldLootContainer.");
                return;
            }

            if (containerPath == null || containerPath.Length == 0)
            {
                Log("[Server] SvrSyncNestedContainer DENY — containerPath is empty.");
                return;
            }

            InventorySystem compartment = netContainer.GetServerCompartment(compartmentIndex);
            if (compartment == null)
            {
                Log($"[Server] SvrSyncNestedContainer DENY — invalid compartment {compartmentIndex}.");
                return;
            }

            // Walk every ID in containerPath except the last — those are ancestors.
            // The last ID in the path is the container whose InventorySystem we replace.
            InventorySystem parentInv = compartment;
            for (int i = 0; i < containerPath.Length - 1; i++)
            {
                PlacedItem ancestor = parentInv.GetItemByID(containerPath[i]);
                if (ancestor?.ContainerInventory == null)
                {
                    Log($"[Server] SvrSyncNestedContainer DENY — " +
                        $"path walk failed at depth {i} id={ShortGuid(containerPath[i])}.");
                    return;
                }
                parentInv = ancestor.ContainerInventory;
            }

            Guid targetId = containerPath[containerPath.Length - 1];
            PlacedItem targetItem = parentInv.GetItemByID(targetId);
            if (targetItem == null)
            {
                Log($"[Server] SvrSyncNestedContainer DENY — " +
                    $"target item {ShortGuid(targetId)} not found at path leaf.");
                return;
            }

            InventoryItemSO itemDef = targetItem.ItemDefinition as InventoryItemSO;
            if (itemDef == null || !itemDef.ProvidesStorage)
            {
                Log($"[Server] SvrSyncNestedContainer DENY — " +
                    $"item {ShortGuid(targetId)} does not provide storage.");
                return;
            }

            if (hasSnapshot)
            {
                if (itemRegistry == null)
                    itemRegistry = Resources.Load<InventoryItemRegistry>("InventoryItemRegistry");

                ContainerItemData data = InventoryNetConverter.FromNetSnapshot(snapshot, itemRegistry);
                if (data != null)
                {
                    InventorySystem containerInv = new InventorySystem(
                        itemDef.StorageGridSize.x,
                        itemDef.StorageGridSize.y,
                        64f, Vector3.zero,
                        itemDef.StorageMaxWeight);
                    data.LoadIntoInventorySystem(containerInv, compartmentIndex: 0);
                    targetItem.ContainerInventory = containerInv;

                    Log($"[Server] SvrSyncNestedContainer — '{itemDef.ItemName}' " +
                        $"id={ShortGuid(targetId)} pathDepth={containerPath.Length} " +
                        $"items={data.GetTotalItemCount()} " +
                        $"container={worldContainerNetId} comp={compartmentIndex}.");
                }
            }
            else
            {
                targetItem.ContainerInventory = null;
                Log($"[Server] SvrSyncNestedContainer — '{itemDef.ItemName}' " +
                    $"id={ShortGuid(targetId)} cleared (empty snapshot).");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Logging / utility
        // ─────────────────────────────────────────────────────────────────────

        private void Log(string msg)
        {
            if (debugMode)
                Debug.Log($"[NetworkedInventory] {msg}");
        }

        private static string ShortGuid(Guid g) => g.ToString("N").Substring(0, 8);

        /// <summary>
        /// Return a new Guid[] equal to <paramref name="existing"/> with <paramref name="id"/> appended.
        /// Used to build container paths for nested-container RPCs.
        /// </summary>
        private static Guid[] AppendGuid(Guid[] existing, Guid id)
        {
            if (existing == null || existing.Length == 0)
                return new Guid[] { id };

            Guid[] result = new Guid[existing.Length + 1];
            Array.Copy(existing, result, existing.Length);
            result[existing.Length] = id;
            return result;
        }
    }
}
