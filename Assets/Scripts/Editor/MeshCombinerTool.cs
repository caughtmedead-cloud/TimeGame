using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Thelos.Editor
{
    public class MeshCombinerTool : EditorWindow
    {
        private GameObject sourceMeshObject;
        private Material targetMaterial;
        private string outputMeshName = "CombinedMesh";
        private bool includeUVs = true;
        private bool includeNormals = true;
        private bool includeTangents = true;
        
        [MenuItem("Tools/Mesh Combiner")]
        public static void ShowWindow()
        {
            GetWindow<MeshCombinerTool>("Mesh Combiner");
        }

        private void OnGUI()
        {
            GUILayout.Label("Combine Multi-Material Mesh", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This tool combines all submeshes from a multi-material mesh into a single mesh with one material.\n\n" +
                "Perfect for use with SplineMesh system!",
                MessageType.Info
            );

            EditorGUILayout.Space();

            sourceMeshObject = EditorGUILayout.ObjectField(
                "Source Mesh Object",
                sourceMeshObject,
                typeof(GameObject),
                false
            ) as GameObject;

            targetMaterial = EditorGUILayout.ObjectField(
                "Target Material",
                targetMaterial,
                typeof(Material),
                false
            ) as Material;

            EditorGUILayout.Space();

            outputMeshName = EditorGUILayout.TextField("Output Mesh Name", outputMeshName);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Include Mesh Data:", EditorStyles.boldLabel);
            includeUVs = EditorGUILayout.Toggle("UVs", includeUVs);
            includeNormals = EditorGUILayout.Toggle("Normals", includeNormals);
            includeTangents = EditorGUILayout.Toggle("Tangents", includeTangents);

            EditorGUILayout.Space();

            GUI.enabled = sourceMeshObject != null && targetMaterial != null && !string.IsNullOrEmpty(outputMeshName);

            if (GUILayout.Button("Combine Mesh", GUILayout.Height(40)))
            {
                CombineMesh();
            }

            GUI.enabled = true;

            EditorGUILayout.Space();

            if (sourceMeshObject != null)
            {
                DisplayMeshInfo();
            }
        }

        private void DisplayMeshInfo()
        {
            MeshFilter meshFilter = sourceMeshObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                EditorGUILayout.HelpBox("No MeshFilter or Mesh found on the selected GameObject.", MessageType.Warning);
                return;
            }

            Mesh mesh = meshFilter.sharedMesh;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Source Mesh Info:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Vertices: {mesh.vertexCount}");
            EditorGUILayout.LabelField($"Triangles: {mesh.triangles.Length / 3}");
            EditorGUILayout.LabelField($"Submeshes: {mesh.subMeshCount}");

            if (mesh.subMeshCount > 1)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Submesh Details:", EditorStyles.boldLabel);
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    int triangleCount = mesh.GetTriangles(i).Length / 3;
                    EditorGUILayout.LabelField($"  Submesh {i}: {triangleCount} triangles");
                }
            }
        }

        private void CombineMesh()
        {
            MeshFilter meshFilter = sourceMeshObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                EditorUtility.DisplayDialog("Error", "No MeshFilter or Mesh found on the selected GameObject.", "OK");
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

            Mesh combinedMesh = CombineSubmeshes(sourceMesh);
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Combined Mesh",
                outputMeshName,
                "asset",
                "Choose where to save the combined mesh",
                "Assets/Models"
            );

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            AssetDatabase.CreateAsset(combinedMesh, path);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Success",
                $"Combined mesh saved to:\n{path}\n\nVertices: {combinedMesh.vertexCount}\nTriangles: {combinedMesh.triangles.Length / 3}",
                "OK"
            );

            Selection.activeObject = combinedMesh;
            EditorGUIUtility.PingObject(combinedMesh);
        }

        private Mesh CombineSubmeshes(Mesh sourceMesh)
        {
            Mesh newMesh = new Mesh();
            newMesh.name = outputMeshName;

            Vector3[] vertices = sourceMesh.vertices;
            Vector3[] normals = includeNormals ? sourceMesh.normals : null;
            Vector4[] tangents = includeTangents ? sourceMesh.tangents : null;
            Vector2[] uv = includeUVs ? sourceMesh.uv : null;
            Vector2[] uv2 = includeUVs ? sourceMesh.uv2 : null;
            Color[] colors = sourceMesh.colors;

            List<int> allTriangles = new List<int>();

            for (int i = 0; i < sourceMesh.subMeshCount; i++)
            {
                int[] submeshTriangles = sourceMesh.GetTriangles(i);
                allTriangles.AddRange(submeshTriangles);
            }

            newMesh.vertices = vertices;
            
            if (normals != null && normals.Length > 0)
            {
                newMesh.normals = normals;
            }
            
            if (tangents != null && tangents.Length > 0)
            {
                newMesh.tangents = tangents;
            }
            
            if (uv != null && uv.Length > 0)
            {
                newMesh.uv = uv;
            }
            
            if (uv2 != null && uv2.Length > 0)
            {
                newMesh.uv2 = uv2;
            }
            
            if (colors != null && colors.Length > 0)
            {
                newMesh.colors = colors;
            }

            newMesh.triangles = allTriangles.ToArray();

            newMesh.RecalculateBounds();
            
            if (normals == null || normals.Length == 0)
            {
                newMesh.RecalculateNormals();
            }
            
            if (tangents == null || tangents.Length == 0)
            {
                newMesh.RecalculateTangents();
            }

            return newMesh;
        }
    }
}
