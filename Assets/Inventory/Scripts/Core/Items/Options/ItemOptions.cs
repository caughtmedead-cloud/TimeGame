using System;
using System.Collections.Generic;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.ScriptableObjects.Options;

namespace Inventory.Scripts.Core.Items.Options
{
    public class ItemOptions
    {
        /// <summary>
        /// Options that from the item data so. The entire list. This should never modify. If you want to modify the options, you can modify the field "Options"
        /// </summary>
        public OptionSo[] DataOptions { get; private set; }

        public List<OptionSo> Options { get; } = new();

        private readonly ItemTable _itemTable;

        public ItemOptions(ItemTable itemTable)
        {
            _itemTable = itemTable;
            _itemTable.OnUpdateUI += _ => UpdateOptions();
        }

        private void NormalizeOptions()
        {
            DataOptions = _itemTable != null && _itemTable.ItemDataSo != null
                ? _itemTable.ItemDataSo.GetOptionsOrdered()
                : Array.Empty<OptionSo>();

            // Clear options then add all options from data, and will filter based on the item conditions.
            Options.Clear();
            Options.AddRange(DataOptions);
        }

        public void UpdateOptions()
        {
            // Normalize options before filtering
            NormalizeOptions();

            HandleEquipOptions();
        }

        private void HandleEquipOptions()
        {
            this.RemoveOption(_itemTable.IsEquipped() ? OptionsType.Equip : OptionsType.Unequip);
        }
    }
}