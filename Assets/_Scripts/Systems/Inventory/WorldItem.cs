using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Represents an item in the 3D game world that can be picked up.
    ///
    /// Design Notes:
    /// - Uses Destroy() for quick prototyping
    /// - TODO: Switch to object pooling for better performance at scale
    /// - TODO: Add FishNet NetworkBehaviour when implementing multiplayer
    /// - Currently uses MonoBehaviour for local-only gameplay
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WorldItem : MonoBehaviour
    {
        [Header("Item Data")]
        [Tooltip("The inventory item definition this represents")]
        [SerializeField] private InventoryItemSO itemDefinition;

        [Tooltip("How many of this item (for stackable items)")]
        [SerializeField] private int quantity = 1;

        [Header("Visual")]
        [Tooltip("Optional: Override the mesh renderer (auto-finds if not set)")]
        [SerializeField] private MeshRenderer meshRenderer;

        [Header("Physics")]
        [Tooltip("Should this item use physics when dropped?")]
        [SerializeField] private bool usePhysics = true;

        [Tooltip("Rigidbody for physics (auto-added if usePhysics is true)")]
        [SerializeField] private Rigidbody rb;

        [Header("Interaction")]
        [Tooltip("Interaction range in meters")]
        [SerializeField] private float interactionRange = 3f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // Cached references
        private Collider itemCollider;

        // Item instance data (for tracked items with durability/uses)
        private ItemInstance itemInstance;

        // Container data (for items that provide storage - backpacks, chests, etc.)
        private ContainerItemData containerData;

        #region Properties

        public InventoryItemSO ItemDefinition => itemDefinition;
        public int Quantity => quantity;
        public float InteractionRange => interactionRange;
        public ItemInstance ItemInstance => itemInstance;
        public ContainerItemData ContainerData => containerData;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Auto-find components
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }

            itemCollider = GetComponent<Collider>();

            // Setup physics
            if (usePhysics)
            {
                if (rb == null)
                {
                    rb = GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = gameObject.AddComponent<Rigidbody>();
                    }
                }
            }
        }

        private void OnValidate()
        {
            // Clamp quantity
            if (itemDefinition != null && itemDefinition.IsStackable)
            {
                quantity = Mathf.Clamp(quantity, 1, itemDefinition.MaxStackSize);
            }
            else
            {
                quantity = 1; // Non-stackable items are always quantity 1
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize this world item with data.
        /// Call this after spawning via Instantiate or object pool.
        /// </summary>
        public void Initialize(InventoryItemSO item, int qty = 1, ItemInstance instance = null, ContainerItemData container = null)
        {
            itemDefinition = item;
            quantity = qty;
            itemInstance = instance;
            containerData = container;

            if (debugMode)
            {
                Debug.Log($"[WorldItem] Initialized: {item.ItemName} x{qty}");
                if (containerData != null)
                {
                    Debug.Log($"  Container has {containerData.GetTotalItemCount()} items inside");
                }
            }
        }

        /// <summary>
        /// Create a WorldItem in the world at a specific position.
        /// Static factory method for easy spawning.
        /// </summary>
        public static WorldItem Spawn(InventoryItemSO itemDef, Vector3 position, Quaternion rotation, int qty = 1, ItemInstance instance = null, Scene? targetScene = null, ContainerItemData containerData = null)
        {
            if (itemDef == null || itemDef.WorldItemPrefab == null)
            {
                Debug.LogError("[WorldItem] Cannot spawn - missing item definition or prefab!");
                return null;
            }

            // TODO: Switch to object pooling instead of Instantiate for better performance
            GameObject obj = Instantiate(itemDef.WorldItemPrefab, position, rotation);

            // Move to target scene if specified (important for multi-scene physics)
            if (targetScene.HasValue && targetScene.Value.IsValid())
            {
                SceneManager.MoveGameObjectToScene(obj, targetScene.Value);
                // Restore position after scene move (MoveGameObjectToScene can change position)
                obj.transform.position = position;
                obj.transform.rotation = rotation;
            }

            WorldItem worldItem = obj.GetComponent<WorldItem>();

            if (worldItem == null)
            {
                Debug.LogError($"[WorldItem] Prefab for {itemDef.ItemName} is missing WorldItem component!");
                Destroy(obj);
                return null;
            }

            worldItem.Initialize(itemDef, qty, instance, containerData);
            return worldItem;
        }

        #endregion

        #region Pickup/Destroy

        /// <summary>
        /// Called when item is picked up by player.
        /// Returns the item data for adding to inventory.
        /// </summary>
        public void OnPickedUp()
        {
            if (debugMode)
            {
                Debug.Log($"[WorldItem] Picked up: {itemDefinition.ItemName} x{quantity}");
            }

            // TODO: Return to object pool instead of destroying
            // For now, just destroy for quick prototyping
            Destroy(gameObject);
        }

        /// <summary>
        /// Split off a quantity from this stack and return a new WorldItem.
        /// Used for "Drop One" functionality.
        /// </summary>
        public WorldItem SplitStack(int amountToSplit)
        {
            if (!itemDefinition.IsStackable)
            {
                Debug.LogWarning("[WorldItem] Cannot split non-stackable item!");
                return null;
            }

            if (amountToSplit >= quantity)
            {
                Debug.LogWarning("[WorldItem] Cannot split entire stack - use pickup instead!");
                return null;
            }

            // Reduce this item's quantity
            quantity -= amountToSplit;

            // Create new WorldItem with split amount (shares same instance reference)
            Vector3 spawnPos = transform.position + transform.right * 0.5f; // Offset to the side
            WorldItem splitItem = Spawn(itemDefinition, spawnPos, transform.rotation, amountToSplit, itemInstance);

            if (debugMode)
            {
                Debug.Log($"[WorldItem] Split stack: {amountToSplit} from {quantity + amountToSplit} total");
            }

            return splitItem;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            // Draw interaction range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }

        #endregion
    }
}
