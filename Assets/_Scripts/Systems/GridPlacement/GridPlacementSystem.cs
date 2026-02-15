using System;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;

namespace TimeGame.Systems.GridPlacement
{
    /// <summary>
    /// Abstract core grid placement system.
    /// Pure logic - no UI, no input, no networking.
    /// 
    /// Can be extended for:
    /// - Inventory systems
    /// - Base building
    /// - Crafting benches
    /// - Any grid-based placement mechanic
    /// 
    /// Uses CodeMonkey's Grid<T> class internally.
    /// </summary>
    public class GridPlacementSystem
    {
        /// <summary>
        /// The underlying grid. Uses CodeMonkey's excellent Grid<T> implementation.
        /// </summary>
        private Grid<GridCell> grid;

        /// <summary>
        /// All currently placed items. Key is InstanceID.
        /// </summary>
        private Dictionary<Guid, PlacedItem> placedItems;

        /// <summary>
        /// Enable/disable debug logging.
        /// </summary>
        public bool EnableDebugLogging { get; set; }

        /// <summary>
        /// Grid width (number of cells horizontally).
        /// </summary>
        public int Width => grid.GetWidth();

        /// <summary>
        /// Grid height (number of cells vertically).
        /// </summary>
        public int Height => grid.GetHeight();

        /// <summary>
        /// Cell size in world/UI units.
        /// </summary>
        public float CellSize => grid.GetCellSize();

        /// <summary>
        /// Event fired when an item is placed.
        /// </summary>
        public event Action<PlacedItem> OnItemPlaced;

        /// <summary>
        /// Event fired when an item is removed.
        /// </summary>
        public event Action<PlacedItem> OnItemRemoved;

        /// <summary>
        /// Constructor: Initialize the grid.
        /// </summary>
        /// <param name="width">Grid width in cells</param>
        /// <param name="height">Grid height in cells</param>
        /// <param name="cellSize">Size of each cell in world/UI units</param>
        /// <param name="origin">Origin position (bottom-left corner)</param>
        public GridPlacementSystem(int width, int height, float cellSize, Vector3 origin)
        {
            // Create grid using CodeMonkey's Grid<T> class
            grid = new Grid<GridCell>(width, height, cellSize, origin, 
                (Grid<GridCell> g, int x, int y) => new GridCell(x, y));

            placedItems = new Dictionary<Guid, PlacedItem>();
            EnableDebugLogging = false;
        }

        #region Placement Operations

        /// <summary>
        /// Try to place an item at the specified position.
        /// Returns true if placement succeeded, false if blocked.
        /// </summary>
        public bool TryPlaceItem(PlacableItemSO itemDefinition, Vector2Int anchorPosition, GridDirection rotation, out PlacedItem placedItem)
        {
            placedItem = null;

            // Validate input
            if (itemDefinition == null)
            {
                LogWarning("Cannot place null item definition");
                return false;
            }

            // Check if can place
            if (!CanPlaceItem(itemDefinition, anchorPosition, rotation))
            {
                if (EnableDebugLogging)
                {
                    LogWarning($"Cannot place {itemDefinition.ItemName} at {anchorPosition} - space occupied or out of bounds");
                }
                return false;
            }

            // Create placed item instance
            placedItem = new PlacedItem(itemDefinition, anchorPosition, rotation);

            // Occupy all cells
            if (EnableDebugLogging)
            {
                Log($"[PLACEMENT] Item {itemDefinition.ItemName} at {anchorPosition} rotation {rotation}");
                Log($"[PLACEMENT]   OccupiedCells count: {placedItem.OccupiedCells.Count}");
                Log($"[PLACEMENT]   OccupiedCells list: [{string.Join(", ", placedItem.OccupiedCells)}]");
            }

            foreach (Vector2Int cellPos in placedItem.OccupiedCells)
            {
                GridCell cell = grid.GetGridObject(cellPos.x, cellPos.y);

                if (EnableDebugLogging)
                {
                    Log($"[PLACEMENT]     Occupying cell {cellPos} (was {(cell.IsAvailable ? "available" : "occupied")})");
                }

                cell.SetOccupyingItem(placedItem);
            }

            // Track placement
            placedItems[placedItem.InstanceID] = placedItem;

            // Fire event
            OnItemPlaced?.Invoke(placedItem);

            if (EnableDebugLogging)
            {
                Log($"Placed {itemDefinition.ItemName} at {anchorPosition} facing {rotation} (ID: {placedItem.InstanceID})");
                Log($"[PLACEMENT] Total items in grid now: {placedItems.Count}");
            }

            return true;
        }

