using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Scened;
using System.Collections;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

/// <summary>
/// Manages loading of timeline scenes and player transitions between them.
/// FIXED: Uses singleton pattern to ensure reliable access from TimelineManager.
/// </summary>
public class TimelineSceneSetup : NetworkBehaviour
{
    // Singleton instance (SERVER-ONLY)
    // This ensures TimelineManager can always find it reliably
    private static TimelineSceneSetup _instance;
    public static TimelineSceneSetup Instance => _instance;
    
    [Header("Timeline Scene Names")]
    [SerializeField] private string pastSceneName = "Timeline_Past";
    [SerializeField] private string presentSceneName = "Timeline_Present";
    [SerializeField] private string futureSceneName = "Timeline_Future";

    // Timeline scene references
    private Scene pastScene;
    private Scene presentScene;
    private Scene futureScene;
    
    // Track if timelines are loaded
    private bool timelinesLoaded = false;

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Register singleton (SERVER-ONLY)
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[TimelineSceneSetup] Multiple TimelineSceneSetup instances detected! Using first instance.");
            return;
        }
        
        _instance = this;
        Debug.Log("[TimelineSceneSetup] 🚀 Registered as singleton - Server starting - loading GLOBAL timeline scenes");
        
        // Subscribe to scene load events
        base.SceneManager.OnLoadEnd += OnSceneLoadEnd;
        
        // Start loading timeline scenes globally
        StartCoroutine(LoadGlobalTimelineScenes());
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        
        // Unregister singleton
        if (_instance == this)
        {
            _instance = null;
            Debug.Log("[TimelineSceneSetup] Singleton unregistered");
        }
        
        if (base.SceneManager != null)
        {
            base.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
        }
        
        timelinesLoaded = false;
    }

    private IEnumerator LoadGlobalTimelineScenes()
    {
        Debug.Log("[TimelineSceneSetup] 📦 Loading all timeline scenes globally...");
        
        string[] sceneNames = new string[] 
        { 
            pastSceneName, 
            presentSceneName, 
            futureSceneName 
        };
        
        SceneLoadData sld = new SceneLoadData(sceneNames);
        sld.Options.LocalPhysics = LocalPhysicsMode.Physics3D;
        sld.Options.AutomaticallyUnload = false;
        sld.ReplaceScenes = ReplaceOption.None;
        
        Debug.Log("[TimelineSceneSetup] Global scenes will be sent to all current and future clients");
        Debug.Log("[TimelineSceneSetup] Creating SINGLE SHARED timeline instances (not per-player)");
        
        base.SceneManager.LoadGlobalScenes(sld);
        
        while (!timelinesLoaded)
        {
            yield return null;
        }
        
        Debug.Log("[TimelineSceneSetup] ✅ All timeline scenes loaded globally!");
        Debug.Log($"  📍 Past: {pastScene.name} (Handle: {pastScene.handle})");
        Debug.Log($"  📍 Present: {presentScene.name} (Handle: {presentScene.handle})");
        Debug.Log($"  📍 Future: {futureScene.name} (Handle: {futureScene.handle})");
        
        VerifyPhysicsIsolation();
    }

    private void OnSceneLoadEnd(SceneLoadEndEventArgs args)
    {
        foreach (Scene loadedScene in args.LoadedScenes)
        {
            if (loadedScene.name == pastSceneName)
            {
                pastScene = loadedScene;
                Debug.Log($"[TimelineSceneSetup] ✅ Past timeline loaded: {pastScene.name} (Handle: {pastScene.handle})");
            }
            else if (loadedScene.name == presentSceneName)
            {
                presentScene = loadedScene;
                Debug.Log($"[TimelineSceneSetup] ✅ Present timeline loaded: {presentScene.name} (Handle: {presentScene.handle})");
            }
            else if (loadedScene.name == futureSceneName)
            {
                futureScene = loadedScene;
                Debug.Log($"[TimelineSceneSetup] ✅ Future timeline loaded: {futureScene.name} (Handle: {futureScene.handle})");
            }
        }
        
        if (pastScene.IsValid() && presentScene.IsValid() && futureScene.IsValid())
        {
            timelinesLoaded = true;
        }
    }

    private void VerifyPhysicsIsolation()
    {
        Debug.Log("[TimelineSceneSetup] 🔬 === PHYSICS ISOLATION VERIFICATION ===");
        
        PhysicsScene pastPhysics = pastScene.GetPhysicsScene();
        PhysicsScene presentPhysics = presentScene.GetPhysicsScene();
        PhysicsScene futurePhysics = futureScene.GetPhysicsScene();
        PhysicsScene defaultPhysics = Physics.defaultPhysicsScene;
        
        int pastHash = pastPhysics.GetHashCode();
        int presentHash = presentPhysics.GetHashCode();
        int futureHash = futurePhysics.GetHashCode();
        int defaultHash = defaultPhysics.GetHashCode();
        
        Debug.Log($"  Past physics hash: {pastHash}");
        Debug.Log($"  Present physics hash: {presentHash}");
        Debug.Log($"  Future physics hash: {futureHash}");
        Debug.Log($"  Default physics hash: {defaultHash}");
        
        bool isolated = (pastHash != presentHash) && 
                       (pastHash != futureHash) && 
                       (presentHash != futureHash) &&
                       (pastHash != defaultHash) &&
                       (presentHash != defaultHash) &&
                       (futureHash != defaultHash);
        
        if (isolated)
        {
            Debug.Log("[TimelineSceneSetup] ✅ PHYSICS ISOLATION WORKING - Each timeline has unique physics scene!");
        }
        else
        {
            Debug.LogError("[TimelineSceneSetup] ❌ PHYSICS ISOLATION FAILED - Timelines share physics scenes!");
        }
    }

    [Server]
    public void TransitionPlayerTimeline(NetworkObject player, TimelineManager.TimelineState oldTimeline, TimelineManager.TimelineState newTimeline)
    {
        if (player == null || !player.IsSpawned)
        {
            Debug.LogError("[TimelineSceneSetup] Cannot transition - invalid player!");
            return;
        }
        
        NetworkConnection owner = player.Owner;
        if (!owner.IsActive)
        {
            Debug.LogError("[TimelineSceneSetup] Cannot transition - no active owner!");
            return;
        }
        
        Scene oldScene = player.gameObject.scene;
        Scene newScene = GetTimelineScene(newTimeline);
        
        if (!newScene.IsValid())
        {
            Debug.LogError($"[TimelineSceneSetup] Cannot transition - invalid {newTimeline} scene!");
            return;
        }
        
        if (oldScene == newScene)
        {
            Debug.LogWarning($"[TimelineSceneSetup] Player {owner.ClientId} already in {newTimeline}");
            return;
        }
        
        Debug.Log($"[TimelineSceneSetup] 🔄 Transitioning Player {owner.ClientId} from {oldScene.name} to {newScene.name}");
        
        // Remove connection from old scene FIRST
        if (oldScene.IsValid())
        {
            Debug.Log($"[TimelineSceneSetup] Removing connection from {oldScene.name}");
            base.SceneManager.RemoveConnectionsFromScene(new NetworkConnection[] { owner }, oldScene);
        }
        
        // Move player to new timeline
        SceneLookupData lookupData = new SceneLookupData(newScene);
        SceneLoadData sld = new SceneLoadData(lookupData)
        {
            Options = new LoadOptions()
            {
                AutomaticallyUnload = false
            },
            MovedNetworkObjects = new NetworkObject[] { player },
            ReplaceScenes = ReplaceOption.None,
            PreferredActiveScene = new PreferredScene(lookupData)
        };
        
        base.SceneManager.LoadConnectionScenes(owner, sld);
        
        // Rebuild observers for visibility update
        Debug.Log($"[TimelineSceneSetup] 🔍 Rebuilding observers for all objects...");
        base.ServerManager.Objects.RebuildObservers();
        
        Debug.Log($"[TimelineSceneSetup] ✅ Player {owner.ClientId} transitioned to {newTimeline}, observers rebuilt");
    }

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

    public Scene GetSceneForTimeline(TimelineManager.TimelineState timeline)
    {
        return GetTimelineScene(timeline);
    }

    public bool AreTimelinesLoaded()
    {
        return timelinesLoaded;
    }
}