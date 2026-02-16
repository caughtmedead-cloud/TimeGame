using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        private Image backgroundImage;  // White tile drag preview
        private TextMeshProUGUI stackCountText;  // Stack count display (bottom-right)
        private TextMeshProUGUI usesCountText;   // Uses count display (bottom-left)

        /// <summary>
        /// The placed item this visual represents.
        /// </summary>
        public PlacedItem PlacedItem { get; private set; }

        /// <summary>
        /// The inventory item definition.
        /// </summary>
        public InventoryItemSO ItemDefinition { get; private set; }
        
        /// <summary>
        /// The inner visual transform that holds rotation.
        /// </summary>
        public RectTransform VisualTransform => visualTransform;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            
            // Create child for visual rotation
            CreateVisualChild();
        }

        /// <summary>
        /// Create the child GameObject that holds the rotatable visual.
        /// Creates: Visual > DragPreview_Background + ItemSprite_Icon
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
            
            // Create BACKGROUND GameObject (white tile - renders BEHIND icon, only visible during drag)
            GameObject backgroundObj = new GameObject("DragPreview_Background");
            backgroundObj.transform.SetParent(visualTransform, false);
            
            RectTransform backgroundRT = backgroundObj.AddComponent<RectTransform>();
            backgroundRT.anchorMin = Vector2.zero;
            backgroundRT.anchorMax = Vector2.one;
            backgroundRT.sizeDelta = Vector2.zero;
            backgroundRT.anchoredPosition = Vector2.zero;
            backgroundRT.pivot = new Vector2(0.5f, 0.5f);
            
            backgroundImage = backgroundObj.AddComponent<Image>();
            backgroundImage.enabled = false;  // Hidden by default
            backgroundImage.raycastTarget = false; // Don't block raycasts
            
            // Create ICON GameObject (item sprite - renders ON TOP of background)
            GameObject iconObj = new GameObject("ItemSprite_Icon");
            iconObj.transform.SetParent(visualTransform, false);
            
            RectTransform iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.sizeDelta = Vector2.zero;
            iconRT.anchoredPosition = Vector2.zero;
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            
            image = iconObj.AddComponent<Image>();
            // CRITICAL: raycastTarget must be TRUE for drag system to detect the item!
            image.raycastTarget = true;
            // Start with no sprite and fully transparent color
            image.sprite = null;
            image.color = Color.clear;
            // Must stay ENABLED for EventSystem to work, but transparent so nothing shows
            image.enabled = true;

            // Create stack count text (bottom-right corner)
            GameObject stackCountObj = new GameObject("StackCount");
            stackCountObj.transform.SetParent(visualTransform, false);

            RectTransform stackCountRT = stackCountObj.AddComponent<RectTransform>();
            stackCountRT.anchorMin = new Vector2(1, 0); // Bottom-right
            stackCountRT.anchorMax = new Vector2(1, 0);
            stackCountRT.pivot = new Vector2(1, 0);
            stackCountRT.anchoredPosition = new Vector2(-2, 2); // Small padding from edge
            stackCountRT.sizeDelta = new Vector2(40, 20);

            stackCountText = stackCountObj.AddComponent<TextMeshProUGUI>();
            stackCountText.fontSize = 14;
            stackCountText.fontStyle = FontStyles.Bold;
            stackCountText.color = Color.white;
            stackCountText.alignment = TextAlignmentOptions.BottomRight;
            stackCountText.raycastTarget = false;
            stackCountText.enableWordWrapping = false;

            // Add outline for readability
            stackCountText.outlineWidth = 0.2f;
            stackCountText.outlineColor = Color.black;

            stackCountText.text = "";
            stackCountText.enabled = false; // Hidden by default

            // Create uses count text (bottom-left corner)
            GameObject usesCountObj = new GameObject("UsesCount");
            usesCountObj.transform.SetParent(visualTransform, false);

            RectTransform usesCountRT = usesCountObj.AddComponent<RectTransform>();
            usesCountRT.anchorMin = new Vector2(0, 0); // Bottom-left
            usesCountRT.anchorMax = new Vector2(0, 0);
            usesCountRT.pivot = new Vector2(0, 0);
            usesCountRT.anchoredPosition = new Vector2(2, 2); // Small padding from edge
            usesCountRT.sizeDelta = new Vector2(50, 20); // Slightly wider for "10/10" format

            usesCountText = usesCountObj.AddComponent<TextMeshProUGUI>();
            usesCountText.fontSize = 14;
            usesCountText.fontStyle = FontStyles.Bold;
            usesCountText.color = Color.white;
            usesCountText.alignment = TextAlignmentOptions.BottomLeft;
            usesCountText.raycastTarget = false;
            usesCountText.enableWordWrapping = false;
            usesCountText.outlineWidth = 0.2f;
            usesCountText.outlineColor = Color.black;
            usesCountText.text = "";
            usesCountText.enabled = false; // Hidden by default
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
                image.color = Color.white; // Proper white for sprite display
                // Already enabled in CreateVisualChild()
            }
            else
            {
                // Fallback: colored square
                image.sprite = null;
                image.color = GetColorForRarity(itemDef.Rarity);
                // Already enabled in CreateVisualChild()
            }

            // Update stack count display
            UpdateStackCount();

            // Update uses count display
            UpdateUsesCount();

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
        /// Enable white tile background for drag preview.
        /// Called by InventoryDragHandler when drag starts.
        /// </summary>
        public void EnableDragBackground(InventoryTileSprites tiles)
        {
            if (backgroundImage == null || tiles == null || tiles.ghostTileSprite == null)
            {
                return;
            }
            
            backgroundImage.sprite = tiles.ghostTileSprite;
            backgroundImage.type = tiles.ghostSpriteType;
            
            if (tiles.ghostSpriteType == Image.Type.Sliced)
            {
                backgroundImage.fillCenter = tiles.fillCenterTiled;
            }
            
            backgroundImage.color = new Color(1f, 1f, 1f, 0.7f); // Semi-transparent white
            backgroundImage.enabled = true;
        }

        /// <summary>
        /// Disable drag background (for placed items).
        /// Called by InventoryDragHandler when drag ends.
        /// </summary>
        public void DisableDragBackground()
        {
            if (backgroundImage != null)
            {
                backgroundImage.enabled = false;
            }
        }

        /// <summary>
        /// Update the stack count display based on the current PlacedItem.
        /// Shows count only if > 1 and item is stackable.
        /// </summary>
        public void UpdateStackCount()
        {
            if (stackCountText == null || PlacedItem == null || ItemDefinition == null)
                return;

            // Only show stack count if item is stackable and count > 1
            if (ItemDefinition.IsStackable && PlacedItem.StackCount > 1)
            {
                stackCountText.text = PlacedItem.StackCount.ToString();
                stackCountText.enabled = true;
            }
            else
            {
                stackCountText.text = "";
                stackCountText.enabled = false;
            }
        }

        /// <summary>
        /// Update the uses count display based on the current ItemInstance.
        /// Shows "current/max" format only if item has limited uses.
        /// </summary>
        public void UpdateUsesCount()
        {
            if (usesCountText == null || PlacedItem == null || ItemDefinition == null)
                return;

            // Check if this is a tracked item with uses
            if (PlacedItem.IsInstanceTracked &&
                PlacedItem.ItemInstances != null &&
                PlacedItem.ItemInstances.Count > 0)
            {
                // Get first item in stack (the one that would be used)
                ItemInstance firstItem = PlacedItem.ItemInstances[0];

                // Only show uses if item has limited uses (not -1)
                if (firstItem.UsesRemaining >= 0 && ItemDefinition is InventoryItemSO itemSO && itemSO.HasLimitedUses)
                {
                    usesCountText.text = $"{firstItem.UsesRemaining}/{itemSO.MaxUses}";
                    usesCountText.enabled = true;
                }
                else
                {
                    usesCountText.text = "";
                    usesCountText.enabled = false;
                }
            }
            else
            {
                usesCountText.text = "";
                usesCountText.enabled = false;
            }
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
