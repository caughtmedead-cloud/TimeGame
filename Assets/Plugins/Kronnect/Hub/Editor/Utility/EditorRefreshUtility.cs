using UnityEditor;

namespace Kronnect.Hub {

    internal static class EditorRefreshUtility {

        public static void RefreshAllViews() {
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

    }

}

