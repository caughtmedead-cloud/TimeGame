using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Thelos.Editor
{
    public class MeshAtlasCombinerTool : EditorWindow
    {
        private GameObject sourceMeshObject;
        private string outputMeshName = "CombinedMesh_Atlas";
        private int atlasSize = 2048;
        private int atlasPadding = 2;
        private bool generateNormalMap = true;
        
        private Vector2 scrollPosition;
        
        [MenuItem("Tools/Mesh Atlas Combiner")]
        public static void ShowWindow()
        {
            GetWindow<MeshAtlasCombinerTool>("Mesh Atlas Combiner");
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("Advanced Mesh + Texture Atlas Combiner", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This tool combines multi-material meshes into:\n" +
                "• Single mesh with remapped UVs\n" +
                "• Combined Base Color texture atlas\n" +
                "• Combined Normal Map texture atlas\n\n" +
                "Perfect for SplineMesh with preserved visual quality!",
                MessageType.Info
            );

            EditorGUILayout.Space();

            sourceMeshObject = EditorGUILayout.ObjectField(
                "Source Mesh Object",
                sourceMeshObject,
                typeof(GameObject),
                false
            ) as GameObject;

            EditorGUILayout.Space();

            outputMeshName = EditorGUILayout.TextField("Output Name", outputMeshName);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Atlas Settings:", EditorStyles.boldLabel);
            
            atlasSize = EditorGUILayout.IntPopup("Atlas Size", atlasSize, 
                new string[] { "512", "1024", "2048", "4096" },
                new int[] { 512, 1024, 2048, 4096 });
            
            atlasPadding = EditorGUILayout.IntSlider("Padding (pixels)", atlasPadding, 0, 16);
            generateNormalMap = EditorGUILayout.Toggle("Generate Normal Atlas", generateNormalMap);

            EditorGUILayout.Space();

            GUI.enabled = sourceMeshObject != null && !string.IsNullOrEmpty(outputMeshName);

            if (GUILayout.Button("Combine Mesh + Create Atlases", GUILayout.Height(40)))
            {
                CombineMeshWithAtlas();
            }

            GUI.enabled = true;

            EditorGUILayout.Space();

            if (sourceMeshObject != null)
            {
                DisplayMeshInfo();
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DisplayMeshInfo()
        {
            MeshFilter meshFilter = sourceMeshObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = sourceMeshObject.GetComponent<MeshRenderer>();
            
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                EditorGUILayout.HelpBox("No MeshFilter or Mesh found on the selected GameObject.", MessageType.Warning);
                return;
            }

            if (meshRenderer == null || meshRenderer.sharedMaterials == null || meshRenderer.sharedMaterials.Length == 0)
            {
                EditorGUILayout.HelpBox("No MeshRenderer or Materials found on the selected GameObject.", MessageType.Warning);
                return;
            }

            Mesh mesh = meshFilter.sharedMesh;
            Material[] materials = meshRenderer.sharedMaterials;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Source Mesh Info:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Vertices: {mesh.vertexCount}");
            EditorGUILayout.LabelField($"Triangles: {mesh.triangles.Length / 3}");
            EditorGUILayout.LabelField($"Submeshes: {mesh.subMeshCount}");
            EditorGUILayout.LabelField($"Materials: {materials.Length}");

            if (materials.Length > 1)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Materials to Combine:", EditorStyles.boldLabel);
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        EditorGUILayout.LabelField($"  {i}: {materials[i].name}");
                        
                        Texture baseColor = GetMainTexture(materials[i]);
                        Texture normalMap = GetNormalTexture(materials[i]);
                        
                        if (baseColor != null)
                            EditorGUILayout.LabelField($"     Base: {baseColor.name} ({baseColor.width}x{baseColor.height})");
                        else
                            EditorGUILayout.LabelField("     Base: None (will use solid color)");
                            
                        if (normalMap != null)
                            EditorGUILayout.LabelField($"     Normal: {normalMap.name} ({normalMap.width}x{normalMap.height})");
                    }
                }
            }
        }

        private void CombineMeshWithAtlas()
        {
            MeshFilter meshFilter = sourceMeshObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = sourceMeshObject.GetComponent<MeshRenderer>();
            
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                EditorUtility.DisplayDialog("Error", "No MeshFilter or Mesh found on the selected GameObject.", "OK");
                return;
            }

            if (meshRenderer == null || meshRenderer.sharedMaterials == null || meshRenderer.sharedMaterials.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "No MeshRenderer or Materials found on the selected GameObject.", "OK");
                return;
            }

            Mesh sourceMesh = meshFilter.sharedMesh;
            
            if (!sourceMesh.isReadable)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    "Source mesh is not readable. Please enable 'Read/Write' in the mesh import settings.",
                    "OK"
                );
                return;
            }

            Material[] materials = meshRenderer.sharedMaterials;

            EditorUtility.DisplayProgressBar("Combining Mesh", "Extracting textures...", 0.1f);

            List<Texture2D> baseColorTextures = new List<Texture2D>();
            List<Texture2D> normalTextures = new List<Texture2D>();
            List<Color> fallbackColors = new List<Color>();

            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];
                
                Texture2D baseColor = GetReadableTexture(GetMainTexture(mat));
                Texture2D normal = generateNormalMap ? GetReadableTexture(GetNormalTexture(mat)) : null;
                
                if (baseColor == null)
                {
                    Color color = GetMainColor(mat);
                    baseColor = CreateSolidColorTexture(256, 256, color);
                    fallbackColors.Add(color);
                }
                else
                {
                    fallbackColors.Add(Color.white);
                }
                
                baseColorTextures.Add(baseColor);
                
                if (generateNormalMap)
                {
                    if (normal == null)
                    {
                        normal = CreateNormalTexture(256, 256);
                    }
                    normalTextures.Add(normal);
                }
            }

            EditorUtility.DisplayProgressBar("Combining Mesh", "Creating texture atlas...", 0.3f);

            Texture2D baseColorAtlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true);
            Rect[] uvRects = baseColorAtlas.PackTextures(baseColorTextures.ToArray(), atlasPadding, atlasSize, false);

            Texture2D normalAtlas = null;
            if (generateNormalMap && normalTextures.Count > 0)
            {
                normalAtlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, true);
                normalAtlas.PackTextures(normalTextures.ToArray(), atlasPadding, atlasSize, false);
            }

            EditorUtility.DisplayProgressBar("Combining Mesh", "Remapping UVs...", 0.6f);

            Mesh combinedMesh = CreateCombinedMeshWithRemappedUVs(sourceMesh, uvRects);
            combinedMesh.name = outputMeshName;

            EditorUtility.DisplayProgressBar("Combining Mesh", "Saving assets...", 0.8f);

            string folderPath = EditorUtility.SaveFolderPanel(
                "Save Combined Assets",
                "Assets/Models",
                ""
            );

            if (string.IsNullOrEmpty(folderPath))
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            if (!folderPath.StartsWith(Application.dataPath))
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", "Please select a folder inside the Assets directory.", "OK");
                return;
            }

            string relativePath = "Assets" + folderPath.Substring(Application.dataPath.Length);

            string meshPath = $"{relativePath}/{outputMeshName}.asset";
            string baseColorPath = $"{relativePath}/{outputMeshName}_BaseColor.png";
            string normalPath = $"{relativePath}/{outputMeshName}_Normal.png";

            AssetDatabase.CreateAsset(combinedMesh, meshPath);

            byte[] baseColorBytes = baseColorAtlas.EncodeToPNG();
            System.IO.File.WriteAllBytes(baseColorPath, baseColorBytes);

            if (normalAtlas != null)
            {
                byte[] normalBytes = normalAtlas.EncodeToPNG();
                System.IO.File.WriteAllBytes(normalPath, normalBytes);
            }

            AssetDatabase.Refresh();

            SetTextureImportSettings(baseColorPath, false);
            
            if (normalAtlas != null)
            {
                SetTextureImportSettings(normalPath, true);
            }

            EditorUtility.ClearProgressBar();

            string successMessage = $"Successfully created:\n\n" +
                $"Mesh: {meshPath}\n" +
                $"Base Color Atlas: {baseColorPath}\n";
            
            if (normalAtlas != null)
            {
                successMessage += $"Normal Atlas: {normalPath}\n";
            }
            
            successMessage += $"\nVertices: {combinedMesh.vertexCount}\n" +
                $"Triangles: {combinedMesh.triangles.Length / 3}\n\n" +
                "Now create a material and assign these textures!";

            EditorUtility.DisplayDialog("Success", successMessage, "OK");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        private Mesh CreateCombinedMeshWithRemappedUVs(Mesh sourceMesh, Rect[] uvRects)
        {
            Mesh newMesh = new Mesh();
            
            Vector3[] originalVertices = sourceMesh.vertices;
            Vector3[] originalNormals = sourceMesh.normals;
            Vector4[] originalTangents = sourceMesh.tangents;
            Vector2[] originalUVs = sourceMesh.uv;
            Color[] originalColors = sourceMesh.colors;

            List<Vector3> newVertices = new List<Vector3>();
            List<Vector3> newNormals = new List<Vector3>();
            List<Vector4> newTangents = new List<Vector4>();
            List<Vector2> newUVs = new List<Vector2>();
            List<Color> newColors = new List<Color>();
            List<int> newTriangles = new List<int>();

            Dictionary<int, int>[] vertexMapping = new Dictionary<int, int>[sourceMesh.subMeshCount];
            for (int i = 0; i < vertexMapping.Length; i++)
            {
                vertexMapping[i] = new Dictionary<int, int>();
            }

            for (int submeshIndex = 0; submeshIndex < sourceMesh.subMeshCount; submeshIndex++)
            {
                int[] submeshTriangles = sourceMesh.GetTriangles(submeshIndex);
                Rect uvRect = uvRects[submeshIndex];

                for (int i = 0; i < submeshTriangles.Length; i++)
                {
                    int oldVertexIndex = submeshTriangles[i];
                    
                    if (!vertexMapping[submeshIndex].ContainsKey(oldVertexIndex))
                    {
                        int newVertexIndex = newVertices.Count;
                        vertexMapping[submeshIndex][oldVertexIndex] = newVertexIndex;
                        
                        newVertices.Add(originalVertices[oldVertexIndex]);
                        
                        if (originalNormals != null && originalNormals.Length > oldVertexIndex)
                            newNormals.Add(originalNormals[oldVertexIndex]);
                        
                        if (originalTangents != null && originalTangents.Length > oldVertexIndex)
                            newTangents.Add(originalTangents[oldVertexIndex]);
                        
                        if (originalColors != null && originalColors.Length > oldVertexIndex)
                            newColors.Add(originalColors[oldVertexIndex]);
                        
                        Vector2 originalUV = originalUVs[oldVertexIndex];
                        Vector2 remappedUV = new Vector2(
                            uvRect.x + originalUV.x * uvRect.width,
                            uvRect.y + originalUV.y * uvRect.height
                        );
                        newUVs.Add(remappedUV);
                    }
                    
                    newTriangles.Add(vertexMapping[submeshIndex][oldVertexIndex]);
                }
            }

            newMesh.vertices = newVertices.ToArray();
            
            if (newNormals.Count > 0)
                newMesh.normals = newNormals.ToArray();
            
            if (newTangents.Count > 0)
                newMesh.tangents = newTangents.ToArray();
            
            if (newColors.Count > 0)
                newMesh.colors = newColors.ToArray();
            
            newMesh.uv = newUVs.ToArray();
            newMesh.triangles = newTriangles.ToArray();

            newMesh.RecalculateBounds();
            
            if (newNormals.Count == 0)
                newMesh.RecalculateNormals();
            
            if (newTangents.Count == 0)
                newMesh.RecalculateTangents();

            return newMesh;
        }

        private Texture GetMainTexture(Material material)
        {
            if (material == null) return null;

            string[] mainTexNames = { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo" };
            
            foreach (string texName in mainTexNames)
            {
                if (material.HasProperty(texName))
                {
                    Texture tex = material.GetTexture(texName);
                    if (tex != null) return tex;
                }
            }
            
            return null;
        }

        private Texture GetNormalTexture(Material material)
        {
            if (material == null) return null;

            string[] normalTexNames = { "_BumpMap", "_NormalMap", "_Normal" };
            
            foreach (string texName in normalTexNames)
            {
                if (material.HasProperty(texName))
                {
                    Texture tex = material.GetTexture(texName);
                    if (tex != null) return tex;
                }
            }
            
            return null;
        }

        private Color GetMainColor(Material material)
        {
            if (material == null) return Color.white;

            string[] colorNames = { "_Color", "_BaseColor", "_Albedo" };
            
            foreach (string colorName in colorNames)
            {
                if (material.HasProperty(colorName))
                {
                    return material.GetColor(colorName);
                }
            }
            
            return Color.white;
        }

        private Texture2D GetReadableTexture(Texture texture)
        {
            if (texture == null) return null;

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath)) return null;

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            Texture2D tex2D = texture as Texture2D;
            if (tex2D == null)
            {
                RenderTexture tmp = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(texture, tmp);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = tmp;
                
                tex2D = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                tex2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
                tex2D.Apply();
                
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(tmp);
            }

            return tex2D;
        }

        private Texture2D CreateSolidColorTexture(int width, int height, Color color)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private Texture2D CreateNormalTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color normalColor = new Color(0.5f, 0.5f, 1f, 1f);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = normalColor;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void SetTextureImportSettings(string path, bool isNormalMap)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.isReadable = true;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = atlasSize;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
