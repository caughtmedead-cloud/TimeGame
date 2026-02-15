using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;

/// <summary>
/// PURE DATA COMPONENT: Tracks temporal stability value for a player.
/// FIXED: OnStabilityUpdated now fires on BOTH server and client.
/// </summary>
public class TemporalStability : NetworkBehaviour
{
    [Header("Stability Configuration")]
    [Tooltip("Maximum stability value (100 = fully stable)")]
    [SerializeField] private float maxStability = 100f;
    
    [Tooltip("Threshold for critical stability warning (flashing red UI)")]
    [SerializeField] private float criticalThreshold = 25f;

    [Header("Debug Settings")]
    [SerializeField] private bool verboseLogging = false;
    [SerializeField] private float logThreshold = 5.0f;

    private readonly SyncVar<float> _currentStability = new SyncVar<float>(
        new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers)
    );
    
    private bool wasCritical = false;
    private float lastLoggedStability = -1f;
    
    // FIXED: This event now fires on BOTH server AND client
    // Server: For TimelineManager to detect stability changes
    // Client: For UI updates
    public event Action<float, float> OnStabilityUpdated; // (current, max)
    
    // Client-only events (UI-specific)
    public event Action OnCriticalStability;
    public event Action OnStabilityDepleted;
    
    public float CurrentStability => _currentStability.Value;
    public float MaxStability => maxStability;
    public float CriticalThreshold => criticalThreshold;
    public bool IsCritical => _currentStability.Value <= criticalThreshold;
    public bool IsDepleted => _currentStability.Value <= 0f;
    
    private void Awake()
    {
        _currentStability.OnChange += OnStabilityChanged;
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentStability.Value = maxStability;
        wasCritical = false;
        lastLoggedStability = maxStability;
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
    }
    
    [Server]
    public void ModifyStability(float amount)
    {
        float oldValue = _currentStability.Value;
        _currentStability.Value = Mathf.Clamp(_currentStability.Value + amount, 0f, maxStability);
        
        if (verboseLogging && (lastLoggedStability < 0 || Mathf.Abs(_currentStability.Value - lastLoggedStability) >= logThreshold))
        {
            Debug.Log($"[TemporalStability] Player {Owner.ClientId} stability: {_currentStability.Value:F1}% (changed {amount:F1})");
            lastLoggedStability = _currentStability.Value;
        }
    }
    
    [Server]
    public void SetStability(float value)
    {
        _currentStability.Value = Mathf.Clamp(value, 0f, maxStability);
        lastLoggedStability = _currentStability.Value;
    }
    
    /// <summary>
    /// FIXED: Now invokes OnStabilityUpdated on BOTH server and client.
    /// Server needs this event for TimelineManager to detect stability changes.
    /// Client needs this event for UI updates.
    /// </summary>
    private void OnStabilityChanged(float previousValue, float newValue, bool asServer)
    {
        if (verboseLogging && (lastLoggedStability < 0 || Mathf.Abs(newValue - lastLoggedStability) >= logThreshold))
        {
            Debug.Log($"[TemporalStability] OnStabilityChanged - Player {Owner.ClientId}, newValue: {newValue:F1}%, asServer: {asServer}, IsOwner: {IsOwner}");
            lastLoggedStability = newValue;
        }
        
        // CRITICAL FIX: Fire OnStabilityUpdated on BOTH server and client!
        // TimelineManager subscribes on server, UI subscribes on client
        OnStabilityUpdated?.Invoke(newValue, maxStability);
        
        // Client-only UI events (only fire on owner)
        if (IsOwner)
        {
            // Check for critical stability warning
            bool isNowCritical = newValue <= criticalThreshold;
            if (isNowCritical && !wasCritical)
            {
                OnCriticalStability?.Invoke();
                wasCritical = true;
            }
            else if (!isNowCritical && wasCritical)
            {
                wasCritical = false;
            }
            
            // Check for depletion
            if (newValue <= 0f && previousValue > 0f)
            {
                OnStabilityDepleted?.Invoke();
            }
        }
    }
    
#if UNITY_EDITOR
    // ===== DEBUG COMMANDS =====
    
    [ContextMenu("Debug: Degrade Stability (10)")]
    private void DebugDegradeStability()
    {
        if (IsServerStarted)
            ModifyStability(-10f);
    }
    
    [ContextMenu("Debug: Restore Stability (10)")]
    private void DebugRestoreStability()
    {
        if (IsServerStarted)
            ModifyStability(10f);
    }
    
    [ContextMenu("Debug: Set Critical (25%)")]
    private void DebugSetCritical()
    {
        if (IsServerStarted)
            SetStability(25f);
    }
    
    [ContextMenu("Debug: Set Depleted (0%)")]
    private void DebugSetDepleted()
    {
        if (IsServerStarted)
            SetStability(0f);
    }
    
    [ContextMenu("Debug: Restore Full")]
    private void DebugRestoreFull()
    {
        if (IsServerStarted)
            SetStability(maxStability);
    }
#endif
}