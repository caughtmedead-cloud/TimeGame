using System;

namespace NewThelos.Inventory.Runtime
{
    /// <summary>
    /// Represents a single item instance in an inventory grid.
    /// This is a runtime data class - lightweight and serializable.
    /// 
    /// Based on UGI's ItemTable but simplified.
    /// </summary>
    [Serializable]
    public class InventoryItem
    {
        /// <summary>
        /// Unique runtime identifier for this specific item instance
        /// </summary>
        public string instanceId;
        
        /// <summary>
        /// References ItemDefinitionSO.itemId (the type of item)
        /// </summary>
        public string itemDefinitionId;
        
        /// <summary>
        /// Top-left position in grid (X coordinate)
        /// </summary>
        public int posX;
        
        /// <summary>
        /// Top-left position in grid (Y coordinate)
        /// </summary>
        public int posY;
        
        /// <summary>
        /// Is this item rotated 90 degrees?
        /// </summary>
        public bool isRotated;
        
        /// <summary>
        /// Stack count (if stackable)
        /// </summary>
        public int stackCount;
        
        /// <summary>
        /// Constructor for new item instance
        /// </summary>
        public InventoryItem(string itemDefinitionId, int posX, int posY, bool isRotated = false, int stackCount = 1)
        {
            this.instanceId = Guid.NewGuid().ToString();
            this.itemDefinitionId = itemDefinitionId;
            this.posX = posX;
            this.posY = posY;
            this.isRotated = isRotated;
            this.stackCount = stackCount;
        }
        
        /// <summary>
        /// Constructor for deserialization
        /// </summary>
        public InventoryItem(string instanceId, string itemDefinitionId, int posX, int posY, bool isRotated, int stackCount)
        {
            this.instanceId = instanceId;
            this.itemDefinitionId = itemDefinitionId;
            this.posX = posX;
            this.posY = posY;
            this.isRotated = isRotated;
            this.stackCount = stackCount;
        }
        
        public override string ToString()
        {
            return $"[{itemDefinitionId}] at ({posX},{posY}) rotated:{isRotated} stack:{stackCount}";
        }
    }
}