using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using FishNet.Object;

public class ZoneCreatorWindow : EditorWindow
{
    private const string BASE_PREFAB_KEY = "Zone_BasePrefab";
    private const string VARIANTS_FOLDER_KEY = "Zone_VariantsFolder";
    private const string PRESETS_FOLDER_KEY = "Zone_PresetsFolder";
    private const string DEFAULT_VARIANTS_PATH = "Assets/_Prefabs/Zones/Variants";
    private const string DEFAULT_PRESETS_PATH = "Assets/_Prefabs/Zones/Presets";
    
    private GameObject basePrefab;
    private string variantsFolder;
    private string presetsFolder;
    
    [System.Serializable]
    private class EffectConfig
    {
        public string effectType = "StatModifier";
        public string targetStatIdentifier = "Stability";
        public float modificationRate = -2.0f;
        public bool foldedOut = true;
    }
    
    private string zoneName = "Zone";
    private ZoneColliderType colliderType = ZoneColliderType.Sphere;
    private float effectRadius = 10f;
    private Color zoneColor = new Color(0f, 1f, 1f, 0.3f);
    private Color selectedColor = new Color(1f, 1f, 0f, 0.5f);
    
    private List<EffectConfig> effects = new List<EffectConfig>();
    
    private bool showRadius = true;
    private bool showCenterPoint = true;
    private float centerPointSize = 0.5f;
    private float infoTextSize = 1.0f;
    private int strutCount = 2;
    private bool showTextLabel = true;
    private float textAnchorHeight = 1.2f;
    
