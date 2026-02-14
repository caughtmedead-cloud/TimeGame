using System;
using System.Collections.Generic;
using UnityEditor;

namespace Kronnect.Hub {

    internal sealed class AssetRuntimeState {
        static readonly Dictionary<string, AssetRuntimeState> States = new Dictionary<string, AssetRuntimeState>();
        bool? _cachedInstall;
        string _cachedFolder;
        double _lastCheckTime;
        readonly AssetIntegration _asset;

        AssetRuntimeState(AssetIntegration asset) {
            _asset = asset;
        }

        public static AssetRuntimeState Get(AssetIntegration asset) {
            if (!States.TryGetValue(asset.Id, out AssetRuntimeState state)) {
                state = new AssetRuntimeState(asset);
                States[asset.Id] = state;
            }
            return state;
        }

        public string InstalledFolder {
            get {
                // Ensure cache is populated by accessing IsInstalled
                _ = IsInstalled;
                return _cachedFolder;
            }
        }

        public bool IsInstalled {
            get {
                double now = EditorApplication.timeSinceStartup;
                if (_cachedInstall.HasValue && now - _lastCheckTime < 1.0) {
                    return _cachedInstall.Value;
                }
                
                _cachedFolder = null;
                string typeName = _asset.VolumeComponentType ?? _asset.SceneComponentType;
                bool typeExists = TypeUtility.TryGetType(typeName, out Type detectedType);
                
                if (_asset.IsBundle) {
                    string installedFolder = _asset.InstalledFolder;
                    bool installedFolderExists = !string.IsNullOrEmpty(installedFolder) && AssetDatabase.IsValidFolder(installedFolder);
                    if (installedFolderExists) {
                        _cachedFolder = installedFolder;
                    }
                    _cachedInstall = installedFolderExists || typeExists;
                } else {
                    bool folderExists = !string.IsNullOrEmpty(_asset.AssetFolder) && AssetDatabase.IsValidFolder(_asset.AssetFolder);
                    if (folderExists) {
                        _cachedFolder = _asset.AssetFolder;
                    }
                    _cachedInstall = folderExists || typeExists;
                }
                
                // Check RequiredTypeName - must exist for asset to be installed
                if (_cachedInstall.Value && !string.IsNullOrEmpty(_asset.RequiredTypeName)) {
                    _cachedInstall = TypeUtility.TryGetType(_asset.RequiredTypeName, out _);
                }
                
                // Check ExcludedTypeName - must NOT exist for asset to be installed
                if (_cachedInstall.Value && !string.IsNullOrEmpty(_asset.ExcludedTypeName)) {
                    _cachedInstall = !TypeUtility.TryGetType(_asset.ExcludedTypeName, out _);
                }
                
                // If installed via type detection but folder not found, find folder from type
                if (_cachedInstall.Value && _cachedFolder == null && detectedType != null) {
                    _cachedFolder = FindFolderFromType(detectedType, _asset);
                }
                
                _lastCheckTime = now;
                return _cachedInstall.Value;
            }
        }

        public void InvalidateCache() {
            _cachedInstall = null;
            _cachedFolder = null;
        }

        static string FindFolderFromType(Type type, AssetIntegration asset) {
            var script = FindScriptFromType(type);
            if (script == null) return null;
            
            string scriptPath = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(scriptPath)) return null;
            
            // Get expected folder names from asset definition
            string expectedFolderName = null;
            if (!string.IsNullOrEmpty(asset.InstalledFolder)) {
                expectedFolderName = System.IO.Path.GetFileName(asset.InstalledFolder);
            }
            if (string.IsNullOrEmpty(expectedFolderName) && !string.IsNullOrEmpty(asset.AssetFolder)) {
                expectedFolderName = System.IO.Path.GetFileName(asset.AssetFolder);
            }
            
            // Walk up directories looking for a folder matching the expected name
            string folder = System.IO.Path.GetDirectoryName(scriptPath);
            while (!string.IsNullOrEmpty(folder) && folder != "Assets") {
                string folderName = System.IO.Path.GetFileName(folder);
                
                // Check if this folder matches the expected asset folder name
                if (!string.IsNullOrEmpty(expectedFolderName) && 
                    string.Equals(folderName, expectedFolderName, StringComparison.OrdinalIgnoreCase)) {
                    return folder.Replace('\\', '/');
                }
                
                string parent = System.IO.Path.GetDirectoryName(folder);
                if (string.IsNullOrEmpty(parent) || parent == "Assets") {
                    break;
                }
                folder = parent;
            }
            
            // Fallback: return the folder containing the script's parent (typically 2 levels up)
            // This handles cases like: Assets/Plugins/MyAsset/Scripts/MyScript.cs -> Assets/Plugins/MyAsset
            string scriptFolder = System.IO.Path.GetDirectoryName(scriptPath);
            if (!string.IsNullOrEmpty(scriptFolder) && scriptFolder != "Assets") {
                string parentFolder = System.IO.Path.GetDirectoryName(scriptFolder);
                // Only return parent if it's a valid subfolder (not Assets root)
                if (!string.IsNullOrEmpty(parentFolder) && parentFolder != "Assets") {
                    return parentFolder.Replace('\\', '/');
                }
                // Return script folder only if it's not the Assets root
                return scriptFolder.Replace('\\', '/');
            }
            
            return null;
        }

        static MonoScript FindScriptFromType(Type type) {
            string[] guids = AssetDatabase.FindAssets($"t:MonoScript {type.Name}");
            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type) {
                    return script;
                }
            }
            return null;
        }
    }

}

