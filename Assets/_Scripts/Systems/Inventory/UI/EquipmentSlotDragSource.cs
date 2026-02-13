using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Makes equipment slot items draggable using StartDragWithExistingVisual.
    /// Creates a temporary grid, places item, gets the visual, then starts drag.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class EquipmentSlotDragSource : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
    {
        private EquipmentSlot equipmentSlot;
        private InventoryDragHandler dragHandler;
        private RectTransform canvasRoot;
        
        // Temporary grid for drag operation
        private GameObject tempGridObject;
        private InventoryGridVisual tempGrid;
        
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

            InventoryItemSO itemDef = equipmentSlot.EquippedItem;
            Log($"Beginning drag of {itemDef.ItemName}");
            
            // Unequip from slot
            equipmentSlot.UnequipItem();
            
            // Create temporary grid and add item
            tempGrid = CreateTempGridAndAddItem(itemDef, out PlacedItem placedItem);
            
            // Get the visual that was spawned
            InventoryItemVisual visual = tempGrid.transform.GetComponentInChildren<InventoryItemVisual>();
            if (visual == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] Could not find visual in temp grid!");
                CleanupTempGrid();
                return;
            }
            
            // Calculate click offset in canvas space
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot,
                mouseScreenPos,
                null,
                out Vector2 mouseCanvasPos
            );
            
            // Parent visual to canvas to get its position in canvas space
            RectTransform visualRT = visual.GetComponent<RectTransform>();
            visualRT.SetParent(canvasRoot, worldPositionStays: true);
            
            Vector2 visualCanvasPos = visualRT.anchoredPosition;
            Vector2 clickOffset = mouseCanvasPos - visualCanvasPos;
            
            Log($"Click offset: {clickOffset}");
            
            // Register temp grid as drop target
            dragHandler.RegisterDropTarget(tempGrid);
            
            // Start drag using the correct method for equipment slots
            dragHandler.StartDragWithExistingVisual(
                visual, 
                placedItem, 
                equipmentSlot,  // Equipment slot is the source
                clickOffset
            );
            
            Log("Started drag with existing visual");
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Drag handler handles everything
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            CleanupTempGrid();
        }

        private InventoryGridVisual CreateTempGridAndAddItem(InventoryItemSO itemDef, out PlacedItem placedItem)
        {
            // Create invisible grid GameObject
            tempGridObject = new GameObject("TempEquipmentDragGrid");
            tempGridObject.transform.SetParent(transform.root, false);
            
            // Add RectTransform
            RectTransform rt = tempGridObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            
            // Add grid visual component
            InventoryGridVisual grid = tempGridObject.AddComponent<InventoryGridVisual>();
            
            // Initialize with item-sized inventory system
            grid.Initialize(
                new InventorySystem(
                    width: itemDef.Width, 
                    height: itemDef.Height,
                    cellSize: 64f,
                    origin: Vector3.zero,
                    maxWeight: 999f
                )
            );
            
            // Make invisible
            CanvasGroup cg = tempGridObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            
            // Add item to grid (this creates the PlacedItem and spawns visual)
            if (!grid.InventorySystem.TryAddItem(itemDef, Vector2Int.zero, GridDirection.Down, out placedItem))
            {
                Debug.LogError("[EquipmentSlotDragSource] Failed to add item to temp grid!");
            }
            
            Log($"Created temp grid: {itemDef.Width}x{itemDef.Height}");
            
            return grid;
        }

        private void CleanupTempGrid()
        {
            if (tempGrid != null)
            {
                dragHandler.UnregisterDropTarget(tempGrid);
                Destroy(tempGridObject);
                tempGrid = null;
                tempGridObject = null;
                
                Log("Cleaned up temp grid");
            }
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
