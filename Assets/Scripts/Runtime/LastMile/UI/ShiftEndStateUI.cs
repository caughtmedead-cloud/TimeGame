using UnityEngine;

namespace LastMile.UI
{
    /// <summary>
    /// Minimal placeholder shift-outcome presentation so the full loop
    /// (countdown -> sunrise -> shelter check -> success/fail) can be verified
    /// end-to-end. Final outcome presentation with full reporting is Phase 5's
    /// job (see roadmap) — this only proves ShiftController.OnShiftSucceeded /
    /// OnShiftFailed fire correctly.
    /// </summary>
    public class ShiftEndStateUI : MonoBehaviour
    {
        // Tag used project-wide to locate the player GameObject (see ItemUsageHandler).
        private const string PlayerTag = "Player";

        [Header("References")]
        [Tooltip("Simple full-screen panel with placeholder 'YOU DIDN'T MAKE IT BACK IN TIME' text.")]
        [SerializeField] private GameObject failStatePanel;
        [Tooltip("Simple full-screen panel with placeholder 'SHIFT COMPLETE' text.")]
        [SerializeField] private GameObject successStatePanel;
        [Tooltip("Auto-resolved from the \"Player\"-tagged GameObject if left unassigned.")]
        [SerializeField] private PlayerController playerController;

        // True once subscribed to ShiftController's events. Subscription is retried
        // every Update() until it succeeds, since Unity doesn't guarantee this
        // object's OnEnable() runs after ShiftController.Awake() has set Instance —
        // cross-object Awake/OnEnable ordering is undefined, only Awake-before-any-Start is.
        private bool isSubscribedToShiftController;

        private void OnEnable()
        {
            ResolvePlayerReferences();
            TrySubscribeToShiftController();
        }

        private void Update()
        {
            if (!isSubscribedToShiftController)
            {
                TrySubscribeToShiftController();
            }
        }

        private void TrySubscribeToShiftController()
        {
            if (ShiftController.Instance == null)
            {
                return;
            }

            ShiftController.Instance.OnShiftFailed += HandleShiftFailed;
            ShiftController.Instance.OnShiftSucceeded += HandleShiftSucceeded;
            isSubscribedToShiftController = true;
        }

        private void ResolvePlayerReferences()
        {
            if (playerController != null)
            {
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (player == null)
            {
                return;
            }

            playerController = player.GetComponent<PlayerController>();
        }

        private void OnDisable()
        {
            if (isSubscribedToShiftController && ShiftController.Instance != null)
            {
                ShiftController.Instance.OnShiftFailed -= HandleShiftFailed;
                ShiftController.Instance.OnShiftSucceeded -= HandleShiftSucceeded;
            }

            isSubscribedToShiftController = false;
        }

        private void HandleShiftFailed()
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

            Debug.Log("[ShiftEndStateUI] Shift failed — player was not sheltered at sunrise.");
        }

        private void HandleShiftSucceeded()
        {
            if (successStatePanel != null)
            {
                successStatePanel.SetActive(true);
            }

            // The shift is over either way, so player input is disabled here too —
            // otherwise movement/camera-look input keeps reading through the panel
            // since PlayerController isn't itself a UI element the canvas can block.
            if (playerController != null && playerController.enabled)
            {
                playerController.enabled = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("[ShiftEndStateUI] Shift succeeded — player was sheltered at sunrise.");
        }
    }
}
