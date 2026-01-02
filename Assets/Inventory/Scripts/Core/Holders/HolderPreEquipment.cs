using System;
using Inventory.Scripts.Core.ScriptableObjects.Items;

namespace Inventory.Scripts.Core.Holders
{
    [Serializable]
    public class HolderPreEquipment
    {
        public string holderId;
        public ItemDataSo item;
    }
}