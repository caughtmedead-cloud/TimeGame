using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        private void Awake()
        {
            // Setup default visuals
            if (crosshairImage != null)
            {
                crosshairImage.color = crosshairColor;
            }

            if (interactionHintText != null)
            {
                interactionHintText.text = defaultHint;
            }
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
