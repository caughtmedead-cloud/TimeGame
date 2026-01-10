using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Scened;
using System.Collections;

/// <summary>
/// Spawns players using FishNet's scene management system.
/// Fixed to properly detect when connection is added to existing stacked scenes.
/// </summary>
public class CustomPlayerSpawner : NetworkBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private NetworkObject playerPrefab;
    
    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(0, 2, 0);
    [SerializeField] private TimelineManager.TimelineState defaultTimeline = TimelineManager.TimelineState.Present;
    
    private TimelineSceneSetup timelineSetup;

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        timelineSetup = FindObjectOfType<TimelineSceneSetup>();
        if (timelineSetup == null)
        {
            Debug.LogError("[CustomPlayerSpawner] No TimelineSceneSetup found!");
            return;
        }
        
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

    private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
    {
        if (!asServer)
        {
            return;
        }
        
        Debug.Log($"[CustomPlayerSpawner] Client {conn.ClientId} loaded start scenes - beginning spawn sequence");
        StartCoroutine(WaitForScenesAndSpawn(conn));
    }

    private IEnumerator WaitForScenesAndSpawn(NetworkConnection conn)
    {
        Debug.Log($"[CustomPlayerSpawner] [Client {conn.ClientId}] Waiting for server timeline scenes...");
        
        // Wait for server to finish loading all timeline scenes
        while (!timelineSetup.AreTimelinesLoaded())
        {
            yield return null;
        }
        
        Debug.Log($"[CustomPlayerSpawner] ✅ [Client {conn.ClientId}] Server timeline scenes ready!");
        
        // Get the target timeline scene
        Scene targetScene = timelineSetup.GetTimelineScene(defaultTimeline);
        
        if (!targetScene.IsValid())
        {
            Debug.LogError($"[CustomPlayerSpawner] ❌ [Client {conn.ClientId}] Target timeline scene is invalid!");
            yield break;
        }
        
        Debug.Log($"[CustomPlayerSpawner] [Client {conn.ClientId}] Loading {defaultTimeline} timeline...");
        
        // Load the connection into the timeline scene
        timelineSetup.LoadTimelineForConnection(conn, defaultTimeline);
        
        // Wait for connection to be added to the scene
        // When loading into an existing scene, it takes a few frames for FishNet to process
        Debug.Log($"[CustomPlayerSpawner] [Client {conn.ClientId}] Waiting for connection to join scene...");
        
        float timeout = 10f;
        float elapsed = 0f;
        bool connectionInScene = false;
        
        while (elapsed < timeout)
        {
            // Check if connection is now in the target scene
            // NetworkConnection.Scenes is a HashSet<Scene> that contains all scenes the connection is in
            if (conn.Scenes != null && conn.Scenes.Contains(targetScene))
            {
                connectionInScene = true;
                Debug.Log($"[CustomPlayerSpawner] ✅ [Client {conn.ClientId}] Connection is now in {defaultTimeline} timeline!");
                break;
            }
            
            yield return null;
            elapsed += Time.deltaTime;
        }
        
        if (!connectionInScene)
        {
            Debug.LogError($"[CustomPlayerSpawner] ❌ [Client {conn.ClientId}] Timeout - connection never joined scene!");
            Debug.LogError($"[CustomPlayerSpawner]    Target scene: {targetScene.name} (handle: {targetScene.handle})");
            Debug.LogError($"[CustomPlayerSpawner]    Connection scenes count: {conn.Scenes?.Count ?? 0}");
            
            if (conn.Scenes != null)
            {
                foreach (Scene s in conn.Scenes)
                {
                    Debug.LogError($"[CustomPlayerSpawner]      - {s.name} (handle: {s.handle})");
                }
            }
            
            yield break;
        }
        
        // Spawn the player
        Debug.Log($"[CustomPlayerSpawner] [Client {conn.ClientId}] Spawning player...");
        SpawnPlayer(conn, targetScene);
    }

    private void SpawnPlayer(NetworkConnection conn, Scene scene)
    {
        if (!scene.IsValid())
        {
            Debug.LogError($"[CustomPlayerSpawner] Cannot spawn player - invalid scene!");
            return;
        }
        
        NetworkObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        
        // Move player to the timeline scene BEFORE spawning
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerInstance.gameObject, scene);
        
        // Spawn for this connection in this scene
        base.ServerManager.Spawn(playerInstance, conn, scene);
        
        Debug.Log($"[CustomPlayerSpawner] ✅ Player spawned for client {conn.ClientId} in scene '{scene.name}'");
        
        // Verify player is in correct physics scene
        var playerPhysicsScene = playerInstance.gameObject.scene.GetPhysicsScene();
        var defaultPhysicsScene = Physics.defaultPhysicsScene;
        bool isDefault = playerPhysicsScene.GetHashCode() == defaultPhysicsScene.GetHashCode();
        
        if (isDefault)
        {
            Debug.LogError($"[CustomPlayerSpawner] ⚠️ Player is in DEFAULT physics scene!");
            Debug.LogError($"[CustomPlayerSpawner]    This will cause cross-timeline collisions!");
        }
        else
        {
            Debug.Log($"[CustomPlayerSpawner] ✅ Player is in LOCAL physics scene");
            Debug.Log($"[CustomPlayerSpawner]    Physics hash: {playerPhysicsScene.GetHashCode()}");
        }
    }
}