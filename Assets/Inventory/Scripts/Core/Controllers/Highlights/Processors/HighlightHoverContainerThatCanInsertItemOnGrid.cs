using Inventory.Scripts.Core.Grids.Helper;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;

namespace Inventory.Scripts.Core.Controllers.Highlights.Processors
{
    // [CreateAssetMenu(menuName = "Inventory/Configuration/Providers/Processors/Highlight Hover Container That Can Insert Item On Grid Processor")]
    public class HighlightHoverContainerThatCanInsertItemOnGrid : HighlightProcessor
    {
        private AbstractItem _hoveredContainerWithSpace;

        protected override void HandleProcess(HighlighterContext ctx, HighlighterState state)
        {
            ResetHoveredBackgroundItem();

            if (ctx.SelectedAbstractGrid == null) return;

            var itemHover = GridHelper.GetItemHover(ctx.TileGridHelperSo, ctx.SelectedAbstractGrid);

            if (itemHover == null)
            {
                return;
            }

            var selectedInventoryItem = ctx.SelectedInventoryItem;
            _hoveredContainerWithSpace = itemHover;

            if (selectedInventoryItem != null &&
                selectedInventoryItem.ItemTable.InventoryMetadata is ContainerMetadata containerFromSelectedItem)
            {
                if (containerFromSelectedItem.IsInsertingInsideYourself(_hoveredContainerWithSpace.ItemTable
                        .CurrentGridTable))
                {
                    return;
                }
            }

            var containsSpaceForItem =
                _hoveredContainerWithSpace.ItemTable.InventoryMetadata is ContainerMetadata containerMetadata &&
                containerMetadata.ContainsSpaceForItem(selectedInventoryItem);

            if (!containsSpaceForItem) return;

            var inventorySettingsSo = ctx.InventorySettingsSo;

            _hoveredContainerWithSpace.SetBackground(true, inventorySettingsSo.OnHoverContainerThatCanBeInserted);
            state.Show = false;
        }

        private void ResetHoveredBackgroundItem()
        {
            if (_hoveredContainerWithSpace == null) return;

            _hoveredContainerWithSpace.SetBackground(false);
            _hoveredContainerWithSpace = null;
        }

        protected override bool ShouldProcess(HighlighterContext ctx, HighlighterState state)
        {
            if (ctx.SelectedAbstractGrid == null) return true;

            return ctx.SelectedInventoryItem != null;
        }
    }
}