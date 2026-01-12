using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Scened;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Spawns players using FishNet's scene management system.
/// Ensures players are properly loaded into timeline scenes before spawning.
/// </summary>
public class CustomPlayerSpawner : NetworkBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(0, 2, 0);
    [SerializeField] private TimelineManager.TimelineState defaultTimeline = TimelineManager.TimelineState.Present;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private TimelineSceneSetup timelineSetup;
    
    private static HashSet<NetworkConnection> spawnedConnections = new HashSet<NetworkConnection>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        timelineSetup = FindFirstObjectByType<TimelineSceneSetup>();
        if (timelineSetup == null)
        {
            Debug.LogError("[CustomPlayerSpawner] No TimelineSceneSetup found!");
            return;
        }
        
        base.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
        base.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        
        if (showDebugLogs)
            Debug.Log("[CustomPlayerSpawner] ✅ Subscribed to OnClientLoadedStartScenes");
    }

    private void Start()
    {
        // For scene objects, OnStartServer might not be called if server started before scene load
        // Check if server is already running and initialize if needed
        if (base.IsServerStarted && timelineSetup == null)
        {
            if (showDebugLogs)
                Debug.Log("[CustomPlayerSpawner] Server already started - initializing from Start()");

            timelineSetup = FindFirstObjectByType<TimelineSceneSetup>();
            if (timelineSetup == null)
            {
                Debug.LogError("[CustomPlayerSpawner] No TimelineSceneSetup found!");
                return;
            }

            base.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
            base.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;

            if (showDebugLogs)
                Debug.Log("[CustomPlayerSpawner] ✅ Subscribed to OnClientLoadedStartScenes");
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        
        if (base.SceneManager != null)
        {
            base.SceneManager.OnClientLoadedStartScenes -= SceneManager_OnClientLoadedStartScenes;
        }
        
        if (base.ServerManager != null)
        {
            base.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
        }
        
        spawnedConnections.Clear();
    }

    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
        {
            if (spawnedConnections.Contains(conn))
            {
                if (showDebugLogs)
                    Debug.Log($"[CustomPlayerSpawner] Removing tracking for disconnected client {conn.ClientId}");
                spawnedConnections.Remove(conn);
            }
        }
    }

    private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
    {
        if (!asServer)
        {
            return;
        }
        
        if (spawnedConnections.Contains(conn))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[CustomPlayerSpawner] Client {conn.ClientId} already has a player spawned - skipping");
            return;
        }
        
        if (showDebugLogs)
            Debug.Log($"[CustomPlayerSpawner] Client {conn.ClientId} loaded start scenes - beginning spawn sequence");
        
        StartCoroutine(WaitForScenesAndSpawn(conn));
    }

    private IEnumerator WaitForScenesAndSpawn(NetworkConnection conn)
    {
        if (showDebugLogs)
            Debug.Log($"[CustomPlayerSpawner] Waiting for timelines to load for client {conn.ClientId}...");
        
        // Wait for server timelines
        while (!timelineSetup.AreTimelinesLoaded())
        {
            yield return null;
        }
        
        // CRITICAL: Wait for THIS connection to have all timelines loaded
        while (!timelineSetup.IsConnectionInitialized(conn))
        {
            yield return null;
        }
        
        if (showDebugLogs)
            Debug.Log($"[CustomPlayerSpawner] ✅ Client {conn.ClientId} has all timelines loaded!");
        
        Scene targetScene = timelineSetup.GetTimelineScene(defaultTimeline);
        
        if (!targetScene.IsValid())
        {
            Debug.LogError($"[CustomPlayerSpawner] ❌ Invalid target scene!");
            yield break;
        }
        
        // Verify connection is in a scene with the target name (works for host and remote)
        bool inTargetByName = false;
        if (conn.Scenes != null)
        {
            foreach (Scene s in conn.Scenes)
            {
                if (s.IsValid() && s.name == targetScene.name)
                {
                    inTargetByName = true;
                    break;
                }
            }
        }
        if (!inTargetByName)
        {
            Debug.LogError($"[CustomPlayerSpawner] ❌ Client {conn.ClientId} not in {defaultTimeline} scene by name!");
            yield break;
        }
        
        SpawnPlayer(conn, targetScene);
        spawnedConnections.Add(conn);
    }

    private void SpawnPlayer(NetworkConnection conn, Scene scene)
    {
        if (!scene.IsValid())
        {
            Debug.LogError($"[CustomPlayerSpawner] Cannot spawn player - invalid scene!");
            return;
        }
        
        NetworkObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerInstance.gameObject, scene);
        
        base.ServerManager.Spawn(playerInstance, conn, scene);
        
        if (showDebugLogs)
            Debug.Log($"[CustomPlayerSpawner] ✅ Player spawned for client {conn.ClientId} in scene '{scene.name}'");
        
        PhysicsScene playerPhysicsScene = playerInstance.gameObject.scene.GetPhysicsScene();
        PhysicsScene defaultPhysicsScene = Physics.defaultPhysicsScene;
        bool isDefault = playerPhysicsScene.GetHashCode() == defaultPhysicsScene.GetHashCode();
        
        if (isDefault)
        {
            Debug.LogError($"[CustomPlayerSpawner] ⚠️ Player is in DEFAULT physics scene!");
            Debug.LogError($"[CustomPlayerSpawner]    This will cause cross-timeline collisions!");
        }
        else
        {
            if (showDebugLogs)
            {
                Debug.Log($"[CustomPlayerSpawner] ✅ Player is in LOCAL physics scene");
                Debug.Log($"[CustomPlayerSpawner]    Physics hash: {playerPhysicsScene.GetHashCode()}");
            }
        }
        
        RefreshPlayerPhysics(playerInstance.gameObject);
    }

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
            Debug.Log($"[CustomPlayerSpawner] Refreshed physics components for player");
    }
}
