using UnityEngine;

namespace LastMile
{
    /// <summary>
    /// Attached to the player. Determines lethal sun exposure purely from
    /// shelter-zone state (see ShelterEffect) — "exposed" simply means "not
    /// currently inside a shelter zone" during the dawn lethal window. No
    /// raycasting or level-geometry/layer setup required, so behavior is
    /// identical everywhere on the map and easy to reason about.
    /// </summary>
    public class SunExposureDetector : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The active night cycle manager. Auto-resolved from NightCycleManager.Instance if left unassigned.")]
        [SerializeField] private NightCycleManager nightCycle;

        [Header("Exposure Check")]
        [Tooltip("How long the player must be continuously unsheltered during the dawn window before it is considered lethal.")]
        [SerializeField] private float sustainedExposureSecondsBeforeLethal = 10f;

        private float sustainedExposureTimer;
        private bool hasFiredLethalExposure;

        public bool IsSheltered { get; private set; }

        /// <summary>How long the player must be continuously unsheltered before it is considered lethal — exposed for the debug menu to cross-check against the dawn window length.</summary>
        public float SustainedExposureSecondsBeforeLethal => sustainedExposureSecondsBeforeLethal;

        /// <summary>True whenever the player is unsheltered during the dawn lethal window (for HUD warning use before the lethal trigger fires).</summary>
        public bool IsCurrentlyExposed => nightCycle != null && nightCycle.IsDawnLethalWindow && !IsSheltered;

        public event System.Action OnLethalExposureConfirmed;

        private void Start()
        {
            if (nightCycle == null)
            {
                nightCycle = NightCycleManager.Instance;
            }

            if (nightCycle == null)
            {
                Debug.LogError("[SunExposureDetector] No NightCycleManager found. Assign one in the Inspector or ensure NightCycleManager.Instance exists in the scene.");
            }
        }

        // Note: there is deliberately no sunrise-triggered instant death. Once the dawn
        // window begins, IsDawnLethalWindow stays true permanently (see NightCycleManager),
        // so Update() keeps accumulating unsheltered time seamlessly across the sunrise
        // moment — only sustainedExposureSecondsBeforeLethal continuous unsheltered time
        // is ever lethal, whether that span starts before or after the timer hits zero.

        private void Update()
        {
            if (nightCycle == null || hasFiredLethalExposure)
            {
                return;
            }

            if (IsCurrentlyExposed)
            {
                sustainedExposureTimer += Time.deltaTime;
                if (sustainedExposureTimer >= sustainedExposureSecondsBeforeLethal)
                {
                    FireLethalExposure();
                }
            }
            else
            {
                sustainedExposureTimer = 0f;
            }
        }

        /// <summary>Called by ShelterEffect when the player enters/exits a shelter zone.</summary>
        public void SetSheltered(bool sheltered)
        {
            IsSheltered = sheltered;
            if (sheltered)
            {
                sustainedExposureTimer = 0f;
            }
        }

        /// <summary>
        /// Debug/test hook — immediately fires the lethal exposure event, bypassing the
        /// sustain timer entirely. Used to validate the fail-state flow on demand.
        /// </summary>
        public void DebugForceLethalExposure()
        {
            FireLethalExposure();
        }

        // Shared by the sustained-exposure timer and the debug menu's force-lethal
        // button so both go through one guarded, fire-once trigger.
        private void FireLethalExposure()
        {
            if (hasFiredLethalExposure)
            {
                return;
            }

            hasFiredLethalExposure = true;
            OnLethalExposureConfirmed?.Invoke();
        }
    }
}
