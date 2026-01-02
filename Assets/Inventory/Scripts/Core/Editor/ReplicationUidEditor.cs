using UnityEditor;
using UnityEngine;

namespace Inventory.Scripts.Core.Editor
{
    [CustomPropertyDrawer(typeof(ReplicationUid))]
    public class ReplicationUidEditor : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Get the id property
            var idProp = property.FindPropertyRelative(ReplicationUid.FieldName);

            EditorGUI.BeginProperty(position, label, property);

            EditForm(position, idProp);

            EditorGUI.EndProperty();
        }

        private void EditForm(Rect position, SerializedProperty idProp)
        {
            var id = idProp.stringValue;

            // If the id is empty, generate a new one
            if (string.IsNullOrEmpty(id))
            {
                idProp.stringValue = GetEmptyStateId();
                idProp.serializedObject.ApplyModifiedProperties();
            }

            // Copy the id to clipboard
            if (GUI.Button(new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 10f,
                    position.width / 2 - 5,
                    EditorGUIUtility.singleLineHeight), "Edit ID"))
            {
                EditorInputDialog.Show(result =>
                    {
                        if (result != null)
                        {
                            idProp.stringValue = result;
                            idProp.serializedObject.ApplyModifiedProperties();
                        }
                        else
                        {
                            Debug.Log("Dialog was canceled.");
                        }
                    },
                    title: "Edit ID",
                    confirmButton: "Set",
                    description: "The holder which will replicate from character inventory",
                    initialInputValue: id
                );
            }

            // Copy the id to clipboard
            if (GUI.Button(new Rect(position.x + position.width / 2 + 5,
                        position.y + EditorGUIUtility.singleLineHeight + 10f, position.width / 2 - 5,
                        EditorGUIUtility.singleLineHeight),
                    "Clear ID"
                ))
            {
                if (EditorUtility.DisplayDialog(
                        "Clear ID",
                        "Are you sure, holder will no longer replicate this uid.",
                        "Clear", "Cancel"))
                {
                    idProp.stringValue = GetEmptyStateId();
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

        private static string GetEmptyStateId()
        {
            return ReplicationUid.DefaultMessageEmpty;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2.6f;
        }
    }
}