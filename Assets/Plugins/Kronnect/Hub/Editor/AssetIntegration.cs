using System;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    internal sealed class AssetIntegration {
        public AssetIntegration(string id, string displayName) {
            Id = id;
            DisplayName = displayName;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; set; }
        public string AssetFolder { get; set; }
        public string InstalledFolder { get; set; }
        public string DocumentationFolder { get; set; }
        public string DemoFolder { get; set; }
        public string AssetStoreUrl { get; set; }
        public string AssetStoreId { get; set; }
        public string PublisherName { get; set; } = "Kronnect";
        public string CacheSearchName { get; set; }
        public bool IsBundle { get; set; }
        public string VolumeComponentType { get; set; }
        public string SceneComponentType { get; set; }
        public string[] AdditionalSceneComponentTypes { get; set; }
        public string[] RenderFeatureTypeNames { get; set; } = Array.Empty<string>();
        /// <summary>
        /// Render feature types to remove during cleanup (even if not part of RenderFeatureTypeNames).
        /// </summary>
        public string[] CleanupRenderFeatureTypeNames { get; set; } = Array.Empty<string>();
        /// <summary>
        /// Renderer data asset names to remove from URP pipeline assets during cleanup.
        /// </summary>
        public string[] CleanupRendererDataAssetNames { get; set; } = Array.Empty<string>();
        public RenderingMode? RequiredRendererMode { get; set; }
        public bool RequiresDepthTexture { get; set; }
        public string IconPath { get; set; }
        /// <summary>
        /// Type that must exist for this asset to be considered installed (used to distinguish variants like Pro vs regular).
        /// </summary>
        public string RequiredTypeName { get; set; }
        /// <summary>
        /// Type that must NOT exist for this asset to be considered installed (used to exclude Pro variant detection for regular version).
        /// </summary>
        public string ExcludedTypeName { get; set; }
    }

}
