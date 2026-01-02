using System.Linq;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;
using UnityEngine;

namespace Inventory.Samples.Examples
{
    public class GetItemsFromContainerMetadata : MonoBehaviour
    {
        public void GetItemsFromContainerMetadataFromItemTable(ItemTable itemTable)
        {
            if (!itemTable.IsContainer()) return;

            if (itemTable.InventoryMetadata is not ContainerMetadata metadata) return;

            var allItemsFromContainerMetadata = metadata.Inventories
                .SelectMany(grid => grid.GetAllItemsFromGrid())
                .ToList();

            // Here is all items from Container Metadata
            // use the allItemsFromContainerMetadata variable
        }
    }
}