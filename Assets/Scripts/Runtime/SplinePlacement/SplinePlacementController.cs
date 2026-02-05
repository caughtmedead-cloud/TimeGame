using UnityEngine;
using System.Collections.Generic;

namespace SplinePlacement
{
    [ExecuteAlways]
    public class SplinePlacementController : MonoBehaviour
    {
        [Header("Configuration")]
        public SplineSegmentLibrary segmentLibrary;
        
        [Header("Spline Settings")]
        public List<Vector3> splinePoints = new List<Vector3>();
        public float segmentSpacing = 1f;
        public bool autoUpdate = true;
        public bool closedLoop = false;
        
        [Header("Placement Options")]
        public bool alignToSpline = true;
        public bool snapToGround = false;
        public LayerMask groundLayer;
        public float groundOffset = 0f;
        
        [Header("Debug")]
        public bool showGizmos = true;
        public Color splineColor = Color.cyan;
        
        private List<GameObject> placedSegments = new List<GameObject>();
        
        public void RegenerateSegments()
        {
            ClearSegments();
            
            if (segmentLibrary == null || splinePoints.Count < 2)
                return;
            
            PlaceSegmentsAlongSpline();
        }
        
        private void PlaceSegmentsAlongSpline()
        {
            for (int i = 0; i < splinePoints.Count - 1; i++)
            {
                Vector3 start = transform.TransformPoint(splinePoints[i]);
                Vector3 end = transform.TransformPoint(splinePoints[i + 1]);
                
                PlaceSegmentsBetweenPoints(start, end, i);
            }
            
            if (closedLoop && splinePoints.Count > 2)
            {
                Vector3 start = transform.TransformPoint(splinePoints[splinePoints.Count - 1]);
                Vector3 end = transform.TransformPoint(splinePoints[0]);
                PlaceSegmentsBetweenPoints(start, end, splinePoints.Count - 1);
            }
        }
        
        private void PlaceSegmentsBetweenPoints(Vector3 start, Vector3 end, int segmentIndex)
        {
            Vector3 direction = end - start;
            float distance = direction.magnitude;
            direction.Normalize();
            
            float spacing = segmentLibrary.defaultSegmentSpacing > 0 ? segmentLibrary.defaultSegmentSpacing : 1f;
            int segmentCount = Mathf.Max(1, Mathf.FloorToInt(distance / spacing));
            
            for (int i = 0; i < segmentCount; i++)
            {
                float t = (float)i / segmentCount;
                Vector3 position = Vector3.Lerp(start, end, t);
                
                if (snapToGround && Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f, groundLayer))
                {
                    position = hit.point + Vector3.up * groundOffset;
                }
                
                Quaternion rotation = alignToSpline ? Quaternion.LookRotation(direction) : Quaternion.identity;
                
                SplineSegmentData segmentData = GetSegmentDataForPosition(segmentIndex, i, segmentCount);
                if (segmentData?.prefab != null)
                {
                    Vector3 finalPosition = position - rotation * segmentData.connectionPointOffset;
                    
                    GameObject instance = Instantiate(segmentData.prefab, finalPosition, rotation, transform);
                    instance.name = $"{segmentData.prefab.name}_{placedSegments.Count}";
                    placedSegments.Add(instance);
                }
            }
        }
        
        private SplineSegmentData GetSegmentDataForPosition(int splineIndex, int segmentIndex, int totalSegments)
        {
            if (segmentIndex == 0 && splineIndex == 0)
            {
                SplineSegmentData startCap = segmentLibrary.GetSegmentByType(SegmentType.StartCap);
                if (startCap?.prefab != null) return startCap;
            }
            
            if (segmentIndex == totalSegments - 1 && splineIndex == splinePoints.Count - 2 && !closedLoop)
            {
                SplineSegmentData endCap = segmentLibrary.GetSegmentByType(SegmentType.EndCap);
                if (endCap?.prefab != null) return endCap;
            }
            
            if (segmentLibrary.autoDetectAngles && splineIndex > 0 && splineIndex < splinePoints.Count - 1)
            {
                float angle = CalculateAngleAtPoint(splineIndex);
                
                if (angle > 80f && angle < 100f)
                {
                    SplineSegmentData corner = segmentLibrary.GetSegmentByType(SegmentType.Corner90);
                    if (corner?.prefab != null) return corner;
                }
                else if (angle > 40f && angle < 50f)
                {
                    SplineSegmentData corner = segmentLibrary.GetSegmentByType(SegmentType.Corner45);
                    if (corner?.prefab != null) return corner;
                }
            }
            
            return segmentLibrary.GetSegmentByType(SegmentType.Straight);
        }
        
        private float CalculateAngleAtPoint(int index)
        {
            if (index <= 0 || index >= splinePoints.Count - 1)
                return 0f;
            
            Vector3 directionIn = (splinePoints[index] - splinePoints[index - 1]).normalized;
            Vector3 directionOut = (splinePoints[index + 1] - splinePoints[index]).normalized;
            
            return Vector3.Angle(directionIn, directionOut);
        }
        
        public void ClearSegments()
        {
            foreach (var segment in placedSegments)
            {
                if (segment != null)
                {
                    DestroyImmediate(segment);
                }
            }
            placedSegments.Clear();
        }
        
        public void AddSplinePoint(Vector3 worldPosition)
        {
            splinePoints.Add(transform.InverseTransformPoint(worldPosition));
            if (autoUpdate)
                RegenerateSegments();
        }
        
        public void RemoveLastPoint()
        {
            if (splinePoints.Count > 0)
            {
                splinePoints.RemoveAt(splinePoints.Count - 1);
                if (autoUpdate)
                    RegenerateSegments();
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!showGizmos || splinePoints.Count < 2)
                return;
            
            Gizmos.color = splineColor;
            
            for (int i = 0; i < splinePoints.Count - 1; i++)
            {
                Vector3 start = transform.TransformPoint(splinePoints[i]);
                Vector3 end = transform.TransformPoint(splinePoints[i + 1]);
                Gizmos.DrawLine(start, end);
                Gizmos.DrawWireSphere(start, 0.1f);
            }
            
            if (splinePoints.Count > 0)
            {
                Vector3 lastPoint = transform.TransformPoint(splinePoints[splinePoints.Count - 1]);
                Gizmos.DrawWireSphere(lastPoint, 0.1f);
            }
            
            if (closedLoop && splinePoints.Count > 2)
            {
                Vector3 start = transform.TransformPoint(splinePoints[splinePoints.Count - 1]);
                Vector3 end = transform.TransformPoint(splinePoints[0]);
                Gizmos.DrawLine(start, end);
            }
        }
    }
}
