using UnityEngine;
using UnityEngine.InputSystem;
using TimeGame.Systems.Inventory;
using TimeGame.Systems.Inventory.UI;

namespace LastMile.DebugTools
{
    /// <summary>
    /// Dedicated in-game debug menu for testing the Last Mile day/night cycle and
    /// shift systems end-to-end without touching gameplay code or waiting real time.
    /// Toggle visibility with F1. Only compiled into editor/development builds.
    /// </summary>
    public class NightCycleDebugMenu : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Auto-resolved from NightCycleManager.Instance if left unassigned.")]
        [SerializeField] private NightCycleManager nightCycle;

        [Tooltip("Auto-resolved from ShiftController.Instance if left unassigned.")]
        [SerializeField] private ShiftController shiftController;

        [Header("Settings")]
        [SerializeField] private KeyCode legacyToggleKey = KeyCode.F1;
        [SerializeField] private bool startVisible = false;

        // Fixed panel size, used to anchor it to the top-right corner of the screen.
        private const float PanelWidth = 340f;
        private const float PanelHeight = 640f;
        private const float PanelMargin = 10f;

        private bool isVisible;
        private string shiftDurationInput = "60";
        private float debugTimeScale = 1f;
        private DeliveryStop[] cachedDeliveryStops = new DeliveryStop[0];

        private void Start()
        {
            isVisible = startVisible;

            if (nightCycle == null)
            {
                nightCycle = NightCycleManager.Instance;
            }

            if (shiftController == null)
            {
                shiftController = ShiftController.Instance;
            }

            RefreshDeliveryStops();
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
            if (shiftController == null)
            {
                shiftController = ShiftController.Instance;
            }

            // Keep the delivery stop list fresh in case stops are added/removed at
            // runtime — a plain array refresh is cheap enough to run every frame here.
            RefreshDeliveryStops();
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
            DrawShiftStateControls();
            GUILayout.Space(10);
            DrawDeliveryControls();

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

            if (shiftController != null)
            {
                int totalStops = shiftController.ActiveManifest != null ? shiftController.ActiveManifest.stops.Count : 0;
                GUILayout.Label($"Player Sheltered: {shiftController.IsPlayerSheltered}");
                GUILayout.Label($"Completed Stops: {shiftController.CompletedStops.Count} / {totalStops}");

                foreach (DeliveryStop stop in cachedDeliveryStops)
                {
                    if (stop == null || stop.Definition == null || stop.IsFullyDelivered)
                    {
                        continue;
                    }

                    GUILayout.Label($"  In progress — {stop.StopId}: {stop.DeliveredQuantity}/{stop.Definition.requiredQuantity} (remaining {stop.RemainingQuantity})");
                }
            }
            else
            {
                GUILayout.Label("No ShiftController found in scene.");
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

        private void DrawShiftStateControls()
        {
            GUILayout.Label("--- Shelter ---");
            if (shiftController == null)
            {
                GUILayout.Label("No ShiftController found in scene.");
                return;
            }

            if (GUILayout.Button(shiftController.IsPlayerSheltered ? "Unshelter Player" : "Shelter Player"))
            {
                shiftController.SetPlayerSheltered(!shiftController.IsPlayerSheltered);
            }

            if (GUILayout.Button("Force Sunrise Now"))
            {
                nightCycle.SkipToNormalizedTime(1f);
            }
        }

        private void DrawDeliveryControls()
        {
            GUILayout.Label("--- Deliveries ---");

            if (cachedDeliveryStops.Length == 0)
            {
                GUILayout.Label("No DeliveryStop found in scene.");
                return;
            }

            foreach (DeliveryStop stop in cachedDeliveryStops)
            {
                if (stop == null)
                {
                    continue;
                }

                string label = $"{stop.StopId} | InRange: {stop.IsPlayerInRange} | Delivered: {stop.DeliveredQuantity}/{(stop.Definition != null ? stop.Definition.requiredQuantity : 0)} | Remaining: {stop.RemainingQuantity}";
                GUILayout.Label(label);

                if (GUILayout.Button($"Open Delivery Window ({stop.StopId})"))
                {
                    if (LastMile.UI.DeliveryGridWindowUI.Instance == null)
                    {
                        Debug.LogWarning("[NightCycleDebugMenu] Cannot open delivery window — no DeliveryGridWindowUI found in scene.");
                    }
                    else
                    {
                        LastMile.UI.DeliveryGridWindowUI.Instance.Open(stop);
                    }
                }
            }
        }

        // Scans the scene for all DeliveryStop components. Called once at Start and
        // once per frame in Update so newly spawned/destroyed stops stay reflected
        // without a manual refresh button — keeping this menu as simple to use as
        // possible.
        private void RefreshDeliveryStops()
        {
            cachedDeliveryStops = Object.FindObjectsByType<DeliveryStop>(FindObjectsSortMode.None);
        }

    }
}
