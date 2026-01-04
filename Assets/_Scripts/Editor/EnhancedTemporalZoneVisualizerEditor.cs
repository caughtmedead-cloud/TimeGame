using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnhancedTemporalZoneVisualizer))]
public class EnhancedTemporalZoneVisualizerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("This component handles Draw XXL visualization for the zone. All settings are configured on the EnhancedTemporalZone component.", MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("Select EnhancedTemporalZone"))
        {
            EnhancedTemporalZoneVisualizer visualizer = (EnhancedTemporalZoneVisualizer)target;
            if (visualizer.zone != null)
            {
                Selection.activeObject = visualizer.zone;
            }
        }
    }
}
