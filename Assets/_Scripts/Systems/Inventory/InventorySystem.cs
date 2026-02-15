using System;
using System.Collections.Generic;
using UnityEngine;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Inventory system built on top of GridPlacementSystem.
    /// Handles inventory-specific logic like weight limits, filtering, etc.
    /// 
    /// This is pure logic - no UI, no networking (yet).
    /// </summary>
    public class InventorySystem
    {
        /// <summary>
        /// The underlying grid placement system.
        /// All placement logic delegates to this.
        /// </summary>
        private GridPlacementSystem gridSystem;

        /// <summary>
        /// Maximum weight capacity (0 = unlimited).
        /// </summary>
        public float MaxWeight { get; set; }

        /// <summary>
        /// Enable weight restrictions?
        /// </summary>
        public bool UseWeightLimit { get; set; }

        /// <summary>
        /// Enable debug logging.
        /// </summary>
        public bool EnableDebugLogging
        {
            get => gridSystem.EnableDebugLogging;
            set => gridSystem.EnableDebugLogging = value;
        }

        /// <summary>
        /// Grid dimensions.
        /// </summary>
        public int Width => gridSystem.Width;
        public int Height => gridSystem.Height;
        public float CellSize => gridSystem.CellSize;

        /// <summary>
        /// Events
        /// </summary>
        public event Action<PlacedItem> OnItemAdded;
        public event Action<PlacedItem> OnItemRemoved;
        public event Action<float> OnWeightChanged;

        /// <summary>
        /// Constructor
        /// </summary>
        public InventorySystem(int width, int height, float cellSize, Vector3 origin, float maxWeight = 0f)
        {
            gridSystem = new GridPlacementSystem(width, height, cellSize, origin);
            MaxWeight = maxWeight;
            UseWeightLimit = maxWeight > 0f;

            // Subscribe to grid events
            gridSystem.OnItemPlaced += HandleItemPlaced;
            gridSystem.OnItemRemoved += HandleItemRemoved;
        }

        #region Add/Remove Items

        /// <summary>
        /// Try to add an item to the inventory.
        /// Returns true if successful, false if blocked or over weight limit.
        /// </summary>
        public bool TryAddItem(InventoryItemSO item, Vector2Int position, GridDirection rotation, out PlacedItem placedItem)
        {
            placedItem = null;

            if (item == null)
            {
                LogWarning("Cannot add null item");
                return false;
            }

            // Check weight limit
            if (UseWeightLimit && !CanAddWeight(item.Weight))
            {
                if (EnableDebugLogging)
                {
                    LogWarning($"Cannot add {item.ItemName} - would exceed weight limit ({GetCurrentWeight()}/{MaxWeight})");
                }
                return false;
            }

            // Try placement in grid system
            bool success = gridSystem.TryPlaceItem(item, position, rotation, out placedItem);

            return success;
        }

        /// <summary>
        /// Try to add an item with a specific instance ID (for network replication).
        /// </summary>
        public bool TryAddItem(Guid instanceID, InventoryItemSO item, Vector2Int position, GridDirection rotation, out PlacedItem placedItem)
        {
            placedItem = null;

            if (item == null)
            {
                LogWarning("Cannot add null item");
                return false;
            }

            // Check weight limit
            if (UseWeightLimit && !CanAddWeight(item.Weight))
            {
                if (EnableDebugLogging)
                {
                    LogWarning($"Cannot add {item.ItemName} - would exceed weight limit ({GetCurrentWeight()}/{MaxWeight})");
                }
                return false;
            }

            // Try placement in grid system with specific ID
            bool success = gridSystem.TryPlaceItem(instanceID, item, position, rotation, out placedItem);

            return success;
        }

        /// <summary>
        /// Check if an item can be added at a position (validates placement + weight).
        /// </summary>
        public bool CanAddItem(InventoryItemSO item, Vector2Int position, GridDirection rotation)
        {
            return CanAddItem(item, position, rotation, Guid.Empty);
        }

        /// <summary>
        /// Check if an item can be added at a position, optionally ignoring a specific item's cells.
        /// Useful for same-grid drag operations.
        /// </summary>
        public bool CanAddItem(InventoryItemSO item, Vector2Int position, GridDirection rotation, Guid ignoreItemID)
        {
            if (item == null) return false;

            // Check weight (but account for items we're moving within the same grid)
            if (UseWeightLimit)
            {
                float weightToAdd = item.Weight;

                // If we're moving an item that's already in this grid (ignoreItemID),
                // we need to subtract its weight since it's already counted in GetCurrentWeight()
                if (ignoreItemID != Guid.Empty)
                {
                    PlacedItem existingItem = gridSystem.GetItemByID(ignoreItemID);
                    if (existingItem != null && existingItem.ItemDefinition is InventoryItemSO existingInventoryItem)
                    {
                        weightToAdd -= existingInventoryItem.Weight;
                    }
                }

                if (!CanAddWeight(weightToAdd))
                {
                    return false;
                }
            }

            // Check grid placement
            return gridSystem.CanPlaceItem(item, position, rotation, ignoreItemID);
        }

        /// <summary>
        /// Remove an item at the specified position.
        /// </summary>
        public bool RemoveItemAt(Vector2Int position)
        {
            return gridSystem.RemoveItemAt(position);
        }

        /// <summary>
        /// Remove an item by instance ID.
        /// </summary>
        public bool RemoveItem(Guid instanceID)
        {
            return gridSystem.RemoveItem(instanceID);
        }

        /// <summary>
        /// Clear all items from inventory.
        /// </summary>
        public void ClearAll()
        {
            gridSystem.ClearAll();
        }

        #endregion

        #region Weight Management

        /// <summary>
        /// Get current total weight of all items.
        /// </summary>
        public float GetCurrentWeight()
        {
            float totalWeight = 0f;

            foreach (PlacedItem placedItem in gridSystem.GetAllPlacedItems())
            {
                if (placedItem.ItemDefinition is InventoryItemSO inventoryItem)
                {
                    totalWeight += inventoryItem.Weight;
                }
            }

            return totalWeight;
        }

        /// <summary>
        /// Get remaining weight capacity.
        /// </summary>
        public float GetRemainingWeight()
        {
            if (!UseWeightLimit) return float.MaxValue;
            return Mathf.Max(0f, MaxWeight - GetCurrentWeight());
        }

        /// <summary>
        /// Check if adding weight would exceed limit.
        /// </summary>
        public bool CanAddWeight(float weight)
        {
            if (!UseWeightLimit) return true;
            return (GetCurrentWeight() + weight) <= MaxWeight;
        }

        /// <summary>
        /// Get weight utilization percentage (0-1).
        /// </summary>
        public float GetWeightPercentage()
        {
            if (!UseWeightLimit || MaxWeight <= 0f) return 0f;
            return Mathf.Clamp01(GetCurrentWeight() / MaxWeight);
        }

        #endregion

        #region Query Operations

        /// <summary>
        /// Get the item at a specific position.
        /// </summary>
        public PlacedItem GetItemAt(Vector2Int position)
        {
            return gridSystem.GetItemAt(position);
        }

        /// <summary>
        /// Get an item by its instance ID.
        /// </summary>
        public PlacedItem GetItemByID(Guid instanceID)
        {
            return gridSystem.GetItemByID(instanceID);
        }

        /// <summary>
        /// Get all items in inventory.
        /// </summary>
        public IReadOnlyCollection<PlacedItem> GetAllItems()
        {
            return gridSystem.GetAllPlacedItems();
        }

        /// <summary>
        /// Get items by category.
        /// </summary>
        public List<PlacedItem> GetItemsByCategory(ItemCategory category)
        {
            List<PlacedItem> items = new List<PlacedItem>();

            foreach (PlacedItem placedItem in gridSystem.GetAllPlacedItems())
            {
                if (placedItem.ItemDefinition is InventoryItemSO inventoryItem)
                {
                    if (inventoryItem.Category == category)
                    {
                        items.Add(placedItem);
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Get items by rarity.
        /// </summary>
        public List<PlacedItem> GetItemsByRarity(ItemRarity rarity)
        {
            List<PlacedItem> items = new List<PlacedItem>();

            foreach (PlacedItem placedItem in gridSystem.GetAllPlacedItems())
            {
                if (placedItem.ItemDefinition is InventoryItemSO inventoryItem)
                {
                    if (inventoryItem.Rarity == rarity)
                    {
                        items.Add(placedItem);
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Count items of a specific type.
        /// </summary>
        public int CountItemsOfType(InventoryItemSO itemType)
        {
            int count = 0;

            foreach (PlacedItem placedItem in gridSystem.GetAllPlacedItems())
            {
                if (placedItem.ItemDefinition == itemType)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Get total number of items in inventory.
        /// </summary>
        public int GetItemCount()
        {
            return gridSystem.GetPlacedItemCount();
        }

        /// <summary>
        /// Check if inventory is empty.
        /// </summary>
        public bool IsEmpty()
        {
            return GetItemCount() == 0;
        }

        #endregion

        #region Grid Operations

        /// <summary>
        /// Check if a grid position is valid.
        /// </summary>
        public bool IsValidGridPosition(Vector2Int position)
        {
            return gridSystem.IsValidGridPosition(position);
        }

        /// <summary>
        /// Convert world position to grid position.
        /// </summary>
        public Vector2Int WorldToGridPosition(Vector3 worldPosition)
        {
            return gridSystem.WorldToGridPosition(worldPosition);
        }

        /// <summary>
        /// Convert grid position to world position.
        /// </summary>
        public Vector3 GridToWorldPosition(Vector2Int gridPosition)
        {
            return gridSystem.GridToWorldPosition(gridPosition);
        }

        /// <summary>
        /// Get the underlying grid system (for advanced usage).
        /// </summary>
        public GridPlacementSystem GetGridSystem()
        {
            return gridSystem;
        }

        #endregion

        #region Event Handlers

        private void HandleItemPlaced(PlacedItem item)
        {
            OnItemAdded?.Invoke(item);
            OnWeightChanged?.Invoke(GetCurrentWeight());

            if (EnableDebugLogging)
            {
                Log($"Item added: {item.ItemDefinition.ItemName} | Weight: {GetCurrentWeight()}/{MaxWeight}");
                Log($"  Anchor: {item.AnchorPosition}, Rotation: {item.Rotation}, OccupiedCells: {item.OccupiedCells.Count}");
            }
        }

        private void HandleItemRemoved(PlacedItem item)
        {
            OnItemRemoved?.Invoke(item);
            OnWeightChanged?.Invoke(GetCurrentWeight());

            if (EnableDebugLogging)
            {
                Log($"Item removed: {item.ItemDefinition.ItemName} | Weight: {GetCurrentWeight()}/{MaxWeight}");
            }
        }

        #endregion

        #region Debug Logging

        private void Log(string message)
        {
            Debug.Log($"[InventorySystem] {message}");
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[InventorySystem] {message}");
        }

        #endregion
    }
}
