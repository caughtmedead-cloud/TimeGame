using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Items;
using UnityEngine;

namespace Inventory.Scripts.Core.Holders
{
    [DefaultExecutionOrder(5)]
    public abstract class Holder : MonoBehaviour
    {
        public UniqueUid uniqueUid;

        [Header("Holding")] [SerializeField] protected HolderData holderData;

        protected virtual void OnEnable()
        {
            StaticInventoryContext.AddHolder(this);
        }

        protected virtual void OnDisable()
        {
            StaticInventoryContext.RemoveHolder(this);

            if (holderData == null) return;

            holderData.OnEquip -= OnEquipItem;
            holderData.OnUnEquip -= OnUnEquipItem;

            OnUnEquipItem(holderData.GetItemEquipped());

            holderData = null;
        }

        public HolderResponse TryEquipItem(ItemTable itemTable)
        {
            if (itemTable == null) return HolderResponse.Error;

            if (IsHolderEquipped())
            {
                Debug.Log("Already equipped".Info());
                return HolderResponse.AlreadyEquipped;
            }

            var (canEquip, holderResponse) = CanPerformEquip(itemTable);

            return !canEquip ? holderResponse : Equip(itemTable);
        }

        private bool IsHolderEquipped()
        {
            return holderData?.GetItemEquipped() != null && holderData.isEquipped;
        }

        public virtual (bool, HolderResponse) CanPerformEquip(ItemTable itemToEquip)
        {
            return (true, HolderResponse.PerformValidation);
        }

        public HolderResponse Equip(ItemTable itemTable)
        {
            if (itemTable == null) return HolderResponse.Error;

            holderData ??= new HolderData(uniqueUid.Uid);

            holderData.OnEquip -= OnEquipItem;
            holderData.OnUnEquip -= OnUnEquipItem;
            holderData.OnEquip += OnEquipItem;
            holderData.OnUnEquip += OnUnEquipItem;

            holderData.Equip(itemTable);

            return HolderResponse.Equipped;
        }


        public HolderResponse Equip(HolderData data)
        {
            if (holderData != null && holderData != data)
            {
                holderData.UnEquip();
                holderData.OnEquip -= OnEquipItem;
                holderData.OnUnEquip -= OnUnEquipItem;
            }

            if (holderData == data)
            {
                OnUnEquipItem(data.GetItemEquipped());
            }

            holderData = data;

            holderData.OnEquip -= OnEquipItem;
            holderData.OnUnEquip -= OnUnEquipItem;
            holderData.OnEquip += OnEquipItem;
            holderData.OnUnEquip += OnUnEquipItem;

            var itemEquipped = data.GetItemEquipped();

            if (itemEquipped != null)
            {
                OnEquipItem(itemEquipped);
            }

            return HolderResponse.Equipped;
        }

        public ItemTable UnEquip()
        {
            var itemEquipped = holderData?.UnEquip();

            if (itemEquipped == null) return null;

            holderData.OnEquip -= OnEquipItem;
            holderData.OnUnEquip -= OnUnEquipItem;

            return itemEquipped;
        }

        public ItemTable PickUp()
        {
            holderData.OnUnEquip -= OnUnEquipItem;

            var itemTable = holderData.PickUp();

            holderData.OnUnEquip += OnUnEquipItem;

            return itemTable;
        }

        protected virtual void OnEquipItem(ItemTable itemTable)
        {
        }

        protected virtual void OnUnEquipItem(ItemTable itemTable)
        {
        }

        public bool IsEquipped()
        {
            return holderData is { isEquipped: true };
        }

        public ItemTable GetItemEquipped()
        {
            return holderData?.GetItemEquipped();
        }

        public HolderData GetHolderData()
        {
            return holderData;
        }
    }
}