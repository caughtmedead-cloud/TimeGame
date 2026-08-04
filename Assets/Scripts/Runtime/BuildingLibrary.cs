using UnityEngine;

namespace BuildingTools
{
    [CreateAssetMenu(fileName = "New Building Library", menuName = "Building Tools/Building Library")]
    public class BuildingLibrary : ScriptableObject
    {
        [Header("Library Info")]
        public string libraryName = "Building Pieces";
        
        [TextArea(2, 4)]
        public string description = "Collection of building pieces for modular construction.";
        
        [Header("Building Pieces")]
        public BuildingPieceDefinition[] pieces = new BuildingPieceDefinition[0];
        
        [Header("Placement Settings")]
        [Tooltip("Snap position to grid during placement")]
        public bool snapToGrid = true;
        
        [Tooltip("Grid size for snapping (typically matches piece width)")]
        public float gridSize = 4f;
        
        [Tooltip("Height between floors")]
        public float floorHeight = 3f;
        
        [Tooltip("Snap rotation to nearest 90 degrees")]
        public bool snapRotation = true;
        
        [Header("Visual Settings")]
        [Tooltip("Color for snap point gizmos")]
        public Color snapPointColor = new Color(1f, 0.5f, 0f, 0.8f);
        
        [Tooltip("Color for placement preview")]
        public Color previewColor = new Color(0.5f, 1f, 0.5f, 0.5f);
        
        public BuildingPieceDefinition GetPiece(int index)
        {
            if (index < 0 || index >= pieces.Length)
                return null;
            
            return pieces[index];
        }
        
        public int GetValidPieceCount()
        {
            int count = 0;
            foreach (var piece in pieces)
            {
                if (piece != null && piece.IsValid())
                    count++;
            }
            return count;
        }
    }
}
