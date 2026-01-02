using Inventory.Scripts.Core.Helper;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Draggable.Processors.Rollbacks
{
    // [CreateAssetMenu(menuName = "Inventory/Configuration/Providers/Processors/Rollback Item To Holder Middleware")]
    public class RollbackItemToHolder : ReleaseProcessor
    {
        protected override void HandleProcess(ReleaseContext ctx, ReleaseState finalState)
        {
            var selectedInventoryItem = ctx.PickupState.Item;

            var itemTable = selectedInventoryItem.ItemTable;
            
            var currentHolder = itemTable.CurrentHolder;

            currentHolder.Equip(itemTable, false);

            if (ctx.Debug)
            {
                Debug.Log(("Item rollbacked to holder. Holder: " + currentHolder).DraggableSystem());
            }
        }

        protected override bool ShouldProcess(ReleaseContext ctx, ReleaseState finalState)
        {
            var selectedInventoryItem = ctx.PickupState.Item;

            var itemTable = selectedInventoryItem.ItemTable;

            return itemTable.CurrentHolder != null;
        }
    }
}