        /// <summary>
        /// Try to place an item with a specific instance ID (for network replication).
        /// </summary>
        public bool TryPlaceItem(Guid instanceID, PlacableItemSO itemDefinition, Vector2Int anchorPosition, GridDirection rotation, out PlacedItem placedItem)
        {
            placedItem = null;

            // Validate input
            if (itemDefinition == null)
            {
                LogWarning("Cannot place null item definition");
                return false;
            }

            // Check if can place
            if (!CanPlaceItem(itemDefinition, anchorPosition, rotation))
            {
                if (EnableDebugLogging)
                {
                    LogWarning($"Cannot place {itemDefinition.ItemName} at {anchorPosition} - space occupied or out of bounds");
                }
                return false;
            }

            // Create placed item instance with specific ID
            placedItem = new PlacedItem(instanceID, itemDefinition, anchorPosition, rotation);

            // Occupy all cells
            if (EnableDebugLogging)
            {
                Log($"[PLACEMENT] Item {itemDefinition.ItemName} at {anchorPosition} rotation {rotation}");
                Log($"[PLACEMENT]   OccupiedCells count: {placedItem.OccupiedCells.Count}");
                Log($"[PLACEMENT]   OccupiedCells list: [{string.Join(", ", placedItem.OccupiedCells)}]");
            }

            foreach (Vector2Int cellPos in placedItem.OccupiedCells)
            {
                GridCell cell = grid.GetGridObject(cellPos.x, cellPos.y);

                if (EnableDebugLogging)
                {
                    Log($"[PLACEMENT]     Occupying cell {cellPos} (was {(cell.IsAvailable ? "available" : "occupied")})");
                }

                cell.SetOccupyingItem(placedItem);
            }

            // Track placement
            placedItems[placedItem.InstanceID] = placedItem;

            // Fire event
            OnItemPlaced?.Invoke(placedItem);

            if (EnableDebugLogging)
            {
                Log($"Placed {itemDefinition.ItemName} at {anchorPosition} facing {rotation} (ID: {placedItem.InstanceID})");
                Log($"[PLACEMENT] Total items in grid now: {placedItems.Count}");
            }

            return true;
        }

        /// <summary>
        /// Check if an item can be placed at the specified position.
        /// Does NOT actually place the item - just validates.
        /// </summary>
        public bool CanPlaceItem(PlacableItemSO itemDefinition, Vector2Int anchorPosition, GridDirection rotation)
        {
            return CanPlaceItem(itemDefinition, anchorPosition, rotation, Guid.Empty);
        }

