using UnityEngine;
using TimeGame.Systems.GridPlacement;
using TimeGame.Systems.Inventory.UI;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Handles consuming items and executing their effects.
    /// Removes depleted items from inventory.
    /// </summary>
    public class ItemUsageHandler : MonoBehaviour
    {
        [Header("Drop Settings")]
        [Tooltip("Player transform to drop items in front of")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Distance in front of player to spawn dropped items")]
        [SerializeField] private float dropDistance = 1.5f;

        [Tooltip("Height offset above ground to spawn dropped items")]
        [SerializeField] private float dropHeightAboveGround = 0.5f;

        [Tooltip("Layer mask for ground detection")]
        [SerializeField] private LayerMask groundLayerMask = -1;

        [Tooltip("Maximum raycast distance to find ground")]
        [SerializeField] private float maxGroundCheckDistance = 100f;

        [Tooltip("ScenePhysics component for scene-aware raycasting")]
        [SerializeField] private ScenePhysics scenePhysics;
        private void Start()
        {
            // Auto-find player transform if not set
            if (playerTransform == null)
            {
                // Try to find the player character (look for FirstPersonController or similar)
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                    Debug.Log($"[ItemUsageHandler] Auto-found player transform: {player.name}");
                }
                else
                {
                    playerTransform = transform;
                    Debug.LogError("[ItemUsageHandler] Could not find player! Using self transform. PLEASE SET PLAYER TRANSFORM IN INSPECTOR!");
                }
            }

            // Sanity check - if player position looks like screen coords, warn
            if (Mathf.Abs(playerTransform.position.x) > 500 || Mathf.Abs(playerTransform.position.y) > 500)
            {
                Debug.LogError($"[ItemUsageHandler] WARNING! Player transform position looks wrong: {playerTransform.position}. This looks like screen coordinates, not world coordinates! Check the Player Transform field in inspector.");
            }

            // Auto-find ScenePhysics if not set
            if (scenePhysics == null)
            {
                scenePhysics = GetComponent<ScenePhysics>();
                if (scenePhysics == null)
                {
                    // Find ScenePhysics from the present timeline scene specifically
                    ScenePhysics[] allScenePhysics = FindObjectsOfType<ScenePhysics>();
                    foreach (ScenePhysics sp in allScenePhysics)
                    {
                        // Look for the Present timeline scene (where ground is)
                        string sceneName = sp.gameObject.scene.name.ToLower();
                        if (sceneName.Contains("present") || sceneName.Contains("timeline_present"))
                        {
                            scenePhysics = sp;
                            Debug.Log($"[ItemUsageHandler] Found ScenePhysics in Present scene: {sp.gameObject.scene.name}");
                            break;
                        }
                    }

                    // If still not found, try finding any PhysicsSceneSync (fallback)
                    if (scenePhysics == null)
                    {
                        foreach (ScenePhysics sp in allScenePhysics)
                        {
                            PhysicsSceneSync sync = sp.GetComponent<PhysicsSceneSync>();
                            if (sync != null)
                            {
                                scenePhysics = sp;
                                Debug.Log($"[ItemUsageHandler] Found ScenePhysics via PhysicsSceneSync in scene: {sp.gameObject.scene.name}");
                                break;
                            }
                        }
                    }
                }

                if (scenePhysics == null)
                {
                    Debug.LogWarning("[ItemUsageHandler] ScenePhysics not found! Raycasts will use default physics scene.");
                }
                else
                {
                    Debug.Log($"[ItemUsageHandler] Using ScenePhysics from scene: {scenePhysics.gameObject.scene.name}");
                }
            }

            // Subscribe to context menu use event
            InventoryContextMenu contextMenu = FindObjectOfType<InventoryContextMenu>();
            if (contextMenu != null)
            {
                contextMenu.OnUseItem += HandleUseItem;
                contextMenu.OnInspectItem += HandleInspectItem;
                contextMenu.OnDropItem += HandleDropAllItems;
                contextMenu.OnDropOneItem += HandleDropOneItem;
                Debug.Log("[ItemUsageHandler] Subscribed to context menu events");
            }
            else
            {
                Debug.LogWarning("[ItemUsageHandler] Could not find InventoryContextMenu in scene!");
            }
        }

        private void HandleUseItem(PlacedItem item, InventoryGridVisual grid)
        {
            if (item == null || grid == null) return;

            // Tracked items only
            if (!item.IsInstanceTracked || item.ItemInstances == null || item.ItemInstances.Count == 0)
            {
                Debug.LogWarning("[ItemUsageHandler] Cannot use non-tracked item!");
                return;
            }

            // Get first item instance (FIFO - first in, first out)
            ItemInstance instance = item.ItemInstances[0];

            // Use the item
            bool depleted = instance.UseItem();

            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            Debug.Log($"[ItemUsageHandler] Used {itemDef.ItemName}. Uses remaining: {instance.UsesRemaining}");

            // Execute item effect (bandages heal, etc.)
            ExecuteItemEffect(itemDef);

            // If depleted, remove from stack
            if (depleted)
            {
                Debug.Log($"[ItemUsageHandler] Item depleted! Removing from stack.");

                // Remove the depleted instance
                item.ItemInstances.RemoveAt(0);

                // If stack is now empty, remove entire item
                if (item.ItemInstances.Count == 0)
                {
                    grid.InventorySystem.RemoveItem(item.InstanceID);
                    Debug.Log($"[ItemUsageHandler] Removed depleted item from inventory.");
                }
                else
                {
                    // Still items in stack - just refresh visual
                    grid.RefreshAllItemVisuals();
                    Debug.Log($"[ItemUsageHandler] Stack still has {item.ItemInstances.Count} items remaining.");
                }
            }
            else
            {
                // Item still has uses - refresh visual to update uses counter
                grid.RefreshAllItemVisuals();
            }
        }

        private void HandleInspectItem(PlacedItem item, InventoryGridVisual grid)
        {
            if (item == null) return;

            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            if (itemDef == null) return;

            Debug.Log($"[ItemUsageHandler] INSPECT: {itemDef.ItemName}");
            Debug.Log($"  Description: {itemDef.Description}");
            Debug.Log($"  Size: {itemDef.Width}x{itemDef.Height}");
            Debug.Log($"  Weight: {itemDef.Weight}kg");
            Debug.Log($"  Value: {itemDef.Value}");
            Debug.Log($"  Category: {itemDef.Category}");
            Debug.Log($"  Rarity: {itemDef.Rarity}");
            Debug.Log($"  Stack Count: {item.StackCount}");

            if (item.IsInstanceTracked && item.ItemInstances != null && item.ItemInstances.Count > 0)
            {
                ItemInstance firstItem = item.ItemInstances[0];
                Debug.Log($"  Uses: {firstItem.UsesRemaining}/{itemDef.MaxUses}");
                Debug.Log($"  Durability: {firstItem.Durability}%");
                Debug.Log($"  Condition: {firstItem.Condition:F2}");
            }

            // TODO: Show actual inspect UI panel
        }

        private void HandleDropOneItem(PlacedItem item, InventoryGridVisual grid)
        {
            if (item == null || grid == null) return;

            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            if (itemDef == null)
            {
                Debug.LogWarning("[ItemUsageHandler] Item definition is null!");
                return;
            }

            // Drop just one item from the stack
            DropItems(item, grid, itemDef, 1);
        }

        private void HandleDropAllItems(PlacedItem item, InventoryGridVisual grid)
        {
            if (item == null || grid == null) return;

            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            if (itemDef == null)
            {
                Debug.LogWarning("[ItemUsageHandler] Item definition is null!");
                return;
            }

            // Drop all items in the stack
            DropItems(item, grid, itemDef, item.StackCount);
        }

        private void DropItems(PlacedItem item, InventoryGridVisual grid, InventoryItemSO itemDef, int quantityToDrop)
        {
            // Calculate horizontal position in front of player
            Vector3 horizontalPosition = playerTransform.position + playerTransform.forward * dropDistance;

            // Raycast down from high above to find ground
            Vector3 rayStart = horizontalPosition + Vector3.up * maxGroundCheckDistance;
            Vector3 dropPosition;

            bool hitGround = false;
            RaycastHit hit;

            // Use ScenePhysics if available, otherwise fall back to regular Physics
            if (scenePhysics != null)
            {
                hitGround = scenePhysics.Raycast(rayStart, Vector3.down, out hit, maxGroundCheckDistance * 2f, groundLayerMask, QueryTriggerInteraction.Ignore);
            }
            else
            {
                hitGround = Physics.Raycast(rayStart, Vector3.down, out hit, maxGroundCheckDistance * 2f, groundLayerMask, QueryTriggerInteraction.Ignore);
            }

            if (hitGround)
            {
                // Found ground, spawn slightly above it
                dropPosition = hit.point + Vector3.up * dropHeightAboveGround;
            }
            else
            {
                // No ground found, use player's height as fallback
                dropPosition = horizontalPosition;
                Debug.LogWarning($"[ItemUsageHandler] No ground found for drop! Spawning at player height.");
            }

            // Spawn individual items (not stacked)
            // Each world item represents a single item, even if dropping multiple
            UnityEngine.SceneManagement.Scene? targetScene = scenePhysics != null ? scenePhysics.gameObject.scene : (UnityEngine.SceneManagement.Scene?)null;

            int itemsToSpawn = Mathf.Min(quantityToDrop, item.StackCount);
            int itemsSpawned = 0;

            for (int i = 0; i < itemsToSpawn; i++)
            {
                // Offset each item slightly in a circle pattern to prevent stacking
                float angle = (360f / itemsToSpawn) * i;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * 0.3f,
                    0,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * 0.3f
                );
                Vector3 spawnPos = dropPosition + offset;

                // Get the instance for this item (if tracked)
                ItemInstance singleInstance = null;
                if (item.IsInstanceTracked && item.ItemInstances != null && i < item.ItemInstances.Count)
                {
                    singleInstance = item.ItemInstances[i];
                }

                // Serialize container data if this is a container item
                ContainerItemData containerData = null;
                if (itemDef.ProvidesStorage && item.ContainerInventory != null)
                {
                    containerData = ContainerItemData.FromInventorySystem(item.ContainerInventory);
                }

                WorldItem droppedItem = WorldItem.Spawn(
                    itemDef,
                    spawnPos,
                    Quaternion.identity,
                    1, // Always spawn 1 item per WorldItem
                    singleInstance,
                    targetScene,
                    containerData
                );

                if (droppedItem != null)
                {
                    // Ensure the rigidbody starts at rest (no initial velocity)
                    Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }

                    itemsSpawned++;
                }
            }

            if (itemsSpawned > 0)
            {
                // CRITICAL: Save original stack count BEFORE modifying ItemInstances
                // (StackCount is derived from ItemInstances.Count for tracked items)
                int originalStackCount = item.StackCount;

                // Remove dropped instances from the item
                if (item.IsInstanceTracked && item.ItemInstances != null)
                {
                    item.ItemInstances.RemoveRange(0, Mathf.Min(itemsSpawned, item.ItemInstances.Count));
                }

                // If we dropped all items, remove the entire item from inventory
                if (itemsSpawned >= originalStackCount)
                {
                    grid.InventorySystem.RemoveItem(item.InstanceID);
                }
                else
                {
                    // Partial drop: reduce stack count
                    int remainingCount = item.StackCount - itemsSpawned;
                    var remainingInstances = item.IsInstanceTracked ? new System.Collections.Generic.List<ItemInstance>(item.ItemInstances) : null;

                    Vector2Int position = item.AnchorPosition;
                    GridPlacement.GridDirection direction = item.Rotation;

                    grid.InventorySystem.RemoveItem(item.InstanceID);

                    // Re-add with reduced count
                    // CRITICAL: For tracked items, pass 0 to prevent creating pristine instances
                    int stackCountToCreate = (remainingInstances != null) ? 0 : remainingCount;

                    bool success = grid.InventorySystem.TryAddItem(
                        itemDef,
                        position,
                        direction,
                        out GridPlacement.PlacedItem newItem,
                        stackCountToCreate,
                        allowAutoStack: false
                    );

                    if (success && remainingInstances != null && newItem != null)
                    {
                        // Add the real instances (no need to clear - stackCountToCreate was 0)
                        newItem.AddInstances(remainingInstances);
                    }
                    else if (!success)
                    {
                        Debug.LogError($"[ItemUsageHandler] Failed to re-add item after partial drop! Remaining items lost!");
                    }
                }

                grid.RefreshAllItemVisuals();
            }
            else
            {
                Debug.LogError($"[ItemUsageHandler] Failed to spawn any world items for {itemDef.ItemName}");
            }
        }

        private void ExecuteItemEffect(InventoryItemSO itemDef)
        {
            if (itemDef == null) return;

            // Placeholder for item effects
            // In future, this could call itemDef.ExecuteEffect() or trigger event system
            Debug.Log($"[ItemUsageHandler] Executing effect for: {itemDef.ItemName}");

            // Example effects based on category:
            switch (itemDef.Category)
            {
                case ItemCategory.Medical:
                    Debug.Log($"  → Healing effect (restore health)");
                    // TODO: Actually heal player
                    break;

                case ItemCategory.Consumable:
                    Debug.Log($"  → Consumable effect (restore stamina/hunger)");
                    // TODO: Apply consumable effect
                    break;

                default:
                    Debug.Log($"  → Generic use effect");
                    break;
            }
        }
    }
}
