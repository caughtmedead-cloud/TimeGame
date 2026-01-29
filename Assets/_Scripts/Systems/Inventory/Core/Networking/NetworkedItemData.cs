using System;
using FishNet.Object.Synchronizing;

namespace NewThelos.Inventory.Networking
{
    /// <summary>
    /// Network-serializable item data for SyncList replication.
    /// Lightweight struct that represents an item instance over the network.
    /// </summary>
    [Serializable]
    public struct NetworkedItemData
    {
        /// <summary>
        /// Unique runtime identifier for this item instance
        /// </summary>
        public string instanceId;
        
        /// <summary>
        /// Item definition ID (references ItemDefinitionSO)
        /// </summary>
        public string itemDefinitionId;
        
        /// <summary>
        /// Which grid this item is in (e.g., "main_inventory", "backpack")
        /// </summary>
        public string gridId;
        
        /// <summary>
        /// Top-left X position in grid
        /// </summary>
        public int posX;
        
        /// <summary>
        /// Top-left Y position in grid
        /// </summary>
        public int posY;
        
        /// <summary>
        /// Is item rotated 90 degrees?
        /// </summary>
        public bool isRotated;
        
        /// <summary>
        /// Stack count
        /// </summary>
        public int stackCount;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public NetworkedItemData(string instanceId, string itemDefinitionId, string gridId, 
                                 int posX, int posY, bool isRotated, int stackCount)
        {
            this.instanceId = instanceId;
            this.itemDefinitionId = itemDefinitionId;
            this.gridId = gridId;
            this.posX = posX;
            this.posY = posY;
            this.isRotated = isRotated;
            this.stackCount = stackCount;
        }
        
        public override string ToString()
        {
            return $"[{itemDefinitionId}] in {gridId} at ({posX},{posY}) rot:{isRotated} stack:{stackCount}";
        }
    }
}