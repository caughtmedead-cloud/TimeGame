using UnityEngine;
using TimeGame.Systems.Inventory.UI;
using TimeGame.Systems.GridPlacement;
using System.Linq;

namespace TimeGame.Systems.Inventory.Testing
{
    /// <summary>
    /// Test harness for inventory system.
    /// Press keys to test equipment equipping and item spawning.
    /// This logic demonstrates patterns that will be used for actual item pickup.
    /// </summary>
    public class InventoryTestHarness : MonoBehaviour
    {
        [Header("Test Items")]
        [Tooltip("Press 1 to equip this helmet")]
        [SerializeField] private InventoryItemSO testHelmet;

        [Tooltip("Press 2 to equip this vest")]
        [SerializeField] private InventoryItemSO testVest;

        [Tooltip("Press 3 to equip this backpack")]
        [SerializeField] private InventoryItemSO testBackpack;

        [Tooltip("Press 4 to spawn this item into any available inventory grid")]
        [SerializeField] private InventoryItemSO testPickupItem;

        [Header("References")]
        [Tooltip("Helmet equipment slot")]
        [SerializeField] private EquipmentSlot helmetSlot;

        [Tooltip("Vest equipment slot")]
        [SerializeField] private EquipmentSlot vestSlot;

        [Tooltip("Backpack equipment slot")]
        [SerializeField] private EquipmentSlot backpackSlot;

        [Tooltip("Player inventory manager (for finding storage grids)")]
        [SerializeField] private PlayerInventoryManager playerInventoryManager;

        [Tooltip("Root transform containing all inventory grids (optional - for finding additional grids)")]
        [SerializeField] private Transform inventoryRootTransform;

        [Header("Settings")]
        [Tooltip("Show debug logs")]
        [SerializeField] private bool verboseLogging = true;

        private void Start()
        {
            // Test harness ready
        }

        private void Update()
        {
            // Press 1 to equip helmet
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                TestEquipItem(testHelmet, helmetSlot, "Helmet");
            }

            // Press 2 to equip vest
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                TestEquipItem(testVest, vestSlot, "Vest");
            }

