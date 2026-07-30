using System.Collections.Generic;
using UnityEngine;
using LastMile;

/// <summary>
/// Marks the player as sheltered (immune to sun exposure) while standing inside
/// a shelter-tagged zone. Attach alongside a GenericZone component.
/// </summary>
public class ShelterEffect : ZoneEffect
{
    private Dictionary<GameObject, SunExposureDetector> cachedDetectors = new Dictionary<GameObject, SunExposureDetector>();

    public override void OnPlayerEnter(GameObject player)
    {
        if (!TryGetExposureDetector(player, out SunExposureDetector detector))
        {
            Debug.LogWarning($"[ShelterEffect] Player {player.name} does not have a SunExposureDetector component.");
            return;
        }

        cachedDetectors[player] = detector;
        detector.SetSheltered(true);

        Debug.Log($"[ShelterEffect] Player {player.name} entered shelter — sun exposure checks bypassed.");
    }

    // Shelter is a binary state, not a per-frame modifier — no continuous
    // behavior is needed while the player stays inside the zone. Override is
    // still required by the abstract ZoneEffect contract.
    public override void OnPlayerStay(GameObject player, float deltaTime, float zoneIntensity)
    {
    }

    public override void OnPlayerExit(GameObject player)
    {
        if (!cachedDetectors.TryGetValue(player, out SunExposureDetector detector))
        {
            TryGetExposureDetector(player, out detector);
        }

        if (detector != null)
        {
            detector.SetSheltered(false);
        }

        cachedDetectors.Remove(player);

        Debug.Log($"[ShelterEffect] Player {player.name} exited shelter — sun exposure checks resumed.");
    }

    private bool TryGetExposureDetector(GameObject player, out SunExposureDetector detector)
    {
        detector = player.GetComponentInParent<SunExposureDetector>();
        return detector != null;
    }

    public override string GetEffectDescription()
    {
        return "Shelter: Safe from sun exposure";
    }
}