        /// <summary>
        /// Check if an item can be placed at a position, optionally ignoring a specific item's cells.
        /// Useful for same-grid drag operations where we want to check if we can move an item to a new position.
        /// </summary>
        /// <param name="itemDefinition">The item to place</param>
        /// <param name="anchorPosition">Where to place it</param>
        /// <param name="rotation">Rotation</param>
        /// <param name="ignoreItemID">If not Guid.Empty, cells occupied by this item will be treated as available</param>
        public bool CanPlaceItem(PlacableItemSO itemDefinition, Vector2Int anchorPosition, GridDirection rotation, Guid ignoreItemID)
        {
            if (itemDefinition == null) return false;

            // Get all cells this placement would occupy
            List<Vector2Int> requiredCells = itemDefinition.GetGridPositionList(anchorPosition, rotation);

            if (EnableDebugLogging)
            {
                Log($"[CAN_PLACE] Checking {itemDefinition.ItemName} at {anchorPosition} rotation {rotation}");
                Log($"[CAN_PLACE]   RequiredCells: [{string.Join(", ", requiredCells)}]");
            }

            // Check each cell
            foreach (Vector2Int cellPos in requiredCells)
            {
                // Check bounds
                if (!IsValidGridPosition(cellPos))
                {
                    if (EnableDebugLogging)
                    {
                        Log($"[CAN_PLACE]   Cell {cellPos} OUT OF BOUNDS");
                    }
                    return false;
                }

                // Check availability
                GridCell cell = grid.GetGridObject(cellPos.x, cellPos.y);

                if (EnableDebugLogging)
                {
                    Log($"[CAN_PLACE]   Cell {cellPos}: {(cell.IsAvailable ? "AVAILABLE" : $"OCCUPIED by {cell.OccupyingItem?.ItemDefinition?.ItemName}")}");
                }

                // If cell is occupied, check if it's occupied by the item we're ignoring
                if (!cell.IsAvailable)
                {
                    // If we're ignoring an item and this cell is occupied by that item, treat it as available
                    if (ignoreItemID != Guid.Empty && cell.OccupyingItem != null && cell.OccupyingItem.InstanceID == ignoreItemID)
                    {
                        if (EnableDebugLogging)
                        {
                            Log($"[CAN_PLACE]   Cell {cellPos}: Ignoring occupation by {ignoreItemID}");
                        }
                        continue; // This cell is OK - it's occupied by the item we're moving
                    }

                    if (EnableDebugLogging)
                    {
                        Log($"[CAN_PLACE]   BLOCKED at {cellPos}");
                    }
                    return false; // Cell is blocked by a different item
                }
            }

            if (EnableDebugLogging)
            {
                Log($"[CAN_PLACE] Result: TRUE");
            }
            return true;
        }

        /// <summary>
        /// Remove an item at the specified grid position.
        /// Returns true if an item was removed.
        /// </summary>
        public bool RemoveItemAt(Vector2Int position)
        {
            // Find which item occupies this position
            if (!IsValidGridPosition(position))
            {
                return false;
            }

            GridCell cell = grid.GetGridObject(position.x, position.y);
            if (cell.IsAvailable)
            {
                // No item here
                return false;
            }

            PlacedItem itemToRemove = cell.OccupyingItem;
            return RemoveItem(itemToRemove);
        }

        /// <summary>
        /// Remove a specific placed item by instance ID.
        /// Returns true if item was found and removed.
        /// </summary>
        public bool RemoveItem(Guid instanceID)
        {
            if (!placedItems.TryGetValue(instanceID, out PlacedItem item))
            {
                return false;
            }

            return RemoveItem(item);
        }

        /// <summary>
        /// Remove a specific placed item instance.
        /// </summary>
        private bool RemoveItem(PlacedItem item)
        {
            if (item == null) return false;

            // Clear all occupied cells
            foreach (Vector2Int cellPos in item.OccupiedCells)
            {
                if (IsValidGridPosition(cellPos))
                {
                    GridCell cell = grid.GetGridObject(cellPos.x, cellPos.y);
                    cell.Clear();
                }
            }

            // Remove from tracking
            placedItems.Remove(item.InstanceID);

            // Fire event
            OnItemRemoved?.Invoke(item);

            if (EnableDebugLogging)
            {
                Log($"Removed {item.ItemDefinition.ItemName} (ID: {item.InstanceID})");
            }

            return true;
        }

        /// <summary>
        /// Clear all placed items from the grid.
        /// </summary>
        public void ClearAll()
        {
            // Copy list to avoid modification during iteration
            List<PlacedItem> itemsToClear = new List<PlacedItem>(placedItems.Values);

            foreach (PlacedItem item in itemsToClear)
            {
                RemoveItem(item);
            }

            if (EnableDebugLogging)
            {
                Log("Cleared all items from grid");
            }
        }

