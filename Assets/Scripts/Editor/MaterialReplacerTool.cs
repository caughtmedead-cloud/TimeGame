using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Thelos.Editor
{
    public class MaterialReplacerTool : EditorWindow
    {
        private DefaultAsset oldMaterialFolder;
        private DefaultAsset newMaterialFolder;
        private DefaultAsset targetPrefabFolder;
        private string oldPrefix = "";
        private string newPrefix = "M_";
        private bool searchInChildren = true;
        private bool applyToPrefabs = true;
        
        private Vector2 scrollPosition;
        private List<ReplacementInfo> replacements = new List<ReplacementInfo>();
        private bool hasScanned = false;

        private class ReplacementInfo
        {
            public GameObject prefab;
            public Material oldMaterial;
            public Material newMaterial;
            public int occurrences;
        }

        [MenuItem("Tools/Material Replacer")]
        public static void ShowWindow()
        {
            GetWindow<MaterialReplacerTool>("Material Replacer");
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("Batch Material Replacer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Replace old materials with new materials on prefabs.\n" +
                "Matches materials by name using prefixes.\n\n" +
                "Example: 'pavement_0001' → 'M_pavement_0001'",
                MessageType.Info
            );

            EditorGUILayout.Space();

            oldMaterialFolder = EditorGUILayout.ObjectField(
                "Old Materials Folder",
                oldMaterialFolder,
                typeof(DefaultAsset),
                false
            ) as DefaultAsset;

            newMaterialFolder = EditorGUILayout.ObjectField(
                "New Materials Folder",
                newMaterialFolder,
                typeof(DefaultAsset),
                false
            ) as DefaultAsset;

            targetPrefabFolder = EditorGUILayout.ObjectField(
                "Prefab Folder (Optional)",
                targetPrefabFolder,
                typeof(DefaultAsset),
                false
            ) as DefaultAsset;

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Naming Convention:", EditorStyles.boldLabel);
            oldPrefix = EditorGUILayout.TextField("Old Prefix", oldPrefix);
            newPrefix = EditorGUILayout.TextField("New Prefix", newPrefix);

            EditorGUILayout.Space();

            searchInChildren = EditorGUILayout.Toggle("Search in Subfolders", searchInChildren);
            applyToPrefabs = EditorGUILayout.Toggle("Apply to Prefabs", applyToPrefabs);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = oldMaterialFolder != null && newMaterialFolder != null;
            if (GUILayout.Button("Scan for Replacements", GUILayout.Height(30)))
            {
                ScanForReplacements();
            }

            GUI.enabled = hasScanned && replacements.Count > 0;
            if (GUILayout.Button("Apply Replacements", GUILayout.Height(30)))
            {
                ApplyReplacements();
            }
            
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (hasScanned)
            {
                DisplayReplacementResults();
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void ScanForReplacements()
        {
            replacements.Clear();
            hasScanned = false;

            string oldFolderPath = AssetDatabase.GetAssetPath(oldMaterialFolder);
            string newFolderPath = AssetDatabase.GetAssetPath(newMaterialFolder);

            if (string.IsNullOrEmpty(oldFolderPath) || string.IsNullOrEmpty(newFolderPath))
            {
                EditorUtility.DisplayDialog("Error", "Please select valid material folders.", "OK");
                return;
            }

            Dictionary<string, Material> oldMaterials = LoadMaterialsFromFolder(oldFolderPath, oldPrefix);
            Dictionary<string, Material> newMaterials = LoadMaterialsFromFolder(newFolderPath, newPrefix);

            string[] prefabGuids;
            if (targetPrefabFolder != null)
            {
                string prefabFolderPath = AssetDatabase.GetAssetPath(targetPrefabFolder);
                string searchPattern = searchInChildren ? "t:Prefab" : "t:Prefab";
                prefabGuids = AssetDatabase.FindAssets(searchPattern, new[] { prefabFolderPath });
            }
            else
            {
                prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            }

            EditorUtility.DisplayProgressBar("Scanning", "Scanning prefabs for materials...", 0);

            Dictionary<string, ReplacementInfo> replacementDict = new Dictionary<string, ReplacementInfo>();

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                
                if (!searchInChildren && targetPrefabFolder != null)
                {
                    string prefabFolder = System.IO.Path.GetDirectoryName(prefabPath).Replace("\\", "/");
                    string targetFolder = AssetDatabase.GetAssetPath(targetPrefabFolder);
                    if (prefabFolder != targetFolder)
                        continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) continue;

                EditorUtility.DisplayProgressBar("Scanning", $"Scanning {prefab.name}...", (float)i / prefabGuids.Length);

                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                
                foreach (Renderer renderer in renderers)
                {
                    Material[] materials = renderer.sharedMaterials;
                    
                    for (int matIndex = 0; matIndex < materials.Length; matIndex++)
                    {
                        Material mat = materials[matIndex];
                        if (mat == null) continue;

                        string baseName = GetBaseName(mat.name, oldPrefix);
                        
                        if (oldMaterials.ContainsKey(baseName) && newMaterials.ContainsKey(baseName))
                        {
                            string key = $"{prefabPath}_{baseName}";
                            
                            if (!replacementDict.ContainsKey(key))
                            {
                                replacementDict[key] = new ReplacementInfo
                                {
                                    prefab = prefab,
                                    oldMaterial = oldMaterials[baseName],
                                    newMaterial = newMaterials[baseName],
                                    occurrences = 0
                                };
                            }
                            
                            replacementDict[key].occurrences++;
                        }
                    }
                }
            }

            replacements = replacementDict.Values.OrderBy(r => r.prefab.name).ToList();
            hasScanned = true;

            EditorUtility.ClearProgressBar();

            if (replacements.Count == 0)
            {
                EditorUtility.DisplayDialog("Scan Complete", "No materials found to replace.", "OK");
            }
        }

        private void ApplyReplacements()
        {
            if (replacements.Count == 0) return;

            if (!EditorUtility.DisplayDialog(
                "Confirm Replacement",
                $"Replace materials in {replacements.Count} prefab(s)?\n\nThis action can be undone with Ctrl+Z.",
                "Replace",
                "Cancel"))
            {
                return;
            }

            int replacedCount = 0;

            EditorUtility.DisplayProgressBar("Replacing Materials", "Processing prefabs...", 0);

            for (int i = 0; i < replacements.Count; i++)
            {
                ReplacementInfo info = replacements[i];
                
                EditorUtility.DisplayProgressBar("Replacing Materials", $"Processing {info.prefab.name}...", (float)i / replacements.Count);

                string prefabPath = AssetDatabase.GetAssetPath(info.prefab);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (applyToPrefabs)
                {
                    Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                    
                    foreach (Renderer renderer in renderers)
                    {
                        Material[] materials = renderer.sharedMaterials;
                        bool changed = false;
                        
                        for (int matIndex = 0; matIndex < materials.Length; matIndex++)
                        {
                            if (materials[matIndex] == info.oldMaterial)
                            {
                                materials[matIndex] = info.newMaterial;
                                changed = true;
                            }
                        }
                        
                        if (changed)
                        {
                            renderer.sharedMaterials = materials;
                            EditorUtility.SetDirty(renderer);
                        }
                    }

                    EditorUtility.SetDirty(prefab);
                    replacedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog(
                "Success",
                $"Successfully replaced materials in {replacedCount} prefab(s)!",
                "OK"
            );

            replacements.Clear();
            hasScanned = false;
        }

        private void DisplayReplacementResults()
        {
            EditorGUILayout.LabelField($"Found {replacements.Count} Replacement(s):", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (replacements.Count == 0)
            {
                EditorGUILayout.HelpBox("No materials to replace found.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            foreach (ReplacementInfo info in replacements)
            {
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.ObjectField(info.prefab, typeof(GameObject), false, GUILayout.Width(150));
                EditorGUILayout.LabelField("→", GUILayout.Width(20));
                EditorGUILayout.ObjectField(info.oldMaterial, typeof(Material), false, GUILayout.Width(150));
                EditorGUILayout.LabelField("→", GUILayout.Width(20));
                EditorGUILayout.ObjectField(info.newMaterial, typeof(Material), false, GUILayout.Width(150));
                EditorGUILayout.LabelField($"({info.occurrences}x)", GUILayout.Width(40));
                
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }
            
            EditorGUILayout.EndVertical();

            int totalOccurrences = replacements.Sum(r => r.occurrences);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Total Material Slots: {totalOccurrences}", EditorStyles.boldLabel);
        }

        private Dictionary<string, Material> LoadMaterialsFromFolder(string folderPath, string prefix)
        {
            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
            
            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                
                if (mat != null)
                {
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        if (!mat.name.StartsWith(prefix))
                            continue;
                    }
                    
                    string baseName = GetBaseName(mat.name, prefix);
                    materials[baseName] = mat;
                }
            }
            
            return materials;
        }

        private string GetBaseName(string materialName, string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return materialName;
            
            if (materialName.StartsWith(prefix))
                return materialName.Substring(prefix.Length);
            
            return materialName;
        }
    }
}
