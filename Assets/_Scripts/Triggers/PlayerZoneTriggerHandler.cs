using UnityEngine;

/// <summary>
/// Forwards the local player's zone trigger enter/exit events directly to the
/// affected BaseZone. Singleplayer replacement for the former server-authoritative
/// FishNet ServerRpc forwarding.
/// </summary>
public class PlayerZoneTriggerHandler : MonoBehaviour
{
    private TriggerDetector _triggerDetector;
    
    private void Awake()
    {
        _triggerDetector = GetComponentInChildren<TriggerDetector>();
        
        if (_triggerDetector == null)
        {
            Debug.LogError("[PlayerZoneTriggerHandler] TriggerDetector component not found! Add it to the TriggerDetector child object.");
            enabled = false;
            return;
        }
        
        _triggerDetector.OnEnter += OnZoneTriggerEnter;
        _triggerDetector.OnExit += OnZoneTriggerExit;
    }
    
    private void OnDestroy()
    {
        if (_triggerDetector != null)
        {
            _triggerDetector.OnEnter -= OnZoneTriggerEnter;
            _triggerDetector.OnExit -= OnZoneTriggerExit;
        }
    }
    
    private void OnZoneTriggerEnter(Collider other)
    {
        if (other == null) return;
        
        var baseZone = other.GetComponent<BaseZone>();
        if (baseZone != null)
        {
            Debug.Log($"[PlayerZoneTriggerHandler] Notifying zone entered: {baseZone.zoneName}");
            baseZone.NotifyObjectEntered(gameObject);
        }
    }
    
    private void OnZoneTriggerExit(Collider other)
    {
        if (other == null) return;
        
        var baseZone = other.GetComponent<BaseZone>();
        if (baseZone != null)
        {
            Debug.Log($"[PlayerZoneTriggerHandler] Notifying zone exited: {baseZone.zoneName}");
            baseZone.NotifyObjectExited(gameObject);
        }
    }
}
