using UnityEngine;
using UnityEditor;

namespace ChainPlacement
{
    public class SpawnPivotVisualizer : EditorWindow
    {
        private GameObject testPrefab;
        private Vector3 pivotPosition = new Vector3(0f, 0f, 1f);
        private Vector3 pivotRotation = Vector3.zero;
        
        private GameObject previewInstance;
        private GameObject pivotGizmo;
        
        [MenuItem("Tools/Chain Placement/Spawn Pivot Visualizer")]
        public static void ShowWindow()
        {
            var window = GetWindow<SpawnPivotVisualizer>("Pivot Visualizer");
            window.minSize = new Vector2(350f, 400f);
            window.Show();
        }
        
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            CleanupPreview();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Spawn Pivot Visualizer", titleStyle);
            
            EditorGUILayout.Space(10f);
            
            DrawInfo();
            
            EditorGUILayout.Space(10f);
            
            DrawPrefabSelection();
            
            EditorGUILayout.Space(10f);
            
            DrawPivotControls();
            
            EditorGUILayout.Space(10f);
            
            DrawPreviewControls();
            
            EditorGUILayout.Space(10f);
            
            DrawCopyToClipboard();
        }
        
        private void DrawInfo()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Purpose", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use this tool to visually configure spawn pivot values for your segments. " +
                "Adjust the position and rotation to see where the next segment will spawn.",
                MessageType.Info
            );
            EditorGUILayout.EndVertical();
        }
        
        private void DrawPrefabSelection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Test Prefab", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            testPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Prefab",
                testPrefab,
                typeof(GameObject),
                false
            );
            
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePreview();
            }
            
            if (testPrefab == null)
            {
                EditorGUILayout.HelpBox("Assign a prefab to visualize its spawn pivot.", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawPivotControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Spawn Pivot Configuration", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            
            pivotPosition = EditorGUILayout.Vector3Field("Position (Local)", pivotPosition);
            pivotRotation = EditorGUILayout.Vector3Field("Rotation (Euler)", pivotRotation);
            
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePivotGizmo();
                SceneView.RepaintAll();
            }
            
            EditorGUILayout.Space(5f);
            
            EditorGUILayout.LabelField("Quick Presets:", EditorStyles.miniLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Straight 1m"))
            {
                pivotPosition = new Vector3(0f, 0f, 1f);
                pivotRotation = Vector3.zero;
                UpdatePivotGizmo();
            }
            
            if (GUILayout.Button("Straight 2m"))
            {
                pivotPosition = new Vector3(0f, 0f, 2f);
                pivotRotation = Vector3.zero;
                UpdatePivotGizmo();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Left 90°"))
            {
                pivotPosition = new Vector3(0f, 0f, 1f);
                pivotRotation = new Vector3(0f, -90f, 0f);
                UpdatePivotGizmo();
            }
            
            if (GUILayout.Button("Right 90°"))
            {
                pivotPosition = new Vector3(0f, 0f, 1f);
                pivotRotation = new Vector3(0f, 90f, 0f);
                UpdatePivotGizmo();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Up 90°"))
            {
                pivotPosition = new Vector3(0f, 0f, 1f);
                pivotRotation = new Vector3(90f, 0f, 0f);
                UpdatePivotGizmo();
            }
            
            if (GUILayout.Button("Down 90°"))
            {
                pivotPosition = new Vector3(0f, 0f, 1f);
                pivotRotation = new Vector3(-90f, 0f, 0f);
                UpdatePivotGizmo();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawPreviewControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            
            if (testPrefab != null && previewInstance == null)
            {
                if (GUILayout.Button("Create Preview in Scene", GUILayout.Height(30f)))
                {
                    CreatePreview();
                }
            }
            else if (previewInstance != null)
            {
                EditorGUILayout.HelpBox("Preview active in scene. Adjust values to see spawn point update.", MessageType.Info);
                
                if (GUILayout.Button("Clear Preview", GUILayout.Height(30f)))
                {
                    CleanupPreview();
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawCopyToClipboard()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Export Values", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Position:", GUILayout.Width(70f));
            EditorGUILayout.SelectableLabel($"({pivotPosition.x:F2}, {pivotPosition.y:F2}, {pivotPosition.z:F2})");
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Rotation:", GUILayout.Width(70f));
            EditorGUILayout.SelectableLabel($"({pivotRotation.x:F0}, {pivotRotation.y:F0}, {pivotRotation.z:F0})");
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5f);
            
            if (GUILayout.Button("Copy Values to Clipboard"))
            {
                string clipboard = $"Position: ({pivotPosition.x:F2}, {pivotPosition.y:F2}, {pivotPosition.z:F2})\n" +
                                 $"Rotation: ({pivotRotation.x:F0}, {pivotRotation.y:F0}, {pivotRotation.z:F0})";
                EditorGUIUtility.systemCopyBuffer = clipboard;
                Debug.Log("Copied to clipboard:\n" + clipboard);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void CreatePreview()
        {
            if (testPrefab == null)
                return;
            
            CleanupPreview();
            
            previewInstance = Instantiate(testPrefab);
            previewInstance.name = "[Preview] " + testPrefab.name;
            previewInstance.transform.position = Vector3.zero;
            previewInstance.transform.rotation = Quaternion.identity;
            
            CreatePivotGizmo();
            
            Selection.activeGameObject = previewInstance;
            SceneView.lastActiveSceneView.FrameSelected();
            
            Debug.Log("Created preview instance. Adjust pivot values to see spawn point.");
        }
        
        private void CreatePivotGizmo()
        {
            if (previewInstance == null)
                return;
            
            pivotGizmo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pivotGizmo.name = "[Spawn Point Gizmo]";
            pivotGizmo.transform.SetParent(previewInstance.transform);
            pivotGizmo.transform.localPosition = pivotPosition;
            pivotGizmo.transform.localRotation = Quaternion.Euler(pivotRotation);
            pivotGizmo.transform.localScale = Vector3.one * 0.2f;
            
            Renderer renderer = pivotGizmo.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0f, 1f, 0.5f, 0.5f);
                renderer.sharedMaterial = mat;
            }
            
            DestroyImmediate(pivotGizmo.GetComponent<Collider>());
        }
        
        private void UpdatePreview()
        {
            if (previewInstance != null)
            {
                CleanupPreview();
                CreatePreview();
            }
        }
        
        private void UpdatePivotGizmo()
        {
            if (pivotGizmo != null)
            {
                pivotGizmo.transform.localPosition = pivotPosition;
                pivotGizmo.transform.localRotation = Quaternion.Euler(pivotRotation);
            }
        }
        
        private void CleanupPreview()
        {
            if (previewInstance != null)
            {
                DestroyImmediate(previewInstance);
                previewInstance = null;
            }
            
            if (pivotGizmo != null)
            {
                DestroyImmediate(pivotGizmo);
                pivotGizmo = null;
            }
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (previewInstance == null || pivotGizmo == null)
                return;
            
            Vector3 pivotWorldPos = pivotGizmo.transform.position;
            Quaternion pivotWorldRot = pivotGizmo.transform.rotation;
            
            Handles.color = Color.red;
            Handles.DrawLine(pivotWorldPos, pivotWorldPos + pivotWorldRot * Vector3.right * 0.5f);
            Handles.ArrowHandleCap(0, pivotWorldPos, pivotWorldRot * Quaternion.LookRotation(Vector3.right), 0.5f, EventType.Repaint);
            
            Handles.color = Color.green;
            Handles.DrawLine(pivotWorldPos, pivotWorldPos + pivotWorldRot * Vector3.up * 0.5f);
            Handles.ArrowHandleCap(0, pivotWorldPos, pivotWorldRot * Quaternion.LookRotation(Vector3.up), 0.5f, EventType.Repaint);
            
            Handles.color = Color.blue;
            Handles.DrawLine(pivotWorldPos, pivotWorldPos + pivotWorldRot * Vector3.forward * 0.5f);
            Handles.ArrowHandleCap(0, pivotWorldPos, pivotWorldRot * Quaternion.LookRotation(Vector3.forward), 0.5f, EventType.Repaint);
            
            GUIStyle labelStyle = new GUIStyle(EditorStyles.whiteBoldLabel)
            {
                fontSize = 12
            };
            Handles.Label(pivotWorldPos + Vector3.up * 0.5f, "SPAWN POINT", labelStyle);
        }
    }
}
