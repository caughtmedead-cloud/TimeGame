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

            // Calculate click offset in screen space
            RectTransform canvasRT = rootCanvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT,
                eventData.position,
                null,
                out Vector2 clickLocalPos
            );

            // Get visual's current position in canvas space
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT,
                tempVisual.GetComponent<RectTransform>().position,
                null,
                out Vector2 visualLocalPos
            );

            Vector2 clickOffset = clickLocalPos - visualLocalPos;
            Log($"Click offset: {clickOffset}");

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
            tempRT.position = equipmentSlot.GetRectTransform().position;
            
            Log("Created temp visual GameObject");
            
            // Add visual component
            tempVisual = tempVisualGO.AddComponent<InventoryItemVisual>();
            tempVisual.Initialize(draggedItem, itemDef, cellSize, null);
            
            Log($"Initialized visual at position {tempRT.position}");
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
