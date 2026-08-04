using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

public class ExportPrefabDependencies : EditorWindow
{
    private GameObject[] prefabs;
    private string targetFolder = "Assets";

    [MenuItem("Tools/Export Prefab Dependencies")]
    static void Init()
    {
        GetWindow<ExportPrefabDependencies>("Export Prefabs");
    }

    void OnGUI()
    {
        GUILayout.Label("Batch Export Prefab Dependencies", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Select one or more prefabs in the Project window, then export.\n" +
            "Each prefab will be placed into its own folder.",
            MessageType.Info
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

        if (GUILayout.Button("Export Selected Prefabs"))
        {
            prefabs = Selection.objects
                .OfType<GameObject>()
                .Where(p => PrefabUtility.IsPartOfPrefabAsset(p))
                .ToArray();

            if (prefabs.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Prefabs Selected",
                    "Please select one or more prefab assets in the Project window.",
                    "OK"
                );
                return;
            }

            ExportPrefabs(prefabs);
            AssetDatabase.Refresh();
        }
    }

    void ExportPrefabs(GameObject[] prefabs)
    {
        foreach (GameObject prefab in prefabs)
        {
            ExportSinglePrefab(prefab);
        }

        EditorUtility.DisplayDialog(
            "Export Complete",
            $"Exported {prefabs.Length} prefab(s) successfully.",
            "OK"
        );
    }

    void ExportSinglePrefab(GameObject prefab)
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
    }
}
