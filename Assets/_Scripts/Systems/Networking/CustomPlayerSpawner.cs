using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Scened;
using System.Collections;

/// <summary>
/// Spawns players using FishNet's scene management system.
/// Updated to use demo patterns: MovedNetworkObjects and PreferredActiveScene.
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
        
        // FIX #1: Use FindFirstObjectByType instead of deprecated FindObjectOfType
        timelineSetup = FindFirstObjectByType<TimelineSceneSetup>();
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
        
        Debug.Log($"[CustomPlayerSpawner] 👤 Client {conn.ClientId} loaded start scenes - beginning spawn sequence");
        StartCoroutine(WaitForTimelinesAndSpawn(conn));
    }

    private IEnumerator WaitForTimelinesAndSpawn(NetworkConnection conn)
    {
        Debug.Log($"[CustomPlayerSpawner] ⏳ [Client {conn.ClientId}] Waiting for server timeline scenes...");
        
        // Wait for server to finish loading all timeline scenes
        while (!timelineSetup.AreTimelinesLoaded())
        {
            yield return null;
        }
        
        Debug.Log($"[CustomPlayerSpawner] ✅ [Client {conn.ClientId}] Server timeline scenes ready!");
        
        // FIX #2: GetTimelineScene exists in TimelineSceneSetup
        Scene targetScene = timelineSetup.GetTimelineScene(defaultTimeline);
        
        if (!targetScene.IsValid())
        {
            Debug.LogError($"[CustomPlayerSpawner] ❌ [Client {conn.ClientId}] Target timeline scene is invalid!");
            yield break;
        }
        
        Debug.Log($"[CustomPlayerSpawner] 🎯 [Client {conn.ClientId}] Spawning player in {defaultTimeline} timeline (Scene: {targetScene.name})...");
        
        // Spawn the player
        SpawnPlayer(conn, targetScene);
    }

    /// <summary>
    /// Spawns a player for the connection in the specified timeline scene.
    /// Uses demo pattern: spawn player, then use LoadConnectionScenes with MovedNetworkObjects.
    /// </summary>
    private void SpawnPlayer(NetworkConnection conn, Scene targetScene)
    {
        if (!targetScene.IsValid())
        {
            Debug.LogError($"[CustomPlayerSpawner] ❌ [Client {conn.ClientId}] Cannot spawn player - invalid scene!");
            return;
        }
        
        // Instantiate player at spawn position
        NetworkObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        
        // Spawn the player for this connection (starts in DontDestroyOnLoad)
        base.ServerManager.Spawn(playerInstance, conn);
        
        Debug.Log($"[CustomPlayerSpawner] 📍 [Client {conn.ClientId}] Player spawned, moving to timeline...");
        
        // Use demo pattern: LoadConnectionScenes with MovedNetworkObjects
        SceneLookupData lookupData = new SceneLookupData(targetScene);
        SceneLoadData sld = new SceneLoadData(lookupData)
        {
            Options = new LoadOptions()
            {
                AutomaticallyUnload = false
            },
            MovedNetworkObjects = new NetworkObject[] { playerInstance },
            ReplaceScenes = ReplaceOption.None,
            PreferredActiveScene = new PreferredScene(lookupData)
        };
        
        // This single call handles everything!
        base.SceneManager.LoadConnectionScenes(conn, sld);
        
        Debug.Log($"[CustomPlayerSpawner] ✅ [Client {conn.ClientId}] Player spawned successfully in {targetScene.name}!");
        
        // Verify player is in correct physics scene
        VerifyPlayerPhysicsScene(playerInstance.gameObject, targetScene);
    }

    private void VerifyPlayerPhysicsScene(GameObject player, Scene scene)
    {
        var playerPhysicsScene = player.scene.GetPhysicsScene();
        var defaultPhysicsScene = Physics.defaultPhysicsScene;
        bool isDefault = playerPhysicsScene.GetHashCode() == defaultPhysicsScene.GetHashCode();
        
        if (isDefault)
        {
            Debug.LogError($"[CustomPlayerSpawner] ⚠️ Player is in DEFAULT physics scene (server-side)!");
            Debug.LogError($"[CustomPlayerSpawner]    This will cause cross-timeline collisions!");
        }
        else
        {
            Debug.Log($"[CustomPlayerSpawner] ✅ Player is in LOCAL physics scene (server-side)");
            Debug.Log($"[CustomPlayerSpawner]    Physics hash: {playerPhysicsScene.GetHashCode()}");
        }
    }
}