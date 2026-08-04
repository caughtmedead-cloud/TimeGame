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
        [SerializeField] private bool verboseLogging = true;

        // State
        private InventoryItemSO equippedItem;
        private System.Guid equippedItemID;
        private PlacedItem equippedPlacedItem; // NESTED INVENTORY: Store full item data including container inventory
        private RectTransform rectTransform;
        private CanvasGroup itemIconCanvasGroup;
        private AspectRatioFitter iconAspectFitter;
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

            // Clip icon rendering and raycasts to the slot bounds
            if (GetComponent<RectMask2D>() == null)
                gameObject.AddComponent<RectMask2D>();

            // Initialize CanvasGroup on itemIconImage
            if (itemIconImage != null)
            {
                itemIconCanvasGroup = itemIconImage.GetComponent<CanvasGroup>();
                if (itemIconCanvasGroup == null)
                {
                    itemIconCanvasGroup = itemIconImage.gameObject.AddComponent<CanvasGroup>();
                    Log("Added CanvasGroup to itemIconImage");
                }

                // AspectRatioFitter keeps the icon at its native proportions inside the slot
                iconAspectFitter = itemIconImage.GetComponent<AspectRatioFitter>();
                if (iconAspectFitter == null)
                {
                    iconAspectFitter = itemIconImage.gameObject.AddComponent<AspectRatioFitter>();
                }
                iconAspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            }
            else
            {
                Debug.LogError($"[EquipmentSlot:{slotName}] itemIconImage is NULL in inspector!", this);
            }

            UpdateVisuals();
        }

        private void Start()
        {
            // Runs after all Awakes — InventoryDragHandler.Instance is guaranteed to be set
            dragHandler = InventoryDragHandler.Instance;
            if (dragHandler != null)
            {
                dragHandler.RegisterDropTarget(this);
                Log("Registered with drag handler");
            }
            else
            {
                Debug.LogWarning($"[EquipmentSlot:{slotName}] No InventoryDragHandler found — drag/drop won't work!");
            }
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

        /// <summary>
        /// DEPRECATED: Creates new PlacedItem from ItemSO, loses ItemInstances and ContainerInventory.
        /// Use TryPlaceExistingItem() instead for drag/drop operations to preserve item lineage.
        /// Only use this for initial spawns (loot generation, quest rewards).
        /// </summary>
        [System.Obsolete("Use TryPlaceExistingItem() for drag/drop to preserve item lineage. This method kills the item's soul!", false)]
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
            equippedPlacedItem = placedItem; // Store the placed item

            Log($"✓ Equipped {item.ItemName} in {slotName}");
            return true;
        }

        /// <summary>
        /// Try to place an existing item (preserving instances and container inventory).
        /// This is the preferred method for equipping items from inventory.
        /// </summary>
        public bool TryPlaceExistingItem(PlacedItem originalItem, GridDirection rotation, Vector2 mouseLocalPosition, out PlacedItem placedItem)
        {
            placedItem = null;

            InventoryItemSO item = originalItem.ItemDefinition as InventoryItemSO;
            if (item == null || !CanAcceptItem(item, rotation, mouseLocalPosition))
            {
                return false;
            }

            // CRITICAL: Create PlacedItem preserving original data BEFORE EquipItem()
            // This ensures equippedPlacedItem is set when OnEquipped event fires!
            // ITEM LINEAGE: Preserve the original InstanceID so systems that track by ID
            // (floating windows, container references) can still find this item after equipping.
            placedItem = new PlacedItem(
                originalItem.InstanceID,
                item,
                Vector2Int.zero,
                GridDirection.Down
            );

            // Transfer ItemInstances and ContainerInventory from original
            if (originalItem.IsInstanceTracked && originalItem.ItemInstances != null && originalItem.ItemInstances.Count > 0)
            {
                // Copy instances from original item
                System.Collections.Generic.List<ItemInstance> instancesCopy = new System.Collections.Generic.List<ItemInstance>(originalItem.ItemInstances);
                placedItem.AddInstances(instancesCopy);
            }

            if (originalItem.ContainerInventory != null)
            {
                placedItem.ContainerInventory = originalItem.ContainerInventory;
            }

            equippedItemID = placedItem.InstanceID;
            equippedPlacedItem = placedItem; // Store BEFORE EquipItem so event handlers can access it

            // Now equip the item (this fires OnEquipped event)
            EquipItem(item);

            Log($"✓ Equipped {item.ItemName} in {slotName} (with {(originalItem.ContainerInventory != null ? "container data" : "no container")})");
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

            // CRITICAL: Fire event BEFORE clearing equippedPlacedItem
            // This allows OnEquipmentUnequipped to save ContainerInventory
            OnItemUnequipped?.Invoke(unequippedItem);

            // Now clear the slot
            equippedItem = null;
            equippedItemID = System.Guid.Empty;
            equippedPlacedItem = null; // Clear stored placed item

            UpdateVisuals();

            Log($"Unequipped {unequippedItem.ItemName}");
            return unequippedItem;
        }

        public System.Guid GetEquippedItemID()
        {
            return equippedItemID;
        }

        /// <summary>
        /// Get the full PlacedItem data for the equipped item (includes container inventory).
        /// Returns null if no item is equipped.
        /// </summary>
        public PlacedItem GetEquippedPlacedItem()
        {
            return equippedPlacedItem;
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
                // Check if we're trying to update visuals while parent hierarchy is inactive
                if (!gameObject.activeInHierarchy)
                {
                    Debug.LogWarning($"[EquipmentSlot:{slotName}] UpdateVisuals called while slot is inactive! Visual update will be deferred until inventory opens.");
                }

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
                    // Update aspect fitter so the icon fits the slot without stretching
                    if (iconAspectFitter != null && equippedItem.ItemIcon.rect.height > 0)
                        iconAspectFitter.aspectRatio = equippedItem.ItemIcon.rect.width / equippedItem.ItemIcon.rect.height;
                    Log($"Updated icon to {equippedItem.ItemName} (activeInHierarchy: {gameObject.activeInHierarchy})");
                }
                else if (!hasItem)
                {
                    itemIconImage.sprite = null;
                    Log($"Cleared icon (activeInHierarchy: {gameObject.activeInHierarchy})");
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
