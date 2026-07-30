using UnityEngine;

/// <summary>
/// Plain local trigger collider forwarder. Attach to a child collider object (e.g. on the
/// player) to raise C# events on trigger enter/exit for other systems to consume.
/// Singleplayer replacement for the former FishNet NetworkTrigger.
/// </summary>
public class TriggerDetector : MonoBehaviour
{
    public event System.Action<Collider> OnEnter;
    public event System.Action<Collider> OnExit;

    private void OnTriggerEnter(Collider other)
    {
        OnEnter?.Invoke(other);
    }

    private void OnTriggerExit(Collider other)
    {
        OnExit?.Invoke(other);
    }
}
