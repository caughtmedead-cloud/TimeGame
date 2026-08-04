using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    sealed class VolumetricLightsValidationProvider : IValidationProvider {
        
        const string SceneComponentType = "VolumetricLights.VolumetricLight";
        const string DepthPrePassFeatureType = "VolumetricLights.VolumetricLightsDepthPrePassFeature";
        const string TranslucentShadowMapFeatureType = "VolumetricLights.VolumetricLightsTranslucentShadowMapFeature";

        public void Validate(ValidationContext context) {
            if (!string.Equals(context.Asset.Id, KronnectAssetIds.VolumetricLights, StringComparison.Ordinal)) {
                return;
            }

            ValidateCastDirectLightRequiresDeferredRendering(context);
            ValidateTranslucencyShadowsRequireRenderFeature(context);

            AddOptionalFeature(context, DepthPrePassFeatureType, "Volumetric Lights Depth PrePass Feature",
                "Enables proper depth handling with transparent objects (particles, glass, water). " +
                "Add if volumetric lights render incorrectly with transparents.");
        }

        public void OnValidationComplete(ValidationContext context) {
        }

        void ValidateCastDirectLightRequiresDeferredRendering(ValidationContext context) {
            Component[] lightComponents = VolumeUtility.FindAllSceneComponents(SceneComponentType);
            if (lightComponents.Length == 0) {
                return;
            }

            bool anyUseCastDirectLight = false;
            foreach (Component lightComponent in lightComponents) {
                SerializedObject so = new SerializedObject(lightComponent);
                SerializedProperty castDirectLightProp = so.FindProperty("castDirectLight");
                if (castDirectLightProp != null && castDirectLightProp.boolValue) {
                    anyUseCastDirectLight = true;
                    break;
                }
            }

            if (!anyUseCastDirectLight) {
                return;
            }

            if (context.RendererData == null) {
                return;
            }

            bool isDeferredEnabled = context.RendererData.renderingMode == RenderingMode.Deferred;

            context.Report.Add(
                "Cast Direct Light requires Deferred Rendering Path",
                isDeferredEnabled,
                "One or more Volumetric Lights have 'Cast Direct Light' enabled. This feature requires Deferred Rendering Path to function correctly. " +
                "The renderer will be switched to Deferred mode.",
                isDeferredEnabled ? null : (Action)(() => EnableDeferredRendering(context)),
                context.RendererData);
        }

        void EnableDeferredRendering(ValidationContext context) {
            if (context.RendererData == null) {
                return;
            }

            Undo.RecordObject(context.RendererData, "Enable Deferred Rendering");
            context.RendererData.renderingMode = RenderingMode.Deferred;
            EditorUtility.SetDirty(context.RendererData);
            AssetDatabase.SaveAssets();
        }

        void ValidateTranslucencyShadowsRequireRenderFeature(ValidationContext context) {
            Component[] lightComponents = VolumeUtility.FindAllSceneComponents(SceneComponentType);
            if (lightComponents.Length == 0) {
                return;
            }

            bool anyUseTranslucency = false;
            foreach (Component lightComponent in lightComponents) {
                SerializedObject so = new SerializedObject(lightComponent);
                SerializedProperty enableShadowsProp = so.FindProperty("enableShadows");
                SerializedProperty shadowTranslucencyProp = so.FindProperty("shadowTranslucency");
                
                if (enableShadowsProp != null && enableShadowsProp.boolValue && 
                    shadowTranslucencyProp != null && shadowTranslucencyProp.boolValue) {
                    anyUseTranslucency = true;
                    break;
                }
            }

            if (!anyUseTranslucency) {
                return;
            }

            if (context.Pipeline == null || context.RendererData == null) {
                return;
            }

            bool hasTranslucentFeature = context.Pipeline.HasRenderFeature(TranslucentShadowMapFeatureType);

            context.Report.Add(
                "Translucency Shadows require Volumetric Lights Translucent Shadow Map Feature",
                hasTranslucentFeature,
                "One or more Volumetric Lights have both 'Enable Shadows' and 'Translucency' enabled. This requires the Volumetric Lights Translucent Shadow Map Feature to be added to the URP renderer.",
                hasTranslucentFeature ? null : (Action)(() => AddTranslucentFeature(context)),
                context.RendererData);
        }

        void AddTranslucentFeature(ValidationContext context) {
            if (context.RendererData == null) {
                return;
            }

            UrpUtility.TryAddRenderFeature(context.RendererData, TranslucentShadowMapFeatureType);
        }

        void AddOptionalFeature(ValidationContext context, string typeName, string displayName, string detailsText) {
            if (context.Pipeline == null || context.RendererData == null) {
                context.Report.AddOptionalAction(
                    $"(Optional) {displayName}",
                    "Cannot validate - URP renderer not configured.",
                    null,
                    context.RendererData,
                    null,
                    "Add"
                );
                return;
            }

            bool isPresent = context.Pipeline.HasRenderFeature(typeName);

            Action addAction = isPresent ? null : (Action)(() => {
                 UrpUtility.TryAddRenderFeature(context.RendererData, typeName);
            });
            
            ScriptableRendererFeature feature = null;
            if (isPresent) {
                 feature = context.RendererData.rendererFeatures.FirstOrDefault(f => f != null && f.GetType().FullName == typeName);
            }

            context.Report.AddOptionalAction(
                $"(Optional) {displayName}",
                detailsText,
                addAction,
                context.RendererData,
                null,
                "Add"
            );
        }
    }
}

