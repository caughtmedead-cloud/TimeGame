using UnityEngine;

public enum ZoneColliderType
{
    Sphere,
    Box,
    Capsule
}

[CreateAssetMenu(fileName = "AnomalyZonePreset", menuName = "Anomaly Zone/Preset")]
public class AnomalyZonePreset : ScriptableObject
{
    public string presetName = "New Preset";
    public string zoneName = "Anomaly Zone";
    public ZoneColliderType colliderType = ZoneColliderType.Sphere;
    public float effectRadius = 10f;
    public float stabilityDrainRate = -2.0f;
    public Color zoneColor = new Color(0f, 1f, 1f, 0.3f);
    public Color selectedColor = new Color(1f, 1f, 0f, 0.5f);
    public bool showRadius = true;
    public bool showCenterPoint = true;
    public float centerPointSize = 0.5f;
    public float infoTextSize = 1.0f;
    public int strutCount = 2;
    public bool showTextLabel = true;
}
