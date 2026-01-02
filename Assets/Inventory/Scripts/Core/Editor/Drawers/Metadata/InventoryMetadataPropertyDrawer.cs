using Inventory.Scripts.Core.Editor.Helper;
using Inventory.Scripts.Core.Items.Metadata;
using UnityEditor;
using UnityEngine;

namespace Inventory.Scripts.Core.Editor.Drawers.Metadata
{
    [CustomPropertyDrawer(typeof(InventoryMetadata))]
    public class InventoryMetadataPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var metadata = EditorPropertyHelper.GetValue<InventoryMetadata>(property, fieldInfo);

            switch (metadata)
            {
                case null:
                    EditorGUI.BeginProperty(position, label, property);
                    EditorGUI.LabelField(position, nameof(InventoryMetadata), "null");
                    EditorGUI.EndProperty();
                    return;
                case ContainerMetadata:
                    return;
            }

            if (metadata.GetType() == typeof(InventoryMetadata))
            {
                // TODO: Implement this metadata
                EditorGUI.BeginProperty(position, label, property);

                EditorGUI.LabelField(position, nameof(InventoryMetadata), "The basic implementation");

                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.PropertyField(position, property, label, true);
        }

        // TODO: Improve the property height for metadata different from InventoryMetadata and ContainerMetadata.
        // If we add a new property to CountableMetadata, it's not increasing the property height.
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var metadata = EditorPropertyHelper.GetValue<InventoryMetadata>(property, fieldInfo);

            if (metadata == null)
            {
                return base.GetPropertyHeight(property, label);
            }

            if (metadata is ContainerMetadata)
            {
                return base.GetPropertyHeight(property, label);
            }

            if (metadata.GetType() == typeof(InventoryMetadata))
            {
                return base.GetPropertyHeight(property, label);
            }

            if (property.isExpanded)
            {
                return base.GetPropertyHeight(property, label) + EditorGUIUtility.singleLineHeight * 2;
            }

            return base.GetPropertyHeight(property, label);
        }
    }
}