using UnityEngine;

public class DebugLogEffect : ZoneEffect
{
    [Header("Debug Settings")]
    [Tooltip("Log when player enters")]
    [SerializeField] private bool logOnEnter = true;
    
    [Tooltip("Log when player stays (every frame)")]
    [SerializeField] private bool logOnStay = false;
    
    [Tooltip("Log when player exits")]
    [SerializeField] private bool logOnExit = true;
    
    [Tooltip("Custom message prefix")]
    [SerializeField] private string messagePrefix = "Zone";
    
    public override void OnPlayerEnter(GameObject player)
    {
        if (logOnEnter)
        {
            Debug.Log($"[{messagePrefix}] Player {player.name} entered {zone.zoneName}");
        }
    }
    
    public override void OnPlayerStay(GameObject player, float deltaTime, float zoneIntensity)
    {
        if (logOnStay)
        {
            float effectiveIntensity = GetEffectiveIntensity(zoneIntensity);
            Debug.Log($"[{messagePrefix}] Player {player.name} in {zone.zoneName} - Intensity: {effectiveIntensity:F2}");
        }
    }
    
    public override void OnPlayerExit(GameObject player)
    {
        if (logOnExit)
        {
            Debug.Log($"[{messagePrefix}] Player {player.name} exited {zone.zoneName}");
        }
    }
    
    public override string GetEffectDescription()
    {
        return "Debug Logging";
    }
}
