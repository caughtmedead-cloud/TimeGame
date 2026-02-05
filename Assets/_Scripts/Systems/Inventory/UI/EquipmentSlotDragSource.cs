using UnityEngine;
using UnityEngine.EventSystems;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Makes equipment slot items draggable using Code Monkey's pattern.
    /// Attach this to the itemIconImage GameObject of an EquipmentSlot.
    /// 
    /// Creates a temporary visual when drag starts, similar to how grids spawn visuals.
    /// This preserves item instance data (durability, etc.) during drag operations.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class EquipmentSlotDragSource : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        private EquipmentSlot equipmentSlot;
        private InventoryDragHandler dragHandler;
        
        // Drag state
        private PlacedItem draggedItem; // Store the full PlacedItem (includes durability, etc.)
        private InventoryItemVisual tempVisual;
        private Transform tempParent; // Temporary parent for the visual during drag
        private bool isDragging = false;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true; // Enable by default for debugging

        private void Awake()
        {
            Debug.Log("[EquipmentSlotDragSource] Awake called!");
            
            // Find parent equipment slot
            equipmentSlot = GetComponentInParent<EquipmentSlot>();
            if (equipmentSlot == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] No EquipmentSlot found in parent!", this);
            }
            else
            {
                Debug.Log($"[EquipmentSlotDragSource] Found equipment slot: {equipmentSlot.GetDisplayName()}");
            }
            
            // Find drag handler (should be global singleton)
            dragHandler = FindObjectOfType<InventoryDragHandler>();
            if (dragHandler == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] No InventoryDragHandler found in scene!", this);
            }
            else
            {
                Debug.Log("[EquipmentSlotDragSource] Found InventoryDragHandler");
            }

            // Create a temporary parent for drag visuals (sibling to the slot)
            if (equipmentSlot != null && equipmentSlot.transform.parent != null)
            {
                GameObject tempParentGO = new GameObject("EquipmentDragContainer");
                tempParentGO.transform.SetParent(equipmentSlot.transform.parent, false);
                tempParent = tempParentGO.transform;
                
                // Add RectTransform
                RectTransform rt = tempParentGO.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                
                Debug.Log("[EquipmentSlotDragSource] Created temp parent container");
            }
            else
            {
                Debug.LogError("[EquipmentSlotDragSource] Cannot create temp parent - equipment slot or its parent is null!", this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Debug.Log($"[EquipmentSlotDragSource] OnBeginDrag triggered! Button: {eventData.button}");
            
            if (equipmentSlot == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] equipmentSlot is null!");
                return;
            }
            
            if (dragHandler == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] dragHandler is null!");
                return;
            }
            
            if (!equipmentSlot.IsOccupied)
            {
                Debug.Log("[EquipmentSlotDragSource] Equipment slot is not occupied");
                return;
            }

            // Get the equipped PlacedItem (this preserves durability, etc.)
            InventoryItemSO itemDef = equipmentSlot.EquippedItem;
            System.Guid equippedID = equipmentSlot.GetEquippedItemID();
            
            Debug.Log($"[EquipmentSlotDragSource] Beginning drag of {itemDef.ItemName} (ID: {equippedID})");
            
            // Create PlacedItem if equipment slot doesn't have one
            draggedItem = new PlacedItem(
                equippedID,
                itemDef,
                Vector2Int.zero, // Equipment slots don't have grid positions
                GridDirection.Down // Equipment items don't rotate in slots
            );

            // Unequip from slot (this hides the icon)
            equipmentSlot.UnequipItem();
            Debug.Log("[EquipmentSlotDragSource] Unequipped item from slot");

            // Create temporary visual similar to how InventoryGridVisual spawns items
            GameObject tempVisualGO = new GameObject($"DragVisual_{itemDef.ItemName}");
            tempVisualGO.transform.SetParent(tempParent, false);
            
            // Add RectTransform with bottom-left pivot (like grid items)
            RectTransform tempRT = tempVisualGO.AddComponent<RectTransform>();
            tempRT.anchorMin = Vector2.zero;
            tempRT.anchorMax = Vector2.zero;
            tempRT.pivot = new Vector2(0, 0); // Bottom-left pivot
            
            Debug.Log("[EquipmentSlotDragSource] Created temp visual GameObject");
            
            // Add visual component
            tempVisual = tempVisualGO.AddComponent<InventoryItemVisual>();
            
            // Initialize with proper parameters
            tempVisual.Initialize(draggedItem, itemDef, 64f, null);
            Debug.Log("[EquipmentSlotDragSource] Initialized visual");
            
            // Position it at the equipment slot initially
            Vector3 slotWorldPos = equipmentSlot.GetRectTransform().position;
            tempVisualGO.transform.position = slotWorldPos;
            Debug.Log($"[EquipmentSlotDragSource] Positioned visual at {slotWorldPos}");
            
            // Now trigger the drag handler
            dragHandler.OnItemBeginDrag(draggedItem.InstanceID);
            Debug.Log("[EquipmentSlotDragSource] Called dragHandler.OnItemBeginDrag()");
            
            isDragging = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Debug.Log("[EquipmentSlotDragSource] OnEndDrag triggered!");
            
            if (!isDragging || dragHandler == null || draggedItem == null)
            {
                Debug.Log($"[EquipmentSlotDragSource] OnEndDrag early exit - isDragging: {isDragging}, dragHandler null: {dragHandler == null}, draggedItem null: {draggedItem == null}");
                return;
            }

            Log($"End drag: {draggedItem.ItemDefinition.ItemName}");

            // Let drag handler handle the drop
            dragHandler.OnItemEndDrag(draggedItem.InstanceID);

            // Check if item was successfully placed
            // If temp visual still exists, drop failed
            bool dropFailed = (tempVisual != null && tempVisual.gameObject != null);

            if (dropFailed)
            {
                // Re-equip to original slot (preserves item instance data)
                Log($"Drop failed - re-equipping {draggedItem.ItemDefinition.ItemName}");
                
                // Re-equip using the original item definition
                equipmentSlot.TryEquipItem(draggedItem.ItemDefinition as InventoryItemSO);
                
                // Destroy temp visual
                if (tempVisual != null && tempVisual.gameObject != null)
                {
                    Destroy(tempVisual.gameObject);
                }
            }

            // Cleanup
            isDragging = false;
            draggedItem = null;
            tempVisual = null;
        }

        private void OnDestroy()
        {
            // Cleanup temp parent
            if (tempParent != null)
            {
                Destroy(tempParent.gameObject);
            }
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[EquipmentSlotDragSource] {message}");
            }
        }
    }
}
