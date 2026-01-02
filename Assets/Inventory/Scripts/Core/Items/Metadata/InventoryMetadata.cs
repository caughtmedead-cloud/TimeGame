using System;

namespace Inventory.Scripts.Core.Items.Metadata
{
    [Serializable]
    public class InventoryMetadata
    {
        [NonSerialized] public ItemTable ItemTable;

        public void Init(ItemTable item)
        {
            ItemTable = item;
            Awake();
        }

        public virtual void Awake()
        {
        }

        /// <summary>
        /// This will be called once we load an inventory and Item is being constructed.
        /// This method should be used to evict load information, so if something in load property is not up to date, you can update here.
        /// Like new properties, or even hard item data so properties.
        /// </summary>
        public virtual void EvictLoad()
        {
        }
    }
}