using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;

namespace ThelosU6.Editor
{
    public static class PixelMasterVariantCreator
    {
        private const string MASTER_MATERIAL_PATH = "Assets/_Art/Materials/Pixel_Master.mat";

        [MenuItem("Assets/Create/Pixel Master Variant", false, 1)]
        private static void CreatePixelMasterVariant()
        {
            Material masterMaterial = AssetDatabase.LoadAssetAtPath<Material>(MASTER_MATERIAL_PATH);
            
            if (masterMaterial == null)
            {
                Debug.LogError($"Could not find Pixel_Master material at {MASTER_MATERIAL_PATH}");
                return;
            }

            string currentFolder = GetCurrentProjectFolder();
            string variantName = GenerateUniqueVariantName(currentFolder);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(currentFolder, variantName + ".mat"));

            Material variant = new Material(masterMaterial);
            variant.parent = masterMaterial;
            
            ProjectWindowUtil.CreateAsset(variant, assetPath);
        }

        [MenuItem("Tools/Create Pixel Master Variant %#m", false, 0)]
        private static void CreatePixelMasterVariantShortcut()
        {
            CreatePixelMasterVariant();
        }

        private static string GetCurrentProjectFolder()
        {
            string path = "Assets";

            if (Selection.activeObject != null)
            {
                string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    if (Directory.Exists(selectedPath))
                    {
                        return selectedPath;
                    }
                    else if (File.Exists(selectedPath))
                    {
                        return Path.GetDirectoryName(selectedPath);
                    }
                }
            }

            string projectWindowPath = GetActiveFolderPathFromProjectWindow();
            if (!string.IsNullOrEmpty(projectWindowPath))
            {
                return projectWindowPath;
            }

            return path;
        }

        private static string GetActiveFolderPathFromProjectWindow()
        {
            System.Type projectWindowUtilType = typeof(ProjectWindowUtil);
            MethodInfo getActiveFolderPath = projectWindowUtilType.GetMethod(
                "GetActiveFolderPath",
                BindingFlags.Static | BindingFlags.NonPublic
            );

            if (getActiveFolderPath != null)
            {
                object result = getActiveFolderPath.Invoke(null, null);
                if (result != null)
                {
                    return result.ToString();
                }
            }

            return string.Empty;
        }

        private static string GenerateUniqueVariantName(string folder)
        {
            string baseName = "Pixel_Master_Variant";
            string testPath = Path.Combine(folder, baseName + ".mat");
            
            if (!File.Exists(testPath))
            {
                return baseName;
            }

            int counter = 1;
            while (File.Exists(testPath))
            {
                baseName = $"Pixel_Master_Variant_{counter}";
                testPath = Path.Combine(folder, baseName + ".mat");
                counter++;
            }

            return baseName;
        }
    }
}
