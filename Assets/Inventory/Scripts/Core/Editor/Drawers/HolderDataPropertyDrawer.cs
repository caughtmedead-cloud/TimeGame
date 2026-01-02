using Inventory.Scripts.Core.Holders;
using UnityEditor;
using UnityEngine;

namespace Inventory.Scripts.Core.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(HolderData))]
    public class HolderDataPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded, label, true);

            var fieldPosition = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width,
                EditorGUIUtility.singleLineHeight);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                var currentProperty = property.Copy();
                currentProperty.NextVisible(true); // Move to the first property

                // Iterate through child properties
                while (currentProperty.NextVisible(false))
                {
                    // Skip 'hiddenValue' property
                    if (IsPropertyNameIncluded(currentProperty))
                    {
                        // Draw each property inside the foldout
                        EditorGUI.PropertyField(fieldPosition, currentProperty, true);

                        // Move to the next position
                        fieldPosition.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        // Override GetPropertyHeight to calculate the total height of all fields
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Base height for the foldout itself
            var height = EditorGUIUtility.singleLineHeight;

            // Add height for the child properties if the foldout is expanded
            if (!property.isExpanded) return height;

            var currentProperty = property.Copy();
            var enterChildren = true;

            // Calculate height dynamically for each child property
            while (currentProperty.NextVisible(enterChildren))
            {
                enterChildren = false;

                // Skip hidden or excluded properties
                if (IsPropertyNameIncluded(currentProperty))
                {
                    // Add height for the current property using its own GetPropertyHeight
                    height += EditorGUI.GetPropertyHeight(currentProperty, true) +
                              EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return height;
        }

        private static bool IsPropertyNameIncluded(SerializedProperty currentProperty)
        {
            return currentProperty.name is "isEquipped" or "itemEquipped";
        }
    }
}