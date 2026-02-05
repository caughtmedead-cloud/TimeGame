using UnityEngine;
using UnityEngine.EventSystems;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Makes equipment slot items draggable using Code Monkey's pattern.
    /// Attach this to the itemIconImage GameObject of an EquipmentSlot.
    /// 
    /// Equipment slots don't have grids, so we:
    /// 1. Create a temporary visual when dragging starts
    /// 2. Use InventoryDragHandler to manage the drag (same lerp movement!)
    /// 3. Clean up when drop succeeds/fails
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class EquipmentSlotDragSource : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        private EquipmentSlot equipmentSlot;
        private InventoryDragHandler dragHandler;
        
        // Drag state
        private InventoryItemSO draggedItemDef;
        private System.Guid tempInstanceID;
        private InventoryItemVisual tempVisual;
        private bool isDragging = false;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        private void Awake()
        {
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
            tempInstanceID = System.Guid.NewGuid();
            
            Log($"Begin drag: {draggedItemDef.ItemName} from {equipmentSlot.GetDisplayName()}");

            // Create a temporary PlacedItem
            PlacedItem tempItem = new PlacedItem(
                tempInstanceID,
                draggedItemDef,
                Vector2Int.zero,
                GridDirection.Down
            );

            // Unequip from slot (this hides the icon)
            equipmentSlot.UnequipItem();

            // Create a temporary visual GameObject under this transform
            GameObject tempVisualGO = new GameObject($"TempDragVisual_{draggedItemDef.ItemName}");
            tempVisualGO.transform.SetParent(equipmentSlot.transform, false);
            
            // Add RectTransform
            RectTransform tempRT = tempVisualGO.AddComponent<RectTransform>();
            tempRT.anchorMin = Vector2.zero;
            tempRT.anchorMax = Vector2.zero;
            tempRT.pivot = new Vector2(0, 0); // Bottom-left pivot like grid items
            tempRT.sizeDelta = new Vector2(64f, 64f); // Default size
            
            // Add visual component
            tempVisual = tempVisualGO.AddComponent<InventoryItemVisual>();
            tempVisual.Initialize(tempItem);
            
            // Now trigger the drag handler
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

            // Let drag handler handle the drop
            dragHandler.OnItemEndDrag(tempInstanceID);

            // Check if item was successfully placed
            // If temp visual still exists, drop failed
            bool dropFailed = (tempVisual != null && tempVisual.gameObject != null);

            if (dropFailed)
            {
                // Re-equip to original slot
                Log($"Drop failed - re-equipping {draggedItemDef.ItemName}");
                equipmentSlot.TryEquipItem(draggedItemDef);
                
                // Destroy temp visual
                if (tempVisual != null && tempVisual.gameObject != null)
                {
                    Destroy(tempVisual.gameObject);
                }
            }

            // Cleanup
            isDragging = false;
            draggedItemDef = null;
            tempVisual = null;
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
