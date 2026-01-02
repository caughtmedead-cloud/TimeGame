using Inventory.Scripts.Core.Grids.Helper;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Highlights.Processors
{
    // [CreateAssetMenu(menuName = "Inventory/Configuration/Providers/Processors/Highlight Overlap Item On Grid Processor")]
    public class HighlightOverlapItemOnGrid : HighlightProcessor
    {
        [SerializeField]
        [Tooltip(
            "Will show a error highlight on the boundaries of the grid. If disable, the highlight will not appear")]
        private bool showBoundariesErrors = true;

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

            if (!boundaryCheck) return showBoundariesErrors;

            var notOverlapping = selectedAbstractGrid.Grid.OverlapCheck(positionOnGrid.x, positionOnGrid.y,
                selectedInventoryItem.ItemTable.Width,
                selectedInventoryItem.ItemTable.Height
            );

            return !notOverlapping;
        }
    }
}