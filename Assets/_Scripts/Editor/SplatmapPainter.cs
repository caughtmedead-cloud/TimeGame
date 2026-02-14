using UnityEngine;
using UnityEditor;

#if UNITY_PROBUILDER
using UnityEngine.ProBuilder;
#endif

namespace Thelos.Editor
{
    public class SplatmapPainter : EditorWindow
    {
        [MenuItem("Thelos/Splatmap Painter")]
        static void OpenWindow()
        {
            SplatmapPainter window = GetWindow<SplatmapPainter>("Splatmap Painter");
            window.minSize = new Vector2(320, 600);
            window.Show();
        }

        private GameObject targetObject;
        private MeshFilter targetMeshFilter;
        private MeshRenderer targetRenderer;
        private Material targetMaterial;
        
        private Texture2D splatmap;
        private string splatmapPropertyName = "_SplatMap";
        private int textureResolution = 2048;
        private string savePath = "Assets/_Art/Materials/Ground/";
        private string textureName = "TerrainSplatmap";
        
        private enum PaintChannel { Red, Green, Blue, Alpha }
        private PaintChannel paintChannel = PaintChannel.Red;
        
        private float brushSize = 10f;
        private float brushStrength = 0.5f;
        private float brushFalloff = 1f;
        
        private bool isPainting = false;
        private bool normalizeChannels = true;
        private bool needsSave = false;
        
        private Color[] splatmapPixels;
        private Vector2 scrollPosition;

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            
            if (needsSave)
            {
                if (EditorUtility.DisplayDialog("Unsaved Changes", 
                    "You have unsaved changes to the splatmap. Save before closing?", 
                    "Save", "Discard"))
                {
                    SaveSplatmap();
                }
            }
        }

        void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("Splatmap Painter", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            DrawSetupSection();
            EditorGUILayout.Space();
            DrawBrushSettings();
            EditorGUILayout.Space();
            DrawTextureInfo();
            EditorGUILayout.Space();
            DrawInstructions();
            
            EditorGUILayout.EndScrollView();
        }

        void DrawSetupSection()
        {
            GUILayout.Label("Setup", EditorStyles.boldLabel);
            
            GameObject newTarget = EditorGUILayout.ObjectField("Target Mesh", targetObject, typeof(GameObject), true) as GameObject;
            if (newTarget != targetObject)
            {
                targetObject = newTarget;
                UpdateTarget();
            }
            
            EditorGUI.BeginDisabledGroup(targetObject == null);
            
            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                Mesh mesh = targetMeshFilter.sharedMesh;
                bool hasUV2 = mesh.uv2 != null && mesh.uv2.Length > 0;
                
                string uvInfo = hasUV2 
                    ? $"✓ Mesh has UV2 ({mesh.uv2.Length} verts)" 
                    : "✗ Mesh missing UV2! Click below to generate.";
                
                EditorGUILayout.HelpBox(uvInfo, hasUV2 ? MessageType.Info : MessageType.Warning);
                
                if (hasUV2)
                {
                    EditorGUILayout.HelpBox(
                        "IMPORTANT: Your shader must use UV2 (TEXCOORD1) for the splatmap!\n" +
                        "UV1 is for tiled detail textures, UV2 is for the splatmap.",
                        MessageType.Info
                    );
                }
                
                if (!hasUV2)
                {
                    if (GUILayout.Button("Generate UV2 for Splatmap", GUILayout.Height(30)))
                    {
                        GenerateUV2ForMesh();
                    }
                }
            }
            
            EditorGUI.BeginDisabledGroup(targetObject == null);
            
            splatmapPropertyName = EditorGUILayout.TextField("Splatmap Property", splatmapPropertyName);
            
            EditorGUILayout.BeginHorizontal();
            textureResolution = EditorGUILayout.IntField("Resolution", textureResolution);
            if (GUILayout.Button("New", GUILayout.Width(60)))
            {
                CreateNewSplatmap();
            }
            EditorGUILayout.EndHorizontal();
            
