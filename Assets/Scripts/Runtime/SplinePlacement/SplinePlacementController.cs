using UnityEngine;
using System.Collections.Generic;

namespace SplinePlacement
{
    [ExecuteAlways]
    public class SplinePlacementController : MonoBehaviour
    {
        [Header("Configuration")]
        public SplineSegmentLibrary segmentLibrary;
        
        [Header("Grid Settings")]
        public float gridSize = 1.82f;
        public bool snapToGrid = true;
        
        [Header("Placement Options")]
        public bool snapToSurface = false;
        public LayerMask surfaceLayer = -1;
        public float surfaceOffset = 0f;
        
        [Header("Preview")]
        public Material previewMaterial;
        public Color previewColor = new Color(0, 1, 0, 0.5f);
        
        [Header("Debug")]
        public bool showGizmos = true;
        public Color gridColor = Color.gray;
        public Color pathColor = Color.cyan;
        
        [System.Serializable]
        public class GridNode
        {
            public Vector3 position;
            public Vector3 direction;
            public SegmentType segmentType;
            public GameObject placedObject;
        }
        
        public List<GridNode> pathNodes = new List<GridNode>();
        private List<GameObject> placedSegments = new List<GameObject>();
        
        public Vector3 SnapToGrid(Vector3 worldPosition)
        {
            if (!snapToGrid)
                return worldPosition;
            
            Vector3 snapped = new Vector3(
                Mathf.Round(worldPosition.x / gridSize) * gridSize,
                worldPosition.y,
                Mathf.Round(worldPosition.z / gridSize) * gridSize
            );
            
            return snapped;
        }
        
        public void AddNode(Vector3 worldPosition, Vector3 direction, SegmentType segmentType)
        {
            Vector3 snappedPos = SnapToGrid(worldPosition);
            
            GridNode node = new GridNode
            {
                position = snappedPos,
                direction = direction,
                segmentType = segmentType
            };
            
            pathNodes.Add(node);
        }
        
        public void CommitPath()
        {
            ClearSegments();
            
            if (segmentLibrary == null || pathNodes.Count == 0)
            {
                Debug.LogWarning("Cannot commit path: No segment library or no nodes.");
                return;
            }
            
            Debug.Log($"Committing path with {pathNodes.Count} nodes...");
            ApplyStartAndEndCaps();
            
            for (int i = 0; i < pathNodes.Count; i++)
            {
                GridNode node = pathNodes[i];
                SplineSegmentData segmentData = segmentLibrary.GetSegmentByType(node.segmentType);
                
                if (segmentData?.prefab != null)
                {
                    Quaternion rotation = CalculateSegmentRotation(node, i);
                    
                    GameObject instance = Instantiate(segmentData.prefab, node.position, rotation, transform);
                    instance.name = $"{segmentData.segmentType}_{i}";
                    placedSegments.Add(instance);
                    node.placedObject = instance;
                    
                    Debug.Log($"  Node {i}: {node.segmentType} at {node.position}, direction: {node.direction}");
                }
                else
                {
                    Debug.LogWarning($"  Node {i}: Missing prefab for {node.segmentType}");
                }
            }
            
            Debug.Log($"Committed {placedSegments.Count} segments.");
        }
        
        private Quaternion CalculateSegmentRotation(GridNode node, int index)
        {
            if (node.direction == Vector3.zero)
                return Quaternion.identity;
            
            if (node.segmentType == SegmentType.Corner90)
            {
                Vector3 incomingDir = index > 0 ? pathNodes[index - 1].direction : Vector3.forward;
                Vector3 outgoingDir = node.direction;
                
                float angle = Vector3.SignedAngle(incomingDir, outgoingDir, Vector3.up);
                
                Quaternion faceIncoming = Quaternion.LookRotation(incomingDir);
                Quaternion tiltDown = Quaternion.Euler(90f, 0f, 0f);
                Quaternion turnLeft = Quaternion.Euler(0f, angle, 0f);
                
                Debug.Log($"    Corner: incoming={incomingDir}, outgoing={outgoingDir}, angle={angle}");
                
                return turnLeft * faceIncoming * tiltDown;
            }
            else
            {
                return Quaternion.LookRotation(node.direction) * Quaternion.Euler(90f, 0f, 0f);
            }
        }
        
        private void ApplyStartAndEndCaps()
        {
            if (pathNodes.Count == 0)
            {
                Debug.LogWarning("ApplyStartAndEndCaps: No nodes to process.");
                return;
            }
            
            Debug.Log($"ApplyStartAndEndCaps: Processing {pathNodes.Count} nodes...");
            
            if (pathNodes.Count == 1)
            {
                Debug.Log("  Single node - setting to StartCap");
                pathNodes[0].segmentType = SegmentType.StartCap;
            }
            else if (pathNodes.Count > 1)
            {
                Debug.Log($"  First node (was {pathNodes[0].segmentType}) -> StartCap");
                pathNodes[0].segmentType = SegmentType.StartCap;
                Debug.Log($"  Last node (was {pathNodes[pathNodes.Count - 1].segmentType}) -> EndCap");
                pathNodes[pathNodes.Count - 1].segmentType = SegmentType.EndCap;
            }
        }
        
        public void ClearPath()
        {
            pathNodes.Clear();
            ClearSegments();
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
        
        private void OnDrawGizmos()
        {
            if (!showGizmos)
                return;
            
            if (snapToGrid)
            {
                DrawGrid();
            }
            
            DrawPath();
        }
        
        private void DrawGrid()
        {
            Gizmos.color = gridColor * 0.3f;
            Vector3 center = transform.position;
            int gridCount = 20;
            float halfSize = gridCount * gridSize * 0.5f;
            
            for (int i = -gridCount/2; i <= gridCount/2; i++)
            {
                Vector3 start = center + new Vector3(i * gridSize, 0, -halfSize);
                Vector3 end = center + new Vector3(i * gridSize, 0, halfSize);
                Gizmos.DrawLine(start, end);
                
                start = center + new Vector3(-halfSize, 0, i * gridSize);
                end = center + new Vector3(halfSize, 0, i * gridSize);
                Gizmos.DrawLine(start, end);
            }
        }
        
        private void DrawPath()
        {
            if (pathNodes.Count == 0)
                return;
            
            Gizmos.color = pathColor;
            
            for (int i = 0; i < pathNodes.Count; i++)
            {
                Vector3 pos = pathNodes[i].position;
                Gizmos.DrawWireSphere(pos, 0.2f);
                
                if (i > 0)
                {
                    Gizmos.DrawLine(pathNodes[i - 1].position, pos);
                }
            }
        }
    }
}
