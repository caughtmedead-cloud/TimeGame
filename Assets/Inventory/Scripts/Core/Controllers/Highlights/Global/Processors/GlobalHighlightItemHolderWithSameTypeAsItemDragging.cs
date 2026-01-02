using System.Linq;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.Items;

namespace Inventory.Scripts.Core.Controllers.Highlights.Global.Processors
{
    // [CreateAssetMenu(menuName = "Inventory/Configuration/Providers/Processors/Highlight Global Item Holder With Same Type As Item Dragging Processor")]
    public class GlobalHighlightItemHolderWithSameTypeAsItemDragging : HighlightGlobalProcessor
    {
        protected override void HandleProcess(HighlightGlobalContext ctx)
        {
            var initializedHolders = StaticInventoryContext.GetInitializedHolders(true);

            var inventorySettingsSo = ctx.InventorySettingsSo;

            var itemDragging = ctx.ItemDragging;
            var isItemDraggingNull = itemDragging == null;

            var uiHolders = initializedHolders.Where(holder => holder is ItemHolder)
                .Cast<ItemHolder>()
                .ToList();

            foreach (var holder in uiHolders)
            {
                if (isItemDraggingNull)
                {
                    holder.StopHighlight();
                    continue;
                }

                if (holder.IsEquipped()) continue;

                if (!IsItemSameType(itemDragging.ItemTable, holder)) continue;

                holder.Highlight(inventorySettingsSo.ColorHighlightHolder);
            }
        }

        private bool IsItemSameType(ItemTable itemTable, ItemHolder holder)
        {
            if (itemTable == null) return false;

            return itemTable.ItemDataSo.ItemDataTypeSo == holder.ItemDataTypeSo;
        }

        protected override bool ShouldProcess(HighlightGlobalContext ctx)
        {
            var inventorySettingsSo = ctx.InventorySettingsSo;

            return inventorySettingsSo.EnableHighlightHolder;
        }
    }
}