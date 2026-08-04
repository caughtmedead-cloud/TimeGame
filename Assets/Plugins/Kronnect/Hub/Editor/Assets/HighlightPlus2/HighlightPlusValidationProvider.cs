using System;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    sealed class HighlightPlusValidationProvider : IValidationProvider {
        const string RenderFeatureTypeName = "HighlightPlus.HighlightPlusRenderPassFeature";
        static Type _rendererFeatureType;

        public void Validate(ValidationContext context) {
            if (!string.Equals(context.Asset.Id, KronnectAssetIds.HighlightPlus, StringComparison.Ordinal)) {
                return;
            }

            ScriptableRendererFeature rendererFeature = FindHighlightPlusFeature(context.RendererData);
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
                "Check Highlight Plus render feature is enabled",
                valid,
                "Checks that the Highlight Plus render feature is enabled in the renderer. " +
                "If disabled, no highlight effects will render.",
                valid ? null : (Action)(() => {
                    Undo.RecordObject(rendererFeature, "Enable Highlight Plus Render Feature");
                    rendererFeature.SetActive(true);
                    EditorUtility.SetDirty(rendererFeature);
                    AssetDatabase.SaveAssets();
                }),
                rendererFeature);
        }

        static ScriptableRendererFeature FindHighlightPlusFeature(UniversalRendererData rendererData) {
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

