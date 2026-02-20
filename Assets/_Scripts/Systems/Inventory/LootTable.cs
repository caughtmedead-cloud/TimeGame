using UnityEngine;
using System.Collections.Generic;
using TimeGame.Systems.Inventory.UI;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// ScriptableObject loot table for WorldLootContainer.
    ///
    /// Design:
    /// - Each LootEntry targets a specific compartment by index.
    /// - Weight-based random roll: higher weight = more likely to appear.
    /// - Roll count determines how many independent rolls happen per population.
    /// - Items that don't fit in the target compartment are silently skipped
    ///   (intentional — prevents overfilling and matches physical plausibility).
    ///
    /// Usage:
    ///   1. Right-click in Project → Create → Inventory → Loot Table
    ///   2. Fill in entries, assign to WorldLootContainer.lootTable
    ///   3. First time player opens the container, Populate() fires once
    /// </summary>
    [CreateAssetMenu(menuName = "TimeGame/Inventory/Loot Table", fileName = "LootTable_New")]
    public class LootTable : ScriptableObject
    {
        [Header("Roll Settings")]
        [Tooltip("How many loot rolls to perform when this table is populated")]
        [SerializeField] private int rollCount = 5;

        [Tooltip("If true, the same entry can be rolled multiple times. If false, each entry can only appear once.")]
        [SerializeField] private bool allowDuplicates = true;

        [Header("Placement Mode")]
        [Tooltip(
            "Generic: ignores compartmentIndex — each rolled item is placed in any compartment that has space. " +
            "Good for crates, backpacks, random piles.\n\n" +
            "Thematic: respects compartmentIndex per entry. " +
            "Good for gun racks, medical bags, organized containers. " +
            "Enable 'Spill To Other Compartments' per entry to allow overflow if the target is full."
        )]
        [SerializeField] private PlacementMode placementMode = PlacementMode.Generic;

        public enum PlacementMode { Generic, Thematic }

        [Header("Entries")]
        [SerializeField] private List<LootEntry> entries = new List<LootEntry>();

        /// <summary>
        /// Populate the provided compartments using this loot table.
        /// Called once on first open by WorldLootContainer.
        /// </summary>
        public void Populate(List<ContainerCompartment> compartments, bool debugMode = false)
        {
            if (entries == null || entries.Count == 0)
            {
                if (debugMode) Debug.Log("[LootTable] No entries defined — container remains empty");
                return;
            }

            List<LootEntry> pool = new List<LootEntry>(entries);
            float totalWeight = GetTotalWeight(pool);
            int rolls = Mathf.Min(rollCount, allowDuplicates ? rollCount : entries.Count);

            for (int i = 0; i < rolls; i++)
            {
                if (pool.Count == 0) break;

                LootEntry rolled = RollEntry(pool, totalWeight);
                if (rolled == null) break;

                bool placed = placementMode == PlacementMode.Generic
                    ? TryPlaceGeneric(compartments, rolled, debugMode)
                    : TryPlaceThematic(compartments, rolled, debugMode);

                if (debugMode)
                    Debug.Log($"[LootTable] Roll {i + 1} ({placementMode}): {rolled.item?.ItemName ?? "null"} x{rolled.GetQuantity()} — {(placed ? "placed" : "skipped (no space)")}");

                if (!allowDuplicates)
                {
                    pool.Remove(rolled);
                    totalWeight = GetTotalWeight(pool);
                }
            }
        }

        /// <summary>
        /// Generic placement — tries every compartment in order until one fits.
        /// compartmentIndex is ignored.
        /// </summary>
        private bool TryPlaceGeneric(List<ContainerCompartment> compartments, LootEntry entry, bool debugMode)
        {
            foreach (var compartment in compartments)
            {
                if (compartment.InventorySystem == null) continue;
                if (TryPlaceItem(compartment.InventorySystem, entry, debugMode))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Thematic placement — tries the designated compartment first.
        /// If spillToOtherCompartments is enabled and the target is full, tries the rest.
        /// </summary>
        private bool TryPlaceThematic(List<ContainerCompartment> compartments, LootEntry entry, bool debugMode)
        {
            int targetIndex = Mathf.Clamp(entry.compartmentIndex, 0, compartments.Count - 1);
            ContainerCompartment target = compartments[targetIndex];

            if (target.InventorySystem != null && TryPlaceItem(target.InventorySystem, entry, debugMode))
                return true;

            if (!entry.spillToOtherCompartments)
                return false;

            // Spill — try all other compartments in order
            for (int i = 0; i < compartments.Count; i++)
            {
                if (i == targetIndex) continue;
                if (compartments[i].InventorySystem == null) continue;
                if (TryPlaceItem(compartments[i].InventorySystem, entry, debugMode))
                    return true;
            }

            return false;
        }

        private bool TryPlaceItem(InventorySystem system, LootEntry entry, bool debugMode)
        {
            if (entry.item == null) return false;

            int qty = entry.GetQuantity();
            
            // CRITICAL FIX: Respect MaxStackSize when spawning loot
            // If quantity exceeds max stack size, we need to create multiple stacks
            int remainingQty = qty;

            // Try all rotations if item supports it
            GridPlacement.GridDirection[] rotations = entry.item.CanRotate
                ? new[] { GridPlacement.GridDirection.Down, GridPlacement.GridDirection.Right,
                          GridPlacement.GridDirection.Up,   GridPlacement.GridDirection.Left }
                : new[] { GridPlacement.GridDirection.Down };

            bool placedAny = false;

            // Keep placing stacks until we've placed all items or run out of space
            while (remainingQty > 0)
            {
                // Clamp to max stack size for this placement
                int qtyThisStack = entry.item.IsStackable 
                    ? Mathf.Min(remainingQty, entry.item.MaxStackSize) 
                    : 1;

                bool placedThisStack = false;

                foreach (var rotation in rotations)
                {
                    int itemW = entry.item.GetRotatedWidth(rotation);
                    int itemH = entry.item.GetRotatedHeight(rotation);

                    for (int y = 0; y <= system.Height - itemH; y++)
                    {
                        for (int x = 0; x <= system.Width - itemW; x++)
                        {
                            var pos = new Vector2Int(x, y);
                            if (!system.CanAddItem(entry.item, pos, rotation)) continue;

                            bool success = system.TryAddItem(
                                entry.item, pos, rotation,
                                out GridPlacement.PlacedItem placed,
                                qtyThisStack,
                                allowAutoStack: true
                            );

                            if (success)
                            {
                                // Optionally initialise a fresh ItemInstance for tracked items
                                if (entry.item.TrackIndividualItems && placed != null)
                                {
                                    // Create instances for the actual stack count
                                    for (int i = 0; i < qtyThisStack; i++)
                                    {
                                        ItemInstance fresh = new ItemInstance();
                                        if (entry.item.HasLimitedUses)
                                            fresh.UsesRemaining = entry.item.MaxUses;
                                        placed.AddInstances(new List<ItemInstance> { fresh });
                                    }
                                }

                                remainingQty -= qtyThisStack;
                                placedAny = true;
                                placedThisStack = true;

                                if (debugMode && remainingQty > 0)
                                {
                                    Debug.Log($"[LootTable] Placed stack of {qtyThisStack} {entry.item.ItemName}, {remainingQty} remaining to place");
                                }

                                break; // Found a spot for this stack, move to next stack
                            }
                        }
                        if (placedThisStack) break;
                    }
                    if (placedThisStack) break;
                }

                // If we couldn't place this stack, no point trying to place more
                if (!placedThisStack)
                {
                    if (debugMode && remainingQty > 0)
                    {
                        Debug.LogWarning($"[LootTable] Out of space! Could not place remaining {remainingQty} {entry.item.ItemName}");
                    }
                    break;
                }
            }

            return placedAny;
        }

        private float GetTotalWeight(List<LootEntry> pool)
        {
            float total = 0f;
            foreach (var e in pool) total += Mathf.Max(0f, e.weight);
            return total;
        }

        private LootEntry RollEntry(List<LootEntry> pool, float totalWeight)
        {
            if (totalWeight <= 0f) return pool.Count > 0 ? pool[0] : null;

            // Use strict less-than so a roll of exactly 0 doesn't always pick entry 0.
            // Random.Range float is exclusive on the upper bound, so roll is in [0, totalWeight).
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var entry in pool)
            {
                cumulative += Mathf.Max(0f, entry.weight);
                if (roll < cumulative)
                    return entry;
            }

            // Fallback: floating point edge case where roll == totalWeight exactly
            return pool[pool.Count - 1];
        }

        /// <summary>
        /// One entry in the loot table.
        /// </summary>
        [System.Serializable]
        public class LootEntry
        {
            [Tooltip("The item to spawn")]
            public InventoryItemSO item;

            [Tooltip("Relative probability weight. Higher = more likely.")]
            [Min(0f)]
            public float weight = 1f;

            [Tooltip("Minimum quantity to spawn (for stackable items)")]
            [Min(1)]
            public int minQuantity = 1;

            [Tooltip("Maximum quantity to spawn (for stackable items)")]
            [Min(1)]
            public int maxQuantity = 1;

            [Tooltip("Thematic mode only: which compartment index to place this item in (0 = first compartment). Ignored in Generic mode.")]
            [Min(0)]
            public int compartmentIndex = 0;

            [Tooltip("Thematic mode only: if the target compartment is full, try other compartments before giving up. Ignored in Generic mode.")]
            public bool spillToOtherCompartments = false;

            public int GetQuantity()
            {
                if (item != null && !item.IsStackable) return 1;
                return Random.Range(minQuantity, maxQuantity + 1);
            }
        }
    }
}
