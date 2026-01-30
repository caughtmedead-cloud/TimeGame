using UnityEngine;
using UnityEngine.UI;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Simple visual representation of a placed inventory item.
    /// Just displays the sprite - no interaction logic here.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class InventoryItemVisual : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Image image;

        /// <summary>
        /// The placed item this visual represents.
        /// </summary>
        public PlacedItem PlacedItem { get; private set; }

        /// <summary>
        /// The inventory item definition.
        /// </summary>
        public InventoryItemSO ItemDefinition { get; private set; }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            image = GetComponent<Image>();
        }

        /// <summary>
        /// Initialize this visual with item data.
        /// </summary>
        public void Initialize(PlacedItem placedItem, InventoryItemSO itemDef, float cellSize)
        {
            PlacedItem = placedItem;
            ItemDefinition = itemDef;

            // CRITICAL: Set pivot to bottom-left to match grid cells
            // This ensures items are positioned from their bottom-left corner, not center
            rectTransform.pivot = new Vector2(0, 0);
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(0, 0);

            // Set sprite
            if (itemDef.ItemIcon != null)
            {
                image.sprite = itemDef.ItemIcon;
                image.enabled = true;
            }
            else
            {
                // Fallback: colored square
                image.sprite = null;
                image.color = GetColorForRarity(itemDef.Rarity);
                image.enabled = true;
            }

            // Calculate size based on item dimensions
            int width = itemDef.GetRotatedWidth(placedItem.Rotation);
            int height = itemDef.GetRotatedHeight(placedItem.Rotation);

            rectTransform.sizeDelta = new Vector2(width * cellSize, height * cellSize);

            // Set rotation
            float angle = itemDef.GetRotationAngle(placedItem.Rotation);
            rectTransform.localRotation = Quaternion.Euler(0, 0, -angle);

            // Position is set by InventoryGridVisual
        }

        /// <summary>
        /// Set the visual position (in local space).
        /// </summary>
        public void SetPosition(Vector2 localPosition)
        {
            rectTransform.anchoredPosition = localPosition;
        }

        /// <summary>
        /// Get color for rarity (fallback when no icon).
        /// </summary>
        private Color GetColorForRarity(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return new Color(0.7f, 0.7f, 0.7f);
                case ItemRarity.Uncommon: return new Color(0.3f, 0.8f, 0.3f);
                case ItemRarity.Rare: return new Color(0.3f, 0.5f, 1f);
                case ItemRarity.Epic: return new Color(0.8f, 0.3f, 0.8f);
                case ItemRarity.Legendary: return new Color(1f, 0.6f, 0f);
                default: return Color.white;
            }
        }

        /// <summary>
        /// Cleanup when destroyed.
        /// </summary>
        private void OnDestroy()
        {
            PlacedItem = null;
            ItemDefinition = null;
        }
    }
}
