using UnityEngine;
using LastMile;

/// <summary>
/// Marks the player as sheltered while standing inside a shelter-tagged zone.
/// Shelter state is reported directly to ShiftController, which resolves the
/// shift's single pass/fail check the moment sunrise fires. Attach alongside
/// a GenericZone component.
/// </summary>
public class ShelterEffect : ZoneEffect
{
    public override void OnPlayerEnter(GameObject player)
    {
        ShiftController.Instance?.SetPlayerSheltered(true);

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
        ShiftController.Instance?.SetPlayerSheltered(false);

        Debug.Log($"[ShelterEffect] Player {player.name} exited shelter — sun exposure checks resumed.");
    }

    public override string GetEffectDescription()
    {
        return "Shelter: Safe from sun exposure";
    }
}
