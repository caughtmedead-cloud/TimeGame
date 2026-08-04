using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kronnect.Hub {

    internal static class AssetStoreUtility {

        static string _cachedAssetStorePath;

        public static string GetAssetStoreCachePath() {
            if (_cachedAssetStorePath != null) return _cachedAssetStorePath;

#if UNITY_EDITOR_WIN
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _cachedAssetStorePath = Path.Combine(appData, "Unity", "Asset Store-5.x");
#elif UNITY_EDITOR_OSX
            string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            _cachedAssetStorePath = Path.Combine(home, "Library", "Unity", "Asset Store-5.x");
#else
            string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            _cachedAssetStorePath = Path.Combine(home, ".local", "share", "unity3d", "Asset Store-5.x");
#endif
            return _cachedAssetStorePath;
        }

        public static string FindCachedPackage(string publisherName, string assetName) {
            string cachePath = GetAssetStoreCachePath();
            if (string.IsNullOrEmpty(cachePath) || !Directory.Exists(cachePath)) {
                return null;
            }

            string[] searchPatterns = new[] {
                Path.Combine(cachePath, publisherName, assetName + ".unitypackage"),
                Path.Combine(cachePath, publisherName, assetName, "*.unitypackage"),
                Path.Combine(cachePath, publisherName + " *", assetName + ".unitypackage"),
                Path.Combine(cachePath, publisherName + " *", assetName, "*.unitypackage"),
                Path.Combine(cachePath, "* " + publisherName, assetName + ".unitypackage"),
                Path.Combine(cachePath, "* " + publisherName, assetName, "*.unitypackage"),
            };

            foreach (string pattern in searchPatterns) {
                string directory = Path.GetDirectoryName(pattern);
                string filePattern = Path.GetFileName(pattern);
                
                if (Directory.Exists(directory)) {
                    string[] files = Directory.GetFiles(directory, filePattern, SearchOption.TopDirectoryOnly);
                    if (files.Length > 0) {
                        return files[0];
                    }
                }
            }

            try {
                string[] publisherDirs = Directory.GetDirectories(cachePath, "*Kronnect*", SearchOption.TopDirectoryOnly);
                foreach (string pubDir in publisherDirs) {
                    string[] packageFiles = Directory.GetFiles(pubDir, "*.unitypackage", SearchOption.AllDirectories);
                    
                    string exactMatch = null;
                    string partialMatch = null;
                    
                    foreach (string packageFile in packageFiles) {
                        string fileName = Path.GetFileNameWithoutExtension(packageFile);
                        string fileNameLower = fileName.ToLower();
                        
                        if (fileNameLower.Contains("hdrp") || fileNameLower.Contains("builtin") || fileNameLower.Contains("built-in")) {
                            continue;
                        }
                        
                        if (string.Equals(fileName, assetName, StringComparison.OrdinalIgnoreCase)) {
                            exactMatch = packageFile;
                            break;
                        }
                        
                        if (partialMatch == null) {
                            string parentDir = Path.GetFileName(Path.GetDirectoryName(packageFile));
                            if (fileName.IndexOf(assetName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                parentDir.IndexOf(assetName, StringComparison.OrdinalIgnoreCase) >= 0) {
                                partialMatch = packageFile;
                            }
                        }
                    }
                    
                    if (exactMatch != null) return exactMatch;
                    if (partialMatch != null) return partialMatch;
                }
            } catch {
            }

            return null;
        }

        public static bool HasCachedPackage(string publisherName, string assetName) {
            string packagePath = FindCachedPackage(publisherName, assetName);
            return !string.IsNullOrEmpty(packagePath) && File.Exists(packagePath);
        }

        public static void RevealPackageLocation(string packagePath) {
            if (string.IsNullOrEmpty(packagePath)) return;

            try {
                string fullPath = Path.GetFullPath(packagePath);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) {
                    return;
                }
                EditorUtility.RevealInFinder(fullPath);
            } catch (Exception ex) {
                Debug.LogError($"Could not open package location: {ex.Message}");
            }
        }

        public static void OpenAssetStoreUrl(string url) {
            if (string.IsNullOrEmpty(url)) return;
            Application.OpenURL(url);
        }
    }

}
