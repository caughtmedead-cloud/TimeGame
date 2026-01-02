namespace Inventory.Scripts.Core.Controllers.Highlights.Processors
{
    // [CreateAssetMenu(menuName = "Inventory/Configuration/Providers/Processors/Highlight Overlap Container Inserting Inside Itself On Grid Processor")]
    public class HighlightOverlapContainerInsertingInsideItself : HighlightProcessor
    {
        protected override void HandleProcess(HighlighterContext ctx, HighlighterState state)
        {
            var tileGridHelperSo = ctx.TileGridHelperSo;
            var selectedInventoryItem = ctx.SelectedInventoryItem;
            var positionOnGrid = ctx.PositionOnGrid;

            var size = tileGridHelperSo.GetItemSize(selectedInventoryItem.ItemTable.Width,
                selectedInventoryItem.ItemTable.Height);

            var calculatePositionOnGrid = tileGridHelperSo.CalculatePositionOnGrid(
                selectedInventoryItem,
                positionOnGrid.x,
                positionOnGrid.y
            );

            var inventorySettingsSo = ctx.InventorySettingsSo;

            state.Show = true;
            state.Color = inventorySettingsSo.OnHoverItemOverlapError;
            state.Size = size;
            state.Position = calculatePositionOnGrid;
        }

        protected override bool ShouldProcess(HighlighterContext ctx, HighlighterState state)
        {
            var selectedInventoryItem = ctx.SelectedInventoryItem;

            if (selectedInventoryItem == null) return false;

            var selectedAbstractGrid = ctx.SelectedAbstractGrid;

            return selectedAbstractGrid != null &&
                   selectedAbstractGrid.Grid.IsInsertingInsideYourself(selectedInventoryItem.ItemTable);
        }
    }
}