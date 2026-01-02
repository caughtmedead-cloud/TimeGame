using System;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.ScriptableObjects.Options;

namespace Inventory.Scripts.Core.Items.Options
{
    public static class OptionsHelper
    {
        public static void AddOption(this ItemOptions itemOptions, OptionsType optionsType)
        {
            var optionSo = itemOptions.FindOption(optionsType);

            if (optionSo == null) return;

            var options = itemOptions.Options;

            if (options.Contains(optionSo)) return;

            options.Add(optionSo);
        }

        public static void RemoveOption(this ItemOptions itemOptions, OptionsType optionsType)
        {
            var optionSo = itemOptions.FindOption(optionsType);

            if (optionSo == null) return;

            itemOptions.Options.Remove(optionSo);
        }

        public static OptionSo FindOption(this ItemOptions itemOptions, OptionsType optionsType)
        {
            var options = itemOptions.DataOptions;

            var optionSo = Array.Find(options, so => so.OptionsType == optionsType);

            return optionSo;
        }
    }
}