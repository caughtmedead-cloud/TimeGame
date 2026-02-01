using UnityEngine;
using UnityEngine.UI;
using System;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Single-item equipment slot with type restrictions.
    /// Used for center panel equipment slots (helmet, vest, weapon, etc.).
    /// Accepts only items of specified types.
    /// </summary>
    public class EquipmentSlot : MonoBehaviour, IInventoryDropTarget
    {
        [Header("Slot Configuration")]
        [Tooltip("Name of this slot (e.g., 'Helmet Slot', 'Primary Weapon')")]
        [SerializeField] private string slotName = "Equipment Slot";

        [Tooltip("Item types this slot accepts")]
        [SerializeField] private ItemType[] acceptedTypes = new ItemType[] { ItemType.None };

        [Header("Visual References")]
        [Tooltip("Image component to display equipped item icon")]
        [SerializeField] private Image itemIconImage;

        [Tooltip("GameObject to show when slot is empty (optional)")]
        [SerializeField] private GameObject emptySlotIndicator;

        [Tooltip("Background image for slot (optional, for hover effects)")]
        [SerializeField] private Image slotBackgroundImage;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        // State
        private InventoryItemSO equippedItem;
        private System.Guid equippedItemID;
        private RectTransform rectTransform;
        private CanvasGroup itemIconCanvasGroup; // Cache the CanvasGroup

        // Events
        public event Action<InventoryItemSO> OnItemEquipped;
        public event Action<InventoryItemSO> OnItemUnequipped;

        /// <summary>
        /// Currently equipped item (null if empty)
        /// </summary>
        public InventoryItemSO EquippedItem => equippedItem;

        /// <summary>
        /// Is this slot currently occupied?
        /// </summary>
        public bool IsOccupied => equippedItem != null;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            
            // Cache CanvasGroup if it exists on itemIconImage
            if (itemIconImage != null)
            {
                itemIconCanvasGroup = itemIconImage.GetComponent<CanvasGroup>();
            }
            
            UpdateVisuals();
        }

        #region IInventoryDropTarget Implementation

        public bool CanAcceptItem(InventoryItemSO item, GridDirection rotation, Vector2 mouseLocalPosition)
        {
            // Already occupied?
            if (IsOccupied)
            {
                Log($"Cannot accept {item.ItemName} - slot already occupied by {equippedItem.ItemName}");
                return false;
            }

            // Check if item type is accepted
            bool typeMatch = false;
            foreach (ItemType acceptedType in acceptedTypes)
            {
                if (item.EquipmentType == acceptedType)
                {
                    typeMatch = true;
                    break;
                }
            }

            if (!typeMatch)
            {
                Log($"Cannot accept {item.ItemName} ({item.EquipmentType}) - slot only accepts: {string.Join(", ", acceptedTypes)}");
                return false;
            }

            Log($"Can accept {item.ItemName} ({item.EquipmentType})");
            return true;
        }

        public bool TryPlaceItem(InventoryItemSO item, GridDirection rotation, Vector2 mouseLocalPosition, out PlacedItem placedItem)
        {
            placedItem = null;

            if (!CanAcceptItem(item, rotation, mouseLocalPosition))
            {
                return false;
            }

            // Equip the item
            EquipItem(item);

            // Create PlacedItem data (slots don't use grid positions)
            // FIX: Use correct constructor signature (Guid, ItemDef, Position, Rotation)
            placedItem = new PlacedItem(
                System.Guid.NewGuid(),   // Guid instanceID
                item,                    // PlacableItemSO itemDefinition
                Vector2Int.zero,         // Vector2Int anchorPosition (slots don't have grid positions)
                GridDirection.Down       // GridDirection rotation (slots don't rotate)
            );

            equippedItemID = placedItem.InstanceID;

            Log($"✓ Equipped {item.ItemName} in {slotName}");
            return true;
        }

        public RectTransform GetRectTransform()
        {
            return rectTransform;
        }

        public string GetDisplayName()
        {
            return slotName;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Manually equip an item (bypassing drag-drop)
        /// </summary>
        public bool TryEquipItem(InventoryItemSO item)
        {
            if (item == null)
            {
                Debug.LogWarning($"[EquipmentSlot] Tried to equip null item in {slotName}");
                return false;
            }

            // Check if accepted
            bool typeMatch = false;
            foreach (ItemType acceptedType in acceptedTypes)
            {
                if (item.EquipmentType == acceptedType)
                {
                    typeMatch = true;
                    break;
                }
            }

            if (!typeMatch)
            {
                Debug.LogWarning($"[EquipmentSlot] {item.ItemName} ({item.EquipmentType}) not accepted by {slotName}");
                return false;
            }

            if (IsOccupied)
            {
                Debug.LogWarning($"[EquipmentSlot] {slotName} already occupied by {equippedItem.ItemName}");
                return false;
            }

            EquipItem(item);
            equippedItemID = System.Guid.NewGuid();
            return true;
        }

        /// <summary>
        /// Unequip current item
        /// </summary>
        public InventoryItemSO UnequipItem()
        {
            if (!IsOccupied)
            {
                Log("Cannot unequip - slot is empty");
                return null;
            }

            InventoryItemSO unequippedItem = equippedItem;
            equippedItem = null;
            equippedItemID = System.Guid.Empty;

            UpdateVisuals();
            OnItemUnequipped?.Invoke(unequippedItem);

            Log($"Unequipped {unequippedItem.ItemName} from {slotName}");
            return unequippedItem;
        }

        /// <summary>
        /// Get the unique ID of the equipped item
        /// </summary>
        public System.Guid GetEquippedItemID()
        {
            return equippedItemID;
        }

        #endregion

        #region Private Methods

        private void EquipItem(InventoryItemSO item)
        {
            equippedItem = item;
            
            // Setup drag functionality BEFORE updating visuals
            if (itemIconImage != null)
            {
                // CRITICAL: Enable raycast target so drag events are received
                itemIconImage.raycastTarget = true;
                
                // Get or add CanvasGroup (cache it)
                if (itemIconCanvasGroup == null)
                {
                    itemIconCanvasGroup = itemIconImage.GetComponent<CanvasGroup>();
                    if (itemIconCanvasGroup == null)
                    {
                        itemIconCanvasGroup = itemIconImage.gameObject.AddComponent<CanvasGroup>();
                        Log($"Added CanvasGroup to {itemIconImage.gameObject.name}");
                    }
                }
                
                // Add drag source component if not present
                EquipmentSlotDragSource dragSource = itemIconImage.GetComponent<EquipmentSlotDragSource>();
                if (dragSource == null)
                {
                    dragSource = itemIconImage.gameObject.AddComponent<EquipmentSlotDragSource>();
                    Log($"Added EquipmentSlotDragSource to {itemIconImage.gameObject.name}");
                }
            }
            
            UpdateVisuals();
            OnItemEquipped?.Invoke(item);
        }

        private void UpdateVisuals()
        {
            bool hasItem = IsOccupied;

            // Update item icon - use CanvasGroup alpha instead of SetActive
            // This keeps the GameObject active so drag events continue to work
            if (itemIconImage != null)
            {
                // Always keep GameObject active (needed for drag events)
                itemIconImage.gameObject.SetActive(true);
                
                // Get CanvasGroup if we don't have it cached yet
                if (itemIconCanvasGroup == null)
                {
                    itemIconCanvasGroup = itemIconImage.GetComponent<CanvasGroup>();
                }
                
                // Use CanvasGroup to control visibility
                if (itemIconCanvasGroup != null)
                {
                    // When empty: alpha = 0 (invisible but still receives events)
                    // When occupied: alpha = 1 (visible)
                    itemIconCanvasGroup.alpha = hasItem ? 1f : 0f;
                    Log($"Set itemIcon alpha to {itemIconCanvasGroup.alpha} (hasItem: {hasItem})");
                }
                else
                {
                    // Fallback if no CanvasGroup (shouldn't happen after first equip)
                    itemIconImage.enabled = hasItem;
                }
                
                if (hasItem && equippedItem.ItemIcon != null)
                {
                    itemIconImage.sprite = equippedItem.ItemIcon;
                }
                else if (!hasItem)
                {
                    // Clear sprite when empty (safety measure)
                    itemIconImage.sprite = null;
                }
            }

            // Update empty indicator
            if (emptySlotIndicator != null)
            {
                emptySlotIndicator.SetActive(!hasItem);
            }
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[EquipmentSlot:{slotName}] {message}");
            }
        }

        #endregion

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-name the GameObject based on accepted types
            if (acceptedTypes != null && acceptedTypes.Length > 0 && acceptedTypes[0] != ItemType.None)
            {
                slotName = $"{acceptedTypes[0]} Slot";
            }
        }
#endif
    }
}
