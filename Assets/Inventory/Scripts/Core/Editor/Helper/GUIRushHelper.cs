using UnityEditor;
using UnityEngine;

namespace Inventory.Scripts.Core.Editor.Helper
{
    public static class GUIRushHelper
    {
        public static Rect NextLine(this Rect position)
        {
            position.y += EditorGUIUtility.singleLineHeight;

            return position;
        }
    }
}