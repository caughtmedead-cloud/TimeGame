using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory.Samples
{
    public class SampleSceneSelected : MonoBehaviour
    {
        [Header("Scene Styles")] [SerializeField, Tooltip("The last will be appear as the first in the dropdown list.")]
        private List<SceneSelected> styles;

        [Header("Configuration")] [SerializeField]
        private Image icon;

        [SerializeField] private TMP_Text title;

        [SerializeField] private TMP_Text path;

        [SerializeField] private TMP_Text description;

        public List<SceneSelected> Styles => styles;

        public void SetSceneSelected(string sceneName, string scenePath)
        {
            var sceneSelected = styles.Find(selected => selected.SceneName.Equals(sceneName));

            if (sceneSelected == null) return;

            icon.sprite = sceneSelected.Icon;
            title.SetText(GetPropertyOrDefault(sceneSelected.Title, sceneName));
            path.SetText(scenePath);
            description.SetText(GetPropertyOrDefault(sceneSelected.Description,
                "Explore a scene from our asset by simply clicking the \"Play Scene\" button and immerse yourself in an enjoyable experience."));
        }

        private static string GetPropertyOrDefault(string value, string defaultValue)
        {
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }


        [Serializable]
        public class SceneSelected
        {
            [SerializeField] private string sceneName;
            [SerializeField] private Sprite icon;
            [SerializeField] private string title;
            [SerializeField] private string description;

            public string SceneName => sceneName;

            public Sprite Icon => icon;

            public string Title => title;

            public string Description => description;
        }
    }
}