using System.Collections.Generic;
using System.Linq;
using Inventory.Scripts.Core.Editor.Helper;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using UnityEditor;
using UnityEngine;

namespace Inventory.Scripts.Core.Editor.Drawers.Metadata
{
    [CustomPropertyDrawer(typeof(ContainerMetadata))]
    public class ContainerMetadataPropertyDrawer : PropertyDrawer
    {
        private const float ScrollViewArea = 484f;
        private const float PaddingContainerGrids = 30f;

        private bool _useDefaultDrawer;
        private readonly Dictionary<string, Vector2> _scrollsPositions = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var metadata = EditorPropertyHelper.GetValue<ContainerMetadata>(property, fieldInfo);

            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded,
                property.displayName
            );

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;

            position = position.NextLine();

            EditorGUI.LabelField(position, "Type", metadata.GetType().Name);
            
            position = position.NextLine();
            
            _useDefaultDrawer = EditorGUI.Toggle(position, "Use Default Drawer", _useDefaultDrawer);

            position = position.NextLine();
            position.y += 4f;

            if (_useDefaultDrawer)
            {
                UseDefaultDrawerProperty(position, property);
            }
            else
            {
                BuildContainerDrawerGUI(position, metadata, property);
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        private void BuildContainerDrawerGUI(Rect position, ContainerMetadata metadata, SerializedProperty property)
        {
            var containerGrids = GetContainerGridsIfExists(metadata);

            if (containerGrids == null)
            {
                UseDefaultDrawerProperty(position, property);
                return;
            }

            var gridsInventory = metadata.Inventories;
            var abstractGrids = containerGrids.GetAbstractGrids();

            if (abstractGrids.Length == 0 || gridsInventory.Count != abstractGrids.Length)
            {
                UseDefaultDrawerProperty(position, property);
                return;
            }

            var abstractGridForTile = FindAbstractGridWhereTileSpecIsNotNull(abstractGrids);

            if (abstractGridForTile == null)
            {
                Debug.LogWarning(
                    "AbstractGrid have something null... inventorySettingsAnchorSo or inventorySettingsSo or tileSpecSo"
                        .Editor());
                UseDefaultDrawerProperty(position, property);
                return;
            }

            var inventorySettingsAnchorSo = abstractGridForTile.InventorySettingsAnchorSo;
            var inventorySettingsSo = inventorySettingsAnchorSo.InventorySettingsSo;
            var tileSpecSo = inventorySettingsSo.TileSpecSo;

            EditorGUI.LabelField(position, "Grid Inventory", $"{containerGrids.name}");

            position = position.NextLine();

            var rect = new Rect(
                position.x + 32f,
                position.y + 8f,
                position.width - EditorGUIUtility.singleLineHeight * EditorGUI.indentLevel,
                ScrollViewArea
            );

            // Background Rect
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.1f));

            var scrollArea = new Rect(rect.x, rect.y, rect.width, ScrollViewArea);

            var rectContainerGrids = GetContainerGridsWidthAndHeight(containerGrids);
            var widthAndHeightForEditorSize = TableDrawerHelper.ParseWidthAndHeightForEditorSize(
                rectContainerGrids,
                tileSpecSo
            );

            var key = widthAndHeightForEditorSize.ToString();
            var scrollPosition = _scrollsPositions.GetValueOrDefault(key, Vector2.zero);

            AddScrollPosition(
                key,
                GUI.BeginScrollView(
                    scrollArea,
                    scrollPosition,
                    new Rect(
                        scrollArea.x,
                        scrollArea.y,
                        widthAndHeightForEditorSize.x + PaddingContainerGrids,
                        widthAndHeightForEditorSize.y + PaddingContainerGrids
                    )
                )
            );

            // Put the pointer in the left top corner of the rect to start drawing the grid.
            rect.x -= 15f;

