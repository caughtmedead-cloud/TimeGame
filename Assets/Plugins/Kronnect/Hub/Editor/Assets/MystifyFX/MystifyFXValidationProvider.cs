using System;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    sealed class MystifyFXValidationProvider : IValidationProvider {
        const string RenderFeatureTypeName = "MystifyFX.MystifyFXRendererFeature";
        static Type _rendererFeatureType;

        public void Validate(ValidationContext context) {
            if (!string.Equals(context.Asset.Id, KronnectAssetIds.MystifyFX, StringComparison.Ordinal)) {
                return;
            }

            ScriptableRendererFeature rendererFeature = FindMystifyFXFeature(context.RendererData);
            ValidateFeatureEnabled(context, rendererFeature);
        }

        public void OnValidationComplete(ValidationContext context) {
        }

        void ValidateFeatureEnabled(ValidationContext context, ScriptableRendererFeature rendererFeature) {
            if (rendererFeature == null) {
                return;
            }

            bool valid = rendererFeature.isActive;
            context.Report.Add(
                "Check Mystify FX render feature is enabled",
                valid,
                "Checks that the Mystify FX render feature is enabled in the renderer. " +
                "If disabled, no Mystify effects will render.",
                valid ? null : (Action)(() => {
                    Undo.RecordObject(rendererFeature, "Enable Mystify FX Render Feature");
                    rendererFeature.SetActive(true);
                    EditorUtility.SetDirty(rendererFeature);
                    AssetDatabase.SaveAssets();
                }),
                rendererFeature);
        }

        static ScriptableRendererFeature FindMystifyFXFeature(UniversalRendererData rendererData) {
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
            TypeUtility.TryGetType(RenderFeatureTypeName, out _rendererFeatureType);
            return _rendererFeatureType;
        }
    }

}

