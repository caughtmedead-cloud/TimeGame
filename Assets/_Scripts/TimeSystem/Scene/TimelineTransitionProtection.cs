using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Scened;

/// <summary>
/// Prevents player from falling through floor during timeline transitions.
/// ROBUST VERSION: Waits for actual scene load completion, not just a timer.
/// Designed specifically for CharacterController-based movement.
/// </summary>
[RequireComponent(typeof(TimelineManager))]
public class TimelineTransitionProtection : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Collider playerCollider;
    
    [Header("Optional - For Rigidbody Players")]
    [SerializeField] private Rigidbody rb;
    
    [Header("Settings")]
    [Tooltip("Freeze player in place during transition")]
    [SerializeField] private bool freezePosition = true;
    
    [Tooltip("Extra safety frames after scene loads")]
    [SerializeField] private int safetyFrameCount = 3;
    
    [Tooltip("Enable verbose logging")]
    [SerializeField] private bool verboseLogging = true;
    
    private TimelineManager timelineManager;
    private bool isProtected = false;
    private Vector3 frozenPosition;
    private int safetyFramesRemaining = 0;
    
    // Track if we're waiting for scene load
    private bool waitingForSceneLoad = false;
    private string expectedSceneName = "";
    
    // Store original settings
    private bool originalControllerEnabled;
    private bool originalIsKinematic;
    
    // Reference to movement script (if you want to disable it)
    private MonoBehaviour movementScript;
    
    private void Awake()
    {
        timelineManager = GetComponent<TimelineManager>();
        
        // Auto-find components if not assigned
        if (characterController == null) 
            characterController = GetComponent<CharacterController>();
        
        if (playerCollider == null) 
            playerCollider = GetComponent<Collider>();
        
        if (rb == null) 
            rb = GetComponent<Rigidbody>();
        
        // Try to find common movement script names (add yours if different)
        if (movementScript == null)
        {
            movementScript = GetComponent<MonoBehaviour>();
            // Add your movement script type here, e.g.:
            // movementScript = GetComponent<PlayerMovement>();
            // movementScript = GetComponent<FirstPersonController>();
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (!IsOwner) return;
        
        // Subscribe to timeline transitions
        if (timelineManager != null)
        {
            timelineManager.OnTimelineTransition += OnTimelineTransition;
        }
        
        // Subscribe to FishNet scene load events
        if (base.SceneManager != null)
        {
            base.SceneManager.OnLoadEnd += OnSceneLoadEnd;
        }
        else
        {
            Debug.LogError("[TransitionProtection] SceneManager is null! Cannot subscribe to scene events.");
        }
        
        if (verboseLogging)
            Debug.Log($"[TransitionProtection] Initialized for Player {Owner.ClientId}");
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        if (!IsOwner) return;
        
        // Unsubscribe from events
        if (timelineManager != null)
        {
            timelineManager.OnTimelineTransition -= OnTimelineTransition;
        }
        
        if (base.SceneManager != null)
        {
            base.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
        }
    }
    
    /// <summary>
    /// Called when timeline transition starts (client-side).
    /// This fires BEFORE the scene actually loads.
    /// </summary>
    private void OnTimelineTransition(TimelineManager.TimelineState newTimeline)
    {
        if (!IsOwner) return;
        
        // Determine expected scene name
        expectedSceneName = newTimeline switch
        {
            TimelineManager.TimelineState.Past => "Timeline_Past",
            TimelineManager.TimelineState.Present => "Timeline_Present",
            TimelineManager.TimelineState.Future => "Timeline_Future",
            _ => ""
        };
        
        if (verboseLogging)
            Debug.Log($"[TransitionProtection] Timeline transition to {newTimeline} starting - enabling protection until {expectedSceneName} loads");
        
        EnableProtection();
    }
    
    /// <summary>
    /// FishNet callback: Called when scene loading completes.
    /// This is when we know it's safe to re-enable physics.
    /// </summary>
    private void OnSceneLoadEnd(SceneLoadEndEventArgs args)
    {
        if (!IsOwner || !waitingForSceneLoad) return;
        
        // Check if our expected scene was loaded
        bool ourSceneLoaded = false;
        foreach (Scene loadedScene in args.LoadedScenes)
        {
            if (loadedScene.name == expectedSceneName)
            {
                ourSceneLoaded = true;
                
                if (verboseLogging)
                    Debug.Log($"[TransitionProtection] Target scene {expectedSceneName} loaded! Starting safety frame countdown...");
                
                break;
            }
        }
        
        if (ourSceneLoaded)
        {
            waitingForSceneLoad = false;
            safetyFramesRemaining = safetyFrameCount;
            
            // Don't disable protection yet - wait for safety frames
        }
    }
    
    private void EnableProtection()
    {
        if (isProtected)
        {
            if (verboseLogging)
                Debug.LogWarning("[TransitionProtection] Protection already enabled!");
            return;
        }
        
        isProtected = true;
        waitingForSceneLoad = true;
        frozenPosition = transform.position;
        
        // Disable CharacterController
        if (characterController != null)
        {
            originalControllerEnabled = characterController.enabled;
            characterController.enabled = false;
            
            if (verboseLogging)
                Debug.Log($"[TransitionProtection] CharacterController disabled at position {frozenPosition}");
        }
        
        // Disable Rigidbody physics (if present)
        if (rb != null)
        {
            originalIsKinematic = rb.isKinematic;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            if (verboseLogging)
                Debug.Log($"[TransitionProtection] Rigidbody set to kinematic");
        }
        
        // Optional: Disable movement script
        // Uncomment and adjust if you have a specific movement script
        // if (movementScript != null)
        // {
        //     movementScript.enabled = false;
        // }
    }
    
    private void Update()
    {
        if (!IsOwner || !isProtected) return;
        
        // Freeze position during protection
        if (freezePosition)
        {
            transform.position = frozenPosition;
        }
        
        // Count down safety frames after scene loads
        if (!waitingForSceneLoad && safetyFramesRemaining > 0)
        {
            safetyFramesRemaining--;
            
            if (verboseLogging)
                Debug.Log($"[TransitionProtection] Safety frame {safetyFrameCount - safetyFramesRemaining}/{safetyFrameCount}");
            
            if (safetyFramesRemaining <= 0)
            {
                DisableProtection();
            }
        }
    }
    
    private void LateUpdate()
    {
        if (!IsOwner || !isProtected) return;
        
        // Extra safety: Force position again after all other updates
        if (freezePosition)
        {
            transform.position = frozenPosition;
        }
    }
    
    private void DisableProtection()
    {
        if (!isProtected)
        {
            if (verboseLogging)
                Debug.LogWarning("[TransitionProtection] Protection already disabled!");
            return;
        }
        
        isProtected = false;
        waitingForSceneLoad = false;
        
        // Re-enable CharacterController
        if (characterController != null)
        {
            characterController.enabled = originalControllerEnabled;
            
            if (verboseLogging)
                Debug.Log($"[TransitionProtection] CharacterController re-enabled at position {transform.position}");
        }
        
        // Re-enable Rigidbody physics (if present)
        if (rb != null)
        {
            rb.isKinematic = originalIsKinematic;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            if (verboseLogging)
                Debug.Log($"[TransitionProtection] Rigidbody physics restored");
        }
        
        // Optional: Re-enable movement script
        // if (movementScript != null)
        // {
        //     movementScript.enabled = true;
        // }
        
        if (verboseLogging)
            Debug.Log($"[TransitionProtection] Protection period ended - player can move again");
    }
    
    // ===== DEBUG COMMANDS =====
    
    [ContextMenu("Force Enable Protection")]
    private void DebugForceProtection()
    {
        if (IsOwner)
        {
            EnableProtection();
            Debug.Log("[TransitionProtection] DEBUG: Force enabled protection");
        }
    }
    
    [ContextMenu("Force Disable Protection")]
    private void DebugForceDisable()
    {
        if (IsOwner)
        {
            DisableProtection();
            Debug.Log("[TransitionProtection] DEBUG: Force disabled protection");
        }
    }
}