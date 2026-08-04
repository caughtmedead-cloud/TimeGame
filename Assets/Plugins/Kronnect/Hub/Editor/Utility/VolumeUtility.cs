using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kronnect.Hub {

    internal static class VolumeUtility {
        public const string VolumeObjectName = "Global Volume";
        public const string VolumeProfilePath = "Assets/Settings/KronnectSetupProfile.asset";
        public const string GeneratedProfilesFolder = "Assets/Settings";

        public static VolumeProfile LoadVolumeProfileAsset() {
            return AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        }

        public static VolumeProfile EnsureVolumeProfileAsset() {
            AssetDatabaseUtility.EnsureFolder(GeneratedProfilesFolder);
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null) {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
                AssetDatabase.SaveAssets();
            }
            return profile;
        }

        public static Volume FindSetupVolume() {
            Volume[] volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Volume existing = volumes.FirstOrDefault(v => v.gameObject != null && v.gameObject.name == VolumeObjectName);
            if (existing != null) {
                return existing;
            }
            GameObject go = GameObject.Find(VolumeObjectName);
            return go != null ? go.GetComponent<Volume>() : null;
        }

        public static Volume EnsureGlobalVolume() {
            Volume[] volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Volume existing = volumes.FirstOrDefault(v => v.isGlobal && v.gameObject.name == VolumeObjectName);
            if (existing != null) {
                return existing;
            }
            GameObject go = GameObject.Find(VolumeObjectName);
            if (go == null) {
                go = new GameObject(VolumeObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Create Global Volume");
            }
            Volume volume = go.GetComponent<Volume>();
            if (volume == null) {
                volume = Undo.AddComponent<Volume>(go);
            }
            volume.isGlobal = true;
            volume.priority = 100f;
            if (volume.sharedProfile == null) {
                VolumeProfile profile = EnsureVolumeProfileAsset();
                if (profile != null) {
                    volume.sharedProfile = profile;
                }
            }
            EditorUtility.SetDirty(volume);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return volume;
        }

        public static VolumeComponent GetVolumeComponent(string typeName) {
            if (!TypeUtility.TryGetType(typeName, out Type type)) {
                return null;
            }

            // Search all volumes in the scene for the component
            Volume[] volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Volume volume in volumes) {
                VolumeProfile profile = volume.sharedProfile;
                if (profile == null) {
                    continue;
                }
                VolumeComponent component = profile.components.FirstOrDefault(c => c != null && c.GetType() == type);
                if (component != null) {
                    return component;
                }
            }

            // Fallback: check the setup profile asset
            VolumeProfile setupProfile = LoadVolumeProfileAsset();
            if (setupProfile != null) {
                return setupProfile.components.FirstOrDefault(c => c != null && c.GetType() == type);
            }

            return null;
        }

        public static VolumeComponent[] GetAllVolumeComponents(string typeName) {
            if (!TypeUtility.TryGetType(typeName, out Type type)) {
                return Array.Empty<VolumeComponent>();
            }

            System.Collections.Generic.List<VolumeComponent> results = new System.Collections.Generic.List<VolumeComponent>();
            System.Collections.Generic.HashSet<VolumeProfile> processedProfiles = new System.Collections.Generic.HashSet<VolumeProfile>();

            Volume[] volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Volume volume in volumes) {
                VolumeProfile profile = volume.sharedProfile;
                if (profile == null || processedProfiles.Contains(profile)) {
                    continue;
                }
                processedProfiles.Add(profile);
                VolumeComponent component = profile.components.FirstOrDefault(c => c != null && c.GetType() == type);
                if (component != null) {
                    results.Add(component);
                }
            }

            // Fallback: check the setup profile asset
            VolumeProfile setupProfile = LoadVolumeProfileAsset();
            if (setupProfile != null && !processedProfiles.Contains(setupProfile)) {
                VolumeComponent component = setupProfile.components.FirstOrDefault(c => c != null && c.GetType() == type);
                if (component != null) {
                    results.Add(component);
                }
            }

            return results.ToArray();
        }

        public static Component FindSceneComponent(string typeName) {
            if (!TypeUtility.TryGetType(typeName, out Type type)) {
                return null;
            }
            return FindSceneComponent(type);
        }

        public static Component FindSceneComponent(Type type) {
            if (type == null) {
                return null;
            }
            return UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault() as Component;
        }

        public static Component[] FindAllSceneComponents(string typeName) {
            if (!TypeUtility.TryGetType(typeName, out Type type)) {
                return Array.Empty<Component>();
            }
            return FindAllSceneComponents(type);
        }

        public static Component[] FindAllSceneComponents(Type type) {
            if (type == null) {
                return Array.Empty<Component>();
            }
            UnityEngine.Object[] objects = UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);
            Component[] components = new Component[objects.Length];
            for (int i = 0; i < objects.Length; i++) {
                components[i] = objects[i] as Component;
            }
            return components;
        }

        public static Volume FindVolumeContaining(VolumeComponent component) {
            if (component == null) {
                return null;
            }
            Volume[] volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Volume volume in volumes) {
                VolumeProfile profile = volume.sharedProfile;
                if (profile != null && profile.components.Contains(component)) {
                    return volume;
                }
            }
            return null;
        }

        public static Volume FindGlobalVolume() {
            Volume[] volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Volume v in volumes) {
                if (v != null && v.isGlobal) {
                    return v;
                }
            }
            return null;
        }

        public static Volume FindVolumeWithComponent(string typeName) {
            if (!TypeUtility.TryGetType(typeName, out Type type)) {
                return null;
            }
            return FindVolumeWithComponent(type);
        }

        public static Volume FindVolumeWithComponent(Type componentType) {
            if (componentType == null) {
                return null;
            }
            Volume[] volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Volume volume in volumes) {
                VolumeProfile profile = volume.sharedProfile;
                if (profile == null) {
                    continue;
                }
                foreach (VolumeComponent c in profile.components) {
                    if (c != null && c.GetType() == componentType) {
                        return volume;
                    }
                }
            }
            return null;
        }

        public static bool TryGetVolumeComponent(string typeName, out VolumeComponent component, out Volume volume) {
            component = null;
            volume = null;
            if (!TypeUtility.TryGetType(typeName, out Type type)) {
                return false;
            }
            Volume[] volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Volume v in volumes) {
                VolumeProfile profile = v.sharedProfile;
                if (profile == null) {
                    continue;
                }
                foreach (VolumeComponent c in profile.components) {
                    if (c != null && c.GetType() == type) {
                        component = c;
                        volume = v;
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool AnyVolumeExists() {
            return UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0;
        }

        public static ScriptableObject CreateScriptableAsset(string fullTypeName, string fileName) {
            if (!TypeUtility.TryGetType(fullTypeName, out Type type)) {
                return null;
            }
            AssetDatabaseUtility.EnsureFolder(GeneratedProfilesFolder);
            string path = Path.Combine(GeneratedProfilesFolder, $"{fileName}.asset").Replace("\\", "/");
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject;
            if (asset != null) {
                return asset;
            }
            asset = ScriptableObject.CreateInstance(type) as ScriptableObject;
            if (asset == null) {
                return null;
            }
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }

}

