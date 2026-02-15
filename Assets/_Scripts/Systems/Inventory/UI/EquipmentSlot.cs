using UnityEngine;
using UnityEngine.UI;
using System;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Single-item equipment slot with type restrictions.
    /// Registers with drag handler to accept dropped items.
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
        private CanvasGroup itemIconCanvasGroup;
        private InventoryDragHandler dragHandler;

        // Events
        public event Action<InventoryItemSO> OnItemEquipped;
        public event Action<InventoryItemSO> OnItemUnequipped;

        public InventoryItemSO EquippedItem => equippedItem;
        public bool IsOccupied => equippedItem != null;

        private void Awake()
        {
            Log("Awake called");
            
            rectTransform = GetComponent<RectTransform>();
            
            // Find drag handler and register
            dragHandler = FindObjectOfType<InventoryDragHandler>();
            if (dragHandler != null)
            {
                dragHandler.RegisterDropTarget(this);
                Log($"Registered with drag handler");
            }
            else
            {
                Debug.LogWarning($"[EquipmentSlot:{slotName}] No InventoryDragHandler found - drag/drop won't work!");
            }
            
            // Initialize CanvasGroup on itemIconImage
            if (itemIconImage != null)
            {
                itemIconCanvasGroup = itemIconImage.GetComponent<CanvasGroup>();
                if (itemIconCanvasGroup == null)
                {
                    itemIconCanvasGroup = itemIconImage.gameObject.AddComponent<CanvasGroup>();
                    Log("Added CanvasGroup to itemIconImage");
                }
            }
            else
            {
                Debug.LogError($"[EquipmentSlot:{slotName}] itemIconImage is NULL in inspector!", this);
            }
            
            UpdateVisuals();
        }

        private void OnDestroy()
        {
            // Unregister from drag handler
            if (dragHandler != null)
            {
                dragHandler.UnregisterDropTarget(this);
                Log("Unregistered from drag handler");
            }
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
                Log($"{item.ItemName} ({item.EquipmentType}) not accepted - only accepts: {string.Join(", ", acceptedTypes)}");
                return false;
            }

            if (IsOccupied)
            {
                Log($"Already occupied by {equippedItem.ItemName}");
                return false;
            }

            EquipItem(item);
            equippedItemID = System.Guid.NewGuid();
            Log($"Successfully equipped {item.ItemName}");
            return true;
        }

        public InventoryItemSO UnequipItem()
        {
            if (!IsOccupied)
            {
                return null;
            }

            InventoryItemSO unequippedItem = equippedItem;
            equippedItem = null;
            equippedItemID = System.Guid.Empty;

            UpdateVisuals();
            OnItemUnequipped?.Invoke(unequippedItem);

            Log($"Unequipped {unequippedItem.ItemName}");
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
            Log($"EquipItem called for {item.ItemName}");
            
            equippedItem = item;
            
            if (itemIconImage != null)
            {
                // Enable raycast target for drag events
                itemIconImage.raycastTarget = true;
                
                // Setup CanvasGroup
                if (itemIconCanvasGroup == null)
                {
                    itemIconCanvasGroup = itemIconImage.GetComponent<CanvasGroup>();
                    if (itemIconCanvasGroup == null)
                    {
                        itemIconCanvasGroup = itemIconImage.gameObject.AddComponent<CanvasGroup>();
                    }
                }
                
                itemIconCanvasGroup.blocksRaycasts = true;
                itemIconCanvasGroup.interactable = true;
                
                // Add drag source if not present
                EquipmentSlotDragSource dragSource = itemIconImage.GetComponent<EquipmentSlotDragSource>();
                if (dragSource == null)
                {
                    dragSource = itemIconImage.gameObject.AddComponent<EquipmentSlotDragSource>();
                    Log($"✓ Added EquipmentSlotDragSource to {itemIconImage.gameObject.name}");
                }
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
                    itemIconCanvasGroup.blocksRaycasts = hasItem;
                    itemIconCanvasGroup.interactable = hasItem;
                }
                
                if (hasItem && equippedItem.ItemIcon != null)
                {
                    itemIconImage.sprite = equippedItem.ItemIcon;
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
