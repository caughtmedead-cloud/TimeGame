using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    internal sealed class ValidationContext {

        internal ValidationContext(
            AssetIntegration asset,
            ValidationReport report,
            UniversalRenderPipelineAsset urpAsset,
            UniversalRendererData rendererData,
            UrpUtility.PipelineState pipeline,
            VolumeProfile volumeProfile,
            Volume setupVolume,
            string setupVolumeObjectName) {

            Asset = asset;
            Report = report;
            UrpAsset = urpAsset;
            RendererData = rendererData;
            Pipeline = pipeline;
            VolumeProfile = volumeProfile;
            SetupVolume = setupVolume;
            SetupVolumeObjectName = setupVolumeObjectName;
        }

        internal ValidationReport Report { get; }
        internal AssetIntegration Asset { get; }
        internal UniversalRenderPipelineAsset UrpAsset { get; }
        internal UniversalRendererData RendererData { get; }
        internal UrpUtility.PipelineState Pipeline { get; }
        internal VolumeProfile VolumeProfile { get; }
        internal Volume SetupVolume { get; }
        internal string SetupVolumeObjectName { get; }
    }

}

