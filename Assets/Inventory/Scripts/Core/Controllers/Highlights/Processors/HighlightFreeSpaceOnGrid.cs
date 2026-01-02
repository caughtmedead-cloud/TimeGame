using Inventory.Scripts.Core.Grids.Helper;

namespace Inventory.Scripts.Core.Controllers.Highlights.Processors
{
    // [CreateAssetMenu(menuName = "Inventory/Configuration/Providers/Processors/Highlight Free Space In Grid Processor")]
    public class HighlightFreeSpaceOnGrid : HighlightProcessor
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
            state.Color = inventorySettingsSo.OnHoverEmptyCell;
            state.Size = size;
            state.Position = calculatePositionOnGrid;
        }

        protected override bool ShouldProcess(HighlighterContext ctx, HighlighterState state)
        {
            if (ctx.SelectedAbstractGrid == null) return false;

            var selectedInventoryItem = ctx.SelectedInventoryItem;

            if (selectedInventoryItem == null) return false;

            var selectedAbstractGrid = ctx.SelectedAbstractGrid;
            var positionOnGrid = ctx.PositionOnGrid;

            var boundaryCheck = selectedAbstractGrid.Grid.BoundaryCheck(
                positionOnGrid.x, positionOnGrid.y,
                selectedInventoryItem.ItemTable.Width,
                selectedInventoryItem.ItemTable.Height
            );

            if (boundaryCheck)
            {
                return selectedAbstractGrid.Grid.OverlapCheck(positionOnGrid.x, positionOnGrid.y,
                    selectedInventoryItem.ItemTable.Width,
                    selectedInventoryItem.ItemTable.Height
                );
            }

            return false;
        }
    }
}