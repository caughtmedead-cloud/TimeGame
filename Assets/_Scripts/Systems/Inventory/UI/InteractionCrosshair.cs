using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Simple crosshair UI that appears when looking at interactable items.
    /// Shows item name and interaction prompts.
    /// </summary>
    public class InteractionCrosshair : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("Crosshair image (optional)")]
        [SerializeField] private Image crosshairImage;

        [Tooltip("Item name text")]
        [SerializeField] private TextMeshProUGUI itemNameText;

        [Tooltip("Interaction hint text (e.g., 'Left-click to pick up')")]
        [SerializeField] private TextMeshProUGUI interactionHintText;

        [Header("Settings")]
        [Tooltip("Crosshair color")]
        [SerializeField] private Color crosshairColor = Color.white;

        [Tooltip("Default interaction hint")]
        [SerializeField] private string defaultHint = "[F] Pick Up  |  [RMB] Options";

        [Header("Animation Settings")]
        [Tooltip("Minimum scale for the crosshair pulse animation")]
        [SerializeField] private float minScale = 0.8f;

        [Tooltip("Maximum scale for the crosshair pulse animation")]
        [SerializeField] private float maxScale = 1.2f;

        [Tooltip("Duration for one pulse cycle (scale down and back up)")]
        [SerializeField] private float pulseDuration = 0.6f;

        [Tooltip("Easing function for the pulse animation")]
        [SerializeField] private Ease pulseEase = Ease.InOutSine;

        [Tooltip("Enable debug logging for animation")]
        [SerializeField] private bool debugAnimation = false;

        private Tween scaleTween;

        private void Awake()
        {
            if (crosshairImage != null)
            {
                crosshairImage.color = crosshairColor;
            }

            if (interactionHintText != null)
            {
                interactionHintText.text = defaultHint;
            }
        }

        private void OnEnable()
        {
            StartPulseAnimation();
        }

        private void OnDisable()
        {
            StopPulseAnimation();
        }

        /// <summary>
        /// Set the item being looked at.
        /// </summary>
        public void SetItem(WorldItem item)
        {
            if (item == null)
            {
                if (itemNameText != null)
                {
                    itemNameText.text = "";
                }
                return;
            }

            if (itemNameText != null)
            {
                string displayText = item.ItemDefinition.ItemName;
                if (item.Quantity > 1)
                {
                    displayText += $" x{item.Quantity}";
                }
                itemNameText.text = displayText;
            }
        }

        private void StartPulseAnimation()
        {
            if (crosshairImage == null)
            {
                if (debugAnimation)
                {
                    Debug.LogWarning("[InteractionCrosshair] Cannot start animation - crosshairImage is null!");
                }
                return;
            }

            StopPulseAnimation();

            if (debugAnimation)
            {
                Debug.Log($"[InteractionCrosshair] Starting pulse animation (min: {minScale}, max: {maxScale}, duration: {pulseDuration})");
            }

            crosshairImage.transform.localScale = Vector3.one * maxScale;

            Sequence pulseSequence = DOTween.Sequence();
            pulseSequence.Append(crosshairImage.transform.DOScale(minScale, pulseDuration / 2f).SetEase(pulseEase));
            pulseSequence.Append(crosshairImage.transform.DOScale(maxScale, pulseDuration / 2f).SetEase(pulseEase));
            pulseSequence.SetLoops(-1);

            scaleTween = pulseSequence;

            if (debugAnimation)
            {
                Debug.Log($"[InteractionCrosshair] Animation started. Tween active: {scaleTween.IsActive()}");
            }
        }

        private void StopPulseAnimation()
        {
            if (scaleTween != null && scaleTween.IsActive())
            {
                scaleTween.Kill();
                
                if (debugAnimation)
                {
                    Debug.Log("[InteractionCrosshair] Pulse animation stopped");
                }
            }

            if (crosshairImage != null)
            {
                crosshairImage.transform.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// Set custom interaction hint text.
        /// </summary>
        public void SetInteractionHint(string hint)
        {
            if (interactionHintText != null)
            {
                interactionHintText.text = hint;
            }
        }

        /// <summary>
        /// Reset to default hint.
        /// </summary>
        public void ResetInteractionHint()
        {
            SetInteractionHint(defaultHint);
        }
    }
}
