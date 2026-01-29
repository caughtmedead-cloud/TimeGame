using UnityEngine;

namespace NewThelos.Inventory.Data
{
    /// <summary>
    /// ScriptableObject that defines an item type.
    /// This is a READ-ONLY database - never modified at runtime.
    /// 
    /// Based on UGI's ItemDataSo but simplified for our needs.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "New Thelos/Inventory/Item Definition")]
    public class ItemDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this item (used for networking)")]
        public string itemId;
        
        [Tooltip("Display name shown to player")]
        public string displayName;
        
        [Header("Visual")]
        [Tooltip("Icon shown in inventory UI")]
        public Sprite icon;
        
        [Header("Grid Dimensions")]
        [Tooltip("Width in grid cells (1 = single cell)")]
        [Range(1, 10)]
        public int width = 1;
        
        [Tooltip("Height in grid cells (1 = single cell)")]
        [Range(1, 10)]
        public int height = 1;
        
        [Header("Properties")]
        [Tooltip("What type of item is this?")]
        public ItemType itemType = ItemType.Generic;
        
        [Tooltip("Can this item stack?")]
        public bool isStackable = false;
        
        [Tooltip("Maximum stack size (if stackable)")]
        public int maxStackSize = 1;
        
        [Tooltip("Can this item be rotated in inventory?")]
        public bool canRotate = true;
        
        [Header("Description")]
        [TextArea(3, 6)]
        public string description;
        
        /// <summary>
        /// Get the width considering rotation
        /// </summary>
        public int GetWidth(bool isRotated)
        {
            return isRotated ? height : width;
        }
        
        /// <summary>
        /// Get the height considering rotation
        /// </summary>
        public int GetHeight(bool isRotated)
        {
            return isRotated ? width : height;
        }
        
        private void OnValidate()
        {
            // Auto-generate itemId from asset name if empty
            if (string.IsNullOrEmpty(itemId))
            {
                itemId = name;
            }
        }
    }
    
    /// <summary>
    /// Item type categories for gameplay rules
    /// </summary>
    public enum ItemType
    {
        Generic,        // Default
        Consumable,     // Food, medkits, etc.
        Equipment,      // Armor, backpacks
        Weapon,         // Guns, melee
        Ammo,           // Bullets, magazines
        Tool,           // Flashlight, scanner
        KeyItem,        // Quest items, keys
        Resource        // Crafting materials
    }
}