using UnityEngine;
using System.Collections.Generic;

public class StatModifierEffect : ZoneEffect
{
    [Header("Stat Modification")]
    [Tooltip("Name of the stat to modify (e.g., 'Stability', 'Radiation', 'Health')")]
    public string targetStatIdentifier = "Stability";
    
    [Tooltip("Modification rate per second (negative = drain, positive = increase)")]
    public float modificationRate = -2.0f;
    
    [Header("Behavior")]
    [Tooltip("Only affect players (ignore other objects)")]
    public bool playersOnly = true;
    
    [Header("Debug")]
    [SerializeField] private bool logModifications = false;
    
    private Dictionary<GameObject, IPlayerStat> cachedStats = new Dictionary<GameObject, IPlayerStat>();
    
    public override void OnPlayerEnter(GameObject player)
    {
        Debug.Log($"[StatModifierEffect] OnPlayerEnter called - Player: {player.name}, Stat: {targetStatIdentifier}");
        
        if (!TryGetPlayerStat(player, out IPlayerStat stat))
        {
            Debug.LogWarning($"[StatModifierEffect] Player {player.name} does not have stat '{targetStatIdentifier}'");
            return;
        }
        
        cachedStats[player] = stat;
        
        Debug.Log($"[StatModifierEffect] Player {player.name} entered zone - will modify '{targetStatIdentifier}' by {modificationRate:F1}/s (Current value: {stat.CurrentValue})");
    }
    
    public override void OnPlayerStay(GameObject player, float deltaTime, float zoneIntensity)
    {
        if (!cachedStats.TryGetValue(player, out IPlayerStat stat))
        {
            if (!TryGetPlayerStat(player, out stat))
                return;
            
            cachedStats[player] = stat;
        }
        
        float effectiveIntensity = GetEffectiveIntensity(zoneIntensity);
        float modificationAmount = modificationRate * effectiveIntensity * deltaTime;
        
        if (logModifications)
        {
            Debug.Log($"[StatModifierEffect] Modifying {player.name}'s {targetStatIdentifier} by {modificationAmount:F2} (intensity: {effectiveIntensity:F2})");
        }
        
        stat.ModifyStat(modificationAmount);
    }
    
    public override void OnPlayerExit(GameObject player)
    {
        cachedStats.Remove(player);
        
        Debug.Log($"[StatModifierEffect] Player {player.name} exited zone - stopped modifying '{targetStatIdentifier}'");
    }
    
    private bool TryGetPlayerStat(GameObject player, out IPlayerStat stat)
    {
        stat = null;
        
        IPlayerStat[] stats = player.GetComponents<IPlayerStat>();
        
        foreach (IPlayerStat playerStat in stats)
        {
            if (playerStat.StatIdentifier == targetStatIdentifier)
            {
                stat = playerStat;
                return true;
            }
        }
        
        return false;
    }
    
    public override string GetEffectDescription()
    {
        string direction = modificationRate >= 0 ? "+" : "";
        return $"{targetStatIdentifier}: {direction}{modificationRate:F1}/s";
    }
    
    public override string GetEffectTypeName()
    {
        string verb = modificationRate < 0 ? "Drain" : (modificationRate > 0 ? "Increase" : "Modify");
        float absRate = Mathf.Abs(modificationRate);
        return $"{verb} {targetStatIdentifier}: {absRate:F1}/s";
    }
    
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(targetStatIdentifier))
        {
            targetStatIdentifier = "Stability";
        }
    }
}
