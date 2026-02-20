using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ZoneVisualizer))]
public class ZoneVisualizerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("This component handles Draw XXL visualization for the zone. All settings are configured on the BaseZone component.", MessageType.Info);
        
        ZoneVisualizer visualizer = (ZoneVisualizer)target;
        
        if (visualizer.zone == null)
        {
            EditorGUILayout.HelpBox("Zone reference is missing! Make sure there is a BaseZone-derived component on this GameObject.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("Zone Type", visualizer.zone.GetType().Name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Zone Name", visualizer.zone.zoneName);
            EditorGUILayout.LabelField("Effect Radius", $"{visualizer.zone.effectRadius:F1}m");
        }
    }
}
