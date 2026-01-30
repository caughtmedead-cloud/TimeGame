using UnityEngine;

namespace TimeGame.Systems.GridPlacement
{
    /// <summary>
    /// Represents a single cell in the grid.
    /// Tracks what item (if any) occupies this cell.
    /// Multiple cells can reference the same PlacedItem for multi-cell objects.
    /// </summary>
    public class GridCell
    {
        private readonly int x;
        private readonly int y;

        /// <summary>
        /// The placed item that occupies this cell.
        /// Null if cell is empty.
        /// For multi-cell items, multiple GridCells will reference the same PlacedItem.
        /// </summary>
        public PlacedItem OccupyingItem { get; private set; }

        public GridCell(int x, int y)
        {
            this.x = x;
            this.y = y;
            OccupyingItem = null;
        }

        /// <summary>
        /// Grid position of this cell.
        /// </summary>
        public Vector2Int Position => new Vector2Int(x, y);

        /// <summary>
        /// Is this cell available for placement?
        /// </summary>
        public bool IsAvailable => OccupyingItem == null;

        /// <summary>
        /// Is this cell occupied by any item?
        /// </summary>
        public bool IsOccupied => OccupyingItem != null;

        /// <summary>
        /// Set which item occupies this cell.
        /// </summary>
        public void SetOccupyingItem(PlacedItem item)
        {
            OccupyingItem = item;
        }

        /// <summary>
        /// Clear this cell (make it available).
        /// </summary>
        public void Clear()
        {
            OccupyingItem = null;
        }

        public override string ToString()
        {
            return $"Cell({x},{y}) - {(IsOccupied ? $"Occupied by {OccupyingItem.ItemDefinition.ItemName}" : "Empty")}";
        }
    }
}
