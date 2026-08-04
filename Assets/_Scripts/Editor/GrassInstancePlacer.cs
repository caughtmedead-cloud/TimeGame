using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Thelos.Editor
{
    public class GrassInstancePlacer : EditorWindow
    {
        private GameObject grassPrefab;
        private Material sharedMaterial;
        private int grassCount = 1000;
        private float areaRadius = 50f;
        private float minScale = 0.8f;
        private float maxScale = 1.2f;
        private LayerMask groundLayer = 1;
        private bool clusterGrass = true;
        private int bladesPerCluster = 50;
        private bool randomRotation = true;
        private bool alignToNormal = false;
        private float normalAlignment = 0.5f;
        private Vector2 scrollPosition;
        
        [MenuItem("Thelos/Grass Instance Placer")]
        static void ShowWindow()
        {
            GrassInstancePlacer window = GetWindow<GrassInstancePlacer>("Grass Placer");
            window.minSize = new Vector2(350, 500);
        }
        
        void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("Grass Instance Placer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Places grass mesh instances optimized for SRP Batcher and ProPixelizer", MessageType.Info);
            EditorGUILayout.Space();
            
            DrawPrefabSettings();
            EditorGUILayout.Space();
            
            DrawPlacementSettings();
            EditorGUILayout.Space();
            
            DrawTransformSettings();
            EditorGUILayout.Space();
            
            DrawOptimizationSettings();
            EditorGUILayout.Space();
            
            DrawButtons();
            
            EditorGUILayout.EndScrollView();
        }
        
        void DrawPrefabSettings()
        {
            GUILayout.Label("Prefab & Material", EditorStyles.boldLabel);
            
            grassPrefab = (GameObject)EditorGUILayout.ObjectField("Grass Prefab", grassPrefab, typeof(GameObject), false);
            sharedMaterial = (Material)EditorGUILayout.ObjectField("Shared Material", sharedMaterial, typeof(Material), false);
            
            if (sharedMaterial != null)
            {
                EditorGUILayout.HelpBox("All grass instances will share this material for SRP Batching.", MessageType.Info);
            }
        }
        
        void DrawPlacementSettings()
        {
            GUILayout.Label("Placement Settings", EditorStyles.boldLabel);
            
            grassCount = EditorGUILayout.IntSlider("Grass Count", grassCount, 100, 10000);
            areaRadius = EditorGUILayout.Slider("Area Radius (m)", areaRadius, 10f, 200f);
            groundLayer = LayerMaskField("Ground Layer", groundLayer);
        }
        
        void DrawTransformSettings()
        {
            GUILayout.Label("Transform Settings", EditorStyles.boldLabel);
            
            minScale = EditorGUILayout.Slider("Min Scale", minScale, 0.5f, 1.5f);
            maxScale = EditorGUILayout.Slider("Max Scale", maxScale, minScale, 2f);
            randomRotation = EditorGUILayout.Toggle("Random Y Rotation", randomRotation);
            alignToNormal = EditorGUILayout.Toggle("Align to Surface Normal", alignToNormal);
            
            if (alignToNormal)
            {
                EditorGUI.indentLevel++;
                normalAlignment = EditorGUILayout.Slider("Alignment Strength", normalAlignment, 0f, 1f);
                EditorGUI.indentLevel--;
            }
        }
        
        void DrawOptimizationSettings()
        {
            GUILayout.Label("Optimization", EditorStyles.boldLabel);
            
            clusterGrass = EditorGUILayout.Toggle("Cluster Grass", clusterGrass);
            
            if (clusterGrass)
            {
                EditorGUI.indentLevel++;
                bladesPerCluster = EditorGUILayout.IntSlider("Blades Per Cluster", bladesPerCluster, 10, 100);
                EditorGUI.indentLevel--;
                
                EditorGUILayout.HelpBox("Clustering improves culling and SECTR streaming performance.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Not clustering will create many individual GameObjects (not recommended for large scenes).", MessageType.Warning);
            }
        }
        
        void DrawButtons()
        {
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Place Grass Instances", GUILayout.Height(40)))
            {
                PlaceGrass();
            }
            
            if (GUILayout.Button("Clear All Grass", GUILayout.Height(30)))
            {
                ClearGrass();
            }
            
            EditorGUILayout.Space();
            
            if (grassPrefab != null && sharedMaterial != null)
            {
                int clusterCount = clusterGrass ? Mathf.CeilToInt(grassCount / (float)bladesPerCluster) : 1;
                EditorGUILayout.HelpBox($"Will create {grassCount} instances in {clusterCount} cluster(s)", MessageType.None);
            }
        }
        
        void PlaceGrass()
        {
            if (grassPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a grass prefab!", "OK");
                return;
            }
            
            if (sharedMaterial == null)
            {
                if (!EditorUtility.DisplayDialog("Warning", "No shared material assigned. Instances will use prefab's material (may break SRP Batching).", "Continue", "Cancel"))
                {
                    return;
                }
            }
            
            GameObject rootObject = new GameObject("GrassInstances_Root");
            int clusterCount = clusterGrass ? Mathf.CeilToInt(grassCount / (float)bladesPerCluster) : grassCount;
            
            List<GameObject> clusters = new List<GameObject>();
            for (int i = 0; i < clusterCount; i++)
            {
                GameObject cluster = new GameObject($"GrassCluster_{i:D4}");
                cluster.transform.parent = rootObject.transform;
                clusters.Add(cluster);
            }
            
            int placedCount = 0;
            int failedCount = 0;
            
            for (int i = 0; i < grassCount; i++)
            {
                if (i % 100 == 0)
                {
                    float progress = i / (float)grassCount;
                    EditorUtility.DisplayProgressBar("Placing Grass", $"Placed {i}/{grassCount} instances...", progress);
                }
                
                Vector2 randomCircle = Random.insideUnitCircle * areaRadius;
                Vector3 spawnPos = new Vector3(randomCircle.x, 1000f, randomCircle.y);
                
                if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 2000f, groundLayer))
                {
                    GameObject grassInstance = (GameObject)PrefabUtility.InstantiatePrefab(grassPrefab);
                    grassInstance.transform.position = hit.point;
                    
                    Quaternion rotation = Quaternion.identity;
                    
                    if (randomRotation)
                    {
                        rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    }
                    
                    if (alignToNormal)
                    {
                        Quaternion normalRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                        rotation = Quaternion.Slerp(rotation, normalRotation * rotation, normalAlignment);
                    }
                    
                    grassInstance.transform.rotation = rotation;
                    
                    float scale = Random.Range(minScale, maxScale);
                    grassInstance.transform.localScale = Vector3.one * scale;
                    
                    if (sharedMaterial != null)
                    {
                        MeshRenderer renderer = grassInstance.GetComponent<MeshRenderer>();
                        if (renderer != null)
                        {
                            renderer.sharedMaterial = sharedMaterial;
                        }
                    }
                    
                    if (clusterGrass)
                    {
                        int clusterIndex = i / bladesPerCluster;
                        grassInstance.transform.parent = clusters[clusterIndex].transform;
                    }
                    else
                    {
                        grassInstance.transform.parent = rootObject.transform;
                    }
                    
                    placedCount++;
                }
                else
                {
                    failedCount++;
                }
            }
            
            EditorUtility.ClearProgressBar();
            
            Selection.activeGameObject = rootObject;
            Undo.RegisterCreatedObjectUndo(rootObject, "Place Grass Instances");
            
            string message = $"✅ Placed {placedCount} grass instances";
            if (clusterGrass)
            {
                message += $" in {clusterCount} clusters";
            }
            if (failedCount > 0)
            {
                message += $"\n⚠️ {failedCount} instances failed to place (no ground found)";
            }
            
            Debug.Log(message);
        }
        
        void ClearGrass()
        {
            GameObject root = GameObject.Find("GrassInstances_Root");
            if (root != null)
            {
                if (EditorUtility.DisplayDialog("Clear Grass", "Are you sure you want to delete all grass instances?", "Yes", "Cancel"))
                {
                    DestroyImmediate(root);
                    Debug.Log("Cleared grass instances");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("No Grass Found", "No grass instances found in scene.", "OK");
            }
        }
        
        LayerMask LayerMaskField(string label, LayerMask layerMask)
        {
            List<string> layers = new List<string>();
            List<int> layerNumbers = new List<int>();
            
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layers.Add(layerName);
                    layerNumbers.Add(i);
                }
            }
            
            int maskWithoutEmpty = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if (((1 << layerNumbers[i]) & layerMask.value) > 0)
                    maskWithoutEmpty |= (1 << i);
            }
            
            maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());
            
            int mask = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if ((maskWithoutEmpty & (1 << i)) > 0)
                    mask |= (1 << layerNumbers[i]);
            }
            
            layerMask.value = mask;
            return layerMask;
        }
    }
}
