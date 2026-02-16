using UnityEngine;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Defines a single context menu option with label and callback.
    /// Used to build dynamic context menus for both inventory and world items.
    /// </summary>
    public class ContextMenuOption
    {
        public string Label { get; set; }
        public System.Action Callback { get; set; }
        public bool IsEnabled { get; set; } = true;

        public ContextMenuOption(string label, System.Action callback, bool isEnabled = true)
        {
            Label = label;
            Callback = callback;
            IsEnabled = isEnabled;
        }
    }
}
