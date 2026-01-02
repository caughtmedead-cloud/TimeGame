using System.Globalization;
using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Grids.Helper;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.Items.Helper;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory.Scripts.Core.Items
{
    public class ItemInventory2D : AbstractItem
    {
        [Header("Item Props Settings")] [SerializeField]
        private Image image;

        [SerializeField] private TMP_Text displayName;

        [SerializeField] private TMP_Text countableText;

        [SerializeField] private Image background;

        private RectTransform _rectTransform;
        private RectTransform _rectTransformIcon;
        private bool _initialized;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _rectTransformIcon = image.GetComponent<RectTransform>();

            countableText.gameObject.SetActive(false);

            if (background != null)
            {
                background.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            _rectTransform.localScale = new Vector3(1, 1, 1);
        }

        protected override void OnSetProperties(ItemTable item)
        {
            if (item == null) return;

            var itemDataSo = item.ItemDataSo;

            if (itemDataSo == null) return;

            displayName.text = itemDataSo.DisplayName;
            SetCountableProperties(item);

            ResizeIcon();
        }

        private void SetCountableProperties(ItemTable itemTable)
        {
            countableText.gameObject.SetActive(false);

            var countableMetadata = itemTable.GetMetadata<CountableMetadata>();

            if (countableMetadata == null) return;

            var itemStackableDataSo = itemTable.GetItemData<ItemStackableDataSo>();

            if (itemStackableDataSo == null) return;

            var stackCount = countableMetadata.Stack.ToString(CultureInfo.CurrentCulture);
            stackCount += itemStackableDataSo.ShowMaxStack ? $"/{countableMetadata.MaxStack}" : "";

            countableText.text = stackCount;

            countableText.gameObject.SetActive(true);
        }

        public override void ResizeIcon(bool isDragging = false)
        {
            image.sprite = ItemTable.ItemDataSo.Icon;

            if (ItemTable.CurrentHolder != null && !isDragging)
            {
                ResizeIconForHolder();
                return;
            }

            ResizeIconForGrid(isDragging);
        }

        private void ResizeIconForHolder()
        {
            var holderInParent = GetComponentInParent<Holder>();

            if (holderInParent == null) return;

            if (holderInParent is not ItemHolder itemHolder) return;

            var holderSize = itemHolder.GetSizeDelta();

            ResizeItem(_rectTransform, holderSize);
            ResizeTexts();

            var inHolderIconSizeSo = ItemTable.ItemDataSo.InHolderIconSizeSo;

            if (inHolderIconSizeSo == null)
            {
                ResizeItem(_rectTransformIcon, holderSize);
                return;
            }

            var size = new Vector2
            {
                x = inHolderIconSizeSo.Width,
                y = inHolderIconSizeSo.Height
            };

            _rectTransformIcon.sizeDelta = size;
            _rectTransformIcon.localPosition = new Vector3
            {
                x = inHolderIconSizeSo.PosX,
                y = inHolderIconSizeSo.PosY,
                z = 0
            };
        }

        private void ResizeIconForGrid(bool isDragging)
        {
            GetRectTransform().sizeDelta = GetGridItemSize();
            ResizeTexts();

            var rectTransformIcon = GetRectTransformIcon();

            rectTransformIcon.sizeDelta = GetIconSize();
            rectTransformIcon.localPosition = GetIconPosition();

            var position = ItemTable.Position;

            if (position == null || isDragging) return;

            var tileGridHelperSo =
                StaticInventoryContext.InventorySettingsAnchorSo.InventorySettingsSo.TileGridHelperSo;

            // Calculate the position from the item inside the grid.
            var onGridPosition = tileGridHelperSo.CalculatePositionOnGrid(
                this,
                position.x,
                position.y
            );

            transform.localPosition = onGridPosition;
        }

        private Vector2 GetGridItemSize()
        {
            var tileSpecSo = StaticInventoryContext.InventorySettingsAnchorSo.InventorySettingsSo.TileSpecSo;

            var size = new Vector2
            {
                x = ItemTable.ItemDataSo.DimensionsSo.Width * tileSpecSo.TileSizeWidth,
                y = ItemTable.ItemDataSo.DimensionsSo.Height * tileSpecSo.TileSizeHeight
            };
            return size;
        }

        private RectTransform GetRectTransform()
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            return _rectTransform;
        }

        private RectTransform GetRectTransformIcon()
        {
            if (_rectTransformIcon == null)
            {
                _rectTransformIcon = GetComponent<RectTransform>();
            }

            return _rectTransformIcon;
        }

        private Vector2 GetIconSize()
        {
            var rectTransformSizeDelta = GetRectTransform().sizeDelta;

            var iconSizeSo = ItemTable.ItemDataSo.IconSizeSo;
            if (iconSizeSo != null)
            {
                return new Vector2
                {
                    x = iconSizeSo.Width,
                    y = iconSizeSo.Height
                };
            }

            return new Vector2
            {
                x = rectTransformSizeDelta.x,
                y = rectTransformSizeDelta.y,
            };
        }

        private Vector3 GetIconPosition()
        {
            var iconSizeSo = ItemTable.ItemDataSo.IconSizeSo;

            if (iconSizeSo != null)
            {
                return new Vector3
                {
                    x = iconSizeSo.PosX,
                    y = iconSizeSo.PosY,
                    z = 0
                };
            }

            return Vector3.zero;
        }

        private void ResizeItem(RectTransform rectTransform, Vector2 holderSize)
        {
            rectTransform.sizeDelta = holderSize;

            var anchors = new Vector2(0.5f, 0.5f);

            rectTransform.anchorMin = anchors;
            rectTransform.anchorMax = anchors;
            rectTransform.pivot = anchors;

            rectTransform.localPosition = new Vector2(0, 0);
        }

        public override void RotateUI()
        {
            // Rotate the entire object...
            _rectTransform.rotation = Quaternion.Euler(
                0,
                0,
                ItemTable.IsRotated ? RotationHelper.GetRotationByType(rotationType) : 0f
            );

            RectHelper.RotateText(
                displayName,
                RectHelper.RotatedAnchor(AnchorPosition.TopRight, ItemTable.IsRotated),
                ItemTable.IsRotated,
                rotationType
            );
            RectHelper.RotateText(
                countableText,
                RectHelper.RotatedAnchor(AnchorPosition.BottomRight, ItemTable.IsRotated),
                ItemTable.IsRotated,
                rotationType
            );

            ResizeTexts();
        }

        private void ResizeTexts()
        {
            RectHelper.ResizeText(displayName, ItemTable.IsRotated, _rectTransform);
            RectHelper.ResizeText(countableText, ItemTable.IsRotated, _rectTransform);
        }

        protected override void SetDragRepresentationStyle()
        {
            image.color = new Color32(255, 255, 225, 165);
            displayName.gameObject.SetActive(false);
            countableText.gameObject.SetActive(false);
        }

        public override void SetDraggingStyle()
        {
            image.color = new Color32(255, 255, 225, 235);
            displayName.gameObject.SetActive(false);
            countableText.gameObject.SetActive(false);
        }

        public override void SetBackground(bool setBackground, Color? color = null)
        {
            if (background == null) return;

            if (color.HasValue)
            {
                background.color = color.Value;
            }

            if (setBackground)
            {
                background.gameObject.SetActive(true);
                return;
            }

            background.gameObject.SetActive(false);
        }
    }
}