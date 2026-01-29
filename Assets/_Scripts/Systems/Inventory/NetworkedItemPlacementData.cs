using System;
using UnityEngine;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Network-synchronized data for a single inventory item placement.
    /// This struct is stored in a SyncList to replicate inventory state across the network.
    /// </summary>
    public struct NetworkedItemPlacementData
    {
        /// <summary>
        /// Name of the ItemTetrisSO asset (e.g., "Rifle", "Medkit").
        /// Server sends this string, clients look it up in the registry.
        /// </summary>
        public string itemSOName;

        /// <summary>
        /// Grid position of the item's anchor point (bottom-left cell).
        /// CodeMonkey's system handles the rest of the multi-cell placement from this anchor.
        /// </summary>
        public Vector2Int gridPosition;

        /// <summary>
        /// Rotation direction index (0-3): Down=0, Left=1, Up=2, Right=3.
        /// Maps directly to PlacedObjectTypeSO.Dir enum.
        /// </summary>
        public int directionIndex;

        /// <summary>
        /// Unique identifier for this specific item instance.
        /// Used to track items for removal operations.
        /// </summary>
        public Guid itemUID;

        public NetworkedItemPlacementData(string itemName, Vector2Int position, int direction, Guid uid)
        {
            itemSOName = itemName;
            gridPosition = position;
            directionIndex = direction;
            itemUID = uid;
        }
    }
}
