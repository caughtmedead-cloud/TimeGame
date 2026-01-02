using System;
using Inventory.Scripts.Core.Displays.Filler.Primitives;
using UnityEditor;
using UnityEngine;

namespace Inventory.Scripts.Core.Editor.Displays
{
    [CustomEditor(typeof(RefreshLayoutFiller), true)]
    public class RefreshLayoutFillerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var objects = Selection.gameObjects;

            GUILayout.Space(10);

            if (GUILayout.Button("Refresh UI"))
            {
                foreach (var obj in objects)
                {
                    var filler = ParseObject(obj);

                    if (filler == null) continue;

                    filler.OnRefreshUI();
                }
            }
        }

        private static RefreshLayoutFiller ParseObject(GameObject obj)
        {
            try
            {
                return obj.GetComponent<RefreshLayoutFiller>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}