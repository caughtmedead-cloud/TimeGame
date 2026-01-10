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
            Debug.LogError("[FishNetConfigHelper] NetworkManager not found!");
            return;
        }
        
        if (_networkManager.TimeManager == null)
        {
            Debug.LogWarning("[FishNetConfigHelper] TimeManager not initialized yet. Start play mode to check.");
            return;
        }
        
        Debug.Log("=== FISHNET CONFIGURATION ===");
        Debug.Log($"Physics Mode: {_networkManager.TimeManager.PhysicsMode}");
        Debug.Log($"Global Physics.simulationMode: {Physics.simulationMode}");
        Debug.Log($"Global Physics.autoSimulation: {Physics.autoSimulation}");
        
        if (_networkManager.TimeManager.PhysicsMode == PhysicsMode.Unity)
        {
            Debug.Log("✅ CORRECT: PhysicsMode.Unity is set (compatible with iStep)");
        }
        else if (_networkManager.TimeManager.PhysicsMode == PhysicsMode.TimeManager)
        {
            Debug.LogWarning("⚠️ WARNING: PhysicsMode.TimeManager is set. This will break iStep!");
            Debug.LogWarning("   Change to PhysicsMode.Unity in NetworkManager > TimeManager");
        }
        else
        {
            Debug.LogWarning($"⚠️ WARNING: PhysicsMode is set to {_networkManager.TimeManager.PhysicsMode}");
        }
        
        Debug.Log("=============================");
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
