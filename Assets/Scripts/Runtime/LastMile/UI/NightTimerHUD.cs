using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LastMile.UI
{
    /// <summary>
    /// Drives the on-screen shift countdown display and a dawn-warning overlay
    /// that fades in once the lethal exposure window begins.
    /// </summary>
    public class NightTimerHUD : MonoBehaviour
    {
        // Seconds the dawn warning overlay takes to fade in once triggered.
        private const float DawnWarningFadeInSeconds = 2f;

        [Header("References")]
        [SerializeField] private TextMeshProUGUI timeRemainingText;
        [SerializeField] private Image dawnWarningOverlay;
        [SerializeField] private Color dawnWarningColor = new Color(1f, 0.3f, 0.1f, 0.5f);

        private bool isDawnWarningFading;
        private float dawnWarningFadeTimer;

        // Cached once subscribed, so OnDisable unsubscribes from the same instance
        // even if NightCycleManager.Instance were to change in the meantime.
        private NightCycleManager subscribedNightCycle;

        private void OnEnable()
        {
            // NightCycleManager.Instance may not be set yet: Unity does not guarantee
            // this object's OnEnable runs after NightCycleManager's Awake when they
            // live on different GameObjects. TrySubscribe() is retried every Update()
            // below until it succeeds, so a late-initializing manager is still caught.
            TrySubscribe();

            if (dawnWarningOverlay != null)
            {
                Color startColor = dawnWarningColor;
                startColor.a = 0f;
                dawnWarningOverlay.color = startColor;
            }
        }

        private void OnDisable()
        {
            if (subscribedNightCycle != null)
            {
                subscribedNightCycle.OnTimeRemainingChanged -= HandleTimeRemainingChanged;
                subscribedNightCycle.OnDawnWindowEntered -= HandleDawnWindowEntered;
                subscribedNightCycle = null;
            }
        }

        private void TrySubscribe()
        {
            if (subscribedNightCycle != null || NightCycleManager.Instance == null)
            {
                return;
            }

            subscribedNightCycle = NightCycleManager.Instance;
            subscribedNightCycle.OnTimeRemainingChanged += HandleTimeRemainingChanged;
            subscribedNightCycle.OnDawnWindowEntered += HandleDawnWindowEntered;
        }

        private void Update()
        {
            if (subscribedNightCycle == null)
            {
                TrySubscribe();
            }

            if (!isDawnWarningFading || dawnWarningOverlay == null)
            {
                return;
            }

            dawnWarningFadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(dawnWarningFadeTimer / DawnWarningFadeInSeconds);
            Color currentColor = dawnWarningColor;
            currentColor.a = Mathf.Lerp(0f, dawnWarningColor.a, t);
            dawnWarningOverlay.color = currentColor;

            if (t >= 1f)
            {
                isDawnWarningFading = false;
            }
        }

        private void HandleTimeRemainingChanged(float secondsRemaining)
        {
            if (timeRemainingText == null)
            {
                return;
            }

            int totalSeconds = Mathf.CeilToInt(Mathf.Max(secondsRemaining, 0f));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timeRemainingText.text = $"{minutes:00}:{seconds:00}";
        }

        private void HandleDawnWindowEntered()
        {
            isDawnWarningFading = true;
            dawnWarningFadeTimer = 0f;
        }
    }
}
