using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;

/// <summary>
/// Debug component to verify physics scene isolation is working correctly.
/// 
/// Attach to player prefab to see which physics scene they're in and test raycasts.
/// Shows in real-time which objects the player can physically interact with.
/// </summary>
public class PhysicsIsolationDebugger : NetworkBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebug = true;
    [SerializeField] private bool showFrameByFrameLogs = false;
    [SerializeField] private float raycastTestDistance = 10f;
    [SerializeField] private LayerMask testLayerMask = -1;
    
    [Header("Visualization")]
    [SerializeField] private bool drawDebugRays = true;
    [SerializeField] private Color rayColor = Color.yellow;
    [SerializeField] private Color hitColor = Color.red;
    
    private CharacterController _characterController;
    private Scene _currentScene;
    private PhysicsScene _currentPhysicsScene;
    private int _lastSceneHandle = 0;
    
    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (!IsOwner) return;
        
        if (enableDebug)
        {
            Debug.Log($"[PhysicsDebugger] ===== PHYSICS ISOLATION DEBUGGER STARTED =====");
            Debug.Log($"[PhysicsDebugger] Global Physics.simulationMode: {Physics.simulationMode}");
            Debug.Log($"[PhysicsDebugger] Global Physics.autoSimulation: {Physics.autoSimulation}");
            
            LogCurrentPhysicsScene();
        }
    }
    
    private void Update()
    {
        if (!IsOwner || !enableDebug) return;
        
        // Check if we changed scenes
        Scene currentScene = gameObject.scene;
        if (currentScene.handle != _lastSceneHandle)
        {
            Debug.Log($"[PhysicsDebugger] 🔄 Scene changed!");
            LogCurrentPhysicsScene();
            _lastSceneHandle = currentScene.handle;
        }
        
        // Perform test raycasts
        if (showFrameByFrameLogs)
        {
            TestPhysicsRaycast();
        }
    }
    
    private void LogCurrentPhysicsScene()
    {
        _currentScene = gameObject.scene;
        _currentPhysicsScene = _currentScene.GetPhysicsScene();
        _lastSceneHandle = _currentScene.handle;
        
        Debug.Log($"[PhysicsDebugger] ===== CURRENT PHYSICS SCENE INFO =====");
        Debug.Log($"[PhysicsDebugger] GameObject: {gameObject.name}");
        Debug.Log($"[PhysicsDebugger] Scene name: {_currentScene.name}");
        Debug.Log($"[PhysicsDebugger] Scene handle: {_currentScene.handle}");
        Debug.Log($"[PhysicsDebugger] Scene is loaded: {_currentScene.isLoaded}");
        Debug.Log($"[PhysicsDebugger] PhysicsScene valid: {_currentPhysicsScene.IsValid()}");
        
        if (_currentPhysicsScene.IsValid())
        {
            Debug.Log($"[PhysicsDebugger] PhysicsScene hash: {_currentPhysicsScene.GetHashCode()}");
            
            // Test if this is the default physics scene
            bool isDefaultScene = _currentPhysicsScene == Physics.defaultPhysicsScene;
            Debug.Log($"[PhysicsDebugger] Is default physics scene: {isDefaultScene}");
            
            if (isDefaultScene)
            {
                Debug.LogWarning($"[PhysicsDebugger] ⚠️ Player is in DEFAULT physics scene! This will cause cross-timeline physics.");
            }
            else
            {
                Debug.Log($"[PhysicsDebugger] ✅ Player is in LOCAL physics scene (isolated)");
            }
        }
        else
        {
            Debug.LogError($"[PhysicsDebugger] ❌ PhysicsScene is INVALID! Physics will not work correctly.");
        }
        
        // Log all loaded scenes and their physics scenes
        Debug.Log($"[PhysicsDebugger] ----- ALL LOADED SCENES -----");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            PhysicsScene physicsScene = scene.GetPhysicsScene();
            bool isDefault = physicsScene == Physics.defaultPhysicsScene;
            bool isCurrent = scene.handle == _currentScene.handle;
            
            string marker = isCurrent ? "👉 " : "   ";
            string defaultMarker = isDefault ? " (DEFAULT)" : " (LOCAL)";
            
            Debug.Log($"[PhysicsDebugger] {marker}Scene: {scene.name}{defaultMarker}");
            Debug.Log($"[PhysicsDebugger]     Handle: {scene.handle}, Physics valid: {physicsScene.IsValid()}, Physics hash: {physicsScene.GetHashCode()}");
        }
    }
    
    private void TestPhysicsRaycast()
    {
        if (!_currentPhysicsScene.IsValid()) return;
        
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 direction = transform.forward;
        
        // Test using local physics scene (CORRECT)
        bool hitLocal = _currentPhysicsScene.Raycast(
            origin, 
            direction, 
            out RaycastHit hitInfoLocal, 
            raycastTestDistance, 
            testLayerMask, 
            QueryTriggerInteraction.Ignore
        );
        
        // Test using default physics (WRONG - this sees all timelines)
        bool hitDefault = Physics.Raycast(
            origin, 
            direction, 
            out RaycastHit hitInfoDefault, 
            raycastTestDistance, 
            testLayerMask, 
            QueryTriggerInteraction.Ignore
        );
        
        if (hitLocal != hitDefault)
        {
            Debug.LogWarning($"[PhysicsDebugger] ⚠️ PHYSICS MISMATCH DETECTED!");
            Debug.LogWarning($"[PhysicsDebugger]   Local physics hit: {hitLocal} {(hitLocal ? hitInfoLocal.collider.name : "nothing")}");
            Debug.LogWarning($"[PhysicsDebugger]   Default physics hit: {hitDefault} {(hitDefault ? hitInfoDefault.collider.name : "nothing")}");
            Debug.LogWarning($"[PhysicsDebugger]   This means physics is NOT properly isolated!");
        }
        
        if (drawDebugRays)
        {
            Color color = hitLocal ? hitColor : rayColor;
            Debug.DrawRay(origin, direction * (hitLocal ? hitInfoLocal.distance : raycastTestDistance), color);
        }
    }
    
    /// <summary>
    /// Call this method to manually trigger a physics scene debug dump.
    /// Useful for testing from the console or other scripts.
    /// </summary>
    [ContextMenu("Debug Current Physics Scene")]
    public void DebugCurrentPhysicsScene()
    {
        LogCurrentPhysicsScene();
        
        // Also test raycasts in all directions
        Debug.Log($"[PhysicsDebugger] ===== TESTING RAYCASTS =====");
        Vector3[] directions = new Vector3[]
        {
            Vector3.forward,
            Vector3.back,
            Vector3.left,
            Vector3.right,
            Vector3.down
        };
        
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        
        foreach (Vector3 dir in directions)
        {
            bool hit = _currentPhysicsScene.Raycast(
                origin, 
                dir, 
                out RaycastHit hitInfo, 
                raycastTestDistance, 
                testLayerMask, 
                QueryTriggerInteraction.Ignore
            );
            
            Debug.Log($"[PhysicsDebugger] Raycast {dir}: {(hit ? $"HIT {hitInfo.collider.name} at distance {hitInfo.distance:F2}" : "MISS")}");
        }
    }
    
    /// <summary>
    /// GUI overlay showing current physics state
    /// </summary>
    private void OnGUI()
    {
        if (!IsOwner || !enableDebug) return;
        
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        
        float y = 10;
        float x = 10;
        float lineHeight = 20;
        
        GUI.Label(new Rect(x, y, 600, lineHeight), $"Physics Scene: {_currentScene.name}", style);
        y += lineHeight;
        
        bool isDefault = _currentPhysicsScene.IsValid() && _currentPhysicsScene == Physics.defaultPhysicsScene;
        GUIStyle statusStyle = new GUIStyle(style);
        statusStyle.normal.textColor = isDefault ? Color.red : Color.green;
        
        string statusText = isDefault ? "⚠️ IN DEFAULT SCENE (BAD)" : "✅ IN LOCAL SCENE (GOOD)";
        GUI.Label(new Rect(x, y, 600, lineHeight), statusText, statusStyle);
        y += lineHeight;
        
        GUI.Label(new Rect(x, y, 600, lineHeight), $"Simulation Mode: {Physics.simulationMode}", style);
        y += lineHeight;
        
        GUI.Label(new Rect(x, y, 600, lineHeight), $"Auto Simulation: {Physics.autoSimulation}", style);
    }
}
