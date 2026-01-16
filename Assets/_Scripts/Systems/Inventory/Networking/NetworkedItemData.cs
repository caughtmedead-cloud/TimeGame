using System;

namespace NewThelos.Systems.Inventory.Networking
{
    /// <summary>
    /// Serializable struct for transmitting item data over FishNet network.
    /// This bridges UGI's ScriptableObject-based architecture with FishNet's networking layer.
    /// 
    /// Design Philosophy:
    /// - Uses string identifiers for ItemDataSo references (resolved via ItemDataRegistry)
    /// - Keeps data minimal for efficient network transmission
    /// - Contains all information needed to reconstruct ItemTable on client
    /// </summary>
    [Serializable]
    public struct NetworkedItemData
    {
        /// <summary>
        /// Unique identifier for this specific item instance (not the item type).
        /// Used to track individual items across the network.
        /// </summary>
        public string itemUID;
        
        /// <summary>
        /// String name of the ItemDataSo ScriptableObject.
        /// The ItemDataRegistry converts this back to an ItemDataSo reference on clients.
        /// </summary>
        public string itemDataSOName;
        
        /// <summary>
        /// Which grid this item is in (0 = player inventory, 1+ = containers, etc.)
        /// </summary>
        public int gridIndex;
        
        /// <summary>
        /// X position in the grid (top-left corner of item).
        /// </summary>
        public int posX;
        
        /// <summary>
        /// Y position in the grid (top-left corner of item).
        /// </summary>
        public int posY;
        
        /// <summary>
        /// Whether the item is rotated 90 degrees.
        /// </summary>
        public bool isRotated;
        
        /// <summary>
        /// Stack count for stackable items.
        /// For non-stackable items, this should always be 1.
        /// </summary>
        public int stackCount;
        
        /// <summary>
        /// Constructor for creating NetworkedItemData from ItemTable.
        /// </summary>
        /// <param name="uid">Unique item instance identifier</param>
        /// <param name="itemDataName">Name of the ItemDataSo ScriptableObject</param>
        /// <param name="grid">Grid index</param>
        /// <param name="x">X position</param>
        /// <param name="y">Y position</param>
        /// <param name="rotated">Is item rotated</param>
        /// <param name="stack">Stack count</param>
        public NetworkedItemData(string uid, string itemDataName, int grid, int x, int y, bool rotated, int stack)
        {
            itemUID = uid;
            itemDataSOName = itemDataName;
            gridIndex = grid;
            posX = x;
            posY = y;
            isRotated = rotated;
            stackCount = stack;
        }
        
        /// <summary>
        /// Returns a readable string representation for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"[NetworkedItemData] {itemDataSOName} (UID: {itemUID}) at Grid{gridIndex}[{posX},{posY}] " +
                   $"Rotated:{isRotated} Stack:{stackCount}";
        }
    }
}