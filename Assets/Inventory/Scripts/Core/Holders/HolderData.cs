using System;
using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Controllers.Save.Converters;
using Inventory.Scripts.Core.Items;
using Newtonsoft.Json;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditorInternal;
#endif

namespace Inventory.Scripts.Core.Holders
{
    [Serializable]
    [JsonConverter(typeof(HolderConverter))]
    public class HolderData
    {
        /// <summary>
        /// The id from primary holder that are storing this information.
        /// </summary>
        public string id;

        [JsonIgnore] public bool isEquipped;

        [SerializeReference] private ItemTable itemEquipped;

        public event Action<ItemTable> OnEquip;
        public event Action<ItemTable> OnUnEquip;

        public HolderData(string holderId)
        {
            id = holderId;
        }

        public void Equip(ItemTable itemTable, bool shouldCloseContainerOpened = true)
        {
            if (itemTable == null) return;

            RemovePreviousEquippedItemIfNeeded();

            itemTable.RemoveItselfFromLocation();

            itemEquipped = itemTable;
            isEquipped = itemEquipped != null;
            itemEquipped?.SetGridProps(null, null, this);

            if (shouldCloseContainerOpened)
            {
                StaticInventoryContext.CloseDisplayContainerIfNeeded(itemEquipped);
            }

            OnEquip?.Invoke(itemEquipped);

#if UNITY_EDITOR
            InternalEditorUtility.RepaintAllViews();
#endif
        }

        public ItemTable UnEquip()
        {
            if (!isEquipped) return null;

            var equippedItem = itemEquipped;

            equippedItem.SetGridProps(null, null, null);

            if (equippedItem.IsContainer())
            {
                StaticInventoryContext.CloseDisplayContainerIfNeeded(equippedItem);
            }

            OnUnEquip?.Invoke(equippedItem);

            itemEquipped = null;
            isEquipped = false;

#if UNITY_EDITOR
            InternalEditorUtility.RepaintAllViews();
#endif

            return equippedItem;
        }

        public ItemTable PickUp()
        {
            var equippedItem = itemEquipped;

            // inventorySo?.RemoveItem(equippedItem);

            // Once we pickup the item in a holder will close the opened inventories
            if (equippedItem.IsContainer())
            {
                StaticInventoryContext.CloseDisplayContainerIfNeeded(equippedItem);
            }

            // This remove the all the "replicated" items in other holders.
            OnUnEquip?.Invoke(equippedItem);

#if UNITY_EDITOR
            InternalEditorUtility.RepaintAllViews();
#endif

            return equippedItem;
        }

        /// <summary>
        /// This method executes if we have performed a "force" equip... This method guarantee that the equipped item is remove and close the container.
        /// </summary>
        private void RemovePreviousEquippedItemIfNeeded()
        {
            if (!isEquipped) return;

            itemEquipped?.RemoveItselfFromHolder();
        }

        public ItemTable GetItemEquipped()
        {
            return itemEquipped;
        }

        public Holder GetPrimaryHolder()
        {
            return StaticInventoryContext.GetHolderFromId(id, itemEquipped);
        }

        public override string ToString()
        {
            return "IsEquipped: " + isEquipped + " ItemEquipped: " + itemEquipped;
        }
    }
}