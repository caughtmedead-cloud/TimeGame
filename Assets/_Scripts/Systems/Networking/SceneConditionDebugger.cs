using UnityEngine;
using FishNet.Object;
using FishNet.Managing;
using FishNet.Managing.Observing;

/// <summary>
/// Debug script to verify Scene Condition is working properly.
/// Attach to any NetworkObject in a timeline scene to see if it's being observed correctly.
/// </summary>
public class SceneConditionDebugger : NetworkBehaviour
{
    [SerializeField] private string sceneName = "Unknown";
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log($"[SceneConditionDebugger] {sceneName} - Server started for {gameObject.name}");
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"[SceneConditionDebugger] {sceneName} - CLIENT CAN SEE {gameObject.name} (Should only see if in {sceneName})");
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        Debug.Log($"[SceneConditionDebugger] {sceneName} - Client stopped observing {gameObject.name}");
    }
    
    private void Start()
    {
        // Basic verification that this object exists on the network
        if (NetworkManager != null)
        {
            Debug.Log($"[SceneConditionDebugger] {sceneName} - NetworkManager found for {gameObject.name}");
        }
        else
        {
            Debug.LogWarning("[SceneConditionDebugger] ⚠️ NetworkManager not found!");
        }
    }
}