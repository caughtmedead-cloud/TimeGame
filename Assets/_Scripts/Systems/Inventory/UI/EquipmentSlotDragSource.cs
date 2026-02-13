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
    /// This creates seamless dragging from equipment slots into inventory grids.
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
        [SerializeField] private float cellSize = 64f; // Size for visual creation

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

            // Create temporary visual
            CreateTempVisual(itemDef);

            // CRITICAL FIX: Calculate click offset properly in canvas space
            RectTransform canvasRT = rootCanvas.GetComponent<RectTransform>();
            RectTransform visualRT = tempVisual.GetComponent<RectTransform>();
            
            // Convert mouse screen position to canvas local position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT,
                eventData.position,
                null,
                out Vector2 mouseCanvasPos
            );
            
            // Visual's anchored position in canvas space (it's already parented to canvas)
            Vector2 visualCanvasPos = visualRT.anchoredPosition;
            
            // Calculate offset: how far is the click from the visual's position?
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
            // Create visual GameObject
            GameObject tempVisualGO = new GameObject($"DragVisual_{itemDef.ItemName}");
            tempVisualGO.transform.SetParent(rootCanvas.transform, false);
            
            // Add RectTransform with bottom-left pivot (matches grid items)
            RectTransform tempRT = tempVisualGO.AddComponent<RectTransform>();
            tempRT.anchorMin = Vector2.zero;
            tempRT.anchorMax = Vector2.zero;
            tempRT.pivot = new Vector2(0, 0);
            
            // Position it at the equipment slot initially
            // Convert equipment slot's screen position to canvas local position
            RectTransform canvasRT = rootCanvas.GetComponent<RectTransform>();
            Vector3 slotScreenPos = equipmentSlot.GetRectTransform().position;
            
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT,
                slotScreenPos,
                null,
                out Vector2 slotCanvasPos
            );
            
            tempRT.anchoredPosition = slotCanvasPos;
            
            Log($"Created temp visual GameObject at canvas position: {slotCanvasPos}");
            
            // Add visual component
            tempVisual = tempVisualGO.AddComponent<InventoryItemVisual>();
            tempVisual.Initialize(draggedItem, itemDef, cellSize, null);
            
            Log($"Initialized visual");
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
