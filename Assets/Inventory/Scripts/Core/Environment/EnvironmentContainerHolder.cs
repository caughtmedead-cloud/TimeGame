using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Displays.Filler;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.ScriptableObjects;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors.Display;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using UnityEngine;

namespace Inventory.Scripts.Core.Environment
{
    /**
     * Useful if you have chests/boxes/barrels/lockers and etc... in your environment and you want to add an inventory to it.
     * You can attach this script and once the player points to this object you can call the <see cref="OpenContainer"/> method
     * Then once you're done, call the <see cref="CloseContainer"/> method
     * You can use <see cref="ToggleEnvironmentContainer"/> that controls the open/close status.
     */
    public class EnvironmentContainerHolder : Holder
    {
        [Header("Environment Container Settings")]
        [SerializeField, Tooltip("Container that will be displayed on this holder.")]
        private ItemContainerDataSo itemContainerDataSo;

        [Header("Displaying on...")] [SerializeField]
        private InventoryDisplayAnchorSo inventoryDisplayAnchorSo;

        [Header("Save Container")] [SerializeField, Tooltip("The inventory that will save this container content.")]
        private InventorySo attachedInventorySo;

        private DisplayData _displayData;

        private void Awake()
        {
            holderData = new HolderData(uniqueUid.Uid);
        }

        private void Start()
        {
            if (!inventoryDisplayAnchorSo)
            {
                Debug.LogError(
                    "Error configuring Environment Container Holder... Property ContainerDisplayAnchorSo has not being set."
                        .Configuration());
            }

            SetContainer(itemContainerDataSo);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            CloseContainer();
        }

        private void SetContainer(ItemContainerDataSo container)
        {
            if (IsEquipped() || container == null) return;

            itemContainerDataSo = container;
            StaticInventoryContext.InventorySupplierSo.EquipItem(container, this);

            if (attachedInventorySo != null)
            {
                attachedInventorySo.AddHolder(holderData);
                attachedInventorySo.OnLoad += LoadContainer;
            }
        }

        /// <summary>
        /// Once you interact with this Environment Container Holder you must call this method to open the Grid Inventory on the UI.
        /// </summary>
        public void OpenContainer()
        {
            var itemEquipped = GetItemEquipped();

            _displayData ??= new DisplayData(itemEquipped);

            inventoryDisplayAnchorSo.OpenDisplay(_displayData);
        }

        /// <summary>
        /// After you done interacting with this Environment Container Holder you must close in order to remove the Grid Inventory from UI.
        /// </summary>
        public void CloseContainer()
        {
            inventoryDisplayAnchorSo.CloseDisplay(_displayData);
        }

        /// <summary>
        /// Will toggle the inventory, if is closed will open the inventory on the UI, if opened will close the Inventory.
        /// </summary>
        public void ToggleEnvironmentContainer()
        {
            var isContainerOpened = IsContainerOpened();

            if (isContainerOpened)
            {
                CloseContainer();
                return;
            }

            OpenContainer();
        }

        private void OnDestroy()
        {
            if (attachedInventorySo != null)
            {
                attachedInventorySo.RemoveHolder(holderData);
                attachedInventorySo.OnLoad -= LoadContainer;
            }
        }

        public bool IsContainerOpened()
        {
            return inventoryDisplayAnchorSo.IsContainerOpened(GetItemEquipped());
        }

        // TODO: Fix this. It's opening the wrong container after the load. (If i have opened two container and I stayed in the second one and hit load, it closes this container a open the other one.)
        private void LoadContainer()
        {
            _displayData = null;
        }
    }
}