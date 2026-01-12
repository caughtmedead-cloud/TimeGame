using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;

/// <summary>
/// CLIENT-SIDE ONLY: Handles timeline transition feedback and debug information.
/// 
/// IMPORTANT: This script does NOT move GameObjects between scenes!
/// GameObject movement is handled SERVER-SIDE by TimelineSceneSetup.
/// 
/// This script only provides:
/// - Visual/audio feedback when timeline changes
/// - Debug logging about current scene/physics
/// - UI updates (if needed in future)
/// 
/// The server automatically moves the player GameObject between scenes,
/// and FishNet replicates this to all clients. This script just reacts to
/// the timeline change event for client-side feedback.
/// 
/// ATTACH TO: Player prefab (same GameObject as TimelineManager)
/// </summary>
[RequireComponent(typeof(TimelineManager))]
public class PlayerPhysicsSceneHandler : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Auto-assigned if not set")]
    [SerializeField] private TimelineManager timelineManager;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private void Awake()
    {
        // Auto-assign if not set
        if (timelineManager == null)
            timelineManager = GetComponent<TimelineManager>();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (showDebugLogs)
        {
            Debug.Log($"[PlayerPhysicsSceneHandler] OnStartClient - Player {Owner.ClientId} (IsOwner: {IsOwner})");
        }
        
        // Subscribe to timeline changes for feedback only
        if (IsOwner && timelineManager != null)
        {
            timelineManager.OnTimelineTransition += OnTimelineTransition;
            
            if (showDebugLogs)
            {
                Scene currentScene = gameObject.scene;
                Debug.Log($"[PlayerPhysicsSceneHandler] [Client] Player {Owner.ClientId} spawned in scene: {currentScene.name}");
                LogPhysicsSceneInfo();
            }
        }
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unsubscribe from events
        if (timelineManager != null)
        {
            timelineManager.OnTimelineTransition -= OnTimelineTransition;
        }
    }
    
    /// <summary>
    /// Client-side callback when timeline changes.
    /// Used for UI feedback and local effects only.
    /// The actual GameObject movement is handled by the server via TimelineSceneSetup.
    /// </summary>
    private void OnTimelineTransition(TimelineManager.TimelineState newTimeline)
    {
        if (!IsOwner) return;
        
        if (showDebugLogs)
        {
            Debug.Log($"[PlayerPhysicsSceneHandler] [Client] Timeline transition to {newTimeline}");
            
            // Wait a frame for scene change to propagate, then log new scene info
            StartCoroutine(LogSceneInfoNextFrame(newTimeline));
        }
        
        // Add client-side effects here:
        // - Play transition sound
        // - Camera shake
        // - Visual effects (screen distortion, particles, etc.)
        // - UI updates
        
        PlayTimelineTransitionEffects(newTimeline);
    }
    
    /// <summary>
    /// Waits a frame, then logs scene information to verify the transition.
    /// </summary>
    private System.Collections.IEnumerator LogSceneInfoNextFrame(TimelineManager.TimelineState expectedTimeline)
    {
        yield return null; // Wait one frame
        
        Scene currentScene = gameObject.scene;
        Debug.Log($"[PlayerPhysicsSceneHandler] [Client] After transition - Player in scene: {currentScene.name}");
        LogPhysicsSceneInfo();
    }
    
    /// <summary>
    /// Logs detailed physics scene information for debugging.
    /// </summary>
    private void LogPhysicsSceneInfo()
    {
        Scene currentScene = gameObject.scene;
        PhysicsScene physicsScene = currentScene.GetPhysicsScene();
        
        Debug.Log($"[PlayerPhysicsSceneHandler] === PHYSICS SCENE DEBUG ===");
        Debug.Log($"  Current Scene: {currentScene.name} (Handle: {currentScene.handle})");
        Debug.Log($"  Physics Scene Valid: {physicsScene.IsValid()}");
        Debug.Log($"  Physics Scene Hash: {physicsScene.GetHashCode()}");
        Debug.Log($"  Default Physics Hash: {Physics.defaultPhysicsScene.GetHashCode()}");
        Debug.Log($"  Is Default Physics: {physicsScene.GetHashCode() == Physics.defaultPhysicsScene.GetHashCode()}");
        Debug.Log($"  Current Timeline: {timelineManager.CurrentTimeline}");
        Debug.Log($"==========================================");
    }
    
    /// <summary>
    /// Plays client-side effects when timeline transitions.
    /// Add your visual/audio feedback here.
    /// </summary>
    private void PlayTimelineTransitionEffects(TimelineManager.TimelineState newTimeline)
    {
        // Example: Play different sounds based on timeline
        switch (newTimeline)
        {
            case TimelineManager.TimelineState.Past:
                // Play "warping backwards" sound
                // Show sepia/desaturated visual effect
                Debug.Log("[PlayerPhysicsSceneHandler] [Client] Playing PAST transition effects");
                break;
                
            case TimelineManager.TimelineState.Present:
                // Play "stabilizing" sound
                // Clear visual effects
                Debug.Log("[PlayerPhysicsSceneHandler] [Client] Playing PRESENT transition effects");
                break;
                
            case TimelineManager.TimelineState.Future:
                // Play "warping forwards" sound
                // Show futuristic/glitchy visual effect
                Debug.Log("[PlayerPhysicsSceneHandler] [Client] Playing FUTURE transition effects");
                break;
        }
    }
    
    /// <summary>
    /// Public method to manually check current physics scene (for debugging via console).
    /// Can be called from Unity's console or other scripts.
    /// </summary>
    [ContextMenu("Debug Current Physics Scene")]
    public void DebugCurrentPhysicsScene()
    {
        LogPhysicsSceneInfo();
    }
}