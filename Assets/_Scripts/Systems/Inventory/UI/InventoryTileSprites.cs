using UnityEngine;
using UnityEngine.UI;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Container for inventory grid tile sprites.
    /// Holds references to empty tile, occupied tile, and drag preview (white tile) sprites.
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryTileSprites", menuName = "TimeGame/Inventory/Tile Sprites")]
    public class InventoryTileSprites : ScriptableObject
    {
        [Header("Grid Cell Sprites")]
        [Tooltip("Sprite for empty grid cells")]
        public Sprite emptyTileSprite;
        
        [Tooltip("Sprite type for empty tile (Simple, Sliced, Tiled, Filled)")]
        public Image.Type emptyTileSpriteType = Image.Type.Simple;
        
        [Tooltip("Sprite for occupied grid cells (cells with items)")]
        public Sprite occupiedTileSprite;
        
        [Tooltip("Sprite type for occupied tile")]
        public Image.Type occupiedTileSpriteType = Image.Type.Simple;
        
        [Header("Drag Preview")]
        [Tooltip("Sprite for drag preview (white tile showing where item will land)")]
        public Sprite ghostTileSprite;
        
        [Tooltip("Sprite type for ghost tile")]
        public Image.Type ghostSpriteType = Image.Type.Sliced;
        
        [Tooltip("If using Sliced sprite type, fill center or show borders only")]
        public bool fillCenterTiled = true;
    }
}
