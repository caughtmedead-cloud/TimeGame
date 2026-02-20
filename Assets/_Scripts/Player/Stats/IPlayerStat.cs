using System;

public interface IPlayerStat
{
    string StatIdentifier { get; }
    
    float CurrentValue { get; }
    
    float MaxValue { get; }
    
    float MinValue { get; }
    
    void ModifyStat(float amount);
    
    void SetStat(float value);
    
    event Action<float, float> OnStatChanged;
}