            // Press 3 to equip backpack
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                TestEquipItem(testBackpack, backpackSlot, "Backpack");
            }

            // Press 4 to pickup item (spawn into any available grid)
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                TestPickupItem(testPickupItem);
            }

            // Press 5 was previously used to manually sync inventory with the multiplayer server.
            // No-op in singleplayer.

            // Press U to unequip helmet
            if (Input.GetKeyDown(KeyCode.U))
            {
                TestUnequipItem(helmetSlot, "Helmet");
            }

            // Press I to unequip vest
            if (Input.GetKeyDown(KeyCode.I))
            {
                TestUnequipItem(vestSlot, "Vest");
            }

            // Press O to unequip backpack
            if (Input.GetKeyDown(KeyCode.O))
            {
                TestUnequipItem(backpackSlot, "Backpack");
            }
        }

        /// <summary>
        /// Test equipping an item into an equipment slot.
        /// Simulates what will happen when player picks up equipment.
        /// ITEM LINEAGE: Creates a living PlacedItem and places it using TryPlaceExistingItem().
        /// This serves as the baseline for auto-equip systems.
        /// </summary>
        private void TestEquipItem(InventoryItemSO item, EquipmentSlot slot, string slotName)
        {
            if (item == null)
            {
                Debug.LogWarning($"[Test Harness] No test item assigned for {slotName}!");
                return;
            }

            if (slot == null)
            {
                Debug.LogError($"[Test Harness] No {slotName} slot assigned!");
                return;
            }

            Log($"--- Testing Equip: {item.ItemName} into {slotName} ---");

            // Check if slot is already occupied
            if (slot.IsOccupied)
            {
                Log($"{slotName} slot is occupied by {slot.EquippedItem.ItemName}. Unequip first!");
                return;
            }

            // ITEM LINEAGE: Create a living PlacedItem (simulates picking up from world/inventory)
            // This preserves ItemInstances and ContainerInventory through all operations
            PlacedItem placedItem = new PlacedItem(
                System.Guid.NewGuid(),
                item,
                Vector2Int.zero,
                GridDirection.Down
            );

            // If this is a container item, initialize its container inventory
            if (item.ProvidesStorage)
            {
                placedItem.ContainerInventory = new InventorySystem(
                    item.StorageGridSize.x,
                    item.StorageGridSize.y,
                    64f, // Must match UI cell size — read by InventoryGridVisual.Initialize() for sizing
                    Vector3.zero,
                    item.StorageMaxWeight
                );
                Log($"  → {item.ItemName} provides storage: {item.StorageGridSize.x}x{item.StorageGridSize.y}");
            }

            // If this is a tracked item, initialize with pristine instance
            if (item.TrackIndividualItems)
            {
                // Create pristine instance
                ItemInstance instance = new ItemInstance();
                if (item.HasLimitedUses)
                {
                    instance.UsesRemaining = item.MaxUses;
                }
                else
                {
                    instance.UsesRemaining = -1;
                }

                placedItem.AddInstances(new System.Collections.Generic.List<ItemInstance> { instance });
                Log($"  → Created tracked item with {instance.UsesRemaining} uses");
            }

            // CRITICAL: Use TryPlaceExistingItem() to preserve the living item's lineage
            // This is the PROPER way to equip items - never use TryPlaceItem() for existing items!
            bool success = slot.TryPlaceExistingItem(placedItem, GridDirection.Down, Vector2.zero, out PlacedItem equippedItem);

            if (success)
            {
                Log($"✓ Successfully equipped {item.ItemName} into {slotName} with full lineage!");
                Log($"  → PlacedItem stored in slot: {equippedItem != null}");
                Log($"  → ContainerInventory preserved: {equippedItem?.ContainerInventory != null}");
            }
            else
            {
                Log($"✗ Failed to equip {item.ItemName} into {slotName}");
            }
        }

        /// <summary>
        /// Test unequipping an item from an equipment slot.
        /// </summary>
        private void TestUnequipItem(EquipmentSlot slot, string slotName)
        {
            if (slot == null)
            {
                Debug.LogError($"[Test Harness] No {slotName} slot assigned!");
                return;
            }

            if (!slot.IsOccupied)
            {
                Log($"{slotName} slot is already empty");
                return;
            }

            string itemName = slot.EquippedItem.ItemName;
            Log($"--- Testing Unequip: {itemName} from {slotName} ---");

            InventoryItemSO unequippedItem = slot.UnequipItem();

            if (unequippedItem != null)
            {
                Log($"✓ Successfully unequipped {itemName} from {slotName}");
                
                // TODO: In real game, unequipped item would go to inventory or ground
                Log($"  → TODO: Handle unequipped item (currently lost)");
            }
            else
            {
                Log($"✗ Failed to unequip from {slotName}");
            }
        }

        /// <summary>
        /// Test picking up an item - tries to place it in any available inventory grid.
        /// This demonstrates the logic we'll use for the actual pickup system.
        /// </summary>
        private void TestPickupItem(InventoryItemSO item)
        {
            if (item == null)
            {
                Debug.LogWarning("[Test Harness] No test pickup item assigned!");
                return;
            }

            Log($"--- Testing Pickup: {item.ItemName} ({item.Width}x{item.Height}) ---");

            // Strategy: Try to place item in any available grid
            // Priority order:
            // 1. Pockets (quick access)
            // 2. Equipment storage (vest, backpack, etc.)
            // 3. Any other available grids

            bool placed = TryPlaceItemInAnyGrid(item);

            if (placed)
            {
                Log($"✓ Successfully picked up {item.ItemName}!");
            }
            else
            {
                Log($"✗ Failed to pick up {item.ItemName} - no space available!");
            }
        }

        /// <summary>
        /// Try to place an item in any available inventory grid.
        /// This is the core logic that will be used for item pickup.
        /// Returns true if item was placed successfully.
        /// </summary>
        private bool TryPlaceItemInAnyGrid(InventoryItemSO item)
        {
            // Step 1: Try pockets first (quick access)
            if (playerInventoryManager != null)
            {
                InventoryGridVisual pocketGrid = playerInventoryManager.GetPocketGrid();
                if (pocketGrid != null && TryPlaceInGrid(item, pocketGrid, "Pockets"))
                {
                    return true;
                }
            }

            // Step 2: Try equipment storage grids (vest, backpack, etc.)
            if (playerInventoryManager != null)
            {
                var equipmentGrids = playerInventoryManager.GetAllEquipmentGrids();
                foreach (var kvp in equipmentGrids)
                {
                    InventoryGridVisual grid = kvp.Value;
                    if (grid != null && TryPlaceInGrid(item, grid, kvp.Key))
                    {
                        return true;
                    }
                }
            }

            // Step 3: Try any other grids found in the scene
            if (inventoryRootTransform != null)
            {
                // Find all grids under the root transform
                InventoryGridVisual[] allGrids = inventoryRootTransform.GetComponentsInChildren<InventoryGridVisual>();
                
                foreach (InventoryGridVisual grid in allGrids)
                {
                    // Skip if we already tried this grid
                    if (playerInventoryManager != null)
                    {
                        if (grid == playerInventoryManager.GetPocketGrid())
                            continue;
                        
                        // Check if this grid is in equipment grids (FIX: iterate through values instead of ContainsValue)
                        bool isEquipmentGrid = false;
                        foreach (var equipGrid in playerInventoryManager.GetAllEquipmentGrids().Values)
                        {
                            if (grid == equipGrid)
                            {
                                isEquipmentGrid = true;
                                break;
                            }
                        }
                        if (isEquipmentGrid)
                            continue;
                    }

                    if (TryPlaceInGrid(item, grid, grid.name))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Try to place an item in a specific grid.
        /// Finds first available position and places item there.
        /// Returns true if placement succeeded.
        /// </summary>
        private bool TryPlaceInGrid(InventoryItemSO item, InventoryGridVisual grid, string gridName)
        {
            if (grid == null || grid.InventorySystem == null)
                return false;

            Log($"  → Trying {gridName}...");

            // Try all rotations
            GridDirection[] rotations = item.CanRotate 
                ? new[] { GridDirection.Down, GridDirection.Right, GridDirection.Up, GridDirection.Left }
                : new[] { GridDirection.Down };

            foreach (GridDirection rotation in rotations)
            {
                // Try to find first available position
                Vector2Int? position = FindFirstAvailablePosition(grid, item, rotation);
                
                if (position.HasValue)
                {
                    // Try to place item
                    bool success = grid.InventorySystem.TryAddItem(
                        item,
                        position.Value,
                        rotation,
                        out PlacedItem placedItem
                    );

                    if (success)
                    {
                        Log($"    ✓ Placed in {gridName} at {position.Value} (rotation: {rotation})");
                        grid.RefreshAllItemVisuals(); // FIX: RefreshAllItemVisuals not RefreshVisuals
                        return true;
                    }
                }
            }

            Log($"    ✗ No space in {gridName}");
            return false;
        }

        /// <summary>
        /// Find first available position in grid for an item.
        /// Scans grid left-to-right, bottom-to-top.
        /// Returns null if no position available.
        /// </summary>
        private Vector2Int? FindFirstAvailablePosition(InventoryGridVisual grid, InventoryItemSO item, GridDirection rotation)
        {
            int width = grid.InventorySystem.Width;
            int height = grid.InventorySystem.Height;

            int itemWidth = item.GetRotatedWidth(rotation);
            int itemHeight = item.GetRotatedHeight(rotation);

            // Scan bottom-to-top, left-to-right
            for (int y = 0; y <= height - itemHeight; y++)
            {
                for (int x = 0; x <= width - itemWidth; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);

                    // Check if this position is valid
                    bool canPlace = grid.InventorySystem.CanAddItem(item, pos, rotation);

                    if (canPlace)
                    {
                        return pos;
                    }
                }
            }

            return null;
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[Test Harness] {message}");
            }
        }

        private void OnGUI()
        {
            // Display instructions
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 16;
            style.normal.textColor = Color.white;
            style.padding = new RectOffset(10, 10, 10, 10);

            GUILayout.BeginArea(new Rect(10, 10, 400, 300));
            GUILayout.Label("=== INVENTORY TEST HARNESS ===", style);
            GUILayout.Label("", style);
            GUILayout.Label("EQUIP ITEMS:", style);
            GUILayout.Label("  [1] - Equip Helmet", style);
            GUILayout.Label("  [2] - Equip Vest", style);
            GUILayout.Label("  [3] - Equip Backpack", style);
            GUILayout.Label("", style);
            GUILayout.Label("UNEQUIP ITEMS:", style);
            GUILayout.Label("  [U] - Unequip Helmet", style);
            GUILayout.Label("  [I] - Unequip Vest", style);
            GUILayout.Label("  [O] - Unequip Backpack", style);
            GUILayout.Label("", style);
            GUILayout.Label("PICKUP TEST:", style);
            GUILayout.Label("  [4] - Pickup Item (any grid)", style);
            GUILayout.Label("", style);
            GUILayout.Label("NETWORK SYNC:", style);
            GUILayout.Label("  [5] - Sync Inventory with Server", style);
            GUILayout.EndArea();
        }
    }
}
