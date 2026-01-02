using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Draggable.Processors.Places
{
    // [CreateAssetMenu(menuName = "Inventory/Configuration/Providers/Processors/Place Item Hovering To Stack Middleware")]
    public class PlaceItemHoveringToStack : ReleaseProcessor
    {
        protected override void HandleProcess(ReleaseContext ctx, ReleaseState finalState)
        {
            var abstractItemHoveringOnGrid = GetItemHover(ctx);

            var itemTableHovering = abstractItemHoveringOnGrid.ItemTable;

            var countableMetadata = itemTableHovering.GetMetadata<CountableMetadata>();

            if (countableMetadata == null)
            {
                finalState.Placed = false;
                return;
            }

            var selectedInventoryItem = ctx.PickupState.Item;

            var couldCombine = countableMetadata.Combine(selectedInventoryItem.ItemTable);

            if (ctx.Debug)
            {
                Debug.Log(("Stacking item inside another. Item Stacked: " + itemTableHovering.ItemDataSo.DisplayName +
                           ", Result: " + couldCombine)
                    .DraggableSystem());
            }

            finalState.Placed = couldCombine;
        }

        private static AbstractItem GetItemHover(ReleaseContext ctx)
        {
            var tileGridHelperSo = ctx.TileGridHelperSo;

            var abstractGrid = ctx.SelectedAbstractGrid;

            var tileGridPositionByGrid = tileGridHelperSo.GetTileGridPositionByGridTable(abstractGrid.transform);

            return abstractGrid.Grid.GetItem(tileGridPositionByGrid.x, tileGridPositionByGrid.y)?.GetAbstractItem();
        }

        protected override bool ShouldProcess(ReleaseContext ctx, ReleaseState finalState)
        {
            if (ctx.SelectedAbstractGrid == null) return false;

            var abstractItemHovering = GetItemHover(ctx);

            if (abstractItemHovering == null) return false;

            var countableMetadata = abstractItemHovering.ItemTable?.GetMetadata<CountableMetadata>();

            return countableMetadata != null &&
                   abstractItemHovering.ItemTable != ctx.PickupState.Item.ItemTable;
        }
    }
}