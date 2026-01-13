
/// <summary>
/// Handles moving the player's physics components to the correct local physics scene
/// when transitioning between timelines.
/// 
/// CRITICAL FOR TIMELINE PHYSICS ISOLATION:
/// CharacterController interacts with physics in whatever scene it's currently assigned to.
/// By default, it uses the DEFAULT physics scene, which means it collides with objects
/// from ALL timelines.
/// 
/// This script ensures the player's CharacterController is moved to the LOCAL physics scene
/// of their current timeline, isolating physics interactions properly.
/// 
/// HOW IT WORKS:
/// 1. Subscribe to TimelineManager.OnTimelineTransition event
/// 2. When timeline changes, move player GameObject to new timeline scene
/// 3. Unity automatically updates CharacterController to use that scene's local physics
/// 4. Player now only collides with objects in their current timeline
/// 
/// ATTACH TO: Player prefab (same GameObject as TimelineManager)
/// </summary>

using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
using FishNet.Managing.Scened;
using FishNet.Managing.Timing;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

[RequireComponent(typeof(TimelineManager))]
[RequireComponent(typeof(CharacterController))]
public class PlayerPhysicsSceneHandler : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Auto-assigned if not set")]
    [SerializeField] private TimelineManager timelineManager;
    [Tooltip("Auto-assigned if not set")]
    [SerializeField] private CharacterController characterController;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private TimelineSceneSetup _timelineSceneSetup;
    
    private void Awake()
    {
        // Auto-assign if not set
        if (timelineManager == null)
            timelineManager = GetComponent<TimelineManager>();
        
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Only the owner needs to handle physics scene transitions
        if (!IsOwner) return;
        
        // Find TimelineSceneSetup
        _timelineSceneSetup = FindFirstObjectByType<TimelineSceneSetup>();
        if (_timelineSceneSetup == null)
        {
            Debug.LogError("[PlayerPhysicsSceneHandler] ❌ TimelineSceneSetup not found!");
            return;
        }
        
        // Subscribe to timeline transitions
        timelineManager.OnTimelineTransition += OnTimelineTransition;
        
        // Move to current timeline's physics scene immediately
        MoveToTimelinePhysicsScene(timelineManager.CurrentTimeline);
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        if (!IsOwner) return;
        
        // Unsubscribe from timeline transitions
        if (timelineManager != null)
        {
            timelineManager.OnTimelineTransition -= OnTimelineTransition;
        }
    }
    
    /// <summary>
    /// Called when the player's timeline changes.
    /// Moves the player GameObject to the new timeline scene's local physics.
    /// </summary>
    private void OnTimelineTransition(TimelineManager.TimelineState newTimeline)
    {
        MoveToTimelinePhysicsScene(newTimeline);
    }
    
    /// <summary>
    /// Moves the player GameObject to the specified timeline scene.
    /// This automatically updates the CharacterController to use that scene's local physics.
    /// </summary>
    private void MoveToTimelinePhysicsScene(TimelineManager.TimelineState timeline)
    {
        if (_timelineSceneSetup == null)
        {
            Debug.LogError("[PlayerPhysicsSceneHandler] ❌ TimelineSceneSetup not found!");
            return;
        }
        
        Scene targetScene = _timelineSceneSetup.GetSceneForTimeline(timeline);
        
        if (!targetScene.IsValid())
        {
            Debug.LogError($"[PlayerPhysicsSceneHandler] ❌ Invalid scene for timeline {timeline}");
            return;
        }
        
        // Check if we're already in the correct scene
        if (gameObject.scene == targetScene)
        {
            return;
        }
        
        // Get current physics scene info BEFORE moving (for debug)
        Scene currentScene = gameObject.scene;
        PhysicsScene currentPhysicsScene = currentScene.GetPhysicsScene();
        
        // Move GameObject to target timeline scene
        // This automatically updates CharacterController's physics scene!
        UnitySceneManager.MoveGameObjectToScene(gameObject, targetScene);
        
        // Get new physics scene info AFTER moving (for debug)
        PhysicsScene newPhysicsScene = targetScene.GetPhysicsScene();
        
        // Verify CharacterController is now using the correct physics scene
        VerifyPhysicsScene(targetScene);
    }
    
    /// <summary>
    /// Verifies that the player's CharacterController is using the correct physics scene.
    /// This is a debug check to ensure the move worked correctly.
    /// </summary>
    private void VerifyPhysicsScene(Scene expectedScene)
    {
        Scene actualScene = gameObject.scene;
        
        if (actualScene != expectedScene)
        {
            Debug.LogError($"[PlayerPhysicsSceneHandler] ❌ PHYSICS SCENE MISMATCH!");
            Debug.LogError($"  - Expected: {expectedScene.name}");
            Debug.LogError($"  - Actual: {actualScene.name}");
            return;
        }
        
        PhysicsScene physicsScene = actualScene.GetPhysicsScene();
        
        if (!physicsScene.IsValid())
        {
            Debug.LogWarning($"[PlayerPhysicsSceneHandler] ⚠️ Physics scene not valid for '{actualScene.name}'");
            Debug.LogWarning("  - Make sure timeline scenes are loaded with LocalPhysicsMode.Physics3D");
            return;
        }
        
        if (showDebugLogs)
        {
        }
    }
    
    /// <summary>
    /// Public method to manually verify current physics scene (for debugging).
    /// </summary>
    public void DebugCurrentPhysicsScene()
    {
        Scene currentScene = gameObject.scene;
        PhysicsScene physicsScene = currentScene.GetPhysicsScene();
        
        Debug.Log($"[PlayerPhysicsSceneHandler] DEBUG INFO:");
        Debug.Log($"  - Current Scene: {currentScene.name} (Handle: {currentScene.handle})");
        Debug.Log($"  - Physics Scene Valid: {physicsScene.IsValid()}");
        Debug.Log($"  - Current Timeline: {timelineManager.CurrentTimeline}");
    }
}