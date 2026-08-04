using System.Collections.Generic;
using UnityEngine;
using TimeGame.Systems.Inventory;

namespace LastMile
{
    /// <summary>
    /// ScriptableObject schema describing a single shift's delivery manifest —
    /// the shift's duration and the set of delivery stops the player must
    /// service before sunrise. Instances are authored as assets and handed to
    /// ShiftController.BeginShift.
    /// </summary>
    [CreateAssetMenu(fileName = "New Shift Manifest", menuName = "LastMile/Shift Manifest")]
    public class ShiftManifestSO : ScriptableObject
    {
        /// <summary>Definition of a single delivery stop within a manifest.</summary>
        [System.Serializable]
        public class DeliveryStopDefinition
        {
            [Tooltip("Unique identifier matching a DeliveryStop.StopId in the scene.")]
            public string stopId;

            [Tooltip("Display name of the delivery recipient.")]
            public string recipientName;

            [Tooltip("The item type this stop requires to be delivered.")]
            public InventoryItemSO requiredItem;

            [Tooltip("Quantity of requiredItem that must be delivered to satisfy this stop.")]
            public int requiredQuantity = 1;

            [Tooltip("Payout awarded for completing this delivery.")]
            public int payoutValue;
        }

        [Header("Manifest")]
        [Tooltip("Display name for this shift's manifest.")]
        public string manifestName;

        [Tooltip("Total shift duration, in seconds, from dusk to sunrise. Passed to NightCycleManager.StartShift.")]
        public float shiftDurationSeconds = 600f;

        [Header("Stops")]
        [Tooltip("All delivery stops that make up this shift's manifest.")]
        public List<DeliveryStopDefinition> stops = new List<DeliveryStopDefinition>();

        private void OnValidate()
        {
            if (stops == null)
            {
                return;
            }

            HashSet<string> seenStopIds = new HashSet<string>();
            foreach (DeliveryStopDefinition stop in stops)
            {
                if (stop == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(stop.stopId))
                {
                    Debug.LogWarning($"[ShiftManifestSO] '{name}' has a delivery stop with an empty stopId — DeliveryStop lookups will fail to match it.");
                    continue;
                }

                if (!seenStopIds.Add(stop.stopId))
                {
                    Debug.LogWarning($"[ShiftManifestSO] '{name}' has a duplicate stopId '{stop.stopId}' — DeliveryStop lookups will resolve ambiguously.");
                }
            }
        }
    }
}
