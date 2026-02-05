using UnityEditor;
using UnityEngine;

public static class PrefabShortcuts
{
    [MenuItem("GameObject/Prefab/Unpack Completely %#u", false, 0)]
    static void UnpackCompletely()
    {
        foreach (var go in Selection.gameObjects)
        {
            PrefabUtility.UnpackPrefabInstance(
                go,
                PrefabUnpackMode.Completely,
                InteractionMode.UserAction
            );
        }
    }
}