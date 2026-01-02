using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Items;

namespace Inventory.Scripts.Options
{
    public class InspectManager : OptionExecutionManager
    {
        protected override void HandleOptionExecution(AbstractItem itemUi)
        {
            var itemTable = itemUi.ItemTable;

            StaticInventoryContext.WindowManager.OpenWindow(itemTable, WindowType.Inspect);
        }
    }
}