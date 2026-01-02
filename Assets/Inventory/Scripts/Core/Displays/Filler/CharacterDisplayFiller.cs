using System.Linq;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Holders;
using UnityEngine;

namespace Inventory.Scripts.Core.Displays.Filler
{
    public class CharacterDisplayFiller : DisplayFiller
    {
        private ReplicatorItemHolder[] _nonStorableHolders;

        protected override void Awake()
        {
            base.Awake();

            _nonStorableHolders = GetComponentsInChildren<ReplicatorItemHolder>();

            if (_nonStorableHolders is not { Length: > 0 })
            {
                Debug.LogWarning(
                    "Character Display Filler without any AbstractFiller in children... Please set some fillers inside the object."
                        .Configuration());
            }
        }

        protected override void OnAwakeDisplay()
        {
            base.OnAwakeDisplay();

            EquipInHolder();
            DisplayData.InventorySo.OnLoad += LoadHolder;
        }

        protected override void OnDisable()
        {
            if (DisplayData != null)
            {
                DisplayData.InventorySo.OnLoad -= LoadHolder;
            }

            base.OnDisable();
        }

        protected override void OnResetDisplay()
        {
            base.OnResetDisplay();

            if (_nonStorableHolders == null) return;

            foreach (var holder in _nonStorableHolders)
            {
                holder.UnEquip();
            }
        }

        private void LoadHolder()
        {
            OnResetDisplay();
            EquipInHolder();
        }

        private void EquipInHolder()
        {
            var characterInventory = DisplayData.InventorySo;
            var holdersNotEquipped = _nonStorableHolders.Where(holder => !holder.IsEquipped());

            foreach (var holder in holdersNotEquipped)
            {
                var holderToReplicate =
                    characterInventory.Holders.FirstOrDefault(data =>
                        holder.replicationUid.ReplicatorUid.Equals(data.id));

                if (holderToReplicate == null)
                {
                    var holderData = new HolderData(holder.replicationUid.ReplicatorUid);
                    characterInventory.AddHolder(holderData);
                    holder.Equip(holderData);
                    continue;
                }

                holder.Equip(holderToReplicate);
            }
        }
    }
}