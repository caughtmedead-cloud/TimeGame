using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Drag source for equipment slots.
    /// Creates a temporary grid-sized visual for dragging.
    /// NO temp grids - just creates visual from item data.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class EquipmentSlotDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private EquipmentSlot equipmentSlot;
        private InventoryDragHandler dragHandler;
        private RectTransform canvasRoot;
        
        // Drag state
        private InventoryItemVisual dragVisual;
        private InventoryItemSO draggedItem;
        private PlacedItem draggedPlacedItem;
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        private void Awake()
        {
            equipmentSlot = GetComponentInParent<EquipmentSlot>();
            if (equipmentSlot == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] No EquipmentSlot found in parent!", this);
            }
            
            dragHandler = FindObjectOfType<InventoryDragHandler>();
            if (dragHandler == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] No InventoryDragHandler found in scene!", this);
            }
            
            // Find canvas root
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvasRoot = canvas.GetComponent<RectTransform>();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (equipmentSlot == null || dragHandler == null || !equipmentSlot.IsOccupied || canvasRoot == null)
            {
                return;
            }

            draggedItem = equipmentSlot.EquippedItem;
            Log($"Beginning drag of {draggedItem.ItemName}");

            // Get the full PlacedItem data (includes container inventory!)
            draggedPlacedItem = equipmentSlot.GetEquippedPlacedItem();

            if (draggedPlacedItem == null)
            {
                // Fallback: Create PlacedItem data if slot doesn't have one stored (shouldn't happen)
                Debug.LogWarning("[EquipmentSlotDragSource] No PlacedItem found in equipment slot - creating new one (container data will be lost!)");
                draggedPlacedItem = new PlacedItem(
                    equipmentSlot.GetEquippedItemID(),
                    draggedItem,
                    Vector2Int.zero,
                    GridDirection.Down
                );
            }

            // Unequip from slot (this hides the slot visual)
            equipmentSlot.UnequipItem();
            
            // Create grid-sized drag visual
            dragVisual = CreateDragVisual(draggedItem, draggedPlacedItem);
            
            // Calculate click offset in canvas space
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot,
                mouseScreenPos,
                null,
                out Vector2 mouseCanvasPos
            );
            
            RectTransform visualRT = dragVisual.GetComponent<RectTransform>();
            Vector2 visualCanvasPos = visualRT.anchoredPosition;
            Vector2 clickOffset = mouseCanvasPos - visualCanvasPos;
            
            Log($"Click offset: {clickOffset}");
            Log($"Mouse canvas pos: {mouseCanvasPos}, Visual canvas pos: {visualCanvasPos}");
            
            // Start drag using equipment slot as source
            dragHandler.StartDragWithExistingVisual(
                dragVisual,
                draggedPlacedItem,
                equipmentSlot,
                clickOffset
            );
            
            Log("Started drag with grid-sized visual");
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Drag handler handles everything
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragHandler != null && draggedPlacedItem != null)
            {
                // CRITICAL: Tell drag handler the drag ended so it can complete drop/return
                dragHandler.OnItemEndDrag(draggedPlacedItem.InstanceID);
                Log("Notified drag handler of end");
            }
            
            // Clear local state
            dragVisual = null;
            draggedItem = null;
            draggedPlacedItem = null;
            
            Log("Drag ended");
        }

        /// <summary>
        /// Create a temporary grid-sized visual for dragging.
        /// Visual starts at equipment slot position, then drag handler moves it.
        /// </summary>
        private InventoryItemVisual CreateDragVisual(InventoryItemSO itemDef, PlacedItem placedItem)
        {
            // Create GameObject
            GameObject visualObj = new GameObject($"Drag_{itemDef.ItemName}");
            
            // Add RectTransform FIRST
            RectTransform rt = visualObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0, 0);
            
            // Parent to equipment slot first, centered
            visualObj.transform.SetParent(equipmentSlot.transform, false);
            rt.anchoredPosition = Vector2.zero; // Center on equipment slot
            
            // Now parent to canvas, maintaining world position
            // This is the SAME technique grid drags use!
            visualObj.transform.SetParent(canvasRoot, worldPositionStays: true);
            
            // DON'T add Image here - InventoryItemVisual.Initialize() will create
            // a visual child with the Image! Adding one here creates duplicates!
            
            // Add CanvasGroup
            CanvasGroup cg = visualObj.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; // Don't block raycasts during drag
            
            // Add InventoryItemVisual component
            InventoryItemVisual visual = visualObj.AddComponent<InventoryItemVisual>();
            
            // Initialize with grid size (64px cells)
            // This will create the visual child with the Image component!
            float cellSize = 64f;
            visual.Initialize(placedItem, itemDef, cellSize, null); // null = no grid
            
            Log($"Created drag visual: {itemDef.Width}x{itemDef.Height} at {cellSize}px cells");
            
            return visual;
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
