using UnityEngine;
using UnityEngine.UI;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Visual ghost/preview of an item while dragging.
    /// Shows where the item will be placed with color feedback (green=valid, red=invalid).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class InventoryItemGhost : MonoBehaviour
    {
        [Header("Visual Feedback")]
        [Tooltip("Color when placement is valid")]
        [SerializeField] private Color validColor = new Color(0.3f, 1f, 0.3f, 0.6f);

        [Tooltip("Color when placement is blocked")]
        [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.6f);

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Image image;
        private RectTransform visualTransform;

        /// <summary>
        /// Currently displayed item definition.
        /// </summary>
        public InventoryItemSO CurrentItem { get; private set; }

        /// <summary>
        /// Current rotation of the ghost.
        /// </summary>
        public GridDirection CurrentRotation { get; private set; }

        /// <summary>
        /// Current cell size.
        /// </summary>
        private float cellSize;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();

            // Configure CanvasGroup for dragging
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false; // Ghost should not block mouse
            canvasGroup.interactable = false;

            // Start hidden
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Initialize ghost with item data.
        /// </summary>
        public void Initialize(InventoryItemSO item, GridDirection rotation, float cellSize)
        {
            CurrentItem = item;
            CurrentRotation = rotation;
            this.cellSize = cellSize;

            // Create visual child if needed
            if (visualTransform == null)
            {
                CreateVisualChild();
            }

            // Setup outer transform (grid-aligned, bottom-left pivot)
            rectTransform.pivot = new Vector2(0, 0);
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(0, 0);

            // Calculate size based on rotation
            int width = item.GetRotatedWidth(rotation);
            int height = item.GetRotatedHeight(rotation);
            rectTransform.sizeDelta = new Vector2(width * cellSize, height * cellSize);

            // No rotation on outer
            rectTransform.localRotation = Quaternion.identity;

            // Set rotation on visual child
            float angle = item.GetRotationAngle(rotation);
            visualTransform.localRotation = Quaternion.Euler(0, 0, -angle);

            // Set sprite
            if (item.ItemIcon != null)
            {
                image.sprite = item.ItemIcon;
                image.color = validColor; // Start with valid color
            }
            else
            {
                // Fallback color
                image.sprite = null;
                image.color = validColor;
            }

            Debug.Log($"[InventoryItemGhost] Initialized with {item.ItemName}, size {width}x{height}");
        }

        /// <summary>
        /// Create the child GameObject that holds the rotatable visual.
        /// </summary>
        private void CreateVisualChild()
        {
            GameObject visualObj = new GameObject("GhostVisual");
            visualObj.transform.SetParent(transform, false);

            visualTransform = visualObj.AddComponent<RectTransform>();
            visualTransform.anchorMin = Vector2.zero;
            visualTransform.anchorMax = Vector2.one;
            visualTransform.sizeDelta = Vector2.zero;
            visualTransform.anchoredPosition = Vector2.zero;
            visualTransform.pivot = new Vector2(0.5f, 0.5f); // Center rotation

            image = visualObj.AddComponent<Image>();
            image.raycastTarget = false; // Ghost should not block raycasts
        }

        /// <summary>
        /// Rotate the ghost to the next direction.
        /// </summary>
        public void Rotate()
        {
            if (CurrentItem == null || !CurrentItem.CanRotate) return;

            CurrentRotation = CurrentItem.GetNextRotation(CurrentRotation);

            // Update size
            int width = CurrentItem.GetRotatedWidth(CurrentRotation);
            int height = CurrentItem.GetRotatedHeight(CurrentRotation);
            rectTransform.sizeDelta = new Vector2(width * cellSize, height * cellSize);

            // Update visual rotation
            float angle = CurrentItem.GetRotationAngle(CurrentRotation);
            visualTransform.localRotation = Quaternion.Euler(0, 0, -angle);

            Debug.Log($"[InventoryItemGhost] Rotated to {CurrentRotation}, new size {width}x{height}");
        }

        /// <summary>
        /// Set the ghost position (in local space, snapped to grid).
        /// </summary>
        public void SetPosition(Vector2 localPosition)
        {
            rectTransform.anchoredPosition = localPosition;
        }

        /// <summary>
        /// Set visual feedback (valid/invalid placement).
        /// </summary>
        public void SetValid(bool isValid)
        {
            if (image != null)
            {
                Color targetColor = isValid ? validColor : invalidColor;
                
                // Preserve the sprite, just change color
                if (CurrentItem != null && CurrentItem.ItemIcon != null)
                {
                    image.color = targetColor;
                }
                else
                {
                    image.color = targetColor;
                }
            }
        }

        /// <summary>
        /// Show the ghost.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            canvasGroup.alpha = 0.7f; // Semi-transparent
            Debug.Log("[InventoryItemGhost] Ghost shown");
        }

        /// <summary>
        /// Hide the ghost.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            canvasGroup.alpha = 0f;
            Debug.Log("[InventoryItemGhost] Ghost hidden");
        }

        /// <summary>
        /// Is the ghost currently visible?
        /// </summary>
        public bool IsVisible()
        {
            return gameObject.activeSelf && canvasGroup.alpha > 0f;
        }
    }
}
