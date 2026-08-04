using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Thelos.Editor
{
    public class AdvancedCircularTerrainGenerator : EditorWindow
    {
        private enum RingType { InnerRing, MiddleRing, OuterRing, Custom }
        private enum SizeMode { Preset, MasterSize }
        
        private SizeMode sizeMode = SizeMode.Preset;
        private RingType ringType = RingType.InnerRing;
        private int segments = 128;
        private int radialDivisions = 32;
        
        [Header("Master Size Control")]
        private float totalRadius = 600f;
        private float innerRingPercent = 33.33f;
        private float middleRingPercent = 33.33f;
        private float outerRingPercent = 33.34f;
        
        [Header("Ring Dimensions")]
        private float customInnerRadius = 0f;
        private float customOuterRadius = 200f;
        
        [Header("Height Variation")]
        private bool useLayeredNoise = true;
        private float baseHeight = 5f;
        private float largeFeatureScale = 0.02f;
        private float largeFeatureStrength = 15f;
        private float mediumFeatureScale = 0.05f;
        private float mediumFeatureStrength = 8f;
        private float smallDetailScale = 0.15f;
        private float smallDetailStrength = 2f;
        
        [Header("Elevation Settings")]
        private bool addRadialElevation = true;
        private AnimationCurve elevationCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.5f);
        private float maxElevationDifference = 20f;
        
        [Header("Features")]
        private bool addHills = true;
        private int hillCount = 5;
        private float hillRadius = 30f;
        private float hillHeight = 10f;
        
        private bool addValleys = true;
        private int valleyCount = 3;
        private float valleyDepth = 8f;
        
        [Header("Multi-Material Zones")]
        private bool useSubmeshes = false;
        private int zoneCount = 1;
        private enum ZonePattern { Radial, Angular, Custom }
        private ZonePattern zonePattern = ZonePattern.Radial;
        
        [Header("SECTR Streaming")]
        private bool generateSeparateGameObjects = false;
        private bool addSectrMembers = false;
        
        private enum VertexColorMode { White, HeightGradient }
        private VertexColorMode vertexColorMode = VertexColorMode.White;
        
        private bool saveMeshAsset = true;
        private bool generateCollider = true;
        private bool optimizeMesh = true;
        
        private Vector2 scrollPosition;
        
        [System.Serializable]
        private class TerrainPreset
        {
            public string name;
            public SizeMode sizeMode;
            public RingType ringType;
            public int segments;
            public int radialDivisions;
            public float totalRadius;
            public float innerRingPercent;
            public float middleRingPercent;
            public float customInnerRadius;
            public float customOuterRadius;
            public bool useLayeredNoise;
            public float baseHeight;
            public float largeFeatureScale;
            public float largeFeatureStrength;
            public float mediumFeatureScale;
            public float mediumFeatureStrength;
            public float smallDetailScale;
            public float smallDetailStrength;
            public bool addRadialElevation;
            public float maxElevationDifference;
            public bool addHills;
            public int hillCount;
            public float hillRadius;
            public float hillHeight;
            public bool addValleys;
            public int valleyCount;
            public float valleyDepth;
            public bool useSubmeshes;
            public int zoneCount;
            public ZonePattern zonePattern;
            public VertexColorMode vertexColorMode;
            public bool saveMeshAsset;
            public bool generateCollider;
            public bool optimizeMesh;
        }
        
        [MenuItem("Thelos/Advanced Circular Terrain Generator")]
        static void ShowWindow()
        {
            AdvancedCircularTerrainGenerator window = GetWindow<AdvancedCircularTerrainGenerator>("Terrain Generator");
            window.minSize = new Vector2(400, 600);
        }
        
        void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("Advanced Circular Terrain Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox("Creates optimized circular mesh terrain with ProPixelizer support and SRP Batcher compatibility.", MessageType.Info);
            EditorGUILayout.Space();
            
            DrawRingSettings();
            EditorGUILayout.Space();
            
            DrawMeshQualitySettings();
            EditorGUILayout.Space();
            
            DrawNoiseSettings();
            EditorGUILayout.Space();
            
            DrawElevationSettings();
            EditorGUILayout.Space();
            
            DrawFeatureSettings();
            EditorGUILayout.Space();
            
            DrawMaterialZoneSettings();
            EditorGUILayout.Space();
            
            DrawOutputSettings();
            EditorGUILayout.Space();
            
            DrawVertexColorSettings();
            EditorGUILayout.Space();
            
            DrawPresetSettings();
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Generate Terrain Ring", GUILayout.Height(50)))
            {
                GenerateAdvancedTerrain();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(GetRingInfo(), MessageType.None);
            
            EditorGUILayout.EndScrollView();
        }
        
        void DrawRingSettings()
        {
            GUILayout.Label("Ring Configuration", EditorStyles.boldLabel);
            
            sizeMode = (SizeMode)EditorGUILayout.EnumPopup("Size Mode", sizeMode);
            EditorGUILayout.Space(5);
            
            if (sizeMode == SizeMode.MasterSize)
            {
                EditorGUILayout.HelpBox("Master Size: Define total city radius and how it's divided into rings", MessageType.Info);
                
                totalRadius = EditorGUILayout.Slider("Total City Radius (m)", totalRadius, 200f, 2000f);
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Ring Distribution (%)", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                
                innerRingPercent = EditorGUILayout.Slider("Inner Ring", innerRingPercent, 10f, 60f);
                middleRingPercent = EditorGUILayout.Slider("Middle Ring", middleRingPercent, 10f, 60f);
                
                float totalPercent = innerRingPercent + middleRingPercent;
                outerRingPercent = 100f - totalPercent;
                
                EditorGUILayout.LabelField($"Outer Ring: {outerRingPercent:F2}% (auto)", EditorStyles.miniLabel);
                
                if (outerRingPercent < 10f)
                {
                    EditorGUILayout.HelpBox("Outer ring is too small! Reduce other percentages.", MessageType.Warning);
                }
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
                
                float innerRadius = 0f;
                float innerBoundary = totalRadius * (innerRingPercent / 100f);
                float middleBoundary = innerBoundary + (totalRadius * (middleRingPercent / 100f));
                float outerBoundary = totalRadius;
                
                EditorGUILayout.LabelField("Calculated Ring Sizes:", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Inner Ring: 0m → {innerBoundary:F1}m (width: {innerBoundary:F1}m)");
                EditorGUILayout.LabelField($"Middle Ring: {innerBoundary:F1}m → {middleBoundary:F1}m (width: {middleBoundary - innerBoundary:F1}m)");
                EditorGUILayout.LabelField($"Outer Ring: {middleBoundary:F1}m → {outerBoundary:F1}m (width: {outerBoundary - middleBoundary:F1}m)");
                EditorGUI.indentLevel--;
                
                EditorGUILayout.Space(5);
                ringType = (RingType)EditorGUILayout.EnumPopup("Generate Which Ring", ringType);
                
                if (ringType == RingType.Custom)
                {
                    EditorGUILayout.HelpBox("In Master Size mode, use preset rings. Switch to Preset mode for full custom control.", MessageType.Info);
                    ringType = RingType.InnerRing;
                }
            }
            else // Preset mode
            {
                EditorGUILayout.HelpBox("Preset Mode: Use predefined ring sizes or create custom dimensions", MessageType.Info);
                
                ringType = (RingType)EditorGUILayout.EnumPopup("Ring Type", ringType);
                
                if (ringType == RingType.Custom)
                {
                    EditorGUILayout.Space(5);
                    customInnerRadius = EditorGUILayout.FloatField("Inner Radius (m)", customInnerRadius);
                    customOuterRadius = EditorGUILayout.FloatField("Outer Radius (m)", customOuterRadius);
                    
                    if (customInnerRadius < 0f) customInnerRadius = 0f;
                    if (customOuterRadius <= customInnerRadius) customOuterRadius = customInnerRadius + 50f;
                    
                    float ringWidth = customOuterRadius - customInnerRadius;
                    EditorGUILayout.HelpBox($"Ring width: {ringWidth:F1}m", MessageType.None);
                }
            }
        }
        
        void DrawMeshQualitySettings()
        {
            GUILayout.Label("Mesh Quality", EditorStyles.boldLabel);
            
            segments = EditorGUILayout.IntSlider("Circular Segments", segments, 64, 1024);
            radialDivisions = EditorGUILayout.IntSlider("Radial Divisions", radialDivisions, 16, 256);
            
            int totalVertices = (segments + 1) * (radialDivisions + 1);
            int totalTriangles = segments * radialDivisions * 2;
            
            EditorGUILayout.HelpBox($"Vertices: {totalVertices:N0} | Triangles: {totalTriangles:N0}", MessageType.None);
        }
        
        void DrawNoiseSettings()
        {
            GUILayout.Label("Layered Noise (Realistic Terrain)", EditorStyles.boldLabel);
            useLayeredNoise = EditorGUILayout.Toggle("Use Layered Noise", useLayeredNoise);
            
            if (useLayeredNoise)
            {
                baseHeight = EditorGUILayout.Slider("Base Height", baseHeight, 0f, 20f);
                
                EditorGUILayout.LabelField("Large Features (Hills/Mountains)", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                largeFeatureScale = EditorGUILayout.Slider("Scale", largeFeatureScale, 0.005f, 0.05f);
                largeFeatureStrength = EditorGUILayout.Slider("Strength", largeFeatureStrength, 0f, 50f);
                EditorGUI.indentLevel--;
                
                EditorGUILayout.LabelField("Medium Features (Slopes)", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                mediumFeatureScale = EditorGUILayout.Slider("Scale", mediumFeatureScale, 0.02f, 0.1f);
                mediumFeatureStrength = EditorGUILayout.Slider("Strength", mediumFeatureStrength, 0f, 20f);
                EditorGUI.indentLevel--;
                
                EditorGUILayout.LabelField("Small Details (Surface)", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                smallDetailScale = EditorGUILayout.Slider("Scale", smallDetailScale, 0.05f, 0.3f);
                smallDetailStrength = EditorGUILayout.Slider("Strength", smallDetailStrength, 0f, 5f);
                EditorGUI.indentLevel--;
            }
        }
        
        void DrawElevationSettings()
        {
            GUILayout.Label("Radial Elevation", EditorStyles.boldLabel);
            addRadialElevation = EditorGUILayout.Toggle("Add Radial Elevation", addRadialElevation);
            
            if (addRadialElevation)
            {
                maxElevationDifference = EditorGUILayout.Slider("Max Elevation Diff", maxElevationDifference, 0f, 50f);
                elevationCurve = EditorGUILayout.CurveField("Elevation Curve", elevationCurve);
            }
        }
        
        void DrawFeatureSettings()
        {
            GUILayout.Label("Procedural Features", EditorStyles.boldLabel);
            
            addHills = EditorGUILayout.Toggle("Add Random Hills", addHills);
            if (addHills)
            {
                EditorGUI.indentLevel++;
                hillCount = EditorGUILayout.IntSlider("Hill Count", hillCount, 1, 15);
                hillRadius = EditorGUILayout.Slider("Hill Radius", hillRadius, 10f, 80f);
                hillHeight = EditorGUILayout.Slider("Hill Height", hillHeight, 5f, 30f);
                EditorGUI.indentLevel--;
            }
            
            addValleys = EditorGUILayout.Toggle("Add Valleys/Depressions", addValleys);
            if (addValleys)
            {
                EditorGUI.indentLevel++;
                valleyCount = EditorGUILayout.IntSlider("Valley Count", valleyCount, 1, 10);
                valleyDepth = EditorGUILayout.Slider("Valley Depth", valleyDepth, 2f, 15f);
                EditorGUI.indentLevel--;
            }
        }
        
        void DrawMaterialZoneSettings()
        {
            GUILayout.Label("Material Zones (Multi-Material Support)", EditorStyles.boldLabel);
            
            useSubmeshes = EditorGUILayout.Toggle("Use Submeshes", useSubmeshes);
            
            if (useSubmeshes)
            {
                EditorGUILayout.HelpBox("Creates multiple submeshes for different material zones (grass, forest, factory, etc.)", MessageType.Info);
                
                EditorGUI.indentLevel++;
                zoneCount = EditorGUILayout.IntSlider("Zone Count", zoneCount, 2, 8);
                zonePattern = (ZonePattern)EditorGUILayout.EnumPopup("Zone Pattern", zonePattern);
                
                if (zonePattern == ZonePattern.Radial)
                {
                    EditorGUILayout.HelpBox("Concentric rings: Inner → Outer zones", MessageType.None);
                }
                else if (zonePattern == ZonePattern.Angular)
                {
                    EditorGUILayout.HelpBox("Pie slices: Zones divide the circle angularly", MessageType.None);
                }
                else
                {
                    EditorGUILayout.HelpBox("Custom: Uses vertex color R channel to determine zone", MessageType.None);
                }
                
                EditorGUILayout.Space(10);
                
                EditorGUILayout.LabelField("SECTR Streaming Compatibility", EditorStyles.miniBoldLabel);
                generateSeparateGameObjects = EditorGUILayout.Toggle("Separate GameObjects", generateSeparateGameObjects);
                
                if (generateSeparateGameObjects)
                {
                    EditorGUILayout.HelpBox("Creates separate child GameObjects for each zone. Required for SECTR dynamic loading/unloading.", MessageType.Info);
                    addSectrMembers = EditorGUILayout.Toggle("Add SECTR_Member", addSectrMembers);
                    
                    if (addSectrMembers)
                    {
                        EditorGUILayout.HelpBox("Automatically adds SECTR_Member component to each zone GameObject for streaming support.", MessageType.None);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Uses submeshes on a single GameObject. Faster rendering but cannot be streamed with SECTR.", MessageType.None);
                }
                
                EditorGUI.indentLevel--;
            }
        }
        
        void DrawOutputSettings()
        {
            GUILayout.Label("Output Options", EditorStyles.boldLabel);
            
            saveMeshAsset = EditorGUILayout.Toggle("Save Mesh Asset", saveMeshAsset);
            generateCollider = EditorGUILayout.Toggle("Generate Mesh Collider", generateCollider);
            optimizeMesh = EditorGUILayout.Toggle("Optimize Mesh", optimizeMesh);
        }
        
        void DrawVertexColorSettings()
        {
            GUILayout.Label("Vertex Colors", EditorStyles.boldLabel);
            
            vertexColorMode = (VertexColorMode)EditorGUILayout.EnumPopup("Color Mode", vertexColorMode);
            
            EditorGUI.indentLevel++;
            switch (vertexColorMode)
            {
                case VertexColorMode.White:
                    EditorGUILayout.HelpBox(
                        "✅ WHITE - Ready for painting!\n" +
                        "All vertices set to white (1,1,1,1).\n" +
                        "Use Vertex Color Painter to paint texture blends.",
                        MessageType.Info
                    );
                    break;
                    
                case VertexColorMode.HeightGradient:
                    EditorGUILayout.HelpBox(
                        "HEIGHT GRADIENT - Grayscale by height\n" +
                        "Vertices colored based on elevation.\n" +
                        "Useful for height-based effects.",
                        MessageType.None
                    );
                    break;
            }
            EditorGUI.indentLevel--;
        }
        
        void DrawPresetSettings()
        {
            GUILayout.Label("Presets", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("💾 Save Preset", GUILayout.Height(30f)))
            {
                SavePresetDialog();
            }
            
            if (GUILayout.Button("📂 Load Preset", GUILayout.Height(30f)))
            {
                LoadPresetDialog();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("🗑️ Delete Preset", GUILayout.Height(25f)))
            {
                DeletePresetDialog();
            }
            
            if (GUILayout.Button("📋 List Presets", GUILayout.Height(25f)))
            {
                ListPresetsDialog();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox(
                "Save your favorite settings as presets!\n" +
                "Quick access to common ring configurations.",
                MessageType.None
            );
        }
        
        string GetRingInfo()
        {
            if (sizeMode == SizeMode.MasterSize)
            {
                float innerBoundary = totalRadius * (innerRingPercent / 100f);
                float middleBoundary = innerBoundary + (totalRadius * (middleRingPercent / 100f));
                float outerBoundary = totalRadius;
                
                switch (ringType)
                {
                    case RingType.InnerRing:
                        return $"Inner Ring: 0-{innerBoundary:F0}m radius\nLab District - Higher elevation, unstable ground\nHigh-tech facilities, reactor core, medical wing\n\nTotal city size: {totalRadius:F0}m | Ring占比: {innerRingPercent:F1}%";
                    case RingType.MiddleRing:
                        return $"Middle Ring: {innerBoundary:F0}-{middleBoundary:F0}m radius\nResidential/Commercial - Gentle slopes, urban terrain\nApartments, shopping districts, transit hubs\n\nTotal city size: {totalRadius:F0}m | Ring占比: {middleRingPercent:F1}%";
                    case RingType.OuterRing:
                        return $"Outer Ring: {middleBoundary:F0}-{outerBoundary:F0}m radius\nIndustrial - Varied elevation, wasteland features\nFactories, warehouses, abandoned infrastructure\n\nTotal city size: {totalRadius:F0}m | Ring占比: {outerRingPercent:F1}%";
                    default:
                        return "";
                }
            }
            else
            {
                switch (ringType)
                {
                    case RingType.InnerRing:
                        return "Inner Ring: 0-200m radius\nLab District - Higher elevation, unstable ground\nHigh-tech facilities, reactor core, medical wing";
                    case RingType.MiddleRing:
                        return "Middle Ring: 200-400m radius\nResidential/Commercial - Gentle slopes, urban terrain\nApartments, shopping districts, transit hubs";
                    case RingType.OuterRing:
                        return "Outer Ring: 400-600m radius\nIndustrial - Varied elevation, wasteland features\nFactories, warehouses, abandoned infrastructure";
                    case RingType.Custom:
                        return $"Custom Ring: {customInnerRadius:F0}-{customOuterRadius:F0}m radius\nCustom dimensions for specific requirements";
                    default:
                        return "";
                }
            }
        }
        
        void GenerateAdvancedTerrain()
        {
            float innerRadius, outerRadius;
            string ringName;
            
            if (sizeMode == SizeMode.MasterSize)
            {
                float innerBoundary = totalRadius * (innerRingPercent / 100f);
                float middleBoundary = innerBoundary + (totalRadius * (middleRingPercent / 100f));
                float outerBoundary = totalRadius;
                
                switch (ringType)
                {
                    case RingType.InnerRing:
                        innerRadius = 0f;
                        outerRadius = innerBoundary;
                        ringName = $"TerrainMesh_InnerRing_{totalRadius:F0}m";
                        break;
                    case RingType.MiddleRing:
                        innerRadius = innerBoundary;
                        outerRadius = middleBoundary;
                        ringName = $"TerrainMesh_MiddleRing_{totalRadius:F0}m";
                        break;
                    case RingType.OuterRing:
                        innerRadius = middleBoundary;
                        outerRadius = outerBoundary;
                        ringName = $"TerrainMesh_OuterRing_{totalRadius:F0}m";
                        break;
                    default:
                        return;
                }
            }
            else
            {
                switch (ringType)
                {
                    case RingType.InnerRing:
                        innerRadius = 0f;
                        outerRadius = 200f;
                        ringName = "TerrainMesh_InnerRing";
                        break;
                    case RingType.MiddleRing:
                        innerRadius = 200f;
                        outerRadius = 400f;
                        ringName = "TerrainMesh_MiddleRing";
                        break;
                    case RingType.OuterRing:
                        innerRadius = 400f;
                        outerRadius = 600f;
                        ringName = "TerrainMesh_OuterRing";
                        break;
                    case RingType.Custom:
                        innerRadius = customInnerRadius;
                        outerRadius = customOuterRadius;
                        ringName = "TerrainMesh_Custom";
                        break;
                    default:
                        return;
                }
            }
            
            Vector3[] hillPositions = GenerateRandomPositions(hillCount, innerRadius, outerRadius);
            Vector3[] valleyPositions = GenerateRandomPositions(valleyCount, innerRadius, outerRadius);
            
            if (useSubmeshes && zoneCount > 1 && generateSeparateGameObjects)
            {
                GenerateTerrainWithSeparateGameObjects(ringName, innerRadius, outerRadius, hillPositions, valleyPositions);
            }
            else
            {
                GenerateTerrainAsSingleMesh(ringName, innerRadius, outerRadius, hillPositions, valleyPositions);
            }
        }
        
        void GenerateTerrainAsSingleMesh(string ringName, float innerRadius, float outerRadius, Vector3[] hillPositions, Vector3[] valleyPositions)
        {
            EditorUtility.DisplayProgressBar("Generating Terrain", "Creating mesh geometry...", 0.1f);
            
            GameObject meshObject = new GameObject(ringName);
            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
            
            EditorUtility.DisplayProgressBar("Generating Terrain", "Calculating height variations...", 0.3f);
            
            Mesh mesh = CreateAdvancedRingMesh(innerRadius, outerRadius, segments, radialDivisions, 
                                               hillPositions, valleyPositions, useSubmeshes, zoneCount, zonePattern);
            
            if (optimizeMesh)
            {
                EditorUtility.DisplayProgressBar("Generating Terrain", "Optimizing mesh...", 0.7f);
                mesh.Optimize();
                mesh.OptimizeIndexBuffers();
                mesh.OptimizeReorderVertexBuffer();
            }
            
            meshFilter.sharedMesh = mesh;
            
            if (generateCollider)
            {
                EditorUtility.DisplayProgressBar("Generating Terrain", "Creating collider...", 0.8f);
                MeshCollider meshCollider = meshObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
            }
            
            if (useSubmeshes && zoneCount > 1)
            {
                Material[] materials = new Material[zoneCount];
                Color[] zoneColors = GenerateZoneColors(zoneCount);
                
                for (int i = 0; i < zoneCount; i++)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.name = $"Zone_{i}_Material";
                    mat.color = zoneColors[i];
                    mat.SetFloat("_Smoothness", 0.2f);
                    materials[i] = mat;
                }
                
                meshRenderer.sharedMaterials = materials;
            }
            else
            {
                Material defaultMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                defaultMaterial.color = new Color(0.4f, 0.35f, 0.3f);
                defaultMaterial.SetFloat("_Smoothness", 0.2f);
                meshRenderer.sharedMaterial = defaultMaterial;
            }
            
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
            
            if (saveMeshAsset)
            {
                EditorUtility.DisplayProgressBar("Generating Terrain", "Saving mesh asset...", 0.9f);
                SaveMeshAsset(mesh, ringName);
            }
            
            Selection.activeGameObject = meshObject;
            Undo.RegisterCreatedObjectUndo(meshObject, "Create Advanced Terrain Ring");
            
            EditorUtility.ClearProgressBar();
            
            Debug.Log($"✅ Generated {ringName}:\n" +
                      $"  • {mesh.vertexCount:N0} vertices\n" +
                      $"  • {mesh.triangles.Length / 3:N0} triangles\n" +
                      $"  • Radius: {innerRadius:F0}m - {outerRadius:F0}m\n" +
                      $"  • Area: {Mathf.PI * (outerRadius * outerRadius - innerRadius * innerRadius):F0}m²");
        }
        
        void GenerateTerrainWithSeparateGameObjects(string ringName, float innerRadius, float outerRadius, Vector3[] hillPositions, Vector3[] valleyPositions)
        {
            EditorUtility.DisplayProgressBar("Generating Terrain", "Creating zone geometry...", 0.1f);
            
            GameObject parentObject = new GameObject($"{ringName}_Parent");
            
            ZoneMeshData[] zoneMeshes = CreateSeparateZoneMeshes(innerRadius, outerRadius, segments, radialDivisions, 
                                                                 hillPositions, valleyPositions, zoneCount, zonePattern);
            
            Color[] zoneColors = GenerateZoneColors(zoneCount);
            
            for (int i = 0; i < zoneMeshes.Length; i++)
            {
                float progress = 0.3f + (i / (float)zoneMeshes.Length) * 0.5f;
                EditorUtility.DisplayProgressBar("Generating Terrain", $"Creating zone {i + 1}/{zoneMeshes.Length}...", progress);
                
                GameObject zoneObject = new GameObject($"Zone_{i}_{zonePattern}");
                zoneObject.transform.parent = parentObject.transform;
                
                MeshFilter meshFilter = zoneObject.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = zoneObject.AddComponent<MeshRenderer>();
                
                Mesh zoneMesh = zoneMeshes[i].mesh;
                
                if (optimizeMesh)
                {
                    zoneMesh.Optimize();
                    zoneMesh.OptimizeIndexBuffers();
                    zoneMesh.OptimizeReorderVertexBuffer();
                }
                
                meshFilter.sharedMesh = zoneMesh;
                
                if (generateCollider)
                {
                    MeshCollider meshCollider = zoneObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = zoneMesh;
                }
                
                Material zoneMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                zoneMaterial.name = $"Zone_{i}_Material";
                zoneMaterial.color = zoneColors[i];
                zoneMaterial.SetFloat("_Smoothness", 0.2f);
                meshRenderer.sharedMaterial = zoneMaterial;
                
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                meshRenderer.receiveShadows = true;
                
                if (addSectrMembers)
                {
                    System.Type sectrMemberType = System.Type.GetType("SECTR_Member, Assembly-CSharp");
                    if (sectrMemberType == null)
                    {
                        sectrMemberType = System.Type.GetType("SECTR_Member, Assembly-CSharp-firstpass");
                    }
                    
                    if (sectrMemberType != null)
                    {
                        zoneObject.AddComponent(sectrMemberType);
                    }
                    else
                    {
                        Debug.LogWarning("SECTR_Member component not found. Make sure SECTR is imported.");
                    }
                }
                
                if (saveMeshAsset)
                {
                    SaveMeshAsset(zoneMesh, $"{ringName}_Zone_{i}");
                }
            }
            
            Selection.activeGameObject = parentObject;
            Undo.RegisterCreatedObjectUndo(parentObject, "Create Terrain with Zones");
            
            EditorUtility.ClearProgressBar();
            
            int totalVerts = 0;
            int totalTris = 0;
            foreach (var zoneData in zoneMeshes)
            {
                totalVerts += zoneData.mesh.vertexCount;
                totalTris += zoneData.mesh.triangles.Length / 3;
            }
            
            Debug.Log($"✅ Generated {ringName} with {zoneCount} separate zones:\n" +
                      $"  • {totalVerts:N0} total vertices\n" +
                      $"  • {totalTris:N0} total triangles\n" +
                      $"  • Radius: {innerRadius:F0}m - {outerRadius:F0}m\n" +
                      $"  • {zoneCount} GameObjects (SECTR-compatible)\n" +
                      $"  • SECTR_Member: {(addSectrMembers ? "Added" : "Not added")}");
        }
        
        Vector3[] GenerateRandomPositions(int count, float innerRadius, float outerRadius)
        {
            Vector3[] positions = new Vector3[count];
            float padding = 20f;
            
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(innerRadius + padding, outerRadius - padding);
                positions[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            
            return positions;
        }
        
        Color[] GenerateZoneColors(int count)
        {
            Color[] colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                float hue = i / (float)count;
                colors[i] = Color.HSVToRGB(hue, 0.6f, 0.7f);
            }
            return colors;
        }
        
        Mesh CreateAdvancedRingMesh(float innerRadius, float outerRadius, int circleSegments, 
                                    int radialDivs, Vector3[] hillPositions, Vector3[] valleyPositions,
                                    bool useSubmeshes, int zoneCount, ZonePattern zonePattern)
        {
            Mesh mesh = new Mesh();
            mesh.name = "AdvancedCircularTerrain";
            
            if ((circleSegments + 1) * (radialDivs + 1) > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            
            int vertexCount = (circleSegments + 1) * (radialDivs + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Color[] colors = new Color[vertexCount];
            
            float noiseOffsetX = Random.Range(-1000f, 1000f);
            float noiseOffsetZ = Random.Range(-1000f, 1000f);
            
            int vertIndex = 0;
            float maxHeight = 0f;
            float minHeight = float.MaxValue;
            
            for (int seg = 0; seg <= circleSegments; seg++)
            {
                float angle = (seg / (float)circleSegments) * Mathf.PI * 2f;
                float cosAngle = Mathf.Cos(angle);
                float sinAngle = Mathf.Sin(angle);
                
                for (int rad = 0; rad <= radialDivs; rad++)
                {
                    float radialT = rad / (float)radialDivs;
                    float radius = Mathf.Lerp(innerRadius, outerRadius, radialT);
                    
                    float x = cosAngle * radius;
                    float z = sinAngle * radius;
                    float y = baseHeight;
                    
                    if (useLayeredNoise)
                    {
                        float largeNoise = Mathf.PerlinNoise(
                            (x + noiseOffsetX) * largeFeatureScale, 
                            (z + noiseOffsetZ) * largeFeatureScale
                        );
                        y += largeNoise * largeFeatureStrength;
                        
                        float mediumNoise = Mathf.PerlinNoise(
                            (x + noiseOffsetX) * mediumFeatureScale, 
                            (z + noiseOffsetZ) * mediumFeatureScale
                        );
                        y += mediumNoise * mediumFeatureStrength;
                        
                        float smallNoise = Mathf.PerlinNoise(
                            (x + noiseOffsetX) * smallDetailScale, 
                            (z + noiseOffsetZ) * smallDetailScale
                        );
                        y += smallNoise * smallDetailStrength;
                    }
                    
                    if (addRadialElevation)
                    {
                        float elevationMultiplier = elevationCurve.Evaluate(radialT);
                        y += elevationMultiplier * maxElevationDifference;
                    }
                    
                    if (addHills)
                    {
                        foreach (Vector3 hillPos in hillPositions)
                        {
                            float distToHill = Vector2.Distance(new Vector2(x, z), new Vector2(hillPos.x, hillPos.z));
                            if (distToHill < hillRadius)
                            {
                                float hillInfluence = 1f - (distToHill / hillRadius);
                                hillInfluence = Mathf.Pow(hillInfluence, 2f);
                                y += hillInfluence * hillHeight;
                            }
                        }
                    }
                    
                    if (addValleys)
                    {
                        foreach (Vector3 valleyPos in valleyPositions)
                        {
                            float distToValley = Vector2.Distance(new Vector2(x, z), new Vector2(valleyPos.x, valleyPos.z));
                            float valleyRadius = hillRadius * 0.8f;
                            if (distToValley < valleyRadius)
                            {
                                float valleyInfluence = 1f - (distToValley / valleyRadius);
                                valleyInfluence = Mathf.Pow(valleyInfluence, 1.5f);
                                y -= valleyInfluence * valleyDepth;
                            }
                        }
                    }
                    
                    vertices[vertIndex] = new Vector3(x, y, z);
                    
                    // Use world-space XZ positions for UVs (proper tiling)
                    uvs[vertIndex] = new Vector2(x, z);
                    
                    if (y > maxHeight) maxHeight = y;
                    if (y < minHeight) minHeight = y;
                    
                    vertIndex++;
                }
            }
            
            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertexColorMode == VertexColorMode.White)
                {
                    colors[i] = Color.white;
                }
                else
                {
                    float normalizedHeight = Mathf.InverseLerp(minHeight, maxHeight, vertices[i].y);
                    colors[i] = new Color(normalizedHeight, normalizedHeight, normalizedHeight, 1f);
                }
            }
            
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            
            if (useSubmeshes && zoneCount > 1)
            {
                mesh.subMeshCount = zoneCount;
                
                List<int>[] submeshTriangles = new List<int>[zoneCount];
                for (int i = 0; i < zoneCount; i++)
                {
                    submeshTriangles[i] = new List<int>();
                }
                
                for (int seg = 0; seg < circleSegments; seg++)
                {
                    for (int rad = 0; rad < radialDivs; rad++)
                    {
                        int bottomLeft = seg * (radialDivs + 1) + rad;
                        int bottomRight = bottomLeft + 1;
                        int topLeft = (seg + 1) * (radialDivs + 1) + rad;
                        int topRight = topLeft + 1;
                        
                        int zoneIndex = DetermineZone(seg, rad, circleSegments, radialDivs, zoneCount, zonePattern);
                        
                        submeshTriangles[zoneIndex].Add(bottomLeft);
                        submeshTriangles[zoneIndex].Add(topLeft);
                        submeshTriangles[zoneIndex].Add(bottomRight);
                        
                        submeshTriangles[zoneIndex].Add(bottomRight);
                        submeshTriangles[zoneIndex].Add(topLeft);
                        submeshTriangles[zoneIndex].Add(topRight);
                    }
                }
                
                for (int i = 0; i < zoneCount; i++)
                {
                    mesh.SetTriangles(submeshTriangles[i], i);
                }
            }
            else
            {
                int triangleCount = circleSegments * radialDivs * 6;
                int[] triangles = new int[triangleCount];
                
                int triIndex = 0;
                for (int seg = 0; seg < circleSegments; seg++)
                {
                    for (int rad = 0; rad < radialDivs; rad++)
                    {
                        int bottomLeft = seg * (radialDivs + 1) + rad;
                        int bottomRight = bottomLeft + 1;
                        int topLeft = (seg + 1) * (radialDivs + 1) + rad;
                        int topRight = topLeft + 1;
                        
                        triangles[triIndex++] = bottomLeft;
                        triangles[triIndex++] = topLeft;
                        triangles[triIndex++] = bottomRight;
                        
                        triangles[triIndex++] = bottomRight;
                        triangles[triIndex++] = topLeft;
                        triangles[triIndex++] = topRight;
                    }
                }
                
                mesh.triangles = triangles;
            }
            
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            
            return mesh;
        }
        
        int DetermineZone(int seg, int rad, int totalSegments, int totalRadialDivs, int zoneCount, ZonePattern pattern)
        {
            switch (pattern)
            {
                case ZonePattern.Radial:
                    float radialT = rad / (float)totalRadialDivs;
                    return Mathf.Clamp(Mathf.FloorToInt(radialT * zoneCount), 0, zoneCount - 1);
                
                case ZonePattern.Angular:
                    float angularT = seg / (float)totalSegments;
                    return Mathf.Clamp(Mathf.FloorToInt(angularT * zoneCount), 0, zoneCount - 1);
                
                case ZonePattern.Custom:
                    return 0;
                
                default:
                    return 0;
            }
        }
        
        private class ZoneMeshData
        {
            public Mesh mesh;
            public int zoneIndex;
        }
        
        ZoneMeshData[] CreateSeparateZoneMeshes(float innerRadius, float outerRadius, int circleSegments, int radialDivs, 
                                                 Vector3[] hillPositions, Vector3[] valleyPositions, int zoneCount, ZonePattern pattern)
        {
            ZoneMeshData[] zoneMeshes = new ZoneMeshData[zoneCount];
            
            List<Vector3>[] zoneVertices = new List<Vector3>[zoneCount];
            List<Vector2>[] zoneUVs = new List<Vector2>[zoneCount];
            List<Color>[] zoneColors = new List<Color>[zoneCount];
            List<int>[] zoneTriangles = new List<int>[zoneCount];
            Dictionary<Vector3, int>[] vertexIndexMaps = new Dictionary<Vector3, int>[zoneCount];
            
            for (int i = 0; i < zoneCount; i++)
            {
                zoneVertices[i] = new List<Vector3>();
                zoneUVs[i] = new List<Vector2>();
                zoneColors[i] = new List<Color>();
                zoneTriangles[i] = new List<int>();
                vertexIndexMaps[i] = new Dictionary<Vector3, int>();
            }
            
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            Vector3[,] heightMap = new Vector3[circleSegments + 1, radialDivs + 1];
            
            for (int seg = 0; seg <= circleSegments; seg++)
            {
                float angle = (seg % circleSegments) * (360f / circleSegments) * Mathf.Deg2Rad;
                
                for (int rad = 0; rad <= radialDivs; rad++)
                {
                    float radialT = rad / (float)radialDivs;
                    float radius = Mathf.Lerp(innerRadius, outerRadius, radialT);
                    
                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;
                    float y = baseHeight;
                    
                    if (useLayeredNoise)
                    {
                        float largeSample = Mathf.PerlinNoise(x * largeFeatureScale, z * largeFeatureScale);
                        float mediumSample = Mathf.PerlinNoise(x * mediumFeatureScale + 100f, z * mediumFeatureScale + 100f);
                        float smallSample = Mathf.PerlinNoise(x * smallDetailScale + 200f, z * smallDetailScale + 200f);
                        
                        y += (largeSample - 0.5f) * 2f * largeFeatureStrength;
                        y += (mediumSample - 0.5f) * 2f * mediumFeatureStrength;
                        y += (smallSample - 0.5f) * 2f * smallDetailStrength;
                    }
                    
                    if (addRadialElevation)
                    {
                        float elevationMultiplier = elevationCurve.Evaluate(radialT);
                        y += elevationMultiplier * maxElevationDifference;
                    }
                    
                    if (addHills)
                    {
                        foreach (Vector3 hillPos in hillPositions)
                        {
                            float distToHill = Vector2.Distance(new Vector2(x, z), new Vector2(hillPos.x, hillPos.z));
                            if (distToHill < hillRadius)
                            {
                                float hillInfluence = 1f - (distToHill / hillRadius);
                                hillInfluence = Mathf.Pow(hillInfluence, 2f);
                                y += hillInfluence * hillHeight;
                            }
                        }
                    }
                    
                    if (addValleys)
                    {
                        foreach (Vector3 valleyPos in valleyPositions)
                        {
                            float distToValley = Vector2.Distance(new Vector2(x, z), new Vector2(valleyPos.x, valleyPos.z));
                            float valleyRadius = hillRadius * 0.8f;
                            if (distToValley < valleyRadius)
                            {
                                float valleyInfluence = 1f - (distToValley / valleyRadius);
                                valleyInfluence = Mathf.Pow(valleyInfluence, 1.5f);
                                y -= valleyInfluence * valleyDepth;
                            }
                        }
                    }
                    
                    heightMap[seg, rad] = new Vector3(x, y, z);
                    
                    if (y > maxHeight) maxHeight = y;
                    if (y < minHeight) minHeight = y;
                }
            }
            
            for (int seg = 0; seg < circleSegments; seg++)
            {
                for (int rad = 0; rad < radialDivs; rad++)
                {
                    int zoneIndex = DetermineZone(seg, rad, circleSegments, radialDivs, zoneCount, pattern);
                    
                    Vector3 v0 = heightMap[seg, rad];
                    Vector3 v1 = heightMap[seg, rad + 1];
                    Vector3 v2 = heightMap[seg + 1, rad];
                    Vector3 v3 = heightMap[seg + 1, rad + 1];
                    
                    int idx0 = GetOrAddVertex(v0, zoneVertices[zoneIndex], vertexIndexMaps[zoneIndex]);
                    int idx1 = GetOrAddVertex(v1, zoneVertices[zoneIndex], vertexIndexMaps[zoneIndex]);
                    int idx2 = GetOrAddVertex(v2, zoneVertices[zoneIndex], vertexIndexMaps[zoneIndex]);
                    int idx3 = GetOrAddVertex(v3, zoneVertices[zoneIndex], vertexIndexMaps[zoneIndex]);
                    
                    zoneTriangles[zoneIndex].Add(idx0);
                    zoneTriangles[zoneIndex].Add(idx2);
                    zoneTriangles[zoneIndex].Add(idx1);
                    
                    zoneTriangles[zoneIndex].Add(idx1);
                    zoneTriangles[zoneIndex].Add(idx2);
                    zoneTriangles[zoneIndex].Add(idx3);
                }
            }
            
            for (int i = 0; i < zoneCount; i++)
            {
                for (int v = 0; v < zoneVertices[i].Count; v++)
                {
                    Vector3 vert = zoneVertices[i][v];
                    zoneUVs[i].Add(new Vector2(vert.x, vert.z));
                    
                    if (vertexColorMode == VertexColorMode.White)
                    {
                        zoneColors[i].Add(Color.white);
                    }
                    else
                    {
                        float normalizedHeight = Mathf.InverseLerp(minHeight, maxHeight, vert.y);
                        zoneColors[i].Add(new Color(normalizedHeight, normalizedHeight, normalizedHeight, 1f));
                    }
                }
                
                Mesh mesh = new Mesh();
                mesh.name = $"ZoneMesh_{i}";
                mesh.vertices = zoneVertices[i].ToArray();
                mesh.triangles = zoneTriangles[i].ToArray();
                mesh.uv = zoneUVs[i].ToArray();
                mesh.colors = zoneColors[i].ToArray();
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                
                zoneMeshes[i] = new ZoneMeshData { mesh = mesh, zoneIndex = i };
            }
            
            return zoneMeshes;
        }
        
        int GetOrAddVertex(Vector3 vertex, List<Vector3> vertices, Dictionary<Vector3, int> indexMap)
        {
            if (indexMap.TryGetValue(vertex, out int existingIndex))
            {
                return existingIndex;
            }
            
            int newIndex = vertices.Count;
            vertices.Add(vertex);
            indexMap[vertex] = newIndex;
            return newIndex;
        }
        
        void SaveMeshAsset(Mesh mesh, string meshName)
        {
            string folderPath = "Assets/_Meshes/Terrain";
            
            if (!AssetDatabase.IsValidFolder("Assets/_Meshes"))
            {
                AssetDatabase.CreateFolder("Assets", "_Meshes");
            }
            
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/_Meshes", "Terrain");
            }
            
            string assetPath = $"{folderPath}/{meshName}.asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            
            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"💾 Mesh saved to: {assetPath}");
        }
        
        TerrainPreset CreatePresetFromCurrentSettings()
        {
            return new TerrainPreset
            {
                sizeMode = sizeMode,
                ringType = ringType,
                segments = segments,
                radialDivisions = radialDivisions,
                totalRadius = totalRadius,
                innerRingPercent = innerRingPercent,
                middleRingPercent = middleRingPercent,
                customInnerRadius = customInnerRadius,
                customOuterRadius = customOuterRadius,
                useLayeredNoise = useLayeredNoise,
                baseHeight = baseHeight,
                largeFeatureScale = largeFeatureScale,
                largeFeatureStrength = largeFeatureStrength,
                mediumFeatureScale = mediumFeatureScale,
                mediumFeatureStrength = mediumFeatureStrength,
                smallDetailScale = smallDetailScale,
                smallDetailStrength = smallDetailStrength,
                addRadialElevation = addRadialElevation,
                maxElevationDifference = maxElevationDifference,
                addHills = addHills,
                hillCount = hillCount,
                hillRadius = hillRadius,
                hillHeight = hillHeight,
                addValleys = addValleys,
                valleyCount = valleyCount,
                valleyDepth = valleyDepth,
                useSubmeshes = useSubmeshes,
                zoneCount = zoneCount,
                zonePattern = zonePattern,
                vertexColorMode = vertexColorMode,
                saveMeshAsset = saveMeshAsset,
                generateCollider = generateCollider,
                optimizeMesh = optimizeMesh
            };
        }
        
        void LoadPresetToCurrentSettings(TerrainPreset preset)
        {
            sizeMode = preset.sizeMode;
            ringType = preset.ringType;
            segments = preset.segments;
            radialDivisions = preset.radialDivisions;
            totalRadius = preset.totalRadius;
            innerRingPercent = preset.innerRingPercent;
            middleRingPercent = preset.middleRingPercent;
            customInnerRadius = preset.customInnerRadius;
            customOuterRadius = preset.customOuterRadius;
            useLayeredNoise = preset.useLayeredNoise;
            baseHeight = preset.baseHeight;
            largeFeatureScale = preset.largeFeatureScale;
            largeFeatureStrength = preset.largeFeatureStrength;
            mediumFeatureScale = preset.mediumFeatureScale;
            mediumFeatureStrength = preset.mediumFeatureStrength;
            smallDetailScale = preset.smallDetailScale;
            smallDetailStrength = preset.smallDetailStrength;
            addRadialElevation = preset.addRadialElevation;
            maxElevationDifference = preset.maxElevationDifference;
            addHills = preset.addHills;
            hillCount = preset.hillCount;
            hillRadius = preset.hillRadius;
            hillHeight = preset.hillHeight;
            addValleys = preset.addValleys;
            valleyCount = preset.valleyCount;
            valleyDepth = preset.valleyDepth;
            useSubmeshes = preset.useSubmeshes;
            zoneCount = preset.zoneCount;
            zonePattern = preset.zonePattern;
            vertexColorMode = preset.vertexColorMode;
            saveMeshAsset = preset.saveMeshAsset;
            generateCollider = preset.generateCollider;
            optimizeMesh = preset.optimizeMesh;
            
            Repaint();
        }
        
        void SavePresetDialog()
        {
            string presetName = EditorPrefs.GetString("TerrainGenerator_LastPresetName", "MyPreset");
            presetName = EditorUtility.SaveFilePanel(
                "Save Terrain Preset",
                "Assets/_TerrainPresets",
                presetName,
                "json"
            );
            
            if (string.IsNullOrEmpty(presetName)) return;
            
            TerrainPreset preset = CreatePresetFromCurrentSettings();
            preset.name = System.IO.Path.GetFileNameWithoutExtension(presetName);
            
            string json = JsonUtility.ToJson(preset, true);
            System.IO.File.WriteAllText(presetName, json);
            
            EditorPrefs.SetString("TerrainGenerator_LastPresetName", preset.name);
            
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog(
                "Preset Saved",
                $"Terrain preset saved:\n{preset.name}\n\nLocation:\n{presetName}",
                "OK"
            );
            
            Debug.Log($"💾 Terrain preset saved: {presetName}");
        }
        
        void LoadPresetDialog()
        {
            string presetPath = EditorUtility.OpenFilePanel(
                "Load Terrain Preset",
                "Assets/_TerrainPresets",
                "json"
            );
            
            if (string.IsNullOrEmpty(presetPath)) return;
            
            if (!System.IO.File.Exists(presetPath))
            {
                EditorUtility.DisplayDialog("Error", "Preset file not found!", "OK");
                return;
            }
            
            string json = System.IO.File.ReadAllText(presetPath);
            TerrainPreset preset = JsonUtility.FromJson<TerrainPreset>(json);
            
            if (preset == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to load preset!", "OK");
                return;
            }
            
            LoadPresetToCurrentSettings(preset);
            
            EditorUtility.DisplayDialog(
                "Preset Loaded",
                $"Preset loaded: {preset.name}\n\nAll settings updated!",
                "OK"
            );
            
            Debug.Log($"📂 Terrain preset loaded: {preset.name}");
        }
        
        void DeletePresetDialog()
        {
            string presetPath = EditorUtility.OpenFilePanel(
                "Select Preset to Delete",
                "Assets/_TerrainPresets",
                "json"
            );
            
            if (string.IsNullOrEmpty(presetPath)) return;
            
            string presetName = System.IO.Path.GetFileNameWithoutExtension(presetPath);
            
            if (EditorUtility.DisplayDialog(
                "Delete Preset",
                $"Delete preset '{presetName}'?\n\nThis cannot be undone!",
                "Delete",
                "Cancel"))
            {
                System.IO.File.Delete(presetPath);
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog("Deleted", $"Preset '{presetName}' deleted.", "OK");
                Debug.Log($"🗑️ Terrain preset deleted: {presetName}");
            }
        }
        
        void ListPresetsDialog()
        {
            string presetsFolder = "Assets/_TerrainPresets";
            
            if (!System.IO.Directory.Exists(presetsFolder))
            {
                EditorUtility.DisplayDialog(
                    "No Presets",
                    "No presets folder found!\n\nSave a preset first to create the folder.",
                    "OK"
                );
                return;
            }
            
            string[] presetFiles = System.IO.Directory.GetFiles(presetsFolder, "*.json");
            
            if (presetFiles.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Presets",
                    "No presets found!\n\nSave a preset first.",
                    "OK"
                );
                return;
            }
            
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"Found {presetFiles.Length} preset(s):\n");
            
            foreach (string file in presetFiles)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                
                try
                {
                    string json = System.IO.File.ReadAllText(file);
                    TerrainPreset preset = JsonUtility.FromJson<TerrainPreset>(json);
                    
                    sb.AppendLine($"• {name}");
                    sb.AppendLine($"  Ring: {preset.ringType}");
                    sb.AppendLine($"  Quality: {preset.segments}×{preset.radialDivisions}");
                    sb.AppendLine($"  Vertex Colors: {preset.vertexColorMode}");
                    sb.AppendLine();
                }
                catch
                {
                    sb.AppendLine($"• {name} (corrupted)");
                    sb.AppendLine();
                }
            }
            
            EditorUtility.DisplayDialog("Terrain Presets", sb.ToString(), "OK");
        }
    }
}
