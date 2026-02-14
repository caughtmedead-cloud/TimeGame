using UnityEditor;

namespace Kronnect.Hub {

    static class AssetDatabaseUtility {
        public static void EnsureFolder(string folderPath) {
            if (AssetDatabase.IsValidFolder(folderPath)) {
                return;
            }
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++) {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }

}

