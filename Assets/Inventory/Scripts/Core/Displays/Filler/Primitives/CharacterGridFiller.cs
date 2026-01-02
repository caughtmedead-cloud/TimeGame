using System;
using System.Collections.Generic;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Grids.Helper;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory.Scripts.Core.Displays.Filler.Primitives
{
    public class CharacterGridFiller : AbstractFiller
    {
        [Header("Holder Container")] [SerializeField]
        private ReplicatorItemHolder holderWithItemContainer;

        private RectTransform _rectTransform;
        private ContainerGrids _openedContainerGrids;
        private HolderData _holderData;

        public override void Init()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        /**
         * This render again once the container was pickup but no placed anywhere (basically a rollback to the ui).
         */
        private void OnEnable()
        {
            OnSet(DisplayData);
        }

        private void OnDisable()
        {
            if (DisplayData != null && DisplayData.InventorySo != null)
            {
                DisplayData.InventorySo.OnLoad -= LoadFiller;
            }

            OnReset();
        }

        private void OnEquipItem(ItemTable containerItem)
        {
            if (IsAlreadyOpened()) return;

            var containerMetadata = containerItem.GetMetadata<ContainerMetadata>();

            _openedContainerGrids = containerMetadata.OpenInventory(transform);
            gameObject.SetActive(true);
            OnRefreshUI();
        }

        private bool IsAlreadyOpened()
        {
            return _openedContainerGrids != null;
        }

        private void OnUnEquipItem(ItemTable containerItem)
        {
            var containerMetadata = containerItem.GetMetadata<ContainerMetadata>();
            containerMetadata.CloseInventory(_openedContainerGrids);
            OnRefreshUI();

            _openedContainerGrids = null;
            gameObject.SetActive(false);
        }

        protected override void OnSet(DisplayData displayData)
        {
            SetHolder();
            if (displayData != null && displayData.InventorySo != null)
            {
                displayData.InventorySo.OnLoad += LoadFiller;
            }
        }

        private void LoadFiller()
        {
            OnReset();
            SetHolder();
        }

        private void SetHolder()
        {
            _holderData = holderWithItemContainer.GetHolderData();

            if (_holderData == null) return;

            _holderData.OnEquip += OnEquipItem;
            _holderData.OnUnEquip += OnUnEquipItem;

            if (_holderData is not { isEquipped: true }) return;

            if (!_holderData.GetItemEquipped().IsContainer()) return;

            OnEquipItem(_holderData.GetItemEquipped());
        }

        public override void OnRefreshUI()
        {
            base.OnRefreshUI();

            RectHelper.SetAnchorsForGrid(_rectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);

            Revalidate(new List<Type>
            {
                typeof(RefreshLayoutFiller)
            });
        }

        protected override void OnReset()
        {
            if (_holderData == null) return;

            _holderData.OnEquip -= OnEquipItem;
            _holderData.OnUnEquip -= OnUnEquipItem;

            if (_holderData is not { isEquipped: true }) return;

            if (!_holderData.GetItemEquipped().IsContainer()) return;

            OnUnEquipItem(_holderData.GetItemEquipped());
        }

#if UNITY_EDITOR
        public void SetHolder(ReplicatorItemHolder replicatorItemHolder)
        {
            holderWithItemContainer = replicatorItemHolder;
        }
#endif
    }
}