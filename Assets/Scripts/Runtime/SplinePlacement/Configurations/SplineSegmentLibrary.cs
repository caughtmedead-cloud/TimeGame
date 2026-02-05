using UnityEngine;
using System.Collections.Generic;

namespace SplinePlacement
{
    [CreateAssetMenu(fileName = "New Segment Library", menuName = "Spline Placement/Segment Library")]
    public class SplineSegmentLibrary : ScriptableObject
    {
        public string libraryName;
        public List<SplineSegmentData> segments = new List<SplineSegmentData>();
        public float defaultSegmentSpacing = 1f;
        public bool autoDetectAngles = true;
        
        public SplineSegmentData GetSegmentForAngle(float angle)
        {
            foreach (var segment in segments)
            {
                if (angle >= segment.angleMin && angle <= segment.angleMax)
                {
                    return segment;
                }
            }
            
            return segments.Count > 0 ? segments[0] : null;
        }
        
        public SplineSegmentData GetSegmentByType(SegmentType type)
        {
            return segments.Find(s => s.segmentType == type);
        }
    }
}
