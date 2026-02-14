using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kronnect.Hub {

    sealed class EdgeFusionValidationProvider : IValidationProvider {

        const string VolumeComponentTypeName = "EdgeFusion.EdgeFusion";

        public void Validate(ValidationContext context) {
            if (!string.Equals(context.Asset.Id, KronnectAssetIds.EdgeFusion, StringComparison.Ordinal)) {
                return;
            }

            ValidateBlendingIntensity(context);
        }

        public void OnValidationComplete(ValidationContext context) {
        }

        void ValidateBlendingIntensity(ValidationContext context) {
            if (!VolumeUtility.AnyVolumeExists()) {
                return;
            }

            VolumeComponent component = VolumeUtility.GetVolumeComponent(VolumeComponentTypeName);
            if (component == null) {
                return;
            }

            var intensityField = component.GetType().GetField("intensity");
            if (intensityField == null) {
                return;
            }

            var intensityParam = intensityField.GetValue(component) as FloatParameter;
            if (intensityParam == null) {
                return;
            }

            bool valid = intensityParam.value > 0f;
            Volume volume = VolumeUtility.FindVolumeContaining(component);

            context.Report.Add(
                "Check Edge Fusion blending intensity is greater than 0",
                valid,
                "Checks that the 'Intensity' parameter in the Edge Fusion volume override is greater than 0. " +
                "If it is 0, edge blending will be disabled.",
                valid ? null : (Action)(() => {
                    SerializedObject so = new SerializedObject(component);
                    so.Update();
                    SerializedParameterUtility.SetParameter(so, "intensity", 1f);
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(component);
                    EditorRefreshUtility.RefreshAllViews();
                }),
                volume != null ? (UnityEngine.Object)volume.gameObject : context.VolumeProfile);
        }

    }

}

