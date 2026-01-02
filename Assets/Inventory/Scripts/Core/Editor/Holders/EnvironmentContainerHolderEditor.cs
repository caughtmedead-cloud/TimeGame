using System;
using Inventory.Scripts.Core.Editor.Helper;
using Inventory.Scripts.Core.Environment;
using UnityEditor;
using UnityEngine;

namespace Inventory.Scripts.Core.Editor.Holders
{
    [CustomEditor(typeof(EnvironmentContainerHolder), true)]
    [CanEditMultipleObjects]
    public class EnvironmentContainerHolderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorPropertyHelper.AddScriptProperty(serializedObject);

            // Get an iterator over all serialized properties
            var property = serializedObject.GetIterator();
            property.NextVisible(true); // Move to the first property

            // Iterate over all properties
            while (property.NextVisible(false))
            {
                // Skip the property you want to hide (in this case, "hiddenValue")
                if (property.name == "hiddenValue")
                    continue;

                // Draw the other properties
                EditorGUILayout.PropertyField(property, true);
            }

            GUILayout.Space(32);

            GUILayout.BeginHorizontal();

            var objects = Selection.gameObjects;

            if (GUILayout.Button("Open"))
            {
                foreach (var obj in objects)
                {
                    var environmentContainerHolder = ParseObject(obj);

                    if (environmentContainerHolder == null) continue;

                    environmentContainerHolder.OpenContainer();
                }
            }

            if (GUILayout.Button("Close"))
            {
                foreach (var obj in objects)
                {
                    var environmentContainerHolder = ParseObject(obj);

                    if (environmentContainerHolder == null) continue;

                    environmentContainerHolder.CloseContainer();
                }
            }

            GUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private static EnvironmentContainerHolder ParseObject(GameObject obj)
        {
            try
            {
                return obj.GetComponent<EnvironmentContainerHolder>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}