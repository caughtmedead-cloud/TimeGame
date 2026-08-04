using UnityEngine;

namespace Thelos.AI
{
    public class PatrolPointManager : MonoBehaviour
    {
        [Header("Patrol Points")]
        [SerializeField] private Transform[] points;
        [SerializeField] private bool createPointsAsChildren = true;
        [SerializeField] private int numberOfPoints = 4;
        [SerializeField] private float patrolRadius = 5f;
        
        public Transform[] Points => points;
        
        [ContextMenu("Create Patrol Points In Circle")]
        private void CreatePatrolPointsInCircle()
        {
            ClearExistingPoints();
            
            points = new Transform[numberOfPoints];
            
            for (int i = 0; i < numberOfPoints; i++)
            {
                float angle = (360f / numberOfPoints) * i;
                float radians = angle * Mathf.Deg2Rad;
                
                Vector3 position = transform.position + new Vector3(
                    Mathf.Cos(radians) * patrolRadius,
                    Mathf.Sin(radians) * patrolRadius,
                    0
                );
                
                GameObject point = new GameObject($"PatrolPoint_{i}");
                point.transform.position = position;
                
                if (createPointsAsChildren)
                {
                    point.transform.SetParent(transform);
                }
                
                points[i] = point.transform;
            }
        }
        
        [ContextMenu("Create Patrol Points In Line")]
        private void CreatePatrolPointsInLine()
        {
            ClearExistingPoints();
            
            points = new Transform[numberOfPoints];
            float spacing = patrolRadius * 2f / (numberOfPoints - 1);
            
            for (int i = 0; i < numberOfPoints; i++)
            {
                Vector3 position = transform.position + new Vector3(
                    -patrolRadius + (spacing * i),
                    0,
                    0
                );
                
                GameObject point = new GameObject($"PatrolPoint_{i}");
                point.transform.position = position;
                
                if (createPointsAsChildren)
                {
                    point.transform.SetParent(transform);
                }
                
                points[i] = point.transform;
            }
        }
        
        [ContextMenu("Clear Patrol Points")]
        private void ClearExistingPoints()
        {
            if (points != null)
            {
                foreach (Transform point in points)
                {
                    if (point != null && createPointsAsChildren)
                    {
                        DestroyImmediate(point.gameObject);
                    }
                }
            }
            
            points = new Transform[0];
        }
        
        private void OnDrawGizmos()
        {
            if (points == null || points.Length == 0) return;
            
            Gizmos.color = Color.cyan;
            
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null) continue;
                
                Gizmos.DrawWireSphere(points[i].position, 0.3f);
                
                if (i < points.Length - 1 && points[i + 1] != null)
                {
                    Gizmos.DrawLine(points[i].position, points[i + 1].position);
                }
            }
        }
    }
}
