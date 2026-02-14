using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    sealed class LBAOValidationProvider : IValidationProvider {
        const string VolumeComponentTypeName = "LBAOFX.LBAO";
        const string RendererFeatureTypeName = "LBAOFX.LBAORenderFeature";
        static Type _volumeComponentType;
        static Type _rendererFeatureType;

        public void Validate(ValidationContext context) {
            if (!string.Equals(context.Asset.Id, KronnectAssetIds.LBAO, StringComparison.Ordinal)) {
                return;
            }

            ScriptableRendererFeature rendererFeature = FindLBAOFeature(context.RendererData);
            ValidateLBAOOverride(context);
            ValidatePostProcessing(context, rendererFeature);
        }

        public void OnValidationComplete(ValidationContext context) {
            if (!string.Equals(context.Asset.Id, KronnectAssetIds.LBAO, StringComparison.Ordinal)) {
                return;
            }

            SelectVolumeWithLBAO();
        }

        void ValidateLBAOOverride(ValidationContext context) {
            if (!VolumeUtility.AnyVolumeExists()) {
                context.Report.Add(
                    "Add a Volume to the scene to configure LBAO",
                    false,
                    "No Volume component found in the scene. A Volume is required to configure LBAO ambient occlusion effects.",
                    () => {
                        Volume volume = VolumeUtility.EnsureGlobalVolume();
                        EnsureLBAOOverride(volume.sharedProfile);
                    },
                    null);
                return;
            }

            Volume volumeWithLBAO = VolumeUtility.FindVolumeWithComponent(VolumeComponentTypeName);
            bool valid = volumeWithLBAO != null;
            Volume existingVolume = VolumeUtility.FindGlobalVolume();
            context.Report.Add(
                "Check LBAO override exists in the Volume profile",
                valid,
                "Checks that an LBAO override is added to the Volume Profile. " +
                "This is where you configure the ambient occlusion effect. Without it, no effects will appear.",
                valid ? null : (Action)(() => {
                    EnsureLBAOInVolume(existingVolume);
                }),
                volumeWithLBAO != null ? volumeWithLBAO.sharedProfile : existingVolume?.sharedProfile);
        }

        void ValidatePostProcessing(ValidationContext context, ScriptableRendererFeature rendererFeature) {
            if (rendererFeature == null) {
                context.Report.Add(
                    "Check camera Post Processing setting is synced with render feature",
                    false,
                    "Cannot validate - LBAO render feature not found. Add the render feature first.",
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
                "Checks that LBAO can render when post-processing is disabled on cameras. " +
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

        void SelectVolumeWithLBAO() {
            Volume volume = VolumeUtility.FindVolumeWithComponent(VolumeComponentTypeName);
            if (volume == null) {
                return;
            }
            Selection.activeObject = volume.gameObject;
            EditorGUIUtility.PingObject(volume.gameObject);
        }

        static ScriptableRendererFeature FindLBAOFeature(UniversalRendererData rendererData) {
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

        void EnsureLBAOInVolume(Volume volume) {
            if (volume == null) {
                volume = VolumeUtility.EnsureGlobalVolume();
            }

            VolumeProfile profile = volume.sharedProfile;
            if (profile == null) {
                profile = VolumeUtility.EnsureVolumeProfileAsset();
                volume.sharedProfile = profile;
            }

            if (profile != null) {
                EnsureLBAOOverride(profile);
            }
        }

        void EnsureLBAOOverride(VolumeProfile profile) {
            Type componentType = GetLBAOVolumeType();
            if (profile == null || componentType == null) {
                return;
            }
            VolumeComponent component = profile.components.FirstOrDefault(c => c != null && c.GetType() == componentType);
            if (component == null) {
                Undo.RecordObject(profile, "Add LBAO Override");
                component = profile.Add(componentType, true);
                component.SetAllOverridesTo(true);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            } else {
                component.SetAllOverridesTo(true);
                EditorUtility.SetDirty(component);
            }
        }

        static Type GetLBAOVolumeType() {
            if (_volumeComponentType != null) {
                return _volumeComponentType;
            }
            TypeUtility.TryGetType(VolumeComponentTypeName, out _volumeComponentType);
            return _volumeComponentType;
        }
    }

}