        #endregion

        #region Query Operations

        /// <summary>
        /// Get the placed item at a specific grid position.
        /// Returns null if no item at that position.
        /// </summary>
        public PlacedItem GetItemAt(Vector2Int position)
        {
            if (!IsValidGridPosition(position))
            {
                return null;
            }

            GridCell cell = grid.GetGridObject(position.x, position.y);
            return cell.OccupyingItem;
        }

        /// <summary>
        /// Get a placed item by its instance ID.
        /// Returns null if not found.
        /// </summary>
        public PlacedItem GetItemByID(Guid instanceID)
        {
            placedItems.TryGetValue(instanceID, out PlacedItem item);
            return item;
        }

        /// <summary>
        /// Get all currently placed items.
        /// </summary>
        public IReadOnlyCollection<PlacedItem> GetAllPlacedItems()
        {
            return placedItems.Values;
        }

        /// <summary>
        /// Get the number of items currently placed.
        /// </summary>
        public int GetPlacedItemCount()
        {
            return placedItems.Count;
        }

        /// <summary>
        /// Check if a grid position is within valid bounds.
        /// </summary>
        public bool IsValidGridPosition(Vector2Int position)
        {
            return grid.IsValidGridPosition(position);
        }

        /// <summary>
        /// Convert world position to grid position.
        /// </summary>
        public Vector2Int WorldToGridPosition(Vector3 worldPosition)
        {
            grid.GetXY(worldPosition, out int x, out int y);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Convert grid position to world position (center of cell).
        /// </summary>
        public Vector3 GridToWorldPosition(Vector2Int gridPosition)
        {
            return grid.GetWorldPosition(gridPosition.x, gridPosition.y) + 
                   new Vector3(CellSize, CellSize) * 0.5f; // Center of cell
        }

        /// <summary>
        /// Get the underlying Grid<GridCell> (for advanced usage).
        /// </summary>
        public Grid<GridCell> GetGrid()
        {
            return grid;
        }

        #endregion

        #region Debug Logging

        private void Log(string message)
        {
            Debug.Log($"[GridPlacementSystem] {message}");
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[GridPlacementSystem] {message}");
        }

        /// <summary>
        /// Dump complete grid state for debugging.
        /// Shows all cells and what items occupy them.
        /// </summary>
        public void DumpGridState(string gridName)
        {
            Debug.Log($"====== GRID STATE DUMP: {gridName} ======");
            Debug.Log($"Grid size: {Width}x{Height} ({Width * Height} total cells)");
            Debug.Log($"Placed items count: {placedItems.Count}");

            // Show all placed items
            Debug.Log("--- Placed Items ---");
            foreach (var kvp in placedItems)
            {
                PlacedItem item = kvp.Value;
                Debug.Log($"  {item.ItemDefinition.ItemName} (ID: {item.InstanceID})");
                Debug.Log($"    Position: {item.AnchorPosition}, Rotation: {item.Rotation}");
                Debug.Log($"    OccupiedCells: {item.OccupiedCells.Count} cells: [{string.Join(", ", item.OccupiedCells)}]");
            }

            // Show cell-by-cell state
            Debug.Log("--- Cell State (bottom-to-top, left-to-right) ---");
            for (int y = 0; y < Height; y++)
            {
                string rowState = $"Row {y}: ";
                for (int x = 0; x < Width; x++)
                {
                    GridCell cell = grid.GetGridObject(x, y);
                    if (cell.IsAvailable)
                    {
                        rowState += "[  ] ";
                    }
                    else
                    {
                        rowState += "[XX] ";
                    }
                }
                Debug.Log(rowState);
            }

            // Count occupied cells
            int occupiedCount = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    GridCell cell = grid.GetGridObject(x, y);
                    if (!cell.IsAvailable)
                    {
                        occupiedCount++;
                    }
                }
            }
            Debug.Log($"Total cells occupied: {occupiedCount}/{Width * Height}");
            Debug.Log($"============================================");
        }

        #endregion
    }
}
