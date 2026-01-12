using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;

/// <summary>
/// CLIENT-SIDE: Handles timeline transition feedback and visual effects.
/// 
/// IMPORTANT: This script provides client-side feedback only!
/// - Server moves GameObject between scenes (authoritative)
/// - FishNet replicates scene changes to clients
/// - This script reacts with visual/audio feedback
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
    [SerializeField] private bool showDebugLogs = false;
    
    private void Awake()
    {
        if (timelineManager == null)
            timelineManager = GetComponent<TimelineManager>();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (IsOwner && timelineManager != null)
        {
            timelineManager.OnTimelineTransition += OnTimelineTransition;
            
            if (showDebugLogs)
            {
                Scene currentScene = gameObject.scene;
                Debug.Log($"[PlayerPhysicsSceneHandler] [Client {Owner.ClientId}] Spawned in scene: {currentScene.name}");
            }
        }
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        if (timelineManager != null)
        {
            timelineManager.OnTimelineTransition -= OnTimelineTransition;
        }
    }
    
    /// <summary>
    /// Client-side timeline transition callback.
    /// Add visual/audio feedback here.
    /// </summary>
    private void OnTimelineTransition(TimelineManager.TimelineState newTimeline)
    {
        if (!IsOwner)
            return;
        
        if (showDebugLogs)
        {
            Debug.Log($"[PlayerPhysicsSceneHandler] [Client {Owner.ClientId}] Timeline → {newTimeline}");
            StartCoroutine(LogSceneInfoNextFrame(newTimeline));
        }
        
        PlayTimelineTransitionEffects(newTimeline);
    }
    
    private System.Collections.IEnumerator LogSceneInfoNextFrame(TimelineManager.TimelineState expectedTimeline)
    {
        yield return null;
        
        if (showDebugLogs)
        {
            Scene currentScene = gameObject.scene;
            Debug.Log($"[PlayerPhysicsSceneHandler] [Client {Owner.ClientId}] Now in scene: {currentScene.name}");
        }
    }
    
    /// <summary>
    /// Play client-side transition effects (sounds, visuals, camera shake, etc.)
    /// </summary>
    private void PlayTimelineTransitionEffects(TimelineManager.TimelineState newTimeline)
    {
        switch (newTimeline)
        {
            case TimelineManager.TimelineState.Past:
                if (showDebugLogs)
                    Debug.Log("[PlayerPhysicsSceneHandler] Playing PAST transition effects");
                break;
                
            case TimelineManager.TimelineState.Present:
                if (showDebugLogs)
                    Debug.Log("[PlayerPhysicsSceneHandler] Playing PRESENT transition effects");
                break;
                
            case TimelineManager.TimelineState.Future:
                if (showDebugLogs)
                    Debug.Log("[PlayerPhysicsSceneHandler] Playing FUTURE transition effects");
                break;
        }
    }
}
