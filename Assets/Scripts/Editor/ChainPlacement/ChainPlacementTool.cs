using UnityEngine;
using UnityEditor;

namespace ChainPlacement
{
    public class ChainPlacementTool : EditorWindow
    {
        private SegmentLibrary selectedLibrary;
        private int selectedSegmentIndex = 0;
        private Vector2 segmentScrollPosition;
        private float placementScale = 1f;
        
        private bool isPlacementMode = false;
        private Vector3 previewPosition;
        private Quaternion previewRotation = Quaternion.identity;
        
        [MenuItem("Tools/Chain Placement/Placement Tool")]
        public static void ShowWindow()
        {
            var window = GetWindow<ChainPlacementTool>("Chain Placement");
            window.minSize = new Vector2(300f, 400f);
            window.Show();
        }
        
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            isPlacementMode = false;
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Chain Placement Tool", titleStyle);
            
            EditorGUILayout.Space(10f);
            
            DrawLibrarySelection();
            
            EditorGUILayout.Space(10f);
            
            if (selectedLibrary != null)
            {
                DrawSegmentList();
                
                EditorGUILayout.Space(10f);
                
                DrawPlacementControls();
            }
            else
            {
                EditorGUILayout.HelpBox("Select a Segment Library to start placing segments.", MessageType.Info);
            }
        }
        
        private void DrawLibrarySelection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Segment Library", EditorStyles.boldLabel);
            
            selectedLibrary = (SegmentLibrary)EditorGUILayout.ObjectField(
                selectedLibrary,
                typeof(SegmentLibrary),
                false
            );
            
