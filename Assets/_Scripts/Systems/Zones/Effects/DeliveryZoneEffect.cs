using UnityEngine;

/// <summary>
/// Forwards a GenericZone's player enter/exit into the co-located DeliveryStop.
/// Attach alongside a GenericZone component and a LastMile.DeliveryStop component.
/// </summary>
public class DeliveryZoneEffect : ZoneEffect
{
    public override void OnPlayerEnter(GameObject player)
    {
        LastMile.DeliveryStop deliveryStop = GetComponent<LastMile.DeliveryStop>();
        if (deliveryStop == null)
        {
            Debug.LogWarning($"[DeliveryZoneEffect] {gameObject.name} does not have a DeliveryStop component.");
            return;
        }

        deliveryStop.SetPlayerInRange(true);
    }

    // Range is binary, not per-frame — no continuous behavior needed while the
    // player stays inside the zone. Override is still required by the abstract
    // ZoneEffect contract.
    public override void OnPlayerStay(GameObject player, float deltaTime, float zoneIntensity)
    {
    }

    public override void OnPlayerExit(GameObject player)
    {
        LastMile.DeliveryStop deliveryStop = GetComponent<LastMile.DeliveryStop>();
        if (deliveryStop == null)
        {
            Debug.LogWarning($"[DeliveryZoneEffect] {gameObject.name} does not have a DeliveryStop component.");
            return;
        }

        deliveryStop.SetPlayerInRange(false);
    }

    public override string GetEffectDescription()
    {
        return "Delivery Stop: cargo drop-off point";
    }
}
