using UnityEngine;
using UnityEngine.UI;
using TimeGame.Systems.GridPlacement;
using DG.Tweening;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Visual ghost/preview of an item while dragging.
    /// Shows where the item will be placed with color feedback (green=valid, red=invalid).
    /// Features smooth DOTween rotation animations for professional feel.
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

        [Header("Rotation Animation (DOTween)")]
        [Tooltip("Duration of rotation animation in seconds")]
        [SerializeField] private float rotationDuration = 0.15f;

        [Tooltip("Easing function for rotation (try OutBack for bounce!)")]
        [SerializeField] private Ease rotationEase = Ease.OutQuad;

        [Tooltip("Add a slight scale punch when rotating for extra polish")]
        [SerializeField] private bool useScalePunch = true;

        [Tooltip("Scale punch strength (1.0 = no punch, 1.2 = 20% larger)")]
        [SerializeField] private float scalePunchAmount = 1.15f;

        [Tooltip("Duration of scale punch animation")]
        [SerializeField] private float scalePunchDuration = 0.1f;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Image image;
        private RectTransform visualTransform;

        // DOTween tracking (use Tween base class to support both Tweener and Sequence)
        private Tween currentRotationTween;
        private Tween currentSizeTween;
        private Tween currentScaleTween;
        
        // Track current angle for smooth rotation transitions
        private float currentAngle = 0f;

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

            // Set rotation on visual child (instant, no animation on init)
            currentAngle = item.GetRotationAngle(rotation);
            visualTransform.localRotation = Quaternion.Euler(0, 0, -currentAngle);

            // Reset scale
            visualTransform.localScale = Vector3.one;

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
        /// Rotate the ghost to the next direction with smooth DOTween animation.
        /// FIX: Use relative rotation to prevent wrap-around spinning.
        /// </summary>
        public void Rotate()
        {
            if (CurrentItem == null || !CurrentItem.CanRotate) return;

            CurrentRotation = CurrentItem.GetNextRotation(CurrentRotation);

            // Calculate new dimensions
            int newWidth = CurrentItem.GetRotatedWidth(CurrentRotation);
            int newHeight = CurrentItem.GetRotatedHeight(CurrentRotation);
            Vector2 newSize = new Vector2(newWidth * cellSize, newHeight * cellSize);

            // Calculate new rotation angle
            float newAngle = CurrentItem.GetRotationAngle(CurrentRotation);
            
            // FIX: Calculate rotation delta to rotate in shortest direction
            // This prevents the 270° -> 0° wrap-around spin bug
            float angleDelta = Mathf.DeltaAngle(currentAngle, newAngle);
            float targetAngle = currentAngle + angleDelta;
            
            // Update tracked angle
            currentAngle = newAngle;

            // Kill any existing tweens to prevent conflicts
            KillActiveTweens();

            // Animate size change smoothly
            currentSizeTween = rectTransform
                .DOSizeDelta(newSize, rotationDuration)
                .SetEase(rotationEase);

            // FIX: Animate rotation smoothly using relative rotation (no more rapid spinning!)
            currentRotationTween = visualTransform
                .DOLocalRotate(new Vector3(0, 0, -targetAngle), rotationDuration, RotateMode.Fast)
                .SetEase(rotationEase);

            // Optional: Scale punch for extra polish (makes it feel snappy!)
            if (useScalePunch)
            {
                visualTransform.localScale = Vector3.one; // Reset scale
                
                // Quick scale up then back to normal
                Sequence punchSequence = DOTween.Sequence();
                punchSequence.Append(visualTransform.DOScale(scalePunchAmount, scalePunchDuration * 0.5f).SetEase(Ease.OutQuad));
                punchSequence.Append(visualTransform.DOScale(1f, scalePunchDuration * 0.5f).SetEase(Ease.InQuad));
                
                currentScaleTween = punchSequence;
            }

            Debug.Log($"[InventoryItemGhost] Smoothly rotating to {CurrentRotation}, new size {newWidth}x{newHeight}");
        }

        /// <summary>
        /// Kill all active DOTween animations on this ghost.
        /// Prevents animation conflicts when rotating rapidly.
        /// </summary>
        private void KillActiveTweens()
        {
            currentRotationTween?.Kill();
            currentSizeTween?.Kill();
            currentScaleTween?.Kill();
            
            currentRotationTween = null;
            currentSizeTween = null;
            currentScaleTween = null;
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
        /// Hide the ghost and cleanup animations.
        /// </summary>
        public void Hide()
        {
            // Kill any active animations
            KillActiveTweens();
            
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

        private void OnDestroy()
        {
            // Cleanup: Kill all tweens when ghost is destroyed
            KillActiveTweens();
        }
    }
}
