using UnityEditor;
using UnityEngine;
using System.IO;

public class ExportPrefabDependencies : EditorWindow
{
    private GameObject prefab;
    private string targetFolder = "Assets";

    [MenuItem("Tools/Export Prefab Dependencies")]
    static void Init()
    {
        GetWindow<ExportPrefabDependencies>("Export Prefab");
    }

    void OnGUI()
    {
        GUILayout.Label("Select Prefab & Target Folder", EditorStyles.boldLabel);

        prefab = (GameObject)EditorGUILayout.ObjectField(
            "Prefab",
            prefab,
            typeof(GameObject),
            false
        );

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Target Folder", targetFolder);
        if (GUILayout.Button("Browse"))
        {
            string newPath = EditorUtility.OpenFolderPanel(
                "Select Target Folder",
                Application.dataPath,
                ""
            );

            if (!string.IsNullOrEmpty(newPath))
            {
                if (newPath.StartsWith(Application.dataPath))
                    targetFolder = "Assets" + newPath.Substring(Application.dataPath.Length);
                else
                    EditorUtility.DisplayDialog(
                        "Invalid Folder",
                        "Folder must be inside the Assets directory.",
                        "OK"
                    );
            }
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (GUILayout.Button("Export Dependencies"))
        {
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a prefab.", "OK");
                return;
            }

            Export();
            AssetDatabase.Refresh();
        }
    }

    void Export()
    {
        string exportFolderName = prefab.name + "_Export";
        string exportPath = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(targetFolder, exportFolderName)
        );

        AssetDatabase.CreateFolder(targetFolder, exportFolderName);

        Object[] deps = EditorUtility.CollectDependencies(new Object[] { prefab });

        foreach (Object dep in deps)
        {
            string path = AssetDatabase.GetAssetPath(dep);
            if (string.IsNullOrEmpty(path))
                continue;

            string ext = Path.GetExtension(path).ToLower();

            if (ext == ".fbx" || ext == ".obj" ||
                ext == ".mat" ||
                ext == ".png" || ext == ".jpg" || ext == ".tga")
            {
                string destPath = Path.Combine(exportPath, Path.GetFileName(path));

                if (!AssetDatabase.CopyAsset(path, destPath))
                {
                    Debug.LogWarning($"Failed to copy {path}");
                }
            }
        }

        EditorUtility.DisplayDialog(
            "Export Complete",
            $"Prefab dependencies exported to:\n{exportPath}",
            "OK"
        );
    }
}
