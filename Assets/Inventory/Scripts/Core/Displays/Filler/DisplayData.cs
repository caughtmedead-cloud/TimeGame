using System;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects;
using UnityEngine;

namespace Inventory.Scripts.Core.Displays.Filler
{
    [Serializable]
    public class DisplayData
    {
        [field: SerializeReference] public InventorySo InventorySo { get; private set; }
        [field: SerializeReference] public ItemTable ItemContainer { get; private set; }

        private bool _isOpened;

        public DisplayData(ItemTable itemContainer)
        {
            ItemContainer = itemContainer;
        }

        public DisplayData(InventorySo inventorySo)
        {
            InventorySo = inventorySo;
        }

        public bool IsOpen()
        {
            return _isOpened;
        }

        public void SetState(bool opened)
        {
            _isOpened = opened;
        }
    }
}