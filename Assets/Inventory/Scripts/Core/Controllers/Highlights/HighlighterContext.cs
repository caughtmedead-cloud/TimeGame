using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Tile;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Highlights
{
    public class HighlighterContext
    {
        public bool Debug { get; set; }

        public InventorySettingsSo InventorySettingsSo { get; }
        public Vector2Int PositionOnGrid { get; }
        public AbstractGrid SelectedAbstractGrid { get; }
        public AbstractItem SelectedInventoryItem { get; }
        public TileGridHelperSo TileGridHelperSo { get; }

        public HighlighterContext(
            InventorySettingsSo inventorySettingsSo,
            Vector2Int positionOnGrid,
            AbstractGrid selectedAbstractGrid,
            AbstractItem selectedInventoryItem,
            TileGridHelperSo tileGridHelperSo
        )
        {
            PositionOnGrid = positionOnGrid;
            SelectedAbstractGrid = selectedAbstractGrid;
            SelectedInventoryItem = selectedInventoryItem;
            TileGridHelperSo = tileGridHelperSo;
            InventorySettingsSo = inventorySettingsSo;
        }

        public static HighlighterContext Empty()
        {
            return new HighlighterContext(
                null,
                Vector2Int.zero,
                null,
                null,
                null
            );
        }
    }
}