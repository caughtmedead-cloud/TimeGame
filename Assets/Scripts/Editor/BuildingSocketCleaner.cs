using UnityEngine;
using UnityEditor;

namespace BuildingTools
{
    public static class BuildingSocketCleaner
    {
        [MenuItem("Tools/TimeGame/Building Tools/Strip BuildingSocket Components from Selection")]
        private static void StripFromSelection()
        {
            if (Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select one or more GameObjects to strip BuildingSocket components from.", "OK");
                return;
            }
            
            int count = 0;
            foreach (GameObject obj in Selection.gameObjects)
            {
                BuildingSocket socket = obj.GetComponent<BuildingSocket>();
                if (socket != null)
                {
                    Undo.DestroyObjectImmediate(socket);
                    count++;
                }
            }
            
            Debug.Log($"Removed {count} BuildingSocket component(s) from selection.");
        }
        
        [MenuItem("Tools/TimeGame/Building Tools/Strip BuildingSocket Components from Selection", true)]
        private static bool ValidateStripFromSelection()
        {
            return Selection.gameObjects.Length > 0;
        }
        
        [MenuItem("Tools/TimeGame/Building Tools/Strip All BuildingSocket Components in Scene")]
        private static void StripFromScene()
        {
            if (!EditorUtility.DisplayDialog(
                "Strip All BuildingSocket Components",
                "This will remove ALL BuildingSocket components from the current scene.\n\nThis operation can be undone with Ctrl+Z.\n\nContinue?",
                "Yes, Remove All",
                "Cancel"))
            {
                return;
            }
            
            BuildingSocket[] allSockets = Object.FindObjectsByType<BuildingSocket>(FindObjectsSortMode.None);
            
            if (allSockets.Length == 0)
            {
                EditorUtility.DisplayDialog("No Components Found", "No BuildingSocket components found in the scene.", "OK");
                return;
            }
            
            Undo.SetCurrentGroupName("Strip All BuildingSocket Components");
            int group = Undo.GetCurrentGroup();
            
            foreach (BuildingSocket socket in allSockets)
            {
                Undo.DestroyObjectImmediate(socket);
            }
            
            Undo.CollapseUndoOperations(group);
            
            Debug.Log($"Removed {allSockets.Length} BuildingSocket component(s) from scene.");
        }
        
        [MenuItem("Tools/TimeGame/Building Tools/Count BuildingSocket Components in Scene")]
        private static void CountInScene()
        {
            BuildingSocket[] allSockets = Object.FindObjectsByType<BuildingSocket>(FindObjectsSortMode.None);
            
            if (allSockets.Length == 0)
            {
                EditorUtility.DisplayDialog("Component Count", "No BuildingSocket components found in the scene.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Component Count", $"Found {allSockets.Length} BuildingSocket component(s) in the scene.\n\nNote: These are editor-only and won't be included in builds.", "OK");
            }
        }
    }
}
