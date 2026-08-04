using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ZonePreset", menuName = "Zones/Zone Preset")]
public class ZonePreset : ScriptableObject
{
    [System.Serializable]
    public class EffectData
    {
        public string effectType = "StatModifier";
        public string targetStatIdentifier = "Stability";
        public float modificationRate = -2.0f;
    }
    
    [Header("Preset Info")]
    public string presetName = "New Preset";
    
    [Header("Zone Identity")]
    public string zoneName = "Zone";
    public string requiredTag = "Player";
    
    [Header("Zone Shape")]
    public ZoneColliderType colliderType = ZoneColliderType.Sphere;
    public float effectRadius = 10f;
    
    [Header("Visual Settings")]
    public Color zoneColor = new Color(0f, 1f, 1f, 0.3f);
    public Color selectedColor = new Color(1f, 1f, 0f, 0.5f);
    public bool showRadius = true;
    public bool showCenterPoint = true;
    public float centerPointSize = 0.5f;
    public float infoTextSize = 1.0f;
    public int strutCount = 2;
    public bool showTextLabel = true;
    public float textAnchorHeight = 1.2f;
    
    [Header("Intensity Gradient")]
    public bool useIntensityGradient = false;
    public AnimationCurve intensityCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);
    public bool showGradientRings = true;
    [Range(2, 10)]
    public int gradientRingCount = 4;
    
    [Header("Effects")]
    public List<EffectData> effects = new List<EffectData>();
}