    private bool useIntensityGradient = false;
    private AnimationCurve intensityCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);
    private bool showGradientRings = true;
    private int gradientRingCount = 4;
    
    private Vector2 scrollPosition;
    private List<ZonePreset> availablePresets = new List<ZonePreset>();
    private ZonePreset selectedPreset;
    
    private bool showAdvancedSettings = false;
    private bool showGradientSettings = false;
    
    private List<string> availableStats;
    private string[] effectTypeOptions = new string[] { "Stat Modifier", "Debug Log" };
    
    [MenuItem("Tools/Zones/Zone Creator")]
    public static void ShowWindow()
    {
        ZoneCreatorWindow window = GetWindow<ZoneCreatorWindow>("Zone Creator");
        window.minSize = new Vector2(500, 700);
        window.Show();
    }
    
    private void OnEnable()
    {
        LoadSettings();
        RefreshPresets();
        RefreshAvailableStats();
        
        if (effects.Count == 0)
        {
            effects.Add(new EffectConfig());
        }
    }
    
    private void LoadSettings()
    {
        string basePrefabPath = EditorPrefs.GetString(BASE_PREFAB_KEY, "");
        if (!string.IsNullOrEmpty(basePrefabPath))
        {
            basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);
        }
        
        variantsFolder = EditorPrefs.GetString(VARIANTS_FOLDER_KEY, DEFAULT_VARIANTS_PATH);
        presetsFolder = EditorPrefs.GetString(PRESETS_FOLDER_KEY, DEFAULT_PRESETS_PATH);
    }
    
    private void RefreshPresets()
    {
        availablePresets.Clear();
        
        if (AssetDatabase.IsValidFolder(presetsFolder))
        {
            string[] presetGuids = AssetDatabase.FindAssets("t:ZonePreset", new[] { presetsFolder });
            foreach (string guid in presetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ZonePreset preset = AssetDatabase.LoadAssetAtPath<ZonePreset>(path);
                if (preset != null)
                {
                    availablePresets.Add(preset);
                }
            }
        }
    }
    
    private void RefreshAvailableStats()
    {
        availableStats = StatRegistry.GetAvailableStatIdentifiers();
    }
    
    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Zone Creator", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        DrawSetupSection();
        EditorGUILayout.Space(10);
        DrawPresetsSection();
        EditorGUILayout.Space(10);
        DrawZoneConfiguration();
        EditorGUILayout.Space(10);
        DrawEffectsSection();
        EditorGUILayout.Space(10);
        DrawGradientSettings();
        EditorGUILayout.Space(10);
        DrawAdvancedSettings();
        EditorGUILayout.Space(10);
        DrawActions();
        
        EditorGUILayout.EndScrollView();
    }
    
    private void DrawSetupSection()
    {
        EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        basePrefab = (GameObject)EditorGUILayout.ObjectField("Base Prefab", basePrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck() && basePrefab != null)
        {
            string path = AssetDatabase.GetAssetPath(basePrefab);
            EditorPrefs.SetString(BASE_PREFAB_KEY, path);
        }
        
        EditorGUILayout.Space(5);
        
        DrawFolderField("Variants Folder:", ref variantsFolder, VARIANTS_FOLDER_KEY);
        DrawFolderField("Presets Folder:", ref presetsFolder, PRESETS_FOLDER_KEY);
        
        EditorGUILayout.Space(5);
        
        if (basePrefab == null)
        {
            EditorGUILayout.HelpBox("Please assign a base prefab with:\n• NetworkObject (IsSpawnable = true)\n• GenericZone component\n• SphereCollider, BoxCollider, CapsuleCollider (all as triggers)", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("✓ Base prefab configured. FishNet will automatically register variants.", MessageType.Info);
        }
    }
    
    private void DrawFolderField(string label, ref string folder, string prefKey)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(100));
        EditorGUILayout.LabelField(folder, EditorStyles.miniLabel);
        if (GUILayout.Button("Change", GUILayout.Width(60)))
        {
            string newPath = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
            if (!string.IsNullOrEmpty(newPath))
            {
                if (newPath.StartsWith(Application.dataPath))
                {
                    newPath = "Assets" + newPath.Substring(Application.dataPath.Length);
                }
                folder = newPath;
                EditorPrefs.SetString(prefKey, newPath);
                
                if (prefKey == PRESETS_FOLDER_KEY)
                    RefreshPresets();
            }
        }
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawPresetsSection()
    {
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
        
        if (availablePresets.Count == 0)
        {
            EditorGUILayout.HelpBox("No presets found. Configure settings below and click 'Save as Preset'.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            
            int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 40) / 120));
            int currentColumn = 0;
            
            foreach (var preset in availablePresets)
            {
                bool isSelected = selectedPreset == preset;
                
                GUI.backgroundColor = isSelected ? Color.green : Color.white;
                
                if (GUILayout.Button(preset.presetName, GUILayout.Width(110), GUILayout.Height(30)))
                {
                    LoadPreset(preset);
                }
                
                GUI.backgroundColor = Color.white;
                
                currentColumn++;
                if (currentColumn >= columns)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    currentColumn = 0;
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Presets"))
        {
            RefreshPresets();
        }
        if (GUILayout.Button("Open Presets Folder"))
        {
            EnsureFolderExists(presetsFolder);
            Object folderObj = AssetDatabase.LoadAssetAtPath<Object>(presetsFolder);
            Selection.activeObject = folderObj;
            EditorGUIUtility.PingObject(folderObj);
        }
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawZoneConfiguration()
    {
        EditorGUILayout.LabelField("Zone Configuration", EditorStyles.boldLabel);
        
        zoneName = EditorGUILayout.TextField("Zone Name", zoneName);
        
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("Collider Shape", EditorStyles.miniLabel);
        colliderType = (ZoneColliderType)EditorGUILayout.EnumPopup("Shape Type", colliderType);
        
        string radiusLabel = colliderType == ZoneColliderType.Sphere ? "Radius" : "Size";
        effectRadius = EditorGUILayout.Slider(radiusLabel, effectRadius, 1f, 100f);
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Visual Settings", EditorStyles.miniLabel);
        zoneColor = EditorGUILayout.ColorField("Zone Color", zoneColor);
        selectedColor = EditorGUILayout.ColorField("Selected Color", selectedColor);
    }
    
    private void DrawEffectsSection()
    {
        EditorGUILayout.LabelField("Effects", EditorStyles.boldLabel);
        
        if (effects.Count == 0)
        {
            EditorGUILayout.HelpBox("No effects configured. Add at least one effect.", MessageType.Warning);
        }
        
        for (int i = 0; i < effects.Count; i++)
        {
            DrawEffectConfig(effects[i], i);
        }
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Effect", GUILayout.Height(30)))
        {
            effects.Add(new EffectConfig());
        }
        
        if (GUILayout.Button("Refresh Stats", GUILayout.Height(30)))
        {
            RefreshAvailableStats();
        }
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawEffectConfig(EffectConfig effect, int index)
    {
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.BeginHorizontal();
        effect.foldedOut = EditorGUILayout.Foldout(effect.foldedOut, $"Effect {index + 1}: {effect.effectType}", true);
        
        if (GUILayout.Button("Remove", GUILayout.Width(60)))
        {
            effects.RemoveAt(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();
        
        if (effect.foldedOut)
        {
            EditorGUI.indentLevel++;
            
            int selectedType = System.Array.IndexOf(effectTypeOptions, effect.effectType);
            if (selectedType < 0) selectedType = 0;
            
            selectedType = EditorGUILayout.Popup("Effect Type", selectedType, effectTypeOptions);
            effect.effectType = effectTypeOptions[selectedType];
            
            if (effect.effectType == "Stat Modifier")
            {
                DrawStatModifierFields(effect);
            }
            else if (effect.effectType == "Debug Log")
            {
                EditorGUILayout.HelpBox("Debug effect will log player enter/exit events.", MessageType.Info);
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawStatModifierFields(EffectConfig effect)
    {
        if (availableStats == null || availableStats.Count == 0)
        {
            EditorGUILayout.HelpBox("No stats found. Make sure you have PlayerStat components in your project.", MessageType.Warning);
            effect.targetStatIdentifier = EditorGUILayout.TextField("Target Stat", effect.targetStatIdentifier);
        }
        else
        {
            int selectedIndex = availableStats.IndexOf(effect.targetStatIdentifier);
            if (selectedIndex < 0) selectedIndex = 0;
            
            selectedIndex = EditorGUILayout.Popup("Target Stat", selectedIndex, availableStats.ToArray());
            effect.targetStatIdentifier = availableStats[selectedIndex];
        }
        
        effect.modificationRate = EditorGUILayout.FloatField("Rate (per second)", effect.modificationRate);
        
        string rateDescription = effect.modificationRate >= 0 
            ? $"Increases {effect.targetStatIdentifier} by {effect.modificationRate:F1}/s" 
            : $"Drains {effect.targetStatIdentifier} by {Mathf.Abs(effect.modificationRate):F1}/s";
        
        EditorGUILayout.HelpBox(rateDescription, MessageType.Info);
    }
    
    private void DrawGradientSettings()
    {
        showGradientSettings = EditorGUILayout.Foldout(showGradientSettings, "Intensity Gradient", true);
        
        if (showGradientSettings)
        {
            EditorGUI.indentLevel++;
            
            useIntensityGradient = EditorGUILayout.Toggle("Use Gradient", useIntensityGradient);
            
            if (useIntensityGradient)
            {
                intensityCurve = EditorGUILayout.CurveField("Intensity Curve", intensityCurve);
                EditorGUILayout.HelpBox("X = distance from center (0=center, 1=edge)\nY = effect multiplier (0-1)", MessageType.Info);
                
                showGradientRings = EditorGUILayout.Toggle("Show Rings", showGradientRings);
                gradientRingCount = EditorGUILayout.IntSlider("Ring Count", gradientRingCount, 2, 10);
            }
            
            EditorGUI.indentLevel--;
        }
    }
    
    private void DrawAdvancedSettings()
    {
        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);
        
        if (showAdvancedSettings)
        {
            EditorGUI.indentLevel++;
            
            showRadius = EditorGUILayout.Toggle("Show Radius", showRadius);
            showCenterPoint = EditorGUILayout.Toggle("Show Center Point", showCenterPoint);
            centerPointSize = EditorGUILayout.Slider("Center Point Size", centerPointSize, 0.1f, 2f);
            infoTextSize = EditorGUILayout.Slider("Info Text Size", infoTextSize, 0.1f, 3f);
            strutCount = EditorGUILayout.IntSlider("Strut Count", strutCount, 0, 8);
            showTextLabel = EditorGUILayout.Toggle("Show Text Label", showTextLabel);
            textAnchorHeight = EditorGUILayout.Slider("Text Anchor Height", textAnchorHeight, -1f, 2f);
            
            EditorGUI.indentLevel--;
        }
    }
    
    private void DrawActions()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.enabled = basePrefab != null && !Application.isPlaying && effects.Count > 0;
        if (GUILayout.Button("Create Variant & Spawn", GUILayout.Height(40)))
        {
            CreateVariantAndSpawn();
        }
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.enabled = effects.Count > 0;
        if (GUILayout.Button("Save as Preset"))
        {
            SaveAsPreset();
        }
        GUI.enabled = true;
        
        GUI.enabled = selectedPreset != null;
        if (GUILayout.Button("Update Preset"))
        {
            UpdatePreset();
        }
        if (GUILayout.Button("Delete Preset"))
        {
            DeletePreset();
        }
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("Reset to Defaults"))
        {
            ResetToDefaults();
        }
        
        if (basePrefab == null)
        {
            EditorGUILayout.HelpBox("Cannot create zones without a base prefab assigned.", MessageType.Warning);
        }
        else if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Cannot create prefab variants during Play Mode.", MessageType.Warning);
        }
        else if (effects.Count == 0)
        {
            EditorGUILayout.HelpBox("Add at least one effect before creating a zone.", MessageType.Warning);
        }
    }
    
    private void CreateVariantAndSpawn()
    {
        if (basePrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "No base prefab assigned.", "OK");
            return;
        }

        if (!EditorSceneManager.GetActiveScene().IsValid())
        {
            EditorUtility.DisplayDialog("Error", "No valid scene is open.", "OK");
            return;
        }

        EnsureFolderExists(variantsFolder);

        string variantName = GenerateUniqueVariantName(zoneName);
        string variantPath = $"{variantsFolder}/{variantName}.prefab";

        GameObject variant = PrefabUtility.SaveAsPrefabAsset(basePrefab, variantPath);

        if (variant == null)
        {
            EditorUtility.DisplayDialog("Error", "Failed to create prefab variant.", "OK");
            return;
        }

        ApplySettingsToVariant(variantPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        GameObject finalVariant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
        GameObject instance = PrefabUtility.InstantiatePrefab(finalVariant) as GameObject;

        if (instance != null)
        {
            instance.name = zoneName;
            instance.transform.position = GetSpawnPosition();
        
            Undo.RegisterCreatedObjectUndo(instance, "Create Zone");
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
        
            SceneView.lastActiveSceneView?.FrameSelected();
        
            Debug.Log($"✅ Created zone variant: {variantName} with {effects.Count} effect(s)");
        }
    }
    
    private void ApplySettingsToVariant(string prefabPath)
    {
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            GenericZone zone = prefabContents.GetComponent<GenericZone>();
        
            if (zone == null)
            {
                zone = prefabContents.AddComponent<GenericZone>();
            }

            ZoneVisualizer visualizer = prefabContents.GetComponent<ZoneVisualizer>();
            if (visualizer == null)
            {
                visualizer = prefabContents.AddComponent<ZoneVisualizer>();
                visualizer.hideFlags = HideFlags.HideInInspector;
            }
            visualizer.zone = zone;
        
            SerializedObject so = new SerializedObject(zone);
        
            so.FindProperty("zoneName").stringValue = zoneName;
            so.FindProperty("_editorEffectRadius").floatValue = effectRadius;
            so.FindProperty("zoneColor").colorValue = zoneColor;
            so.FindProperty("selectedColor").colorValue = selectedColor;
            so.FindProperty("showRadius").boolValue = showRadius;
            so.FindProperty("showCenterPoint").boolValue = showCenterPoint;
            so.FindProperty("centerPointSize").floatValue = centerPointSize;
            so.FindProperty("infoTextSize").floatValue = infoTextSize;
            so.FindProperty("strutCount").intValue = strutCount;
            so.FindProperty("showTextLabel").boolValue = showTextLabel;
            so.FindProperty("textAnchorHeight").floatValue = textAnchorHeight;
            so.FindProperty("useIntensityGradient").boolValue = useIntensityGradient;
            so.FindProperty("intensityCurve").animationCurveValue = intensityCurve;
            so.FindProperty("showGradientRings").boolValue = showGradientRings;
            so.FindProperty("gradientRingCount").intValue = gradientRingCount;
        
            so.ApplyModifiedProperties();
        
            RemoveAllEffects(prefabContents);
            AddEffectsToPrefab(prefabContents);
        
            ConfigureColliders(prefabContents);
        
            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }
    
    private void RemoveAllEffects(GameObject obj)
    {
        var existingEffects = obj.GetComponents<ZoneEffect>();
        foreach (var effect in existingEffects)
        {
            Object.DestroyImmediate(effect);
        }
    }
    
    private void AddEffectsToPrefab(GameObject prefabContents)
    {
        foreach (var effectConfig in effects)
        {
            if (effectConfig.effectType == "Stat Modifier")
            {
                StatModifierEffect effect = prefabContents.AddComponent<StatModifierEffect>();
                effect.targetStatIdentifier = effectConfig.targetStatIdentifier;
                effect.modificationRate = effectConfig.modificationRate;
                effect.useZoneIntensity = useIntensityGradient;
            }
            else if (effectConfig.effectType == "Debug Log")
            {
                DebugLogEffect effect = prefabContents.AddComponent<DebugLogEffect>();
            }
        }
    }
    
    private void ConfigureColliders(GameObject prefabContents)
    {
        RemoveAllColliders(prefabContents);

        Collider newCollider = null;

        switch (colliderType)
        {
            case ZoneColliderType.Sphere:
                SphereCollider sphere = prefabContents.AddComponent<SphereCollider>();
                sphere.radius = effectRadius;
                sphere.isTrigger = true;
                newCollider = sphere;
                break;
        
            case ZoneColliderType.Box:
                BoxCollider box = prefabContents.AddComponent<BoxCollider>();
                box.size = new Vector3(effectRadius * 2f, effectRadius * 2f, effectRadius * 2f);
                box.isTrigger = true;
                newCollider = box;
                break;
        
            case ZoneColliderType.Capsule:
                CapsuleCollider capsule = prefabContents.AddComponent<CapsuleCollider>();
                capsule.radius = effectRadius;
                capsule.height = effectRadius * 4f;
                capsule.isTrigger = true;
                newCollider = capsule;
                break;
        }

        GenericZone zone = prefabContents.GetComponent<GenericZone>();
        if (zone != null)
        {
            SerializedObject so = new SerializedObject(zone);
            so.FindProperty("_colliderType").enumValueIndex = (int)colliderType;
            so.ApplyModifiedProperties();
        }
    }

    private void RemoveAllColliders(GameObject obj)
    {
        SphereCollider[] spheres = obj.GetComponents<SphereCollider>();
        BoxCollider[] boxes = obj.GetComponents<BoxCollider>();
        CapsuleCollider[] capsules = obj.GetComponents<CapsuleCollider>();
        
        foreach (var col in spheres)
            Object.DestroyImmediate(col);
        
        foreach (var col in boxes)
            Object.DestroyImmediate(col);
        
        foreach (var col in capsules)
            Object.DestroyImmediate(col);
    }
    
    private string GenerateUniqueVariantName(string baseName)
    {
        string sanitized = baseName.Replace(" ", "_");
        string testPath = $"{variantsFolder}/{sanitized}.prefab";
        
        if (!File.Exists(testPath))
        {
            return sanitized;
        }
        
        int counter = 1;
        while (File.Exists($"{variantsFolder}/{sanitized}_{counter:D2}.prefab"))
        {
            counter++;
        }
        
        return $"{sanitized}_{counter:D2}";
    }
    
    private Vector3 GetSpawnPosition()
    {
        if (SceneView.lastActiveSceneView != null)
        {
            Camera sceneCamera = SceneView.lastActiveSceneView.camera;
            Vector3 spawnPos = sceneCamera.transform.position + sceneCamera.transform.forward * 10f;
            
            if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 100f))
            {
                return hit.point + Vector3.up * effectRadius;
            }
            
            return spawnPos;
        }
        
        return Vector3.zero;
    }
    
    private void SaveAsPreset()
    {
        EnsureFolderExists(presetsFolder);
        
        string presetName = zoneName.Replace(" ", "_");
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Preset",
            presetName,
            "asset",
            "Enter preset name",
            presetsFolder
        );
        
        if (string.IsNullOrEmpty(path)) return;
        
        ZonePreset preset = ScriptableObject.CreateInstance<ZonePreset>();
        preset.presetName = Path.GetFileNameWithoutExtension(path);
        ApplyCurrentSettingsToPreset(preset);
        
        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();
        
        RefreshPresets();
        selectedPreset = preset;
        
        Debug.Log($"✅ Saved preset: {preset.presetName}");
    }
    
    private void UpdatePreset()
    {
        if (selectedPreset == null) return;
        
        ApplyCurrentSettingsToPreset(selectedPreset);
        EditorUtility.SetDirty(selectedPreset);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"✅ Updated preset: {selectedPreset.presetName}");
    }
    
    private void DeletePreset()
    {
        if (selectedPreset == null) return;
        
        if (EditorUtility.DisplayDialog(
            "Delete Preset",
            $"Are you sure you want to delete preset '{selectedPreset.presetName}'?",
            "Delete",
            "Cancel"))
        {
            string path = AssetDatabase.GetAssetPath(selectedPreset);
            AssetDatabase.DeleteAsset(path);
            selectedPreset = null;
            RefreshPresets();
            
            Debug.Log("✅ Preset deleted");
        }
    }
    
    private void LoadPreset(ZonePreset preset)
    {
        selectedPreset = preset;
        
        zoneName = preset.zoneName;
        colliderType = preset.colliderType;
        effectRadius = preset.effectRadius;
        zoneColor = preset.zoneColor;
        selectedColor = preset.selectedColor;
        showRadius = preset.showRadius;
        showCenterPoint = preset.showCenterPoint;
        centerPointSize = preset.centerPointSize;
        infoTextSize = preset.infoTextSize;
        strutCount = preset.strutCount;
        showTextLabel = preset.showTextLabel;
        textAnchorHeight = preset.textAnchorHeight;
        useIntensityGradient = preset.useIntensityGradient;
        intensityCurve = preset.intensityCurve;
        showGradientRings = preset.showGradientRings;
        gradientRingCount = preset.gradientRingCount;
        
        effects.Clear();
        foreach (var effectData in preset.effects)
        {
            effects.Add(new EffectConfig
            {
                effectType = effectData.effectType,
                targetStatIdentifier = effectData.targetStatIdentifier,
                modificationRate = effectData.modificationRate,
                foldedOut = false
            });
        }
        
        if (effects.Count == 0)
        {
            effects.Add(new EffectConfig());
        }
        
        Repaint();
    }
    
    private void ApplyCurrentSettingsToPreset(ZonePreset preset)
    {
        preset.zoneName = zoneName;
        preset.colliderType = colliderType;
        preset.effectRadius = effectRadius;
        preset.zoneColor = zoneColor;
        preset.selectedColor = selectedColor;
        preset.showRadius = showRadius;
        preset.showCenterPoint = showCenterPoint;
        preset.centerPointSize = centerPointSize;
        preset.infoTextSize = infoTextSize;
        preset.strutCount = strutCount;
        preset.showTextLabel = showTextLabel;
        preset.textAnchorHeight = textAnchorHeight;
        preset.useIntensityGradient = useIntensityGradient;
        preset.intensityCurve = intensityCurve;
        preset.showGradientRings = showGradientRings;
        preset.gradientRingCount = gradientRingCount;
        
        preset.effects.Clear();
        foreach (var effectConfig in effects)
        {
            preset.effects.Add(new ZonePreset.EffectData
            {
                effectType = effectConfig.effectType,
                targetStatIdentifier = effectConfig.targetStatIdentifier,
                modificationRate = effectConfig.modificationRate
            });
        }
    }
    
    private void ResetToDefaults()
    {
        zoneName = "Zone";
        colliderType = ZoneColliderType.Sphere;
        effectRadius = 10f;
        zoneColor = new Color(0f, 1f, 1f, 0.3f);
        selectedColor = new Color(1f, 1f, 0f, 0.5f);
        showRadius = true;
        showCenterPoint = true;
        centerPointSize = 0.5f;
        infoTextSize = 1.0f;
        strutCount = 2;
        showTextLabel = true;
        textAnchorHeight = 1.2f;
        useIntensityGradient = false;
        intensityCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);
        showGradientRings = true;
        gradientRingCount = 4;
        selectedPreset = null;
        
        effects.Clear();
        effects.Add(new EffectConfig());
        
        Repaint();
    }
    
    private void EnsureFolderExists(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        
        string[] folders = path.Split('/');
        string currentPath = folders[0];
        
        for (int i = 1; i < folders.Length; i++)
        {
            string nextPath = $"{currentPath}/{folders[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, folders[i]);
            }
            currentPath = nextPath;
        }
        
        AssetDatabase.Refresh();
    }
}
