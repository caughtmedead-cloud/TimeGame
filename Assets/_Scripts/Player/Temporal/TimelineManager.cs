using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections;  // ADDED for IEnumerator

/// <summary>
/// Manages timeline state and transitions for a player.
/// FIXED: Uses singleton pattern to reliably find TimelineSceneSetup for pure clients.
/// </summary>
[RequireComponent(typeof(TemporalStability))]
public class TimelineManager : NetworkBehaviour
{
    [Header("Component References")]
    [Tooltip("Auto-assigned if not set")]
    [SerializeField] private TemporalStability temporalStability;
    
    [Header("Random Timeline Shifts")]
    [Tooltip("Enable random unpredictable timeline shifts. When disabled, uses simple threshold-based transitions for testing.")]
    [SerializeField] private bool useRandomShifts = false;
    
    [Space(10)]
    [Header("→ Random Shift Settings (Only when enabled)")]
    [Tooltip("When stability drops below this, random timeline shifts begin")]
    [SerializeField] private float randomShiftThreshold = 50f;
    
    [Tooltip("Minimum seconds between random shift checks")]
    [SerializeField] private float minShiftInterval = 5f;
    
    [Tooltip("Maximum seconds between random shift checks")]
    [SerializeField] private float maxShiftInterval = 15f;
    
    [Tooltip("Probability of timeline shift when check occurs (0.3 = 30% chance)")]
    [Range(0f, 1f)]
    [SerializeField] private float shiftChance = 0.3f;
    
    [Space(10)]
    [Header("→ Simple Mode Settings (Only when disabled)")]
    [Tooltip("Above this = Present timeline (60-100%)")]
    [SerializeField] private float futureTransitionThreshold = 60f;
    
    [Tooltip("Below futureThreshold but above this = Future timeline (20-59%)")]
    [SerializeField] private float pastTransitionThreshold = 20f;
    
