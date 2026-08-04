using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    sealed class RadiantValidationProvider : IValidationProvider {

        const string ShadowMapRenderFeatureTypeName = "RadiantGI.Universal.RadiantShadowMapRenderFeature";
        const string RenderFeatureTypeName = "RadiantGI.Universal.RadiantRenderFeature";
        const string VolumeComponentTypeName = "RadiantGI.Universal.RadiantGlobalIllumination";

        public void Validate(ValidationContext context) {
            if (!string.Equals(context.Asset.Id, KronnectAssetIds.Radiant, StringComparison.Ordinal)) {
                return;
            }

            if (!VolumeUtility.AnyVolumeExists()) {
                return;
            }

            VolumeUtility.TryGetVolumeComponent(VolumeComponentTypeName, out VolumeComponent component, out Volume volume);
            ScriptableRendererFeature renderFeature = FindRadiantRenderFeature(context);

            ValidateIndirectIntensity(context, component, volume);
            ValidateShadowMapRenderFeature(context, component);
            ValidateRenderingPathMatchesRenderer(context, renderFeature);
            AddSwitchToDeferredOption(context, renderFeature);
        }

        void ValidateIndirectIntensity(ValidationContext context, VolumeComponent component, Volume volume) {
            if (component == null) {
                return;
            }

            var intensityField = component.GetType().GetField("indirectIntensity");
            if (intensityField == null) {
                return;
            }

            var intensityParam = intensityField.GetValue(component) as FloatParameter;
            if (intensityParam == null) {
                return;
            }

            bool valid = intensityParam.value > 0;

            context.Report.Add(
                "Check Indirect Intensity is greater than 0",
                valid,
                "Checks that the 'Indirect Intensity' in the Radiant GI volume is greater than 0. " +
                "If set to 0, the global illumination effect will not be visible.",
                valid ? null : (Action)(() => {
                    SerializedObject so = new SerializedObject(component);
                    so.Update();
                    SerializedParameterUtility.SetParameter(so, "indirectIntensity", 1f);
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(component);
                    EditorRefreshUtility.RefreshAllViews();
                }),
                volume != null ? (UnityEngine.Object)volume.gameObject : context.VolumeProfile);
        }

        public void OnValidationComplete(ValidationContext context) {
        }

        void ValidateShadowMapRenderFeature(ValidationContext context, VolumeComponent component) {
            if (component == null) {
                return;
            }

            var fallbackRSMField = component.GetType().GetField("fallbackReflectiveShadowMap");
            if (fallbackRSMField == null) {
                return;
            }

            var fallbackRSMParam = fallbackRSMField.GetValue(component) as BoolParameter;
            if (fallbackRSMParam == null || !fallbackRSMParam.value) {
                return;
            }

            Type shadowMapFeatureType = TypeCache.GetTypesDerivedFrom<ScriptableRendererFeature>()
                .FirstOrDefault(t => t.FullName == ShadowMapRenderFeatureTypeName);
            if (shadowMapFeatureType == null) {
                context.Report.Add(
                    "Check Radiant Shadow Map render feature is added",
                    false,
                    "Cannot validate - Radiant Shadow Map render feature type not found.",
                    showTarget: null);
                return;
            }

            if (context.RendererData == null) {
                context.Report.Add(
                    "Check Radiant Shadow Map render feature is added",
                    false,
                    "Cannot validate - URP renderer not found.",
                    showTarget: null);
                return;
            }

            bool hasFeature = context.RendererData.rendererFeatures.Any(f => f != null && shadowMapFeatureType.IsAssignableFrom(f.GetType()));

            context.Report.Add(
                "Check Radiant Shadow Map render feature is added",
                hasFeature,
                "Checks that the 'Radiant Shadow Map Render Feature' is added to the URP renderer. " +
                "This is required when the 'Fallback: Reflective Shadow Map' option is enabled in the Radiant GI volume.",
                hasFeature ? null : (Action)(() => {
                    ScriptableRendererFeature feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(shadowMapFeatureType);
                    feature.name = shadowMapFeatureType.Name;
                    Undo.RecordObject(context.RendererData, "Add Radiant Shadow Map Render Feature");
                    AssetDatabase.AddObjectToAsset(feature, context.RendererData);
                    var features = context.RendererData.rendererFeatures;
                    features.Add(feature);
                    EditorUtility.SetDirty(context.RendererData);
                    AssetDatabase.SaveAssets();
                }),
                showTarget: context.RendererData);
        }

        void ValidateRenderingPathMatchesRenderer(ValidationContext context, ScriptableRendererFeature renderFeature) {
            if (context.RendererData == null) {
                context.Report.Add(
                    "Check Radiant rendering path matches URP renderer",
                    false,
                    "Cannot validate - URP renderer not found.",
                    showTarget: null);
                return;
            }

            if (renderFeature == null) {
                context.Report.Add(
                    "Check Radiant rendering path matches URP renderer",
                    false,
                    "Cannot validate - Radiant render feature not found. Add the render feature first.",
                    showTarget: context.RendererData);
                return;
            }

            var renderingPathField = renderFeature.GetType().GetField("renderingPath");
            if (renderingPathField == null) {
                return;
            }

            object fieldValue = renderingPathField.GetValue(renderFeature);
            string radiantPathName = fieldValue?.ToString() ?? "Unknown";

            RenderingMode rendererMode = context.RendererData.renderingMode;
            bool rendererDeferred = rendererMode == RenderingMode.Deferred;

            bool valid = radiantPathName == "Both" ||
                (rendererDeferred && radiantPathName == "Deferred") ||
                (!rendererDeferred && radiantPathName == "Forward");

            context.Report.Add(
                "Check Radiant rendering path matches URP renderer",
                valid,
                $"Checks that the Radiant render feature 'Rendering Path' matches the renderer mode ({rendererMode}). " +
                "If the settings differ, Radiant GI will not render correctly.",
                valid ? null : (Action)(() => {
                    object targetValue = Enum.Parse(renderingPathField.FieldType, rendererDeferred ? "Deferred" : "Forward");
                    Undo.RecordObject(renderFeature, "Match Radiant Rendering Path");
                    renderingPathField.SetValue(renderFeature, targetValue);
                    EditorUtility.SetDirty(renderFeature);
                    AssetDatabase.SaveAssets();
                }),
                renderFeature);
        }

        void AddSwitchToDeferredOption(ValidationContext context, ScriptableRendererFeature renderFeature) {
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
                "Switches the renderer to Deferred mode for improved global illumination quality. " +
                "In Deferred mode, Radiant GI has access to G-Buffer data which provides better accuracy and performance. " +
                "This action also sets the Radiant render feature to Deferred mode.",
                () => {
                    UrpUtility.SetRenderingMode(rendererData, RenderingMode.Deferred);

                    ScriptableRendererFeature targetFeature = renderFeature ?? FindRadiantRenderFeature(context);
                    if (targetFeature != null) {
                        var renderingPathField = targetFeature.GetType().GetField("renderingPath");
                        if (renderingPathField != null) {
                            object deferredValue = Enum.Parse(renderingPathField.FieldType, "Deferred");
                            Undo.RecordObject(targetFeature, "Set Radiant Rendering Path");
                            renderingPathField.SetValue(targetFeature, deferredValue);
                            EditorUtility.SetDirty(targetFeature);
                        }
                    }

                    AssetDatabase.SaveAssets();
                },
                rendererData,
                null,
                "Switch");
        }

        static ScriptableRendererFeature FindRadiantRenderFeature(ValidationContext context) {
            if (context.RendererData == null) {
                return null;
            }

            Type renderFeatureType = TypeCache.GetTypesDerivedFrom<ScriptableRendererFeature>()
                .FirstOrDefault(t => t.FullName == RenderFeatureTypeName);
            if (renderFeatureType == null) {
                return null;
            }

            return context.RendererData.rendererFeatures.FirstOrDefault(f => f != null && renderFeatureType.IsAssignableFrom(f.GetType()));
        }
    }

}

