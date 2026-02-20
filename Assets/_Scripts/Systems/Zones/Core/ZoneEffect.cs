using UnityEngine;

public abstract class ZoneEffect : MonoBehaviour
{
    [Header("Effect Settings")]
    [Tooltip("Enable/disable this effect")]
    public bool isEnabled = true;
    
    [Header("Intensity")]
    [Tooltip("Use the zone's intensity gradient for this effect")]
    public bool useZoneIntensity = true;
    
    [Tooltip("Custom intensity curve (overrides zone curve if not using zone intensity)")]
    public AnimationCurve customIntensityCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);
    
    protected BaseZone zone;
    
    public virtual void Initialize(BaseZone parentZone)
    {
        zone = parentZone;
        OnEffectInitialized();
    }
    
    protected virtual void OnEffectInitialized()
    {
    }
    
    public abstract void OnPlayerEnter(GameObject player);
    
    public abstract void OnPlayerStay(GameObject player, float deltaTime, float intensity);
    
    public abstract void OnPlayerExit(GameObject player);
    
    public abstract string GetEffectDescription();
    
    public virtual string GetEffectTypeName()
    {
        return GetType().Name.Replace("Effect", "");
    }
    
    protected float GetEffectiveIntensity(float zoneIntensity)
    {
        return useZoneIntensity ? zoneIntensity : 1f;
    }
}
