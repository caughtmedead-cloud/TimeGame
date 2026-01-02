using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors;
using UnityEngine;

namespace Inventory.Scripts.Options
{
    public class DiscardManager : OptionExecutionManager
    {
        [Header("Player Transform Anchor")] [SerializeField]
        private TransformAnchorSo playerDropTransformAnchorSo;

        protected override void HandleOptionExecution(AbstractItem itemUi)
        {
            var itemTable = itemUi.ItemTable;

            var playerTransform = playerDropTransformAnchorSo.isSet
                ? playerDropTransformAnchorSo.Value
                : gameObject.transform;

            StaticInventoryContext.InventorySupplierSo.DropItem(itemUi, playerTransform);
            StaticInventoryContext.CloseDisplayContainerIfNeeded(itemTable);
        }
    }
}