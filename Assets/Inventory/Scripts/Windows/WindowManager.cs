using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Windows;

namespace Inventory.Scripts.Windows
{
    public class WindowManager : WindowController
    {
        protected override Window GetPrefabWindow(WindowType windowType)
        {
            return windowType switch
            {
                WindowType.Inspect => inventorySettingsAnchorSo.InventorySettingsSo.BasicPrefabWindow,
                WindowType.Container => inventorySettingsAnchorSo.InventorySettingsSo.ContainerPrefabWindow,
                _ => base.GetPrefabWindow(windowType)
            };
        }

        protected override int GetMaxWindowOpen(WindowType windowType)
        {
            return windowType switch
            {
                WindowType.Inspect => inventorySettingsAnchorSo.InventorySettingsSo.MaxInspectWindowOpen,
                WindowType.Container => inventorySettingsAnchorSo.InventorySettingsSo.MaxContainerWindowOpen,
                _ => base.GetMaxWindowOpen(windowType)
            };
        }
    }
}