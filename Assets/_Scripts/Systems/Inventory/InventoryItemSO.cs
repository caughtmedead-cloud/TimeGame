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

        [Tooltip("Track individual item instances in stack (durability, uses, etc.)\n" +
                 "TRUE = Each item in stack has individual properties\n" +
                 "FALSE = Items are identical homogeneous stack (just count them)\n\n" +
                 "Use for: Medical supplies, tools, weapons with durability\n" +
                 "Don't use for: Basic resources, ammo, generic consumables")]
        public bool TrackIndividualItems = false;

        [Header("Consumable Properties")]
        [Tooltip("Does this item have limited uses?")]
        public bool HasLimitedUses = false;

        [Tooltip("Maximum number of uses this item has when pristine (1-128)")]
        [Range(1, 128)]
        public int MaxUses = 1;

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

        [Header("Use Effects")]
        [Tooltip("Health restored when this item is used. 0 = no heal.")]
        public float HealAmount = 0f;

        [Tooltip("Hunger restored when this item is used. 0 = no restore.")]
        public float HungerRestore = 0f;

        [Tooltip("Stamina restored when this item is used. 0 = no restore.")]
        public float StaminaRestore = 0f;

        /// <summary>
        /// Prefab for 3D world representation (dropped items, pickups, etc.)
        /// Must have WorldItem component attached.
        /// </summary>
        [Header("World Representation")]
        [Tooltip("Prefab to spawn when dropped in world (must have WorldItem component)")]
        public GameObject WorldItemPrefab;

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

        /// <summary>
        /// CODE MONKEY'S EXACT IMPLEMENTATION:
        /// Get the rotation offset for positioning the visual during drag.
        /// This keeps the item's visual stable when rotating.
        /// 
        /// The offset compensates for how the item's pivot shifts during rotation.
        /// Based on PlacedObjectTypeSO.GetRotationOffset() from Code Monkey's system.
        /// </summary>
        public Vector2Int GetRotationOffset(GridPlacement.GridDirection dir)
        {
            switch (dir)
            {
                default:
                case GridPlacement.GridDirection.Down:
                    return new Vector2Int(0, 0);
                    
                case GridPlacement.GridDirection.Left:
                    return new Vector2Int(0, Width);
                    
                case GridPlacement.GridDirection.Up:
                    return new Vector2Int(Width, Height);
                    
                case GridPlacement.GridDirection.Right:
                    return new Vector2Int(Height, 0);
            }
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
