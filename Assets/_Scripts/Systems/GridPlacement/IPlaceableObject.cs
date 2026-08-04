using System.Collections.Generic;
using UnityEngine;

namespace TimeGame.Systems.GridPlacement
{
    /// <summary>
    /// Interface for any object that can be placed on a grid.
    /// Implement this for inventory items, buildings, crafting components, etc.
    /// </summary>
    public interface IPlaceableObject
    {
        /// <summary>
        /// Unique identifier for this object type (e.g., "Rifle", "WallSegment")
        /// </summary>
        string ObjectTypeName { get; }

        /// <summary>
        /// Width in grid cells (horizontal size)
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Height in grid cells (vertical size)
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Get all grid positions this object occupies based on anchor position and rotation.
        /// Anchor is typically bottom-left corner.
        /// </summary>
        /// <param name="anchorPosition">The anchor grid position</param>
        /// <param name="rotation">Rotation direction (0-3)</param>
        /// <returns>List of all occupied grid positions</returns>
        List<Vector2Int> GetOccupiedCells(Vector2Int anchorPosition, int rotation);

        /// <summary>
        /// Can this object be rotated?
        /// </summary>
        bool CanRotate { get; }
    }
}
