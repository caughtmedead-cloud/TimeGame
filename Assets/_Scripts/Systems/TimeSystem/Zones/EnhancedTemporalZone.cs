using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using DrawXXL;
#endif

[System.Obsolete("Use GenericZone with StabilityDrainEffect instead. This class remains for backward compatibility.")]
public class EnhancedTemporalZone : GenericZone
{
    [Header("Legacy Stability Drain (Deprecated)")]
    [Tooltip("Stability drain per second (negative value) - Use StabilityDrainEffect component instead")]
    [SerializeField] private float stabilityDrainRate = -2.0f;
    
    private readonly List<TemporalStability> affectedPlayers = new List<TemporalStability>();
    private StabilityDrainEffect drainEffect;

#if UNITY_EDITOR
    private EnhancedTemporalZoneVisualizer visualizer;
#endif
    
    public float StabilityDrainRate
    {
        get
        {
            if (drainEffect != null)
                return drainEffect.StabilityDrainRate;
            return stabilityDrainRate;
        }
        set
        {
            stabilityDrainRate = value;
            if (drainEffect != null)
                drainEffect.StabilityDrainRate = value;
        }
    }
    
    protected override void Awake()
    {
        base.Awake();
        MigrateToNewSystem();
    }
    
    protected override void Start()
    {
        base.Start();
        
#if UNITY_EDITOR
        visualizer = GetComponent<EnhancedTemporalZoneVisualizer>();
        if (visualizer != null)
        {
            visualizer.zone = this;
        }
#endif
    }
    
    private void MigrateToNewSystem()
    {
        drainEffect = GetComponent<StabilityDrainEffect>();
        
        if (drainEffect == null)
        {
            drainEffect = gameObject.AddComponent<StabilityDrainEffect>();
            drainEffect.StabilityDrainRate = stabilityDrainRate;
            drainEffect.useZoneIntensity = true;
        }
    }
    
    protected override void Update()
    {
        base.Update();
        
        if (!IsServerStarted) return;
        
        for (int i = affectedPlayers.Count - 1; i >= 0; i--)
        {
            TemporalStability stability = affectedPlayers[i];
            
            if (stability == null)
            {
                affectedPlayers.RemoveAt(i);
                continue;
            }
            
            if (stability.gameObject.scene != gameObject.scene)
            {
                affectedPlayers.RemoveAt(i);
                continue;
            }
        }
    }
    
    public void PlayerEntered(TemporalStability stability)
    {
        if (!IsServerStarted) return;
        
        if (stability != null && !affectedPlayers.Contains(stability))
        {
            affectedPlayers.Add(stability);
        }
    }
    
    public void PlayerExited(TemporalStability stability)
    {
        if (!IsServerStarted) return;
        
        if (stability != null && affectedPlayers.Remove(stability))
        {
        }
    }
    
    public override string GetZoneTypeDescription()
    {
        return $"Drain: {StabilityDrainRate:F1}/s";
    }
    
    protected override void OnValidate()
    {
        base.OnValidate();
        
#if UNITY_EDITOR
        if (visualizer != null)
        {
            visualizer.zone = this;
        }
        
        drainEffect = GetComponent<StabilityDrainEffect>();
        if (drainEffect != null && drainEffect.StabilityDrainRate != stabilityDrainRate)
        {
            drainEffect.StabilityDrainRate = stabilityDrainRate;
        }
#endif
    }
}
