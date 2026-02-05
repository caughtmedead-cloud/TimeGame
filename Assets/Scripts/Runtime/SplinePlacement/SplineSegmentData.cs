using UnityEngine;

namespace SplinePlacement
{
    [System.Serializable]
    public class SplineSegmentData
    {
        public SegmentType segmentType;
        public GameObject prefab;
        public float length = 1f;
        public Vector3 connectionPointOffset = Vector3.zero;
        public bool canRepeat = true;
        
        [Range(0f, 180f)]
        public float angleMin = 0f;
        
        [Range(0f, 180f)]
        public float angleMax = 180f;
    }
}
