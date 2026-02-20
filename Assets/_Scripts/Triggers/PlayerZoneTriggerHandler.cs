using UnityEngine;
using FishNet.Object;
using FishNet.Component.Prediction;
using FishNet.Connection;

public class PlayerZoneTriggerHandler : NetworkBehaviour
{
    private NetworkTrigger _networkTrigger;
    
    private void Awake()
    {
        _networkTrigger = GetComponentInChildren<NetworkTrigger>();
        
        if (_networkTrigger == null)
        {
            Debug.LogError("[PlayerZoneTriggerHandler] ❌ NetworkTrigger component not found! Add it to the TriggerDetector child object.");
            enabled = false;
            return;
        }
        
        _networkTrigger.OnEnter += OnZoneTriggerEnter;
        _networkTrigger.OnExit += OnZoneTriggerExit;
    }
    
    private void OnDestroy()
    {
        if (_networkTrigger != null)
        {
            _networkTrigger.OnEnter -= OnZoneTriggerEnter;
            _networkTrigger.OnExit -= OnZoneTriggerExit;
        }
    }
    
    private void OnZoneTriggerEnter(Collider other)
    {
        if (!IsOwner) return;
        if (other == null) return;
        
        Debug.Log($"[PlayerZoneTriggerHandler] CLIENT - Detected zone trigger ENTER: {other.gameObject.name}");
        
        // Check for new BaseZone system
        var baseZone = other.GetComponent<BaseZone>();
        if (baseZone != null)
        {
            var zoneNetworkObject = baseZone.GetComponent<NetworkObject>();
            if (zoneNetworkObject != null)
            {
                Debug.Log($"[PlayerZoneTriggerHandler] CLIENT - Notifying server about BaseZone: {baseZone.zoneName}");
                NotifyServerZoneEntered_ServerRpc(zoneNetworkObject);
            }
            else
            {
                Debug.LogWarning($"[PlayerZoneTriggerHandler] ⚠️ Zone '{baseZone.zoneName}' has no NetworkObject component!");
            }
            return;
        }
        
        // Legacy support for EnhancedTemporalZone
        var enhancedZone = other.GetComponent<EnhancedTemporalZone>();
        if (enhancedZone != null)
        {
            var zoneNetworkObject = enhancedZone.GetComponent<NetworkObject>();
            if (zoneNetworkObject != null)
            {
                NotifyServerZoneEntered_ServerRpc(zoneNetworkObject);
            }
        }
    }
    
    private void OnZoneTriggerExit(Collider other)
    {
        if (!IsOwner) return;
        if (other == null) return;
        
        Debug.Log($"[PlayerZoneTriggerHandler] CLIENT - Detected zone trigger EXIT: {other.gameObject.name}");
        
        // Check for new BaseZone system
        var baseZone = other.GetComponent<BaseZone>();
        if (baseZone != null)
        {
            var zoneNetworkObject = baseZone.GetComponent<NetworkObject>();
            if (zoneNetworkObject != null)
            {
                Debug.Log($"[PlayerZoneTriggerHandler] CLIENT - Notifying server about BaseZone exit: {baseZone.zoneName}");
                NotifyServerZoneExited_ServerRpc(zoneNetworkObject);
            }
            return;
        }
        
        // Legacy support
        var enhancedZone = other.GetComponent<EnhancedTemporalZone>();
        if (enhancedZone != null)
        {
            var zoneNetworkObject = enhancedZone.GetComponent<NetworkObject>();
            if (zoneNetworkObject != null)
            {
                NotifyServerZoneExited_ServerRpc(zoneNetworkObject);
            }
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void NotifyServerZoneEntered_ServerRpc(NetworkObject zoneNetworkObject, NetworkConnection sender = null)
    {
        if (zoneNetworkObject == null)
        {
            Debug.LogWarning($"[Server] ⚠️ Zone NetworkObject is null!");
            return;
        }
        
        Debug.Log($"[PlayerZoneTriggerHandler] SERVER RPC - Player {gameObject.name} entered zone");
        
        // New BaseZone system - trigger directly on the zone
        var baseZone = zoneNetworkObject.GetComponent<BaseZone>();
        if (baseZone != null)
        {
            Debug.Log($"[PlayerZoneTriggerHandler] SERVER - Calling BaseZone trigger for {gameObject.name}");
            baseZone.OnNetworkTriggerEnter(gameObject);
            return;
        }
        
        // Legacy EnhancedTemporalZone
        var enhancedZone = zoneNetworkObject.GetComponent<EnhancedTemporalZone>();
        if (enhancedZone != null)
        {
            var stability = GetComponent<TemporalStability>();
            if (stability != null)
            {
                enhancedZone.PlayerEntered(stability);
            }
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void NotifyServerZoneExited_ServerRpc(NetworkObject zoneNetworkObject, NetworkConnection sender = null)
    {
        if (zoneNetworkObject == null)
        {
            Debug.LogWarning($"[Server] ⚠️ Zone NetworkObject is null!");
            return;
        }
        
        Debug.Log($"[PlayerZoneTriggerHandler] SERVER RPC - Player {gameObject.name} exited zone");
        
        // New BaseZone system
        var baseZone = zoneNetworkObject.GetComponent<BaseZone>();
        if (baseZone != null)
        {
            Debug.Log($"[PlayerZoneTriggerHandler] SERVER - Calling BaseZone exit for {gameObject.name}");
            baseZone.OnNetworkTriggerExit(gameObject);
            return;
        }
        
        // Legacy
        var enhancedZone = zoneNetworkObject.GetComponent<EnhancedTemporalZone>();
        if (enhancedZone != null)
        {
            var stability = GetComponent<TemporalStability>();
            if (stability != null)
            {
                enhancedZone.PlayerExited(stability);
            }
        }
    }
}
