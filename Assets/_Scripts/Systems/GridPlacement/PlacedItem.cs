using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimeGame.Systems.GridPlacement
{
    /// <summary>
    /// Represents a runtime instance of a placed item in the grid.
    /// This is the "physical" object that occupies cells.
    /// Can be extended for inventory items, buildings, etc.
    /// </summary>
    public class PlacedItem
    {
        /// <summary>
        /// Unique ID for this placed item instance.
        /// Used for networking and removal operations.
        /// </summary>
        public Guid InstanceID { get; private set; }

        /// <summary>
        /// The item definition (ScriptableObject) this placement represents.
        /// </summary>
        public PlacableItemSO ItemDefinition { get; private set; }

        /// <summary>
        /// Anchor position in the grid (typically bottom-left corner).
        /// </summary>
        public Vector2Int AnchorPosition { get; private set; }

        /// <summary>
        /// Current rotation direction.
        /// </summary>
        public GridDirection Rotation { get; private set; }

        /// <summary>
        /// List of all grid cells this item occupies.
        /// Cached for performance.
        /// </summary>
        public List<Vector2Int> OccupiedCells { get; private set; }

        /// <summary>
        /// Optional: Reference to visual representation (MonoBehaviour).
        /// Can be null if this is a data-only placement.
        /// </summary>
        public MonoBehaviour VisualRepresentation { get; set; }

        public PlacedItem(PlacableItemSO itemDefinition, Vector2Int anchorPosition, GridDirection rotation)
        {
            InstanceID = Guid.NewGuid();
            ItemDefinition = itemDefinition;
            AnchorPosition = anchorPosition;
            Rotation = rotation;
            
            // Calculate and cache occupied cells
            OccupiedCells = itemDefinition.GetGridPositionList(anchorPosition, rotation);
            
            VisualRepresentation = null;
        }

        /// <summary>
        /// Constructor with specific instance ID (used for network replication).
        /// </summary>
        public PlacedItem(Guid instanceID, PlacableItemSO itemDefinition, Vector2Int anchorPosition, GridDirection rotation)
        {
            InstanceID = instanceID;
            ItemDefinition = itemDefinition;
            AnchorPosition = anchorPosition;
            Rotation = rotation;
            
            // Calculate and cache occupied cells
            OccupiedCells = itemDefinition.GetGridPositionList(anchorPosition, rotation);
            
            VisualRepresentation = null;
        }

        /// <summary>
        /// Check if this placement contains a specific grid position.
        /// </summary>
        public bool ContainsPosition(Vector2Int position)
        {
            return OccupiedCells.Contains(position);
        }

        public override string ToString()
        {
            return $"{ItemDefinition.ItemName} at {AnchorPosition} facing {Rotation} (ID: {InstanceID})";
        }
    }
}
