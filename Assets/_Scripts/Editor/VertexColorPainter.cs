using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Thelos.Editor
{
    public class VertexColorPainter : EditorWindow
    {
        [MenuItem("Thelos/Vertex Color Painter")]
        static void OpenWindow()
        {
            VertexColorPainter window = GetWindow<VertexColorPainter>("Vertex Color Painter");
            window.minSize = new Vector2(320, 500);
            window.Show();
        }

        private GameObject targetObject;
        private MeshFilter targetMeshFilter;
        private Mesh editableMesh;
        
        private enum PaintChannel { Red, Green, Blue, Alpha, All }
        private PaintChannel paintChannel = PaintChannel.Red;
        
        private float brushSize = 5f;
        private float brushStrength = 0.5f;
        private float brushFalloff = 0.5f;
        
        private bool isPainting = false;
        private bool showVertexColors = true;
        private bool normalizeColors = true;
        
        private Color paintColor = Color.white;
        private Material previewMaterial;
        
        private Vector2 scrollPosition;
        
        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            CreatePreviewMaterial();
        }
        
        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            
            if (previewMaterial != null)
            {
                DestroyImmediate(previewMaterial);
            }
        }
        
        void CreatePreviewMaterial()
        {
            Shader shader = Shader.Find("Hidden/VertexColorPreview");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            previewMaterial = new Material(shader);
        }
        
        void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("Vertex Color Painter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Paint vertex colors to control texture blending on terrain meshes.\n\nRed = Texture 1\nGreen = Texture 2\nBlue = Texture 3\nAlpha = Texture 4", MessageType.Info);
            EditorGUILayout.Space();
            
            DrawTargetSelection();
            EditorGUILayout.Space();
            
            if (targetMeshFilter != null && editableMesh != null)
            {
                DrawBrushSettings();
                EditorGUILayout.Space();
                
                DrawChannelPresets();
                EditorGUILayout.Space();
                
                DrawUtilities();
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        void DrawTargetSelection()
        {
            GUILayout.Label("Target Mesh", EditorStyles.boldLabel);
            
            GameObject newTarget = (GameObject)EditorGUILayout.ObjectField("Target GameObject", targetObject, typeof(GameObject), true);
            
            if (newTarget != targetObject)
            {
                targetObject = newTarget;
                SetupMesh();
            }
            
            if (targetObject == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with a MeshFilter to start painting.", MessageType.Warning);
            }
            else if (targetMeshFilter == null)
            {
                EditorGUILayout.HelpBox("Selected GameObject has no MeshFilter component.", MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox($"Mesh: {editableMesh.name}\nVertices: {editableMesh.vertexCount}", MessageType.None);
            }
        }
        
        void DrawBrushSettings()
        {
            GUILayout.Label("Brush Settings", EditorStyles.boldLabel);
            
            paintChannel = (PaintChannel)EditorGUILayout.EnumPopup("Paint Channel", paintChannel);
            
            EditorGUILayout.Space();
            brushSize = EditorGUILayout.Slider("Brush Size", brushSize, 0.1f, 50f);
            brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0f, 1f);
            brushFalloff = EditorGUILayout.Slider("Brush Falloff", brushFalloff, 0f, 1f);
            
            EditorGUILayout.Space();
            normalizeColors = EditorGUILayout.Toggle("Auto Normalize (RGBA = 1)", normalizeColors);
            
            if (normalizeColors)
            {
                EditorGUILayout.HelpBox("Colors will be normalized so R+G+B+A = 1.0 (prevents over-bright blending)", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            showVertexColors = EditorGUILayout.Toggle("Show Vertex Colors", showVertexColors);
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Hold SHIFT and click in Scene View to paint.\nHold SHIFT and drag to paint continuously.", MessageType.Info);
        }
        
        void DrawChannelPresets()
        {
            GUILayout.Label("Quick Presets", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Red (Texture 1)", GUILayout.Height(30)))
            {
                paintChannel = PaintChannel.Red;
            }
            if (GUILayout.Button("Green (Texture 2)", GUILayout.Height(30)))
            {
                paintChannel = PaintChannel.Green;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Blue (Texture 3)", GUILayout.Height(30)))
            {
                paintChannel = PaintChannel.Blue;
            }
            if (GUILayout.Button("Alpha (Texture 4)", GUILayout.Height(30)))
            {
                paintChannel = PaintChannel.Alpha;
            }
            EditorGUILayout.EndHorizontal();
        }
        
        void DrawUtilities()
        {
            GUILayout.Label("Utilities", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill Red (1,0,0,0)"))
            {
                FillVertexColors(new Color(1, 0, 0, 0));
            }
            if (GUILayout.Button("Fill Green (0,1,0,0)"))
            {
                FillVertexColors(new Color(0, 1, 0, 0));
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill Blue (0,0,1,0)"))
            {
                FillVertexColors(new Color(0, 0, 1, 0));
            }
            if (GUILayout.Button("Fill Alpha (0,0,0,1)"))
            {
                FillVertexColors(new Color(0, 0, 0, 1));
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Clear All (White)", GUILayout.Height(25)))
            {
                FillVertexColors(Color.white);
            }
            
            if (GUILayout.Button("Randomize Colors", GUILayout.Height(25)))
            {
                RandomizeVertexColors();
            }
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Save Mesh as Asset", GUILayout.Height(30)))
            {
                SaveMeshAsAsset();
            }
        }
        
        void SetupMesh()
        {
            if (targetObject == null)
            {
                targetMeshFilter = null;
                editableMesh = null;
                return;
            }
            
            targetMeshFilter = targetObject.GetComponent<MeshFilter>();
            
            if (targetMeshFilter == null || targetMeshFilter.sharedMesh == null)
            {
                editableMesh = null;
                return;
            }
            
            editableMesh = Instantiate(targetMeshFilter.sharedMesh);
            
            if (editableMesh.colors == null || editableMesh.colors.Length != editableMesh.vertexCount)
            {
                Color[] colors = new Color[editableMesh.vertexCount];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = Color.white;
                }
                editableMesh.colors = colors;
            }
            
            targetMeshFilter.sharedMesh = editableMesh;
            
            EditorUtility.SetDirty(targetObject);
        }
        
        void OnSceneGUI(SceneView sceneView)
        {
            if (targetObject == null || editableMesh == null || targetMeshFilter == null)
                return;
            
            Event e = Event.current;
            
            if (e.shift && e.type == EventType.MouseDown && e.button == 0)
            {
                isPainting = true;
                PaintAtMousePosition(e);
                e.Use();
            }
            
            if (e.type == EventType.MouseUp)
            {
                isPainting = false;
            }
            
            if (isPainting && e.type == EventType.MouseDrag && e.shift)
            {
                PaintAtMousePosition(e);
                e.Use();
            }
            
            if (showVertexColors)
            {
                DrawVertexColorGizmos();
            }
            
            DrawBrushPreview(e);
            
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 300, 100));
            GUILayout.Label($"Vertex Painter: {paintChannel}", EditorStyles.whiteLargeLabel);
            GUILayout.Label($"Brush: {brushSize:F1}m | Strength: {brushStrength:F2}", EditorStyles.whiteLabel);
            GUILayout.Label("Hold SHIFT + Click to paint", EditorStyles.whiteLabel);
            GUILayout.EndArea();
            Handles.EndGUI();
            
            sceneView.Repaint();
        }
        
        void PaintAtMousePosition(Event e)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                if (hit.collider.gameObject == targetObject)
                {
                    PaintAtPoint(hit.point);
                }
            }
        }
        
        void PaintAtPoint(Vector3 worldPoint)
        {
            Vector3 localPoint = targetObject.transform.InverseTransformPoint(worldPoint);
            
            Vector3[] vertices = editableMesh.vertices;
            Color[] colors = editableMesh.colors;
            
            bool modified = false;
            
            for (int i = 0; i < vertices.Length; i++)
            {
                float distance = Vector3.Distance(vertices[i], localPoint);
                
                if (distance <= brushSize)
                {
                    float influence = 1f - (distance / brushSize);
                    influence = Mathf.Pow(influence, 1f + brushFalloff * 3f);
                    influence *= brushStrength;
                    
                    Color currentColor = colors[i];
                    
                    switch (paintChannel)
                    {
                        case PaintChannel.Red:
                            currentColor.r = Mathf.Lerp(currentColor.r, 1f, influence);
                            if (normalizeColors) currentColor = NormalizeColor(currentColor, 0);
                            break;
                        case PaintChannel.Green:
                            currentColor.g = Mathf.Lerp(currentColor.g, 1f, influence);
                            if (normalizeColors) currentColor = NormalizeColor(currentColor, 1);
                            break;
                        case PaintChannel.Blue:
                            currentColor.b = Mathf.Lerp(currentColor.b, 1f, influence);
                            if (normalizeColors) currentColor = NormalizeColor(currentColor, 2);
                            break;
                        case PaintChannel.Alpha:
                            currentColor.a = Mathf.Lerp(currentColor.a, 1f, influence);
                            if (normalizeColors) currentColor = NormalizeColor(currentColor, 3);
                            break;
                        case PaintChannel.All:
                            currentColor = Color.Lerp(currentColor, Color.white, influence);
                            break;
                    }
                    
                    colors[i] = currentColor;
                    modified = true;
                }
            }
            
            if (modified)
            {
                editableMesh.colors = colors;
                EditorUtility.SetDirty(targetObject);
            }
        }
        
        Color NormalizeColor(Color color, int preserveChannel)
        {
            float[] channels = new float[] { color.r, color.g, color.b, color.a };
            
            float sum = color.r + color.g + color.b + color.a;
            
            if (sum > 1f)
            {
                float excess = sum - 1f;
                
                for (int i = 0; i < 4; i++)
                {
                    if (i != preserveChannel)
                    {
                        float reduction = (channels[i] / (sum - channels[preserveChannel])) * excess;
                        channels[i] = Mathf.Max(0, channels[i] - reduction);
                    }
                }
            }
            
            return new Color(channels[0], channels[1], channels[2], channels[3]);
        }
        
        void DrawBrushPreview(Event e)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                if (hit.collider.gameObject == targetObject)
                {
                    Handles.color = GetChannelColor();
                    Handles.DrawWireDisc(hit.point, hit.normal, brushSize);
                    Handles.color = new Color(GetChannelColor().r, GetChannelColor().g, GetChannelColor().b, 0.1f);
                    Handles.DrawSolidDisc(hit.point, hit.normal, brushSize);
                }
            }
        }
        
        void DrawVertexColorGizmos()
        {
            if (editableMesh == null) return;
            
            Vector3[] vertices = editableMesh.vertices;
            Color[] colors = editableMesh.colors;
            
            Matrix4x4 matrix = targetObject.transform.localToWorldMatrix;
            
            int step = Mathf.Max(1, vertices.Length / 500);
            
            for (int i = 0; i < vertices.Length; i += step)
            {
                Vector3 worldPos = matrix.MultiplyPoint3x4(vertices[i]);
                Handles.color = colors[i];
                Handles.SphereHandleCap(0, worldPos, Quaternion.identity, 0.2f, EventType.Repaint);
            }
        }
        
        Color GetChannelColor()
        {
            switch (paintChannel)
            {
                case PaintChannel.Red: return Color.red;
                case PaintChannel.Green: return Color.green;
                case PaintChannel.Blue: return Color.blue;
                case PaintChannel.Alpha: return Color.yellow;
                default: return Color.white;
            }
        }
        
        void FillVertexColors(Color color)
        {
            if (editableMesh == null) return;
            
            Color[] colors = new Color[editableMesh.vertexCount];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = color;
            }
            
            editableMesh.colors = colors;
            EditorUtility.SetDirty(targetObject);
        }
        
        void RandomizeVertexColors()
        {
            if (editableMesh == null) return;
            
            Color[] colors = new Color[editableMesh.vertexCount];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color(Random.value, Random.value, Random.value, Random.value);
                colors[i] = NormalizeColorSimple(colors[i]);
            }
            
            editableMesh.colors = colors;
            EditorUtility.SetDirty(targetObject);
        }
        
        Color NormalizeColorSimple(Color color)
        {
            float sum = color.r + color.g + color.b + color.a;
            if (sum == 0) return new Color(0.25f, 0.25f, 0.25f, 0.25f);
            return new Color(color.r / sum, color.g / sum, color.b / sum, color.a / sum);
        }
        
        void SaveMeshAsAsset()
        {
            if (editableMesh == null) return;
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Mesh Asset",
                $"{editableMesh.name}_Painted",
                "asset",
                "Save mesh with vertex colors"
            );
            
            if (string.IsNullOrEmpty(path)) return;
            
            Mesh meshToSave = Instantiate(editableMesh);
            
            AssetDatabase.CreateAsset(meshToSave, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"Mesh saved to: {path}");
            EditorUtility.DisplayDialog("Success", $"Mesh saved to:\n{path}", "OK");
        }
    }
}
