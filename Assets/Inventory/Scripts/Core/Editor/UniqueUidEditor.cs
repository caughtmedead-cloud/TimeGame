using System;
using Inventory.Scripts.Core.Environment;
using Inventory.Scripts.Core.Helper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

#pragma warning disable CS0162 // Unreachable code detected

namespace Inventory.Scripts.Core.Editor
{
    [CustomPropertyDrawer(typeof(UniqueUid))]
    public class UniqueUidEditor : PropertyDrawer
    {
        // Disable this if you don't want to appear the log of generating a new id everytime a container with the same id is displayed.
        private const bool LogNewIdGenerating = false;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Get the id property
            var idProp = property.FindPropertyRelative(UniqueUid.FieldName);
            var id = idProp.stringValue;

            // Check if the object is a prefab instance
            var isPrefabInstance = PrefabUtility.GetPrefabInstanceStatus(property.serializedObject.targetObject) !=
                                   PrefabInstanceStatus.NotAPrefab;

            // Check if the object is in a prefab stage
            var isPrefabStage = PrefabStageUtility.GetCurrentPrefabStage() != null;

            // Check if the object is a prefab
            var isPrefab = IsPrefab(property, isPrefabInstance, isPrefabStage);

            var ugiComponent = property.serializedObject.targetObject;

            switch (isPrefab)
            {
                // If the object is not a prefab, allow editing the id
                case false:
                    EditForm(position, idProp, ugiComponent);
                    break;
                // If the object is a prefab and has a non-empty id, clear the id
                case true when !string.IsNullOrEmpty(id):
                    idProp.stringValue = null;
                    property.serializedObject.ApplyModifiedProperties();
                    break;
            }


            EditorGUI.EndProperty();
        }

        private bool IsPrefab(SerializedProperty property, bool isPrefabInstance, bool isPrefabStage)
        {
/*
            if (AvoidPrefabIds)
            {
                return PrefabUtility.GetPrefabAssetType(property.serializedObject.targetObject) !=
                    PrefabAssetType.NotAPrefab && !isPrefabInstance || isPrefabStage;
            }
*/

            return false;
        }

        private void EditForm(Rect position, SerializedProperty idProp, Object ugiComponent)
        {
            var id = idProp.stringValue;

            // If the id is empty, generate a new one
            if (string.IsNullOrEmpty(id))
            {
                idProp.stringValue = GetGuid();
                id = idProp.stringValue;
                idProp.serializedObject.ApplyModifiedProperties();
            }

            FixIdByTypeIfNeeded(id, idProp, ugiComponent);

            // Copy the id to clipboard
            if (GUI.Button(
                    new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 10f, position.width / 2 - 5,
                        EditorGUIUtility.singleLineHeight),
                    "Copy ID"
                ))
            {
                GUIUtility.systemCopyBuffer = id;
            }

            // Randomize the id
            if (GUI.Button(new Rect(position.x + position.width / 2 + 5,
                    position.y + EditorGUIUtility.singleLineHeight + 10f, position.width / 2 - 5,
                    EditorGUIUtility.singleLineHeight), "Randomize ID"))
            {
                if (EditorUtility.DisplayDialog(
                        "Randomize ID",
                        "Are you sure, this can break existing save data",
                        "Randomize", "Cancel"))
                {
                    idProp.stringValue = GetGuid();
                    idProp.serializedObject.ApplyModifiedProperties();
                }
            }


            EditorGUI.BeginDisabledGroup(true);
            // Display the id
            EditorGUI.PropertyField(
                new Rect(position.x, position.y + 2f, position.width, EditorGUIUtility.singleLineHeight),
                idProp
            );
            EditorGUI.EndDisabledGroup();
        }

        private static void FixIdByTypeIfNeeded(string id, SerializedProperty idProp, Object ugiComponent)
        {
            if (ugiComponent == null) return;

            if (typeof(EnvironmentContainerHolder) != ugiComponent.GetType())
            {
                return;
            }

            if (!FixContainerIdHelper.NeedToFixContainerId(id))
            {
                return;
            }

            if (LogNewIdGenerating)
            {
                Debug.Log(
                    $"Generating a new '{UniqueUid.FieldName}' value for the {ugiComponent.name} because it already exists in/another scene. This prevent from breaking the save system."
                        .Editor());
            }

            idProp.stringValue = GetGuid();
            idProp.serializedObject.ApplyModifiedProperties();
        }

        private static string GetGuid()
        {
            return Guid.NewGuid().ToString();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2.6f;
        }
    }
}