            savePath = EditorGUILayout.TextField("Save Path", savePath);
            textureName = EditorGUILayout.TextField("Texture Name", textureName);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load from Material", GUILayout.Height(30)))
            {
                LoadSplatmapFromMaterial();
            }
            if (GUILayout.Button("Save Splatmap", GUILayout.Height(30)))
            {
                SaveSplatmap();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.EndDisabledGroup();
        }

        void DrawBrushSettings()
        {
            GUILayout.Label("Brush Settings", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(splatmap == null);
            
            paintChannel = (PaintChannel)EditorGUILayout.EnumPopup("Paint Channel", paintChannel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Channel:", GUILayout.Width(60));
            if (GUILayout.Toggle(paintChannel == PaintChannel.Red, "Red (Tex 1)", EditorStyles.miniButtonLeft))
                paintChannel = PaintChannel.Red;
            if (GUILayout.Toggle(paintChannel == PaintChannel.Green, "Green (Tex 2)", EditorStyles.miniButtonMid))
                paintChannel = PaintChannel.Green;
            if (GUILayout.Toggle(paintChannel == PaintChannel.Blue, "Blue (Tex 3)", EditorStyles.miniButtonMid))
                paintChannel = PaintChannel.Blue;
            if (GUILayout.Toggle(paintChannel == PaintChannel.Alpha, "Alpha (Tex 4)", EditorStyles.miniButtonRight))
                paintChannel = PaintChannel.Alpha;
            EditorGUILayout.EndHorizontal();
            
            brushSize = EditorGUILayout.Slider("Brush Size", brushSize, 0.1f, 50f);
            brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0f, 1f);
            brushFalloff = EditorGUILayout.Slider("Brush Falloff", brushFalloff, 0.1f, 5f);
            
            normalizeChannels = EditorGUILayout.Toggle("Normalize Channels", normalizeChannels);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Channel"))
            {
                ClearChannel(paintChannel);
            }
            if (GUILayout.Button("Fill Channel"))
            {
                FillChannel(paintChannel);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.EndDisabledGroup();
        }

        void DrawTextureInfo()
        {
            GUILayout.Label("Texture Info", EditorStyles.boldLabel);
            
            if (splatmap != null)
            {
                EditorGUILayout.LabelField("Status:", "Loaded");
                EditorGUILayout.LabelField("Resolution:", $"{splatmap.width}x{splatmap.height}");
                EditorGUILayout.LabelField("Format:", splatmap.format.ToString());
                
                if (needsSave)
                {
                    EditorGUILayout.HelpBox("Texture has unsaved changes!", MessageType.Warning);
                }
                
                Rect previewRect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true));
                EditorGUI.DrawPreviewTexture(previewRect, splatmap);
            }
            else
            {
                EditorGUILayout.HelpBox("No splatmap loaded. Create new or load from material.", MessageType.Info);
            }
        }

        void DrawInstructions()
        {
            GUILayout.Label("Instructions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Select your terrain mesh\n" +
                "2. Create new splatmap or load existing\n" +
                "3. Select paint channel (R/G/B/A)\n" +
                "4. Hold SHIFT + Left Click to paint in Scene view\n" +
                "5. Save when done\n\n" +
                "Each channel controls one texture:\n" +
                "Red = Texture 1, Green = Texture 2\n" +
                "Blue = Texture 3, Alpha = Texture 4",
                MessageType.Info
            );
        }

        void OnSceneGUI(SceneView sceneView)
        {
            if (targetObject == null || splatmap == null)
                return;

            Event e = Event.current;
            
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;
            bool hitTarget = false;
            
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                if (hit.collider != null && hit.collider.gameObject == targetObject)
                {
                    hitTarget = true;
                    
                    Handles.color = GetChannelColor();
                    Handles.DrawWireDisc(hit.point, hit.normal, brushSize);
                    Handles.color = new Color(GetChannelColor().r, GetChannelColor().g, GetChannelColor().b, 0.1f);
                    Handles.DrawSolidDisc(hit.point, hit.normal, brushSize);
                }
            }
            
