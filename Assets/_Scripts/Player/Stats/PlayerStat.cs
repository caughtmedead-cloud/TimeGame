using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;

public abstract class PlayerStat : NetworkBehaviour, IPlayerStat
{
    [Header("Stat Configuration")]
    [SerializeField] protected float maxValue = 100f;
    [SerializeField] protected float minValue = 0f;
    [SerializeField] protected float startingValue = 100f;
    
    [Header("Thresholds (Optional)")]
    [Tooltip("Threshold for critical warning (optional)")]
    [SerializeField] protected float criticalThreshold = 25f;
    [SerializeField] protected bool useCriticalThreshold = false;
    
    [Header("Debug Settings")]
    [SerializeField] protected bool verboseLogging = false;
    [SerializeField] protected float logThreshold = 5.0f;
    
    protected readonly SyncVar<float> _currentValue = new SyncVar<float>(
        new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers)
    );
    
    protected bool wasCritical = false;
    protected float lastLoggedValue = -1f;
    
    public event Action<float, float> OnStatChanged;
    public event Action OnCriticalReached;
    public event Action OnMinReached;
    public event Action OnMaxReached;
    
    public abstract string StatIdentifier { get; }
    public float CurrentValue => _currentValue.Value;
    public float MaxValue => maxValue;
    public float MinValue => minValue;
    public float CriticalThreshold => criticalThreshold;
    public bool IsCritical => useCriticalThreshold && _currentValue.Value <= criticalThreshold;
    public bool IsAtMin => _currentValue.Value <= minValue;
    public bool IsAtMax => _currentValue.Value >= maxValue;
    
    protected virtual void Awake()
    {
        _currentValue.OnChange += OnValueChanged;
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentValue.Value = startingValue;
        wasCritical = false;
        lastLoggedValue = startingValue;
    }
    
    [Server]
    public virtual void ModifyStat(float amount)
    {
        float oldValue = _currentValue.Value;
        _currentValue.Value = Mathf.Clamp(_currentValue.Value + amount, minValue, maxValue);
        
        if (verboseLogging && (lastLoggedValue < 0 || Mathf.Abs(_currentValue.Value - lastLoggedValue) >= logThreshold))
        {
            Debug.Log($"[{StatIdentifier}] Player {Owner.ClientId} {StatIdentifier}: {_currentValue.Value:F1}/{maxValue:F1} (changed {amount:F1})");
            lastLoggedValue = _currentValue.Value;
        }
    }
    
    [Server]
    public virtual void SetStat(float value)
    {
        _currentValue.Value = Mathf.Clamp(value, minValue, maxValue);
        lastLoggedValue = _currentValue.Value;
    }
    
    protected virtual void OnValueChanged(float previousValue, float newValue, bool asServer)
    {
        if (verboseLogging && (lastLoggedValue < 0 || Mathf.Abs(newValue - lastLoggedValue) >= logThreshold))
        {
            Debug.Log($"[{StatIdentifier}] OnValueChanged - Player {Owner.ClientId}, newValue: {newValue:F1}/{maxValue:F1}, asServer: {asServer}");
            lastLoggedValue = newValue;
        }
        
        OnStatChanged?.Invoke(newValue, maxValue);
        
        if (IsOwner)
        {
            if (useCriticalThreshold)
            {
                bool isNowCritical = newValue <= criticalThreshold;
                if (isNowCritical && !wasCritical)
                {
                    OnCriticalReached?.Invoke();
                    wasCritical = true;
                }
                else if (!isNowCritical && wasCritical)
                {
                    wasCritical = false;
                }
            }
            
            if (newValue <= minValue && previousValue > minValue)
            {
                OnMinReached?.Invoke();
            }
            
            if (newValue >= maxValue && previousValue < maxValue)
            {
                OnMaxReached?.Invoke();
            }
        }
    }
    
    [ContextMenu("Debug: Increase Stat (+10)")]
    protected void DebugIncreaseStat()
    {
        if (IsServerStarted)
            ModifyStat(10f);
    }
    
    [ContextMenu("Debug: Decrease Stat (-10)")]
    protected void DebugDecreaseStat()
    {
        if (IsServerStarted)
            ModifyStat(-10f);
    }
    
    [ContextMenu("Debug: Set to Max")]
    protected void DebugSetMax()
    {
        if (IsServerStarted)
            SetStat(maxValue);
    }
    
    [ContextMenu("Debug: Set to Min")]
    protected void DebugSetMin()
    {
        if (IsServerStarted)
            SetStat(minValue);
    }
    
    [ContextMenu("Debug: Set to Critical")]
    protected void DebugSetCritical()
    {
        if (IsServerStarted && useCriticalThreshold)
            SetStat(criticalThreshold);
    }
}
