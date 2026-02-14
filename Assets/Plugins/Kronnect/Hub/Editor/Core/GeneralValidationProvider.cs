using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    sealed class GeneralValidationProvider : IValidationProvider {

        public void Validate(ValidationContext context) {
            if (context.UrpAsset == null || context.RendererData == null || context.Pipeline == null) {
                return;
            }

            ValidateRenderFeatures(context);
            ValidateAllQualityLevelsRenderFeatures(context);
            ValidateDepthTexture(context);
            ValidateCopyDepthMode(context);
            ValidateRendererMode(context);
            ValidateAccurateGBufferNormals(context);
            ValidateVolumeExists(context);
            ValidateVolumeProfile(context);
            ValidateVolumeComponent(context);
            ValidateSceneComponent(context);
        }

        public void OnValidationComplete(ValidationContext context) {
        }

        void ValidateDepthTexture(ValidationContext context) {
            if (!context.Asset.RequiresDepthTexture) {
                return;
            }

            UniversalRenderPipelineAsset urpAsset = context.UrpAsset;
            bool valid = context.Pipeline.SupportsCameraDepthTexture;
            context.Report.Add(
                "Check Depth Texture is enabled in URP asset",
                valid,
                "Checks that depth texture is enabled in the URP asset. Required for this effect to render correctly. " +
                "Without it, the effect may show artifacts or fail to render.",
                valid ? null : (Action)(() => {
                    Undo.RecordObject(urpAsset, "Enable Depth Texture");
                    urpAsset.supportsCameraDepthTexture = true;
                    EditorUtility.SetDirty(urpAsset);
                    AssetDatabase.SaveAssets();
                }),
                urpAsset,
                isStillInvalid: () => urpAsset != null && !urpAsset.supportsCameraDepthTexture);
        }

        void ValidateCopyDepthMode(ValidationContext context) {
            if (string.Equals(context.Asset.Id, KronnectAssetIds.Beautify, StringComparison.Ordinal)) {
                return;
            }

            UniversalRendererData rendererData = context.RendererData;
            CopyDepthMode? copyDepthMode = context.Pipeline.CopyDepthMode;
            bool valid = copyDepthMode == CopyDepthMode.AfterOpaques;
            context.Report.Add(
                "Check Depth Texture Mode is set to After Opaques",
                valid,
                "Checks that depth is copied after opaque objects render, ensuring complete depth data for effects. " +
                "Wrong mode may cause fog or screen-space effects to clip through geometry.",
                valid ? null : (Action)(() => {
                    Undo.RecordObject(rendererData, "Set Depth Texture Mode");
                    rendererData.copyDepthMode = CopyDepthMode.AfterOpaques;
                    EditorUtility.SetDirty(rendererData);
                    AssetDatabase.SaveAssets();
                }),
                rendererData,
                isStillInvalid: () => rendererData != null && rendererData.copyDepthMode != CopyDepthMode.AfterOpaques);
        }

        void ValidateRendererMode(ValidationContext context) {
            if (!context.Asset.RequiredRendererMode.HasValue) {
                return;
            }

            RenderingMode? rendererMode = context.Pipeline.RendererMode;
            bool valid = rendererMode == context.Asset.RequiredRendererMode.Value;
            string requiredMode = context.Asset.RequiredRendererMode.Value.ToString();
            UniversalRendererData rendererData = context.RendererData;
            RenderingMode targetMode = context.Asset.RequiredRendererMode.Value;
            context.Report.Add(
                $"Check Rendering Path is set to {requiredMode}",
                valid,
                $"Checks that rendering path is set to '{requiredMode}'. This asset requires {requiredMode} mode. " +
                $"Wrong mode may cause the effect to not render or produce incorrect results.",
                valid ? null : (Action)(() => {
                    UrpUtility.SetRenderingMode(rendererData, targetMode);
                    AssetDatabase.SaveAssets();
                }),
                rendererData);
        }

        void ValidateAccurateGBufferNormals(ValidationContext context) {
            RenderingMode? rendererMode = context.Pipeline.RendererMode;
            if (!rendererMode.HasValue || rendererMode.Value != RenderingMode.Deferred) {
                return;
            }

            bool? accurateGBufferNormals = context.Pipeline.AccurateGBufferNormals;
            bool isEnabled = accurateGBufferNormals ?? false;
            
            if (isEnabled) {
                return;
            }
            
            context.Report.AddOptionalAction(
                "(Optional) Enable Accurate G-Buffer Normals for better quality",
                "Accurate G-Buffer Normals improves normal precision in Deferred rendering at a small performance cost. " +
                "This is recommended for better visual quality with lighting and post-processing effects. " +
                "Note: This is an optional optimization - the effects will work without it.",
                () => {
                    Undo.RecordObject(context.RendererData, "Enable Accurate G-Buffer Normals");
                    context.RendererData.accurateGbufferNormals = true;
                    EditorUtility.SetDirty(context.RendererData);
                    AssetDatabase.SaveAssets();
                },
                context.RendererData,
                null,
                "Enable");
        }

        void ValidateRenderFeatures(ValidationContext context) {
            string[] featureTypes = context.Asset.RenderFeatureTypeNames ?? Array.Empty<string>();
            if (featureTypes.Length == 0) return;

            string assetName = context.Asset.DisplayName;
            
            if (context.Pipeline == null || context.RendererData == null) {
                context.Report.Add(
                    $"Check {assetName} Render Feature is added to the Renderer",
                    false,
                    $"Checks that the {assetName} render feature is added to the Universal Renderer. " +
                    $"Cannot validate - URP renderer is not configured.",
                    showTarget: context.UrpAsset);
                return;
            }

            bool allFeaturesPresent = true;
            foreach (string typeName in featureTypes) {
                if (!context.Pipeline.HasRenderFeature(typeName)) {
                    allFeaturesPresent = false;
                    break;
                }
            }

            context.Report.Add(
                $"Check {assetName} Render Feature is added to the Renderer",
                allFeaturesPresent,
                $"Checks that the {assetName} render feature is added to the Universal Renderer. " +
                $"Without this render feature, the effect will not render.",
                allFeaturesPresent ? null : (Action)(() => {
                    foreach (string typeName in featureTypes) {
                        if (!context.Pipeline.HasRenderFeature(typeName)) {
                            UrpUtility.TryAddRenderFeature(context.RendererData, typeName);
                        }
                    }
                }),
                showTarget: context.RendererData);
        }

        void ValidateAllQualityLevelsRenderFeatures(ValidationContext context) {
            string[] featureTypes = context.Asset.RenderFeatureTypeNames ?? Array.Empty<string>();
            if (featureTypes.Length == 0) return;

            string assetName = context.Asset.DisplayName;
            int qualityLevelCount = QualitySettings.count;
            string[] qualityNames = QualitySettings.names;
            System.Collections.Generic.List<string> missingLevels = new System.Collections.Generic.List<string>();

            for (int i = 0; i < qualityLevelCount; i++) {
                RenderPipelineAsset pipelineAsset = QualitySettings.GetRenderPipelineAssetAt(i);
                UniversalRenderPipelineAsset urpAsset = pipelineAsset as UniversalRenderPipelineAsset;
                
                if (urpAsset == null) {
                    missingLevels.Add(qualityNames[i]);
                    continue;
                }

                UrpUtility.PipelineState pipeline = UrpUtility.GetPipelineState(urpAsset);
                if (pipeline == null || pipeline.DefaultRendererData == null) {
                    missingLevels.Add(qualityNames[i]);
                    continue;
                }

                foreach (string typeName in featureTypes) {
                    if (!pipeline.HasRenderFeature(typeName)) {
                        missingLevels.Add(qualityNames[i]);
                        break;
                    }
                }
            }

            if (missingLevels.Count > 0) {
                string levelsList = string.Join(", ", missingLevels);
                context.Report.AddWarning(
                    $"Some Quality Levels lack the {assetName} Render Feature",
                    $"The following quality levels don't have a URP asset with the required {assetName} render feature: {levelsList}. " +
                    $"If the application runs using one of these quality levels, the effect won't be visible. " +
                    $"Please manually assign the render feature to each quality level's URP renderer.",
                    () => SettingsService.OpenProjectSettings("Project/Quality"));
            }
        }

        void ValidateVolumeExists(ValidationContext context) {
            if (string.IsNullOrEmpty(context.Asset.VolumeComponentType)) {
                return;
            }

            if (string.Equals(context.Asset.Id, KronnectAssetIds.Beautify, StringComparison.Ordinal)) {
                return;
            }

            Volume volume = VolumeUtility.FindGlobalVolume();
            bool valid = volume != null;
            context.Report.Add(
                "Check a global Volume exists in the scene",
                valid,
                "Checks that a global Volume exists in the scene. Volumes control post-processing settings. " +
                "Without it, volume-based effects cannot be configured.",
                valid ? null : (Action)(() => {
                    VolumeUtility.EnsureGlobalVolume();
                }),
                volume != null ? volume.gameObject : null,
                isStillInvalid: () => VolumeUtility.FindGlobalVolume() == null);
        }

        void ValidateVolumeProfile(ValidationContext context) {
            if (string.IsNullOrEmpty(context.Asset.VolumeComponentType)) {
                return;
            }

            if (string.Equals(context.Asset.Id, KronnectAssetIds.Beautify, StringComparison.Ordinal)) {
                return;
            }

            Volume volume = VolumeUtility.FindGlobalVolume();
            bool valid = volume != null && volume.sharedProfile != null;

            context.Report.Add(
                "Check the Volume has a profile assigned",
                valid,
                "Checks that the Volume has a profile assigned. The profile stores effect settings. " +
                "Without it, no volume-based effects will be active.",
                valid ? null : (Action)(() => {
                    Volume vol = VolumeUtility.FindGlobalVolume();
                    if (vol == null) {
                        vol = VolumeUtility.EnsureGlobalVolume();
                    }
                    if (vol != null && vol.sharedProfile == null) {
                        VolumeProfile newProfile = VolumeUtility.EnsureVolumeProfileAsset();
                        if (newProfile != null) {
                            Undo.RecordObject(vol, "Assign Volume Profile");
                            vol.sharedProfile = newProfile;
                            EditorUtility.SetDirty(vol);
                        }
                    }
                }),
                volume != null ? volume.gameObject : null,
                isStillInvalid: () => {
                    Volume vol = VolumeUtility.FindGlobalVolume();
                    return vol == null || vol.sharedProfile == null;
                });
        }

        void ValidateVolumeComponent(ValidationContext context) {
            if (string.IsNullOrEmpty(context.Asset.VolumeComponentType)) {
                return;
            }

            if (string.Equals(context.Asset.Id, KronnectAssetIds.Beautify, StringComparison.Ordinal)) {
                return;
            }

            if (!TypeUtility.TryGetType(context.Asset.VolumeComponentType, out Type componentType)) {
                return;
            }

            string componentName = componentType.Name;
            Volume volume = VolumeUtility.FindGlobalVolume();
            VolumeProfile profile = volume != null ? volume.sharedProfile : null;
            bool valid = profile != null && profile.components.Any(c => c != null && c.GetType() == componentType);

            context.Report.Add(
                $"Check {componentName} override exists in Volume Profile",
                valid,
                $"Checks that '{componentName}' override is added to the Volume Profile. " +
                $"Without it, this effect will not be active.",
                valid ? null : (Action)(() => {
                    EnsureVolumeComponent(context.Asset.VolumeComponentType);
                }),
                profile);
        }


        static void EnsureVolumeComponent(string volumeComponentTypeName) {
            if (!TypeUtility.TryGetType(volumeComponentTypeName, out Type componentType)) {
                return;
            }

            Volume volume = VolumeUtility.FindGlobalVolume();
            if (volume == null) {
                volume = VolumeUtility.EnsureGlobalVolume();
            }
            if (volume == null) {
                return;
            }

            VolumeProfile profile = volume.sharedProfile;
            if (profile == null) {
                profile = VolumeUtility.EnsureVolumeProfileAsset();
                if (profile != null) {
                    Undo.RecordObject(volume, "Assign Volume Profile");
                    volume.sharedProfile = profile;
                    EditorUtility.SetDirty(volume);
                }
            }
            if (profile == null) {
                return;
            }

            bool hasComponent = profile.components.Any(c => c != null && c.GetType() == componentType);
            if (!hasComponent) {
                Undo.RecordObject(profile, $"Add {componentType.Name} Override");
                VolumeComponent component = profile.Add(componentType, true);
                component.SetAllOverridesTo(true);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
        }

        void ValidateSceneComponent(ValidationContext context) {
            if (string.IsNullOrEmpty(context.Asset.SceneComponentType)) {
                return;
            }

            if (string.Equals(context.Asset.Id, KronnectAssetIds.HighlightPlus, StringComparison.Ordinal) ||
                string.Equals(context.Asset.Id, KronnectAssetIds.LiquidVolume, StringComparison.Ordinal) ||
                string.Equals(context.Asset.Id, KronnectAssetIds.LiquidVolumePro, StringComparison.Ordinal) ||
                string.Equals(context.Asset.Id, KronnectAssetIds.MystifyFX, StringComparison.Ordinal)) {
                return;
            }

            if (!TypeUtility.TryGetType(context.Asset.SceneComponentType, out Type componentType)) {
                return;
            }

            string componentName = componentType.Name;
            Component foundComponent = UnityEngine.Object.FindObjectsByType(componentType, FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault() as Component;

            bool isOptionalSceneComponent = string.Equals(context.Asset.Id, KronnectAssetIds.VolumetricFog, StringComparison.Ordinal) ||
                                            string.Equals(context.Asset.Id, KronnectAssetIds.DynamicFog, StringComparison.Ordinal) ||
                                            string.Equals(context.Asset.Id, KronnectAssetIds.VolumetricLights, StringComparison.Ordinal);

            if (isOptionalSceneComponent) {
                if (foundComponent == null) {
                    Action optionalFixAction = null;
                    string description = $"Add a '{componentName}' component to the scene to see the effect.";
                    if (string.Equals(context.Asset.Id, KronnectAssetIds.VolumetricFog, StringComparison.Ordinal)) {
                        optionalFixAction = () => EnsureVolumetricFogSceneComponent(componentType);
                        description = $"Add a '{componentName}' component to the scene to see the fog effect. This creates a sample fog volume you can customize.";
                    } else if (string.Equals(context.Asset.Id, KronnectAssetIds.DynamicFog, StringComparison.Ordinal)) {
                        optionalFixAction = () => EnsureDynamicFogSceneComponent(componentType);
                        description = $"Add a '{componentName}' component to the scene to see the fog effect. This creates a sample fog volume you can customize.";
                    } else if (string.Equals(context.Asset.Id, KronnectAssetIds.VolumetricLights, StringComparison.Ordinal)) {
                        optionalFixAction = () => EnsureVolumetricLightsSceneComponent(componentType);
                        description = $"Add a '{componentName}' component to a light in the scene to see the volumetric effect. This creates a sample spotlight you can customize.";
                    }
                    context.Report.AddOptionalAction(
                        $"(Optional) Add {componentName} component to the scene",
                        description,
                        optionalFixAction,
                        null,
                        null,
                        "Add");
                }
                return;
            }

            bool valid = foundComponent != null;
            Action fixAction = null;
            if (!valid) {
                if (string.Equals(context.Asset.Id, KronnectAssetIds.Umbra, StringComparison.Ordinal)) {
                    fixAction = () => EnsureUmbraSceneComponent(componentType);
                } else if (string.Equals(context.Asset.Id, KronnectAssetIds.GlobalSnow, StringComparison.Ordinal)) {
                    fixAction = () => EnsureGlobalSnowSceneComponent(componentType);
                }
            }

            UnityEngine.Object showTarget = foundComponent != null ? foundComponent.gameObject : null;
            context.Report.Add(
                $"Add {componentName} component to the scene",
                valid,
                $"A '{componentName}' component is needed in the scene to configure and control the effect. " +
                $"Without it, the effect won't render.",
                fixAction,
                showTarget);
        }

        static void EnsureUmbraSceneComponent(Type componentType) {
            if (componentType == null) {
                return;
            }

            Component existing = UnityEngine.Object.FindObjectsByType(componentType, FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault() as Component;
            if (existing != null) {
                return;
            }

            Light directionalLight = FindOrCreateDirectionalLight();
            if (directionalLight == null) {
                return;
            }

            if (directionalLight.GetComponent(componentType) != null) {
                return;
            }

            Undo.AddComponent(directionalLight.gameObject, componentType);
            EditorUtility.SetDirty(directionalLight);
            EditorSceneManager.MarkSceneDirty(directionalLight.gameObject.scene);
        }

        static void EnsureGlobalSnowSceneComponent(Type componentType) {
            if (componentType == null) {
                return;
            }

            Component existing = UnityEngine.Object.FindObjectsByType(componentType, FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault() as Component;
            if (existing != null) {
                return;
            }

            Camera mainCamera = FindOrCreateMainCamera();
            if (mainCamera == null) {
                return;
            }

            if (mainCamera.GetComponent(componentType) != null) {
                return;
            }

            Undo.AddComponent(mainCamera.gameObject, componentType);
            EditorUtility.SetDirty(mainCamera);
            EditorSceneManager.MarkSceneDirty(mainCamera.gameObject.scene);
        }

        static void EnsureVolumetricFogSceneComponent(Type componentType) {
            if (componentType == null) {
                return;
            }

            Component existing = UnityEngine.Object.FindObjectsByType(componentType, FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault() as Component;
            if (existing != null) {
                return;
            }

            if (!TypeUtility.TryGetType("VolumetricFogAndMist2.VolumetricFogManager", out Type managerType)) {
                return;
            }

            System.Reflection.MethodInfo createMethod = managerType.GetMethod("CreateFogVolume", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (createMethod == null) {
                return;
            }

            GameObject fogVolume = createMethod.Invoke(null, new object[] { "Volumetric Fog Volume" }) as GameObject;
            if (fogVolume == null) {
                return;
            }

            Undo.RegisterCreatedObjectUndo(fogVolume, "Create Fog Volume");
            EditorSceneManager.MarkSceneDirty(fogVolume.scene);
        }

        static void EnsureVolumetricLightsSceneComponent(Type componentType) {
            if (componentType == null) {
                return;
            }

            Component existing = UnityEngine.Object.FindObjectsByType(componentType, FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault() as Component;
            if (existing != null) {
                return;
            }

            GameObject go = new GameObject("Volumetric Spot Light", typeof(Light));
            Light light = go.GetComponent<Light>();
            light.type = LightType.Spot;
            
            Camera sceneCamera = SceneView.lastActiveSceneView?.camera;
            if (sceneCamera != null) {
                go.transform.position = sceneCamera.transform.TransformPoint(Vector3.forward * 50f);
            }

            Undo.AddComponent(go, componentType);
            Undo.RegisterCreatedObjectUndo(go, "Create Volumetric Spot Light");
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeObject = go;
        }

        static void EnsureDynamicFogSceneComponent(Type componentType) {
            if (componentType == null) {
                return;
            }

            Component existing = UnityEngine.Object.FindObjectsByType(componentType, FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault() as Component;
            if (existing != null) {
                return;
            }

            if (!TypeUtility.TryGetType("DynamicFogAndMist2.DynamicFogManager", out Type managerType)) {
                return;
            }

            System.Reflection.MethodInfo createMethod = managerType.GetMethod("CreateFogVolume",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (createMethod == null) {
                return;
            }

            GameObject fogVolume = createMethod.Invoke(null, new object[] { "Dynamic Fog Volume" }) as GameObject;
            if (fogVolume == null) {
                return;
            }

            fogVolume.transform.position = Vector3.zero;
            fogVolume.transform.localScale = new Vector3(5000, 5000, 5000);

            Undo.RegisterCreatedObjectUndo(fogVolume, "Create Dynamic Fog Volume");
            EditorSceneManager.MarkSceneDirty(fogVolume.scene);
            Selection.activeObject = fogVolume;
        }

        static Camera FindOrCreateMainCamera() {
            Camera mainCamera = Camera.main;
            if (mainCamera != null) {
                return mainCamera;
            }

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (cameras.Length > 0) {
                return cameras[0];
            }

            GameObject go = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(go, "Create Main Camera");
            Camera created = Undo.AddComponent<Camera>(go);
            go.tag = "MainCamera";
            EditorUtility.SetDirty(created);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return created;
        }

        static Light FindOrCreateDirectionalLight() {
            Light light = GetExistingDirectionalLight();
            if (light != null) {
                return light;
            }

            GameObject go = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(go, "Create Directional Light");
            Light created = Undo.AddComponent<Light>(go);
            created.type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            EditorUtility.SetDirty(created);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return created;
        }

        static Light GetExistingDirectionalLight() {
            Light sun = RenderSettings.sun;
            if (sun != null && sun.type == LightType.Directional) {
                return sun;
            }

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Light light in lights) {
                if (light != null && light.type == LightType.Directional) {
                    return light;
                }
            }
            return null;
        }

    }

}