            for (var i = 0; i < gridsInventory.Count; i++)
            {
                var pos = rect;

                // Add paddings from rect to start the grid
                pos.x += 16f;
                pos.y += 16f;

                var abstractGrid = abstractGrids[i];
                var gridTable = gridsInventory[i];

                var abstractGridTransform = (RectTransform)abstractGrid.transform;

                var worldRect = GetWorldRect(abstractGridTransform);

                // This represent the position where the left top is located on the prefab.
                var positionOnPrefab = new Vector2(worldRect.x, Mathf.Abs(worldRect.height) - Mathf.Abs(worldRect.y));
                var resolveVectorForEditor =
                    TableDrawerHelper.ParseWidthAndHeightForEditorSize(positionOnPrefab, tileSpecSo);

                pos.x += resolveVectorForEditor.x;
                pos.y += Mathf.Abs(resolveVectorForEditor.y);

                TableDrawerHelper.DrawTable(pos, gridTable.Width, gridTable.Height, gridTable.Slots);
            }

            GUI.EndScrollView();
        }

        private void AddScrollPosition(string key, Vector2 beginScrollView)
        {
            if (_scrollsPositions.ContainsKey(key))
            {
                _scrollsPositions.Remove(key);
            }

            _scrollsPositions.Add(key, beginScrollView);
        }

        private static void UseDefaultDrawerProperty(Rect position, SerializedProperty property)
        {
            var propertyGridsInventory =
                EditorPropertyHelper.FindRelativeAutoProperty(property, nameof(ContainerMetadata.Inventories));

            if (propertyGridsInventory != null)
            {
                EditorGUI.PropertyField(
                    new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                    propertyGridsInventory,
                    true
                );
            }
        }

        private static Vector2 GetContainerGridsWidthAndHeight(ContainerGrids containerGrids)
        {
            var rectTransform = (RectTransform)containerGrids.transform;
            var rectTransformRect = rectTransform.rect;

            var rectWidth = rectTransformRect.width;
            var rectHeight = rectTransformRect.height;

            return new Vector2(rectWidth, rectHeight);
        }

        private static Rect GetWorldRect(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return new Rect(corners[0], corners[2] - corners[0]);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return base.GetPropertyHeight(property, label);
            }

            var metadata = EditorPropertyHelper.GetValue<ContainerMetadata>(property, fieldInfo);

            var gridsHeight = 0f;

            var currentGridTableProperty =
                EditorPropertyHelper.FindRelativeAutoProperty(property, nameof(ContainerMetadata.Inventories));

            if (currentGridTableProperty != null && _useDefaultDrawer)
            {
                gridsHeight += EditorGUI.GetPropertyHeight(currentGridTableProperty, true) +
                               2.5f * EditorGUIUtility.singleLineHeight;
            }

            var containerGridsIfExists = GetContainerGridsIfExists(metadata);

            if (!_useDefaultDrawer && containerGridsIfExists != null)
            {
                var abstractGrids = containerGridsIfExists.GetAbstractGrids();
                var abstractGrid = FindAbstractGridWhereTileSpecIsNotNull(abstractGrids);

                if (abstractGrid == null)
                {
                    gridsHeight += EditorGUI.GetPropertyHeight(currentGridTableProperty, true);
                }

                gridsHeight += ScrollViewArea + 2f * TableDrawerHelper.TableLineSpace;
            }

            return gridsHeight;
        }

        private ContainerGrids GetContainerGridsIfExists(ContainerMetadata containerMetadata)
        {
            var itemTable = containerMetadata.ItemTable;

            if (itemTable?.ItemDataSo is ItemContainerDataSo containerDataSo)
            {
                return containerDataSo.ContainerGrids;
            }

            return null;
        }

        private static AbstractGrid FindAbstractGridWhereTileSpecIsNotNull(AbstractGrid[] abstractGrids)
        {
            var abstractGridForTile = abstractGrids.FirstOrDefault(abstractGrid =>
                abstractGrid.InventorySettingsAnchorSo != null &&
                abstractGrid.InventorySettingsAnchorSo.InventorySettingsSo != null &&
                abstractGrid.InventorySettingsAnchorSo.InventorySettingsSo.TileSpecSo !=
                null);

            return abstractGridForTile;
        }
    }
}