using System;

public class TemporalStability : PlayerStat
{
    public override string StatIdentifier => "Stability";
    
    public event Action<float, float> OnStabilityUpdated;
    public event Action OnCriticalStability;
    public event Action OnStabilityDepleted;
    
    public float CurrentStability => CurrentValue;
    public float MaxStability => MaxValue;
    public bool IsDepleted => IsAtMin;
    
    protected override void Awake()
    {
        maxValue = 100f;
        minValue = 0f;
        startingValue = 100f;
        criticalThreshold = 25f;
        useCriticalThreshold = true;
        
        base.Awake();
        
        OnStatChanged += (current, max) => OnStabilityUpdated?.Invoke(current, max);
        OnCriticalReached += () => OnCriticalStability?.Invoke();
        OnMinReached += () => OnStabilityDepleted?.Invoke();
    }
    
    public void ModifyStability(float amount)
    {
        ModifyStat(amount);
    }
    
    public void SetStability(float value)
    {
        SetStat(value);
    }
}