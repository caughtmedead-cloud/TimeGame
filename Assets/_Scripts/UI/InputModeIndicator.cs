using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using NewThelos.Systems.Input;

namespace NewThelos.UI
{
    /// <summary>
    /// Visual indicator showing current input mode.
    /// </summary>
    public class InputModeIndicator : NetworkBehaviour
    {
        [SerializeField] private Text indicatorText;
        [SerializeField] private Color gameplayColor = Color.white;
        [SerializeField] private Color uiColor = Color.yellow;
        
        private InputModeManager _inputModeManager;
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (!IsOwner)
            {
                if (indicatorText != null)
                    indicatorText.gameObject.SetActive(false);
                return;
            }
            
            // Find InputModeManager on player
            _inputModeManager = GetComponentInParent<InputModeManager>();
            
            if (_inputModeManager != null)
            {
                // Subscribe to mode changes
                _inputModeManager.OnInputModeChanged += OnInputModeChanged;
                
                // Set initial state
                OnInputModeChanged(_inputModeManager.CurrentMode);
            }
        }
        
        private void OnDestroy()
        {
            if (_inputModeManager != null)
            {
                _inputModeManager.OnInputModeChanged -= OnInputModeChanged;
            }
        }
        
        private void OnInputModeChanged(InputMode newMode)
        {
            if (indicatorText == null)
                return;
            
            switch (newMode)
            {
                case InputMode.Gameplay:
                    indicatorText.text = "MODE: Gameplay (Tab for UI)";
                    indicatorText.color = gameplayColor;
                    break;
                
                case InputMode.UI:
                    indicatorText.text = "MODE: UI (Tab for Gameplay)";
                    indicatorText.color = uiColor;
                    break;
            }
        }
    }
}