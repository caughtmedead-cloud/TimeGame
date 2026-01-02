using System;
using Inventory.Scripts.Core.Controllers.Save;
using Inventory.Scripts.Core.Editor.Helper;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Inventory.Scripts.Core.Editor.Inventory
{
    [CustomEditor(typeof(InventorySo))]
    [CanEditMultipleObjects]
    public class InventorySoEditor : UnityEditor.Editor
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

            EditorGUILayout.LabelField("Actions");

            GUILayout.BeginHorizontal();

            var objects = Selection.objects;

            if (GUILayout.Button("Load"))
            {
                foreach (var obj in objects)
                {
                    var inventorySo = ParseObject(obj);

                    if (inventorySo == null) continue;

                    ApplyLoad(inventorySo);
                }
            }

            if (GUILayout.Button("Save"))
            {
                foreach (var obj in objects)
                {
                    var inventorySo = ParseObject(obj);

                    if (inventorySo == null) continue;

                    ApplySave(inventorySo);
                }
            }

            if (GUILayout.Button("Open"))
            {
                foreach (var obj in objects)
                {
                    var inventorySo = ParseObject(obj);

                    if (inventorySo == null) continue;

                    OpenLocations(inventorySo);
                }
            }

            GUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private static void ApplyLoad(InventorySo inventorySo)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("You can only load if you are playing...".Info());
                return;
            }

            inventorySo.Load();

            EditorUtility.SetDirty(inventorySo);
        }

        private static void ApplySave(InventorySo inventorySo)
        {
            inventorySo.Save();

            EditorUtility.SetDirty(inventorySo);
        }

        private static void OpenLocations(InventorySo inventorySo)
        {
            EditorUtility.RevealInFinder(LocalSaveStrategySo.GetPath(inventorySo.GetSaveKey()));
        }

        private static InventorySo ParseObject(object obj)
        {
            try
            {
                return (InventorySo)obj;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}