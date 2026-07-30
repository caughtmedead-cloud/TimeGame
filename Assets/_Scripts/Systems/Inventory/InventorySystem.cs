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
        public bool TryAddItem(InventoryItemSO item, Vector2Int position, GridDirection rotation, out PlacedItem placedItem, int stackCount = 1, bool allowAutoStack = true)
        {
            placedItem = null;

            if (item == null)
            {
                LogWarning("Cannot add null item");
                return false;
            }

            // If item is stackable and auto-stacking is allowed, first try to add to existing stacks
            if (allowAutoStack && item.IsStackable && stackCount > 0)
            {
                int remaining = TryAddToExistingStacks(item, stackCount);

                // If all items were added to existing stacks, we're done
                if (remaining == 0)
                {
                    // Find one of the stacks we added to (for the output parameter)
                    foreach (PlacedItem existingItem in gridSystem.GetAllPlacedItems())
                    {
                        if (existingItem.ItemDefinition == item)
                        {
                            placedItem = existingItem;
                            return true;
                        }
                    }
                    return true;
                }

                // Update stackCount to remaining amount that needs a new stack
                stackCount = remaining;
            }

            // Check weight limit
            if (UseWeightLimit && !CanAddWeight(item.Weight * stackCount))
            {
                if (EnableDebugLogging)
                {
                    LogWarning($"Cannot add {item.ItemName} x{stackCount} - would exceed weight limit ({GetCurrentWeight()}/{MaxWeight})");
                }
                return false;
            }

            // Try placement in grid system
            bool success = gridSystem.TryPlaceItem(item, position, rotation, out placedItem, stackCount);

            // Ensure storage containers always have an initialized (possibly empty) InventorySystem.
            if (success && placedItem != null && item.ProvidesStorage && placedItem.ContainerInventory == null)
                placedItem.ContainerInventory = new InventorySystem(
                    item.StorageGridSize.x, item.StorageGridSize.y,
                    CellSize, Vector3.zero, item.StorageMaxWeight);

            return success;
        }

        /// <summary>
        /// Try to add an item with a specific instance ID (for network replication / drag-drop lineage).
        /// stackCount defaults to 1 but must be passed explicitly when dragging stacks cross-grid.
        /// </summary>
        public bool TryAddItem(Guid instanceID, InventoryItemSO item, Vector2Int position, GridDirection rotation, out PlacedItem placedItem, int stackCount = 1)
        {
            placedItem = null;

            if (item == null)
            {
                LogWarning("Cannot add null item");
                return false;
            }

            // Check weight limit (use full stack weight)
            if (UseWeightLimit && !CanAddWeight(item.Weight * Mathf.Max(1, stackCount)))
            {
                if (EnableDebugLogging)
                {
                    LogWarning($"Cannot add {item.ItemName} x{stackCount} - would exceed weight limit ({GetCurrentWeight()}/{MaxWeight})");
                }
                return false;
            }

            // Try placement in grid system with specific ID and stack count
            bool success = gridSystem.TryPlaceItem(instanceID, item, position, rotation, out placedItem, stackCount);

            // Ensure storage containers always have an initialized (possibly empty) InventorySystem.
            if (success && placedItem != null && item.ProvidesStorage && placedItem.ContainerInventory == null)
                placedItem.ContainerInventory = new InventorySystem(
                    item.StorageGridSize.x, item.StorageGridSize.y,
                    CellSize, Vector3.zero, item.StorageMaxWeight);

            return success;
        }

        /// <summary>
        /// Try to add items to existing stacks of the same type.
        /// Returns the number of items that couldn't be added to existing stacks.
        ///
        /// NOTE: This is the basic version for simple stacking.
        /// For durability/modification support, override PlacedItem.CanMergeWith()
        /// or use a more sophisticated stacking system.
        /// </summary>
        private int TryAddToExistingStacks(InventoryItemSO item, int count)
        {
            if (!item.IsStackable || count <= 0)
                return count;

            int remaining = count;

            // Find all existing stacks of this item
            foreach (PlacedItem existingItem in gridSystem.GetAllPlacedItems())
            {
                if (existingItem.ItemDefinition == item)
                {
                    // Calculate how many we can add to this stack
                    int spaceInStack = item.MaxStackSize - existingItem.StackCount;
                    if (spaceInStack > 0)
                    {
                        int toAdd = Mathf.Min(spaceInStack, remaining);
                        existingItem.AddToStack(toAdd);
                        remaining -= toAdd;

                        // Notify weight bar — AddToStack bypasses the grid event pipeline
                        OnWeightChanged?.Invoke(GetCurrentWeight());

                        if (EnableDebugLogging)
                        {
                            Log($"Added {toAdd} {item.ItemName} to existing stack (now {existingItem.StackCount}/{item.MaxStackSize})");
                        }

                        if (remaining == 0)
                            break;
                    }
                }
            }

            return remaining;
        }

        /// <summary>
        /// Advanced version: Try to add a specific PlacedItem (with data) to existing stacks.
        /// Uses PlacedItem.CanMergeWith() to respect durability and modifications.
        /// Returns true if fully merged, false if needs a new stack.
        /// </summary>
        public bool TryMergePlacedItem(PlacedItem itemToMerge, out int remainingCount)
        {
            remainingCount = itemToMerge.StackCount;
            InventoryItemSO itemDef = itemToMerge.ItemDefinition as InventoryItemSO;

            if (itemDef == null || !itemDef.IsStackable)
            {
                return false; // Can't merge non-stackable items
            }

            // Find compatible stacks
            foreach (PlacedItem existingItem in gridSystem.GetAllPlacedItems())
            {
                if (existingItem.CanMergeWith(itemToMerge))
                {
                    int spaceInStack = itemDef.MaxStackSize - existingItem.StackCount;
                    if (spaceInStack > 0)
                    {
                        int toAdd = Mathf.Min(spaceInStack, remainingCount);
                        existingItem.AddToStack(toAdd);
                        remainingCount -= toAdd;

                        // Notify weight bar — AddToStack bypasses the grid event pipeline
                        OnWeightChanged?.Invoke(GetCurrentWeight());

                        if (EnableDebugLogging)
                        {
                            Log($"Merged {toAdd} {itemDef.ItemName} into compatible stack (now {existingItem.StackCount}/{itemDef.MaxStackSize})");
                        }

                        if (remainingCount == 0)
                            return true; // Fully merged
                    }
                }
            }

            return remainingCount == 0;
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
        /// Moves an already-placed item to a new position without destroying the
        /// <see cref="PlacedItem"/> object.  Preserves all item state:
        /// <c>ItemInstances</c>, <c>ContainerInventory</c>, <c>SplitFromInstanceID</c>,
        /// and any external C# references to the object stay valid.
        ///
        /// Use this instead of <c>RemoveItem + TryAddItem</c> for any same-inventory
        /// reposition (e.g. drag-drop within the same grid or within the same nested container).
        ///
        /// Returns <c>false</c> without side-effects if the item is not in this inventory
        /// or if the target cells are blocked by a different item.
        /// </summary>
        public bool RepositionItem(Guid instanceID, Vector2Int newPosition, GridDirection newRotation)
        {
            return gridSystem.RepositionItem(instanceID, newPosition, newRotation);
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
        /// Get current total weight of all items, including contents of any nested
        /// containers (bags inside bags, etc.), recursively.
        /// </summary>
        public float GetCurrentWeight()
        {
            float totalWeight = 0f;

            foreach (PlacedItem placedItem in gridSystem.GetAllPlacedItems())
            {
                // ContainerHelper.GetTotalWeight handles recursion into nested containers
                totalWeight += ContainerHelper.GetTotalWeight(placedItem);
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

            // If the placed item is a container, bubble its weight changes up to us
            // so our weight bar stays accurate when items are added/removed inside it.
            SubscribeToContainerWeight(item);

            OnWeightChanged?.Invoke(GetCurrentWeight());

            if (EnableDebugLogging)
            {
                Log($"Item added: {item.ItemDefinition.ItemName} | Weight: {GetCurrentWeight()}/{MaxWeight}");
                Log($"  Anchor: {item.AnchorPosition}, Rotation: {item.Rotation}, OccupiedCells: {item.OccupiedCells.Count}");
            }
        }

        private void HandleItemRemoved(PlacedItem item)
        {
            // Stop listening to the removed container's weight changes
            UnsubscribeFromContainerWeight(item);

            OnItemRemoved?.Invoke(item);
            OnWeightChanged?.Invoke(GetCurrentWeight());

            if (EnableDebugLogging)
            {
                Log($"Item removed: {item.ItemDefinition.ItemName} | Weight: {GetCurrentWeight()}/{MaxWeight}");
            }
        }

        /// <summary>
        /// Subscribe to a placed item's ContainerInventory weight changes AND to its
        /// OnContainerInventoryChanged event so we re-wire automatically whenever the
        /// ContainerInventory property is swapped (cross-grid drag, equip/unequip, etc.).
        /// </summary>
        private void SubscribeToContainerWeight(PlacedItem item)
        {
            if (item == null) return;

            // Listen for future ContainerInventory swaps on this item
            item.OnContainerInventoryChanged -= OnItemContainerInventoryChanged;
            item.OnContainerInventoryChanged += OnItemContainerInventoryChanged;

            // Wire up the current ContainerInventory if one is already assigned
            if (item.ContainerInventory != null)
            {
                item.ContainerInventory.OnWeightChanged -= OnNestedWeightChanged;
                item.ContainerInventory.OnWeightChanged += OnNestedWeightChanged;
            }
        }

        private void UnsubscribeFromContainerWeight(PlacedItem item)
        {
            if (item == null) return;

            item.OnContainerInventoryChanged -= OnItemContainerInventoryChanged;

            if (item.ContainerInventory != null)
                item.ContainerInventory.OnWeightChanged -= OnNestedWeightChanged;
        }

        /// <summary>
        /// Called when a held item's ContainerInventory property is reassigned.
        /// Unsubscribes from the old system and subscribes to the new one.
        /// </summary>
        private void OnItemContainerInventoryChanged(InventorySystem oldSystem, InventorySystem newSystem)
        {
            if (oldSystem != null)
                oldSystem.OnWeightChanged -= OnNestedWeightChanged;

            if (newSystem != null)
            {
                newSystem.OnWeightChanged -= OnNestedWeightChanged;
                newSystem.OnWeightChanged += OnNestedWeightChanged;
            }

            // Refresh our own weight bar since the nested total just changed
            OnWeightChanged?.Invoke(GetCurrentWeight());
        }

        /// <summary>
        /// Fired when any directly-held container's contents change weight.
        /// Re-broadcasts this system's total (which now includes the nested change).
        /// </summary>
        private void OnNestedWeightChanged(float _)
        {
            OnWeightChanged?.Invoke(GetCurrentWeight());
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
