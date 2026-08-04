using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kronnect.Hub {

    sealed class VolumetricFogValidationProvider : IValidationProvider {

        const string SceneComponentType = "VolumetricFogAndMist2.VolumetricFog";
        const string ProfileType = "VolumetricFogAndMist2.VolumetricFogProfile";

        public void Validate(ValidationContext context) {
            if (!string.Equals(context.Asset.Id, KronnectAssetIds.VolumetricFog, StringComparison.Ordinal)) {
                return;
            }

            ValidateProfileAssigned(context);
            AddSelectManagerOptionAction(context);
        }

        public void OnValidationComplete(ValidationContext context) {
        }

        void ValidateProfileAssigned(ValidationContext context) {
            Component[] fogComponents = VolumeUtility.FindAllSceneComponents(SceneComponentType);
            if (fogComponents.Length == 0) {
                return;
            }

            foreach (Component fogComponent in fogComponents) {
                SerializedObject so = new SerializedObject(fogComponent);
                SerializedProperty profileProp = so.FindProperty("profile");

                bool hasProfile = profileProp != null && profileProp.objectReferenceValue != null;
                string componentName = fogComponent.gameObject.name;

                context.Report.Add(
                    $"Check '{componentName}' has a Volumetric Fog Profile assigned",
                    hasProfile,
                    $"Volumetric Fog component on '{componentName}' requires a profile to configure fog appearance and behavior. " +
                    "Without a profile, the fog effect will not render correctly.",
                    hasProfile ? null : (Action)(() => EnsureProfileAssigned(fogComponent)),
                    fogComponent.gameObject);
            }
        }

        void AddSelectManagerOptionAction(ValidationContext context) {
            Component[] fogComponents = VolumeUtility.FindAllSceneComponents(SceneComponentType);
            if (fogComponents.Length == 0) {
                return;
            }

            context.Report.AddOptionalAction(
                "(Optional) Select Volumetric Fog Manager to Customize Global Fog Settings",
                "Selects the Volumetric Fog Manager component in the Inspector to allow customization of global fog settings including downscaling, render queue, performance options, and shader keywords.",
                null,
                null,
                () => SelectFogManager(fogComponents[0]),
                "Settings");
        }

        static void SelectFogManager(Component fogComponent) {
            if (!TypeUtility.TryGetType("VolumetricFogAndMist2.VolumetricFogManager", out Type managerType)) {
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

            ScriptableObject profile = VolumeUtility.CreateScriptableAsset(ProfileType, "VolumetricFogProfile");
            if (profile == null) {
                Debug.LogWarning("Failed to create Volumetric Fog Profile. Ensure the asset is properly installed.");
                return;
            }

            profileProp.objectReferenceValue = profile;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(fogComponent);
            EditorUtility.SetDirty(profile);
        }
    }
}

