using UnityEngine;

namespace TimeGame.Player
{
    /// <summary>
    /// Simple script to toggle cursor lock state for inventory access.
    /// 
    /// Controls:
    /// - Tab: Toggle cursor lock (for inventory/UI interaction)
    /// - Escape: Unlock cursor (alternative)
    /// 
    /// When cursor is unlocked, you can interact with UI while still being able to move.
    /// </summary>
    public class CursorToggle : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Start with cursor locked (typical for FPS games)")]
        [SerializeField] private bool startLocked = true;

        [Header("Debug")]
        [Tooltip("Show current cursor state in console")]
        [SerializeField] private bool showDebugMessages = false;

        private bool isCursorLocked;

        void Start()
        {
            // Set initial cursor state
            if (startLocked)
            {
                LockCursor();
            }
            else
            {
                UnlockCursor();
            }
        }

        void Update()
        {
            // Tab key toggles cursor lock
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleCursor();
            }

            // Escape key always unlocks cursor
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                UnlockCursor();
            }
        }

        private void ToggleCursor()
        {
            if (isCursorLocked)
            {
                UnlockCursor();
            }
            else
            {
                LockCursor();
            }
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            isCursorLocked = true;

            if (showDebugMessages)
            {
                Debug.Log("[CursorToggle] Cursor LOCKED (gameplay mode)");
            }
        }

        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            isCursorLocked = false;

            if (showDebugMessages)
            {
                Debug.Log("[CursorToggle] Cursor UNLOCKED (UI mode)");
            }
        }

        /// <summary>
        /// Public method to check if cursor is currently locked
        /// </summary>
        public bool IsCursorLocked()
        {
            return isCursorLocked;
        }

        /// <summary>
        /// Public method to force lock cursor (called from other scripts if needed)
        /// </summary>
        public void ForceLockCursor()
        {
            LockCursor();
        }

        /// <summary>
        /// Public method to force unlock cursor (called from other scripts if needed)
        /// </summary>
        public void ForceUnlockCursor()
        {
            UnlockCursor();
        }
    }
}
