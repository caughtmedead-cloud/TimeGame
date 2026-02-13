using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Makes equipment slot items draggable by creating a temp visual
    /// and handing it off to the InventoryDragHandler.
    /// 
    /// Equipment slots store items at any size but display them uniformly.
    /// When dragging OUT, the item visual is created at its TRUE grid size
    /// so it can be placed properly in inventory grids.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class EquipmentSlotDragSource : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
    {
        private EquipmentSlot equipmentSlot;
        private InventoryDragHandler dragHandler;
        private Canvas rootCanvas;
        
        // Drag state
        private PlacedItem draggedItem;
        private InventoryItemVisual tempVisual;
        private bool isDragging = false;

        [Header("Visual Settings")]
        [Tooltip("Cell size for grid-based inventory (should match your inventory grid cell size)")]
        [SerializeField] private float gridCellSize = 64f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        private void Awake()
        {
            Log("Awake called");
            
            // Find parent equipment slot
            equipmentSlot = GetComponentInParent<EquipmentSlot>();
            if (equipmentSlot == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] No EquipmentSlot found in parent!", this);
            }
            
            // Find drag handler
            dragHandler = FindObjectOfType<InventoryDragHandler>();
            if (dragHandler == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] No InventoryDragHandler found in scene!", this);
            }

            // Find root canvas
            rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (rootCanvas == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] Cannot find root canvas!", this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Log($"OnBeginDrag triggered! Button: {eventData.button}");
            
            if (equipmentSlot == null || dragHandler == null || rootCanvas == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] Missing required references!");
                return;
            }
            
            if (!equipmentSlot.IsOccupied)
            {
                Log("Equipment slot is not occupied");
                return;
            }

            // Get equipped item data
            InventoryItemSO itemDef = equipmentSlot.EquippedItem;
            System.Guid equippedID = equipmentSlot.GetEquippedItemID();
            
            Log($"Beginning drag of {itemDef.ItemName} (ID: {equippedID})");
            Log($"Item grid size: {itemDef.Width}x{itemDef.Height}");
            
            // Create PlacedItem data
            draggedItem = new PlacedItem(
                equippedID,
                itemDef,
                Vector2Int.zero,
                GridDirection.Down
            );

            // Unequip from slot (this hides the icon in the slot)
            equipmentSlot.UnequipItem();
            Log("Unequipped item from slot");

            // Create temporary visual at item's TRUE grid size
            CreateTempVisual(itemDef);

            // Calculate click offset in canvas space
            // Visual is already parented to canvas, so both mouse and visual are in the same coordinate system!
            RectTransform canvasRT = rootCanvas.GetComponent<RectTransform>();
            RectTransform visualRT = tempVisual.GetComponent<RectTransform>();
            
            // Convert mouse screen position to canvas anchored position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT,
                eventData.position,
                null,
                out Vector2 mouseCanvasPos
            );
            
            // Visual's position in canvas anchored position space (same coordinate system as mouse!)
            Vector2 visualCanvasPos = visualRT.anchoredPosition;
            
            // Calculate offset: how far is the click from the visual's bottom-left corner?
            Vector2 clickOffset = mouseCanvasPos - visualCanvasPos;
            
            Log($"Mouse canvas pos: {mouseCanvasPos}, Visual canvas pos: {visualCanvasPos}, Click offset: {clickOffset}");

            // Hand off to drag handler
            dragHandler.StartDragWithExistingVisual(tempVisual, draggedItem, equipmentSlot, clickOffset);
            Log("Handed off to drag handler");
            
            isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Drag handler handles all movement
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Log("OnEndDrag triggered");
            
            if (!isDragging || dragHandler == null || draggedItem == null)
            {
                Log($"OnEndDrag early exit - isDragging: {isDragging}");
                return;
            }

            // Let drag handler handle the drop
            dragHandler.OnItemEndDrag(draggedItem.InstanceID);
            Log("Called dragHandler.OnItemEndDrag()");

            // Cleanup
            isDragging = false;
            draggedItem = null;
            tempVisual = null; // Drag handler owns cleanup now
        }

        private void CreateTempVisual(InventoryItemSO itemDef)
        {
            RectTransform slotRT = equipmentSlot.GetRectTransform();
            RectTransform canvasRT = rootCanvas.GetComponent<RectTransform>();
            
            // Create visual GameObject
            GameObject tempVisualGO = new GameObject($"DragVisual_{itemDef.ItemName}");
            
            // Add RectTransform with bottom-left pivot (matches grid items)
            RectTransform tempRT = tempVisualGO.AddComponent<RectTransform>();
            tempRT.anchorMin = Vector2.zero;
            tempRT.anchorMax = Vector2.zero;
            tempRT.pivot = new Vector2(0, 0); // Bottom-left pivot
            
            // Parent to canvas with worldPositionStays TRUE to maintain world position
            tempRT.SetParent(canvasRT, worldPositionStays: true);
            
            // Get the slot's center in world space
            Vector3 slotWorldCenter = slotRT.TransformPoint(slotRT.rect.center);
            
            // Calculate item's visual size at grid scale
            Vector2 itemVisualSize = new Vector2(
                itemDef.Width * gridCellSize,
                itemDef.Height * gridCellSize
            );
            
            // CRITICAL: Set the RectTransform's actual size!
            tempRT.sizeDelta = itemVisualSize;
            
            // Set world position to slot center
            tempRT.position = slotWorldCenter;
            
            // Now offset by half size to center it (since pivot is bottom-left)
            // Work in anchored position space after setting world position
            tempRT.anchoredPosition -= itemVisualSize * 0.5f;
            
            Log($"Slot world center: {slotWorldCenter}, Visual anchored pos: {tempRT.anchoredPosition}, Size: {itemVisualSize}");
            
            // Add visual component with item's TRUE grid size
            tempVisual = tempVisualGO.AddComponent<InventoryItemVisual>();
            tempVisual.Initialize(draggedItem, itemDef, gridCellSize, null);
            
            Log($"Initialized visual with grid cell size: {gridCellSize}");
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[EquipmentSlotDragSource:{equipmentSlot?.GetDisplayName()}] {message}");
            }
        }
    }
}
