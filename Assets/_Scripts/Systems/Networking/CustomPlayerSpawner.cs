using System.Collections;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

/// <summary>
/// Custom player spawner using the instanced dungeon pattern.
/// 
/// FLOW:
/// 1. Server starts → Timeline scenes load on server (via TimelineSceneSetup)
/// 2. Client connects → OnClientLoadedStartScenes fires
/// 3. Wait for server-side timeline scenes to be ready
/// 4. Load Present timeline FOR THIS CLIENT ONLY using LoadConnectionScenes
/// 5. Wait for client to finish loading the scene
/// 6. Spawn player in Present timeline
/// 
/// KEY DIFFERENCE FROM OLD VERSION:
/// - OLD: LoadGlobalScenes → AddConnectionToScene → Spawn
/// - NEW: LoadConnectionScenes → Wait for load → Spawn
/// 
/// The LoadConnectionScenes call automatically handles:
/// - Loading the scene on the client
/// - Adding the connection as an observer to the scene
/// - Making Scene Condition return TRUE for that client only
/// </summary>
public class CustomPlayerSpawner : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkObject playerPrefab;
    
    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(0, 1, 0);
    [SerializeField] private float maxWaitTime = 10f; // Failsafe timeout for scene loading
    
    private TimelineSceneSetup _timelineSceneSetup;
    
    private void Awake()
    {
        // Find TimelineSceneSetup reference
        _timelineSceneSetup = FindFirstObjectByType<TimelineSceneSetup>();
        if (_timelineSceneSetup == null)
        {
            Debug.LogError("[CustomPlayerSpawner] ❌ TimelineSceneSetup not found! Player spawning will fail.");
        }
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        if (base.SceneManager == null)
        {
            Debug.LogError("[CustomPlayerSpawner] ❌ SceneManager is null!");
            return;
        }
        
        // Subscribe to client loaded start scenes event
        base.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
        Debug.Log("[CustomPlayerSpawner] ✅ Subscribed to OnClientLoadedStartScenes");
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        
        if (base.SceneManager != null)
        {
            base.SceneManager.OnClientLoadedStartScenes -= SceneManager_OnClientLoadedStartScenes;
        }
    }
    
    /// <summary>
    /// Called when a client finishes loading their starting scenes.
    /// This is where we initiate the player spawn sequence.
    /// </summary>
    [Server]
    private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
    {
        // Skip if this is the server's own callback
        if (asServer)
        {
            Debug.Log("[CustomPlayerSpawner] Skipping server connection callback");
            return;
        }
        
        Debug.Log($"[CustomPlayerSpawner] Client {conn.ClientId} loaded start scenes - beginning spawn sequence");
        
        // Start coroutine to wait for scenes and then spawn
        StartCoroutine(WaitForScenesAndSpawn(conn));
    }
    
    /// <summary>
    /// Coroutine that waits for server-side timeline scenes to be ready,
    /// loads Present timeline for the connecting client,
    /// waits for the client to finish loading the scene,
    /// then spawns the player.
    /// </summary>
    private IEnumerator WaitForScenesAndSpawn(NetworkConnection conn)
    {
        // ==================================================================================
        // STEP 1: Wait for server-side scenes to be ready
        // ==================================================================================
        Debug.Log($"[CustomPlayerSpawner] [Client {conn.ClientId}] Step 1: Waiting for server-side timeline scenes...");
        
        float waitStartTime = Time.time;
        
        while (_timelineSceneSetup == null || !_timelineSceneSetup.AreScenesReady())
        {
            // Timeout failsafe
            if (Time.time - waitStartTime > maxWaitTime)
            {
                Debug.LogError($"[CustomPlayerSpawner] ❌ [Client {conn.ClientId}] Timeout waiting for server-side timeline scenes!");
                yield break; // Exit coroutine - can't proceed
            }
            
            yield return null; // Wait one frame
        }
        
        Debug.Log($"[CustomPlayerSpawner] ✅ [Client {conn.ClientId}] Server-side timeline scenes ready!");
        
        // ==================================================================================
        // STEP 2: Load Present timeline FOR THIS CLIENT using LoadConnectionScenes
        // ==================================================================================
        Debug.Log($"[CustomPlayerSpawner] [Client {conn.ClientId}] Step 2: Loading Present timeline for this client...");
        
        _timelineSceneSetup.LoadTimelineForConnection(conn, TimelineManager.TimelineState.Present);
        
        // ==================================================================================
        // STEP 3: Wait for client to finish loading the Present timeline scene
        // ==================================================================================
        Debug.Log($"[CustomPlayerSpawner] [Client {conn.ClientId}] Step 3: Waiting for client to finish loading Present timeline...");
        
        waitStartTime = Time.time;
        
        // Wait until the connection's scene list includes the Present scene
        UnityEngine.SceneManagement.Scene presentScene = _timelineSceneSetup.GetSceneForTimeline(TimelineManager.TimelineState.Present);
        
        while (!conn.Scenes.Contains(presentScene))
        {
            // Timeout failsafe
            if (Time.time - waitStartTime > maxWaitTime)
            {
                Debug.LogError($"[CustomPlayerSpawner] ❌ [Client {conn.ClientId}] Timeout waiting for client to load Present timeline!");
                yield break; // Exit coroutine - can't proceed
            }
            
            yield return null; // Wait one frame
        }
        
        Debug.Log($"[CustomPlayerSpawner] ✅ [Client {conn.ClientId}] Client finished loading Present timeline!");
        
        // ==================================================================================
        // STEP 4: Spawn player
        // ==================================================================================
        Debug.Log($"[CustomPlayerSpawner] [Client {conn.ClientId}] Step 4: Spawning player...");
        
        SpawnPlayer(conn, presentScene);
    }
    
    /// <summary>
    /// Spawns player prefab for the given connection.
    /// Player is spawned into the specified scene.
    /// </summary>
    [Server]
    private void SpawnPlayer(NetworkConnection conn, UnityEngine.SceneManagement.Scene scene)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[CustomPlayerSpawner] ❌ Player prefab is not assigned!");
            return;
        }
        
        // Get player prefab from pool (or instantiate if not pooled)
        NetworkObject nob = base.NetworkManager.GetPooledInstantiated(playerPrefab, true);
        
        // Set spawn position
        nob.transform.position = spawnPosition;
        
        // IMPORTANT: Move the player GameObject to the Present timeline scene
        // This ensures the player exists in the correct scene hierarchy
        UnitySceneManager.MoveGameObjectToScene(nob.gameObject, scene);
        
        // Spawn for the specific connection
        // This makes the connection the owner of the player object
        base.ServerManager.Spawn(nob, conn);
        
        Debug.Log($"[CustomPlayerSpawner] ✅ Player spawned for client {conn.ClientId} in scene '{scene.name}'");
    }
}