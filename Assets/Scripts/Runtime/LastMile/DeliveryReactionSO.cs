using System.Collections.Generic;
using UnityEngine;

namespace LastMile
{
    /// <summary>
    /// Data table pairing a DeliveryStop.DeliveryOutcome with a set of short reaction
    /// lines. DeliveryGridWindowUI picks a random line for whichever outcome
    /// ResolveDelivery returned so Confirm always shows some feedback text.
    /// </summary>
    [CreateAssetMenu(fileName = "New Delivery Reactions", menuName = "LastMile/Delivery Reactions")]
    public class DeliveryReactionSO : ScriptableObject
    {
        /// <summary>Pairing of a single outcome to its pool of possible reaction lines.</summary>
        [System.Serializable]
        public class ReactionEntry
        {
            public DeliveryStop.DeliveryOutcome outcome;
            public List<string> lines = new List<string>();
        }

        [Tooltip("One entry per DeliveryOutcome, each with a pool of lines to pick from at random.")]
        public List<ReactionEntry> reactions = new List<ReactionEntry>();

        /// <summary>
        /// Returns a random authored line for the given outcome, or a generic default
        /// if no entry (or no lines) has been authored for it — never returns blank text.
        /// </summary>
        public string GetReactionLine(DeliveryStop.DeliveryOutcome outcome)
        {
            if (reactions != null)
            {
                foreach (ReactionEntry entry in reactions)
                {
                    if (entry == null || entry.outcome != outcome)
                    {
                        continue;
                    }

                    if (entry.lines != null && entry.lines.Count > 0)
                    {
                        return entry.lines[Random.Range(0, entry.lines.Count)];
                    }

                    break;
                }
            }

            return GetDefaultLine(outcome);
        }

        private static string GetDefaultLine(DeliveryStop.DeliveryOutcome outcome)
        {
            switch (outcome)
            {
                case DeliveryStop.DeliveryOutcome.Success:
                    return "Delivered.";
                case DeliveryStop.DeliveryOutcome.Partial:
                    return "That's not all of it, but it'll do for now.";
                case DeliveryStop.DeliveryOutcome.MissingItems:
                    return "This isn't what I ordered.";
                case DeliveryStop.DeliveryOutcome.AlreadyDelivered:
                    return "You've already delivered everything here.";
                case DeliveryStop.DeliveryOutcome.TooLateInShift:
                    return "It's too late for this now.";
                default:
                    return string.Empty;
            }
        }

        private void OnValidate()
        {
            if (reactions == null)
            {
                return;
            }

            foreach (ReactionEntry entry in reactions)
            {
                if (entry != null && (entry.lines == null || entry.lines.Count == 0))
                {
                    Debug.LogWarning($"[DeliveryReactionSO] '{name}' has a reaction entry for outcome '{entry.outcome}' with no lines authored — GetReactionLine will fall back to a generic default.");
                }
            }
        }
    }
}
