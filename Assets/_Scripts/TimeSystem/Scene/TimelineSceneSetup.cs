using System.Collections;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;  // ← Keep this for Scene, LoadSceneMode, etc.
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;  // ← Alias ONLY SceneManager

/// <summary>
/// INSTANCED DUNGEON PATTERN FOR TIMELINE SCENES (CORRECTED FOR FISHNET V4)
/// 
/// FishNet V4 LIMITATION: LoadGlobalScenes does NOT support scene stacking!
/// SOLUTION: Use Unity's SceneManager.LoadSceneAsync() to load scenes on server,
/// then use LoadConnectionScenes() to give specific clients access to those scenes.
/// </summary>
public class TimelineSceneSetup : NetworkBehaviour
{
    [Header("Timeline Scene Names")]
    [SerializeField] private string pastSceneName = "Timeline_Past";
    [SerializeField] private string presentSceneName = "Timeline_Present";
    [SerializeField] private string futureSceneName = "Timeline_Future";
    
    [Header("Scene Settings")]
    [SerializeField] private LocalPhysicsMode physicsMode = LocalPhysicsMode.Physics3D;
    
    private Scene _pastScene;
    private Scene _presentScene;
    private Scene _futureScene;
    
    private bool _serverScenesReady = false;
    
    public bool AreScenesReady() => _serverScenesReady;
    
    public Scene GetSceneForTimeline(TimelineManager.TimelineState timeline)
    {
        if (!_serverScenesReady)
        {
            Debug.LogWarning("[TimelineSceneSetup] Scenes not ready yet!");
            return default;
        }
        
        return timeline switch
        {
            TimelineManager.TimelineState.Past => _pastScene,
            TimelineManager.TimelineState.Present => _presentScene,
            TimelineManager.TimelineState.Future => _futureScene,
            _ => _presentScene
        };
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        Debug.Log("[TimelineSceneSetup] Server starting - beginning timeline scene load sequence");
        StartCoroutine(LoadTimelineScenesOnServer());
    }
    
    /// <summary>
    /// Loads all three timeline scenes on the SERVER using Unity's SceneManager.
    /// 
    /// CRITICAL FIX: We use Unity's LoadSceneAsync with LoadSceneMode.Additive
    /// because FishNet's LoadGlobalScenes does NOT support scene stacking.
    /// 
    /// After loading with Unity, we can then use FishNet's LoadConnectionScenes
    /// to give specific clients access to these already-loaded scenes.
    /// </summary>
    private IEnumerator LoadTimelineScenesOnServer()
    {
        Debug.Log("[TimelineSceneSetup] Loading timeline scenes on server with Unity SceneManager...");
        
        // ==================================================================================
        // Load Past timeline using UNITY's SceneManager (not FishNet)
        // ==================================================================================
        Debug.Log($"[TimelineSceneSetup] Loading '{pastSceneName}'...");
        AsyncOperation pastOp = UnitySceneManager.LoadSceneAsync(pastSceneName, LoadSceneMode.Additive);
        yield return pastOp;
        
        _pastScene = UnitySceneManager.GetSceneByName(pastSceneName);
        if (_pastScene.IsValid())
        {
            Debug.Log($"[TimelineSceneSetup] ✅ '{pastSceneName}' loaded (Handle: {_pastScene.handle})");
            
            // Set physics mode if needed (Unity 2020.2+)
            #if UNITY_2020_2_OR_NEWER
            if (physicsMode == LocalPhysicsMode.Physics3D)
            {
                _pastScene.GetPhysicsScene(); // Creates local physics scene
            }
            else if (physicsMode == LocalPhysicsMode.Physics2D)
            {
                _pastScene.GetPhysicsScene2D(); // Creates local 2D physics scene
            }
            #endif
        }
        else
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Failed to load '{pastSceneName}'!");
            yield break;
        }
        
        // ==================================================================================
        // Load Present timeline
        // ==================================================================================
        Debug.Log($"[TimelineSceneSetup] Loading '{presentSceneName}'...");
        AsyncOperation presentOp = UnitySceneManager.LoadSceneAsync(presentSceneName, LoadSceneMode.Additive);
        yield return presentOp;
        
