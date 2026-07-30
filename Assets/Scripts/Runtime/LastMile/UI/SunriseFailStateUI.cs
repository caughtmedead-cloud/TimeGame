using UnityEngine;

namespace LastMile.UI
{
    /// <summary>
    /// Minimal placeholder fail-state presentation so the full loop (countdown ->
    /// dawn window -> exposure check -> death) can be verified end-to-end.
    /// Final fail-state presentation is an open design question left for a
    /// later pass (see GDD section 8) — this only proves the death trigger works.
    /// </summary>
    public class SunriseFailStateUI : MonoBehaviour
    {
        // Tag used project-wide to locate the player GameObject (see ItemUsageHandler).
        private const string PlayerTag = "Player";

        [Header("References")]
        [Tooltip("Auto-resolved from the \"Player\"-tagged GameObject if left unassigned.")]
        [SerializeField] private SunExposureDetector exposureDetector;
        [Tooltip("Simple full-screen panel with placeholder 'YOU DIDN'T MAKE IT BACK IN TIME' text.")]
        [SerializeField] private GameObject failStatePanel;
        [Tooltip("Auto-resolved from the \"Player\"-tagged GameObject if left unassigned.")]
        [SerializeField] private PlayerController playerController;

        private void OnEnable()
        {
            ResolvePlayerReferences();

            if (exposureDetector != null)
            {
                exposureDetector.OnLethalExposureConfirmed += HandleLethalExposureConfirmed;
            }
            else
            {
                Debug.LogError("[SunriseFailStateUI] exposureDetector is not assigned and could not be auto-resolved from a \"Player\"-tagged GameObject — fail state will never trigger.");
            }
        }

        private void ResolvePlayerReferences()
        {
            if (exposureDetector != null && playerController != null)
            {
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (player == null)
            {
                return;
            }

            if (exposureDetector == null)
            {
                exposureDetector = player.GetComponent<SunExposureDetector>();
            }

            if (playerController == null)
            {
                playerController = player.GetComponent<PlayerController>();
            }
        }

        private void OnDisable()
        {
            if (exposureDetector != null)
            {
                exposureDetector.OnLethalExposureConfirmed -= HandleLethalExposureConfirmed;
            }
        }

        private void HandleLethalExposureConfirmed()
        {
            if (failStatePanel != null)
            {
                failStatePanel.SetActive(true);
            }

            if (playerController != null && playerController.enabled)
            {
                playerController.enabled = false;
            }

            // Unlock the cursor so the player can interact with the fail-state panel.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("[SunriseFailStateUI] Lethal sun exposure confirmed — fail state triggered.");
        }
    }
}
