using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    sealed class GlobalValidationProvider {

        public void Validate(ValidationReport report) {
            ValidateUrpAsset(report);
            ValidateQualityUrp(report);
            ValidateRendererData(report);
            ValidateInputSystemEventSystem(report);
        }

        void ValidateUrpAsset(ValidationReport report) {
            UniversalRenderPipelineAsset urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            bool valid = urpAsset != null;
            report.Add(
                "Check URP asset is assigned in Project Settings / Graphics",
                valid,
                "Checks that a URP asset is assigned in Project Settings > Graphics. This is the default or fallback URP used if none is set in Quality Settings. " +
                "Without a URP asset assigned, no post-processing or custom render features will work.",
                valid ? null : (Action)(() => {
                    UniversalRenderPipelineAsset assetToAssign = FindOrCreateUrpAsset();
                    if (assetToAssign != null) {
                        GraphicsSettings.defaultRenderPipeline = assetToAssign;
                        EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                }),
                showAction: () => SettingsService.OpenProjectSettings("Project/Graphics"),
                isStillInvalid: () => GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset == null);
        }

        void ValidateQualityUrp(ValidationReport report) {
            UniversalRenderPipelineAsset qualityAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            bool valid = qualityAsset != null;
            report.Add(
                "Check URP asset is assigned in Project Settings / Quality",
                valid,
                "Checks that the active Quality Level has a URP asset assigned. " +
                "Without it, effects may not appear or behave incorrectly on this quality tier.",
                valid ? null : (Action)(() => {
                    UniversalRenderPipelineAsset urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                    if (urpAsset == null) {
                        urpAsset = FindOrCreateUrpAsset();
                    }
                    if (urpAsset != null) {
                        QualitySettings.renderPipeline = urpAsset;
                        UnityEngine.Object qualitySettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
                        if (qualitySettings != null) {
                            EditorUtility.SetDirty(qualitySettings);
                        }
                        AssetDatabase.SaveAssets();
                    }
                }),
                showAction: () => SettingsService.OpenProjectSettings("Project/Quality"),
                isStillInvalid: () => QualitySettings.renderPipeline as UniversalRenderPipelineAsset == null);
        }

        void ValidateRendererData(ValidationReport report) {
            UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            UrpUtility.PipelineState pipeline = UrpUtility.GetPipelineState(urpAsset);
            UniversalRendererData rendererData = pipeline?.DefaultRendererData as UniversalRendererData;
            
            bool valid = rendererData != null;
            report.Add(
                "Check URP asset contains a valid Renderer",
                valid,
                "Checks that the URP asset has a Universal Renderer Data assigned. " +
                "Required to add render features. Without it, effects won't execute.",
                valid ? null : (Action)(() => {
                    UniversalRenderPipelineAsset activeUrp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                    if (activeUrp == null) return;
                    
                    UniversalRendererData renderer = FindOrCreateRendererForUrpAsset(activeUrp);
                    if (renderer != null) {
                        AssignRendererToUrpAsset(activeUrp, renderer);
                    }
                }),
                showTarget: (UnityEngine.Object)rendererData ?? urpAsset,
                isStillInvalid: () => {
                    UniversalRenderPipelineAsset activeUrp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                    if (activeUrp == null) return true;
                    UrpUtility.PipelineState state = UrpUtility.GetPipelineState(activeUrp);
                    return state?.DefaultRendererData == null;
                });
        }

        void ValidateInputSystemEventSystem(ValidationReport report) {
            // Check if project uses New Input System
            if (!IsUsingNewInputSystem()) {
                return; // Skip validation if not using new input system
            }

            // Find EventSystem with old StandaloneInputModule
            var eventSystemWithOldInput = FindEventSystemWithStandaloneInputModule();
            if (eventSystemWithOldInput == null) {
                return; // No problem found, skip adding validation entry
            }

            report.Add(
                "Check EventSystem uses New Input System module",
                false,
                "The project is configured to use the New Input System, but an EventSystem in the scene is using the old StandaloneInputModule. " +
                "This can cause UI interactions to not work correctly. The fix will replace StandaloneInputModule with InputSystemUIInputModule.",
                (Action)(() => {
                    var eventSystem = FindEventSystemWithStandaloneInputModule();
                    if (eventSystem != null) {
                        ReplaceStandaloneInputModuleWithNewInputSystem(eventSystem);
                    }
                }),
                showTarget: eventSystemWithOldInput,
                isStillInvalid: () => FindEventSystemWithStandaloneInputModule() != null);
        }

        static bool IsUsingNewInputSystem() {
#if ENABLE_INPUT_SYSTEM
            return true;
#else
            return false;
#endif
        }

        static GameObject FindEventSystemWithStandaloneInputModule() {
            // Find all EventSystems in the scene
            var eventSystems = UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
            
            foreach (var eventSystem in eventSystems) {
                // Check if it has StandaloneInputModule (old input system)
                var standaloneModule = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (standaloneModule != null) {
                    return eventSystem.gameObject;
                }
            }
            
            return null;
        }

        static void ReplaceStandaloneInputModuleWithNewInputSystem(GameObject eventSystemObject) {
            // Remove old StandaloneInputModule
            var standaloneModule = eventSystemObject.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standaloneModule != null) {
                Undo.DestroyObjectImmediate(standaloneModule);
            }

            // Add new InputSystemUIInputModule if not already present
            Type inputSystemUIModuleType = FindInputSystemUIModuleType();
            if (inputSystemUIModuleType != null) {
                var existingModule = eventSystemObject.GetComponent(inputSystemUIModuleType);
                if (existingModule == null) {
                    ObjectFactory.AddComponent(eventSystemObject, inputSystemUIModuleType);
                }
            } else {
                Debug.LogWarning("Could not find InputSystemUIInputModule type. Make sure the Input System package is installed.");
            }

            EditorUtility.SetDirty(eventSystemObject);
        }

        static Type FindInputSystemUIModuleType() {
            // Try different assembly names the Input System might use
            string[] possibleTypeNames = {
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem",
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, UnityEngine.InputSystem",
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, UnityEngine.InputSystem.UI"
            };

            foreach (string typeName in possibleTypeNames) {
                if (TypeUtility.TryGetType(typeName, out Type foundType)) {
                    return foundType;
                }
            }

            // Fallback: search all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                try {
                    Type type = assembly.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
                    if (type != null) {
                        return type;
                    }
                } catch {
                    // Ignore assembly load errors
                }
            }

            return null;
        }

        static UniversalRendererData FindOrCreateRendererForUrpAsset(UniversalRenderPipelineAsset urpAsset) {
            string urpAssetPath = AssetDatabase.GetAssetPath(urpAsset);
            if (string.IsNullOrEmpty(urpAssetPath)) return null;
            
            string folder = Path.GetDirectoryName(urpAssetPath);
            string urpAssetName = Path.GetFileNameWithoutExtension(urpAssetPath);
            
            // Determine expected renderer name based on URP asset name
            string expectedRendererName = null;
            if (urpAssetName == "PC_RPAsset") {
                expectedRendererName = "PC_Renderer";
            } else if (urpAssetName == "Mobile_RPAsset") {
                expectedRendererName = "Mobile_Renderer";
            }
            
            // Try to find the expected renderer first
            if (!string.IsNullOrEmpty(expectedRendererName)) {
                string expectedRendererPath = $"{folder}/{expectedRendererName}.asset";
                UniversalRendererData expectedRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(expectedRendererPath);
                if (expectedRenderer != null) {
                    return expectedRenderer;
                }
            }
            
            // Search for any renderer in the same folder
            string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData", new[] { folder });
            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (renderer != null) {
                    return renderer;
                }
            }
            
            // Create a new renderer in the same folder
            string newRendererName = !string.IsNullOrEmpty(expectedRendererName) ? expectedRendererName : "UniversalRenderer";
            string newRendererPath = $"{folder}/{newRendererName}.asset";
            
            UniversalRendererData newRenderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(newRenderer, newRendererPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Created new Universal Renderer at {newRendererPath}");
            return newRenderer;
        }

        static void AssignRendererToUrpAsset(UniversalRenderPipelineAsset urpAsset, UniversalRendererData rendererData) {
            SerializedObject so = new SerializedObject(urpAsset);
            SerializedProperty renderersProp = so.FindProperty("m_RendererDataList");
            
            if (renderersProp != null && renderersProp.isArray) {
                renderersProp.arraySize = 1;
                SerializedProperty element = renderersProp.GetArrayElementAtIndex(0);
                element.objectReferenceValue = rendererData;
                so.ApplyModifiedProperties();
            }
            
            SerializedProperty defaultRendererProp = so.FindProperty("m_DefaultRendererIndex");
            if (defaultRendererProp != null) {
                defaultRendererProp.intValue = 0;
                so.ApplyModifiedProperties();
            }
            
            EditorUtility.SetDirty(urpAsset);
            AssetDatabase.SaveAssets();
        }

        static UniversalRenderPipelineAsset FindOrCreateUrpAsset() {
            const string setupFolder = "Assets/Settings";
            
            // First, try to find existing URP assets in the Settings folder
            UniversalRenderPipelineAsset existingAsset = FindExistingUrpAsset(setupFolder);
            if (existingAsset != null) {
                return existingAsset;
            }
            
            // If no existing asset found, create a new one
            return CreateUrpAssetInSetupFolder();
        }

        static UniversalRenderPipelineAsset FindExistingUrpAsset(string folder) {
            if (!AssetDatabase.IsValidFolder(folder)) {
                return null;
            }

            // Determine platform-specific asset name preference
            bool isMobilePlatform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ||
                                    EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
            
            string preferredAssetName = isMobilePlatform ? "Mobile_RPAsset" : "PC_RPAsset";
            string preferredPath = $"{folder}/{preferredAssetName}.asset";
            
            // Try platform-specific asset first
            UniversalRenderPipelineAsset preferredAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(preferredPath);
            if (preferredAsset != null) {
                return preferredAsset;
            }
            
            // Try the other platform asset as fallback
            string fallbackAssetName = isMobilePlatform ? "PC_RPAsset" : "Mobile_RPAsset";
            string fallbackPath = $"{folder}/{fallbackAssetName}.asset";
            UniversalRenderPipelineAsset fallbackAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(fallbackPath);
            if (fallbackAsset != null) {
                return fallbackAsset;
            }
            
            // Search for any URP asset in the folder
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { folder });
            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset != null) {
                    return asset;
                }
            }
            
            return null;
        }

        static UniversalRenderPipelineAsset CreateUrpAssetInSetupFolder() {
            const string setupFolder = "Assets/Settings";
            const string rendererPath = "Assets/Settings/UniversalRenderer.asset";
            const string urpAssetPath = "Assets/Settings/UniversalRenderPipelineAsset.asset";
            
            UniversalRenderPipelineAsset existingUrp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpAssetPath);
            if (existingUrp != null) {
                return existingUrp;
            }
            
            AssetDatabaseUtility.EnsureFolder(setupFolder);
            
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null) {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, rendererPath);
            }
            
            UniversalRenderPipelineAsset urpAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            AssetDatabase.CreateAsset(urpAsset, urpAssetPath);
            
            SerializedObject so = new SerializedObject(urpAsset);
            SerializedProperty renderersProp = so.FindProperty("m_RendererDataList");
            if (renderersProp != null && renderersProp.isArray) {
                renderersProp.arraySize = 1;
                SerializedProperty element = renderersProp.GetArrayElementAtIndex(0);
                element.objectReferenceValue = rendererData;
                so.ApplyModifiedProperties();
            }
            
            SerializedProperty defaultRendererProp = so.FindProperty("m_DefaultRendererIndex");
            if (defaultRendererProp != null) {
                defaultRendererProp.intValue = 0;
                so.ApplyModifiedProperties();
            }
            
            EditorUtility.SetDirty(urpAsset);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Created new URP Asset at {urpAssetPath}");
            return urpAsset;
        }
    }

}
