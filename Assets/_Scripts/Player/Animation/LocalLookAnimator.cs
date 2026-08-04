using UnityEngine;
using FIMSpace.FLook;

/// <summary>
/// Drives the local player's head/look animation directly from the first-person camera's
/// look target. Singleplayer replacement for the former NetworkedLookAnimator.
/// </summary>
public class LocalLookAnimator : MonoBehaviour
{
    [SerializeField] private FLookAnimator lookAnimator;

    private void Awake()
    {
        if (lookAnimator == null)
            lookAnimator = GetComponent<FLookAnimator>();
    }

    private void Start()
    {
        if (lookAnimator == null) return;

        lookAnimator.enabled = true;

        FirstPersonCamera fpsCam = GetComponent<FirstPersonCamera>();
        if (fpsCam != null && fpsCam.GetLookAtTarget() != null)
        {
            lookAnimator.ObjectToFollow = fpsCam.GetLookAtTarget().transform;

            // Only animate head/neck bones, not root
            lookAnimator.BackBonesCount = 0;
            lookAnimator.LookAnimatorAmount = 1f;
        }
    }
}
