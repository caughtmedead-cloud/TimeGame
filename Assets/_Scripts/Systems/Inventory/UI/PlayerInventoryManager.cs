using UnityEngine;
using System.Collections.Generic;

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
            
            // Only remove grid if item had storage
            if (item.ProvidesStorage)
            {
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
                Log($"Spawned equipment storage grid: {gridName} ({gridSize.x}x{gridSize.y}, {maxWeight}kg capacity) from {item.ItemName}");
            }
        }

        private void RemoveEquipmentGrid(EquipmentStorageMapping mapping)
        {
            if (storagePanel == null) return;

            string gridName = mapping.gridName;

            // TODO: Handle items in storage before removing
            // Option A: Drop items on ground
            // Option B: Try to move to pockets
            // For now, items will be lost (warn in logs)
            if (equipmentGrids.ContainsKey(gridName))
            {
                InventoryGridVisual grid = equipmentGrids[gridName];
                if (grid != null && grid.InventorySystem != null)
                {
                    int itemCount = grid.InventorySystem.GetAllItems().Count;
                    if (itemCount > 0)
                    {
                        Debug.LogWarning($"[PlayerInventoryManager] Removing {gridName} with {itemCount} items! Items will be lost. TODO: Implement item handling.");
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
        /// Manually refresh all grids (useful after loading save data)
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
