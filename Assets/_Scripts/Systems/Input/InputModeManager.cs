using UnityEngine;

namespace NewThelos.Systems.Input
{
    /// <summary>
    /// Manages input modes: Gameplay (mouse locked) vs UI (mouse free).
    /// Attach to the Player prefab.
    /// 
    /// Controls:
    /// - Tab: Toggle between gameplay and UI mode
    /// - Escape: Switch to UI mode
    /// </summary>
    public class InputModeManager : MonoBehaviour
    {
        [Header("Input Settings")]
        [Tooltip("Key to toggle between gameplay and UI mode")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
        
        [Tooltip("Key to force UI mode (same as toggle but doesn't toggle back)")]
        [SerializeField] private KeyCode uiModeKey = KeyCode.Escape;
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;
        
        // Current mode
        private InputMode _currentMode = InputMode.Gameplay;
        
        // Events that other systems can subscribe to
        public event System.Action<InputMode> OnInputModeChanged;
        
        public InputMode CurrentMode => _currentMode;
        
        private void Start()
        {
            // Start in gameplay mode
            SetInputMode(InputMode.Gameplay);
        }
        
        private void Update()
        {
            // Toggle between modes with Tab
            if (UnityEngine.Input.GetKeyDown(toggleKey))
            {
                ToggleInputMode();
            }
            
            // Force UI mode with Escape
            if (UnityEngine.Input.GetKeyDown(uiModeKey))
            {
                if (_currentMode != InputMode.UI)
                {
                    SetInputMode(InputMode.UI);
                }
            }
        }
        
        /// <summary>
        /// Toggle between Gameplay and UI mode
        /// </summary>
        public void ToggleInputMode()
        {
            InputMode newMode = _currentMode == InputMode.Gameplay ? InputMode.UI : InputMode.Gameplay;
            SetInputMode(newMode);
        }
        
        /// <summary>
        /// Set specific input mode
        /// </summary>
        public void SetInputMode(InputMode mode)
        {
            if (_currentMode == mode)
                return;
            
            _currentMode = mode;
            
            // Apply cursor state based on mode
            switch (mode)
            {
                case InputMode.Gameplay:
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    break;
                
                case InputMode.UI:
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
            }
            
            // Notify subscribers
            OnInputModeChanged?.Invoke(_currentMode);
            
            if (verboseLogging)
                Debug.Log($"[InputModeManager] Mode changed to: {_currentMode}");
        }
        
        /// <summary>
        /// Check if player is in gameplay mode (can move, shoot, etc.)
        /// </summary>
        public bool IsGameplayMode()
        {
            return _currentMode == InputMode.Gameplay;
        }
        
        /// <summary>
        /// Check if player is in UI mode (can click buttons, drag items, etc.)
        /// </summary>
        public bool IsUIMode()
        {
            return _currentMode == InputMode.UI;
        }
    }
    
    /// <summary>
    /// Input mode states
    /// </summary>
    public enum InputMode
    {
        Gameplay,   // Mouse locked, player can move/look
        UI          // Mouse free, player can interact with UI
    }
}