using UnityEngine;
using System.Collections.Generic;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Manages the player's inventory grids in the right panel.
    /// Handles spawning/removing grids based on equipped storage items (vest, backpack, etc.)
    /// </summary>
    public class PlayerInventoryManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ScrollableInventoryPanel storagePanel;
        [SerializeField] private EquipmentSlot vestSlot;
        [SerializeField] private EquipmentSlot backpackSlot;

        [Header("Permanent Storage")]
        [Tooltip("Pockets always present, never removed")]
        [SerializeField] private bool spawnPocketGrid = true;
        [SerializeField] private Vector2Int pocketGridSize = new Vector2Int(4, 2);
        [SerializeField] private float pocketMaxWeight = 5f;

        [Header("Equipment Storage Defaults")]
        [Tooltip("Default grid sizes if item doesn't specify")]
        [SerializeField] private Vector2Int defaultVestGridSize = new Vector2Int(6, 4);
        [SerializeField] private float defaultVestMaxWeight = 20f;
        [SerializeField] private Vector2Int defaultBackpackGridSize = new Vector2Int(8, 6);
        [SerializeField] private float defaultBackpackMaxWeight = 50f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        // Grid tracking
        private InventoryGridVisual pocketGrid;
        private InventoryGridVisual vestGrid;
        private InventoryGridVisual backpackGrid;

        // Grid names for tracking
        private const string POCKET_GRID_NAME = "PocketGrid";
        private const string VEST_GRID_NAME = "VestStorageGrid";
        private const string BACKPACK_GRID_NAME = "BackpackStorageGrid";

        private void Start()
        {
            // Setup pocket grid (always present)
            if (spawnPocketGrid)
            {
                SpawnPocketGrid();
            }

            // Subscribe to equipment slot events
            if (vestSlot != null)
            {
                vestSlot.OnItemEquipped += OnVestEquipped;
                vestSlot.OnItemUnequipped += OnVestUnequipped;
            }

            if (backpackSlot != null)
            {
                backpackSlot.OnItemEquipped += OnBackpackEquipped;
                backpackSlot.OnItemUnequipped += OnBackpackUnequipped;
            }

            Log("PlayerInventoryManager initialized");
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (vestSlot != null)
            {
                vestSlot.OnItemEquipped -= OnVestEquipped;
                vestSlot.OnItemUnequipped -= OnVestUnequipped;
            }

            if (backpackSlot != null)
            {
                backpackSlot.OnItemEquipped -= OnBackpackEquipped;
                backpackSlot.OnItemUnequipped -= OnBackpackUnequipped;
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

        #region Vest Storage

        private void OnVestEquipped(InventoryItemSO vest)
        {
            Log($"Vest equipped: {vest.ItemName}");
            SpawnVestGrid(vest);
        }

        private void OnVestUnequipped(InventoryItemSO vest)
        {
            Log($"Vest unequipped: {vest.ItemName}");
            RemoveVestGrid();
        }

        private void SpawnVestGrid(InventoryItemSO vest)
        {
            if (storagePanel == null) return;

            // Check if vest has custom storage properties
            // For now, use defaults - in future, InventoryItemSO could have StorageWidth/StorageHeight
            Vector2Int gridSize = defaultVestGridSize;
            float maxWeight = defaultVestMaxWeight;

            // TODO: Get from vest.StorageGridSize if property exists
            // gridSize = vest.StorageGridSize != Vector2Int.zero ? vest.StorageGridSize : defaultVestGridSize;

            vestGrid = storagePanel.SpawnGrid(
                VEST_GRID_NAME,
                gridSize.x,
                gridSize.y,
                maxWeight,
                null,
                withLabel: true,
                labelText: $"{vest.ItemName} Storage"
            );

            Log($"Spawned vest storage grid: {gridSize.x}x{gridSize.y}");
        }

        private void RemoveVestGrid()
        {
            if (storagePanel == null) return;

            // First, need to move items out of vest storage
            // TODO: Drop items on ground or try to move to other storage
            
            bool removed = storagePanel.RemoveGrid(VEST_GRID_NAME);
            if (removed)
            {
                vestGrid = null;
                Log("Removed vest storage grid");
            }
        }

        #endregion

        #region Backpack Storage

        private void OnBackpackEquipped(InventoryItemSO backpack)
        {
            Log($"Backpack equipped: {backpack.ItemName}");
            SpawnBackpackGrid(backpack);
        }

        private void OnBackpackUnequipped(InventoryItemSO backpack)
        {
            Log($"Backpack unequipped: {backpack.ItemName}");
            RemoveBackpackGrid();
        }

        private void SpawnBackpackGrid(InventoryItemSO backpack)
        {
            if (storagePanel == null) return;

            // Check if backpack has custom storage properties
            Vector2Int gridSize = defaultBackpackGridSize;
            float maxWeight = defaultBackpackMaxWeight;

            // TODO: Get from backpack.StorageGridSize if property exists

            backpackGrid = storagePanel.SpawnGrid(
                BACKPACK_GRID_NAME,
                gridSize.x,
                gridSize.y,
                maxWeight,
                null,
                withLabel: true,
                labelText: $"{backpack.ItemName} Storage"
            );

            Log($"Spawned backpack storage grid: {gridSize.x}x{gridSize.y}");
        }

        private void RemoveBackpackGrid()
        {
            if (storagePanel == null) return;

            // TODO: Handle items in backpack when unequipping
            
            bool removed = storagePanel.RemoveGrid(BACKPACK_GRID_NAME);
            if (removed)
            {
                backpackGrid = null;
                Log("Removed backpack storage grid");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the pocket grid (always present)
        /// </summary>
        public InventoryGridVisual GetPocketGrid() => pocketGrid;

        /// <summary>
        /// Get the vest storage grid (null if no vest equipped)
        /// </summary>
        public InventoryGridVisual GetVestGrid() => vestGrid;

        /// <summary>
        /// Get the backpack storage grid (null if no backpack equipped)
        /// </summary>
        public InventoryGridVisual GetBackpackGrid() => backpackGrid;

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

            // Respawn pocket grid
            if (spawnPocketGrid)
            {
                SpawnPocketGrid();
            }

            // Respawn equipment grids based on current equipment
            if (vestSlot != null && vestSlot.IsOccupied)
            {
                SpawnVestGrid(vestSlot.EquippedItem);
            }

            if (backpackSlot != null && backpackSlot.IsOccupied)
            {
                SpawnBackpackGrid(backpackSlot.EquippedItem);
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
}
