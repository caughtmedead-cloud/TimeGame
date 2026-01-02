using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Items;
using UnityEngine;

namespace Inventory.Scripts.Options
{
    public class UnEquipManager : OptionExecutionManager
    {
        protected override void HandleOptionExecution(AbstractItem itemUi)
        {
            var itemTable = itemUi.ItemTable;

            var gridResponse = StaticInventoryContext.InventorySupplierSo
                .PlaceItemInPlayerInventory(itemTable);

            if (gridResponse != GridResponse.Inserted)
            {
                Debug.Log(
                    $"Cannot find any space on player inventory to unequip this item. Result: {gridResponse}".Info());
            }
        }
    }
}