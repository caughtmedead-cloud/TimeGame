using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.Items;
using UnityEngine;

namespace Inventory.Scripts.Core.Environment
{
    [DefaultExecutionOrder(5)]
    public class EnvironmentItemHolder : Holder
    {
        private void Awake()
        {
            holderData = new HolderData(uniqueUid.Uid);
        }

        protected void Start()
        {
            if (IsEquipped()) return;

            var itemTable = StaticInventoryContext.RetrieveItemByEnvironmentHolderId(uniqueUid.Uid);

            Equip(itemTable);
        }

        protected override void OnEquipItem(ItemTable itemTable)
        {
            base.OnEquipItem(itemTable);

            StaticInventoryContext.GroundItemsInventorySo.AddHolder(holderData);
        }

        protected override void OnUnEquipItem(ItemTable itemTable)
        {
            base.OnUnEquipItem(itemTable);

            StaticInventoryContext.GroundItemsInventorySo.RemoveHolder(holderData);
        }
    }
}