            if (selectedLibrary != null)
            {
                EditorGUILayout.LabelField($"Library: {selectedLibrary.libraryName}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Segments: {selectedLibrary.GetValidSegmentCount()}", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSegmentList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Available Segments", EditorStyles.boldLabel);
            
            if (selectedLibrary.segments.Length == 0)
            {
                EditorGUILayout.HelpBox("No segments defined in library.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }
            
            segmentScrollPosition = EditorGUILayout.BeginScrollView(
                segmentScrollPosition,
                GUILayout.Height(200f)
            );
            
            for (int i = 0; i < selectedLibrary.segments.Length; i++)
            {
                var segDef = selectedLibrary.segments[i];
                
                if (segDef == null || !segDef.IsValid())
                    continue;
                
                if (!segDef.canBeStartSegment)
                    continue;
                
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                
                bool isSelected = selectedSegmentIndex == i;
                Color bgColor = isSelected ? new Color(0.3f, 0.6f, 1f, 0.3f) : Color.clear;
                
                GUI.backgroundColor = bgColor;
                
                if (segDef.icon != null)
                {
                    if (GUILayout.Button(segDef.icon, GUILayout.Width(50f), GUILayout.Height(50f)))
                    {
                        selectedSegmentIndex = i;
                    }
                }
                else
                {
                    if (GUILayout.Button("No Icon", GUILayout.Width(50f), GUILayout.Height(50f)))
                    {
                        selectedSegmentIndex = i;
                    }
                }
                
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(segDef.segmentName, EditorStyles.boldLabel);
                
                if (segDef.prefab != null)
                {
                    EditorGUILayout.LabelField($"Prefab: {segDef.prefab.name}", EditorStyles.miniLabel);
                }
                
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        
        private void DrawPlacementControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
            
            var selectedSegment = selectedLibrary.GetSegment(selectedSegmentIndex);
            
            if (selectedSegment != null)
            {
                EditorGUILayout.LabelField($"Selected: {selectedSegment.segmentName}", EditorStyles.miniLabel);
                
                EditorGUILayout.Space(5f);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Placement Scale:", GUILayout.Width(110f));
                placementScale = EditorGUILayout.Slider(placementScale, 0.1f, 5f);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5f);
                
                Color buttonColor = isPlacementMode ? Color.green : GUI.backgroundColor;
                GUI.backgroundColor = buttonColor;
                
                string buttonText = isPlacementMode ? "PLACEMENT MODE ACTIVE - Click in Scene" : "Start Placement Mode";
                
                if (GUILayout.Button(buttonText, GUILayout.Height(40f)))
                {
                    isPlacementMode = !isPlacementMode;
                    
                    if (isPlacementMode)
                    {
                        if (placementScale == 0f)
                        {
                            placementScale = selectedSegment.defaultScale > 0f ? selectedSegment.defaultScale : 1f;
                        }
                        SceneView.lastActiveSceneView.Focus();
                    }
                }
                
                GUI.backgroundColor = Color.white;
                
                if (isPlacementMode)
                {
                    EditorGUILayout.HelpBox("Click in the Scene view to place the segment. Press ESC to cancel.", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select a valid segment to place.", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (!isPlacementMode || selectedLibrary == null)
                return;
            
            var selectedSegment = selectedLibrary.GetSegment(selectedSegmentIndex);
            
            if (selectedSegment == null || !selectedSegment.IsValid())
                return;
            
            Event e = Event.current;
            
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                previewPosition = hit.point;
                
                if (selectedLibrary.snapToGrid)
                {
                    float gridSize = selectedLibrary.gridSize;
                    previewPosition.x = Mathf.Round(previewPosition.x / gridSize) * gridSize;
                    previewPosition.z = Mathf.Round(previewPosition.z / gridSize) * gridSize;
                }
            }
            else
            {
                Plane plane = new Plane(Vector3.up, Vector3.zero);
                
                if (plane.Raycast(ray, out float distance))
                {
                    previewPosition = ray.GetPoint(distance);
                    
                    if (selectedLibrary.snapToGrid)
                    {
                        float gridSize = selectedLibrary.gridSize;
                        previewPosition.x = Mathf.Round(previewPosition.x / gridSize) * gridSize;
                        previewPosition.z = Mathf.Round(previewPosition.z / gridSize) * gridSize;
                    }
                }
            }
            
            DrawPreview(selectedSegment);
            
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                PlaceSegment(selectedSegment);
                e.Use();
            }
            
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                isPlacementMode = false;
                e.Use();
            }
            
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            sceneView.Repaint();
        }
        
        private void DrawPreview(SegmentDefinition segDef)
        {
            Handles.color = selectedLibrary.previewColor;
            Handles.CubeHandleCap(0, previewPosition, previewRotation, 0.5f, EventType.Repaint);
            
            Handles.color = Color.cyan;
            Handles.SphereHandleCap(0, previewPosition, Quaternion.identity, 0.2f, EventType.Repaint);
            
            Handles.color = Color.red;
            Handles.DrawLine(previewPosition, previewPosition + previewRotation * Vector3.right * 1f);
            
            Handles.color = Color.green;
            Handles.DrawLine(previewPosition, previewPosition + previewRotation * Vector3.up * 1f);
            
            Handles.color = Color.blue;
            Handles.DrawLine(previewPosition, previewPosition + previewRotation * Vector3.forward * 1f);
            
            GUIStyle labelStyle = new GUIStyle(EditorStyles.whiteBoldLabel);
            Handles.Label(previewPosition + Vector3.up * 0.5f, $"Place: {segDef.segmentName}", labelStyle);
        }
        
        private void PlaceSegment(SegmentDefinition segDef)
        {
            GameObject prefab = segDef.prefab;
            
            if (prefab == null)
            {
                Debug.LogError("Segment prefab is null!");
                return;
            }
            
            GameObject newObj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            
            if (newObj == null)
            {
                newObj = Instantiate(prefab);
            }
            
            newObj.transform.position = previewPosition;
            newObj.transform.rotation = previewRotation;
            newObj.transform.localScale = Vector3.one * placementScale;
            
            ChainSegment newSegment = newObj.GetComponent<ChainSegment>();
            
            if (newSegment == null)
            {
                newSegment = newObj.AddComponent<ChainSegment>();
            }
            
            newSegment.InitializeFromDefinition(selectedLibrary, selectedSegmentIndex);
            
            Undo.RegisterCreatedObjectUndo(newObj, "Place Chain Segment");
            
            Selection.activeGameObject = newObj;
            
            isPlacementMode = false;
            
            Debug.Log($"Placed segment: {segDef.segmentName} at {previewPosition} with scale {placementScale}");
        }
    }
}
