using UnityEngine;
using UnityEditor;

namespace BuildingTools
{
    public static class BuildingSocketCleanup
    {
        [MenuItem("Tools/Building Tools/Strip BuildingSocket Components from Selection")]
        private static void StripSocketsFromSelection()
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
            
            if (count > 0)
            {
                Debug.Log($"Removed {count} BuildingSocket component(s) from selection.");
            }
            else
            {
                Debug.Log("No BuildingSocket components found on selected objects.");
            }
        }
        
        [MenuItem("Tools/Building Tools/Strip BuildingSocket Components from Selection", true)]
        private static bool ValidateStripSocketsFromSelection()
        {
            return Selection.gameObjects.Length > 0;
        }
        
        [MenuItem("Tools/Building Tools/Strip All BuildingSocket Components in Scene")]
        private static void StripAllSocketsInScene()
        {
            BuildingSocket[] allSockets = Object.FindObjectsByType<BuildingSocket>(FindObjectsSortMode.None);
            
            if (allSockets.Length == 0)
            {
                EditorUtility.DisplayDialog("No Components Found", "No BuildingSocket components found in the current scene.", "OK");
                return;
            }
            
            bool proceed = EditorUtility.DisplayDialog(
                "Strip All BuildingSocket Components?",
                $"This will remove {allSockets.Length} BuildingSocket component(s) from the scene.\n\n" +
                "Note: These components are already excluded from builds automatically.\n\n" +
                "Are you sure you want to proceed?",
                "Yes, Strip All",
                "Cancel"
            );
            
            if (!proceed)
                return;
            
            Undo.SetCurrentGroupName("Strip All BuildingSocket Components");
            int group = Undo.GetCurrentGroup();
            
            foreach (BuildingSocket socket in allSockets)
            {
                Undo.DestroyObjectImmediate(socket);
            }
            
            Undo.CollapseUndoOperations(group);
            
            Debug.Log($"Removed {allSockets.Length} BuildingSocket component(s) from scene.");
        }
        
        [MenuItem("Tools/Building Tools/Info: BuildingSocket Components")]
        private static void ShowInfo()
        {
            EditorUtility.DisplayDialog(
                "BuildingSocket Component Info",
                "BuildingSocket components are EDITOR-ONLY and automatically excluded from builds.\n\n" +
                "✓ Safe to leave in scene during development\n" +
                "✓ Will not appear in builds\n" +
                "✓ No performance impact on final game\n\n" +
                "You can manually strip them using:\n" +
                "• 'Strip from Selection' - Remove from selected objects\n" +
                "• 'Strip All in Scene' - Remove from entire scene\n\n" +
                "Useful for cleaning up prefabs before committing to version control.",
                "OK"
            );
        }
    }
}
