using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace LastMile.UI
{
    /// <summary>
    /// Lightweight "Press F to deliver to {recipient}" proximity prompt. Driven by
    /// DeliveryStop.StopsInRange (zone-based proximity from Phase 2) rather than
    /// PlayerItemInteraction's forward-raycast — the delivery prompt is proximity
    /// based, not look-based. Opens DeliveryGridWindowUI on Player.Interact.
    /// </summary>
    public class DeliveryPromptUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private TextMeshProUGUI promptText;

        private PlayerInputActions inputActions;

        private void OnEnable()
        {
            inputActions = new PlayerInputActions();
            inputActions.Enable();
            inputActions.Player.Interact.performed += OnInteractPressed;

            if (promptRoot != null)
            {
                promptRoot.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (inputActions != null)
            {
                inputActions.Player.Interact.performed -= OnInteractPressed;
                inputActions.Disable();
                inputActions.Dispose();
                inputActions = null;
            }
        }

        private void Update()
        {
            DeliveryStop stop = GetMostRecentStopInRange();
            bool canShowPrompt = stop != null
                && DeliveryGridWindowUI.Instance != null
                && !DeliveryGridWindowUI.Instance.IsOpen
                && !stop.IsFullyDelivered;

            if (canShowPrompt)
            {
                ShowPrompt(stop);
            }
            else if (promptRoot != null)
            {
                promptRoot.SetActive(false);
            }
        }

        private void ShowPrompt(DeliveryStop stop)
        {
            if (promptRoot != null)
            {
                promptRoot.SetActive(true);
            }

            if (promptText != null && stop.Definition != null)
            {
                promptText.text = $"Press F to deliver {stop.RemainingQuantity}x to {stop.Definition.recipientName}";
            }
        }

        private void OnInteractPressed(InputAction.CallbackContext context)
        {
            DeliveryStop stop = GetMostRecentStopInRange();
            if (stop == null || stop.IsFullyDelivered)
            {
                return;
            }

            if (DeliveryGridWindowUI.Instance == null || DeliveryGridWindowUI.Instance.IsOpen)
            {
                return;
            }

            DeliveryGridWindowUI.Instance.Open(stop);
        }

        // The most-recently-entered stop is the last one added to the registry —
        // good enough for the common case of only ever being near one stop at a time.
        private DeliveryStop GetMostRecentStopInRange()
        {
            var stopsInRange = DeliveryStop.StopsInRange;
            return stopsInRange.Count > 0 ? stopsInRange[stopsInRange.Count - 1] : null;
        }
    }
}
