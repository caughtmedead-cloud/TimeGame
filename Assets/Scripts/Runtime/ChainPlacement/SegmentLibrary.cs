using UnityEngine;

namespace ChainPlacement
{
    [CreateAssetMenu(fileName = "New Segment Library", menuName = "Chain Placement/Segment Library")]
    public class SegmentLibrary : ScriptableObject
    {
        [Header("Library Info")]
        public string libraryName = "Pipes";
        
        [TextArea(2, 4)]
        public string description = "Collection of segments for chain placement.";
        
        [Header("Segments")]
        public SegmentDefinition[] segments = new SegmentDefinition[0];
        
        [Header("Preview Settings")]
        [Tooltip("Color for spawn pivot gizmos")]
        public Color pivotGizmoColor = new Color(0f, 1f, 0.5f, 0.8f);
        
        [Tooltip("Color for segment preview meshes")]
        public Color previewColor = new Color(0f, 1f, 1f, 0.3f);
        
        [Header("Placement Settings")]
        [Tooltip("Snap position to grid during initial placement")]
        public bool snapToGrid = true;
        
        [Tooltip("Grid size for snapping")]
        public float gridSize = 1f;
        
        [Tooltip("Snap rotation to nearest 90 degrees")]
        public bool snapRotation = true;
        
        public SegmentDefinition GetSegment(int index)
        {
            if (index < 0 || index >= segments.Length)
                return null;
            
            return segments[index];
        }
        
        public int GetValidSegmentCount()
        {
            int count = 0;
            foreach (var seg in segments)
            {
                if (seg != null && seg.IsValid())
                    count++;
            }
            return count;
        }
    }
}
