using System;
using UnityEditor;
using UnityEngine;

namespace Inventory.Scripts.Core.Editor
{
    public class EditorInputDialog : EditorWindow
    {
        private string _description;
        private string _confirmButton;
        private string _cancelButton;

        private string _inputText = "";
        private Action<string> _callback;

        private void OnGUI()
        {
            GUILayout.Label(_description, EditorStyles.label);

            GUILayout.Space(16);

            GUILayout.Label("Enter Text:", EditorStyles.boldLabel);
            _inputText = EditorGUILayout.TextField(_inputText);

            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_confirmButton) ||
                (Event.current.keyCode == KeyCode.Return && Event.current.type == EventType.KeyUp))
            {
                Close();
                _callback?.Invoke(_inputText);
            }

            if (GUILayout.Button(_cancelButton))
            {
                Close();
                _callback?.Invoke(null);
            }

            GUILayout.EndHorizontal();
        }

        private void SetParams(
            string confirmButton,
            string cancelButton,
            string description,
            string initialInputValue
        )
        {
            _description = description;
            _confirmButton = confirmButton;
            _cancelButton = cancelButton;
            _inputText = initialInputValue;
        }

        public static void Show(Action<string> callback,
            string title = "Modal Dialog",
            string confirmButton = "Set",
            string cancelButton = "Cancel",
            string description = "Pass a value to the input and receive that value on the callback action",
            string initialInputValue = ""
        )
        {
            var window = CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window._callback = callback;
            window.position = new Rect(Screen.width / 2f, Screen.height / 0.5f, 420, 100);
            window.SetParams(confirmButton, cancelButton, description, initialInputValue);
            window.minSize = new Vector2(256f, 100f);
            window.Show();
        }
    }
}