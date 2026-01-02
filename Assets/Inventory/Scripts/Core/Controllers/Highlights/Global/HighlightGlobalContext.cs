using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Tile;

namespace Inventory.Scripts.Core.Controllers.Highlights.Global
{
    public class HighlightGlobalContext
    {
        public bool Debug { get; set; }

        public InventorySettingsSo InventorySettingsSo { get; }
        public AbstractItem ItemDragging { get; }
        public TileGridHelperSo TileGridHelperSo { get; }

        public HighlightGlobalContext(
            InventorySettingsSo inventorySettingsSo,
            AbstractItem itemDragging,
            TileGridHelperSo tileGridHelperSo
        )
        {
            InventorySettingsSo = inventorySettingsSo;
            ItemDragging = itemDragging;
            TileGridHelperSo = tileGridHelperSo;
        }
    }
}