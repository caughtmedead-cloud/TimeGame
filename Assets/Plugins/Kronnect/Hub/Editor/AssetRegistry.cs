using System;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    internal static class AssetRegistry {

        const string AssetsFolderPath = "Assets/Plugins/Kronnect/Hub/Editor/Assets/";

        public static readonly IReadOnlyList<AssetIntegration> Assets = new List<AssetIntegration> {
            new AssetIntegration(KronnectAssetIds.Beautify, "Beautify 3") {
                Description = "Advanced post-processing effects",
                AssetFolder = "Assets/Beautify",
                DocumentationFolder = "Assets/Beautify/URP/Documentation",
                DemoFolder = "Assets/Beautify/URP/Demo",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/beautify-3-advanced-post-processing-233073",
                AssetStoreId = "233073",
                CacheSearchName = "Beautify 3",
                IsBundle = true,
                VolumeComponentType = "Beautify.Universal.Beautify",
                AdditionalSceneComponentTypes = new[] { "Beautify.Universal.BeautifySettings" },
                RenderFeatureTypeNames = new[] { "Beautify.Universal.BeautifyRendererFeature" },
                IconPath = AssetsFolderPath + "Beautify/BeautifyIcon.png"
            },
            new AssetIntegration(KronnectAssetIds.DynamicFog, "Dynamic Fog & Mist 2") {
                Description = "Volumetric fog with sub-volumes",
                AssetFolder = "Assets/DynamicFog",
                InstalledFolder = "Assets/DynamicFogURP",
                DocumentationFolder = "Assets/DynamicFogURP/Documentation",
                DemoFolder = "Assets/DynamicFogURP/Demo",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/dynamic-fog-mist-2-48200",
                AssetStoreId = "48200",
                CacheSearchName = "Dynamic Fog",
                IsBundle = true,
                SceneComponentType = "DynamicFogAndMist2.DynamicFog",
                AdditionalSceneComponentTypes = new[] { "DynamicFogAndMist2.DynamicFogManager" },
                RequiresDepthTexture = true,
                IconPath = AssetsFolderPath + "DynamicFog/DynamicFogIcon.png"
            },
            new AssetIntegration(KronnectAssetIds.EdgeFusion, "Edge Fusion") {
                Description = "Smooth surface contacts",
                AssetFolder = "Assets/Edge Fusion",
                DocumentationFolder = "Assets/Edge Fusion/Documentation",
                DemoFolder = "Assets/Edge Fusion/Demo",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/edge-fusion-smooth-surface-contacts-334484",
                AssetStoreId = "334484",
                CacheSearchName = "Edge Fusion",
                VolumeComponentType = "EdgeFusion.EdgeFusion",
                RenderFeatureTypeNames = new[] { "EdgeFusion.EdgeFusionRenderFeature" },
                IconPath = AssetsFolderPath + "EdgeFusion/EdgeFusionIcon.png"
            },
            new AssetIntegration(KronnectAssetIds.GlobalSnow, "Global Snow 2") {
                Description = "Dynamic snow coverage",
                AssetFolder = "Assets/GlobalSnow 2",
                InstalledFolder = "Assets/GlobalSnow",
                DocumentationFolder = "Assets/GlobalSnow/Documentation",
                DemoFolder = "Assets/GlobalSnow/Demo",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/global-snow-2-248191",
                AssetStoreId = "248191",
                CacheSearchName = "Global Snow 2",
                IsBundle = true,
                SceneComponentType = "GlobalSnowEffect.GlobalSnow",
                RenderFeatureTypeNames = new[] { "GlobalSnowEffect.GlobalSnowRenderFeature" },
                CleanupRendererDataAssetNames = new[] { "GlobalSnowZenithalRenderer" },
                RequiredRendererMode = RenderingMode.Deferred,
                IconPath = AssetsFolderPath + "GlobalSnow/GlobalSnowIcon.png"
            },
            new AssetIntegration(KronnectAssetIds.HighlightPlus, "Highlight Plus 2") {
                Description = "Advanced object highlighting",
                AssetFolder = "Assets/HighlightPlusBundle",
                InstalledFolder = "Assets/HighlightPlus",
                DocumentationFolder = "Assets/HighlightPlus/Documentation",
                DemoFolder = "Assets/HighlightPlus/Demo",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/highlight-plus-2-all-in-one-outline-selection-effects-321005",
                AssetStoreId = "321005",
                CacheSearchName = "Highlight Plus 2",
                IsBundle = true,
                RenderFeatureTypeNames = new[] { "HighlightPlus.HighlightPlusRenderPassFeature" },
                SceneComponentType = "HighlightPlus.HighlightEffect",
                IconPath = AssetsFolderPath + "HighlightPlus2/highlightPlusIcon.png"
            },
            new AssetIntegration(KronnectAssetIds.LiquidVolume, "Liquid Volume 2") {
                Description = "Realistic liquid containers",
                AssetFolder = "Assets/LiquidVolume2",
                InstalledFolder = "Assets/LiquidVolume",
                DocumentationFolder = "Assets/LiquidVolume/Documentation",
                DemoFolder = "Assets/LiquidVolume/Demos",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/liquid-volume-2-249127",
                AssetStoreId = "249127",
                CacheSearchName = "Liquid Volume",
                IsBundle = true,
                SceneComponentType = "LiquidVolumeFX.LiquidVolume",
                CleanupRenderFeatureTypeNames = new[] { "LiquidVolumeFX.LiquidVolumeDepthPrePassRenderFeature" },
                ExcludedTypeName = "LiquidVolumeFX.LiquidVolumeLayer",
                IconPath = AssetsFolderPath + "LiquidVolume2/LiquidVolume2Icon.png"
            },
            new AssetIntegration(KronnectAssetIds.LiquidVolumePro, "Liquid Volume Pro 2") {
                Description = "Advanced liquid containers with multiple layers",
                AssetFolder = "Assets/LiquidVolumeProBundle",
                InstalledFolder = "Assets/LiquidVolumePro",
                DocumentationFolder = "Assets/LiquidVolumePro/Documentation",
                DemoFolder = "Assets/LiquidVolumePro/Demos",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/liquid-volume-pro-2-129967",
                AssetStoreId = "129967",
                CacheSearchName = "Liquid Volume Pro",
                IsBundle = true,
                RenderFeatureTypeNames = new[] { "LiquidVolumeFX.LiquidVolumeDepthPrePassRenderFeature" },
                SceneComponentType = "LiquidVolumeFX.LiquidVolume",
                RequiredTypeName = "LiquidVolumeFX.LiquidVolumeLayer",
                IconPath = AssetsFolderPath + "LiquidVolumePro2/LiquidVolumePro2Icon.png"
            },
            new AssetIntegration(KronnectAssetIds.LBAO, "Luma Based Ambient Occlusion") {
                Description = "For 2D scenes without depth",
                AssetFolder = "Assets/LBAO2",
                InstalledFolder = "Assets/LBAO URP",
                DocumentationFolder = "Assets/LBAO URP/Documentation",
                DemoFolder = "Assets/LBAO URP/Demo",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/luma-based-ambient-occlusion-2-ssao-2d-249066",
                AssetStoreId = "249066",
                CacheSearchName = "Luma Based Ambient Occlusion 2",
                IsBundle = true,
                VolumeComponentType = "LBAOFX.LBAO",
                RenderFeatureTypeNames = new[] { "LBAOFX.LBAORenderFeature" },
                IconPath = AssetsFolderPath + "LBAO2/LBAO2Icon.png"
            },
            new AssetIntegration(KronnectAssetIds.MystifyFX, "Mystify FX") {
                Description = "Blur, distortion and visual effects",
                AssetFolder = "Assets/MystifyFX",
                DocumentationFolder = "Assets/MystifyFX/Documentation",
                DemoFolder = "Assets/MystifyFX/Demo",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/mystify-fx-303296",
                AssetStoreId = "303296",
                CacheSearchName = "Mystify FX",
                RenderFeatureTypeNames = new[] { "MystifyFX.MystifyFXRendererFeature" },
                SceneComponentType = "MystifyFX.MystifyEffect",
                IconPath = AssetsFolderPath + "MystifyFX/MystifyFX.png"
            },
            new AssetIntegration(KronnectAssetIds.Radiant, "Radiant GI") {
                Description = "Real-time global illumination",
                AssetFolder = "Assets/RadiantGI Bundle",
                InstalledFolder = "Assets/RadiantGI",
                DocumentationFolder = "Assets/RadiantGI/Documentation",
                DemoFolder = "Assets/RadiantGI/Demos",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/radiant-global-illumination-225934",
                AssetStoreId = "225934",
                CacheSearchName = "Radiant",
                IsBundle = true,
                VolumeComponentType = "RadiantGI.Universal.RadiantGlobalIllumination",
                RenderFeatureTypeNames = new[] { "RadiantGI.Universal.RadiantRenderFeature" },
                IconPath = AssetsFolderPath + "Radiant/RadiantIcon.png"
            },
            new AssetIntegration(KronnectAssetIds.Shiny, "Shiny SSR 2") {
                Description = "Screen-space reflections",
                AssetFolder = "Assets/ShinySSRR Bundle",
                InstalledFolder = "Assets/ShinySSRR",
                DocumentationFolder = "Assets/ShinySSRR/Documentation",
                DemoFolder = "Assets/ShinySSRR/Demo",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/shiny-ssr-2-screen-space-reflections-188638",
                AssetStoreId = "188638",
                CacheSearchName = "Shiny",
                IsBundle = true,
                VolumeComponentType = "ShinySSRR.ShinyScreenSpaceRaytracedReflections",
                RenderFeatureTypeNames = new[] { "ShinySSRR.ShinySSRR" },
                IconPath = AssetsFolderPath + "Shiny/ShinyIcon.png"
            },
            new AssetIntegration(KronnectAssetIds.Umbra, "Umbra Soft Shadows") {
                Description = "Soft shadows and contact shadows",
                AssetFolder = "Assets/UmbraSoftShadows",
                DocumentationFolder = "Assets/UmbraSoftShadows/Documentation",
                DemoFolder = "Assets/UmbraSoftShadows/Demo",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/umbra-soft-shadows-better-directional-contact-shadows-for-urp-282485",
                AssetStoreId = "282485",
                CacheSearchName = "Umbra",
                SceneComponentType = "Umbra.UmbraSoftShadows",
                RenderFeatureTypeNames = new[] { "Umbra.UmbraRenderFeature" },
                IconPath = AssetsFolderPath + "Umbra/UmbraIcon.png"
            },
            new AssetIntegration(KronnectAssetIds.VolumetricFog, "Volumetric Fog & Mist 2") {
                Description = "Realistic fog and mist effects",
                AssetFolder = "Assets/VolumetricFogBundle",
                InstalledFolder = "Assets/VolumetricFog2",
                DocumentationFolder = "Assets/VolumetricFog2/Documentation",
                DemoFolder = "Assets/VolumetricFog2/Demo",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/volumetric-fog-mist-2-162694",
                AssetStoreId = "162694",
                CacheSearchName = "Volumetric Fog",
                IsBundle = true,
                RenderFeatureTypeNames = new[] { "VolumetricFogAndMist2.VolumetricFogRenderFeature" },
                SceneComponentType = "VolumetricFogAndMist2.VolumetricFog",
                AdditionalSceneComponentTypes = new[] { "VolumetricFogAndMist2.VolumetricFogManager" },
                RequiresDepthTexture = true,
                IconPath = AssetsFolderPath + "VolumetricFog/VolumetricFogIcon.png"
            },
            new AssetIntegration(KronnectAssetIds.VolumetricLights, "Volumetric Lights 2") {
                Description = "Volumetric scattering effects for local lights",
                AssetFolder = "Assets/VolumetricLightsBundle",
                InstalledFolder = "Assets/VolumetricLights",
                DocumentationFolder = "Assets/VolumetricLights/Documentation",
                DemoFolder = "Assets/VolumetricLights/Demos",
                AssetStoreUrl = "https://assetstore.unity.com/packages/vfx/shaders/volumetric-lights-2-234539",
                AssetStoreId = "234539",
                CacheSearchName = "Volumetric Lights 2",
                IsBundle = true,
                RenderFeatureTypeNames = new[] {
                    "VolumetricLights.VolumetricLightsRenderFeature"
                },
                CleanupRenderFeatureTypeNames = new[] {
                    "VolumetricLights.VolumetricLightsDepthPrePassFeature",
                    "VolumetricLights.VolumetricLightsTranslucentShadowMapFeature"
                },
                SceneComponentType = "VolumetricLights.VolumetricLight",
                AdditionalSceneComponentTypes = new[] {
                    "VolumetricLights.VolumetricLightsTranslucency"
                },
                CleanupRendererDataAssetNames = new[] { "VolumetricLightsDepthRenderer" },
                RequiresDepthTexture = true,
                IconPath = AssetsFolderPath + "VolumetricLights/VolumetricLightsIcon.png"
            }
        };

        public static readonly IReadOnlyList<IValidationProvider> GeneralValidationProviders;
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<IValidationProvider>> AssetValidationProviders;

        static AssetRegistry() {
            GeneralValidationProviders = new IValidationProvider[] {
                new GeneralValidationProvider()
            };

            Dictionary<string, IReadOnlyList<IValidationProvider>> validationProviders = new Dictionary<string, IReadOnlyList<IValidationProvider>>();
            TryAddValidationProvider<BeautifyValidationProvider>(validationProviders, KronnectAssetIds.Beautify);
            TryAddValidationProvider<EdgeFusionValidationProvider>(validationProviders, KronnectAssetIds.EdgeFusion);
            TryAddValidationProvider<ShinyValidationProvider>(validationProviders, KronnectAssetIds.Shiny);
            TryAddValidationProvider<RadiantValidationProvider>(validationProviders, KronnectAssetIds.Radiant);
            TryAddValidationProvider<VolumetricFogValidationProvider>(validationProviders, KronnectAssetIds.VolumetricFog);
            TryAddValidationProvider<VolumetricLightsValidationProvider>(validationProviders, KronnectAssetIds.VolumetricLights);
            TryAddValidationProvider<DynamicFogValidationProvider>(validationProviders, KronnectAssetIds.DynamicFog);
            TryAddValidationProvider<HighlightPlusValidationProvider>(validationProviders, KronnectAssetIds.HighlightPlus);
            TryAddValidationProvider<LiquidVolumeValidationProvider>(validationProviders, KronnectAssetIds.LiquidVolumePro);
            TryAddValidationProvider<LBAOValidationProvider>(validationProviders, KronnectAssetIds.LBAO);
            TryAddValidationProvider<MystifyFXValidationProvider>(validationProviders, KronnectAssetIds.MystifyFX);
            AssetValidationProviders = validationProviders;
        }

        static void TryAddValidationProvider<T>(Dictionary<string, IReadOnlyList<IValidationProvider>> dict, string assetId) where T : IValidationProvider, new() {
            try {
                dict[assetId] = new IValidationProvider[] { new T() };
            } catch (Exception) {
            }
        }
    }

}

