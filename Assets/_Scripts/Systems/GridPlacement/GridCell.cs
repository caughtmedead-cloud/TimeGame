using UnityEngine;

namespace TimeGame.Systems.GridPlacement
{
    /// <summary>
    /// Represents a single cell in the grid.
    /// Tracks what object (if any) occupies this cell.
    /// </summary>
    public class GridCell
    {
        private readonly int x;
        private readonly int y;

        /// <summary>
        /// The object placement that occupies this cell.
        /// Null if cell is empty.
        /// Multiple cells can reference the same placement (for multi-cell objects).
        /// </summary>
        public ObjectPlacement OccupyingPlacement { get; private set; }

        public GridCell(int x, int y)
        {
            this.x = x;
            this.y = y;
            OccupyingPlacement = null;
        }

        /// <summary>
        /// Grid position of this cell
        /// </summary>
        public Vector2Int Position => new Vector2Int(x, y);

        /// <summary>
        /// Is this cell available for placement?
        /// </summary>
        public bool IsAvailable => OccupyingPlacement == null;

        /// <summary>
        /// Is this cell occupied by any object?
        /// </summary>
        public bool IsOccupied => OccupyingPlacement != null;

        /// <summary>
        /// Set which object placement occupies this cell
        /// </summary>
        public void SetOccupyingPlacement(ObjectPlacement placement)
        {
            OccupyingPlacement = placement;
        }

        /// <summary>
        /// Clear this cell (make it available)
        /// </summary>
        public void Clear()
        {
            OccupyingPlacement = null;
        }

        public override string ToString()
        {
            return $"Cell({x},{y}) - {(IsOccupied ? $"Occupied by {OccupyingPlacement.PlaceableObject.ObjectTypeName}" : "Empty")}";
        }
    }
}
