using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace BuildingTools
{
    public class BuildingLibrarySetupHelper : EditorWindow
    {
        private BuildingLibrary targetLibrary;
        private string searchFolder = "Assets";
        private string searchFilter = "";
        private Vector2 scrollPosition;
        
        private List<GameObject> foundPrefabs = new List<GameObject>();
        private Dictionary<GameObject, bool> selectedPrefabs = new Dictionary<GameObject, bool>();
        private Dictionary<GameObject, BuildingPieceType> prefabTypes = new Dictionary<GameObject, BuildingPieceType>();
        
        [MenuItem("Tools/Building Tools/Library Setup Helper")]
        public static void ShowWindow()
        {
            BuildingLibrarySetupHelper window = GetWindow<BuildingLibrarySetupHelper>("Library Setup");
            window.minSize = new Vector2(400, 500);
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Building Library Setup Helper", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Quickly populate a Building Library with prefabs from any folder.", MessageType.Info);
            
            EditorGUILayout.Space();
            
            targetLibrary = EditorGUILayout.ObjectField("Target Library", targetLibrary, typeof(BuildingLibrary), false) as BuildingLibrary;
            
            if (targetLibrary == null)
            {
                EditorGUILayout.HelpBox("Create or assign a Building Library to continue.", MessageType.Warning);
                
                if (GUILayout.Button("Create New Library", GUILayout.Height(30)))
                {
                    CreateNewLibrary();
                }
                return;
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Search Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            searchFolder = EditorGUILayout.TextField("Search Folder", searchFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        searchFolder = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            searchFilter = EditorGUILayout.TextField("Name Filter (optional)", searchFilter);
            EditorGUILayout.HelpBox("Filter by name, e.g., 'Wall' or 'Floor' to find specific pieces", MessageType.None);
            
            if (GUILayout.Button("Search for Prefabs", GUILayout.Height(30)))
            {
                SearchForPrefabs();
            }
            
            EditorGUILayout.Space();
            
            if (foundPrefabs.Count > 0)
            {
                EditorGUILayout.LabelField($"Found Prefabs ({foundPrefabs.Count})", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All"))
                {
                    foreach (var prefab in foundPrefabs)
                    {
                        selectedPrefabs[prefab] = true;
                    }
                }
                if (GUILayout.Button("Deselect All"))
                {
                    foreach (var prefab in foundPrefabs)
                    {
                        selectedPrefabs[prefab] = false;
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();
                
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
                
                foreach (var prefab in foundPrefabs)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.BeginHorizontal();
                    
                    if (!selectedPrefabs.ContainsKey(prefab))
                        selectedPrefabs[prefab] = false;
                    
                    selectedPrefabs[prefab] = EditorGUILayout.Toggle(selectedPrefabs[prefab], GUILayout.Width(20));
                    
                    EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
                    
                    EditorGUILayout.EndHorizontal();
                    
                    if (selectedPrefabs[prefab])
                    {
                        if (!prefabTypes.ContainsKey(prefab))
                        {
                            prefabTypes[prefab] = AutoDetectType(prefab);
                        }
                        
                        prefabTypes[prefab] = (BuildingPieceType)EditorGUILayout.EnumPopup("Type", prefabTypes[prefab]);
                    }
                    
                    EditorGUILayout.EndVertical();
                }
                
                EditorGUILayout.EndScrollView();
                
                EditorGUILayout.Space();
                
                int selectedCount = selectedPrefabs.Count(kvp => kvp.Value);
                
                GUI.enabled = selectedCount > 0;
                if (GUILayout.Button($"Add {selectedCount} Selected Prefabs to Library", GUILayout.Height(40)))
                {
                    AddSelectedPrefabsToLibrary();
                }
                GUI.enabled = true;
            }
            else if (foundPrefabs.Count == 0 && selectedPrefabs.Count > 0)
            {
                EditorGUILayout.HelpBox("No prefabs found. Try a different folder or filter.", MessageType.Info);
            }
        }
        
        private void CreateNewLibrary()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Building Library",
                "New Building Library",
                "asset",
                "Create a new building library"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                BuildingLibrary newLibrary = CreateInstance<BuildingLibrary>();
                newLibrary.libraryName = System.IO.Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(newLibrary, path);
                AssetDatabase.SaveAssets();
                
                targetLibrary = newLibrary;
            }
        }
        
        private void SearchForPrefabs()
        {
            foundPrefabs.Clear();
            selectedPrefabs.Clear();
            prefabTypes.Clear();
            
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { searchFolder });
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab != null)
                {
                    if (string.IsNullOrEmpty(searchFilter) || 
                        prefab.name.IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foundPrefabs.Add(prefab);
                    }
                }
            }
            
            foundPrefabs = foundPrefabs.OrderBy(p => p.name).ToList();
        }
        
        private BuildingPieceType AutoDetectType(GameObject prefab)
        {
            string name = prefab.name.ToLower();
            
            if (name.Contains("concrete") && name.Contains("wall"))
                return BuildingPieceType.ConcreteWall;
            
            if (name.Contains("concrete") && name.Contains("floor"))
                return BuildingPieceType.ConcreteFloor;
            
            if (name.Contains("tile") && name.Contains("wall"))
                return BuildingPieceType.TileWall;
            
            if (name.Contains("tile") && name.Contains("floor"))
                return BuildingPieceType.TileFloor;
            
            if (name.Contains("floor") || name.Contains("ground"))
                return BuildingPieceType.ConcreteFloor;
            
            if (name.Contains("wall"))
                return BuildingPieceType.ConcreteWall;
            
            if (name.Contains("door"))
                return BuildingPieceType.Door;
            
            if (name.Contains("window"))
                return BuildingPieceType.Window;
            
            if (name.Contains("decor") || name.Contains("balcony"))
                return BuildingPieceType.Decor;
            
            return BuildingPieceType.ConcreteWall;
        }
        
        private void AddSelectedPrefabsToLibrary()
        {
            if (targetLibrary == null)
                return;
            
            var selected = foundPrefabs.Where(p => selectedPrefabs.ContainsKey(p) && selectedPrefabs[p]).ToList();
            
            if (selected.Count == 0)
                return;
            
            Undo.RecordObject(targetLibrary, "Add Prefabs to Library");
            
            List<BuildingPieceDefinition> newPieces = new List<BuildingPieceDefinition>();
            
            if (targetLibrary.pieces != null)
            {
                newPieces.AddRange(targetLibrary.pieces);
            }
            
            foreach (var prefab in selected)
            {
                BuildingPieceDefinition newPiece = new BuildingPieceDefinition
                {
                    pieceName = prefab.name,
                    prefab = prefab,
                    pieceType = prefabTypes.ContainsKey(prefab) ? prefabTypes[prefab] : BuildingPieceType.ConcreteWall,
                    gridSize = new Vector3(4, 3, 0.5f),
                    sockets = new Socket[0],
                    canPlaceOnGround = true,
                    canStack = false
                };
                
                newPieces.Add(newPiece);
            }
            
            targetLibrary.pieces = newPieces.ToArray();
            
            EditorUtility.SetDirty(targetLibrary);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Added {selected.Count} prefabs to {targetLibrary.libraryName}");
            
            EditorGUIUtility.PingObject(targetLibrary);
        }
    }
}
