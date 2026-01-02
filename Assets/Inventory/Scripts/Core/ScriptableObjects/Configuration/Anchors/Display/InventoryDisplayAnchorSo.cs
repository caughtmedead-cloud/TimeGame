using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Displays;
using Inventory.Scripts.Core.Displays.Filler;
using Inventory.Scripts.Core.Items;
using UnityEngine;

namespace Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors.Display
{
    [CreateAssetMenu(menuName = "Inventory/Inventory Display Anchor So")]
    public class InventoryDisplayAnchorSo : AnchorSo<InventoryDisplay>
    {
        private void Awake()
        {
            StaticInventoryContext.AddDisplays(this);
        }

        private void OnEnable()
        {
            StaticInventoryContext.AddDisplays(this);
        }

        public void OpenDisplay(DisplayData displayData)
        {
            if (!isSet || Value == null) return;

            Value.DisplayInteract(displayData, DisplayInteraction.Open);
        }

        public void CloseDisplay(DisplayData displayData)
        {
            if (!isSet || Value == null) return;

            Value.DisplayInteract(displayData, DisplayInteraction.Close);
        }

        public void CloseCharacterInventory(InventorySo inventorySo)
        {
            if (!isSet || Value == null) return;

            Value.DisplayInteract(new DisplayData(inventorySo), DisplayInteraction.Close);
        }

        public void CloseContainer(ItemTable itemContainer)
        {
            if (!isSet || Value == null) return;

            Value.DisplayInteract(itemContainer, DisplayInteraction.Close);
        }

        public void CloseAll()
        {
            if (!isSet || Value == null) return;

            Value.CloseAll();
        }

        public bool IsContainerOpened(ItemTable itemContainer)
        {
            return isSet && Value != null && Value.IsItemContainerOpened(itemContainer);
        }

        public bool IsInventoryOpened(InventorySo inventorySo)
        {
            return isSet && Value != null && Value.IsInventoryOpened(inventorySo);
        }

        public bool IsDisplayOpened(DisplayData displayData)
        {
            return isSet && Value != null && Value.IsDisplayOpened(displayData);
        }
    }
}