using System.Collections.Generic;
using UnityEngine;

namespace LastMile
{
    /// <summary>
    /// Singleton orchestrator that owns the active delivery manifest, starts/ends
    /// shifts against NightCycleManager, tracks shelter state and delivery
    /// progress, and resolves the single pass/fail check at sunrise.
    ///
    /// Per the GDD's "immediate lethal fail-state" design: the shift's outcome is
    /// decided in one shot the moment NightCycleManager.OnSunrise fires — succeeded
    /// if the player is currently sheltered, failed otherwise. No sustained-exposure
    /// timer or raycasting is involved.
    /// </summary>
    public class ShiftController : MonoBehaviour
    {
        [Header("Testing")]
        [Tooltip("Optional — if assigned, Start() automatically begins this manifest so this system is testable standalone without waiting on a chaining system to begin shifts.")]
        [SerializeField] private ShiftManifestSO testManifest;

        private readonly List<ShiftManifestSO.DeliveryStopDefinition> completedStops = new List<ShiftManifestSO.DeliveryStopDefinition>();
        private bool hasResolvedShiftOutcome;
        private bool isSubscribedToSunrise;

        public static ShiftController Instance { get; private set; }

        /// <summary>The manifest currently active for this shift, or null if no shift has been started.</summary>
        public ShiftManifestSO ActiveManifest { get; private set; }

        /// <summary>True while the player is standing inside a shelter zone.</summary>
        public bool IsPlayerSheltered { get; private set; }

        /// <summary>All delivery stops successfully completed so far this shift.</summary>
        public IReadOnlyList<ShiftManifestSO.DeliveryStopDefinition> CompletedStops => completedStops;

        /// <summary>Fires when a new shift begins with its manifest.</summary>
        public event System.Action<ShiftManifestSO> OnShiftStarted;

        /// <summary>Fires whenever a delivery stop is successfully completed.</summary>
        public event System.Action<ShiftManifestSO.DeliveryStopDefinition> OnStopDelivered;

        /// <summary>Fires whenever a delivery stop receives a partial delivery — progress-only, does not mark the stop complete.</summary>
        public event System.Action<ShiftManifestSO.DeliveryStopDefinition, int> OnStopPartiallyDelivered;

        /// <summary>Fires once at sunrise if the player was sheltered at that moment.</summary>
        public event System.Action OnShiftSucceeded;

        /// <summary>Fires once at sunrise if the player was not sheltered at that moment — the GDD's immediate lethal fail-state.</summary>
        public event System.Action OnShiftFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ShiftController] Duplicate instance detected — destroying self.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (testManifest != null)
            {
                BeginShift(testManifest);
            }
        }

        /// <summary>Resets shift state and starts the countdown for the given manifest.</summary>
        public void BeginShift(ShiftManifestSO manifest)
        {
            if (NightCycleManager.Instance == null)
            {
                Debug.LogError("[ShiftController] Cannot begin shift — no NightCycleManager found in scene.");
                return;
            }

            ActiveManifest = manifest;
            completedStops.Clear();
            hasResolvedShiftOutcome = false;

            if (isSubscribedToSunrise)
            {
                NightCycleManager.Instance.OnSunrise -= HandleSunrise;
            }
            NightCycleManager.Instance.OnSunrise += HandleSunrise;
            isSubscribedToSunrise = true;

            NightCycleManager.Instance.StartShift(manifest.shiftDurationSeconds);

            OnShiftStarted?.Invoke(manifest);
        }

        /// <summary>Called by the shelter zone effect whenever the player enters/exits shelter.</summary>
        public void SetPlayerSheltered(bool sheltered)
        {
            IsPlayerSheltered = sheltered;
        }

        /// <summary>Called by DeliveryStop.ResolveDelivery once a stop's accumulated delivered quantity reaches its requirement.</summary>
        public void NotifyStopDelivered(ShiftManifestSO.DeliveryStopDefinition stop)
        {
            if (completedStops.Contains(stop))
            {
                return;
            }

            completedStops.Add(stop);
            OnStopDelivered?.Invoke(stop);
        }

        /// <summary>Called by DeliveryStop.ResolveDelivery when some but not enough cargo was delivered — progress-tracking only, does not affect completedStops.</summary>
        public void NotifyStopPartiallyDelivered(ShiftManifestSO.DeliveryStopDefinition stop, int deliveredQuantity)
        {
            OnStopPartiallyDelivered?.Invoke(stop, deliveredQuantity);
        }

        // Resolves the shift's single pass/fail outcome the moment sunrise fires.
        // Guarded so it only ever resolves once per shift — does not unsubscribe
        // from OnSunrise here, since BeginShift always re-subscribes fresh.
        private void HandleSunrise()
        {
            if (hasResolvedShiftOutcome)
            {
                return;
            }
            hasResolvedShiftOutcome = true;

            if (IsPlayerSheltered)
            {
                OnShiftSucceeded?.Invoke();
            }
            else
            {
                OnShiftFailed?.Invoke();
            }
        }
    }
}
