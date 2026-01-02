using System.Linq;
using Inventory.Scripts.Core.Holders;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Inventory.Scripts.Core.Editor.Holders
{
    [CustomEditor(typeof(ReplicatorItemHolder), true)]
    public class ReplicatorItemHolderEditor : UnityEditor.Editor
    {
        private const string ScriptPropertyName = "m_Script";
        private const string UniqueUid = "uniqueUid";
        private const string ReplicationUid = "replicationUid";
        private const string DisplayRotatedIcon = "displayRotatedIcon";
        private const string UseDefaultSpritePropertyName = "useDefaultSprite";
        private const string DefaultSpritePropertyName = "defaultSprite";
        private const string SpriteColorPropertyName = "spriteColor";
        private const string InventorySoPropertyName = "inventorySo";

        private const string GameObjectName = "Image_Holder";

        private ReplicatorItemHolder _replicatorItemHolder;

        private bool _useDefaultSprite;

        private SerializedProperty _useDefaultSpriteProperty;
        private SerializedProperty _defaultSprite;
        private SerializedProperty _spriteColor;

        private GameObject _itemHolderImage;

        private void OnEnable()
        {
            _useDefaultSpriteProperty = serializedObject.FindProperty(UseDefaultSpritePropertyName);
            _defaultSprite = serializedObject.FindProperty(DefaultSpritePropertyName);
            _spriteColor = serializedObject.FindProperty(SpriteColorPropertyName);
        }

        public override void OnInspectorGUI()
        {
            _replicatorItemHolder = (ReplicatorItemHolder)target;

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(ScriptPropertyName));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(serializedObject.FindProperty(ReplicationUid));

            GUILayout.Space(12f);
            GUILayout.Label("Item Holder Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_useDefaultSpriteProperty);

            EditorGUI.BeginDisabledGroup(!_useDefaultSpriteProperty.boolValue);

            EditorGUILayout.PropertyField(_defaultSprite);
            EditorGUILayout.PropertyField(_spriteColor);

            if (!_replicatorItemHolder.UseDefaultSprite)
            {
                DeleteObjectInHierarchy();
            }

            GUILayout.Space(4);
            if (GUILayout.Button("Create/Update Image in Children"))
            {
                if (_replicatorItemHolder.UseDefaultSprite)
                {
                    CreateSpriteInChildren();
                }
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(serializedObject.FindProperty(DisplayRotatedIcon));

            DrawPropertiesExcluding(serializedObject, ScriptPropertyName, UniqueUid, ReplicationUid,
                UseDefaultSpritePropertyName,
                DefaultSpritePropertyName, SpriteColorPropertyName, DisplayRotatedIcon, InventorySoPropertyName);

            serializedObject.ApplyModifiedProperties();
        }

        private void CreateSpriteInChildren()
        {
            if (_replicatorItemHolder == null) return;

            _itemHolderImage = GetItemHolderImage();

            _itemHolderImage.transform.SetParent(_replicatorItemHolder.transform, false);
            _itemHolderImage.transform.SetAsFirstSibling();

            _itemHolderImage.layer = LayerMask.NameToLayer("UI");

            var image = GetImage(_itemHolderImage);

            image.sprite = _replicatorItemHolder.DefaultSprite;
            image.color = _replicatorItemHolder.SpriteColor;

            SaveScene();
        }

        private void DeleteObjectInHierarchy()
        {
            var gameObject = FindImageHolderInChildren();

            if (gameObject == null) return;

            DestroyImmediate(gameObject.gameObject);
            SaveScene();
        }

        private GameObject GetItemHolderImage()
        {
            var gameObject = FindImageHolderInChildren();

            return gameObject ? gameObject.gameObject : new GameObject(GameObjectName, typeof(RectTransform));
        }

        private Image FindImageHolderInChildren()
        {
            var componentsInChildren = _replicatorItemHolder.GetComponentsInChildren<Image>();

            return (from componentsInChild in componentsInChildren
                where componentsInChild != null && componentsInChild.name.Equals(GameObjectName)
                select componentsInChild.GetComponent<Image>()).FirstOrDefault();
        }

        private static Image GetImage(GameObject gameObject)
        {
            var image = gameObject.GetComponent<Image>();

            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
            }

            return image;
        }

        private static void SaveScene()
        {
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
    }
}