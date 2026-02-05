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
        [SerializeField] private bool verboseLogging = true; // Enable by default for debugging

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
            Debug.Log($"[EquipmentSlot:{slotName}] Awake called!");
            
            rectTransform = GetComponent<RectTransform>();
            
            // Initialize CanvasGroup on itemIconImage if it exists
            if (itemIconImage != null)
            {
                Debug.Log($"[EquipmentSlot:{slotName}] itemIconImage is assigned: {itemIconImage.gameObject.name}");
                
                itemIconCanvasGroup = itemIconImage.GetComponent<CanvasGroup>();
                if (itemIconCanvasGroup == null)
                {
                    itemIconCanvasGroup = itemIconImage.gameObject.AddComponent<CanvasGroup>();
                    Debug.Log($"[EquipmentSlot:{slotName}] Added CanvasGroup to {itemIconImage.gameObject.name}");
                }
            }
            else
            {
                Debug.LogError($"[EquipmentSlot:{slotName}] itemIconImage is NULL in inspector!", this);
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
            placedItem = new PlacedItem(
                System.Guid.NewGuid(),
                item,
                Vector2Int.zero,
                GridDirection.Down
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

        public bool TryEquipItem(InventoryItemSO item)
        {
            Debug.Log($"[EquipmentSlot:{slotName}] TryEquipItem called with {(item != null ? item.ItemName : "NULL")}");
            
            if (item == null)
            {
                Debug.LogWarning($"[EquipmentSlot:{slotName}] Tried to equip null item");
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
                Debug.LogWarning($"[EquipmentSlot:{slotName}] {item.ItemName} ({item.EquipmentType}) not accepted - only accepts: {string.Join(", ", acceptedTypes)}");
                return false;
            }

            if (IsOccupied)
            {
                Debug.LogWarning($"[EquipmentSlot:{slotName}] Already occupied by {equippedItem.ItemName}");
                return false;
            }

            EquipItem(item);
            equippedItemID = System.Guid.NewGuid();
            Debug.Log($"[EquipmentSlot:{slotName}] Successfully equipped {item.ItemName}");
            return true;
        }

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

            Debug.Log($"[EquipmentSlot:{slotName}] Unequipped {unequippedItem.ItemName}");
            return unequippedItem;
        }

        public System.Guid GetEquippedItemID()
        {
            return equippedItemID;
        }

        #endregion

        #region Private Methods

        private void EquipItem(InventoryItemSO item)
        {
            Debug.Log($"[EquipmentSlot:{slotName}] EquipItem called for {item.ItemName}");
            
            equippedItem = item;
            
            if (itemIconImage != null)
            {
                Debug.Log($"[EquipmentSlot:{slotName}] Setting up itemIconImage for dragging");
                
                // CRITICAL: Enable raycast target so drag events are received
                itemIconImage.raycastTarget = true;
                Debug.Log($"[EquipmentSlot:{slotName}] Set raycastTarget = true");
                
                // Get or add CanvasGroup
                if (itemIconCanvasGroup == null)
                {
                    itemIconCanvasGroup = itemIconImage.GetComponent<CanvasGroup>();
                    if (itemIconCanvasGroup == null)
                    {
                        itemIconCanvasGroup = itemIconImage.gameObject.AddComponent<CanvasGroup>();
                        Debug.Log($"[EquipmentSlot:{slotName}] Added CanvasGroup");
                    }
                }
                
                // CRITICAL: CanvasGroup must allow raycasts for drag events to work!
                itemIconCanvasGroup.blocksRaycasts = true;
                itemIconCanvasGroup.interactable = true;
                Debug.Log($"[EquipmentSlot:{slotName}] Set CanvasGroup blocksRaycasts=true, interactable=true");
                
                // Add drag source component if not present
                EquipmentSlotDragSource dragSource = itemIconImage.GetComponent<EquipmentSlotDragSource>();
                if (dragSource == null)
                {
                    Debug.Log($"[EquipmentSlot:{slotName}] EquipmentSlotDragSource not found, adding it now...");
                    dragSource = itemIconImage.gameObject.AddComponent<EquipmentSlotDragSource>();
                    if (dragSource != null)
                    {
                        Debug.Log($"[EquipmentSlot:{slotName}] ✓ Successfully added EquipmentSlotDragSource to {itemIconImage.gameObject.name}");
                    }
                    else
                    {
                        Debug.LogError($"[EquipmentSlot:{slotName}] ✗ Failed to add EquipmentSlotDragSource!", this);
                    }
                }
                else
                {
                    Debug.Log($"[EquipmentSlot:{slotName}] EquipmentSlotDragSource already exists");
                }
            }
            else
            {
                Debug.LogError($"[EquipmentSlot:{slotName}] itemIconImage is NULL! Cannot setup dragging.", this);
            }
            
            UpdateVisuals();
            OnItemEquipped?.Invoke(item);
        }

        private void UpdateVisuals()
        {
            bool hasItem = IsOccupied;

            if (itemIconImage != null)
            {
                itemIconImage.gameObject.SetActive(true);
                
                if (itemIconCanvasGroup != null)
                {
                    itemIconCanvasGroup.alpha = hasItem ? 1f : 0f;
                    
                    // CRITICAL: When hiding, disable raycasts. When showing, enable them.
                    itemIconCanvasGroup.blocksRaycasts = hasItem;
                    itemIconCanvasGroup.interactable = hasItem;
                    
                    Log($"Set itemIcon alpha to {(hasItem ? 1f : 0f)}, blocksRaycasts={hasItem}");
                }
                
                if (hasItem && equippedItem.ItemIcon != null)
                {
                    itemIconImage.sprite = equippedItem.ItemIcon;
                    Log($"Set sprite to {equippedItem.ItemIcon.name}");
                }
                else if (!hasItem)
                {
                    itemIconImage.sprite = null;
                }
            }

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
            if (acceptedTypes != null && acceptedTypes.Length > 0 && acceptedTypes[0] != ItemType.None)
            {
                slotName = $"{acceptedTypes[0]} Slot";
            }
        }
#endif
    }
}
