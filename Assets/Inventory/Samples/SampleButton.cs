using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Inventory.Samples
{
    public class SampleButton : MonoBehaviour
    {
        public void GoBackToSampleScene()
        {
            var sampleSceneName = GetSampleSceneName();

            SceneManager.LoadScene(sampleSceneName);
        }

        private static string GetSampleSceneName()
        {
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                var sceneName = Path.GetFileNameWithoutExtension(scenePath);

                if (sceneName.Equals("Sample", StringComparison.OrdinalIgnoreCase))
                {
                    return sceneName;
                }
            }

            var firstScenePath = SceneUtility.GetScenePathByBuildIndex(0);
            return Path.GetFileNameWithoutExtension(firstScenePath);
        }
    }
}