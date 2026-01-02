using Inventory.Scripts.Core.ScriptableObjects.Items;

namespace Inventory.Scripts.Core.Items.Metadata
{
    public static class StaticMetadataCreator
    {
        public static InventoryMetadata CreateMetadata(ItemTable itemTable)
        {
            var itemDataSo = itemTable.ItemDataSo;

            var metadata = CreateInstancedMetadata(itemDataSo);
            metadata.Init(itemTable);

            return metadata;
        }

        private static InventoryMetadata CreateInstancedMetadata(ItemDataSo itemDataSo)
        {
            return itemDataSo switch
            {
                ItemContainerDataSo => new ContainerMetadata(),
                ItemStackableDataSo => new CountableMetadata(),
                _ => new InventoryMetadata()
            };
        }
    }
}