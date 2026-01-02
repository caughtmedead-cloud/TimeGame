using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory.Scripts.Core.Holders
{
    public class ItemHolder : Holder
    {
        [SerializeField] private bool useDefaultSprite;

        [SerializeField] private Sprite defaultSprite;

        [SerializeField] private Color spriteColor;

        [SerializeField] [Tooltip("Will display the inventory item icon rotated.")]
        private bool displayRotatedIcon;

        [Header("Type Settings")] [SerializeField]
        private ItemDataTypeSo itemDataTypeSo;

        public bool UseDefaultSprite => useDefaultSprite;

        public Sprite DefaultSprite => defaultSprite;

        public Color SpriteColor => spriteColor;

        public bool DisplayRotatedIcon => displayRotatedIcon;

        public ItemDataTypeSo ItemDataTypeSo => itemDataTypeSo;

        protected Image Image;
        protected Color DefaultColor;
        protected AbstractItem EquippedAbstractItemUi;

        private Image _childedImage;
        private RectTransform _holderRectTransform;
        private bool _initialized;

        protected void Awake()
        {
            InitVariables();
        }

        private void InitVariables()
        {
            if (_initialized) return;

            Image = GetComponent<Image>();
            if (UseDefaultSprite)
            {
                _childedImage = GetChildedImage();
            }

            DefaultColor = Image.color;
            _holderRectTransform = GetComponent<RectTransform>();

            _initialized = true;
        }

        private Image GetChildedImage()
        {
            try
            {
                var imageChilded = GetComponentsInChildren<Image>()[0];

                if (Image == imageChilded)
                {
                    imageChilded = GetComponentsInChildren<Image>()[1];
                }

                return imageChilded;
            }
            catch
            {
                Debug.LogError(
                    "Error getting childed image... Check if the image is created or 'useDefaultSprite' is unchecked."
                        .Settings());
                return null;
            }
        }

        public override (bool, HolderResponse) CanPerformEquip(ItemTable itemToEquip)
        {
            var isSameType = itemDataTypeSo == itemToEquip.ItemDataSo.ItemDataTypeSo;

            return (isSameType, HolderResponse.NotCorrectType);
        }

        protected override void OnEquipItem(ItemTable itemTable)
        {
            InitVariables();
            base.OnEquipItem(itemTable);

            EquippedAbstractItemUi = StaticInventoryContext.CreateSimpleAbstractItem(_holderRectTransform);
            EquippedAbstractItemUi.RefreshItem(itemTable);

            DisableDefaultImage();
            ResizeItem(EquippedAbstractItemUi);
        }

        protected override void OnUnEquipItem(ItemTable itemTable)
        {
            InitVariables();
            base.OnUnEquipItem(itemTable);

            EnableDefaultImage();

            if (EquippedAbstractItemUi)
            {
                Destroy(EquippedAbstractItemUi.gameObject);
            }
        }

        public virtual void Highlight(Color color)
        {
            Image.color = color;
        }

        public virtual void StopHighlight()
        {
            Image.color = DefaultColor;
        }

        protected void ResizeItem(AbstractItem item)
        {
            item.ResizeIcon();

            switch (displayRotatedIcon)
            {
                case false when item.ItemTable.IsRotated:
                case true when !item.ItemTable.IsRotated:
                    item.Rotate();
                    break;
            }
        }

        public Vector2 GetSizeDelta()
        {
            InitVariables();

            var sizeDelta = _holderRectTransform.sizeDelta;

            var size = new Vector2
            {
                x = displayRotatedIcon ? sizeDelta.y - 10 : sizeDelta.x - 10,
                y = displayRotatedIcon ? sizeDelta.x - 10 : sizeDelta.y - 10
            };

            return size;
        }

        public AbstractItem GetAbstractItem()
        {
            return IsEquipped() ? EquippedAbstractItemUi : null;
        }

        private void EnableDefaultImage()
        {
            InitVariables();

            if (_childedImage == null) return;

            _childedImage.gameObject.SetActive(true);
        }

        private void DisableDefaultImage()
        {
            InitVariables();

            if (_childedImage == null) return;

            _childedImage.gameObject.SetActive(false);
        }
    }
}