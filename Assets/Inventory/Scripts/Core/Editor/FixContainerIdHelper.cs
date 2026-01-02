using System.Collections.Generic;
using System.Linq;
using Inventory.Scripts.Core.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

#pragma warning disable CS0162 // Unreachable code detected

namespace Inventory.Scripts.Core.Editor
{
    public static class FixContainerIdHelper
    {
        private const bool SearchOtherScenes = false;

        public static bool NeedToFixContainerId(string id)
        {
            var activeScenePath = SceneManager.GetActiveScene().path;

            foreach (var scene in GetPaths())
            {
                Scene? otherScene = null;
                if (activeScenePath != scene)
                {
                    otherScene = EditorSceneManager.OpenScene(scene, OpenSceneMode.Additive);
                }

                var components = Object.FindObjectsOfType<EnvironmentContainerHolder>(true).ToList();

                // Find all components with the same id
                var matchingHolders = components.Where(holder => holder.uniqueUid.Uid == id).ToList();

                CloseOtherScene(otherScene);

                if (matchingHolders.Count > 1) return true;
            }

            return false;
        }

        private static void CloseOtherScene(Scene? otherScene)
        {
            if (otherScene.HasValue)
            {
                EditorSceneManager.CloseScene(otherScene.Value, true);
            }
        }

        private static List<string> GetPaths()
        {
            var list = new List<string> { SceneManager.GetActiveScene().path };

            if (!SearchOtherScenes) return list;

#if UNITY_EDITOR
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            foreach (var guid in sceneGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (list.Contains(path)) continue;

                list.Add(path);
            }
#endif

            return list;
            // var targets = Selection.GetFiltered(typeof(SceneAsset), SelectionMode.DeepAssets);
            // return targets.Cast<SceneAsset>().ToList();
            // .Select(AssetDatabase.GetAssetPath)
        }
    }
}