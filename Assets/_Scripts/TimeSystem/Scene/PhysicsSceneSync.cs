using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Synchronizes local physics scene simulation for stacked/instanced scenes.
/// Required when using LocalPhysicsMode.Physics3D or Physics2D.
/// From FishNet docs: Local physics scenes do NOT simulate automatically; call Simulate() each frame.
/// Setup: add this component to a GameObject in each timeline scene.
/// </summary>
public class PhysicsSceneSync : NetworkBehaviour
{
    [Header("Physics Type")]
    [SerializeField] private bool synchronize3D = true;
    [SerializeField] private bool synchronize2D = false;
    
    private PhysicsScene _physicsScene;
    private PhysicsScene2D _physicsScene2D;
    private Scene _scene;
    
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
            }
        }
        
        if (synchronize2D)
        {
            _physicsScene2D = _scene.GetPhysicsScene2D();
            if (!_physicsScene2D.IsValid())
            {
                Debug.LogWarning($"[PhysicsSceneSync] 2D physics scene not valid for scene '{_scene.name}'. " +
                    "Make sure scene is loaded with LocalPhysicsMode.Physics2D");
            }
        }
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        if (base.NetworkManager != null && base.NetworkManager.TimeManager != null)
        {
            base.TimeManager.OnPrePhysicsSimulation += TimeManager_OnPrePhysicsSimulation;
            Debug.Log($"[PhysicsSceneSync] Subscribed to physics simulation for scene '{_scene.name}'");
        }
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        
        if (base.NetworkManager != null && base.NetworkManager.TimeManager != null)
        {
            base.TimeManager.OnPrePhysicsSimulation -= TimeManager_OnPrePhysicsSimulation;
        }
    }
    
    private void TimeManager_OnPrePhysicsSimulation(float delta)
    {
        if (synchronize3D && _physicsScene.IsValid())
        {
            _physicsScene.Simulate(delta);
        }
        
        if (synchronize2D && _physicsScene2D.IsValid())
        {
            _physicsScene2D.Simulate(delta);
        }
    }
}
