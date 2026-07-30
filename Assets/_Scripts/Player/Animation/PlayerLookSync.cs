using UnityEngine;
using FIMSpace.FLook;

/// <summary>
/// Drives head-look behaviour from the local player's camera direction.
/// Singleplayer replacement for the former networked look-direction sync.
/// </summary>
public class PlayerLookSync : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FLookAnimator lookAnimator;
    [SerializeField] private Camera playerCamera;

    [Header("Settings")]
    [SerializeField] private bool enableLookAnimator = true;

    private void Awake()
    {
        if (lookAnimator == null)
            lookAnimator = GetComponent<FLookAnimator>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    private void Start()
    {
        if (lookAnimator == null)
        {
            enabled = false;
            return;
        }

        if (!enableLookAnimator)
            return;

        if (playerCamera == null)
        {
            enabled = false;
        }
    }

    public void SetLookAnimatorEnabled(bool enabled)
    {
        enableLookAnimator = enabled;

        if (lookAnimator != null)
        {
            lookAnimator.enabled = enabled;
        }
    }
}

