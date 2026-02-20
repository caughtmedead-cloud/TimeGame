using UnityEngine;
using System.Text;

public class GenericZone : BaseZone
{
    public override string GetZoneTypeDescription()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ZoneEffect[] effects = GetComponents<ZoneEffect>();
            if (effects == null || effects.Length == 0)
                return "No Effects";
            
            StringBuilder sb = new StringBuilder();
            int enabledCount = 0;
            
            foreach (ZoneEffect effect in effects)
            {
                if (effect != null && effect.isEnabled)
                {
                    if (enabledCount > 0)
                        sb.Append(", ");
                    
                    sb.Append(effect.GetEffectTypeName());
                    enabledCount++;
                }
            }
            
            return enabledCount > 0 ? sb.ToString() : "No Active Effects";
        }
#endif
        
        if (zoneEffects == null || zoneEffects.Count == 0)
            return "No Effects";
        
        StringBuilder sbRuntime = new StringBuilder();
        int enabledCountRuntime = 0;
        
        foreach (ZoneEffect effect in zoneEffects)
        {
            if (effect != null && effect.isEnabled)
            {
                if (enabledCountRuntime > 0)
                    sbRuntime.Append(", ");
                
                sbRuntime.Append(effect.GetEffectTypeName());
                enabledCountRuntime++;
            }
        }
        
        return enabledCountRuntime > 0 ? sbRuntime.ToString() : "No Active Effects";
    }
    
    protected override void Start()
    {
        base.Start();
        
#if UNITY_EDITOR
        ZoneVisualizer visualizer = GetComponent<ZoneVisualizer>();
        if (visualizer != null)
        {
            visualizer.zone = this;
        }
#endif
    }
    
    protected override void OnValidate()
    {
        base.OnValidate();
        
#if UNITY_EDITOR
        ZoneVisualizer visualizer = GetComponent<ZoneVisualizer>();
        if (visualizer != null)
        {
            visualizer.zone = this;
        }
#endif
    }
}
