using UnityEngine;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Inventory-specific item definition.
    /// Extends the abstract PlacableItemSO with inventory properties.
    /// </summary>
    [CreateAssetMenu(fileName = "New Inventory Item", menuName = "TimeGame/Inventory/Inventory Item")]
    public class InventoryItemSO : GridPlacement.PlacableItemSO
    {
        [Header("Inventory Properties")]
        [Tooltip("Item icon/sprite for UI display")]
        public Sprite ItemIcon;

        [Tooltip("Item description")]
        [TextArea(2, 4)]
        public string Description;

        [Header("Stack Properties")]
        [Tooltip("Can this item stack?")]
        public bool IsStackable = false;

        [Tooltip("Maximum stack size (if stackable)")]
        public int MaxStackSize = 1;

        [Header("Item Type")]
        [Tooltip("Equipment slot type (for slot restrictions)")]
        public ItemType EquipmentType = ItemType.None;

        [Tooltip("Category for filtering/sorting")]
        public ItemCategory Category = ItemCategory.General;

        [Tooltip("Item rarity/quality")]
        public ItemRarity Rarity = ItemRarity.Common;

        [Header("Gameplay Properties")]
        [Tooltip("Item weight (for encumbrance systems)")]
        public float Weight = 1f;

        [Tooltip("Item value (for trading/selling)")]
        public int Value = 1;

        [Header("Storage Properties")]
        [Tooltip("Does this item provide storage when equipped?")]
        public bool ProvidesStorage = false;

        [Tooltip("Size of storage grid this item provides (width x height)")]
        public Vector2Int StorageGridSize = new Vector2Int(6, 4);

        [Tooltip("Maximum weight capacity of this item's storage")]
        public float StorageMaxWeight = 20f;

        /// <summary>
        /// Optional: Prefab for 3D world representation (dropped items, etc.)
        /// </summary>
        [Header("World Representation")]
        [Tooltip("Prefab to spawn when dropped in world (optional)")]
        public GameObject WorldPrefab;

        /// <summary>
        /// Get display name for UI
        /// </summary>
        public string GetDisplayName()
        {
            return string.IsNullOrEmpty(ItemName) ? name : ItemName;
        }

        /// <summary>
        /// Get formatted description with properties
        /// </summary>
        public string GetFormattedDescription()
        {
            string desc = Description;
            desc += $"\n\nSize: {Width}×{Height}";
            desc += $"\nWeight: {Weight}kg";
            desc += $"\nValue: {Value}";
            if (IsStackable)
            {
                desc += $"\nMax Stack: {MaxStackSize}";
            }
            if (ProvidesStorage)
            {
                desc += $"\n\nProvides Storage: {StorageGridSize.x}×{StorageGridSize.y} ({StorageMaxWeight}kg capacity)";
            }
            return desc;
        }
    }

    /// <summary>
    /// Equipment slot types for slot restrictions.
    /// Used by EquipmentSlot to determine which items can be equipped.
    /// </summary>
    public enum ItemType
    {
        None,           // Not equippable, storage only
        Helmet,
        Mask,
        Glasses,
        Headset,
        Vest,
        ArmorPlate,
        Backpack,
        Holster,
        PrimaryWeapon,
        SecondaryWeapon,
        Sidearm,
        Melee,
        Grenade,
        Consumable
    }

    /// <summary>
    /// Item categories for organization
    /// </summary>
    public enum ItemCategory
    {
        General,
        Weapon,
        Ammo,
        Armor,
        Medical,
        Tool,
        Resource,
        Consumable,
        Quest
    }

    /// <summary>
    /// Item rarity/quality tiers
    /// </summary>
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
}
