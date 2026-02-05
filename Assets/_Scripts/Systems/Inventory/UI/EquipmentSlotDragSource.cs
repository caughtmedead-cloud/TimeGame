using UnityEngine;
using UnityEngine.EventSystems;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Makes equipment slot items draggable using Code Monkey's pattern.
    /// Attach this to the itemIconImage GameObject of an EquipmentSlot.
    /// 
    /// Equipment slots work differently from grids:
    /// - Items spawn as temporary visuals when dragging starts
    /// - Items are created fresh at the drop target
    /// - No persistent instance IDs needed
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class EquipmentSlotDragSource : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        private EquipmentSlot equipmentSlot;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private InventoryDragHandler dragHandler;
        
        // Temporary drag state - for creating a temporary item visual
        private InventoryItemSO draggedItemDef;
        private System.Guid tempInstanceID;
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
            
            // Find drag handler (should be global singleton)
            dragHandler = FindObjectOfType<InventoryDragHandler>();
            if (dragHandler == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] No InventoryDragHandler found in scene!", this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (equipmentSlot == null || dragHandler == null || !equipmentSlot.IsOccupied)
            {
                return;
            }

            // Get the equipped item
            draggedItemDef = equipmentSlot.EquippedItem;
            
            Log($"Begin drag: {draggedItemDef.ItemName} from {equipmentSlot.GetDisplayName()}");

            // Create a temporary item in the equipment slot's "virtual grid"
            // This allows InventoryDragHandler to treat it like a grid item
            tempInstanceID = System.Guid.NewGuid();
            
            // Create a temporary PlacedItem for the drag handler
            PlacedItem tempItem = new PlacedItem(
                draggedItemDef,
                Vector2Int.zero, // Position doesn't matter for equipment slots
                GridDirection.Down, // Equipment items don't rotate in slots
                tempInstanceID
            );

            // Unequip from slot (removes visual)
            equipmentSlot.UnequipItem();

            // Create a temporary visual for dragging
            // We'll spawn it in the equipment slot's transform temporarily
            GameObject tempVisualGO = new GameObject("TempDragVisual");
            tempVisualGO.transform.SetParent(equipmentSlot.transform, false);
            
            InventoryItemVisual tempVisual = tempVisualGO.AddComponent<InventoryItemVisual>();
            tempVisual.Initialize(tempItem);
            
            // Now trigger the drag handler with this temp item
            dragHandler.OnItemBeginDrag(tempInstanceID);
            
            isDragging = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || dragHandler == null)
            {
                return;
            }

            Log($"End drag: {draggedItemDef?.ItemName}");

            // The drag handler will handle the drop logic
            // We just need to clean up if the drop failed
            dragHandler.OnItemEndDrag(tempInstanceID);

            // Check if the item was successfully placed somewhere
            // If not, re-equip it to the original slot
            bool wasPlaced = !equipmentSlot.IsOccupied; // If slot is still empty, item was placed elsewhere

            if (!wasPlaced)
            {
                // Drop failed - re-equip to original slot
                Log($"Drop failed - re-equipping {draggedItemDef.ItemName}");
                equipmentSlot.TryEquipItem(draggedItemDef);
            }

            // Cleanup
            isDragging = false;
            draggedItemDef = null;
            tempInstanceID = System.Guid.Empty;
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