        _presentScene = UnitySceneManager.GetSceneByName(presentSceneName);
        if (_presentScene.IsValid())
        {
            Debug.Log($"[TimelineSceneSetup] ✅ '{presentSceneName}' loaded (Handle: {_presentScene.handle})");
            
            #if UNITY_2020_2_OR_NEWER
            if (physicsMode == LocalPhysicsMode.Physics3D)
            {
                _presentScene.GetPhysicsScene();
            }
            else if (physicsMode == LocalPhysicsMode.Physics2D)
            {
                _presentScene.GetPhysicsScene2D();
            }
            #endif
        }
        else
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Failed to load '{presentSceneName}'!");
            yield break;
        }
        
        // ==================================================================================
        // Load Future timeline
        // ==================================================================================
        Debug.Log($"[TimelineSceneSetup] Loading '{futureSceneName}'...");
        AsyncOperation futureOp = UnitySceneManager.LoadSceneAsync(futureSceneName, LoadSceneMode.Additive);
        yield return futureOp;
        
        _futureScene = UnitySceneManager.GetSceneByName(futureSceneName);
        if (_futureScene.IsValid())
        {
            Debug.Log($"[TimelineSceneSetup] ✅ '{futureSceneName}' loaded (Handle: {_futureScene.handle})");
            
            #if UNITY_2020_2_OR_NEWER
            if (physicsMode == LocalPhysicsMode.Physics3D)
            {
                _futureScene.GetPhysicsScene();
            }
            else if (physicsMode == LocalPhysicsMode.Physics2D)
            {
                _futureScene.GetPhysicsScene2D();
            }
            #endif
        }
        else
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Failed to load '{futureSceneName}'!");
            yield break;
        }
        
        // Mark as ready
        _serverScenesReady = true;
        
        Debug.Log("[TimelineSceneSetup] ✅ All timeline scenes loaded on server!");
        Debug.Log($"  - Past: {pastSceneName} (Handle: {_pastScene.handle})");
        Debug.Log($"  - Present: {presentSceneName} (Handle: {_presentScene.handle})");
        Debug.Log($"  - Future: {futureSceneName} (Handle: {_futureScene.handle})");
    }
    
    /// <summary>
    /// Loads a specific timeline scene for a specific connection.
    /// Now that scenes are loaded on the server via Unity, we use FishNet's
    /// LoadConnectionScenes to give the client access to the already-loaded scene.
    /// </summary>
    [Server]
    public void LoadTimelineForConnection(NetworkConnection conn, TimelineManager.TimelineState timeline)
    {
        if (!_serverScenesReady)
        {
            Debug.LogError("[TimelineSceneSetup] Cannot load timeline for connection - scenes not ready!");
            return;
        }
        
        Scene targetScene = GetSceneForTimeline(timeline);
        
        if (!targetScene.IsValid())
        {
            Debug.LogError($"[TimelineSceneSetup] Invalid scene for timeline {timeline}");
            return;
        }
        
        Debug.Log($"[TimelineSceneSetup] Loading {timeline} timeline for client {conn.ClientId}...");
        
        // Create scene load data from the existing scene reference
        SceneLoadData sld = new SceneLoadData(targetScene);
        sld.Options.AllowStacking = true; // ALLOWED for connection scenes!
        sld.Options.LocalPhysics = physicsMode;
        
        // Load the scene for this connection
        // This adds the connection as an observer to the scene
        base.SceneManager.LoadConnectionScenes(new NetworkConnection[] { conn }, sld);
        
        Debug.Log($"[TimelineSceneSetup] ✅ {timeline} timeline loading for client {conn.ClientId}");
    }
    
    [Server]
    public void UnloadTimelineForConnection(NetworkConnection conn, TimelineManager.TimelineState timeline)
    {
        if (!_serverScenesReady)
        {
            Debug.LogError("[TimelineSceneSetup] Cannot unload timeline for connection - scenes not ready!");
            return;
        }
        
        Scene targetScene = GetSceneForTimeline(timeline);
        
        if (!targetScene.IsValid())
        {
            Debug.LogError($"[TimelineSceneSetup] Invalid scene for timeline {timeline}");
            return;
        }
        
        Debug.Log($"[TimelineSceneSetup] Unloading {timeline} timeline for client {conn.ClientId}...");
        
        SceneUnloadData sud = new SceneUnloadData(targetScene);
        base.SceneManager.UnloadConnectionScenes(new NetworkConnection[] { conn }, sud);
        
        Debug.Log($"[TimelineSceneSetup] ✅ {timeline} timeline unloading for client {conn.ClientId}");
    }
    
    [Server]
    public void TransitionPlayerTimeline(NetworkConnection conn, 
        TimelineManager.TimelineState oldTimeline, 
        TimelineManager.TimelineState newTimeline)
    {
        Debug.Log($"[TimelineSceneSetup] Transitioning client {conn.ClientId}: {oldTimeline} → {newTimeline}");
        
        // Find the player's NetworkObject
        NetworkObject playerNob = null;
        foreach (NetworkObject nob in conn.Objects)
        {
            // Find player by checking for TimelineManager component
            if (nob.GetComponent<TimelineManager>() != null)
            {
                playerNob = nob;
                break;
            }
        }
        
        if (playerNob == null)
        {
            Debug.LogError($"[TimelineSceneSetup] Cannot find player NetworkObject for client {conn.ClientId}!");
            return;
        }
        
        // Get the target scene
        Scene newScene = GetSceneForTimeline(newTimeline);
        
        if (!newScene.IsValid())
        {
            Debug.LogError($"[TimelineSceneSetup] Invalid scene for timeline {newTimeline}");
            return;
        }
        
        // =====================================================================
        // NEW: Load the new scene WITH the player moved to it
        // =====================================================================
        
        SceneLoadData sld = new SceneLoadData(newScene);
        sld.Options.AllowStacking = true;
        sld.Options.LocalPhysics = physicsMode;
        
        // CRITICAL: Add player to MovedNetworkObjects array
        // This tells FishNet to move the player to the new scene during load
        sld.MovedNetworkObjects = new NetworkObject[] { playerNob };
        
        Debug.Log($"[TimelineSceneSetup] Moving player {conn.ClientId} to {newTimeline} scene...");
        
        // Load new scene for connection (with player moving to it)
        base.SceneManager.LoadConnectionScenes(new NetworkConnection[] { conn }, sld);
        
        // =====================================================================
        // Unload the old scene AFTER the new one is loaded
        // =====================================================================
        
        Scene oldScene = GetSceneForTimeline(oldTimeline);
        if (oldScene.IsValid())
        {
            SceneUnloadData sud = new SceneUnloadData(oldScene);
            base.SceneManager.UnloadConnectionScenes(new NetworkConnection[] { conn }, sud);
            Debug.Log($"[TimelineSceneSetup] Unloaded old {oldTimeline} scene for client {conn.ClientId}");
        }
        
        Debug.Log($"[TimelineSceneSetup] ✅ Player {conn.ClientId} transitioned to {newTimeline}");
    }
}
