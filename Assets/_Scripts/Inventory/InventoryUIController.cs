using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;

namespace TimeGame.Inventory
{
    /// <summary>
    /// Manages inventory UI visibility and input for the local player.
    /// Attached to the player prefab to control their inventory UI.
    /// Also disables player movement/look input while inventory is open.
    /// </summary>
    public class InventoryUIController : NetworkBehaviour
    {
        [Header("Inventory UI References")]
        [Tooltip("The Canvas GameObject containing the entire inventory UI")]
        [SerializeField] private GameObject inventoryCanvas;
        
        [Header("Player Input References")]
        [Tooltip("PlayerController component - auto-found if not assigned")]
        [SerializeField] private PlayerController playerController;

        [Tooltip("PlayerInventoryManager component - auto-found if not assigned")]
        [SerializeField] private TimeGame.Systems.Inventory.UI.PlayerInventoryManager playerInventoryManager;
        
        [Header("Input Settings")]
        [Tooltip("Enable debug logging for inventory UI actions")]
        [SerializeField] private bool debugMode = true;
        
        // Private state
        private bool isInventoryOpen = false;
        private PlayerInputActions inputActions;
        private bool cursorWasLocked = false;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Create input actions
            inputActions = new PlayerInputActions();
            
            // Auto-find PlayerController if not assigned
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
                if (playerController == null)
                {
                    Debug.LogError("[InventoryUIController] PlayerController not found! Please assign it in inspector.");
                }
            }

            // Auto-find PlayerInventoryManager if not assigned
            if (playerInventoryManager == null)
            {
                playerInventoryManager = GetComponentInChildren<TimeGame.Systems.Inventory.UI.PlayerInventoryManager>(true);
                if (playerInventoryManager == null)
                {
                    Debug.LogWarning("[InventoryUIController] PlayerInventoryManager not found. Grid refresh on open will be skipped.");
                }
            }
            
            // Validate references
            if (inventoryCanvas == null)
            {
                Debug.LogError("[InventoryUIController] No inventory canvas assigned! Please assign in inspector.");
            }
        }
        
        private void OnEnable()
        {
            if (inputActions != null)
            {
                inputActions.Player.Inventory.performed += OnInventoryToggle;
            }
        }
        
        private void OnDisable()
        {
            if (inputActions != null)
            {
                inputActions.Player.Inventory.performed -= OnInventoryToggle;
            }
            
            // Clean up if inventory was left open
            if (isInventoryOpen)
            {
                CloseInventory(false);
            }
        }
        
        private void Update()
        {
            // Allow ESC key to close inventory (in addition to Tab toggle)
            // Using old Input system here since ESC is a universal close key
            if (IsOwner && isInventoryOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseInventory();
                
                if (debugMode)
                {
                    Debug.Log("[InventoryUIController] Inventory closed via ESC key");
                }
            }
        }
        
        #endregion
        
        #region FishNet Lifecycle
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (IsOwner)
            {
                // Enable input for local player
                inputActions.Player.Enable();
                
                // Start with inventory closed
                if (inventoryCanvas != null)
                {
                    inventoryCanvas.SetActive(false);
                }
                
                if (debugMode)
                {
                    Debug.Log("[InventoryUIController] Initialized for local player. Press Tab to open inventory, ESC to close.");
                }
            }
            else
            {
                // Disable input for non-owned players
                inputActions.Player.Disable();
                
                // Hide inventory UI for remote players
                if (inventoryCanvas != null)
                {
                    inventoryCanvas.SetActive(false);
                }
            }
        }
        
        #endregion
        
        #region Input Handling
        
        private void OnInventoryToggle(InputAction.CallbackContext context)
        {
            if (!IsOwner) return; // Only local player can toggle their inventory
            
            if (isInventoryOpen)
            {
                CloseInventory();
            }
            else
            {
                OpenInventory();
            }
        }
        
        #endregion
        
        #region Inventory UI Control
        
        /// <summary>
        /// Opens the inventory UI and unlocks the cursor
        /// </summary>
        private void OpenInventory()
        {
            if (!IsOwner) return;
            
            isInventoryOpen = true;
            
            // Show inventory UI
            if (inventoryCanvas != null)
            {
                inventoryCanvas.SetActive(true);
            }

            // Refresh all inventory grids to show items added while UI was closed
            if (playerInventoryManager != null)
            {
                playerInventoryManager.RefreshAllGrids();
            }

            // Unlock cursor for UI interaction
            cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Disable player input (movement, look, jump, etc.)
            DisablePlayerInput();
            
            if (debugMode)
            {
                Debug.Log("[InventoryUIController] Inventory opened - player input disabled");
            }
        }
        
        /// <summary>
        /// Closes the inventory UI and restores cursor state
        /// </summary>
        /// <param name="restoreCursor">Whether to restore the previous cursor lock state</param>
        private void CloseInventory(bool restoreCursor = true)
        {
            if (!IsOwner) return;
            
            isInventoryOpen = false;
            
            // Hide inventory UI
            if (inventoryCanvas != null)
            {
                inventoryCanvas.SetActive(false);
            }
            
            // Restore cursor state if requested
            if (restoreCursor && cursorWasLocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            
            // Re-enable player input
            EnablePlayerInput();
            
            if (debugMode)
            {
                Debug.Log("[InventoryUIController] Inventory closed - player input enabled");
            }
        }
        
        #endregion
        
        #region Player Input Control
        
        /// <summary>
        /// Disables player movement/look input while inventory is open
        /// </summary>
        private void DisablePlayerInput()
        {
            if (playerController != null && playerController.enabled)
            {
                // Disable the PlayerController's Update loop
                // This prevents all movement, look, jump, sprint, crouch input
                playerController.enabled = false;
                
                if (debugMode)
                {
                    Debug.Log("[InventoryUIController] PlayerController disabled");
                }
            }
        }
        
        /// <summary>
        /// Re-enables player movement/look input when inventory closes
        /// </summary>
        private void EnablePlayerInput()
        {
            if (playerController != null && !playerController.enabled)
            {
                // Re-enable the PlayerController's Update loop
                playerController.enabled = true;
                
                if (debugMode)
                {
                    Debug.Log("[InventoryUIController] PlayerController enabled");
                }
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Check if the inventory is currently open
        /// </summary>
        public bool IsInventoryOpen => isInventoryOpen;
        
        /// <summary>
        /// Programmatically open the inventory (for external systems)
        /// </summary>
        public void Open()
        {
            if (!IsOwner) return;
            if (!isInventoryOpen)
            {
                OpenInventory();
            }
        }
        
        /// <summary>
        /// Programmatically close the inventory (for external systems)
        /// </summary>
        public void Close()
        {
            if (!IsOwner) return;
            if (isInventoryOpen)
            {
                CloseInventory();
            }
        }
        
        #endregion
    }
}
