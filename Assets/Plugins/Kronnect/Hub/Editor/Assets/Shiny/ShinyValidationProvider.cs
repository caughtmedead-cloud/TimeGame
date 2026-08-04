using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    sealed class ShinyValidationProvider : IValidationProvider {
        const string VolumeComponentTypeName = "ShinySSRR.ShinyScreenSpaceRaytracedReflections";
        const string RendererFeatureTypeName = "ShinySSRR.ShinySSRR";
        const string ReflectionsScriptTypeName = "ShinySSRR.Reflections";
        static Type _rendererFeatureType;
        static Type _reflectionsScriptType;

        public void Validate(ValidationContext context) {
            if (!string.Equals(context.Asset.Id, KronnectAssetIds.Shiny, StringComparison.Ordinal)) {
                return;
            }

            ScriptableRendererFeature rendererFeature = FindShinyFeature(context.RendererData);
            ValidateFeatureEnabled(context, rendererFeature);
            ValidateCameraLayerMask(context, rendererFeature);
            ValidateDeferredModeMatch(context, rendererFeature);
            ValidatePostProcessing(context, rendererFeature);
            ValidateReflectionsMultiplier(context);
            ValidateReflectionsScriptsInForward(context, rendererFeature);
            AddSwitchToDeferredOption(context, rendererFeature);
        }

        void ValidateFeatureEnabled(ValidationContext context, ScriptableRendererFeature rendererFeature) {
            if (rendererFeature == null) {
                context.Report.Add(
                    "Check Shiny render feature is enabled",
                    false,
                    "Cannot validate - Shiny render feature not found. Add the render feature first.",
                    showTarget: context.RendererData);
                return;
            }

            bool valid = rendererFeature.isActive;
            context.Report.Add(
                "Check Shiny render feature is enabled",
                valid,
                "Checks that the Shiny render feature is enabled in the renderer. " +
                "If disabled, no reflections will render.",
                valid ? null : (Action)(() => {
                    Undo.RecordObject(rendererFeature, "Enable Shiny Render Feature");
                    rendererFeature.SetActive(true);
                    EditorUtility.SetDirty(rendererFeature);
                    AssetDatabase.SaveAssets();
                }),
                rendererFeature);
        }

        void ValidateCameraLayerMask(ValidationContext context, ScriptableRendererFeature rendererFeature) {
            if (rendererFeature == null) {
                context.Report.Add(
                    "Check camera layer is included in Shiny camera layer mask",
                    false,
                    "Cannot validate - Shiny render feature not found. Add the render feature first.",
                    showTarget: context.RendererData);
                return;
            }

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Camera gameCamera = null;
            foreach (Camera cam in cameras) {
                if (cam != null && cam.cameraType == CameraType.Game) {
                    gameCamera = cam;
                    break;
                }
            }

            if (gameCamera == null) {
                context.Report.Add(
                    "Check camera layer is included in Shiny camera layer mask",
                    false,
                    "Cannot validate - No active game camera found in scene.",
                    showTarget: null);
                return;
            }

            int cameraLayerMask = GetCameraLayerMask(rendererFeature);
            int cameraLayer = gameCamera.gameObject.layer;
            bool cameraIncluded = (cameraLayerMask & (1 << cameraLayer)) != 0;
            string cameraName = gameCamera.gameObject.name;
            string layerName = LayerMask.LayerToName(cameraLayer);

            context.Report.Add(
                "Check camera layer is included in Shiny camera layer mask",
                cameraIncluded,
                $"Checks that the game camera '{cameraName}' (layer: {layerName}) is included in the Shiny render feature's camera layer mask. " +
                "If excluded, Shiny will not render on this camera.",
                cameraIncluded ? null : (Action)(() => {
                    Undo.RecordObject(rendererFeature, "Include Camera Layer");
                    SetCameraLayerMask(rendererFeature, cameraLayerMask | (1 << cameraLayer));
                    EditorUtility.SetDirty(rendererFeature);
                    AssetDatabase.SaveAssets();
                }),
                rendererFeature);
        }

        void AddSwitchToDeferredOption(ValidationContext context, ScriptableRendererFeature rendererFeature) {
            if (context.RendererData == null) {
                return;
            }

            bool isForwardMode = context.RendererData.renderingMode != RenderingMode.Deferred;
            if (!isForwardMode) {
                return;
            }

            UniversalRendererData rendererData = context.RendererData;
            context.Report.AddOptionalAction(
                "(Optional) Switch to Deferred Rendering",
                "Switches the renderer to Deferred mode for automatic screen-space reflections on all surfaces. " +
                "In Deferred mode, Shiny SSR uses G-Buffer data and doesn't require Reflections scripts on objects. " +
                "This will also update the Shiny render feature to use deferred mode.",
                () => {
                    UrpUtility.SetRenderingMode(rendererData, RenderingMode.Deferred);

                    ScriptableRendererFeature feature = rendererFeature ?? FindShinyFeature(rendererData);
                    if (feature != null) {
                        Undo.RecordObject(feature, "Enable Shiny Deferred Mode");
                        SetUseDeferredOption(feature, true);
                        EditorUtility.SetDirty(feature);
                    }

                    AssetDatabase.SaveAssets();
                },
                rendererData,
                null,
                "Switch");
        }

        public void OnValidationComplete(ValidationContext context) {
        }

        void ValidateDeferredModeMatch(ValidationContext context, ScriptableRendererFeature rendererFeature) {
            if (rendererFeature == null || context.RendererData == null) {
                context.Report.Add(
                    "Check Shiny render feature deferred mode matches renderer",
                    false,
                    "Cannot validate - Shiny render feature not found. Add the render feature first.",
                    showTarget: context.RendererData);
                return;
            }

            RenderingMode rendererMode = context.RendererData.renderingMode;
            bool isDeferredRenderer = rendererMode == RenderingMode.Deferred;
            bool useDeferredOption = GetUseDeferredOption(rendererFeature);

            bool valid = isDeferredRenderer == useDeferredOption;
            string currentMode = isDeferredRenderer ? "Deferred" : "Forward";
            string featureSetting = useDeferredOption ? "enabled" : "disabled";

            context.Report.Add(
                "Check Shiny render feature deferred mode matches renderer",
                valid,
                $"Checks that the 'Use Deferred' option in the Shiny render feature matches the renderer's rendering path. " +
                $"Currently renderer is '{currentMode}' but 'Use Deferred' is {featureSetting}. " +
                $"Mismatch causes reflections to not work correctly.",
                valid ? null : (Action)(() => {
                    Undo.RecordObject(rendererFeature, "Set Shiny Deferred Mode");
                    SetUseDeferredOption(rendererFeature, isDeferredRenderer);
                    EditorUtility.SetDirty(rendererFeature);
                    AssetDatabase.SaveAssets();
                }),
                rendererFeature);
        }

        void ValidatePostProcessing(ValidationContext context, ScriptableRendererFeature rendererFeature) {
            if (rendererFeature == null) {
                context.Report.Add(
                    "Check camera Post Processing setting is synced with Shiny render feature",
                    false,
                    "Cannot validate - Shiny render feature not found. Add the render feature first.",
                    showTarget: context.RendererData);
                return;
            }

            bool cameraDisablesPost = UrpUtility.HasCameraWithPostProcessingDisabled();
            bool ignoreOptionEnabled = GetIgnorePostProcessingOption(rendererFeature);

            bool valid = !cameraDisablesPost || ignoreOptionEnabled;

            context.Report.Add(
                "Check camera Post Processing setting is synced with Shiny render feature",
                valid,
                "Checks that Shiny can render on cameras with post-processing disabled. " +
                "The 'Ignore Post Processing Option' in the render feature bypasses the camera checkbox. " +
                "Without it, reflections won't appear on cameras with post-processing unchecked.",
                valid ? null : (Action)(() => {
                    Undo.RecordObject(rendererFeature, "Enable Ignore Post Processing");
                    SetIgnorePostProcessingOption(rendererFeature, true);
                    EditorUtility.SetDirty(rendererFeature);
                    AssetDatabase.SaveAssets();
                }),
                rendererFeature);
        }

        void ValidateReflectionsMultiplier(ValidationContext context) {
            if (!VolumeUtility.AnyVolumeExists()) {
                return;
            }

            VolumeComponent component = VolumeUtility.GetVolumeComponent(VolumeComponentTypeName);
            if (component == null) {
                return;
            }

            float multiplier = GetReflectionsMultiplier(component);
            bool valid = multiplier > 0f;
            Volume volume = VolumeUtility.FindVolumeContaining(component);

            context.Report.Add(
                "Check Reflections Multiplier is greater than zero",
                valid,
                "Checks that the 'Reflections Multiplier' in the Shiny volume component is greater than 0. " +
                "The effect is inactive when this value is 0.",
                valid ? null : (Action)(() => {
                    SerializedObject so = new SerializedObject(component);
                    so.Update();
                    SerializedParameterUtility.SetParameter(so, "reflectionsMultiplier", 1f);
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(component);
                    EditorRefreshUtility.RefreshAllViews();
                }),
                volume != null ? volume.gameObject : (UnityEngine.Object)volume?.sharedProfile);
        }


        void ValidateReflectionsScriptsInForward(ValidationContext context, ScriptableRendererFeature rendererFeature) {
            if (rendererFeature == null) {
                return;
            }

            bool useDeferredOption = GetUseDeferredOption(rendererFeature);
            if (useDeferredOption) {
                return;
            }

            Type reflectionsType = GetReflectionsScriptType();
            if (reflectionsType == null) {
                context.Report.Add(
                    "Check Reflections scripts exist in scene (Forward mode)",
                    false,
                    "Cannot validate - Reflections script type not found.",
                    showTarget: null);
                return;
            }

            UnityEngine.Object[] reflectionsScripts = UnityEngine.Object.FindObjectsByType(reflectionsType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool hasReflectionsScripts = reflectionsScripts.Length > 0;

            context.Report.Add(
                "Check Reflections scripts exist in scene (Forward mode)",
                hasReflectionsScripts,
                "In Forward rendering mode, Shiny requires 'Reflections' scripts attached to objects that should receive reflections. " +
                "Without these scripts, no objects will show reflections. Add the Reflections component to objects or use Deferred mode for automatic reflections.",
                showTarget: hasReflectionsScripts ? reflectionsScripts[0] : null);

            if (hasReflectionsScripts) {
                ValidateReflectionsHaveReflectivity(context, reflectionsScripts);
            }
        }

        void ValidateReflectionsHaveReflectivity(ValidationContext context, UnityEngine.Object[] reflectionsScripts) {
            bool hasReflectivity = false;
            UnityEngine.Object firstWithReflectivity = null;
            UnityEngine.Object firstWithoutReflectivity = null;

            foreach (UnityEngine.Object obj in reflectionsScripts) {
                if (obj == null) continue;

                bool ignore = GetReflectionsIgnore(obj);
                if (ignore) continue;

                float smoothness = GetReflectionsSmoothness(obj);
                float metallic = GetReflectionsMetallic(obj);

                if (smoothness > 0 || metallic > 0) {
                    hasReflectivity = true;
                    if (firstWithReflectivity == null) {
                        firstWithReflectivity = obj;
                    }
                } else if (firstWithoutReflectivity == null) {
                    firstWithoutReflectivity = obj;
                }
            }

            context.Report.Add(
                "Check Reflections scripts have reflectivity > 0 (Forward mode)",
                hasReflectivity,
                "Checks that at least one Reflections script has smoothness or metallic values greater than 0. " +
                "If all values are 0, no reflections will be visible on those objects.",
                showTarget: hasReflectivity ? firstWithReflectivity : firstWithoutReflectivity);
        }

        static bool GetReflectionsIgnore(UnityEngine.Object reflections) {
            if (reflections == null) return true;
            FieldInfo field = reflections.GetType().GetField("ignore", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.GetValue(reflections) is bool value) {
                return value;
            }
            return false;
        }

        static float GetReflectionsSmoothness(UnityEngine.Object reflections) {
            if (reflections == null) return 0f;
            FieldInfo field = reflections.GetType().GetField("smoothness", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.GetValue(reflections) is float value) {
                return value;
            }
            return 0f;
        }

        static float GetReflectionsMetallic(UnityEngine.Object reflections) {
            if (reflections == null) return 0f;
            FieldInfo field = reflections.GetType().GetField("metallic", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.GetValue(reflections) is float value) {
                return value;
            }
            return 0f;
        }

        static ScriptableRendererFeature FindShinyFeature(UniversalRendererData rendererData) {
            if (rendererData == null) {
                return null;
            }
            Type featureType = GetRendererFeatureType();
            if (featureType == null) {
                return null;
            }
            return rendererData.rendererFeatures.FirstOrDefault(f => f != null && f.GetType() == featureType);
        }

        static Type GetRendererFeatureType() {
            if (_rendererFeatureType != null) {
                return _rendererFeatureType;
            }
            TypeUtility.TryGetType(RendererFeatureTypeName, out _rendererFeatureType);
            return _rendererFeatureType;
        }

        static Type GetReflectionsScriptType() {
            if (_reflectionsScriptType != null) {
                return _reflectionsScriptType;
            }
            TypeUtility.TryGetType(ReflectionsScriptTypeName, out _reflectionsScriptType);
            return _reflectionsScriptType;
        }

        static bool GetUseDeferredOption(ScriptableRendererFeature feature) {
            if (feature == null) return false;
            FieldInfo field = feature.GetType().GetField("useDeferred", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.GetValue(feature) is bool value) {
                return value;
            }
            return false;
        }

        static void SetUseDeferredOption(ScriptableRendererFeature feature, bool value) {
            if (feature == null) return;
            FieldInfo field = feature.GetType().GetField("useDeferred", BindingFlags.Public | BindingFlags.Instance);
            field?.SetValue(feature, value);
        }

        static bool GetIgnorePostProcessingOption(ScriptableRendererFeature feature) {
            if (feature == null) return false;
            FieldInfo field = feature.GetType().GetField("ignorePostProcessingOption", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.GetValue(feature) is bool value) {
                return value;
            }
            return false;
        }

        static void SetIgnorePostProcessingOption(ScriptableRendererFeature feature, bool value) {
            if (feature == null) return;
            FieldInfo field = feature.GetType().GetField("ignorePostProcessingOption", BindingFlags.Public | BindingFlags.Instance);
            field?.SetValue(feature, value);
        }

        static float GetReflectionsMultiplier(VolumeComponent component) {
            if (component == null) return 0f;
            FieldInfo field = component.GetType().GetField("reflectionsMultiplier", BindingFlags.Public | BindingFlags.Instance);
            if (field != null) {
                object paramValue = field.GetValue(component);
                if (paramValue != null) {
                    PropertyInfo valueProp = paramValue.GetType().GetProperty("value");
                    if (valueProp != null && valueProp.GetValue(paramValue) is float floatValue) {
                        return floatValue;
                    }
                }
            }
            return 0f;
        }


        static int GetCameraLayerMask(ScriptableRendererFeature feature) {
            if (feature == null) return -1;
            FieldInfo field = feature.GetType().GetField("cameraLayerMask", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.GetValue(feature) is LayerMask layerMask) {
                return layerMask.value;
            }
            return -1;
        }

        static void SetCameraLayerMask(ScriptableRendererFeature feature, int value) {
            if (feature == null) return;
            FieldInfo field = feature.GetType().GetField("cameraLayerMask", BindingFlags.Public | BindingFlags.Instance);
            if (field != null) {
                field.SetValue(feature, (LayerMask)value);
            }
        }
    }

}

