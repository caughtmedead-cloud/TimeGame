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
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
    }
    
    private void Start()
    {
        // Basic verification that this object exists on the network
        if (NetworkManager != null)
        {
        }
        else
        {
        }
    }
}