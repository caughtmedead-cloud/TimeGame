using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Tile;

namespace Inventory.Scripts.Core.Grids.Helper
{
    public static class GridHelper
    {
        public static AbstractItem GetItemHover(TileGridHelperSo tileGridHelperSo, AbstractGrid abstractGrid)
        {
            var tileGridPositionByGrid = tileGridHelperSo.GetTileGridPositionByGridTable(abstractGrid.transform);

            return abstractGrid.Grid.GetItem(tileGridPositionByGrid.x, tileGridPositionByGrid.y)?.GetAbstractItem();
        }
    }
}