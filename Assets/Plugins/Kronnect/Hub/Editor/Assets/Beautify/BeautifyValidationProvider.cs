using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    sealed class BeautifyValidationProvider : IValidationProvider {
        const string VolumeComponentTypeName = "Beautify.Universal.Beautify";
        const string RendererFeatureTypeName = "Beautify.Universal.BeautifyRendererFeature";
        static Type _volumeComponentType;
        static Type _rendererFeatureType;

        public void Validate(ValidationContext context) {
            if (!string.Equals(context.Asset.Id, KronnectAssetIds.Beautify, StringComparison.Ordinal)) {
                return;
            }

            ScriptableRendererFeature rendererFeature = FindBeautifyFeature(context.RendererData);
            ValidateDepthTextureMode(context, rendererFeature);
            ValidateBeautifyOverride(context);
            ValidatePostProcessing(context, rendererFeature);

            context.Report.AddOptionalAction(
                "(Optional) Quick Effect Settings",
                "Applies a preset with sharpening, bloom, vignette, and color adjustments. " +
                "Good starting point for a polished look. Customize further in the Volume Profile.",
                ApplyQuickEffectSettings);
        }

        static void ApplyQuickEffectSettings() {
            VolumeComponent component = VolumeUtility.GetVolumeComponent(VolumeComponentTypeName);
            if (component == null) {
                Debug.LogWarning("Beautify volume component not found. Run Check Setup first to create it.");
                return;
            }

            SerializedObject so = new SerializedObject(component);
            so.Update();

            SerializedParameterUtility.SetParameter(so, "sharpenIntensity", 4f);
            SerializedParameterUtility.SetParameter(so, "ditherIntensity", 0.005f);
            SerializedParameterUtility.SetParameter(so, "brightness", 1.05f);
            SerializedParameterUtility.SetParameter(so, "saturate", 1f);
            SerializedParameterUtility.SetParameter(so, "contrast", 1.02f);
            SerializedParameterUtility.SetParameter(so, "bloomIntensity", 0.25f);
            SerializedParameterUtility.SetParameter(so, "bloomThreshold", 0.75f);
            SerializedParameterUtility.SetParameter(so, "vignettingOuterRing", 0.325f);
            SerializedParameterUtility.SetParameter(so, "vignettingInnerRing", 0.925f);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            EditorRefreshUtility.RefreshAllViews();

            Debug.Log("[Beautify] Quick Effect Settings applied.");
        }

        public void OnValidationComplete(ValidationContext context) {
            if (!string.Equals(context.Asset.Id, KronnectAssetIds.Beautify, StringComparison.Ordinal)) {
                return;
            }

            SelectVolumeWithBeautify();
        }

        void ValidateDepthTextureMode(ValidationContext context, ScriptableRendererFeature rendererFeature) {
            if (rendererFeature == null || context.RendererData == null) {
                context.Report.Add(
                    "Check Depth Texture Mode is compatible with Beautify render pass",
                    false,
                    "Cannot validate - Beautify render feature not found. Add the render feature first.",
                    showTarget: context.RendererData);
                return;
            }

            CopyDepthMode copyDepthMode = context.RendererData.copyDepthMode;
            RenderPassEvent beautifyEvent = GetRenderPassEvent(rendererFeature);

            bool valid;
            string validModes;

            bool rendersBeforeTransparents = beautifyEvent < RenderPassEvent.BeforeRenderingTransparents;

            if (rendersBeforeTransparents) {
                valid = copyDepthMode == CopyDepthMode.ForcePrepass || copyDepthMode == CopyDepthMode.AfterOpaques;
                validModes = "'Force Prepass' or 'After Opaques'";
            } else {
                valid = copyDepthMode == CopyDepthMode.ForcePrepass || 
                        copyDepthMode == CopyDepthMode.AfterOpaques || 
                        copyDepthMode == CopyDepthMode.AfterTransparents;
                validModes = "'Force Prepass', 'After Opaques', or 'After Transparents'";
            }

            string beautifyTiming = rendersBeforeTransparents ? "before transparent objects" : "after transparent objects";

            context.Report.Add(
                "Check Depth Texture Mode is compatible with Beautify render pass",
                valid,
                $"Checks depth texture is available when Beautify runs ({beautifyTiming}). " +
                $"Valid modes: {validModes}. Wrong mode may cause DOF or outline artifacts.",
                valid ? null : (Action)(() => {
                    Undo.RecordObject(context.RendererData, "Set Depth Texture Mode");
                    context.RendererData.copyDepthMode = CopyDepthMode.AfterOpaques;
                    EditorUtility.SetDirty(context.RendererData);
                    AssetDatabase.SaveAssets();
                }),
                context.RendererData);
        }

        static RenderPassEvent GetRenderPassEvent(ScriptableRendererFeature feature) {
            if (feature == null) return RenderPassEvent.AfterRenderingTransparents;
            FieldInfo field = feature.GetType().GetField("renderPassEvent", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.GetValue(feature) is RenderPassEvent passEvent) {
                return passEvent;
            }
            return RenderPassEvent.AfterRenderingTransparents;
        }

        void ValidateBeautifyOverride(ValidationContext context) {
            if (!VolumeUtility.AnyVolumeExists()) {
                context.Report.Add(
                    "Add a Volume to the scene to configure Beautify",
                    false,
                    "No Volume component found in the scene. A Volume is required to configure Beautify post-processing effects.",
                    () => {
                        Volume volume = VolumeUtility.EnsureGlobalVolume();
                        EnsureBeautifyOverride(volume.sharedProfile);
                    },
                    null);
                return;
            }

            Volume volumeWithBeautify = VolumeUtility.FindVolumeWithComponent(VolumeComponentTypeName);
            bool valid = volumeWithBeautify != null;
            Volume existingVolume = VolumeUtility.FindGlobalVolume();
            context.Report.Add(
                "Check Beautify override exists in the Volume profile",
                valid,
                "Checks that a Beautify override is added to the Volume Profile. " +
                "This is where you configure effects like sharpening, bloom, DOF, etc. Without it, no effects will appear.",
                valid ? null : (Action)(() => {
                    EnsureBeautifyInVolume(existingVolume);
                }),
                volumeWithBeautify != null ? volumeWithBeautify.sharedProfile : existingVolume?.sharedProfile);
        }


        void ValidatePostProcessing(ValidationContext context, ScriptableRendererFeature rendererFeature) {
            if (rendererFeature == null) {
                context.Report.Add(
                    "Check camera Post Processing setting is synced with render feature",
                    false,
                    "Cannot validate - Beautify render feature not found. Add the render feature first.",
                    showTarget: context.RendererData);
                return;
            }

            bool cameraDisablesPost = UrpUtility.HasCameraWithPostProcessingDisabled();
            bool cameraHasNoPostData = UrpUtility.HasCameraWithoutPostProcessingData();
            bool ignoreOptionEnabled = GetIgnorePostProcessingOption(rendererFeature);

            bool postProcessingMayBeDisabled = cameraDisablesPost || cameraHasNoPostData;
            bool valid = !postProcessingMayBeDisabled || ignoreOptionEnabled;

            Action fixAction = postProcessingMayBeDisabled && !ignoreOptionEnabled ? (Action)(() => {
                Undo.RecordObject(rendererFeature, "Enable Ignore Post Processing");
                SetIgnorePostProcessingOption(rendererFeature, true);
                EditorUtility.SetDirty(rendererFeature);
                AssetDatabase.SaveAssets();
            }) : null;

            context.Report.Add(
                "Check camera Post Processing setting is synced with render feature",
                valid,
                "Checks that Beautify can render when post-processing is disabled on cameras. " +
                "The 'Ignore Post Processing Option' in the render feature bypasses the camera's post-processing checkbox. " +
                "Without it, effects won't appear when post-processing is disabled.",
                fixAction,
                context.RendererData);
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

        void SelectVolumeWithBeautify() {
            Volume volume = VolumeUtility.FindVolumeWithComponent(VolumeComponentTypeName);
            if (volume == null) {
                return;
            }
            Selection.activeObject = volume.gameObject;
            EditorGUIUtility.PingObject(volume.gameObject);
        }

        static ScriptableRendererFeature FindBeautifyFeature(UniversalRendererData rendererData) {
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

        void EnsureBeautifyInVolume(Volume volume) {
            if (volume == null) {
                volume = VolumeUtility.EnsureGlobalVolume();
            }

            VolumeProfile profile = volume.sharedProfile;
            if (profile == null) {
                profile = VolumeUtility.EnsureVolumeProfileAsset();
                volume.sharedProfile = profile;
            }

            if (profile != null) {
                EnsureBeautifyOverride(profile);
            }
        }

        void EnsureBeautifyOverride(VolumeProfile profile) {
            Type componentType = GetBeautifyVolumeType();
            if (profile == null || componentType == null) {
                return;
            }
            VolumeComponent component = profile.components.FirstOrDefault(c => c != null && c.GetType() == componentType);
            if (component == null) {
                Undo.RecordObject(profile, "Add Beautify Override");
                component = profile.Add(componentType, true);
                component.SetAllOverridesTo(true);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            } else {
                component.SetAllOverridesTo(true);
                EditorUtility.SetDirty(component);
            }
        }


        static Type GetBeautifyVolumeType() {
            if (_volumeComponentType != null) {
                return _volumeComponentType;
            }
            TypeUtility.TryGetType(VolumeComponentTypeName, out _volumeComponentType);
            return _volumeComponentType;
        }
    }

}

