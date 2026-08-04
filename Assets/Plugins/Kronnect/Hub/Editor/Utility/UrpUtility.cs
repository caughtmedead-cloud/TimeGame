using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kronnect.Hub {

    internal static class UrpUtility {

        static readonly string[] RenderingModePropertyNames = { "m_RenderingMode", "renderingMode", "m_renderingMode" };

        public static bool SetRenderingMode(UniversalRendererData rendererData, RenderingMode mode) {
            if (rendererData == null) return false;

            SerializedObject so = new SerializedObject(rendererData);
            so.Update();

            foreach (string propName in RenderingModePropertyNames) {
                SerializedProperty prop = so.FindProperty(propName);
                if (prop != null) {
                    prop.enumValueIndex = (int)mode;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(rendererData);
                    return true;
                }
            }

            PropertyInfo renderingModeProperty = typeof(UniversalRendererData).GetProperty("renderingMode", 
                BindingFlags.Public | BindingFlags.Instance);
            if (renderingModeProperty != null && renderingModeProperty.CanWrite) {
                Undo.RecordObject(rendererData, "Set Rendering Mode");
                renderingModeProperty.SetValue(rendererData, mode);
                EditorUtility.SetDirty(rendererData);
                return true;
            }

            FieldInfo renderingModeField = typeof(UniversalRendererData).GetField("m_RenderingMode", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (renderingModeField != null) {
                Undo.RecordObject(rendererData, "Set Rendering Mode");
                renderingModeField.SetValue(rendererData, mode);
                EditorUtility.SetDirty(rendererData);
                return true;
            }

            return false;
        }

        public static bool TryAddRenderFeature(UniversalRendererData rendererData, string typeName) {
            if (rendererData == null) return false;
            if (!TypeUtility.TryGetType(typeName, out Type featureType)) return false;
            if (!typeof(ScriptableRendererFeature).IsAssignableFrom(featureType)) return false;

            ScriptableRendererFeature feature = ScriptableObject.CreateInstance(featureType) as ScriptableRendererFeature;
            if (feature == null) return false;

            feature.name = featureType.Name;
            feature.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            SerializedObject so = new SerializedObject(rendererData);
            SerializedProperty featuresProp = so.FindProperty("m_RendererFeatures");
            SerializedProperty mapProp = so.FindProperty("m_RendererFeatureMap");
            int index = featuresProp.arraySize;
            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(index).objectReferenceValue = feature;
            if (mapProp != null) {
                if (mapProp.arraySize < featuresProp.arraySize) {
                    mapProp.arraySize = featuresProp.arraySize;
                }
                mapProp.GetArrayElementAtIndex(index).boolValue = true;
            }
            so.ApplyModifiedProperties();

            feature.Create();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static bool HasCameraWithPostProcessingDisabled() {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Camera camera in cameras) {
                if (camera == null || camera.cameraType != CameraType.Game) {
                    continue;
                }
                UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (data != null && !data.renderPostProcessing) {
                    return true;
                }
            }
            return false;
        }

        public static bool HasCameraWithoutPostProcessingData() {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Camera camera in cameras) {
                if (camera == null || camera.cameraType != CameraType.Game) {
                    continue;
                }
                UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (data == null) {
                    return true;
                }
            }
            return false;
        }

        public static PipelineState GetPipelineState(UniversalRenderPipelineAsset asset) {
            if (asset == null) return null;
            SerializedObject so = new SerializedObject(asset);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            SerializedProperty defaultIndexProp = so.FindProperty("m_DefaultRendererIndex");
            ScriptableRendererData[] rendererData = new ScriptableRendererData[list.arraySize];
            for (int i = 0; i < list.arraySize; i++) {
                rendererData[i] = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
            }
            int defaultIndex = Mathf.Clamp(defaultIndexProp.intValue, 0, Mathf.Max(0, rendererData.Length - 1));
            return new PipelineState(asset, rendererData, defaultIndex);
        }

        internal sealed class PipelineState {
            readonly UniversalRenderPipelineAsset _asset;
            readonly ScriptableRendererData[] _rendererData;
            readonly int _defaultRendererIndex;

            internal PipelineState(UniversalRenderPipelineAsset asset, ScriptableRendererData[] rendererData, int defaultRendererIndex) {
                _asset = asset;
                _rendererData = rendererData;
                _defaultRendererIndex = defaultRendererIndex;
            }

            public bool SupportsCameraDepthTexture => _asset.supportsCameraDepthTexture;

            public CopyDepthMode? CopyDepthMode {
                get {
                    if (DefaultRendererData is UniversalRendererData universalRendererData) {
                        return universalRendererData.copyDepthMode;
                    }
                    return null;
                }
            }

            public RenderingMode? RendererMode {
                get {
                    if (DefaultRendererData is UniversalRendererData universalRendererData) {
                        return universalRendererData.renderingMode;
                    }
                    return null;
                }
            }

            public bool? AccurateGBufferNormals {
                get {
                    if (DefaultRendererData is UniversalRendererData universalRendererData) {
                        return universalRendererData.accurateGbufferNormals;
                    }
                    return null;
                }
            }

            public ScriptableRendererData DefaultRendererData =>
                _defaultRendererIndex >= 0 && _defaultRendererIndex < _rendererData.Length ? _rendererData[_defaultRendererIndex] : null;

            public bool HasRenderFeature(string typeName) {
                if (DefaultRendererData == null) {
                    return false;
                }
                foreach (ScriptableRendererFeature feature in DefaultRendererData.rendererFeatures) {
                    if (feature != null && feature.GetType().FullName == typeName) {
                        return true;
                    }
                }
                return false;
            }
        }
    }

}

