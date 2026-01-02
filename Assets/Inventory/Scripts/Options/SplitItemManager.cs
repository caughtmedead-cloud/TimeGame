using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using UnityEngine;

namespace Inventory.Scripts.Options
{
    public class SplitItemManager : OptionExecutionManager
    {
        protected override void HandleOptionExecution(AbstractItem itemUi)
        {
            var itemWillBeSplit = itemUi.ItemTable;

            var countableMetadata = itemWillBeSplit.GetMetadata<CountableMetadata>();

            if (countableMetadata == null)
            {
                Debug.LogWarning($"Cannot split item which is not a {nameof(ItemStackableDataSo)}");
                return;
            }

            countableMetadata.SplitHalf();
        }
    }
}