using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(RingMeshGenerator))]
public class RingMeshGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RingMeshGenerator generator = (RingMeshGenerator)target;

        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("Generate Mesh Preview", GUILayout.Height(30)))
        {
            generator.GenerateMesh();
            EditorUtility.SetDirty(generator);
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Save Mesh as Asset", GUILayout.Height(30)))
        {
            SaveMeshAsAsset(generator);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "1. Generate Mesh Preview to see results in Scene view\n" +
            "2. Save Mesh as Asset to create a permanent mesh file\n" +
            "3. Saved meshes can be used with MeshColliders", 
            MessageType.Info
        );
    }

    private void SaveMeshAsAsset(RingMeshGenerator generator)
    {
        MeshFilter meshFilter = generator.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("Error", "Generate mesh first before saving!", "OK");
            return;
        }

        string directory = "Assets/_Art/Mesh";
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string fileName = $"RingMesh_{generator.InnerRadius}_{generator.OuterRadius}_{System.DateTime.Now.Ticks}.asset";
        string path = Path.Combine(directory, fileName);

        Mesh meshToSave = Instantiate(meshFilter.sharedMesh);
        AssetDatabase.CreateAsset(meshToSave, path);
        AssetDatabase.SaveAssets();

        meshFilter.sharedMesh = meshToSave;

        EditorUtility.DisplayDialog("Success", $"Mesh saved to:\n{path}", "OK");
        EditorGUIUtility.PingObject(meshToSave);
    }
}
