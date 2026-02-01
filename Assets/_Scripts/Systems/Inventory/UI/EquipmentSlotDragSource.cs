using UnityEngine;
using UnityEngine.EventSystems;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Makes equipment slot items draggable.
    /// Attach this to the itemIconImage GameObject of an EquipmentSlot.
    /// Handles unequipping on drag start and re-equipping on drag cancel.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class EquipmentSlotDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private EquipmentSlot equipmentSlot;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private InventoryDragHandler dragHandler;
        
        // Drag state
        private InventoryItemSO draggedItem;
        private bool isDragging = false;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            
            // Find parent equipment slot
            equipmentSlot = GetComponentInParent<EquipmentSlot>();
            if (equipmentSlot == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] No EquipmentSlot found in parent!", this);
            }
            
            // Find drag handler
            dragHandler = GetComponentInParent<InventoryDragHandler>();
            if (dragHandler == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] No InventoryDragHandler found in parent!", this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (equipmentSlot == null || dragHandler == null || !equipmentSlot.IsOccupied)
            {
                return;
            }

            // Get the equipped item
            draggedItem = equipmentSlot.EquippedItem;
            
            Log($"Begin drag: {draggedItem.ItemName} from {equipmentSlot.GetDisplayName()}");

            // Unequip from slot (removes visual)
            equipmentSlot.UnequipItem();

            // Start drag through handler
            // Equipment items don't rotate, so always use Down direction
            dragHandler.BeginDrag(draggedItem, GridDirection.Down, equipmentSlot);
            
            isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || dragHandler == null)
            {
                return;
            }

            // Update drag position
            dragHandler.UpdateDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || dragHandler == null)
            {
                return;
            }

            Log($"End drag: {draggedItem.ItemName}");

            // Try to drop item
            bool dropped = dragHandler.EndDrag();

            if (!dropped)
            {
                // Drop failed - re-equip to original slot
                Log($"Drop failed - re-equipping {draggedItem.ItemName} to {equipmentSlot.GetDisplayName()}");
                equipmentSlot.TryEquipItem(draggedItem);
            }
            else
            {
                Log($"Successfully dropped {draggedItem.ItemName}");
            }

            // Clear drag state
            isDragging = false;
            draggedItem = null;
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
