using Inventory.Scripts.Core.Helper;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Draggable.Processors.Pickups
{
    // [CreateAssetMenu(menuName = "Inventory/Configuration/Providers/Processors/Pickup Item In Holder Processor")]
    public class PickupItemInHolder : PickupProcessor
    {
        protected override void HandleProcess(PickupContext ctx, PickupState finalState)
        {
            var itemHolderFromPickup = ctx.ItemHolderFromStartDragging;

            var itemTable = itemHolderFromPickup.PickUp();
            var pickupItem = itemTable?.GetAbstractItem();

            if (pickupItem == null)
            {
                Debug.LogError("Could not find abstract item in holder...");
                return;
            }

            var displayRotatedIcon = itemHolderFromPickup.DisplayRotatedIcon;

            switch (displayRotatedIcon)
            {
                case true when !pickupItem.ItemTable.IsRotated:
                case false when pickupItem.ItemTable.IsRotated:
                    pickupItem.Rotate();
                    break;
            }

            finalState.Item = pickupItem;

            if (ctx.Debug)
            {
                Debug.Log(("Picking item from holder. Item: " + pickupItem.ItemTable.ItemDataSo.DisplayName +
                           " Holder: " +
                           itemHolderFromPickup.name)
                    .DraggableSystem());
            }
        }

        protected override bool ShouldProcess(PickupContext ctx, PickupState finalState)
        {
            var selectedInventoryItem = ctx.SelectedInventoryItem;
            var holderFromPickup = ctx.ItemHolderFromStartDragging;

            return selectedInventoryItem == null && holderFromPickup != null && holderFromPickup.IsEquipped();
        }
    }
}