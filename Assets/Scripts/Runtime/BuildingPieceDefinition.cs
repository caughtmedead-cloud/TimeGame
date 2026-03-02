using UnityEngine;

namespace BuildingTools
{
    public enum BuildingPieceType
    {
        ConcreteWall,
        ConcreteFloor,
        TileWall,
        TileFloor,
        Door,
        Window,
        Decor
    }
    
    [System.Serializable]
    public class BuildingPieceDefinition
    {
        [Header("Piece Info")]
        public string pieceName = "Unnamed Piece";
        public GameObject prefab;
        public BuildingPieceType pieceType = BuildingPieceType.ConcreteWall;
        
        [Header("Sockets")]
        [Tooltip("Socket-based connections for smart building")]
        public Socket[] sockets = new Socket[0];
        
        [Header("Dimensions")]
        [Tooltip("Grid space this piece occupies")]
        public Vector3 gridSize = new Vector3(4, 3, 0.5f);
        
        [Header("Placement Rules")]
        [Tooltip("Can this be placed on the ground?")]
        public bool canPlaceOnGround = true;
        
        [Tooltip("Can this be stacked on top of other pieces?")]
        public bool canStack = false;
        
        [Tooltip("Tags this piece provides for connections (comma separated)")]
        public string providedTags = "";
        
        public bool IsValid()
        {
            return prefab != null;
        }
        
        public Socket GetSocket(int index)
        {
            if (sockets == null || sockets.Length == 0)
                return null;
            
            if (index < 0 || index >= sockets.Length)
                return null;
            
            return sockets[index];
        }
        
        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(providedTags) || string.IsNullOrEmpty(tag))
                return true;
            
            string[] tags = providedTags.Split(',');
            foreach (string t in tags)
            {
                if (t.Trim().Equals(tag.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
