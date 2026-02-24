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
            // Ensure the inspect panel singleton exists
            if (ItemInspectPanel.Instance == null)
            {
                GameObject panelGO = new GameObject("ItemInspectPanel");
                panelGO.AddComponent<ItemInspectPanel>();
                // Awake fires immediately, sets Instance and builds UI
            }

            // Pass the shared inventory font to the inspect panel so text matches the rest of the UI.
            // SetFont() is a no-op if the panel already has a font assigned in the inspector.
            InventoryGridFactory factory = FindObjectOfType<InventoryGridFactory>();
            if (factory != null && ItemInspectPanel.Instance != null)
                ItemInspectPanel.Instance.SetFont(factory.UIFont);

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

            // Subscribe to context menu events
            InventoryContextMenu contextMenu = FindObjectOfType<InventoryContextMenu>();
            if (contextMenu != null)
            {
                contextMenu.OnOpenItem += HandleOpenItem;
                contextMenu.OnUseItem += HandleUseItem;
                contextMenu.OnInspectItem += HandleInspectItem;
                contextMenu.OnDropItem += HandleDropAllItems;
                contextMenu.OnDropOneItem += HandleDropOneItem;
                contextMenu.OnEquipItem += HandleEquipItem;
                contextMenu.OnEquipmentSlotUnequip += HandleEquipmentSlotUnequip;
                contextMenu.OnEquipmentSlotInspect += HandleEquipmentSlotInspect;
                Debug.Log("[ItemUsageHandler] Subscribed to context menu events");
            }
            else
            {
                Debug.LogWarning("[ItemUsageHandler] Could not find InventoryContextMenu in scene!");
            }
        }

        private void HandleOpenItem(PlacedItem item, InventoryGridVisual grid)
        {
            if (item == null) return;

            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            if (itemDef == null || !itemDef.ProvidesStorage)
            {
                Debug.LogWarning("[ItemUsageHandler] Cannot open - item doesn't provide storage!");
                return;
            }

            // Open in floating window (for nested inventory containers)
            FloatingContainerWindowManager windowManager = FloatingContainerWindowManager.Instance;
            if (windowManager != null)
            {
                int parentNetId       = -1;
                int parentCompartment = -1;
                System.Guid[] parentPath = null;

                // Case A: item is directly in a root world-container compartment grid.
                ContainerInteractionManager cim = ContainerInteractionManager.Instance;
                if (cim != null && cim.IsContainerOpen && grid != null)
                {
                    if (cim.TryGetContainerContext(grid, out parentNetId, out parentCompartment))
                    {
                        parentPath = System.Array.Empty<System.Guid>(); // root level — no ancestors
                    }
                }

                // Case B: item is inside an already-open floating window (deeper nesting).
                if (parentNetId < 0 && grid != null)
                {
                    FloatingContainerWindow parentWindow =
                        windowManager.FindWindowByGrid(grid);

                    if (parentWindow != null)
                    {
                        FloatingContainerWindow.ParentContainerContext ctx =
                            parentWindow.GetParentContext();

                        if (ctx.IsValid)
                        {
                            parentNetId       = ctx.WorldContainerNetId;
                            parentCompartment = ctx.CompartmentIndex;
                            // Build the path for the child window:
                            // parent window's ancestor chain + parent window's own container ID.
                            parentPath = AppendGuid(ctx.ContainerPath, parentWindow.ContainerItem.InstanceID);
                        }
                    }
                }

                windowManager.OpenContainer(item, parentNetId, parentCompartment, parentPath);
                Debug.Log($"[ItemUsageHandler] Opened {itemDef.ItemName} in floating window" +
                          (parentNetId >= 0
                              ? $" (world container {parentNetId} compartment {parentCompartment} pathDepth={parentPath?.Length ?? 0})"
                              : ""));
                return;
            }

            // Fallback: Try world container system (for loot containers in world)
            ContainerInteractionManager containerManager = FindObjectOfType<ContainerInteractionManager>();
            if (containerManager != null)
            {
                ContainerHelper.OpenContainer(item, containerManager);
                Debug.Log($"[ItemUsageHandler] Opened {itemDef.ItemName} using ContainerInteractionManager (world container)");
                return;
            }

            Debug.LogError("[ItemUsageHandler] Cannot open container - No FloatingContainerWindowManager or ContainerInteractionManager found in scene!");
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

            ItemInspectPanel.Instance?.Show(itemDef, item);
        }

        /// <summary>
        /// Equip an item from a grid directly into the first compatible empty slot.
        /// Removes the item from the grid and places it in the equipment slot, preserving full lineage.
        /// </summary>
        private void HandleEquipItem(PlacedItem item, InventoryGridVisual grid, EquipmentSlot targetSlot)
        {
            if (item == null || grid == null || targetSlot == null) return;

            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            if (itemDef == null) return;

            // Remove from grid first
            grid.InventorySystem.RemoveItem(item.InstanceID);
            grid.RefreshAllItemVisuals();

            // Equip into slot, preserving full PlacedItem lineage
            bool equipped = targetSlot.TryPlaceExistingItem(item, GridDirection.Down, Vector2.zero, out _);
            if (!equipped)
            {
                // Slot rejected item — put it back
                Debug.LogWarning($"[ItemUsageHandler] Equip failed for {itemDef.ItemName}, returning to grid");
                grid.InventorySystem.TryAddItem(item.InstanceID, itemDef, item.AnchorPosition, item.Rotation, out _, item.StackCount);
                grid.RefreshAllItemVisuals();
            }
            else
            {
                Debug.Log($"[ItemUsageHandler] Equipped {itemDef.ItemName} via context menu");
            }
        }

        /// <summary>
        /// Unequip an item from an equipment slot and move it to the first available grid slot.
        /// </summary>
        private void HandleEquipmentSlotUnequip(EquipmentSlot slot)
        {
            if (slot == null || !slot.IsOccupied) return;

            InventoryItemSO itemDef = slot.EquippedItem;
            PlacedItem placedItem = slot.GetEquippedPlacedItem();

            // Unequip fires OnItemUnequipped which saves ContainerInventory
            slot.UnequipItem();

            if (placedItem == null)
            {
                Debug.LogWarning($"[ItemUsageHandler] Unequipped {itemDef?.ItemName} but had no PlacedItem data");
                return;
            }

            // Find first grid with a free position that fits the item
            PlayerInventoryManager inventoryManager = PlayerInventoryManager.Instance;
            if (inventoryManager != null)
            {
                foreach (InventoryGridVisual grid in inventoryManager.GetAllGrids())
                {
                    if (grid == null || grid.InventorySystem == null) continue;

                    // Scan all positions to find one where the item fits
                    Vector2Int? freePos = FindFirstFreePosition(grid.InventorySystem, itemDef, GridDirection.Down);
                    if (freePos == null) continue;

                    bool placed = grid.InventorySystem.TryAddItem(
                        placedItem.InstanceID,
                        itemDef,
                        freePos.Value,
                        GridDirection.Down,
                        out PlacedItem restoredItem
                    );

                    if (placed && restoredItem != null)
                    {
                        // Transfer container inventory and instances
                        if (placedItem.ContainerInventory != null)
                            restoredItem.ContainerInventory = placedItem.ContainerInventory;
                        if (placedItem.IsInstanceTracked && placedItem.ItemInstances?.Count > 0)
                            restoredItem.AddInstances(placedItem.ItemInstances);

                        grid.RefreshAllItemVisuals();
                        Debug.Log($"[ItemUsageHandler] Unequipped {itemDef.ItemName} → moved to {grid.name} at {freePos.Value}");
                        return;
                    }
                }
            }

            Debug.LogWarning($"[ItemUsageHandler] No space found for unequipped {itemDef?.ItemName} — item lost!");
        }

        /// <summary>
        /// Scans all positions in the given inventory system and returns the first position
        /// where the item can be placed. Accounts for item rotation dimensions so we don't
        /// wastefully test positions that couldn't possibly fit. Returns null if no free slot.
        /// (Same pattern as InventoryTestHarness.FindFirstAvailablePosition)
        /// </summary>
        private Vector2Int? FindFirstFreePosition(InventorySystem system, InventoryItemSO item, GridDirection rotation)
        {
            if (system == null || item == null) return null;

            int itemWidth  = item.GetRotatedWidth(rotation);
            int itemHeight = item.GetRotatedHeight(rotation);

            for (int y = 0; y <= system.Height - itemHeight; y++)
            {
                for (int x = 0; x <= system.Width - itemWidth; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (system.CanAddItem(item, pos, rotation))
                        return pos;
                }
            }

            return null;
        }

        /// <summary>
        /// Inspect an item currently equipped in a slot.
        /// </summary>
        private void HandleEquipmentSlotInspect(EquipmentSlot slot)
        {
            if (slot == null || !slot.IsOccupied) return;

            InventoryItemSO itemDef = slot.EquippedItem;
            PlacedItem placedItem   = slot.GetEquippedPlacedItem();

            ItemInspectPanel.Instance?.Show(itemDef, placedItem);
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
                    // ITEM LINEAGE: Partial drop - item is still alive, just modified
                    // The instances were already removed at line 349 via RemoveRange()
                    // For tracked items, StackCount is derived from ItemInstances.Count, so it's already correct
                    // For homogeneous items, we need to manually reduce the stack count

                    if (!item.IsInstanceTracked)
                    {
                        // Homogeneous items: reduce stack count manually
                        // This is safe because we're not using instances
                        int newStackCount = originalStackCount - itemsSpawned;

                        // Unfortunately PlacedItem doesn't have a SetStackCount method
                        // We need to use the internal StackCount property
                        // The instances were already removed, so for tracked items this is automatic
                        // For non-tracked, we need to handle this differently

                        // Since we can't directly modify StackCount for homogeneous items,
                        // we'll need to remove and recreate ONLY for homogeneous items
                        Vector2Int position = item.AnchorPosition;
                        GridPlacement.GridDirection direction = item.Rotation;

                        grid.InventorySystem.RemoveItem(item.InstanceID);

                        bool success = grid.InventorySystem.TryAddItem(
                            itemDef,
                            position,
                            direction,
                            out PlacedItem newItem,
                            newStackCount,
                            allowAutoStack: false
                        );

                        if (!success)
                        {
                            Debug.LogError($"[ItemUsageHandler] Failed to re-add homogeneous item after partial drop!");
                        }
                    }
                    // For tracked items, no action needed - RemoveRange already modified StackCount
                }

                grid.RefreshAllItemVisuals();
            }
            else
            {
                Debug.LogError($"[ItemUsageHandler] Failed to spawn any world items for {itemDef.ItemName}");
            }
        }

        /// <summary>
        /// Return a new Guid[] that is <paramref name="existing"/> with <paramref name="id"/> appended.
        /// </summary>
        private static System.Guid[] AppendGuid(System.Guid[] existing, System.Guid id)
        {
            if (existing == null || existing.Length == 0)
                return new System.Guid[] { id };

            System.Guid[] result = new System.Guid[existing.Length + 1];
            System.Array.Copy(existing, result, existing.Length);
            result[existing.Length] = id;
            return result;
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
