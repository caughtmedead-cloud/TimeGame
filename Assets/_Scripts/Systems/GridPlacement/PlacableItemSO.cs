using System.Collections.Generic;
using UnityEngine;

namespace TimeGame.Systems.GridPlacement
{
    /// <summary>
    /// Abstract base class for any item that can be placed on a grid.
    /// 
    /// Extend this for:
    /// - InventoryItemSO (items in inventory)
    /// - BuildingPieceSO (base building components)
    /// - CraftingComponentSO (crafting bench slots)
    /// - etc.
    /// </summary>
    public abstract class PlacableItemSO : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("Unique name for this item type (e.g., 'Rifle', 'Wall', 'WorkbenchSlot')")]
        public string ItemName;

        [Header("Grid Dimensions")]
        [Tooltip("Width in grid cells when facing Down (0°)")]
        public int Width = 1;

        [Tooltip("Height in grid cells when facing Down (0°)")]
        public int Height = 1;

        [Header("Rotation")]
        [Tooltip("Can this item be rotated?")]
        public bool CanRotate = true;

        /// <summary>
        /// Get all grid positions this item occupies based on anchor and rotation.
        /// This handles dimension swapping for Left/Right rotations.
        /// 
        /// Rotation logic (borrowed from CodeMonkey):
        /// - Down/Up: Use normal width x height
        /// - Left/Right: Swap dimensions (height x width)
        /// </summary>
        public List<Vector2Int> GetGridPositionList(Vector2Int anchorPosition, GridDirection rotation)
        {
            List<Vector2Int> gridPositionList = new List<Vector2Int>();

            switch (rotation)
            {
                default:
                case GridDirection.Down:
                case GridDirection.Up:
                    // Normal orientation: width x height
                    for (int x = 0; x < Width; x++)
                    {
                        for (int y = 0; y < Height; y++)
                        {
                            gridPositionList.Add(anchorPosition + new Vector2Int(x, y));
                        }
                    }
                    break;

                case GridDirection.Left:
                case GridDirection.Right:
                    // Rotated 90°: swap dimensions (height x width)
                    for (int x = 0; x < Height; x++)
                    {
                        for (int y = 0; y < Width; y++)
                        {
                            gridPositionList.Add(anchorPosition + new Vector2Int(x, y));
                        }
                    }
                    break;
            }

            return gridPositionList;
        }

        /// <summary>
        /// Get the width at a specific rotation.
        /// Swaps with height for Left/Right rotations.
        /// </summary>
        public int GetRotatedWidth(GridDirection rotation)
        {
            switch (rotation)
            {
                default:
                case GridDirection.Down:
                case GridDirection.Up:
                    return Width;
                case GridDirection.Left:
                case GridDirection.Right:
                    return Height;
            }
        }

        /// <summary>
        /// Get the height at a specific rotation.
        /// Swaps with width for Left/Right rotations.
        /// </summary>
        public int GetRotatedHeight(GridDirection rotation)
        {
            switch (rotation)
            {
                default:
                case GridDirection.Down:
                case GridDirection.Up:
                    return Height;
                case GridDirection.Left:
                case GridDirection.Right:
                    return Width;
            }
        }

        /// <summary>
        /// Get rotation angle in degrees for visual representation.
        /// </summary>
        public float GetRotationAngle(GridDirection rotation)
        {
            switch (rotation)
            {
                default:
                case GridDirection.Down:  return 0f;
                case GridDirection.Left:  return 90f;
                case GridDirection.Up:    return 180f;
                case GridDirection.Right: return 270f;
            }
        }

        /// <summary>
        /// Rotate to the next direction (clockwise).
        /// </summary>
        public GridDirection GetNextRotation(GridDirection current)
        {
            if (!CanRotate) return current;

            switch (current)
            {
                default:
                case GridDirection.Down:  return GridDirection.Left;
                case GridDirection.Left:  return GridDirection.Up;
                case GridDirection.Up:    return GridDirection.Right;
                case GridDirection.Right: return GridDirection.Down;
            }
        }

        /// <summary>
        /// Validate this item definition.
        /// </summary>
        protected virtual void OnValidate()
        {
            // Ensure width/height are at least 1
            Width = Mathf.Max(1, Width);
            Height = Mathf.Max(1, Height);

            // Ensure name is set
            if (string.IsNullOrEmpty(ItemName))
            {
                ItemName = name; // Use asset name as fallback
            }
        }
    }
}
