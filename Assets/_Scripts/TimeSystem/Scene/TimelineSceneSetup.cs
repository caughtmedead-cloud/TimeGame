using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Scened;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// SERVER-ONLY: Manages timeline scene loading and player GameObject placement.
/// 
/// CRITICAL RESPONSIBILITIES:
/// 1. Load all 3 timeline scenes on server with LocalPhysics
/// 2. Load timeline scenes for specific player connections
/// 3. Move player GameObjects between timeline scenes (SERVER-SIDE!)
/// 4. Unload timeline scenes from player connections
/// 
/// WHY GAMEOBJECT MOVEMENT MUST BE SERVER-SIDE:
/// - FishNet's networking requires server authority over GameObject scene placement
/// - Clients cannot move NetworkObjects between scenes - only the server can
/// - Moving on server automatically replicates to all clients via FishNet
/// 
/// ATTACH TO: NetworkObject in Bootstrap scene (server-only)
/// </summary>
public class TimelineSceneSetup : NetworkBehaviour
{
    [Header("Timeline Scene Names")]
    [SerializeField] private string pastSceneName = "Timeline_Past";
    [SerializeField] private string presentSceneName = "Timeline_Present";
    [SerializeField] private string futureSceneName = "Timeline_Future";
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Scene references (server-only)
    private Scene pastScene;
    private Scene presentScene;
    private Scene futureScene;
    
    private bool timelinesLoaded = false;
    
    // Track pending scene loads during initialization
    private Dictionary<string, Scene> pendingSceneLoads = new Dictionary<string, Scene>();
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        Debug.Log("[TimelineSceneSetup] Starting timeline initialization...");
        
        // Subscribe to scene loading events
        base.SceneManager.OnLoadEnd += SceneManager_OnLoadEnd;
        
