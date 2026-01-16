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
        
        var enhancedZone = other.GetComponent<EnhancedTemporalZone>();
        if (enhancedZone != null)
        {
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
        if (other == null) return;
        
        var enhancedZone = other.GetComponent<EnhancedTemporalZone>();
        if (enhancedZone != null)
        {
            var zoneNetworkObject = enhancedZone.GetComponent<NetworkObject>();
            if (zoneNetworkObject != null)
            {
                NotifyServerEnhancedZoneExited_ServerRpc(zoneNetworkObject);
            }
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
        }
    }
}
