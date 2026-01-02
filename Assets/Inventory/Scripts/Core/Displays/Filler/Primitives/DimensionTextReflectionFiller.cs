using Inventory.Scripts.Core.ScriptableObjects.Items.Dimensions;
using TMPro;
using UnityEngine;

namespace Inventory.Scripts.Core.Displays.Filler.Primitives
{
    [RequireComponent(typeof(TMP_Text))]
    public class DimensionTextReflectionFiller : ReflectionContentFiller<DimensionsSo>
    {
        private TMP_Text _text;

        public override void Init()
        {
            _text = GetComponent<TMP_Text>();
        }

        protected override void OnSet(DisplayData displayData)
        {
            var itemContainer = displayData.ItemContainer;

            var dimensionsSo = GetObjectValue(itemContainer);

            var valueText = $"{dimensionsSo.Width}x{dimensionsSo.Height}";

            _text.SetText(valueText);
        }

        protected override void OnReset()
        {
            _text.SetText("");
        }

        protected override bool ShouldFill(DisplayData displayData)
        {
            return displayData is { ItemContainer: not null };
        }
    }
}