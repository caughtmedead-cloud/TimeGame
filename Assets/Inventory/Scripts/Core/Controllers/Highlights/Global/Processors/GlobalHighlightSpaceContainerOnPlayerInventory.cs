using System;
using System.Linq;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.ScriptableObjects;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Highlights.Global.Processors
{
    // [CreateAssetMenu(menuName = "Inventory/Configuration/Providers/Processors/Highlight Global Player Space Container On Player Inventory Processor")]
    public class GlobalHighlightSpaceContainerOnPlayerInventory : HighlightGlobalProcessor
    {
        protected override void HandleProcess(HighlightGlobalContext ctx)
        {
            var inventorySettingsSo = ctx.InventorySettingsSo;

            var playerInventorySo = GetPlayerInventorySo(inventorySettingsSo);

            if (!playerInventorySo.HasItems()) return;

            var allItemsFromPlayerInventory = playerInventorySo
                .GetGrids()
                .SelectMany(grid => grid.GetAllItemsFromGrid());

            var itemDragging = ctx.ItemDragging;
            var colorBackground = inventorySettingsSo.OnContainerInPlayerInventoryFitsItemDragged;

            foreach (var item in allItemsFromPlayerInventory)
            {
                if (itemDragging == null)
                {
                    item.UpdateUI(new ItemUIUpdater(
                        Tuple.Create<bool, Color?>(false, null)
                    ));
                    continue;
                }

                if (item.InventoryMetadata is not ContainerMetadata metadata) continue;

                if (itemDragging.ItemTable.InventoryMetadata is ContainerMetadata itemDraggingMetadata)
                {
                    var isInsertingInsideYourself =
                        itemDraggingMetadata.IsInsertingInsideYourself(item.CurrentGridTable);

                    if (isInsertingInsideYourself)
                    {
                        continue;
                    }
                }

                var containsSpaceForItem = metadata.ContainsSpaceForItem(itemDragging);

                // TODO: Validate if the inventoryItem is already inside the container that, if so should not update the background.
                item.UpdateUI(new ItemUIUpdater(
                    Tuple.Create<bool, Color?>(containsSpaceForItem, colorBackground)
                ));
            }
        }

        private InventorySo GetPlayerInventorySo(InventorySettingsSo inventorySettingsSo)
        {
            return inventorySettingsSo.PlayerInventorySo;
        }

        protected override bool ShouldProcess(HighlightGlobalContext ctx)
        {
            var inventorySettingsSo = ctx.InventorySettingsSo;

            return inventorySettingsSo.EnableHighlightContainerInPlayerInventoryFitsItemDragged;
        }
    }
}