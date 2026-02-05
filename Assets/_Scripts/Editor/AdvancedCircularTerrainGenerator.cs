using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Thelos.Editor
{
    public class AdvancedCircularTerrainGenerator : EditorWindow
    {
        private enum RingType { InnerRing, MiddleRing, OuterRing, Custom }
        
        private RingType ringType = RingType.InnerRing;
        private int segments = 128;
        private int radialDivisions = 32;
        
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
        
        [Header("Output Settings")]
        private bool saveMeshAsset = true;
        private bool generateCollider = true;
        private bool optimizeMesh = true;
        
        private Vector2 scrollPosition;
        
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
            
            ringType = (RingType)EditorGUILayout.EnumPopup("Ring Type", ringType);
            
            if (ringType == RingType.Custom)
            {
                customInnerRadius = EditorGUILayout.FloatField("Inner Radius (m)", customInnerRadius);
                customOuterRadius = EditorGUILayout.FloatField("Outer Radius (m)", customOuterRadius);
                
                if (customInnerRadius < 0f) customInnerRadius = 0f;
                if (customOuterRadius <= customInnerRadius) customOuterRadius = customInnerRadius + 50f;
            }
        }
        
        void DrawMeshQualitySettings()
        {
            GUILayout.Label("Mesh Quality", EditorStyles.boldLabel);
            
            segments = EditorGUILayout.IntSlider("Circular Segments", segments, 64, 256);
            radialDivisions = EditorGUILayout.IntSlider("Radial Divisions", radialDivisions, 16, 64);
            
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
        
        string GetRingInfo()
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
        
        void GenerateAdvancedTerrain()
        {
            float innerRadius, outerRadius;
            string ringName;
            
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
            
            EditorUtility.DisplayProgressBar("Generating Terrain", "Creating mesh geometry...", 0.1f);
            
            GameObject meshObject = new GameObject(ringName);
            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
            
            Vector3[] hillPositions = GenerateRandomPositions(hillCount, innerRadius, outerRadius);
            Vector3[] valleyPositions = GenerateRandomPositions(valleyCount, innerRadius, outerRadius);
            
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
                    
                    float uvScale = 10f;
                    uvs[vertIndex] = new Vector2(seg / (float)circleSegments * uvScale, radialT * uvScale);
                    
                    if (y > maxHeight) maxHeight = y;
                    if (y < minHeight) minHeight = y;
                    
                    vertIndex++;
                }
            }
            
            for (int i = 0; i < vertices.Length; i++)
            {
                float normalizedHeight = Mathf.InverseLerp(minHeight, maxHeight, vertices[i].y);
                colors[i] = new Color(normalizedHeight, normalizedHeight, normalizedHeight, 1f);
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
    }
}
