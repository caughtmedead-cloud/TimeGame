using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
using FishNet.Managing.Scened;
using FishNet.Connection;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles loading timeline scenes using FishNet's scene management for proper physics isolation.
/// Based on actual FishNet documentation at https://fish-networking.gitbook.io/docs/
/// </summary>
public class TimelineSceneSetup : NetworkBehaviour
{
    [Header("Timeline Scene Names")]
    [SerializeField] private string pastSceneName = "Timeline_Past";
    [SerializeField] private string presentSceneName = "Timeline_Present";
    [SerializeField] private string futureSceneName = "Timeline_Future";

    // Track loaded timeline scenes
    private Scene pastScene;
    private Scene presentScene;
    private Scene futureScene;
    private bool timelinesLoaded = false;

    // Track scene loads
    private Dictionary<string, Scene> pendingSceneLoads = new Dictionary<string, Scene>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        Debug.Log("[TimelineSceneSetup] Server starting - using FishNet scene management");
        
        // Subscribe to OnLoadEnd to track loaded scenes
        base.SceneManager.OnLoadEnd += OnSceneLoadEnd;
        
        StartCoroutine(LoadTimelineScenesOnServer());
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        
        if (base.SceneManager != null)
        {
            base.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
        }
    }

    /// <summary>
    /// Called when scenes finish loading. Tracks loaded scenes.
    /// Based on FishNet docs: args.LoadedScenes contains Scene references
    /// </summary>
    private void OnSceneLoadEnd(SceneLoadEndEventArgs args)
    {
        // Only process on server
        if (!args.QueueData.AsServer)
            return;

        // Check each loaded scene
        foreach (Scene scene in args.LoadedScenes)
        {
            if (pendingSceneLoads.ContainsKey(scene.name))
            {
                pendingSceneLoads[scene.name] = scene;
                Debug.Log($"[TimelineSceneSetup] ✅ Scene '{scene.name}' loaded (Handle: {scene.handle})");
            }
        }
    }

    private IEnumerator LoadTimelineScenesOnServer()
    {
        Debug.Log("[TimelineSceneSetup] Loading timeline scenes through FishNet SceneManager");
        
        // Load all three timelines
        yield return StartCoroutine(LoadTimelineScene(pastSceneName));
        pastScene = pendingSceneLoads[pastSceneName];
        
        yield return StartCoroutine(LoadTimelineScene(presentSceneName));
        presentScene = pendingSceneLoads[presentSceneName];
        
        yield return StartCoroutine(LoadTimelineScene(futureSceneName));
        futureScene = pendingSceneLoads[futureSceneName];
        
        timelinesLoaded = true;
        
        Debug.Log($"[TimelineSceneSetup] ✅ All timeline scenes loaded!");
        Debug.Log($"  - Past: {pastScene.name} (Handle: {pastScene.handle})");
        Debug.Log($"  - Present: {presentScene.name} (Handle: {presentScene.handle})");
        Debug.Log($"  - Future: {futureScene.name} (Handle: {futureScene.handle})");
        
        VerifyPhysicsIsolation();
    }

    private IEnumerator LoadTimelineScene(string sceneName)
    {
        Debug.Log($"[TimelineSceneSetup] Loading '{sceneName}' through FishNet...");
        
        // Register this scene as pending
        pendingSceneLoads[sceneName] = default;
        
        // Create scene load data with LOCAL PHYSICS
        SceneLoadData sld = new SceneLoadData(sceneName);
        sld.Options.AllowStacking = true;
        sld.Options.LocalPhysics = LocalPhysicsMode.Physics3D;
        sld.Options.AutomaticallyUnload = false;
        
        // Load the scene through FishNet
        base.SceneManager.LoadConnectionScenes(sld);
        
        // Wait for scene to be registered via OnSceneLoadEnd
        float timeout = 10f;
        float elapsed = 0f;
        while (!pendingSceneLoads[sceneName].IsValid() && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
        
        if (!pendingSceneLoads[sceneName].IsValid())
        {
            Debug.LogError($"[TimelineSceneSetup] ❌ Failed to load '{sceneName}' - timeout!");
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

    private void VerifyPhysicsIsolation()
    {
        Debug.Log("[TimelineSceneSetup] === PHYSICS ISOLATION VERIFICATION ===");
        
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
    /// Loads a specific timeline for a connection.
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
    /// Unloads a timeline from a specific connection (client-side only).
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
    /// Transitions a player from one timeline to another.
    /// Unloads old timeline from client, loads new timeline.
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

        // CRITICAL: Unload old timeline from this client's view
        // This removes the visual clutter of objects from the old timeline
        UnloadTimelineForConnection(conn, oldTimeline);
        
        // Load new timeline for this client
        LoadTimelineForConnection(conn, newTimeline);
        
        Debug.Log($"[TimelineSceneSetup] ✅ Transition complete for client {conn.ClientId}");
    }

    /// <summary>
    /// Gets the scene for a specific timeline.
    /// Required by PlayerPhysicsSceneHandler and other systems.
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
    /// Alternative name for GetTimelineScene to match your existing code.
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