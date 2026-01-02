namespace Inventory.Scripts.Core.Controllers.Highlights.Processors
{
    // [CreateAssetMenu(menuName = "Inventory/Configuration/Providers/Processors/Highlight Item In Grid Processor")]
    public class HighlightItemCursorHoverOnGrid : HighlightProcessor
    {
        protected override void HandleProcess(HighlighterContext ctx, HighlighterState state)
        {
            var positionOnGrid = ctx.PositionOnGrid;
            var selectedAbstractGrid = ctx.SelectedAbstractGrid;

            var itemToHighlight = selectedAbstractGrid.Grid.GetItem(
                positionOnGrid.x,
                positionOnGrid.y
            )?.GetAbstractItem();

            if (itemToHighlight == null)
            {
                state.Show = false;
                return;
            }

            var tileGridHelperSo = ctx.TileGridHelperSo;

            var size = tileGridHelperSo.GetItemSize(itemToHighlight.ItemTable.Width, itemToHighlight.ItemTable.Height);

            var pos = tileGridHelperSo.CalculatePositionOnGrid(
                itemToHighlight,
                itemToHighlight.ItemTable.Position.x,
                itemToHighlight.ItemTable.Position.y
            );

            var onHoverItem = ctx.InventorySettingsSo.OnHoverItem;

            state.Show = true;
            state.Color = onHoverItem;
            state.Size = size;
            state.Position = pos;
        }

        protected override bool ShouldProcess(HighlighterContext ctx, HighlighterState state)
        {
            if (ctx.SelectedAbstractGrid == null) return false;

            return ctx.SelectedInventoryItem == null;
        }
    }
}