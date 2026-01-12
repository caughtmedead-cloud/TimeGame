using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SERVER-ONLY: Manually simulates local physics scenes for timeline isolation.
/// 
/// IMPORTANT: Only the SERVER has LocalPhysics scenes in FishNet's design.
/// Clients use default physics and just visualize server-authoritative state.
/// 
/// This script detects if it's running on the server with a LocalPhysics scene
/// and manually simulates it during FixedUpdate.
/// 
/// ATTACH TO: NetworkObject in EACH timeline scene
/// </summary>
public class PhysicsSceneSync : NetworkBehaviour
{
    [Header("Physics Type")]
    [Tooltip("Enable 3D physics simulation for this scene's local physics scene")]
    [SerializeField] private bool synchronize3D = true;
    
    [Tooltip("Enable 2D physics simulation for this scene's local physics scene")]
    [SerializeField] private bool synchronize2D = false;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    private PhysicsScene _physicsScene;
    private PhysicsScene2D _physicsScene2D;
    private Scene _scene;
    private bool _isLocalPhysicsScene3D;
    private bool _isLocalPhysicsScene2D;
    private bool _isServer;
    
    private void Awake()
    {
        _scene = gameObject.scene;
        _isServer = false;
        
        if (synchronize3D)
        {
            _physicsScene = _scene.GetPhysicsScene();
            _isLocalPhysicsScene3D = _physicsScene.IsValid() && _physicsScene != Physics.defaultPhysicsScene;
        }
        
        if (synchronize2D)
        {
            _physicsScene2D = _scene.GetPhysicsScene2D();
            _isLocalPhysicsScene2D = _physicsScene2D.IsValid() && _physicsScene2D != Physics2D.defaultPhysicsScene;
        }
    }
    
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        _isServer = base.IsServerStarted;
        
        if (_isServer)
        {
            if (_isLocalPhysicsScene3D && showDebugLogs)
                Debug.Log($"[PhysicsSceneSync] SERVER: Simulating LocalPhysics for '{_scene.name}'");
            else if (!_isLocalPhysicsScene3D && showDebugLogs)
                Debug.LogWarning($"[PhysicsSceneSync] SERVER: '{_scene.name}' using default physics - no simulation needed");
            
            if ((_isLocalPhysicsScene3D && synchronize3D) || (_isLocalPhysicsScene2D && synchronize2D))
            {
                base.TimeManager.OnFixedUpdate += TimeManager_OnFixedUpdate;
            }
        }
        else
        {
            if (showDebugLogs)
                Debug.Log($"[PhysicsSceneSync] CLIENT: '{_scene.name}' - default physics (expected FishNet behavior)");
        }
    }
    
    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        
        if (_isServer && ((_isLocalPhysicsScene3D && synchronize3D) || (_isLocalPhysicsScene2D && synchronize2D)))
        {
            base.TimeManager.OnFixedUpdate -= TimeManager_OnFixedUpdate;
        }
    }
    
    private void TimeManager_OnFixedUpdate()
    {
        if (!_isServer)
            return;
            
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
    
    [ContextMenu("Debug Physics Scene Info")]
    public void DebugPhysicsSceneInfo()
    {
        Debug.Log($"[PhysicsSceneSync] === PHYSICS DEBUG INFO for '{_scene.name}' ===");
        Debug.Log($"  Is Server: {_isServer}");
        Debug.Log($"  TimeManager Physics Mode: {(base.NetworkManager != null ? base.TimeManager.PhysicsMode.ToString() : "N/A")}");
        Debug.Log($"  Global simulation mode: {Physics.simulationMode}");
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
        Debug.Log($"  Expected behavior: SERVER has LocalPhysics, CLIENTS have default physics");
        Debug.Log($"======================================================");
    }
}
