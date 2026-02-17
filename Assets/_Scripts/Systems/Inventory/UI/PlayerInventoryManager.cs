using UnityEngine;
using System.Collections.Generic;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Manages the player's inventory grids in the right panel.
    /// Handles spawning/removing grids based on equipped storage items.
    /// Uses flexible array-based approach for any equipment that provides storage.
    /// Grid sizes are determined by the equipped item's properties.
    /// </summary>
    public class PlayerInventoryManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ScrollableInventoryPanel storagePanel;

        [Header("Permanent Storage")]
        [Tooltip("Pockets always present, never removed")]
        [SerializeField] private bool spawnPocketGrid = true;
        [SerializeField] private Vector2Int pocketGridSize = new Vector2Int(4, 2);
        [SerializeField] private float pocketMaxWeight = 5f;

        [Header("Equipment Storage Mappings")]
        [Tooltip("Array of equipment slots that provide storage when equipped")]
        [SerializeField] private EquipmentStorageMapping[] equipmentStorageMappings;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        // Grid tracking
        private InventoryGridVisual pocketGrid;
        private Dictionary<string, InventoryGridVisual> equipmentGrids = new Dictionary<string, InventoryGridVisual>();

        // Grid names
        private const string POCKET_GRID_NAME = "PocketGrid";

        private void Start()
        {
            // Setup pocket grid (always present)
            if (spawnPocketGrid)
            {
                SpawnPocketGrid();
            }

            // Subscribe to all equipment slot events
            if (equipmentStorageMappings != null)
            {
                foreach (var mapping in equipmentStorageMappings)
                {
                    if (mapping.slot != null)
                    {
                        // Store mapping reference on the event subscription
                        mapping.slot.OnItemEquipped += (item) => OnEquipmentEquipped(mapping, item);
                        mapping.slot.OnItemUnequipped += (item) => OnEquipmentUnequipped(mapping, item);
                        
                        Log($"Subscribed to {mapping.slot.name} events");
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayerInventoryManager] Equipment storage mapping has null slot!");
                    }
                }
            }

            Log("PlayerInventoryManager initialized");
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (equipmentStorageMappings != null)
            {
                foreach (var mapping in equipmentStorageMappings)
                {
                    if (mapping.slot != null)
                    {
                        mapping.slot.OnItemEquipped -= (item) => OnEquipmentEquipped(mapping, item);
                        mapping.slot.OnItemUnequipped -= (item) => OnEquipmentUnequipped(mapping, item);
                    }
                }
            }
        }

        #region Pocket Grid (Permanent)

        private void SpawnPocketGrid()
        {
            if (storagePanel == null)
            {
                Debug.LogError("[PlayerInventoryManager] No storage panel assigned!");
                return;
            }

            pocketGrid = storagePanel.SpawnGrid(
                POCKET_GRID_NAME,
                pocketGridSize.x,
                pocketGridSize.y,
                pocketMaxWeight,
                null,
                withLabel: true,
                labelText: "Pockets"
            );

            Log($"Spawned pocket grid: {pocketGridSize.x}x{pocketGridSize.y}");
        }

        #endregion

        #region Equipment Storage (Dynamic)

        private void OnEquipmentEquipped(EquipmentStorageMapping mapping, InventoryItemSO item)
        {
            Log($"Equipment equipped: {item.ItemName} in {mapping.slot.name}");

            // ITEM LINEAGE: Ensure the slot has a PlacedItem stored
            // If it doesn't (e.g., equipped via TryEquipItem instead of TryPlaceExistingItem),
            // the container inventory won't persist through unequip/re-equip
            PlacedItem placedItem = mapping.slot.GetEquippedPlacedItem();
            if (placedItem == null)
            {
                Debug.LogWarning($"[PlayerInventoryManager] {item.ItemName} was equipped without a PlacedItem! Container data will not persist. Use TryPlaceExistingItem() instead of TryEquipItem().");
            }

            // Close any floating window that was open for this container item.
            // The item is now equipped (becoming a sidebar grid), so the floating window is stale.
            if (placedItem != null)
            {
                FloatingContainerWindowManager windowManager = FindObjectOfType<FloatingContainerWindowManager>();
                windowManager?.CloseWindowForItem(placedItem);
            }

            // Only spawn storage grid if item provides storage
            if (item.ProvidesStorage)
            {
                SpawnEquipmentGrid(mapping, item);
            }
            else
            {
                Log($"{item.ItemName} does not provide storage - skipping grid spawn");
            }
        }

        private void OnEquipmentUnequipped(EquipmentStorageMapping mapping, InventoryItemSO item)
        {
            Log($"Equipment unequipped: {item.ItemName} from {mapping.slot.name}");

            // ITEM LINEAGE: Save container contents back to the PlacedItem before removing grid
            if (item.ProvidesStorage)
            {
                // Get the equipment slot's PlacedItem
                PlacedItem equippedItem = mapping.slot.GetEquippedPlacedItem();

                // Get the equipment storage grid's inventory
                string gridName = mapping.gridName;
                if (equipmentGrids.ContainsKey(gridName))
                {
                    InventoryGridVisual grid = equipmentGrids[gridName];
                    if (grid != null && grid.InventorySystem != null && equippedItem != null)
                    {
                        // CRITICAL: Store the grid's inventory in the PlacedItem's ContainerInventory
                        // This preserves all items inside the backpack!
                        equippedItem.ContainerInventory = grid.InventorySystem;

                        int itemCount = grid.InventorySystem.GetAllItems().Count;
                        Log($"Saved {itemCount} items from {gridName} to {item.ItemName}.ContainerInventory");
                    }
                }

                // Now safe to remove the grid
                RemoveEquipmentGrid(mapping);
            }
        }

        private void SpawnEquipmentGrid(EquipmentStorageMapping mapping, InventoryItemSO item)
        {
            if (storagePanel == null) return;

            // Get grid configuration from ITEM (not defaults!)
            Vector2Int gridSize = item.StorageGridSize;
            float maxWeight = item.StorageMaxWeight;
            string gridName = mapping.gridName;

            // Validate item storage size
            if (gridSize.x <= 0 || gridSize.y <= 0)
            {
                Debug.LogWarning($"[PlayerInventoryManager] {item.ItemName} has invalid StorageGridSize ({gridSize.x}x{gridSize.y}), using defaults");
                gridSize = mapping.defaultGridSize;
            }

            // Validate weight
            if (maxWeight <= 0)
            {
                Debug.LogWarning($"[PlayerInventoryManager] {item.ItemName} has invalid StorageMaxWeight ({maxWeight}), using default");
                maxWeight = mapping.defaultMaxWeight;
            }

            // Spawn the grid
            InventoryGridVisual grid = storagePanel.SpawnGrid(
                gridName,
                gridSize.x,
                gridSize.y,
                maxWeight,
                null,
                withLabel: true,
                labelText: mapping.displayLabel ?? $"{item.ItemName} Storage"
            );

            if (grid != null)
            {
                equipmentGrids[gridName] = grid;

                // ITEM LINEAGE: Restore items from PlacedItem's ContainerInventory.
                // CRITICAL: We swap the grid's InventorySystem to be the ContainerInventory directly,
                // exactly like FloatingContainerWindowManager does. This means items are NEVER copied —
                // they always live in exactly one InventorySystem. Copying would cause duplication if a
                // floating window is still open (both would display the same items from two systems).
                PlacedItem equippedPlacedItem = mapping.slot.GetEquippedPlacedItem();
                if (equippedPlacedItem != null && equippedPlacedItem.ContainerInventory != null)
                {
                    // Swap in the container's existing system WITHOUT touching the factory layout.
                    // All items remain in the same system object — no duplication possible.
                    grid.SwapInventorySystem(equippedPlacedItem.ContainerInventory);
                    grid.RefreshAllItemVisuals();

                    int itemCount = equippedPlacedItem.ContainerInventory.GetAllItems().Count;
                    Log($"Restored {itemCount} items from {item.ItemName}.ContainerInventory to {gridName} (system swap, no copy)");
                }

                Log($"Spawned equipment storage grid: {gridName} ({gridSize.x}x{gridSize.y}, {maxWeight}kg capacity) from {item.ItemName}");
            }
        }

        private void RemoveEquipmentGrid(EquipmentStorageMapping mapping)
        {
            if (storagePanel == null) return;

            string gridName = mapping.gridName;

            // Items should be saved to ContainerInventory BEFORE this method is called
            // (handled in OnEquipmentUnequipped)
            if (equipmentGrids.ContainsKey(gridName))
            {
                InventoryGridVisual grid = equipmentGrids[gridName];
                if (grid != null && grid.InventorySystem != null)
                {
                    int itemCount = grid.InventorySystem.GetAllItems().Count;
                    if (itemCount > 0)
                    {
                        Log($"Removing {gridName} with {itemCount} items (items should already be saved to ContainerInventory)");
                    }
                }
            }

            bool removed = storagePanel.RemoveGrid(gridName);
            if (removed)
            {
                equipmentGrids.Remove(gridName);
                Log($"Removed equipment storage grid: {gridName}");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the pocket grid (always present)
        /// </summary>
        public InventoryGridVisual GetPocketGrid() => pocketGrid;

        /// <summary>
        /// Get an equipment storage grid by name
        /// </summary>
        public InventoryGridVisual GetEquipmentGrid(string gridName)
        {
            equipmentGrids.TryGetValue(gridName, out InventoryGridVisual grid);
            return grid;
        }

        /// <summary>
        /// Get all currently active equipment grids
        /// </summary>
        public IReadOnlyDictionary<string, InventoryGridVisual> GetAllEquipmentGrids()
        {
            return equipmentGrids;
        }

        /// <summary>
        /// Get all inventory grids (pockets + equipment storage).
        /// Useful for finding space when picking up items.
        /// </summary>
        public System.Collections.Generic.IEnumerable<InventoryGridVisual> GetAllGrids()
        {
            System.Collections.Generic.List<InventoryGridVisual> allGrids = new System.Collections.Generic.List<InventoryGridVisual>();

            // Add pocket grids
            if (storagePanel != null)
            {
                allGrids.AddRange(storagePanel.GetAllGrids());
            }

            // Add equipment storage grids
            allGrids.AddRange(equipmentGrids.Values);

            return allGrids;
        }

        /// <summary>
        /// Refresh only the item visuals on all existing grids.
        /// Does NOT destroy/recreate grids - just updates the visual representation.
        /// Use this when opening inventory UI to show items added while closed.
        /// </summary>
        public void RefreshAllGridVisuals()
        {
            // Refresh pocket grid visuals
            if (storagePanel != null)
            {
                foreach (var grid in storagePanel.GetAllGrids())
                {
                    if (grid != null)
                    {
                        grid.RefreshAllItemVisuals();
                    }
                }
            }

            // Refresh equipment grid visuals
            foreach (var grid in equipmentGrids.Values)
            {
                if (grid != null)
                {
                    grid.RefreshAllItemVisuals();
                }
            }

            Log("Refreshed all grid visuals");
        }

        /// <summary>
        /// Manually refresh all grids (useful after loading save data)
        /// WARNING: This destroys and recreates all grids!
        /// For simple visual refresh, use RefreshAllGridVisuals() instead.
        /// </summary>
        public void RefreshAllGrids()
        {
            // Clear existing grids
            if (storagePanel != null)
            {
                storagePanel.ClearAllGrids();
            }

            equipmentGrids.Clear();

            // Respawn pocket grid
            if (spawnPocketGrid)
            {
                SpawnPocketGrid();
            }

            // Respawn equipment grids based on current equipment
            if (equipmentStorageMappings != null)
            {
                foreach (var mapping in equipmentStorageMappings)
                {
                    if (mapping.slot != null && mapping.slot.IsOccupied)
                    {
                        InventoryItemSO item = mapping.slot.EquippedItem;
                        if (item != null && item.ProvidesStorage)
                        {
                            SpawnEquipmentGrid(mapping, item);
                        }
                    }
                }
            }

            Log("Refreshed all player inventory grids");
        }

        #endregion

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[PlayerInventoryManager] {message}");
            }
        }
    }

    /// <summary>
    /// Defines mapping between an equipment slot and its storage grid properties.
    /// Add entries in Inspector for any equipment that provides storage.
    /// Default values used as fallback if item doesn't specify valid storage properties.
    /// </summary>
    [System.Serializable]
    public class EquipmentStorageMapping
    {
        [Tooltip("The equipment slot to monitor")]
        public EquipmentSlot slot;

        [Tooltip("Unique name for this grid (e.g., 'VestStorageGrid', 'BackpackStorageGrid')")]
        public string gridName = "EquipmentStorageGrid";

        [Tooltip("Display label for this storage (e.g., 'Vest Storage', 'Backpack')")]
        public string displayLabel = "Storage";

        [Header("Fallback Grid Properties")]
        [Tooltip("Fallback grid size if item doesn't specify valid size")]
        public Vector2Int defaultGridSize = new Vector2Int(6, 4);

        [Tooltip("Fallback max weight if item doesn't specify valid weight")]
        public float defaultMaxWeight = 20f;
    }
}
