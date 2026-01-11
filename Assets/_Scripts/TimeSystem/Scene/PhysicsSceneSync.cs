using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Synchronizes local physics scene simulation for FishNet stacked/instanced scenes.
/// 
/// DESIGNED FOR: PhysicsMode.Unity + iStep compatibility
/// 
/// This script ensures local physics scenes (created with LocalPhysicsMode.Physics3D) 
/// are manually simulated during Unity's FixedUpdate, while allowing the default physics 
/// scene to auto-simulate normally.
/// 
/// KEY FEATURES:
/// - Works with PhysicsMode.Unity (no global Physics.simulationMode changes)
/// - Compatible with iStep and other animation systems that use Physics raycasts
/// - Maintains physics isolation between timeline scenes
/// - Uses FishNet's TimeManager.OnFixedUpdate for consistent timing
/// 
/// Setup: Add this component to a NetworkObject in EACH timeline scene that needs 
/// isolated physics simulation.
/// </summary>
public class PhysicsSceneSync : NetworkBehaviour
{
    [Header("Physics Type")]
    [Tooltip("Enable 3D physics simulation for this scene's local physics scene")]
    [SerializeField] private bool synchronize3D = true;
    
    [Tooltip("Enable 2D physics simulation for this scene's local physics scene")]
    [SerializeField] private bool synchronize2D = false;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showSimulationLogs = false;
    
    private PhysicsScene _physicsScene;
    private PhysicsScene2D _physicsScene2D;
    private Scene _scene;
    private bool _isLocalPhysicsScene3D;
    private bool _isLocalPhysicsScene2D;
    
    private void Awake()
    {
        _scene = gameObject.scene;
        
        if (synchronize3D)
        {
            _physicsScene = _scene.GetPhysicsScene();
            
            if (!_physicsScene.IsValid())
            {
                Debug.LogWarning($"[PhysicsSceneSync] 3D physics scene not valid for scene '{_scene.name}'. " +
                    "Make sure scene is loaded with LocalPhysicsMode.Physics3D");
                _isLocalPhysicsScene3D = false;
            }
            else
            {
                _isLocalPhysicsScene3D = _physicsScene != Physics.defaultPhysicsScene;
                
                if (_isLocalPhysicsScene3D)
                {
                }
                else
                {
                    Debug.LogWarning($"[PhysicsSceneSync] Scene '{_scene.name}' is using DEFAULT physics scene. " +
                        "No manual simulation needed.");
                }
            }
        }
        
        if (synchronize2D)
        {
            _physicsScene2D = _scene.GetPhysicsScene2D();
            
            if (!_physicsScene2D.IsValid())
            {
                Debug.LogWarning($"[PhysicsSceneSync] 2D physics scene not valid for scene '{_scene.name}'. " +
                    "Make sure scene is loaded with LocalPhysicsMode.Physics2D");
                _isLocalPhysicsScene2D = false;
            }
            else
            {
                _isLocalPhysicsScene2D = _physicsScene2D != Physics2D.defaultPhysicsScene;
                
                if (_isLocalPhysicsScene2D)
                {
                }
                else
                {
                    Debug.LogWarning($"[PhysicsSceneSync] Scene '{_scene.name}' is using DEFAULT 2D physics scene. " +
                        "No manual simulation needed.");
                }
            }
        }
    }
    
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        if ((_isLocalPhysicsScene3D && synchronize3D) || (_isLocalPhysicsScene2D && synchronize2D))
        {
            base.TimeManager.OnFixedUpdate += TimeManager_OnFixedUpdate;
        }
    }
    
    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        
        if ((_isLocalPhysicsScene3D && synchronize3D) || (_isLocalPhysicsScene2D && synchronize2D))
        {
            base.TimeManager.OnFixedUpdate -= TimeManager_OnFixedUpdate;
        }
    }
    
    private void TimeManager_OnFixedUpdate()
    {
        float delta = Time.fixedDeltaTime;
        
        if (_isLocalPhysicsScene3D && synchronize3D && _physicsScene.IsValid())
        {
            try
            {
                _physicsScene.Simulate(delta);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PhysicsSceneSync] ❌ Error simulating 3D physics for '{_scene.name}': {e.Message}");
            }
        }
        
        if (_isLocalPhysicsScene2D && synchronize2D && _physicsScene2D.IsValid())
        {
            try
            {
                _physicsScene2D.Simulate(delta);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PhysicsSceneSync] ❌ Error simulating 2D physics for '{_scene.name}': {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// Debug method to verify physics scene configuration.
    /// Useful for troubleshooting physics isolation issues.
    /// </summary>
    [ContextMenu("Debug Physics Scene Info")]
    public void DebugPhysicsSceneInfo()
    {
        Debug.Log($"[PhysicsSceneSync] === PHYSICS DEBUG INFO for '{_scene.name}' ===");
        Debug.Log($"  TimeManager Physics Mode: {(base.NetworkManager != null ? base.TimeManager.PhysicsMode.ToString() : "N/A")}");
        Debug.Log($"  Global simulation mode: {Physics.simulationMode}");
        Debug.Log($"  Global auto-simulation: {Physics.autoSimulation}");
        Debug.Log($"  3D PhysicsScene valid: {_physicsScene.IsValid()}");
        Debug.Log($"  3D is local scene: {_isLocalPhysicsScene3D}");
        
        if (_physicsScene.IsValid())
        {
            Debug.Log($"  3D PhysicsScene hash: {_physicsScene.GetHashCode()}");
            Debug.Log($"  Default PhysicsScene hash: {Physics.defaultPhysicsScene.GetHashCode()}");
        }
        
        Debug.Log($"  2D PhysicsScene valid: {_physicsScene2D.IsValid()}");
        Debug.Log($"  2D is local scene: {_isLocalPhysicsScene2D}");
        Debug.Log($"  Scene name: {_scene.name}");
        Debug.Log($"  Scene handle: {_scene.handle}");
        Debug.Log($"  Scene is loaded: {_scene.isLoaded}");
        Debug.Log($"======================================================");
    }
}
