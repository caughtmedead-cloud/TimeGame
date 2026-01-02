using UnityEngine;
using UnityEngine.UI;

namespace Inventory.Scripts.Core.Displays.Filler.Primitives
{
    [RequireComponent(typeof(Image))]
    public class SpriteFiller : ReflectionContentFiller<Sprite>
    {
        private Image _image;

        public override void Init()
        {
            _image = GetComponent<Image>();
        }

        protected override void OnSet(DisplayData displayData)
        {
            var itemContainer = displayData.ItemContainer;

            var propertyValue = GetObjectValue(itemContainer);

            _image.sprite = propertyValue;
        }

        protected override void OnReset()
        {
            _image.sprite = default;
        }

        protected override bool ShouldFill(DisplayData displayData)
        {
            return displayData is { ItemContainer: not null };
        }
    }
}