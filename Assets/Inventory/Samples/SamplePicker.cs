using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Inventory.Samples
{
    public class SamplePicker : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown sceneDropdown;
        [SerializeField] private SampleSceneSelected sampleSceneSelected;

        private void Start()
        {
            sceneDropdown.ClearOptions();
            PopulateSceneDropdown();
            Cursor.lockState = CursorLockMode.None;
        }

        private void OnEnable()
        {
            sceneDropdown.onValueChanged.AddListener(ChangeSelectedScene);
        }

        private void OnDisable()
        {
            sceneDropdown.onValueChanged.RemoveListener(ChangeSelectedScene);
        }

        private void PopulateSceneDropdown()
        {
            if (SceneManager.sceneCountInBuildSettings < 1)
            {
                Debug.LogError("Scenes aren't configured on build settings...");
                return;
            }

            var sceneNames = sampleSceneSelected.Styles
                .Select(sceneSelected => sceneSelected.Title)
                .ToList();

            sceneNames.Reverse();
            sceneDropdown.AddOptions(sceneNames);
            ChangeSelectedScene(0);
        }

        private void ChangeSelectedScene(int value)
        {
            var title = sceneDropdown.options[value].text;

            var sceneFound = FindSelectedScene(title);

            if (sceneFound == null)
            {
                Debug.LogError($"It cannot be found scene with title. Title: {title}");
                return;
            }

            var sceneName = sceneFound.SceneName;

            var scenePath = GetScenePathByName(sceneName);

            sampleSceneSelected.SetSceneSelected(sceneName, scenePath);
        }

        private static string GetScenePathByName(string sceneName)
        {
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                var sceneNameByPath = Path.GetFileNameWithoutExtension(scenePath);

                if (sceneName.Equals(sceneNameByPath))
                {
                    return scenePath;
                }
            }

            return null;
        }

        public void LoadSelectedScene()
        {
            var selectedSceneTitle = sceneDropdown.options[sceneDropdown.value].text;

            var sceneSelected = FindSelectedScene(selectedSceneTitle);

            if (sceneSelected == null)
            {
                Debug.LogError($"It cannot be found scene with title. Title: {selectedSceneTitle}");
                return;
            }

            SceneManager.LoadScene(sceneSelected.SceneName);
        }

        private SampleSceneSelected.SceneSelected FindSelectedScene(string title)
        {
            return sampleSceneSelected.Styles
                .Find(selected => selected.Title.Equals(title));
        }
    }
}