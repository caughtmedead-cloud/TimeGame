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
        private AspectRatioFitter iconAspectFitter; // Scales icon to fill cell while preserving aspect ratio
        private Image backgroundImage;  // White tile drag preview
        private TextMeshProUGUI stackCountText;  // Stack count display (bottom-right)
        private TextMeshProUGUI usesCountText;   // Uses count display (bottom-left)
        private bool outlineApplied = false;     // Track if outline has been applied to TextMeshPro

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

        private void OnEnable()
        {
            // Apply TextMeshPro outline if not already applied
            // This handles cases where visuals are created while UI is inactive
            if (!outlineApplied && stackCountText != null && usesCountText != null)
            {
                stackCountText.outlineWidth = 0.2f;
                stackCountText.outlineColor = Color.black;
                usesCountText.outlineWidth = 0.2f;
                usesCountText.outlineColor = Color.black;
                outlineApplied = true;
            }
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
            // Always enabled and transparent — this is the ACTUAL raycast hit area for drag.
            // Kept cell-sized so clicks outside the cell footprint are never detected.
            backgroundImage.color = Color.clear;
            backgroundImage.raycastTarget = true;
            backgroundImage.enabled = true;
            
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
            // Raycast handled by DragPreview_Background (always cell-sized) — icon can overflow safely
            image.raycastTarget = false;
            // Do NOT use preserveAspect — AspectRatioFitter below handles this better
            image.preserveAspect = false;
            // Start with no sprite and fully transparent color
            image.sprite = null;
            image.color = Color.clear;
            // Must stay ENABLED for EventSystem to work, but transparent so nothing shows
            image.enabled = true;

            // AspectRatioFitter: scales icon to fill the cell footprint as much as possible
            // while preserving the sprite's native aspect ratio.
            // EnvelopeParent = fill/cover (may overflow slightly, clipped by parent mask if present)
            // FitInParent    = letterbox/pillarbox (always fully visible, may have gaps)
            iconAspectFitter = iconObj.AddComponent<AspectRatioFitter>();
            iconAspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            // Create stack count text (top-right corner)
            // CRITICAL: Parent to ROOT transform (not visualTransform) so it NEVER rotates
            GameObject stackCountObj = new GameObject("StackCount");
            stackCountObj.transform.SetParent(transform, false); // <- PARENT TO ROOT!

            RectTransform stackCountRT = stackCountObj.AddComponent<RectTransform>();
            stackCountRT.anchorMin = new Vector2(1, 1); // Top-right
            stackCountRT.anchorMax = new Vector2(1, 1);
            stackCountRT.pivot = new Vector2(1, 1);
            stackCountRT.anchoredPosition = new Vector2(-2, -2); // Small padding from edge
            stackCountRT.sizeDelta = new Vector2(40, 20);

            stackCountText = stackCountObj.AddComponent<TextMeshProUGUI>();
            stackCountText.fontSize = 14;
            stackCountText.fontStyle = FontStyles.Bold;
            stackCountText.color = Color.white;
            stackCountText.alignment = TextAlignmentOptions.TopRight;
            stackCountText.raycastTarget = false;
            stackCountText.enableWordWrapping = false;

            // Add outline for readability (only if GameObject is active, otherwise defer)
            if (gameObject.activeInHierarchy)
            {
                stackCountText.outlineWidth = 0.2f;
                stackCountText.outlineColor = Color.black;
                outlineApplied = true; // Mark as applied
            }

            stackCountText.text = "";
            stackCountText.enabled = false; // Hidden by default

            // Create uses count text (bottom-left corner)
            // CRITICAL: Parent to ROOT transform (not visualTransform) so it NEVER rotates
            GameObject usesCountObj = new GameObject("UsesCount");
            usesCountObj.transform.SetParent(transform, false); // <- PARENT TO ROOT!

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

            // Add outline for readability (only if GameObject is active, otherwise defer)
            if (gameObject.activeInHierarchy)
            {
                usesCountText.outlineWidth = 0.2f;
                usesCountText.outlineColor = Color.black;
                // outlineApplied already set above for stackCountText
            }

            usesCountText.text = "";
            usesCountText.enabled = false; // Hidden by default
        }

        /// <summary>
        /// Initialize this visual with item data.
        /// gridVisual: null for standalone visuals, non-null for grid items
        /// </summary>
        public void Initialize(PlacedItem placedItem, InventoryItemSO itemDef, float cellSize, InventoryGridVisual gridVisual, TMPro.TMP_FontAsset font = null)
        {
            PlacedItem = placedItem;
            ItemDefinition = itemDef;

            // Apply font to text components if provided
            if (font != null)
            {
                if (stackCountText != null) stackCountText.font = font;
                if (usesCountText != null) usesCountText.font = font;
            }

            // Ensure rectTransform is set (Awake may not have been called if parent is inactive)
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            // Ensure visual child exists (Awake may not have been called)
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
            // NOTE: Text elements are parented to root, so they won't rotate
            float angle = itemDef.GetRotationAngle(placedItem.Rotation);
            visualTransform.localRotation = Quaternion.Euler(0, 0, -angle);

            // Set sprite on visual child
            if (itemDef.ItemIcon != null)
            {
                image.sprite = itemDef.ItemIcon;
                image.color = Color.white; // Proper white for sprite display
                // Tell the AspectRatioFitter the sprite's native pixel ratio
                if (iconAspectFitter != null && itemDef.ItemIcon.rect.height > 0)
                    iconAspectFitter.aspectRatio = itemDef.ItemIcon.rect.width / itemDef.ItemIcon.rect.height;
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
        /// Resize the outer RectTransform to match new rotated item dimensions.
        /// Called by InventoryDragHandler when the player rotates during drag.
        /// </summary>
        public void ResizeForRotation(InventoryItemSO itemDef, GridPlacement.GridDirection rotation, float cellSize)
        {
            if (rectTransform == null || itemDef == null) return;
            int w = itemDef.GetRotatedWidth(rotation);
            int h = itemDef.GetRotatedHeight(rotation);
            rectTransform.sizeDelta = new Vector2(w * cellSize, h * cellSize);
            // Update aspect fitter ratio to match the new bounding box orientation
            if (iconAspectFitter != null && image != null && image.sprite != null)
                iconAspectFitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
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
            // enabled stays true — background is always on for raycasting
        }

        /// <summary>
        /// Disable drag background visual (for placed items).
        /// Does NOT disable the Image component — it must stay enabled for raycasting.
        /// Called by InventoryDragHandler when drag ends.
        /// </summary>
        public void DisableDragBackground()
        {
            if (backgroundImage != null)
            {
                // Clear sprite and make transparent — keeps raycastTarget active
                backgroundImage.sprite = null;
                backgroundImage.color = Color.clear;
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
