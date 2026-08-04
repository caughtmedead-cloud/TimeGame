using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace RoadSystem
{
    [RequireComponent(typeof(SplineContainer))]
    public class RoadSplineHelper : MonoBehaviour
    {
        [Header("Terrain Conforming")]
        [Tooltip("Automatically adjust spline knots to follow terrain height")]
        public bool conformToTerrain = false;
        
        [Tooltip("Layer mask for terrain raycasting")]
        public LayerMask terrainLayer = -1;
        
        [Tooltip("Offset above terrain surface")]
        public float terrainOffset = 0.1f;

        [Header("Visualization")]
        [Tooltip("Show road direction arrows in Scene view")]
        public bool showDirectionGizmos = true;
        
        [Tooltip("Show distance markers along road")]
        public bool showDistanceMarkers = false;
        
        [Tooltip("Distance between markers (meters)")]
        public float markerInterval = 10f;

        [Header("Auto-Update")]
        [Tooltip("Update terrain conforming in edit mode")]
        public bool updateInEditMode = false;

        private SplineContainer splineContainer;

        private void OnValidate()
        {
            splineContainer = GetComponent<SplineContainer>();
        }

        private void Start()
        {
            splineContainer = GetComponent<SplineContainer>();
            
            if (conformToTerrain)
            {
                ConformToTerrain();
            }
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (!Application.isPlaying && updateInEditMode && conformToTerrain)
            {
                ConformToTerrain();
            }
        }
#endif

        [ContextMenu("Conform to Terrain")]
        public void ConformToTerrain()
        {
            if (splineContainer == null)
            {
                Debug.LogWarning("No SplineContainer found!");
                return;
            }

            var splines = splineContainer.Splines;
            if (splines.Count == 0) return;

            var spline = splines[0];
            
            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];
                Vector3 worldPos = transform.TransformPoint((Vector3)knot.Position);
                
                if (Physics.Raycast(worldPos + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 200f, terrainLayer))
                {
                    Vector3 newWorldPos = hit.point + Vector3.up * terrainOffset;
                    knot.Position = transform.InverseTransformPoint(newWorldPos);
                    spline[i] = knot;
                }
            }

            Debug.Log($"Conformed {spline.Count} spline knots to terrain.");
        }

        [ContextMenu("Smooth Spline Curves")]
        public void SmoothCurves()
        {
            if (splineContainer == null) return;

            var splines = splineContainer.Splines;
            if (splines.Count == 0) return;

            var spline = splines[0];
            
            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];
                knot.Rotation = quaternion.identity;
                spline.SetTangentMode(i, TangentMode.AutoSmooth);
                spline[i] = knot;
            }

            Debug.Log("Smoothed all spline curves.");
        }

        [ContextMenu("Make All Corners Linear")]
        public void MakeLinear()
        {
            if (splineContainer == null) return;

            var splines = splineContainer.Splines;
            if (splines.Count == 0) return;

            var spline = splines[0];
            
            for (int i = 0; i < spline.Count; i++)
            {
                spline.SetTangentMode(i, TangentMode.Linear);
            }

            Debug.Log("Set all knots to linear mode.");
        }

        public float GetTotalRoadLength()
        {
            if (splineContainer == null || splineContainer.Splines.Count == 0) return 0f;
            return splineContainer.Splines[0].GetLength();
        }

        public Vector3 GetPositionAtDistance(float distance)
        {
            if (splineContainer == null || splineContainer.Splines.Count == 0) return Vector3.zero;

            float t = distance / GetTotalRoadLength();
            return (Vector3)splineContainer.EvaluatePosition(0, t);
        }

        public Vector3 GetDirectionAtDistance(float distance)
        {
            if (splineContainer == null || splineContainer.Splines.Count == 0) return Vector3.forward;

            float t = distance / GetTotalRoadLength();
            return ((Vector3)splineContainer.EvaluateTangent(0, t)).normalized;
        }

        private void OnDrawGizmos()
        {
            if (splineContainer == null || splineContainer.Splines.Count == 0) return;

            var spline = splineContainer.Splines[0];
            if (spline.Count == 0) return;

            if (showDirectionGizmos)
            {
                DrawDirectionArrows();
            }

            if (showDistanceMarkers)
            {
                DrawDistanceMarkers();
            }
        }

        private void DrawDirectionArrows()
        {
            var spline = splineContainer.Splines[0];
            float length = spline.GetLength();
            int arrowCount = Mathf.Max(5, Mathf.CeilToInt(length / 5f));

            Gizmos.color = Color.cyan;

            for (int i = 0; i < arrowCount; i++)
            {
                float t = i / (float)(arrowCount - 1);
                
                float3 position = splineContainer.EvaluatePosition(0, t);
                float3 tangent = splineContainer.EvaluateTangent(0, t);

                Vector3 worldPos = transform.TransformPoint(position);
                Vector3 worldTangent = transform.TransformDirection(tangent).normalized;

                Gizmos.DrawRay(worldPos, worldTangent * 2f);
                
                Vector3 arrowLeft = Quaternion.Euler(0, -30, 0) * worldTangent * 0.5f;
                Vector3 arrowRight = Quaternion.Euler(0, 30, 0) * worldTangent * 0.5f;
                
                Gizmos.DrawLine(worldPos + worldTangent * 2f, worldPos + worldTangent * 2f - arrowLeft);
                Gizmos.DrawLine(worldPos + worldTangent * 2f, worldPos + worldTangent * 2f - arrowRight);
            }
        }

        private void DrawDistanceMarkers()
        {
            var spline = splineContainer.Splines[0];
            float length = spline.GetLength();
            int markerCount = Mathf.FloorToInt(length / markerInterval);

            Gizmos.color = Color.yellow;

            for (int i = 0; i <= markerCount; i++)
            {
                float distance = i * markerInterval;
                float t = distance / length;
                
                float3 position = splineContainer.EvaluatePosition(0, t);
                Vector3 worldPos = transform.TransformPoint(position);

                Gizmos.DrawWireSphere(worldPos, 0.3f);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(worldPos + Vector3.up * 0.5f, $"{distance:F0}m");
#endif
            }
        }
    }
}
