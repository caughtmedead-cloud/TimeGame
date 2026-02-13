using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Makes equipment slot items draggable using the standard grid drag system.
    /// Creates a temporary 1x1 grid, places the item in it, then triggers normal grid drag.
    /// This ensures equipment drags work EXACTLY like inventory drags.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class EquipmentSlotDragSource : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
    {
        private EquipmentSlot equipmentSlot;
        private InventoryDragHandler dragHandler;
        
        // Temporary grid for drag operation
        private GameObject tempGridObject;
        private InventoryGridVisual tempGrid;
        private System.Guid draggedItemID;
        
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
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (equipmentSlot == null || dragHandler == null || !equipmentSlot.IsOccupied)
            {
                return;
            }

            InventoryItemSO itemDef = equipmentSlot.EquippedItem;
            Log($"Beginning drag of {itemDef.ItemName}");
            
            // Unequip from slot
            draggedItemID = equipmentSlot.GetEquippedItemID();
            equipmentSlot.UnequipItem();
            
            // Create temporary grid (invisible, just for drag logic)
            CreateTempGrid(itemDef);
            
            // Place item in temp grid
            tempGrid.InventorySystem.TryAddItem(itemDef, Vector2Int.zero, GridDirection.Down, out PlacedItem placed);
            
            // Register temp grid as drop target
            dragHandler.RegisterDropTarget(tempGrid);
            
            // Trigger normal grid drag
            dragHandler.OnItemBeginDrag(draggedItemID);
            
            Log("Triggered standard grid drag");
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Drag handler handles everything
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (tempGrid != null)
            {
                // Unregister and destroy temp grid
                dragHandler.UnregisterDropTarget(tempGrid);
                Destroy(tempGridObject);
                tempGrid = null;
                tempGridObject = null;
                
                Log("Cleaned up temp grid");
            }
        }

        private void CreateTempGrid(InventoryItemSO itemDef)
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
            tempGrid = tempGridObject.AddComponent<InventoryGridVisual>();
            
            // Initialize with item's size (using dummy values for position/weight)
            tempGrid.Initialize(
                new InventorySystem(
                    width: itemDef.Width, 
                    height: itemDef.Height,
                    cellSize: 64f,
                    anchorPosition: Vector3.zero,
                    maxWeight: 999f
                ),
                cellSize: 64f,
                maxWeight: null
            );
            
            // Make invisible
            CanvasGroup cg = tempGridObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            
            Log($"Created temp grid: {itemDef.Width}x{itemDef.Height}");
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
