using Inventory.Scripts.Core.Editor.Helper;
using Inventory.Scripts.Core.Items;
using UnityEditor;
using UnityEngine;

namespace Inventory.Scripts.Core.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(ItemTable))]
    public class ItemTablePropertyDrawer : PropertyDrawer
    {
        private const float MarginTopField = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var itemTable = EditorPropertyHelper.GetValue<ItemTable>(property, fieldInfo);

            if (itemTable == null)
            {
                EditorGUI.LabelField(position, property.displayName, "null");
                EditorGUI.EndProperty();
                return;
            }

            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded,
                property.displayName
            );

            if (!property.isExpanded) return;

            var totalPosition = new Rect(position);

            EditorGUI.indentLevel++;

            var rect = CreateItemDataSoProperties(totalPosition, itemTable);

            rect.y += 1.5f * EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                "Grid Props", EditorStyles.boldLabel);
            var gridPosXRect = BuildTextField(
                totalPosition,
                nameof(ItemTable.Position) + ".x",
                itemTable.Position?.x.ToString(),
                rect
            );
            var gridPosYRect = BuildTextField(
                totalPosition,
                nameof(Position) + ".y",
                itemTable.Position?.y.ToString(),
                gridPosXRect
            );

            gridPosYRect.y += EditorGUIUtility.singleLineHeight + MarginTopField;

            var currentGridTableProperty =
                EditorPropertyHelper.FindRelativeAutoProperty(property, nameof(ItemTable.CurrentGridTable));

            EditorGUI.PropertyField(
                new Rect(gridPosYRect.x, gridPosYRect.y, gridPosYRect.width, EditorGUIUtility.singleLineHeight),
                currentGridTableProperty,
                true);

            var gridPropertyHeight = EditorGUI.GetPropertyHeight(currentGridTableProperty, true);

            gridPosYRect.y += gridPropertyHeight > 50
                ? gridPropertyHeight + EditorGUIUtility.singleLineHeight
                : EditorGUIUtility.singleLineHeight + MarginTopField;

            var currentHolderProperty =
                EditorPropertyHelper.FindRelativeAutoProperty(property, nameof(ItemTable.CurrentHolder));

            var holderPropertyExpanded = EditorGUI.PropertyField(
                new Rect(gridPosYRect.x, gridPosYRect.y, gridPosYRect.width, EditorGUIUtility.singleLineHeight),
                currentHolderProperty,
                true);

            var holderPropertyHeight = EditorGUI.GetPropertyHeight(currentHolderProperty, true);

            gridPosYRect.y += holderPropertyHeight > 50
                ? holderPropertyHeight + EditorGUIUtility.singleLineHeight
                : EditorGUIUtility.singleLineHeight + MarginTopField;

            EditorGUI.PropertyField(
                new Rect(gridPosYRect.x, gridPosYRect.y, gridPosYRect.width, EditorGUIUtility.singleLineHeight),
                EditorPropertyHelper.FindRelativeAutoProperty(property, nameof(ItemTable.InventoryMetadata)),
                true);

            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }

        private static Rect CreateItemDataSoProperties(Rect totalPosition, ItemTable itemTable)
        {
            if (itemTable.ItemDataSo == null)
            {
                return totalPosition;
            }

            var widthRect = BuildTextField(totalPosition, "Display name", itemTable.ItemDataSo.DisplayName,
                new Rect(totalPosition.x, totalPosition.y + EditorGUIUtility.singleLineHeight, totalPosition.width,
                    EditorGUIUtility.singleLineHeight), true);
            var widthProperty = BuildTextField(totalPosition, "Width", itemTable.Width.ToString(), widthRect);
            var heightProperty = BuildTextField(totalPosition, "Height", itemTable.Height.ToString(), widthProperty);

            return heightProperty;
        }

        private static Rect BuildTextField(Rect position, string text, string value, Rect verticalRect,
            bool isInitialProp = false)
        {
            var rect = new Rect(verticalRect);
            if (!isInitialProp)
            {
                rect.y += EditorGUIUtility.singleLineHeight + MarginTopField;
            }

            EditorGUI.LabelField(
                new Rect(position.x, rect.y, position.width, EditorGUIUtility.singleLineHeight), text, value);
            return rect;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var itemTable = EditorPropertyHelper.GetValue<ItemTable>(property, fieldInfo);

            if (itemTable == null)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            if (!property.isExpanded)
            {
                return base.GetPropertyHeight(property, label);
            }

            var height = EditorGUIUtility.singleLineHeight;

            var grid = property.FindPropertyRelative(
                EditorPropertyHelper.ParseAutoPropertyName(nameof(ItemTable.CurrentGridTable)));
            if (grid != null)
            {
                height += EditorGUI.GetPropertyHeight(grid, true);
            }

            var inventoryMetadataProperty =
                property.FindPropertyRelative(
                    EditorPropertyHelper.ParseAutoPropertyName(nameof(ItemTable.InventoryMetadata)));

            if (inventoryMetadataProperty != null)
            {
                height += EditorGUI.GetPropertyHeight(inventoryMetadataProperty, true);
            }

            var currentProperty = property.Copy();
            currentProperty.NextVisible(true);

            // Calculate height dynamically for each child property
            while (currentProperty.NextVisible(false))
            {
                if (currentProperty.name.Contains(nameof(ItemTable.CurrentHolder)))
                {
                    // Add height for the current property using its own GetPropertyHeight
                    height += EditorGUI.GetPropertyHeight(currentProperty, true) +
                              EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return 8 * EditorGUIUtility.singleLineHeight + height;
        }
    }
}