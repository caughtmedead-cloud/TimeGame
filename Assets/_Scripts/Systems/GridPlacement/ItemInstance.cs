using System;
using UnityEngine;

namespace TimeGame.Systems.GridPlacement
{
    /// <summary>
    /// Represents a single physical item instance within a stack.
    /// Used for tracking individual item properties like durability, uses, condition, etc.
    ///
    /// FishNet Serialization Ready:
    /// - All fields are simple types (Guid, int, float)
    /// - Can be easily serialized with custom Writer/Reader extensions
    /// </summary>
    [System.Serializable]
    public class ItemInstance
    {
        /// <summary>
        /// Unique ID for this specific physical item.
        /// Each item in a stack has its own InstanceID.
        /// </summary>
        public Guid InstanceID;

        /// <summary>
        /// Item durability (0-100).
        /// 100 = pristine, 0 = broken.
        /// </summary>
        public int Durability;

        /// <summary>
        /// Remaining uses for consumable items.
        /// -1 = infinite uses (not consumable).
        /// </summary>
        public int UsesRemaining;

        /// <summary>
        /// Overall item condition (0.0 - 1.0).
        /// Normalized durability value for quick checks.
        /// </summary>
        public float Condition;

        /// <summary>
        /// Default constructor - creates a pristine item.
        /// </summary>
        public ItemInstance()
        {
            InstanceID = Guid.NewGuid();
            Durability = 100;
            UsesRemaining = -1; // -1 = infinite
            Condition = 1.0f;
        }

        /// <summary>
        /// Constructor with specific properties.
        /// </summary>
        public ItemInstance(int durability, int uses = -1)
        {
            InstanceID = Guid.NewGuid();
            Durability = Mathf.Clamp(durability, 0, 100);
            UsesRemaining = uses;
            Condition = durability / 100f;
        }

        /// <summary>
        /// Constructor with specific InstanceID (for network replication).
        /// </summary>
        public ItemInstance(Guid instanceID, int durability, int uses, float condition)
        {
            InstanceID = instanceID;
            Durability = Mathf.Clamp(durability, 0, 100);
            UsesRemaining = uses;
            Condition = Mathf.Clamp01(condition);
        }

        /// <summary>
        /// Apply damage to this item instance.
        /// Returns true if item broke (durability reached 0).
        /// </summary>
        public bool ApplyDamage(int damage)
        {
            Durability = Mathf.Max(0, Durability - damage);
            Condition = Durability / 100f;
            return Durability <= 0;
        }

        /// <summary>
        /// Repair this item instance.
        /// </summary>
        public void Repair(int amount)
        {
            Durability = Mathf.Min(100, Durability + amount);
            Condition = Durability / 100f;
        }

        /// <summary>
        /// Use this item (decrements uses).
        /// Returns true if item has no uses remaining.
        /// </summary>
        public bool UseItem()
        {
            if (UsesRemaining < 0) return false; // Infinite uses

            UsesRemaining--;
            return UsesRemaining <= 0;
        }

        /// <summary>
        /// Check if this item is in pristine condition.
        /// </summary>
        public bool IsPristine()
        {
            return Durability >= 100;
        }

        /// <summary>
        /// Check if this item is damaged.
        /// </summary>
        public bool IsDamaged()
        {
            return Durability < 100;
        }

        /// <summary>
        /// Check if this item is broken.
        /// </summary>
        public bool IsBroken()
        {
            return Durability <= 0;
        }

        public override string ToString()
        {
            string usesInfo = UsesRemaining >= 0 ? $", {UsesRemaining} uses" : "";
            return $"Item {InstanceID.ToString().Substring(0, 8)}... ({Durability}% durability{usesInfo})";
        }
    }
}
