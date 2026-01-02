using Inventory.Scripts.Core.Displays.Filler;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors.Display;
using UnityEngine;

namespace Inventory.Scripts.Core.Holders
{
    public class ContainerHolder : ItemHolder
    {
        [Space(16)] [Header("Container Holder Settings")] [SerializeField]
        private InventoryDisplayAnchorSo inventoryDisplayAnchorSo;

        private DisplayData _displayData;

        protected override void OnEquipItem(ItemTable itemTable)
        {
            base.OnEquipItem(itemTable);

            if (!itemTable.IsContainer()) return;

            if (inventoryDisplayAnchorSo == null)
            {
                Debug.LogError(
                    "[containerDisplayAnchorSo] Display Anchor not configured... Please make sure to create the scriptable object, pointing to a Container Display and this Container Holder."
                        .Configuration());
                return;
            }

            _displayData = new DisplayData(itemTable);

            inventoryDisplayAnchorSo.OpenDisplay(_displayData);
        }

        protected override void OnUnEquipItem(ItemTable itemTable)
        {
            base.OnUnEquipItem(itemTable);

            if (itemTable == null) return;

            if (!itemTable.IsContainer()) return;

            if (inventoryDisplayAnchorSo == null)
            {
                Debug.LogError(
                    "[containerDisplayAnchorSo] Display Anchor not configured... Please make sure to create the scriptable object, pointing to a Container Display and this Container Holder."
                        .Configuration());
                return;
            }

            inventoryDisplayAnchorSo.CloseDisplay(_displayData);
            _displayData = null;
        }
    }
}