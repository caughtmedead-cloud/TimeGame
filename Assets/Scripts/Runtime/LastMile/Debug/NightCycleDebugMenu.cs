using UnityEngine;
using UnityEngine.InputSystem;

namespace LastMile.DebugTools
{
    /// <summary>
    /// Dedicated in-game debug menu for testing the Last Mile day/night cycle and
    /// shelter systems end-to-end without touching gameplay code or waiting real time.
    /// Toggle visibility with F1. Only compiled into editor/development builds.
    /// </summary>
    public class NightCycleDebugMenu : MonoBehaviour
    {
        // Tag used project-wide to locate the player GameObject (see ItemUsageHandler).
        private const string PlayerTag = "Player";

        [Header("References")]
        [Tooltip("Auto-resolved from NightCycleManager.Instance if left unassigned.")]
        [SerializeField] private NightCycleManager nightCycle;

        [Tooltip("Auto-resolved from the \"Player\"-tagged GameObject if left unassigned.")]
        [SerializeField] private SunExposureDetector exposureDetector;

        [Header("Settings")]
        [SerializeField] private KeyCode legacyToggleKey = KeyCode.F1;
        [SerializeField] private bool startVisible = false;

        // Fixed panel size, used to anchor it to the top-right corner of the screen.
        private const float PanelWidth = 340f;
        private const float PanelHeight = 520f;
        private const float PanelMargin = 10f;

        private bool isVisible;
        private string shiftDurationInput = "60";
        private float debugTimeScale = 1f;

        private void Start()
        {
            isVisible = startVisible;

            if (nightCycle == null)
            {
                nightCycle = NightCycleManager.Instance;
            }

            ResolveExposureDetector();
        }

        private void ResolveExposureDetector()
        {
            if (exposureDetector != null)
            {
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (player != null)
            {
                exposureDetector = player.GetComponent<SunExposureDetector>();
            }
        }

        private void Update()
        {
            // Support both input backends since the project runs "Both" active input
            // handling — Keyboard.current covers the new Input System, legacyToggleKey
            // covers editors/builds where only the old Input Manager is polled.
            bool togglePressed = (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                || Input.GetKeyDown(legacyToggleKey);

            if (togglePressed)
            {
                isVisible = !isVisible;
            }

            // Keep trying to resolve references — the player/manager may spawn after
            // this menu's Start() already ran once.
            if (nightCycle == null)
            {
                nightCycle = NightCycleManager.Instance;
            }
            if (exposureDetector == null)
            {
                ResolveExposureDetector();
            }
        }

        private void OnGUI()
        {
            if (!isVisible)
            {
                return;
            }

            float x = Screen.width - PanelWidth - PanelMargin;
            GUILayout.BeginArea(new Rect(x, PanelMargin, PanelWidth, PanelHeight), GUI.skin.box);
            GUILayout.Label("=== NIGHT CYCLE DEBUG MENU (F1 to hide) ===");
            GUILayout.Space(6);

            if (nightCycle == null)
            {
                GUILayout.Label("No NightCycleManager found in scene.");
                GUILayout.EndArea();
                return;
            }

            DrawState();
            GUILayout.Space(10);
            DrawShiftControls();
            GUILayout.Space(10);
            DrawTimeScaleControls();
            GUILayout.Space(10);
            DrawSkipControls();
            GUILayout.Space(10);
            DrawShelterControls();

            GUILayout.EndArea();
        }

        private void DrawState()
        {
            GUILayout.Label("--- State ---");
            int totalSeconds = Mathf.CeilToInt(Mathf.Max(nightCycle.TimeRemainingSeconds, 0f));
            GUILayout.Label($"Time Remaining: {totalSeconds / 60:00}:{totalSeconds % 60:00}");
            GUILayout.Label($"Normalized Time: {nightCycle.NormalizedTime:F2}");
            GUILayout.Label($"Shift Active: {nightCycle.IsShiftActive}");
            GUILayout.Label($"Dawn Lethal Window: {nightCycle.IsDawnLethalWindow}");

            if (exposureDetector != null)
            {
                GUILayout.Label($"Player Sheltered: {exposureDetector.IsSheltered}");
                GUILayout.Label($"Player Exposed: {exposureDetector.IsCurrentlyExposed}");
                GUILayout.Label($"Sustain Threshold: {exposureDetector.SustainedExposureSecondsBeforeLethal:F1}s continuous unsheltered (persists across sunrise)");
            }
            else
            {
                GUILayout.Label("No SunExposureDetector found on Player.");
            }
        }

        private void DrawShiftControls()
        {
            GUILayout.Label("--- Shift ---");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Duration (s):", GUILayout.Width(90));
            shiftDurationInput = GUILayout.TextField(shiftDurationInput, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Start Shift"))
            {
                if (float.TryParse(shiftDurationInput, out float duration))
                {
                    nightCycle.StartShift(duration);
                }
                else
                {
                    Debug.LogWarning("[NightCycleDebugMenu] Invalid shift duration entered.");
                }
            }
        }

        private void DrawTimeScaleControls()
        {
            GUILayout.Label("--- Time Scale ---");
            GUILayout.Label($"Current: {debugTimeScale:F1}x");
            debugTimeScale = GUILayout.HorizontalSlider(debugTimeScale, 0f, 50f);
            if (GUILayout.Button("Apply Time Scale"))
            {
                nightCycle.SetTimeScale(debugTimeScale);
            }
        }

        private void DrawSkipControls()
        {
            GUILayout.Label("--- Skip ---");
            if (GUILayout.Button("Skip to Dawn Window Start"))
            {
                nightCycle.SkipToNormalizedTime(nightCycle.DawnWindowStartNormalized);
            }
            if (GUILayout.Button("Force Sunrise (end shift)"))
            {
                nightCycle.SkipToNormalizedTime(1f);
            }
        }

        private void DrawShelterControls()
        {
            GUILayout.Label("--- Shelter / Exposure ---");
            if (exposureDetector == null)
            {
                GUILayout.Label("No SunExposureDetector found on Player.");
                return;
            }

            if (GUILayout.Button(exposureDetector.IsSheltered ? "Unshelter Player" : "Shelter Player"))
            {
                exposureDetector.SetSheltered(!exposureDetector.IsSheltered);
            }

            if (GUILayout.Button("Force Lethal Exposure"))
            {
                exposureDetector.DebugForceLethalExposure();
            }
        }
    }
}
