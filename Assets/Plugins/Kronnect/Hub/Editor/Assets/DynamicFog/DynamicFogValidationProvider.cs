using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    sealed class DynamicFogValidationProvider : IValidationProvider {
        const string SceneComponentTypeName = "DynamicFogAndMist2.DynamicFog";
        const string ManagerTypeName = "DynamicFogAndMist2.DynamicFogManager";
        const string ProfileTypeName = "DynamicFogAndMist2.DynamicFogProfile";

        public void Validate(ValidationContext context) {
            if (!string.Equals(context.Asset.Id, KronnectAssetIds.DynamicFog, StringComparison.Ordinal)) {
                return;
            }

            ValidateDepthTextureMode(context);
            ValidateProfileAssigned(context);
            AddSelectManagerOptionAction(context);
        }

        public void OnValidationComplete(ValidationContext context) {
        }

        void ValidateDepthTextureMode(ValidationContext context) {
            if (context.RendererData == null) {
                return;
            }

            CopyDepthMode copyDepthMode = context.RendererData.copyDepthMode;
            bool valid = copyDepthMode == CopyDepthMode.AfterOpaques || copyDepthMode == CopyDepthMode.ForcePrepass;

            context.Report.Add(
                "Check Depth Texture Mode is not After Transparents",
                valid,
                "Checks that depth is copied after opaque objects or during prepass. " +
                "Dynamic Fog requires depth data before transparents. 'After Transparents' mode will cause incorrect rendering.",
                valid ? null : (Action)(() => {
                    Undo.RecordObject(context.RendererData, "Set Depth Texture Mode");
                    context.RendererData.copyDepthMode = CopyDepthMode.AfterOpaques;
                    EditorUtility.SetDirty(context.RendererData);
                    AssetDatabase.SaveAssets();
                }),
                context.RendererData);
        }

        void ValidateProfileAssigned(ValidationContext context) {
            Component[] fogComponents = VolumeUtility.FindAllSceneComponents(SceneComponentTypeName);
            if (fogComponents.Length == 0) {
                return;
            }

            foreach (Component fogComponent in fogComponents) {
                SerializedObject so = new SerializedObject(fogComponent);
                SerializedProperty profileProp = so.FindProperty("profile");

                bool hasProfile = profileProp != null && profileProp.objectReferenceValue != null;
                string componentName = fogComponent.gameObject.name;

                context.Report.Add(
                    $"Check '{componentName}' has a Dynamic Fog Profile assigned",
                    hasProfile,
                    $"Dynamic Fog component on '{componentName}' requires a profile to configure fog appearance and behavior. " +
                    "Without a profile, the fog effect will not render correctly.",
                    hasProfile ? null : (Action)(() => EnsureProfileAssigned(fogComponent)),
                    fogComponent.gameObject);
            }
        }

        void AddSelectManagerOptionAction(ValidationContext context) {
            Component[] fogComponents = VolumeUtility.FindAllSceneComponents(SceneComponentTypeName);
            if (fogComponents.Length == 0) {
                return;
            }

            context.Report.AddOptionalAction(
                "(Optional) Select Dynamic Fog Manager to Customize Global Fog Settings",
                "Selects the Dynamic Fog Manager component in the Inspector to allow customization of global fog settings including sun, downscaling, rendering options, and shader keywords.",
                null,
                null,
                SelectFogManager,
                "Settings");
        }

        static void SelectFogManager() {
            if (!TypeUtility.TryGetType(ManagerTypeName, out Type managerType)) {
                return;
            }

            Component manager = UnityEngine.Object.FindObjectsByType(managerType, FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault() as Component;

            if (manager == null) {
                return;
            }

            Selection.activeGameObject = manager.gameObject;
            EditorGUIUtility.PingObject(manager.gameObject);
        }

        static void EnsureProfileAssigned(Component fogComponent) {
            if (fogComponent == null) {
                return;
            }

            SerializedObject so = new SerializedObject(fogComponent);
            SerializedProperty profileProp = so.FindProperty("profile");
            if (profileProp == null) {
                return;
            }

            if (profileProp.objectReferenceValue != null) {
                return;
            }

            ScriptableObject profile = VolumeUtility.CreateScriptableAsset(ProfileTypeName, "DynamicFogProfile");
            if (profile == null) {
                Debug.LogWarning("Failed to create Dynamic Fog Profile. Ensure the asset is properly installed.");
                return;
            }

            profileProp.objectReferenceValue = profile;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(fogComponent);
            EditorUtility.SetDirty(profile);
        }
    }

}

