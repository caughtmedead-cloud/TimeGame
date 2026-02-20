using UnityEngine;

[System.Obsolete("Use StatModifierEffect with targetStatIdentifier='Stability' instead. This class remains for backward compatibility.")]
public class StabilityDrainEffect : StatModifierEffect
{
    public float StabilityDrainRate
    {
        get => modificationRate;
        set => modificationRate = value;
    }
    
    protected override void OnEffectInitialized()
    {
        base.OnEffectInitialized();
        
        targetStatIdentifier = "Stability";
    }
    
    private void Reset()
    {
        targetStatIdentifier = "Stability";
        modificationRate = -2.0f;
    }
}
