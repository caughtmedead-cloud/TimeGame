using Inventory.Scripts.Core.Grids.Helper;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;

namespace Inventory.Scripts.Core.Controllers.Highlights.Processors
{
    // [CreateAssetMenu(menuName = "Inventory/Configuration/Providers/Processors/Highlight Hover Stackable Item On Grid Processor")]
    public class HighlightHoverStackableItemOnGrid : HighlightProcessor
    {
        private AbstractItem _hoveredStackableItem;

        protected override void HandleProcess(HighlighterContext ctx, HighlighterState state)
        {
            if (ctx.SelectedAbstractGrid == null) return;

            var itemHover = GridHelper.GetItemHover(ctx.TileGridHelperSo, ctx.SelectedAbstractGrid);

            if (itemHover == null)
            {
                return;
            }

            var selectedInventoryItem = ctx.SelectedInventoryItem;

            var countableMetadata = itemHover.ItemTable.GetMetadata<CountableMetadata>();

            if (countableMetadata == null) return;

            if (selectedInventoryItem.ItemTable.ItemDataSo != itemHover.ItemTable.ItemDataSo) return;

            var inventorySettingsSo = ctx.InventorySettingsSo;

            itemHover.SetBackground(true, inventorySettingsSo.OnHoverStackableItem);
            state.Show = false;
        }

        protected override bool ShouldProcess(HighlighterContext ctx, HighlighterState state)
        {
            if (ctx.SelectedAbstractGrid == null) return true;

            return ctx.SelectedInventoryItem != null;
        }
    }
}