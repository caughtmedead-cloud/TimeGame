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

            // Create a temporary parent for drag visuals (sibling to the slot)
            GameObject tempParentGO = new GameObject("EquipmentDragContainer");
            tempParentGO.transform.SetParent(equipmentSlot.transform.parent, false);
            tempParent = tempParentGO.transform;
            
            // Add RectTransform
            RectTransform rt = tempParentGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (equipmentSlot == null || dragHandler == null || !equipmentSlot.IsOccupied)
            {
                return;
            }

            // Get the equipped PlacedItem (this preserves durability, etc.)
            // NOTE: Equipment slots need to be updated to store PlacedItem instead of just InventoryItemSO
            InventoryItemSO itemDef = equipmentSlot.EquippedItem;
            System.Guid equippedID = equipmentSlot.GetEquippedItemID();
            
            // Create PlacedItem if equipment slot doesn't have one
            // IMPORTANT: This should ideally come from the equipment slot directly to preserve instance data
            draggedItem = new PlacedItem(
                equippedID,
                itemDef,
                Vector2Int.zero, // Equipment slots don't have grid positions
                GridDirection.Down // Equipment items don't rotate in slots
            );
            
            Log($"Begin drag: {itemDef.ItemName} (ID: {equippedID})");

            // Unequip from slot (this hides the icon)
            equipmentSlot.UnequipItem();

            // Create temporary visual similar to how InventoryGridVisual spawns items
            GameObject tempVisualGO = new GameObject($"DragVisual_{itemDef.ItemName}");
            tempVisualGO.transform.SetParent(tempParent, false);
            
            // Add RectTransform with bottom-left pivot (like grid items)
            RectTransform tempRT = tempVisualGO.AddComponent<RectTransform>();
            tempRT.anchorMin = Vector2.zero;
            tempRT.anchorMax = Vector2.zero;
            tempRT.pivot = new Vector2(0, 0); // Bottom-left pivot
            
            // Add visual component
            tempVisual = tempVisualGO.AddComponent<InventoryItemVisual>();
            
            // Initialize with proper parameters
            // Pass null for gridVisual since equipment slots don't have grids
            tempVisual.Initialize(draggedItem, itemDef, 64f, null);
            
            // Position it at the equipment slot initially
            Vector3 slotWorldPos = equipmentSlot.GetRectTransform().position;
            tempVisualGO.transform.position = slotWorldPos;
            
            // Now trigger the drag handler
            dragHandler.OnItemBeginDrag(draggedItem.InstanceID);
            
            isDragging = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || dragHandler == null || draggedItem == null)
            {
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
                // TODO: When equipment slots are updated to store PlacedItem, pass the full item here
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
