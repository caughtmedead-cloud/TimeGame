using UnityEditor;
using UnityEngine;

namespace Inventory.Samples.UtilityScenes.IconGenerator.Scripts.Editor
{
    [CustomEditor(typeof(Samples.IconGenerator.Scripts.IconGenerator), true)]
    public class IconGeneratorEditor : UnityEditor.Editor
    {
        private Samples.IconGenerator.Scripts.IconGenerator _iconGenerator;

        public override void OnInspectorGUI()
        {
            _iconGenerator = (Samples.IconGenerator.Scripts.IconGenerator)target;

            DrawDefaultInspector();

            GUILayout.Space(24);

            GUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("Debug Props", EditorStyles.boldLabel);
            GUILayout.Space(8);
            GUILayout.Label("Pixels: " + _iconGenerator.GetPixels());
            GUILayout.Label("Objects Count: " +
                            _iconGenerator.GetSceneObjects().Count);

            GUILayout.EndVertical();

            GUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Take Screenshots"))
            {
                if (_iconGenerator.GetSceneObjects().Count == 0)
                {
                    Debug.LogWarning("Make sure to set the GameObjects in the property 'Scene Objects'");
                    return;
                }

                _iconGenerator.ProcessScreenshot();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}