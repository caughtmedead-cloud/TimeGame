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
/// ARCHITECTURE:
/// - Server loads 3 shared timeline scenes with LocalPhysics isolation
/// - ALL clients load ALL 3 timeline scenes (same instances as server)
/// - Player GameObjects move between scenes based on timeline state (server-side)
/// - FishNet Scene Condition handles visibility automatically
/// 
/// PHYSICS ARCHITECTURE:
/// - Each timeline has isolated LocalPhysics (Physics3D)
/// - Players only collide with objects in their current timeline scene
/// - PhysicsSceneSync handles manual simulation for each scene
/// 
/// VISIBILITY ARCHITECTURE:
/// - Clients load all 3 scenes but FishNet Scene Condition controls what they see
/// - Players only see NetworkObjects in scenes where their player GameObject is located
/// - Moving player GameObject between scenes automatically updates visibility
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
    
    private Dictionary<TimelineManager.TimelineState, Scene> _timelineScenes = new Dictionary<TimelineManager.TimelineState, Scene>();
    
    private HashSet<NetworkConnection> _initializedConnections = new HashSet<NetworkConnection>();
    
    private bool timelinesLoaded = false;
    private bool initializationStarted = false;
    
    #region Lifecycle
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        if (initializationStarted)
        {
            if (showDebugLogs)
                Debug.LogWarning("[TimelineSceneSetup] Already initializing - skipping duplicate OnStartServer");
            return;
        }
        
        initializationStarted = true;
        
        if (showDebugLogs)
            Debug.Log("[TimelineSceneSetup] Starting timeline initialization...");
        
        base.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        
        StartCoroutine(LoadAllTimelines());
    }
    
    private void Start()
    {
        if (initializationStarted)
        {
            return;
        }
        
        // For scene objects, OnStartServer might not be called if server started before scene load
        // Check if server is already running and initialize if needed
        if (base.IsServerStarted && !timelinesLoaded)
        {
            initializationStarted = true;
            
            if (showDebugLogs)
                Debug.Log("[TimelineSceneSetup] Server already started - initializing from Start()");

            base.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
            StartCoroutine(LoadAllTimelines());
        }
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        
        if (base.ServerManager != null)
        {
            base.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
        }
        
        timelinesLoaded = false;
        initializationStarted = false;
        _initializedConnections.Clear();
        _timelineScenes.Clear();
    }
    
    #endregion
    
    #region Connection Events
    
    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started)
        {
            if (showDebugLogs)
                Debug.Log($"[TimelineSceneSetup] New connection {conn.ClientId} - queueing timeline load");
            
            StartCoroutine(LoadAllTimelinesForConnection(conn));
        }
        else if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
        {
            if (_initializedConnections.Contains(conn))
            {
                if (showDebugLogs)
                    Debug.Log($"[TimelineSceneSetup] Connection {conn.ClientId} disconnected - cleaning up");
                
                _initializedConnections.Remove(conn);
            }
        }
    }
    
    #endregion
    
    #region Server Timeline Loading
    
    /// <summary>
    /// SERVER-ONLY: Loads all 3 timeline scenes on the server with LocalPhysics isolation
    /// using Unity's native SceneManager (LocalPhysics works only when the server loads via Unity).
    /// </summary>
    private IEnumerator LoadAllTimelines()
    {
        if (showDebugLogs)
            Debug.Log("[TimelineSceneSetup] Loading server timeline scenes with LocalPhysics...");
        
        yield return StartCoroutine(LoadTimelineScene(TimelineManager.TimelineState.Past, pastSceneName));
        yield return StartCoroutine(LoadTimelineScene(TimelineManager.TimelineState.Present, presentSceneName));
        yield return StartCoroutine(LoadTimelineScene(TimelineManager.TimelineState.Future, futureSceneName));
        
        timelinesLoaded = true;
        
        if (showDebugLogs)
            Debug.Log("[TimelineSceneSetup] ✅ All server timelines loaded!");
        
        VerifyPhysicsIsolation();

        RegisterHostConnectionIfAvailable();
    }
    
    /// <summary>
    /// Loads a single timeline scene on the server via Unity API so LocalPhysics is honored.
    /// </summary>
    private IEnumerator LoadTimelineScene(TimelineManager.TimelineState timeline, string sceneName)
    {
        if (showDebugLogs)
            Debug.Log($"[TimelineSceneSetup] Loading '{sceneName}' with LocalPhysics...");
        
        var loadParams = new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics3D);
        AsyncOperation loadOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, loadParams);

        if (loadOp == null)
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Failed to start loading {sceneName}!");
            yield break;
        }

        while (!loadOp.isDone)
            yield return null;
        
        Scene loadedScene = default;
        float timeout = 5f;
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.name == sceneName && scene.IsValid())
                {
                    loadedScene = scene;
                    break;
                }
            }
            
            if (loadedScene.IsValid())
                break;
                
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        if (!loadedScene.IsValid())
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Failed to load {sceneName} after {timeout}s!");
            yield break;
        }
        
        _timelineScenes[timeline] = loadedScene;
        
        PhysicsScene physicsScene = loadedScene.GetPhysicsScene();
        if (physicsScene.IsValid())
        {
            int hash = physicsScene.GetHashCode();
            int defaultHash = Physics.defaultPhysicsScene.GetHashCode();
            
            if (hash == defaultHash)
            {
                Debug.LogError($"[TimelineSceneSetup] ❌ '{sceneName}' is using DEFAULT physics scene!");
            }
            else if (showDebugLogs)
            {
                Debug.Log($"[TimelineSceneSetup] ✅ '{sceneName}' has LOCAL physics scene (hash: {hash})");
            }
        }
        else
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ '{sceneName}' physics scene is INVALID!");
        }
    }
    
    #endregion
    
    #region Client Timeline Loading
    
    /// <summary>
    /// SERVER-ONLY: Loads ALL 3 timeline scenes for a specific client connection.
    /// 
    /// CRITICAL: Clients must have all 3 scenes loaded to receive NetworkObject updates.
    /// FishNet's Scene Condition handles visibility based on which scene the player GameObject is in.
    /// 
    /// This applies to ALL clients including the host's local client - each gets their own
    /// stacked LocalPhysics instances via LoadConnectionScenes().
    /// </summary>
    private IEnumerator LoadAllTimelinesForConnection(NetworkConnection conn)
    {
        while (!timelinesLoaded)
        {
            yield return null;
        }
        
        if (showDebugLogs)
            Debug.Log($"[TimelineSceneSetup] Loading all timelines for client {conn.ClientId}...");
        
        yield return StartCoroutine(LoadTimelineForConnectionInternal(conn, TimelineManager.TimelineState.Past));
        yield return StartCoroutine(LoadTimelineForConnectionInternal(conn, TimelineManager.TimelineState.Present));
        yield return StartCoroutine(LoadTimelineForConnectionInternal(conn, TimelineManager.TimelineState.Future));
        
        _initializedConnections.Add(conn);
        
        if (showDebugLogs)
            Debug.Log($"[TimelineSceneSetup] ✅ All timelines loaded for client {conn.ClientId}");
    }
    
    /// <summary>
    /// Internal coroutine to load a single timeline for a connection.
    /// Loads stacked LocalPhysics instances for ALL clients (including host's local client).
    /// </summary>
    private IEnumerator LoadTimelineForConnectionInternal(NetworkConnection conn, TimelineManager.TimelineState timeline)
    {
        Scene targetScene = GetTimelineScene(timeline);
        
        if (!targetScene.IsValid())
        {
            Debug.LogError($"[TimelineSceneSetup] Invalid scene for timeline {timeline}!");
            yield break;
        }
        
        string sceneName = timeline switch
        {
            TimelineManager.TimelineState.Past => pastSceneName,
            TimelineManager.TimelineState.Present => presentSceneName,
            TimelineManager.TimelineState.Future => futureSceneName,
            _ => ""
        };
        
        if (showDebugLogs)
            Debug.Log($"[TimelineSceneSetup] Loading {timeline} for client {conn.ClientId}...");
        
        // Load stacked scene for ALL clients (including host's local client)
        SceneLoadData sld = new SceneLoadData(sceneName);
        sld.Options.LocalPhysics = LocalPhysicsMode.Physics3D;
        sld.Options.AllowStacking = true;
        
        base.SceneManager.LoadConnectionScenes(conn, sld);
        
        float timeout = 10f;
        float elapsed = 0f;
        Scene loadedScene = default;
        
        while (elapsed < timeout)
        {
            if (conn.Scenes != null && conn.Scenes.Count > 0)
            {
                foreach (Scene scene in conn.Scenes)
                {
                    if (scene.name == sceneName)
                    {
                        loadedScene = scene;
                        break;
                    }
                }
                
                if (loadedScene.IsValid())
                {
                    if (showDebugLogs)
                        Debug.Log($"[TimelineSceneSetup] ✅ Client {conn.ClientId} loaded into {timeline}");
                    yield break;
                }
            }
            
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        Debug.LogError($"[TimelineSceneSetup] ❌ Timeout loading {timeline} for client {conn.ClientId}");
    }

    /// <summary>
    /// PUBLIC: Explicitly loads a specific timeline for a connection.
    /// Used by CustomPlayerSpawner before spawning a player.
    /// </summary>
    public void LoadTimelineForConnection(NetworkConnection conn, TimelineManager.TimelineState timeline)
    {
        if (!timelinesLoaded)
        {
            Debug.LogError($"[TimelineSceneSetup] Cannot load timeline - server timelines not loaded yet!");
            return;
        }
        
        StartCoroutine(LoadTimelineForConnectionInternal(conn, timeline));
    }
    
    #endregion
    
    #region Player Transitions
    
    /// <summary>
    /// SERVER-ONLY: Transitions a player from one timeline to another.
    /// 
    /// CRITICAL PROCESS:
    /// 1. Find the player's NetworkObject
    /// 2. Move player GameObject to new timeline scene (SERVER-SIDE!)
    /// 3. FishNet Scene Condition automatically updates visibility for all clients
    /// 4. Verify physics scene and refresh colliders
    /// 
    /// The client already has all 3 scenes loaded, so no loading/unloading is needed.
    /// Moving the GameObject is enough for FishNet to handle visibility changes.
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
        
        if (!_initializedConnections.Contains(conn))
        {
            Debug.LogError($"[TimelineSceneSetup] Cannot transition - client {conn.ClientId} not initialized!");
            return;
        }

        if (showDebugLogs)
            Debug.Log($"[TimelineSceneSetup] Transitioning client {conn.ClientId}: {oldTimeline} → {newTimeline}");
        
        NetworkObject playerObject = FindPlayerForConnection(conn);
        
        if (playerObject == null)
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Cannot find player NetworkObject for client {conn.ClientId}!");
            return;
        }
        
        Scene oldScene = GetTimelineScene(oldTimeline);
        Scene newScene = GetTimelineScene(newTimeline);
        
        if (!newScene.IsValid())
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Invalid scene for timeline {newTimeline}!");
            return;
        }
        
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerObject.gameObject, newScene);
        
        if (showDebugLogs)
            Debug.Log($"[TimelineSceneSetup] ✅ Moved player GameObject to {newTimeline} scene on server");
        
        StartCoroutine(VerifyPlayerPhysicsAfterTransition(playerObject, newScene, newTimeline));
        
        if (showDebugLogs)
            Debug.Log($"[TimelineSceneSetup] ✅ Transition complete for client {conn.ClientId}");
    }
    
    #endregion
    
    #region Physics Verification
    
    /// <summary>
    /// Verifies that all timeline scenes have isolated physics and are NOT using the default physics scene.
    /// </summary>
    private void VerifyPhysicsIsolation()
    {
        Debug.Log("[TimelineSceneSetup] === PHYSICS ISOLATION VERIFICATION ===");
        
        Scene pastScene = default;
        Scene presentScene = default;
        Scene futureScene = default;
        
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            
            if (scene.name == pastSceneName)
                pastScene = scene;
            else if (scene.name == presentSceneName)
                presentScene = scene;
            else if (scene.name == futureSceneName)
                futureScene = scene;
        }
        
        if (!pastScene.IsValid())
        {
            Debug.LogError("[TimelineSceneSetup] ❌ Past scene not found!");
            return;
        }
        if (!presentScene.IsValid())
        {
            Debug.LogError("[TimelineSceneSetup] ❌ Present scene not found!");
            return;
        }
        if (!futureScene.IsValid())
        {
            Debug.LogError("[TimelineSceneSetup] ❌ Future scene not found!");
            return;
        }
        
        _timelineScenes[TimelineManager.TimelineState.Past] = pastScene;
        _timelineScenes[TimelineManager.TimelineState.Present] = presentScene;
        _timelineScenes[TimelineManager.TimelineState.Future] = futureScene;
        
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
        
        bool pastIsDefault = (pastHash == defaultHash);
        bool presentIsDefault = (presentHash == defaultHash);
        bool futureIsDefault = (futureHash == defaultHash);
        
        if (pastIsDefault)
            Debug.LogError("  ❌ Past timeline using DEFAULT physics!");
        if (presentIsDefault)
            Debug.LogError("  ❌ Present timeline using DEFAULT physics!");
        if (futureIsDefault)
            Debug.LogError("  ❌ Future timeline using DEFAULT physics!");
        
        if (pastHash == presentHash || pastHash == futureHash || presentHash == futureHash)
        {
            Debug.LogError("  ❌ Timeline physics scenes are NOT isolated!");
        }
        else if (!pastIsDefault && !presentIsDefault && !futureIsDefault)
        {
            Debug.Log("  ✅ All timelines have ISOLATED LOCAL physics scenes!");
        }
    }
    
    /// <summary>
    /// Verifies that the player is in the correct physics scene after a transition.
    /// Refreshes physics components to ensure proper collision detection.
    /// </summary>
    private IEnumerator VerifyPlayerPhysicsAfterTransition(NetworkObject playerObject, Scene targetScene, TimelineManager.TimelineState timeline)
    {
        yield return new WaitForSeconds(0.5f);
        
        Scene playerCurrentScene = playerObject.gameObject.scene;
        PhysicsScene playerPhysicsScene = playerCurrentScene.GetPhysicsScene();
        PhysicsScene targetPhysicsScene = targetScene.GetPhysicsScene();
        
        int playerHash = playerPhysicsScene.GetHashCode();
        int targetHash = targetPhysicsScene.GetHashCode();
        
        if (playerHash != targetHash)
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Player physics scene mismatch after transition!");
            Debug.LogError($"  Player scene: {playerCurrentScene.name} (hash: {playerHash})");
            Debug.LogError($"  Target scene: {targetScene.name} (hash: {targetHash})");
        }
        else if (showDebugLogs)
        {
            Debug.Log($"[TimelineSceneSetup] ✅ Player physics scene verified for {timeline} (hash: {playerHash})");
        }
        
        RefreshPlayerPhysics(playerObject.gameObject);
    }
    
    /// <summary>
    /// Refreshes physics components after moving to a new scene.
    /// This ensures colliders and rigidbodies are properly registered in the new physics scene.
    /// 
    /// CRITICAL: Without this, colliders may not detect objects in the new physics scene.
    /// </summary>
    private void RefreshPlayerPhysics(GameObject player)
    {
        Collider[] colliders = player.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
            col.enabled = true;
        }
        
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.WakeUp();
        }
        
        if (showDebugLogs)
            Debug.Log($"[TimelineSceneSetup] Refreshed {colliders.Length} colliders and rigidbody");
    }

    /// <summary>
    /// When running as host, load stacked LocalPhysics timeline scenes for the local connection.
    /// CRITICAL FIX: Host's local client needs its OWN LocalPhysics scene instances!
    /// Do NOT use AddConnectionToScene - that just adds to the server's global scenes.
    /// Instead, load stacked scenes for the local client just like remote clients.
    /// </summary>
    private void RegisterHostConnectionIfAvailable()
    {
        if (!base.IsHostStarted)
            return;

        NetworkConnection localConn = base.NetworkManager?.ClientManager?.Connection;
        if (localConn == null)
            return;

        if (_initializedConnections.Contains(localConn))
            return;

        if (showDebugLogs)
            Debug.Log($"[TimelineSceneSetup] Host detected - loading stacked timeline scenes for LocalConnection {localConn.ClientId}");

        StartCoroutine(LoadAllTimelinesForConnection(localConn));
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// SERVER-ONLY: Finds the player NetworkObject for a specific connection.
    /// Searches for a NetworkObject owned by the connection that has a TimelineManager component.
    /// Returns null if player not found.
    /// </summary>
    private NetworkObject FindPlayerForConnection(NetworkConnection conn)
    {
        foreach (NetworkObject nob in base.ServerManager.Objects.Spawned.Values)
        {
            if (nob.Owner == conn)
            {
                if (nob.GetComponent<TimelineManager>() != null)
                {
                    if (showDebugLogs)
                        Debug.Log($"[TimelineSceneSetup] Found player NetworkObject for client {conn.ClientId}");
                    return nob;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets the scene for a specific timeline.
    /// Re-queries by name to ensure we have a fresh Scene struct (Scene is a struct, not a class).
    /// </summary>
    public Scene GetTimelineScene(TimelineManager.TimelineState timeline)
    {
        string sceneName = timeline switch
        {
            TimelineManager.TimelineState.Past => pastSceneName,
            TimelineManager.TimelineState.Present => presentSceneName,
            TimelineManager.TimelineState.Future => futureSceneName,
            _ => ""
        };
        
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"[TimelineSceneSetup] Invalid timeline state: {timeline}");
            return default;
        }
        
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.name == sceneName && scene.IsValid())
            {
                _timelineScenes[timeline] = scene;
                return scene;
            }
        }
        
        if (showDebugLogs)
            Debug.LogWarning($"[TimelineSceneSetup] Timeline {timeline} scene '{sceneName}' not found in loaded scenes!");
        
        return default;
    }
    
    /// <summary>
    /// Alternative name for GetTimelineScene (for backwards compatibility).
    /// </summary>
    public Scene GetSceneForTimeline(TimelineManager.TimelineState timeline)
    {
        return GetTimelineScene(timeline);
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Returns true if all server timeline scenes are loaded and ready.
    /// </summary>
    public bool AreTimelinesLoaded()
    {
        return timelinesLoaded;
    }
    
    /// <summary>
    /// Returns true if a connection has been initialized with all timeline scenes.
    /// Used by CustomPlayerSpawner to determine if a client is ready for spawning.
    /// </summary>
    public bool IsConnectionInitialized(NetworkConnection conn)
    {
        return _initializedConnections.Contains(conn);
    }
    
    /// <summary>
    /// Gets the name of the Past timeline scene.
    /// </summary>
    public string GetPastSceneName() => pastSceneName;
    
    /// <summary>
    /// Gets the name of the Present timeline scene.
    /// </summary>
    public string GetPresentSceneName() => presentSceneName;
    
    /// <summary>
    /// Gets the name of the Future timeline scene.
    /// </summary>
    public string GetFutureSceneName() => futureSceneName;
    
    #endregion
}
