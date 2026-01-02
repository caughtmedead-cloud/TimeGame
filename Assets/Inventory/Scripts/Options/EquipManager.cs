using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.Items;
using UnityEngine;

namespace Inventory.Scripts.Options
{
    public class EquipManager : OptionExecutionManager
    {
        protected override void HandleOptionExecution(AbstractItem itemUi)
        {
            var itemTable = itemUi.ItemTable;
            var itemDataTypeSo = itemTable.ItemDataSo.ItemDataTypeSo;

            var holderToEquip = StaticInventoryContext.GetInitializedHolders().Find(holder =>
                !holder.IsEquipped() && holder is ItemHolder itemHolder && itemHolder.ItemDataTypeSo == itemDataTypeSo);

            if (holderToEquip == null)
            {
                Debug.Log(("Not found any holder free and with the same item type. Type: " + itemDataTypeSo).Info());
                return;
            }

            var equippableMessages = StaticInventoryContext.InventorySupplierSo.TryEquipItem(
                itemTable,
                holderToEquip
            );

            if (equippableMessages == HolderResponse.Equipped)
            {
                Debug.Log("Equipped item!".Info());
            }
        }
    }
}