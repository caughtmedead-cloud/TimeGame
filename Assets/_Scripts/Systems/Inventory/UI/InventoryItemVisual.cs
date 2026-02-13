using UnityEngine;
using UnityEngine.UI;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Simple visual representation of a placed inventory item.
    /// Uses a two-transform hierarchy for proper rotation:
    /// - Outer: Grid-aligned positioning (pivot 0,0)
    /// - Inner: Visual rotation (pivot 0.5,0.5 - center)
    /// 
    /// Can be used for:
    /// 1. Grid items (with drag-drop)
    /// 2. Standalone drag visuals (no drag-drop)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class InventoryItemVisual : MonoBehaviour
    {
        private RectTransform rectTransform;
        private RectTransform visualTransform; // Child that actually rotates
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
            
            // Create child for visual rotation
            CreateVisualChild();
        }

        /// <summary>
        /// Create the child GameObject that holds the rotatable visual.
        /// </summary>
        private void CreateVisualChild()
        {
            // Create child GameObject
            GameObject visualObj = new GameObject("Visual");
            visualObj.transform.SetParent(transform, false);
            
            // Setup RectTransform
            visualTransform = visualObj.AddComponent<RectTransform>();
            visualTransform.anchorMin = Vector2.zero;
            visualTransform.anchorMax = Vector2.one;
            visualTransform.sizeDelta = Vector2.zero;
            visualTransform.anchoredPosition = Vector2.zero;
            
            // This will rotate around its center (0.5, 0.5)
            visualTransform.pivot = new Vector2(0.5f, 0.5f);
            
            // Add Image component
            image = visualObj.AddComponent<Image>();
        }

        /// <summary>
        /// Initialize this visual with item data.
        /// gridVisual: null for standalone visuals, non-null for grid items
        /// </summary>
        public void Initialize(PlacedItem placedItem, InventoryItemSO itemDef, float cellSize, InventoryGridVisual gridVisual)
        {
            PlacedItem = placedItem;
            ItemDefinition = itemDef;

            // Ensure visual child exists
            if (visualTransform == null)
            {
                CreateVisualChild();
            }

            // OUTER TRANSFORM: Grid-aligned positioning (bottom-left pivot)
            rectTransform.pivot = new Vector2(0, 0);
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(0, 0);

            // Calculate size based on item dimensions
            int width = itemDef.GetRotatedWidth(placedItem.Rotation);
            int height = itemDef.GetRotatedHeight(placedItem.Rotation);

            rectTransform.sizeDelta = new Vector2(width * cellSize, height * cellSize);
            
            // No rotation on outer transform
            rectTransform.localRotation = Quaternion.identity;

            // INNER TRANSFORM: Visual rotation (center pivot)
            // Set rotation on the visual child
            float angle = itemDef.GetRotationAngle(placedItem.Rotation);
            visualTransform.localRotation = Quaternion.Euler(0, 0, -angle);

            // Set sprite on visual child
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

            // Add drag-drop component ONLY if we have a grid (grid items only)
            // Standalone visuals (equipment drags, temporary visuals) don't need drag-drop
            if (gridVisual != null)
            {
                InventoryItemDragDrop dragDrop = gameObject.GetComponent<InventoryItemDragDrop>();
                if (dragDrop == null)
                {
                    dragDrop = gameObject.AddComponent<InventoryItemDragDrop>();
                }

                // Also need CanvasGroup for drag-drop
                CanvasGroup canvasGroup = gameObject.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }

                // Setup drag-drop component
                dragDrop.Setup(gridVisual, placedItem.InstanceID);
            }
            else
            {
                // Standalone visual - ensure CanvasGroup exists but no drag-drop
                CanvasGroup canvasGroup = gameObject.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            // Position is set by InventoryGridVisual or by the caller
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