            if (e.shift && e.type == EventType.MouseDown && e.button == 0)
            {
                isPainting = true;
                if (hitTarget)
                {
                    PaintAtPosition(hit);
                    e.Use();
                }
            }
            
            if (e.type == EventType.MouseUp && e.button == 0)
            {
                isPainting = false;
            }
            
            if (isPainting && e.shift && hitTarget)
            {
                if (e.type == EventType.MouseDrag)
                {
                    PaintAtPosition(hit);
                    e.Use();
                }
            }
            
            if (e.shift && hitTarget)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                SceneView.RepaintAll();
            }
            
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 280, 120));
            GUILayout.BeginVertical("box");
            GUILayout.Label("Splatmap Painter Active", EditorStyles.boldLabel);
            GUILayout.Label($"Channel: {paintChannel}");
            GUILayout.Label($"Brush Size: {brushSize:F1}");
            GUILayout.Label($"Collider Hit: {(hitTarget ? "YES" : "NO")}");
            GUILayout.Label("Hold SHIFT + Left Click to paint");
            GUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();
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

        void UpdateTarget()
        {
            if (targetObject != null)
            {
                targetMeshFilter = targetObject.GetComponent<MeshFilter>();
                targetRenderer = targetObject.GetComponent<MeshRenderer>();
                
                if (targetRenderer != null)
                {
                    targetMaterial = targetRenderer.sharedMaterial;
                }
            }
        }

        void CreateNewSplatmap()
        {
            string fullPath = savePath + textureName + ".png";
            
            if (System.IO.File.Exists(fullPath))
            {
                if (!EditorUtility.DisplayDialog("Overwrite?", 
                    $"Texture already exists at:\n{fullPath}\n\nOverwrite?", "Yes", "Cancel"))
                {
                    return;
                }
            }
            
            Texture2D newTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false, true);
            
            Color[] pixels = new Color[textureResolution * textureResolution];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(1f, 0f, 0f, 0f);
            }
            
            newTexture.SetPixels(pixels);
            newTexture.Apply();
            
            byte[] pngData = newTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(fullPath, pngData);
            AssetDatabase.Refresh();
            
            TextureImporter importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
            if (importer != null)
            {
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = true;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            
            splatmap = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            splatmapPixels = splatmap.GetPixels();
            needsSave = false;
            
            if (targetMaterial != null && targetMaterial.HasProperty(splatmapPropertyName))
            {
                targetMaterial.SetTexture(splatmapPropertyName, splatmap);
                EditorUtility.SetDirty(targetMaterial);
            }
            
            Debug.Log($"Created and saved new {textureResolution}x{textureResolution} splatmap at {fullPath}");
        }

        void LoadSplatmapFromMaterial()
        {
            if (targetMaterial == null)
            {
                EditorUtility.DisplayDialog("Error", "No material found on target object.", "OK");
                return;
            }
            
            if (!targetMaterial.HasProperty(splatmapPropertyName))
            {
                EditorUtility.DisplayDialog("Error", 
                    $"Material does not have property '{splatmapPropertyName}'.", "OK");
                return;
            }
            
            Texture loadedTexture = targetMaterial.GetTexture(splatmapPropertyName);
            
            if (loadedTexture == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    $"No texture assigned to '{splatmapPropertyName}' on material.", "OK");
                return;
            }
            
            string path = AssetDatabase.GetAssetPath(loadedTexture);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            
            splatmap = loadedTexture as Texture2D;
            splatmapPixels = splatmap.GetPixels();
            needsSave = false;
            
            Debug.Log($"Loaded splatmap from material: {loadedTexture.name} at {path}");
        }

        Texture2D DuplicateTexture(Texture2D source)
        {
            if (source == null)
                return null;
            
            RenderTexture renderTex = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear
            );

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            
            Texture2D readableTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            readableTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableTexture.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            
            return readableTexture;
        }

        void SaveSplatmap()
        {
            if (splatmap == null)
            {
                EditorUtility.DisplayDialog("Error", "No splatmap to save.", "OK");
                return;
            }
            
            string fullPath = savePath + textureName + ".png";
            byte[] pngData = splatmap.EncodeToPNG();
            System.IO.File.WriteAllBytes(fullPath, pngData);
            
            AssetDatabase.Refresh();
            
            TextureImporter importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
            if (importer != null)
            {
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = true;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            
            needsSave = false;
            
            Texture2D savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            if (targetMaterial != null && targetMaterial.HasProperty(splatmapPropertyName))
            {
                targetMaterial.SetTexture(splatmapPropertyName, savedTexture);
            }
            
            EditorUtility.DisplayDialog("Success", $"Splatmap saved to:\n{fullPath}", "OK");
            Selection.activeObject = savedTexture;
        }

        void PaintAtPosition(RaycastHit hit)
        {
            if (splatmap == null || targetMeshFilter == null)
            {
                Debug.LogWarning("Splatmap or MeshFilter is null!");
                return;
            }
            
            if (splatmapPixels == null || splatmapPixels.Length != splatmap.width * splatmap.height)
            {
                Debug.Log("Refreshing splatmap pixel cache...");
                splatmapPixels = splatmap.GetPixels();
            }
            
            Mesh mesh = targetMeshFilter.sharedMesh;
            if (mesh == null)
            {
                Debug.LogError("Mesh is null!");
                return;
            }
            
            if (mesh.uv == null || mesh.uv.Length == 0)
            {
                Debug.LogError("Mesh has no UV coordinates!");
                return;
            }
            
            Vector2 uv = GetUVAtHitPoint(hit, mesh);
            
            Debug.Log($"Painting at UV: {uv}, Pixel: ({Mathf.RoundToInt(uv.x * (splatmap.width - 1))}, {Mathf.RoundToInt(uv.y * (splatmap.height - 1))}) Channel: {paintChannel}");
            
            int centerX = Mathf.RoundToInt(uv.x * (splatmap.width - 1));
            int centerY = Mathf.RoundToInt(uv.y * (splatmap.height - 1));
            
            int brushRadiusPixels = Mathf.Max(1, Mathf.RoundToInt(brushSize * 10));
            
            for (int y = -brushRadiusPixels; y <= brushRadiusPixels; y++)
            {
                for (int x = -brushRadiusPixels; x <= brushRadiusPixels; x++)
                {
                    int pixelX = centerX + x;
                    int pixelY = centerY + y;
                    
                    if (pixelX < 0 || pixelX >= splatmap.width || pixelY < 0 || pixelY >= splatmap.height)
                        continue;
                    
                    float distance = Mathf.Sqrt(x * x + y * y);
                    if (distance > brushRadiusPixels)
                        continue;
                    
                    float falloff = Mathf.Pow(1f - (distance / brushRadiusPixels), brushFalloff);
                    float strength = brushStrength * falloff;
                    
                    int pixelIndex = pixelY * splatmap.width + pixelX;
                    Color currentColor = splatmapPixels[pixelIndex];
                    
                    switch (paintChannel)
                    {
                        case PaintChannel.Red:
                            currentColor.r = Mathf.Clamp01(currentColor.r + strength);
                            break;
                        case PaintChannel.Green:
                            currentColor.g = Mathf.Clamp01(currentColor.g + strength);
                            break;
                        case PaintChannel.Blue:
                            currentColor.b = Mathf.Clamp01(currentColor.b + strength);
                            break;
                        case PaintChannel.Alpha:
                            currentColor.a = Mathf.Clamp01(currentColor.a + strength);
                            break;
                    }
                    
                    if (normalizeChannels)
                    {
                        float sum = currentColor.r + currentColor.g + currentColor.b + currentColor.a;
                        if (sum > 0f)
                        {
                            currentColor.r /= sum;
                            currentColor.g /= sum;
                            currentColor.b /= sum;
                            currentColor.a /= sum;
                        }
                    }
                    
                    splatmapPixels[pixelIndex] = currentColor;
                }
            }
            
            splatmap.SetPixels(splatmapPixels);
            splatmap.Apply();
            
            needsSave = true;
            Repaint();
        }

        Vector2 GetUVAtHitPoint(RaycastHit hit, Mesh mesh)
        {
            int[] triangles = mesh.triangles;
            Vector2[] uvs = mesh.uv2 != null && mesh.uv2.Length > 0 ? mesh.uv2 : mesh.uv;
            
            if (uvs == null || uvs.Length == 0)
            {
                Debug.LogError("Mesh has no UV coordinates!");
                return Vector2.zero;
            }
            
            int triIndex = hit.triangleIndex * 3;
            Vector2 uv0 = uvs[triangles[triIndex]];
            Vector2 uv1 = uvs[triangles[triIndex + 1]];
            Vector2 uv2 = uvs[triangles[triIndex + 2]];
            
            Vector3 bary = hit.barycentricCoordinate;
            Vector2 uv = uv0 * bary.x + uv1 * bary.y + uv2 * bary.z;
            
            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);
            
            return uv;
        }

        void ClearChannel(PaintChannel channel)
        {
            if (splatmap == null)
                return;
            
            for (int i = 0; i < splatmapPixels.Length; i++)
            {
                Color c = splatmapPixels[i];
                switch (channel)
                {
                    case PaintChannel.Red: c.r = 0f; break;
                    case PaintChannel.Green: c.g = 0f; break;
                    case PaintChannel.Blue: c.b = 0f; break;
                    case PaintChannel.Alpha: c.a = 0f; break;
                }
                splatmapPixels[i] = c;
            }
            
            splatmap.SetPixels(splatmapPixels);
            splatmap.Apply();
            needsSave = true;
            Repaint();
        }

        void FillChannel(PaintChannel channel)
        {
            if (splatmap == null)
                return;
            
            for (int i = 0; i < splatmapPixels.Length; i++)
            {
                Color c = splatmapPixels[i];
                switch (channel)
                {
                    case PaintChannel.Red: c.r = 1f; break;
                    case PaintChannel.Green: c.g = 1f; break;
                    case PaintChannel.Blue: c.b = 1f; break;
                    case PaintChannel.Alpha: c.a = 1f; break;
                }
                
                if (normalizeChannels)
                {
                    float sum = c.r + c.g + c.b + c.a;
                    if (sum > 0f)
                    {
                        c.r /= sum;
                        c.g /= sum;
                        c.b /= sum;
                        c.a /= sum;
                    }
                }
                
                splatmapPixels[i] = c;
            }
            
            splatmap.SetPixels(splatmapPixels);
            splatmap.Apply();
            needsSave = true;
            Repaint();
        }
        
        void GenerateUV2ForMesh()
        {
            if (targetMeshFilter == null || targetMeshFilter.sharedMesh == null)
            {
                EditorUtility.DisplayDialog("Error", "No mesh found on target object.", "OK");
                return;
            }
            
            Mesh sourceMesh = targetMeshFilter.sharedMesh;
            string meshPath = AssetDatabase.GetAssetPath(sourceMesh);
            bool isProBuilderMesh = false;
            
            #if UNITY_PROBUILDER
            var proBuilderMesh = targetObject.GetComponent<ProBuilderMesh>();
            if (proBuilderMesh != null)
            {
                isProBuilderMesh = true;
                Debug.Log("Detected ProBuilder mesh. Will create a new mesh asset with UV2.");
            }
            #endif
            
            if (string.IsNullOrEmpty(meshPath) || isProBuilderMesh)
            {
                if (!isProBuilderMesh)
                {
                    EditorUtility.DisplayDialog("Error", 
                        "Cannot find mesh asset path. The mesh may be generated at runtime or is a ProBuilder mesh.", "OK");
                    return;
                }
                
                string newMeshPath = EditorUtility.SaveFilePanelInProject(
                    "Save Mesh with UV2",
                    targetObject.name + "_UV2",
                    "asset",
                    "Choose where to save the mesh with UV2 coordinates"
                );
                
                if (string.IsNullOrEmpty(newMeshPath))
                    return;
                
                #if UNITY_PROBUILDER
                proBuilderMesh.ToMesh();
                proBuilderMesh.Refresh();
                #endif
                
                Mesh newMesh = Mesh.Instantiate(sourceMesh) as Mesh;
                newMesh.name = targetObject.name + "_UV2";
                
                Unwrapping.GenerateSecondaryUVSet(newMesh);
                
                AssetDatabase.CreateAsset(newMesh, newMeshPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                targetMeshFilter.sharedMesh = newMesh;
                EditorUtility.SetDirty(targetMeshFilter);
                
                #if UNITY_PROBUILDER
                if (proBuilderMesh != null)
                {
                    if (EditorUtility.DisplayDialog("Remove ProBuilder Component?",
                        "The mesh now has UV2 coordinates saved as a separate asset.\n\n" +
                        "Do you want to remove the ProBuilder component? This will make the mesh static but you won't be able to edit it with ProBuilder anymore.\n\n" +
                        "If you keep ProBuilder, any further ProBuilder edits will overwrite the custom UV2.",
                        "Remove ProBuilder", "Keep ProBuilder"))
                    {
                        proBuilderMesh.preserveMeshAssetOnDestroy = true;
                        DestroyImmediate(proBuilderMesh);
                        Debug.Log("ProBuilder component removed. Mesh is now static.");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Warning",
                            "ProBuilder component kept. Be aware that editing the mesh with ProBuilder may overwrite the UV2 coordinates.\n\n" +
                            "If you need to edit with ProBuilder again, you'll need to regenerate UV2 afterwards.",
                            "OK");
                    }
                }
                #endif
                
                Debug.Log($"Generated UV2 for mesh and saved to: {newMeshPath}");
                EditorUtility.DisplayDialog("Success", 
                    $"UV2 generated successfully!\nNew mesh saved to:\n{newMeshPath}\n\nYou can now paint on the splatmap.", "OK");
                
                Repaint();
                return;
            }
            
            Mesh meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            
            if (meshAsset == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    $"Could not load mesh from path:\n{meshPath}", "OK");
                return;
            }
            
            if (!EditorUtility.DisplayDialog("Generate UV2?", 
                $"This will generate lightmap-style UVs in UV2 channel for:\n{meshAsset.name}\nat:\n{meshPath}\n\nContinue?", 
                "Yes", "Cancel"))
            {
                return;
            }
            
            Mesh newMesh2 = Mesh.Instantiate(meshAsset) as Mesh;
            newMesh2.name = meshAsset.name;
            
            Unwrapping.GenerateSecondaryUVSet(newMesh2);
            
            string directory = System.IO.Path.GetDirectoryName(meshPath);
            string filename = System.IO.Path.GetFileNameWithoutExtension(meshPath);
            string newPath = $"{directory}/{filename}_UV2.asset";
            
            AssetDatabase.CreateAsset(newMesh2, newPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            targetMeshFilter.sharedMesh = newMesh2;
            EditorUtility.SetDirty(targetMeshFilter);
            
            Debug.Log($"Generated UV2 for mesh and saved to: {newPath}");
            EditorUtility.DisplayDialog("Success", 
                $"UV2 generated successfully!\nNew mesh saved to:\n{newPath}\n\nYou can now paint on the splatmap.", "OK");
            
            Repaint();
        }
    }
}
