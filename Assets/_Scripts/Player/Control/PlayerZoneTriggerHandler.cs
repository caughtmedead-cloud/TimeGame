using UnityEngine;
using FishNet.Object;
using FishNet.Component.Prediction;
using FishNet.Connection;

public class PlayerZoneTriggerHandler : NetworkBehaviour
{
    private NetworkTrigger _networkTrigger;
    private TemporalStability _temporalStability;
    
    private void Awake()
    {
        _networkTrigger = GetComponentInChildren<NetworkTrigger>();
        _temporalStability = GetComponent<TemporalStability>();
        
        if (_networkTrigger == null)
        {
            Debug.LogError("[PlayerZoneTriggerHandler] ❌ NetworkTrigger component not found! Add it to the TriggerDetector child object.");
            enabled = false;
            return;
        }
        
        if (_temporalStability == null)
        {
            Debug.LogError("[PlayerZoneTriggerHandler] ❌ TemporalStability component not found!");
            enabled = false;
            return;
        }
        
        _networkTrigger.OnEnter += OnZoneTriggerEnter;
        _networkTrigger.OnExit += OnZoneTriggerExit;
        
        Debug.Log($"[PlayerZoneTriggerHandler] ✅ Subscribed to NetworkTrigger events for player {Owner?.ClientId}");
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
        
        Debug.Log($"[PlayerZoneTriggerHandler] 🎯 Trigger entered: {other.gameObject.name}");
        
        var temporalZone = other.GetComponent<TemporalAnomalyZone>();
        if (temporalZone != null)
        {
            Debug.Log($"[PlayerZoneTriggerHandler] ✅ Entered temporal anomaly zone '{temporalZone.zoneName}' - notifying server");
            
            var zoneNetworkObject = temporalZone.GetComponent<NetworkObject>();
            if (zoneNetworkObject != null)
            {
                NotifyServerTemporalZoneEntered_ServerRpc(zoneNetworkObject);
            }
            else
            {
                Debug.LogWarning($"[PlayerZoneTriggerHandler] ⚠️ Temporal anomaly zone '{temporalZone.zoneName}' has no NetworkObject component!");
            }
            return;
        }
        
        var enhancedZone = other.GetComponent<EnhancedTemporalZone>();
        if (enhancedZone != null)
        {
            Debug.Log($"[PlayerZoneTriggerHandler] ✅ Entered enhanced anomaly zone '{enhancedZone.zoneName}' - notifying server");
            
            var zoneNetworkObject = enhancedZone.GetComponent<NetworkObject>();
            if (zoneNetworkObject != null)
            {
                NotifyServerEnhancedZoneEntered_ServerRpc(zoneNetworkObject);
            }
            else
            {
                Debug.LogWarning($"[PlayerZoneTriggerHandler] ⚠️ Enhanced anomaly zone '{enhancedZone.zoneName}' has no NetworkObject component!");
            }
        }
    }
    
    private void OnZoneTriggerExit(Collider other)
    {
        if (!IsOwner) return;
        
        Debug.Log($"[PlayerZoneTriggerHandler] 🚪 Trigger exited: {other.gameObject.name}");
        
        var temporalZone = other.GetComponent<TemporalAnomalyZone>();
        if (temporalZone != null)
        {
            Debug.Log($"[PlayerZoneTriggerHandler] ✅ Exited temporal anomaly zone '{temporalZone.zoneName}' - notifying server");
            
            var zoneNetworkObject = temporalZone.GetComponent<NetworkObject>();
            if (zoneNetworkObject != null)
            {
                NotifyServerTemporalZoneExited_ServerRpc(zoneNetworkObject);
            }
            return;
        }
        
        var enhancedZone = other.GetComponent<EnhancedTemporalZone>();
        if (enhancedZone != null)
        {
            Debug.Log($"[PlayerZoneTriggerHandler] ✅ Exited enhanced anomaly zone '{enhancedZone.zoneName}' - notifying server");
            
            var zoneNetworkObject = enhancedZone.GetComponent<NetworkObject>();
            if (zoneNetworkObject != null)
            {
                NotifyServerEnhancedZoneExited_ServerRpc(zoneNetworkObject);
            }
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void NotifyServerTemporalZoneEntered_ServerRpc(NetworkObject zoneNetworkObject, NetworkConnection sender = null)
    {
        if (zoneNetworkObject == null)
        {
            Debug.LogWarning($"[Server] ⚠️ Zone NetworkObject is null!");
            return;
        }
        
        var zone = zoneNetworkObject.GetComponent<TemporalAnomalyZone>();
        if (zone != null)
        {
            zone.PlayerEntered(_temporalStability);
            Debug.Log($"[Server] ✅ Player {sender?.ClientId} entered temporal zone '{zone.zoneName}'");
        }
        else
        {
            Debug.LogWarning($"[Server] ⚠️ Zone NetworkObject has no TemporalAnomalyZone component!");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void NotifyServerTemporalZoneExited_ServerRpc(NetworkObject zoneNetworkObject, NetworkConnection sender = null)
    {
        if (zoneNetworkObject == null)
        {
            Debug.LogWarning($"[Server] ⚠️ Zone NetworkObject is null!");
            return;
        }
        
        var zone = zoneNetworkObject.GetComponent<TemporalAnomalyZone>();
        if (zone != null)
        {
            zone.PlayerExited(_temporalStability);
            Debug.Log($"[Server] ✅ Player {sender?.ClientId} exited temporal zone '{zone.zoneName}'");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void NotifyServerEnhancedZoneEntered_ServerRpc(NetworkObject zoneNetworkObject, NetworkConnection sender = null)
    {
        if (zoneNetworkObject == null)
        {
            Debug.LogWarning($"[Server] ⚠️ Zone NetworkObject is null!");
            return;
        }
        
        var zone = zoneNetworkObject.GetComponent<EnhancedTemporalZone>();
        if (zone != null)
        {
            zone.PlayerEntered(_temporalStability);
            Debug.Log($"[Server] ✅ Player {sender?.ClientId} entered enhanced zone '{zone.zoneName}'");
        }
        else
        {
            Debug.LogWarning($"[Server] ⚠️ Zone NetworkObject has no EnhancedTemporalZone component!");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void NotifyServerEnhancedZoneExited_ServerRpc(NetworkObject zoneNetworkObject, NetworkConnection sender = null)
    {
        if (zoneNetworkObject == null)
        {
            Debug.LogWarning($"[Server] ⚠️ Zone NetworkObject is null!");
            return;
        }
        
        var zone = zoneNetworkObject.GetComponent<EnhancedTemporalZone>();
        if (zone != null)
        {
            zone.PlayerExited(_temporalStability);
            Debug.Log($"[Server] ✅ Player {sender?.ClientId} exited enhanced zone '{zone.zoneName}'");
        }
    }
}
