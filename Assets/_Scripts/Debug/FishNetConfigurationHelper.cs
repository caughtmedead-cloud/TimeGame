using UnityEngine;
using FishNet.Managing;
using FishNet.Managing.Timing;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Helper script to verify and configure FishNet NetworkManager for PhysicsMode.Unity + iStep.
/// Attach this to your NetworkManager GameObject temporarily to verify configuration.
/// </summary>
[RequireComponent(typeof(NetworkManager))]
public class FishNetConfigurationHelper : MonoBehaviour
{
    private NetworkManager _networkManager;
    
    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
    }
    
    [ContextMenu("Verify Configuration")]
    public void VerifyConfiguration()
    {
        if (_networkManager == null)
        {
            return;
        }
        
        if (_networkManager.TimeManager == null)
        {
            return;
        }
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && _networkManager == null)
        {
            _networkManager = GetComponent<NetworkManager>();
        }
    }
#endif
}