    private readonly SyncVar<TimelineState> _currentTimeline = new SyncVar<TimelineState>(
        new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers)
    );
    
    private float nextRandomShiftCheck = 0f;
    private TimelineSceneSetup _timelineSceneSetup;
    
    public event Action<TimelineState> OnTimelineTransition;
    
    public enum TimelineState
    {
        Past,
        Present,
        Future
    }
    
    public TimelineState CurrentTimeline => _currentTimeline.Value;
    
    private void Awake()
    {
        if (temporalStability == null)
        {
            temporalStability = GetComponent<TemporalStability>();
        }
        
        _currentTimeline.OnChange += OnTimelineChanged;
    }
    
    /// <summary>
    /// FIXED: Uses singleton pattern instead of FindFirstObjectByType for reliable pure client support.
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // FIXED: Use singleton instead of FindFirstObjectByType
        // FindFirstObjectByType fails for pure clients due to unpredictable scene search order
        _timelineSceneSetup = TimelineSceneSetup.Instance;
        
        if (_timelineSceneSetup == null)
        {
            Debug.LogWarning($"[TimelineManager] ⚠️ TimelineSceneSetup singleton not registered yet for Player {Owner.ClientId} - waiting...");
            StartCoroutine(WaitForTimelineSceneSetup());
            return;
        }
        
        Debug.Log($"[TimelineManager] ✅ Found TimelineSceneSetup singleton for Player {Owner.ClientId}");
        InitializeServerState();
    }
    
    /// <summary>
    /// ADDED: Coroutine to wait for TimelineSceneSetup singleton registration.
    /// This handles race conditions where players spawn before Bootstrap scene completes initialization.
    /// </summary>
    private IEnumerator WaitForTimelineSceneSetup()
    {
        Debug.Log($"[TimelineManager] ⏳ Waiting for TimelineSceneSetup singleton for Player {Owner.ClientId}...");
        
        float timeout = Time.time + 5f;
        int attempts = 0;
        
        while (_timelineSceneSetup == null && Time.time < timeout)
        {
            _timelineSceneSetup = TimelineSceneSetup.Instance;
            
            if (_timelineSceneSetup != null)
            {
                Debug.Log($"[TimelineManager] ✅ Found TimelineSceneSetup singleton (after {attempts} attempts) for Player {Owner.ClientId}");
                InitializeServerState();
                yield break;
            }
            
            attempts++;
            yield return null;
        }
        
        if (_timelineSceneSetup == null)
        {
            Debug.LogError($"[TimelineManager] ❌ TimelineSceneSetup STILL not found after {attempts} attempts! Player {Owner.ClientId} CANNOT transition timelines!");
        }
    }
    
    /// <summary>
    /// ADDED: Helper method to initialize server state once TimelineSceneSetup is found.
    /// </summary>
    private void InitializeServerState()
    {
        _currentTimeline.Value = TimelineState.Present;
        nextRandomShiftCheck = 0f;
        
        Debug.Log($"[TimelineManager] OnStartServer - Player {Owner.ClientId} initialized on server");
        
        if (temporalStability != null)
        {
            temporalStability.OnStabilityUpdated += OnStabilityChanged_Server;
            Debug.Log($"[TimelineManager] Subscribed to TemporalStability events for Player {Owner.ClientId}");
        }
        else
        {
            Debug.LogError("[TimelineManager] TemporalStability component not found!");
        }
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        
        if (temporalStability != null)
        {
            temporalStability.OnStabilityUpdated -= OnStabilityChanged_Server;
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[TimelineManager] OnStartClient - Player {Owner.ClientId} started on client (IsOwner: {IsOwner})");
    }
    
    private void Update()
    {
        if (!IsServerStarted) return;
        
        if (useRandomShifts && temporalStability != null)
        {
            if (temporalStability.CurrentStability >= randomShiftThreshold && nextRandomShiftCheck != 0f)
            {
                nextRandomShiftCheck = 0f;
                Debug.Log($"[Server] Player {Owner.ClientId} stability recovered - random shifts disabled");
            }
        }
    }
    
    [Server]
    private void OnStabilityChanged_Server(float newStability, float maxStability)
    {
        if (useRandomShifts)
        {
            if (newStability < randomShiftThreshold)
            {
                CheckRandomTimelineShift();
            }
        }
        else
        {
            CheckSimpleTimelineTransition(newStability);
        }
    }
    
    [Server]
    private void CheckSimpleTimelineTransition(float stability)
    {
        TimelineState targetTimeline;
        
        if (stability >= futureTransitionThreshold)
        {
            targetTimeline = TimelineState.Present;
        }
        else if (stability >= pastTransitionThreshold)
        {
            targetTimeline = TimelineState.Future;
        }
        else
        {
            targetTimeline = TimelineState.Past;
        }
        
        if (targetTimeline != _currentTimeline.Value)
        {
            Debug.Log($"[Server] Player {Owner.ClientId} stability {stability:F1}% - transitioning to {targetTimeline}");
            TransitionTimeline(targetTimeline);
        }
    }
    
    [Server]
    private void CheckRandomTimelineShift()
    {
        if (nextRandomShiftCheck == 0f)
        {
            nextRandomShiftCheck = Time.time + UnityEngine.Random.Range(minShiftInterval, maxShiftInterval);
            Debug.Log($"[Server] Player {Owner.ClientId} entered unstable temporal zone - random shifts enabled");
            return;
        }
        
        if (Time.time >= nextRandomShiftCheck)
        {
            if (UnityEngine.Random.value <= shiftChance)
            {
                TimelineState randomTimeline = (UnityEngine.Random.value > 0.5f) 
                    ? TimelineState.Future 
                    : TimelineState.Past;
                
                if (randomTimeline != _currentTimeline.Value)
                {
                    Debug.Log($"[Server] Random temporal tear! Shifting player {Owner.ClientId} to {randomTimeline}");
                    TransitionTimeline(randomTimeline);
                }
            }
            else
            {
                if (_currentTimeline.Value != TimelineState.Present && UnityEngine.Random.value > 0.5f)
                {
                    Debug.Log($"[Server] Temporal tear stabilized - returning player {Owner.ClientId} to Present");
                    TransitionTimeline(TimelineState.Present);
                }
            }
            
            nextRandomShiftCheck = Time.time + UnityEngine.Random.Range(minShiftInterval, maxShiftInterval);
        }
    }
    
    [Server]
    private void TransitionTimeline(TimelineState newTimeline)
    {
        if (_timelineSceneSetup == null)
        {
            Debug.LogError($"[TimelineManager] ❌ Cannot transition - TimelineSceneSetup not found on server for Player {Owner.ClientId}!");
            return;
        }
        
        TimelineState oldTimeline = _currentTimeline.Value;
        
        Debug.Log($"[TimelineManager] [Server] Player {Owner.ClientId} transitioning from {oldTimeline} to {newTimeline}");
        
        _currentTimeline.Value = newTimeline;
        _timelineSceneSetup.TransitionPlayerTimeline(NetworkObject, oldTimeline, newTimeline);
        
        Debug.Log($"[TimelineManager] ✅ [Server] Scene transition initiated for player {Owner.ClientId}: {oldTimeline} → {newTimeline}");
    }
    
    private void OnTimelineChanged(TimelineState previousTimeline, TimelineState newTimeline, bool asServer)
    {
        Debug.Log($"[TimelineManager] OnTimelineChanged - Player {Owner.ClientId}, IsOwner: {IsOwner}, asServer: {asServer}, {previousTimeline} → {newTimeline}");
        
        if (!IsOwner)
        {
            Debug.Log($"[TimelineManager] Skipping UI update - not owner (Player {Owner.ClientId})");
            return;
        }
        
        Debug.Log($"[TimelineManager] Firing OnTimelineTransition event for Player {Owner.ClientId}");
        
        OnTimelineTransition?.Invoke(newTimeline);
        Debug.Log($"[Client] Timeline shifted: {previousTimeline} → {newTimeline}");
    }
}