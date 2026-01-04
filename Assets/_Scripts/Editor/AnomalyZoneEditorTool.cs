using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class AnomalyZoneCreatorWindow : EditorWindow
{
    private const string BASE_PREFAB_KEY = "AnomalyZone_BasePrefab";
    private const string VARIANTS_FOLDER_KEY = "AnomalyZone_VariantsFolder";
    private const string PRESETS_FOLDER_KEY = "AnomalyZone_PresetsFolder";
    private const string DEFAULT_VARIANTS_PATH = "Assets/_Prefabs/Anomalies/Variants";
    private const string DEFAULT_PRESETS_PATH = "Assets/_Prefabs/Anomalies/Presets";
    
    private GameObject basePrefab;
    private string variantsFolder;
    private string presetsFolder;
    
    private string zoneName = "Anomaly Zone";
    private ZoneColliderType colliderType = ZoneColliderType.Sphere;
    private float effectRadius = 10f;
    private float stabilityDrainRate = -2.0f;
    private Color zoneColor = new Color(0f, 1f, 1f, 0.3f);
    private Color selectedColor = new Color(1f, 1f, 0f, 0.5f);
    private bool showRadius = true;
    private bool showCenterPoint = true;
    private float centerPointSize = 0.5f;
    private float infoTextSize = 1.0f;
    private int strutCount = 2;
    private bool showTextLabel = true;
    
    private Vector2 scrollPosition;
    private List<AnomalyZonePreset> availablePresets = new List<AnomalyZonePreset>();
    private AnomalyZonePreset selectedPreset;
    
    private bool showAdvancedSettings = false;
    
    [MenuItem("Tools/Anomaly Zone/Create New Zone")]
    public static void ShowWindow()
    {
        AnomalyZoneCreatorWindow window = GetWindow<AnomalyZoneCreatorWindow>("Anomaly Zone Creator");
        window.minSize = new Vector2(450, 600);
        window.Show();
    }
    
    private void OnEnable()
    {
        LoadSettings();
        RefreshPresets();
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
            string[] presetGuids = AssetDatabase.FindAssets("t:AnomalyZonePreset", new[] { presetsFolder });
            foreach (string guid in presetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnomalyZonePreset preset = AssetDatabase.LoadAssetAtPath<AnomalyZonePreset>(path);
                if (preset != null)
                {
                    availablePresets.Add(preset);
                }
            }
        }
    }
    
    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Anomaly Zone Creator", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        DrawSetupSection();
        EditorGUILayout.Space(10);
        DrawPresetsSection();
        EditorGUILayout.Space(10);
        DrawZoneConfiguration();
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
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Variants Folder:", GUILayout.Width(100));
        EditorGUILayout.LabelField(variantsFolder, EditorStyles.miniLabel);
        if (GUILayout.Button("Change", GUILayout.Width(60)))
        {
            string newPath = EditorUtility.OpenFolderPanel("Select Variants Folder", "Assets", "");
            if (!string.IsNullOrEmpty(newPath))
            {
                if (newPath.StartsWith(Application.dataPath))
                {
                    newPath = "Assets" + newPath.Substring(Application.dataPath.Length);
                }
                variantsFolder = newPath;
                EditorPrefs.SetString(VARIANTS_FOLDER_KEY, newPath);
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Presets Folder:", GUILayout.Width(100));
        EditorGUILayout.LabelField(presetsFolder, EditorStyles.miniLabel);
        if (GUILayout.Button("Change", GUILayout.Width(60)))
        {
            string newPath = EditorUtility.OpenFolderPanel("Select Presets Folder", "Assets", "");
            if (!string.IsNullOrEmpty(newPath))
            {
                if (newPath.StartsWith(Application.dataPath))
                {
                    newPath = "Assets" + newPath.Substring(Application.dataPath.Length);
                }
                presetsFolder = newPath;
                EditorPrefs.SetString(PRESETS_FOLDER_KEY, newPath);
                RefreshPresets();
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        if (basePrefab == null)
        {
            EditorGUILayout.HelpBox("Please assign a base prefab with:\n• NetworkObject (IsSpawnable = true)\n• EnhancedTemporalZone component\n• SphereCollider, BoxCollider, CapsuleCollider (all as triggers)", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("✓ Base prefab configured. FishNet will automatically register variants.", MessageType.Info);
        }
    }
    
    private void DrawPresetsSection()
    {
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
        
        if (availablePresets.Count == 0)
        {
            EditorGUILayout.HelpBox("No presets found. Create your first preset by configuring settings below and clicking 'Save as Preset'.", MessageType.Info);
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
        
        EditorGUILayout.Space(3);
        stabilityDrainRate = EditorGUILayout.FloatField("Drain Rate (per second)", stabilityDrainRate);
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Visual Settings", EditorStyles.miniLabel);
        zoneColor = EditorGUILayout.ColorField("Zone Color", zoneColor);
        selectedColor = EditorGUILayout.ColorField("Selected Color", selectedColor);
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
            
            EditorGUI.indentLevel--;
        }
    }
    
    private void DrawActions()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.enabled = basePrefab != null && !Application.isPlaying;
        if (GUILayout.Button("Create Variant & Spawn", GUILayout.Height(40)))
        {
            CreateVariantAndSpawn();
        }
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Save as Preset"))
        {
            SaveAsPreset();
        }
        
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
        
            Undo.RegisterCreatedObjectUndo(instance, "Create Anomaly Zone");
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
        
            SceneView.lastActiveSceneView?.FrameSelected();
        
            Debug.Log($"✅ Created and spawned anomaly zone variant: {variantName} ({colliderType} shape)");
            Debug.Log($"⚙️ FishNet will automatically register this prefab in DefaultPrefabObjects.");
        }
    }
    
    private void ApplySettingsToVariant(string prefabPath)
    {
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            EnhancedTemporalZone zone = prefabContents.GetComponent<EnhancedTemporalZone>();
        
            if (zone == null)
            {
                Debug.LogError("Base prefab does not have EnhancedTemporalZone component!");
                return;
            }

            // Ensure visualizer component exists in the prefab
            EnhancedTemporalZoneVisualizer visualizer = prefabContents.GetComponent<EnhancedTemporalZoneVisualizer>();
            if (visualizer == null)
            {
                visualizer = prefabContents.AddComponent<EnhancedTemporalZoneVisualizer>();
                visualizer.hideFlags = HideFlags.HideInInspector;
            }
            visualizer.zone = zone;
        
            SerializedObject so = new SerializedObject(zone);
        
            so.FindProperty("zoneName").stringValue = zoneName;
            so.FindProperty("_editorEffectRadius").floatValue = effectRadius;
            so.FindProperty("stabilityDrainRate").floatValue = stabilityDrainRate;
            so.FindProperty("zoneColor").colorValue = zoneColor;
            so.FindProperty("selectedColor").colorValue = selectedColor;
            so.FindProperty("showRadius").boolValue = showRadius;
            so.FindProperty("showCenterPoint").boolValue = showCenterPoint;
            so.FindProperty("centerPointSize").floatValue = centerPointSize;
            so.FindProperty("infoTextSize").floatValue = infoTextSize;
            so.FindProperty("strutCount").intValue = strutCount;
            so.FindProperty("showTextLabel").boolValue = showTextLabel;
        
            so.ApplyModifiedProperties();
        
            ConfigureColliders(prefabContents);
        
            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
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

        EnhancedTemporalZone zone = prefabContents.GetComponent<EnhancedTemporalZone>();
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
        
        AnomalyZonePreset preset = CreateInstance<AnomalyZonePreset>();
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
    
    private void LoadPreset(AnomalyZonePreset preset)
    {
        selectedPreset = preset;
        
        zoneName = preset.zoneName;
        colliderType = preset.colliderType;
        effectRadius = preset.effectRadius;
        stabilityDrainRate = preset.stabilityDrainRate;
        zoneColor = preset.zoneColor;
        selectedColor = preset.selectedColor;
        showRadius = preset.showRadius;
        showCenterPoint = preset.showCenterPoint;
        centerPointSize = preset.centerPointSize;
        infoTextSize = preset.infoTextSize;
        strutCount = preset.strutCount;
        showTextLabel = preset.showTextLabel;
        
        Repaint();
    }
    
    private void ApplyCurrentSettingsToPreset(AnomalyZonePreset preset)
    {
        preset.zoneName = zoneName;
        preset.colliderType = colliderType;
        preset.effectRadius = effectRadius;
        preset.stabilityDrainRate = stabilityDrainRate;
        preset.zoneColor = zoneColor;
        preset.selectedColor = selectedColor;
        preset.showRadius = showRadius;
        preset.showCenterPoint = showCenterPoint;
        preset.centerPointSize = centerPointSize;
        preset.infoTextSize = infoTextSize;
        preset.strutCount = strutCount;
        preset.showTextLabel = showTextLabel;
    }
    
    private void ResetToDefaults()
    {
        zoneName = "Anomaly Zone";
        colliderType = ZoneColliderType.Sphere;
        effectRadius = 10f;
        stabilityDrainRate = -2.0f;
        zoneColor = new Color(0f, 1f, 1f, 0.3f);
        selectedColor = new Color(1f, 1f, 0f, 0.5f);
        showRadius = true;
        showCenterPoint = true;
        centerPointSize = 0.5f;
        infoTextSize = 1.0f;
        strutCount = 2;
        showTextLabel = true;
        selectedPreset = null;
        
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
