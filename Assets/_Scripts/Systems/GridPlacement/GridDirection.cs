using UnityEngine;

namespace TimeGame.Systems.GridPlacement
{
    /// <summary>
    /// Defines the four cardinal directions for rotation.
    /// This matches CodeMonkey's system for compatibility.
    /// 
    /// Rotation order: Down (0°) → Left (90°) → Up (180°) → Right (270°)
    /// </summary>
    public enum GridDirection
    {
        Down = 0,   // 0°
        Left = 1,   // 90°
        Up = 2,     // 180°
        Right = 3   // 270°
    }
}