        // Start loading timelines
        StartCoroutine(LoadAllTimelines());
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        
        if (base.SceneManager != null)
        {
            base.SceneManager.OnLoadEnd -= SceneManager_OnLoadEnd;
        }
    }
    
    /// <summary>
    /// Loads all timeline scenes on the server with LocalPhysics isolation.
    /// </summary>
    private IEnumerator LoadAllTimelines()
    {
        Debug.Log("[TimelineSceneSetup] Loading timeline scenes...");
        
        // Load all three timelines with LocalPhysics
        yield return StartCoroutine(LoadTimelineScene(pastSceneName));
        yield return StartCoroutine(LoadTimelineScene(presentSceneName));
        yield return StartCoroutine(LoadTimelineScene(futureSceneName));
        
        // CRITICAL: Store scene references BEFORE calling VerifyPhysicsIsolation!
        pastScene = pendingSceneLoads[pastSceneName];
        presentScene = pendingSceneLoads[presentSceneName];
        futureScene = pendingSceneLoads[futureSceneName];
        
        timelinesLoaded = true;
        
        Debug.Log("[TimelineSceneSetup] ✅ All timelines loaded!");
        
        // NOW verify physics isolation (scenes are assigned)
        VerifyPhysicsIsolation();
    }
    
    /// <summary>
    /// Loads a single timeline scene globally on the server.
    /// </summary>
    private IEnumerator LoadTimelineScene(string sceneName)
    {
        Debug.Log($"[TimelineSceneSetup] Loading '{sceneName}' with LocalPhysics...");
        
        // Create scene load data
        SceneLoadData sld = new SceneLoadData(sceneName);
        sld.Options.LocalPhysics = LocalPhysicsMode.Physics3D;
        sld.Options.AllowStacking = true;
        
        // Load on server without connection parameter = server-only scene
        // This allows stacking and creates LocalPhysics scene
        base.SceneManager.LoadConnectionScenes(sld);
        
        // Wait for scene to load
        float timeout = 10f;
        float elapsed = 0f;
        
        while (!pendingSceneLoads.ContainsKey(sceneName) && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
        
        if (!pendingSceneLoads.ContainsKey(sceneName))
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Timeout loading '{sceneName}'!");
            yield break;
        }
        
        Scene loadedScene = pendingSceneLoads[sceneName];
        
        // Verify local physics scene was created
        PhysicsScene physicsScene = loadedScene.GetPhysicsScene();
        bool isDefault = physicsScene.GetHashCode() == Physics.defaultPhysicsScene.GetHashCode();
        
        if (isDefault)
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ '{sceneName}' is using DEFAULT physics scene!");
        }
        else
        {
            Debug.Log($"[TimelineSceneSetup] ✅ '{sceneName}' has LOCAL physics scene (hash: {physicsScene.GetHashCode()})");
        }
    }
    
    /// <summary>
    /// Called when a scene finishes loading. Used to track scene references.
    /// </summary>
    private void SceneManager_OnLoadEnd(SceneLoadEndEventArgs args)
    {
        foreach (Scene scene in args.LoadedScenes)
        {
            if (scene.name == pastSceneName || scene.name == presentSceneName || scene.name == futureSceneName)
            {
                pendingSceneLoads[scene.name] = scene;
                Debug.Log($"[TimelineSceneSetup] Scene '{scene.name}' loaded (handle: {scene.handle})");
            }
        }
    }
    
    private void VerifyPhysicsIsolation()
    {
        Debug.Log("[TimelineSceneSetup] === PHYSICS ISOLATION VERIFICATION ===");
        
        // Check if scenes are valid before trying to get their physics scenes
        if (!pastScene.IsValid())
        {
            Debug.LogError("[TimelineSceneSetup] ❌ Past scene is invalid!");
            return;
        }
        if (!presentScene.IsValid())
        {
            Debug.LogError("[TimelineSceneSetup] ❌ Present scene is invalid!");
            return;
        }
        if (!futureScene.IsValid())
        {
            Debug.LogError("[TimelineSceneSetup] ❌ Future scene is invalid!");
            return;
        }
        
        var pastPhysics = pastScene.GetPhysicsScene();
        var presentPhysics = presentScene.GetPhysicsScene();
        var futurePhysics = futureScene.GetPhysicsScene();
        var defaultPhysics = Physics.defaultPhysicsScene;
        
        int pastHash = pastPhysics.GetHashCode();
        int presentHash = presentPhysics.GetHashCode();
        int futureHash = futurePhysics.GetHashCode();
        int defaultHash = defaultPhysics.GetHashCode();
        
        Debug.Log($"  Past physics hash: {pastHash}");
        Debug.Log($"  Present physics hash: {presentHash}");
        Debug.Log($"  Future physics hash: {futureHash}");
        Debug.Log($"  Default physics hash: {defaultHash}");
        
        bool allDifferent = (pastHash != presentHash) && (pastHash != futureHash) && (presentHash != futureHash);
        bool noneDefault = (pastHash != defaultHash) && (presentHash != defaultHash) && (futureHash != defaultHash);
        
        if (allDifferent && noneDefault)
        {
            Debug.Log("[TimelineSceneSetup] ✅ PHYSICS ISOLATION WORKING!");
        }
        else
        {
            Debug.LogError("[TimelineSceneSetup] ❌ PHYSICS ISOLATION BROKEN!");
            if (!allDifferent) Debug.LogError("  Timeline scenes are sharing physics scenes!");
            if (!noneDefault) Debug.LogError("  One or more timelines using default physics scene!");
        }
    }
    
    /// <summary>
    /// SERVER-ONLY: Loads a specific timeline for a connection.
    /// This adds the connection to an existing timeline scene.
    /// </summary>
    public void LoadTimelineForConnection(NetworkConnection conn, TimelineManager.TimelineState timeline)
    {
        if (!timelinesLoaded)
        {
            Debug.LogError($"[TimelineSceneSetup] Cannot load timeline - timelines not loaded yet!");
            return;
        }
        
        Scene targetScene = GetTimelineScene(timeline);
        
        if (!targetScene.IsValid())
        {
            Debug.LogError($"[TimelineSceneSetup] Invalid scene for timeline {timeline}!");
            return;
        }
        
        Debug.Log($"[TimelineSceneSetup] Loading {timeline} timeline for client {conn.ClientId}...");
        
        string sceneName = timeline switch
        {
            TimelineManager.TimelineState.Past => pastSceneName,
            TimelineManager.TimelineState.Present => presentSceneName,
            TimelineManager.TimelineState.Future => futureSceneName,
            _ => ""
        };
        
        // Create scene load data to load client into existing scene
        SceneLoadData sld = new SceneLoadData(sceneName);
        sld.Options.LocalPhysics = LocalPhysicsMode.Physics3D;
        sld.Options.AllowStacking = true;
        
        // Use scene handle to load into EXISTING stacked scene
        sld.SceneLookupDatas = new SceneLookupData[] { new SceneLookupData(targetScene) };
        
        base.SceneManager.LoadConnectionScenes(conn, sld);
        
        Debug.Log($"[TimelineSceneSetup] ✅ {timeline} timeline loading for client {conn.ClientId}");
    }
    
    /// <summary>
    /// SERVER-ONLY: Unloads a timeline from a specific connection (client-side only).
    /// The scene remains loaded on the server for other players.
    /// </summary>
    private void UnloadTimelineForConnection(NetworkConnection conn, TimelineManager.TimelineState timeline)
    {
        Scene targetScene = GetTimelineScene(timeline);
        
        if (!targetScene.IsValid())
        {
            Debug.LogWarning($"[TimelineSceneSetup] Cannot unload {timeline} - invalid scene!");
            return;
        }
        
        string sceneName = timeline switch
        {
            TimelineManager.TimelineState.Past => pastSceneName,
            TimelineManager.TimelineState.Present => presentSceneName,
            TimelineManager.TimelineState.Future => futureSceneName,
            _ => ""
        };
        
        Debug.Log($"[TimelineSceneSetup] Unloading {timeline} timeline from client {conn.ClientId}...");
        
        // Create scene unload data
        SceneUnloadData sud = new SceneUnloadData(sceneName);
        
        // Use scene handle to unload the correct stacked instance
        sud.SceneLookupDatas = new SceneLookupData[] { new SceneLookupData(targetScene) };
        
        // Unload for this specific connection only
        // The scene stays loaded on the server for other players
        base.SceneManager.UnloadConnectionScenes(conn, sud);
        
        Debug.Log($"[TimelineSceneSetup] ✅ {timeline} timeline unloading from client {conn.ClientId}");
    }
    
    /// <summary>
    /// SERVER-ONLY: Transitions a player from one timeline to another.
    /// 
    /// CRITICAL: This moves the player GameObject between scenes ON THE SERVER.
    /// Clients cannot move GameObjects - only the server can.
    /// 
    /// PROCESS:
    /// 1. Find the player's NetworkObject
    /// 2. Move player GameObject to new timeline scene (SERVER-SIDE!)
    /// 3. Unload old timeline from client's view
    /// 4. Load new timeline for client's view
    /// </summary>
    public void TransitionPlayerTimeline(NetworkConnection conn, 
        TimelineManager.TimelineState oldTimeline, 
        TimelineManager.TimelineState newTimeline)
    {
        if (!timelinesLoaded)
        {
            Debug.LogError($"[TimelineSceneSetup] Cannot transition - timelines not loaded!");
            return;
        }

        Debug.Log($"[TimelineSceneSetup] Transitioning client {conn.ClientId} from {oldTimeline} to {newTimeline}");
        
        // STEP 1: Find the player's NetworkObject for this connection
        NetworkObject playerObject = FindPlayerForConnection(conn);
        
        if (playerObject == null)
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Cannot find player NetworkObject for client {conn.ClientId}!");
            return;
        }
        
        // STEP 2: Move player GameObject to new timeline scene (SERVER-SIDE!)
        Scene newScene = GetTimelineScene(newTimeline);
        
        if (!newScene.IsValid())
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Invalid scene for timeline {newTimeline}!");
            return;
        }
        
        // Move the player GameObject to the new timeline scene
        // This is CRITICAL for physics isolation - player will now collide with objects in new timeline only
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerObject.gameObject, newScene);
        
        Debug.Log($"[TimelineSceneSetup] ✅ Moved player GameObject to {newTimeline} scene on server");
        
        // Verify the move worked
        PhysicsScene playerPhysicsScene = playerObject.gameObject.scene.GetPhysicsScene();
        PhysicsScene targetPhysicsScene = newScene.GetPhysicsScene();
        
        if (playerPhysicsScene.GetHashCode() != targetPhysicsScene.GetHashCode())
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Player physics scene mismatch!");
            Debug.LogError($"  Player hash: {playerPhysicsScene.GetHashCode()}");
            Debug.LogError($"  Target hash: {targetPhysicsScene.GetHashCode()}");
        }
        else
        {
            Debug.Log($"[TimelineSceneSetup] ✅ Player physics scene verified (hash: {playerPhysicsScene.GetHashCode()})");
        }
        
        // STEP 3: Unload old timeline from this client's view
        // This removes visual clutter of objects from the old timeline
        UnloadTimelineForConnection(conn, oldTimeline);
        
        // STEP 4: Load new timeline for this client's view
        LoadTimelineForConnection(conn, newTimeline);
        
        Debug.Log($"[TimelineSceneSetup] ✅ Transition complete for client {conn.ClientId}");
    }
    
    /// <summary>
    /// SERVER-ONLY: Finds the player NetworkObject for a specific connection.
    /// Returns null if player not found.
    /// </summary>
    private NetworkObject FindPlayerForConnection(NetworkConnection conn)
    {
        // Iterate through all spawned NetworkObjects to find this player
        foreach (NetworkObject nob in base.ServerManager.Objects.Spawned.Values)
        {
            // Check if this NetworkObject belongs to this connection and is a player
            if (nob.Owner == conn)
            {
                // Verify it has player components (TimelineManager is a good indicator)
                if (nob.GetComponent<TimelineManager>() != null)
                {
                    Debug.Log($"[TimelineSceneSetup] Found player NetworkObject for client {conn.ClientId}");
                    return nob;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets the scene for a specific timeline.
    /// </summary>
    public Scene GetTimelineScene(TimelineManager.TimelineState timeline)
    {
        return timeline switch
        {
            TimelineManager.TimelineState.Past => pastScene,
            TimelineManager.TimelineState.Present => presentScene,
            TimelineManager.TimelineState.Future => futureScene,
            _ => default
        };
    }
    
    /// <summary>
    /// Alternative name for GetTimelineScene (for compatibility).
    /// </summary>
    public Scene GetSceneForTimeline(TimelineManager.TimelineState timeline)
    {
        return GetTimelineScene(timeline);
    }
    
    /// <summary>
    /// Checks if all timelines are loaded.
    /// </summary>
    public bool AreTimelinesLoaded() => timelinesLoaded;
}