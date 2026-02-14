using UnityEngine;
using UnityEditor;
using System.IO;

public class ZeroPrefabPositions : EditorWindow
{
    [MenuItem("Tools/Zero Prefab Positions")]
    public static void ZeroAllPrefabPositions()
    {
        string prefabsPath = "Assets/_Prefabs";
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabsPath });
        
        int processedCount = 0;
        int skippedCount = 0;
        
        EditorUtility.DisplayProgressBar("Zeroing Prefab Positions", "Processing prefabs...", 0f);
        
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            float progress = (float)processedCount / prefabGuids.Length;
            EditorUtility.DisplayProgressBar("Zeroing Prefab Positions", $"Processing: {path}", progress);
            
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                skippedCount++;
                continue;
            }
            
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);
            
            if (prefabInstance != null)
            {
                Transform rootTransform = prefabInstance.transform;
                
                if (rootTransform.localPosition != Vector3.zero)
                {
                    rootTransform.localPosition = Vector3.zero;
                    PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                    processedCount++;
                }
                else
                {
                    skippedCount++;
                }
                
                PrefabUtility.UnloadPrefabContents(prefabInstance);
            }
        }
        
        EditorUtility.ClearProgressBar();
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"Prefab Position Reset Complete!\nProcessed: {processedCount}\nSkipped (already zero): {skippedCount}\nTotal: {prefabGuids.Length}");
        EditorUtility.DisplayDialog("Complete", 
            $"Zeroed positions for {processedCount} prefabs.\n{skippedCount} prefabs were already at (0,0,0).", "OK");
    }
}
