using System.Collections.Generic;
using UnityEngine;
using TimeGame.Systems.Inventory;
using TimeGame.Systems.GridPlacement;

namespace LastMile
{
    /// <summary>
    /// Placed at an in-world delivery point; resolves whatever cargo is sitting in
    /// a scratch delivery grid against its matching ShiftManifestSO.DeliveryStopDefinition
    /// and reports the outcome to ShiftController. Range-enter/exit is forwarded in by
    /// a paired DeliveryZoneEffect on the same GameObject.
    /// </summary>
    public class DeliveryStop : MonoBehaviour
    {
        /// <summary>Outcome of a ResolveDelivery call.</summary>
        public enum DeliveryOutcome
        {
            Success,
            Partial,
            MissingItems,
            AlreadyDelivered,
            TooLateInShift
        }

        [Header("Identity")]
        [Tooltip("Must match a ShiftManifestSO.DeliveryStopDefinition.stopId in the active manifest.")]
        [SerializeField] private string stopId;

        private ShiftManifestSO.DeliveryStopDefinition definition;

        // Registry of every DeliveryStop the player currently stands within range of,
        // maintained by SetPlayerInRange — lets DeliveryPromptUI avoid a per-frame scan.
        private static readonly List<DeliveryStop> stopsInRange = new List<DeliveryStop>();

        public string StopId => stopId;

        /// <summary>True while the player is within range of this delivery stop.</summary>
        public bool IsPlayerInRange { get; private set; }

        /// <summary>Total quantity of the required item delivered to this stop so far this shift.</summary>
        public int DeliveredQuantity { get; private set; }

        /// <summary>The resolved manifest definition for this stop, or null if not yet resolved. Exposed for debug/UI display.</summary>
        public ShiftManifestSO.DeliveryStopDefinition Definition => definition;

        /// <summary>Quantity of the required item still needed to fully satisfy this stop.</summary>
        public int RemainingQuantity => Definition != null ? Mathf.Max(0, Definition.requiredQuantity - DeliveredQuantity) : 0;

        /// <summary>True once accumulated deliveries have reached the required quantity for this stop.</summary>
        public bool IsFullyDelivered => Definition != null && DeliveredQuantity >= Definition.requiredQuantity;

        /// <summary>Every DeliveryStop the player is currently within range of.</summary>
        public static IReadOnlyList<DeliveryStop> StopsInRange => stopsInRange;

        private void Start()
        {
            ResolveDefinition();
        }

        private void ResolveDefinition()
        {
            if (ShiftController.Instance == null || ShiftController.Instance.ActiveManifest == null)
            {
                Debug.LogError($"[DeliveryStop] '{gameObject.name}' could not resolve its manifest — no active ShiftController/manifest found.");
                return;
            }

            foreach (ShiftManifestSO.DeliveryStopDefinition candidate in ShiftController.Instance.ActiveManifest.stops)
            {
                if (candidate.stopId == stopId)
                {
                    definition = candidate;
                    return;
                }
            }

            Debug.LogError($"[DeliveryStop] '{gameObject.name}' has stopId '{stopId}' which does not match any stop in the active manifest — manifest/scene mismatch.");
        }

        /// <summary>Called by DeliveryZoneEffect when the player enters/exits this stop's zone.</summary>
        public void SetPlayerInRange(bool inRange)
        {
            IsPlayerInRange = inRange;

            if (inRange)
            {
                if (!stopsInRange.Contains(this))
                {
                    stopsInRange.Add(this);
                }
            }
            else
            {
                stopsInRange.Remove(this);
            }
        }

        /// <summary>
        /// Resolves a delivery attempt against whatever is currently sitting in
        /// deliveryGridInventory. Always resolves — classifies the outcome from the
        /// grid's actual contents rather than rejecting the attempt outright. Consumes
        /// exactly what is needed (topping up a prior Partial), returns the rest.
        /// Mutates deliveryGridInventory directly — treat it as empty once this returns.
        /// </summary>
        public DeliveryOutcome ResolveDelivery(InventorySystem deliveryGridInventory, out List<PlacedItem> itemsToReturn)
        {
            itemsToReturn = new List<PlacedItem>();

            // Snapshot before any mutation — GetAllItems() is a live view of the
            // underlying collection and would throw if mutated while enumerated.
            List<PlacedItem> allItems = new List<PlacedItem>(deliveryGridInventory.GetAllItems());

            if (definition == null)
            {
                ResolveDefinition();
            }

            DeliveryOutcome outcome;

            if (IsFullyDelivered)
            {
                itemsToReturn.AddRange(allItems);
                outcome = DeliveryOutcome.AlreadyDelivered;
            }
            else if (NightCycleManager.Instance != null && NightCycleManager.Instance.IsDawnLethalWindow)
            {
                // Design choice, tunable: deliveries stop counting once the lethal dawn
                // window opens, incentivizing the player to already be heading back.
                itemsToReturn.AddRange(allItems);
                outcome = DeliveryOutcome.TooLateInShift;
            }
            else if (definition == null)
            {
                itemsToReturn.AddRange(allItems);
                outcome = DeliveryOutcome.MissingItems;
            }
            else
            {
                List<PlacedItem> matchingItems = new List<PlacedItem>();
                int matchingQuantity = 0;

                foreach (PlacedItem placedItem in allItems)
                {
                    if (placedItem.ItemDefinition == definition.requiredItem)
                    {
                        matchingItems.Add(placedItem);
                        matchingQuantity += placedItem.StackCount;
                    }
                    else
                    {
                        // Wrong items are never consumed, regardless of outcome.
                        itemsToReturn.Add(placedItem);
                    }
                }

                int needed = RemainingQuantity;
                int toConsume = Mathf.Min(needed, matchingQuantity);
                int remainingToConsume = toConsume;

                foreach (PlacedItem placedItem in matchingItems)
                {
                    if (remainingToConsume <= 0)
                    {
                        // Nothing left to consume — return this stack untouched.
                        itemsToReturn.Add(placedItem);
                        continue;
                    }

                    int stackCount = placedItem.StackCount;
                    if (stackCount <= remainingToConsume)
                    {
                        // Fully consumed — not returned, will simply be removed below.
                        remainingToConsume -= stackCount;
                    }
                    else
                    {
                        // Partially consumed — reduce the stack, return the leftover.
                        placedItem.RemoveFromStack(remainingToConsume);
                        remainingToConsume = 0;
                        itemsToReturn.Add(placedItem);
                    }
                }

                DeliveredQuantity += toConsume;

                if (IsFullyDelivered)
                {
                    outcome = DeliveryOutcome.Success;
                    ShiftController.Instance.NotifyStopDelivered(definition);
                }
                else if (toConsume > 0)
                {
                    outcome = DeliveryOutcome.Partial;
                    ShiftController.Instance.NotifyStopPartiallyDelivered(definition, DeliveredQuantity);
                }
                else
                {
                    outcome = DeliveryOutcome.MissingItems;
                }
            }

            // Empty the delivery grid entirely — consumed items are gone, returned items
            // are handed back to the player by the caller, either way this grid is done.
            foreach (PlacedItem placedItem in allItems)
            {
                deliveryGridInventory.RemoveItem(placedItem.InstanceID);
            }

            return outcome;
        }
    }
}
