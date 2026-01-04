using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EnhancedTemporalZone))]
public class EnhancedTemporalZoneEditor : Editor
{
    private SerializedProperty colliderTypeProp;
    private SerializedProperty editorEffectRadiusProp;
    private SerializedProperty zoneNameProp;
    private SerializedProperty stabilityDrainRateProp;
    private SerializedProperty zoneColorProp;
    private SerializedProperty selectedColorProp;
    private SerializedProperty showRadiusProp;
    private SerializedProperty showCenterPointProp;
    private SerializedProperty centerPointSizeProp;
    private SerializedProperty infoTextSizeProp;
    private SerializedProperty strutCountProp;
    private SerializedProperty showTextLabelProp;
    private SerializedProperty showGizmosProp;
    private SerializedProperty gizmoColorProp;

    private void OnEnable()
    {
        colliderTypeProp = serializedObject.FindProperty("_colliderType");
        editorEffectRadiusProp = serializedObject.FindProperty("_editorEffectRadius");
        zoneNameProp = serializedObject.FindProperty("zoneName");
        stabilityDrainRateProp = serializedObject.FindProperty("stabilityDrainRate");
        zoneColorProp = serializedObject.FindProperty("zoneColor");
        selectedColorProp = serializedObject.FindProperty("selectedColor");
        showRadiusProp = serializedObject.FindProperty("showRadius");
        showCenterPointProp = serializedObject.FindProperty("showCenterPoint");
        centerPointSizeProp = serializedObject.FindProperty("centerPointSize");
        infoTextSizeProp = serializedObject.FindProperty("infoTextSize");
        strutCountProp = serializedObject.FindProperty("strutCount");
        showTextLabelProp = serializedObject.FindProperty("showTextLabel");
        showGizmosProp = serializedObject.FindProperty("showGizmos");
        gizmoColorProp = serializedObject.FindProperty("gizmoColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Zone Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(zoneNameProp, new GUIContent("Zone Name"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Zone Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(colliderTypeProp, new GUIContent("Collider Type"));

        EditorGUILayout.PropertyField(editorEffectRadiusProp, new GUIContent("Effect Radius"));
        if (editorEffectRadiusProp.floatValue < 1f)
            editorEffectRadiusProp.floatValue = 1f;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Stability Drain", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stabilityDrainRateProp, new GUIContent("Drain Rate (per second)"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Visual Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(zoneColorProp);
        EditorGUILayout.PropertyField(selectedColorProp);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Draw XXL Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(showRadiusProp);
        EditorGUILayout.PropertyField(showCenterPointProp);
        EditorGUILayout.PropertyField(centerPointSizeProp);
        EditorGUILayout.PropertyField(infoTextSizeProp, new GUIContent("Info Text Size"));
        EditorGUILayout.PropertyField(strutCountProp);
        EditorGUILayout.PropertyField(showTextLabelProp);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(showGizmosProp);
        EditorGUILayout.PropertyField(gizmoColorProp);

        serializedObject.ApplyModifiedProperties();
    }
}
