using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EnhancedTemporalZone))]
public class EnhancedTemporalZoneEditor : Editor
{
    private SerializedProperty colliderTypeProp;
    private SerializedProperty editorEffectRadiusProp;
    private SerializedProperty zoneNameProp;
    private SerializedProperty stabilityDrainRateProp;
    
    private SerializedProperty useIntensityGradientProp;
    private SerializedProperty intensityCurveProp;
    private SerializedProperty showGradientRingsProp;
    private SerializedProperty gradientRingCountProp;
    
    private SerializedProperty zoneColorProp;
    private SerializedProperty selectedColorProp;
    private SerializedProperty showRadiusProp;
    private SerializedProperty showCenterPointProp;
    private SerializedProperty centerPointSizeProp;
    private SerializedProperty infoTextSizeProp;
    private SerializedProperty strutCountProp;
    private SerializedProperty showTextLabelProp;
    private SerializedProperty textAnchorHeightProp;
    private SerializedProperty showGizmosProp;
    private SerializedProperty gizmoColorProp;

    private void OnEnable()
    {
        colliderTypeProp = serializedObject.FindProperty("_colliderType");
        editorEffectRadiusProp = serializedObject.FindProperty("_editorEffectRadius");
        zoneNameProp = serializedObject.FindProperty("zoneName");
        stabilityDrainRateProp = serializedObject.FindProperty("stabilityDrainRate");
        
        useIntensityGradientProp = serializedObject.FindProperty("useIntensityGradient");
        intensityCurveProp = serializedObject.FindProperty("intensityCurve");
        showGradientRingsProp = serializedObject.FindProperty("showGradientRings");
        gradientRingCountProp = serializedObject.FindProperty("gradientRingCount");
        
        zoneColorProp = serializedObject.FindProperty("zoneColor");
        selectedColorProp = serializedObject.FindProperty("selectedColor");
        showRadiusProp = serializedObject.FindProperty("showRadius");
        showCenterPointProp = serializedObject.FindProperty("showCenterPoint");
        centerPointSizeProp = serializedObject.FindProperty("centerPointSize");
        infoTextSizeProp = serializedObject.FindProperty("infoTextSize");
        strutCountProp = serializedObject.FindProperty("strutCount");
        showTextLabelProp = serializedObject.FindProperty("showTextLabel");
        textAnchorHeightProp = serializedObject.FindProperty("textAnchorHeight");
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
        EditorGUILayout.LabelField("Intensity Gradient", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useIntensityGradientProp, new GUIContent("Use Intensity Gradient"));
        
        if (useIntensityGradientProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(intensityCurveProp, new GUIContent("Intensity Curve"));
            EditorGUILayout.HelpBox("X = Distance from center (0=center, 1=edge)\nY = Drain multiplier (0=none, 1=full)", MessageType.Info);
            EditorGUILayout.PropertyField(showGradientRingsProp, new GUIContent("Show Gradient Rings"));
            if (showGradientRingsProp.boolValue)
            {
                EditorGUILayout.PropertyField(gradientRingCountProp, new GUIContent("Ring Count"));
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Visual Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(zoneColorProp, new GUIContent("Zone Color"));
        EditorGUILayout.PropertyField(selectedColorProp, new GUIContent("Selected Color"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Draw XXL Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(showRadiusProp, new GUIContent("Show Radius"));
        EditorGUILayout.PropertyField(showCenterPointProp, new GUIContent("Show Center Point"));
        EditorGUILayout.PropertyField(centerPointSizeProp, new GUIContent("Center Point Size"));
        EditorGUILayout.PropertyField(infoTextSizeProp, new GUIContent("Info Text Size"));
        EditorGUILayout.PropertyField(strutCountProp, new GUIContent("Strut Count"));
        EditorGUILayout.PropertyField(showTextLabelProp, new GUIContent("Show Text Label"));
        EditorGUILayout.PropertyField(textAnchorHeightProp, new GUIContent("Text Anchor Height"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(showGizmosProp, new GUIContent("Show Gizmos"));
        EditorGUILayout.PropertyField(gizmoColorProp, new GUIContent("Gizmo Color"));

        serializedObject.ApplyModifiedProperties();
    }
}
