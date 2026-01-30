using UnityEngine;
using UnityEngine.UI;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Visual ghost/preview of an item being dragged.
    /// Shows where the item will be placed with visual feedback (green = valid, red = invalid).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class InventoryItemGhost : MonoBehaviour
    {
        [Header("Visual Feedback")]
        [SerializeField] private Color validColor = new Color(0.3f, 1f, 0.3f, 0.5f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        private RectTransform rectTransform;
        private RectTransform visualTransform;
        private Image image;
        private CanvasGroup canvasGroup;

        // Current state
        private InventoryItemSO currentItem;
        private GridDirection currentRotation;
        private float cellSize;
        private bool isValid;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            
            // Add CanvasGroup for transparency control
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0.7f;
            canvasGroup.blocksRaycasts = false; // Ghost shouldn't block mouse
            
            CreateVisualChild();
            
            // Start hidden
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Create the child GameObject that holds the rotatable visual.
        /// </summary>
        private void CreateVisualChild()
        {
            GameObject visualObj = new GameObject("Visual");
            visualObj.transform.SetParent(transform, false);
            
            visualTransform = visualObj.AddComponent<RectTransform>();
            visualTransform.anchorMin = Vector2.zero;
            visualTransform.anchorMax = Vector2.one;
            visualTransform.sizeDelta = Vector2.zero;
            visualTransform.anchoredPosition = Vector2.zero;
            visualTransform.pivot = new Vector2(0.5f, 0.5f);
            
            image = visualObj.AddComponent<Image>();
        }

        /// <summary>
        /// Show the ghost for a specific item.
        /// </summary>
        public void Show(InventoryItemSO item, GridDirection rotation, float cellSize)
        {
            this.currentItem = item;
            this.currentRotation = rotation;
            this.cellSize = cellSize;

            // Outer transform setup
            rectTransform.pivot = new Vector2(0, 0);
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(0, 0);

            // Calculate size based on rotation
            int width = item.GetRotatedWidth(rotation);
            int height = item.GetRotatedHeight(rotation);
            rectTransform.sizeDelta = new Vector2(width * cellSize, height * cellSize);

            // Set sprite
            if (item.ItemIcon != null)
            {
                image.sprite = item.ItemIcon;
            }
            else
            {
                // Fallback: solid color
                image.sprite = null;
                image.color = Color.white;
            }

            // Set rotation on visual child
            float angle = item.GetRotationAngle(rotation);
            visualTransform.localRotation = Quaternion.Euler(0, 0, -angle);

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide the ghost.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Update ghost position to follow mouse.
        /// </summary>
        public void UpdatePosition(Vector2 localPosition)
        {
            rectTransform.anchoredPosition = localPosition;
        }

        /// <summary>
        /// Rotate the ghost (cycles through 4 directions).
        /// </summary>
        public void Rotate()
        {
            if (currentItem == null || !currentItem.CanRotate) return;

            // Cycle rotation
            currentRotation = currentItem.GetNextRotation(currentRotation);

            // Update size
            int width = currentItem.GetRotatedWidth(currentRotation);
            int height = currentItem.GetRotatedHeight(currentRotation);
            rectTransform.sizeDelta = new Vector2(width * cellSize, height * cellSize);

            // Update visual rotation
            float angle = currentItem.GetRotationAngle(currentRotation);
            visualTransform.localRotation = Quaternion.Euler(0, 0, -angle);
        }

        /// <summary>
        /// Set visual feedback based on placement validity.
        /// </summary>
        public void SetValid(bool valid)
        {
            isValid = valid;
            image.color = valid ? validColor : invalidColor;
        }

        /// <summary>
        /// Get current rotation.
        /// </summary>
        public GridDirection GetCurrentRotation()
        {
            return currentRotation;
        }

        /// <summary>
        /// Get current item.
        /// </summary>
        public InventoryItemSO GetCurrentItem()
        {
            return currentItem;
        }

        /// <summary>
        /// Is the ghost currently valid for placement?
        /// </summary>
        public bool IsValid()
        {
            return isValid;
        }
    }
}
