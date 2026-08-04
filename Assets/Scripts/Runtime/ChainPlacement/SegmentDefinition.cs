using UnityEngine;

namespace ChainPlacement
{
    [System.Serializable]
    public class SpawnPivot
    {
        [Tooltip("Name for this spawn point (e.g., 'Forward', 'Left', 'Right', 'Up')")]
        public string name = "Forward";
        
        [Tooltip("Local position offset where the next segment will spawn")]
        public Vector3 position = new Vector3(0f, 0f, 1f);
        
        [Tooltip("Local rotation offset for the next segment")]
        public Vector3 rotation = Vector3.zero;
        
        public SpawnPivot()
        {
            name = "Forward";
            position = new Vector3(0f, 0f, 1f);
            rotation = Vector3.zero;
        }
        
        public SpawnPivot(string pivotName, Vector3 pivotPosition, Vector3 pivotRotation)
        {
            name = pivotName;
            position = pivotPosition;
            rotation = pivotRotation;
        }
    }
    
    [System.Serializable]
    public class SegmentDefinition
    {
        [Header("Segment Info")]
        public string segmentName = "Unnamed Segment";
        public GameObject prefab;
        
        [Header("Spawn Pivots")]
        [Tooltip("Spawn points where next segments can connect. Multiple pivots enable T-junctions, crosses, etc.")]
        public SpawnPivot[] spawnPivots = new SpawnPivot[] 
        { 
            new SpawnPivot("Forward", new Vector3(0f, 0f, 1f), Vector3.zero) 
        };
        
        [Header("Legacy (Auto-Migrated)")]
        [Tooltip("Legacy single spawn pivot - automatically migrated to spawnPivots array")]
        public Vector3 spawnPivotPosition = new Vector3(0f, 0f, 1f);
        public Vector3 spawnPivotRotation = Vector3.zero;
        
        [Header("Visual")]
        [Tooltip("Optional icon for inspector buttons")]
        public Texture2D icon;
        
        [Header("Placement Options")]
        [Tooltip("Can this segment be placed as the first segment in a chain?")]
        public bool canBeStartSegment = true;
        
        [Tooltip("Can other segments connect to this one?")]
        public bool canHaveChildren = true;
        
        [Tooltip("Default scale for this segment when placed (1.0 = original size)")]
        [Range(0.1f, 10f)]
        public float defaultScale = 1f;
        
        public bool IsValid()
        {
            return prefab != null;
        }
        
        public void MigrateLegacyPivot()
        {
            if (spawnPivots == null || spawnPivots.Length == 0)
            {
                spawnPivots = new SpawnPivot[] 
                { 
                    new SpawnPivot("Forward", spawnPivotPosition, spawnPivotRotation) 
                };
            }
        }
        
        public SpawnPivot GetSpawnPivot(int index)
        {
            if (spawnPivots == null || spawnPivots.Length == 0)
                return null;
            
            if (index < 0 || index >= spawnPivots.Length)
                return null;
            
            return spawnPivots[index];
        }
    }
}
