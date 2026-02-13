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
            
            // Create PlacedItem data (for drag handler)
            draggedPlacedItem = new PlacedItem(
                equipmentSlot.GetEquippedItemID(),
                draggedItem,
                Vector2Int.zero,
                GridDirection.Down
            );
            
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
            // Drag handler owns the visual now - it will clean up
            dragVisual = null;
            draggedItem = null;
            draggedPlacedItem = null;
            
            Log("Drag ended");
        }

        /// <summary>
        /// Create a temporary grid-sized visual for dragging.
        /// This visual is NOT part of any grid - it's standalone.
        /// </summary>
        private InventoryItemVisual CreateDragVisual(InventoryItemSO itemDef, PlacedItem placedItem)
        {
            // Create GameObject
            GameObject visualObj = new GameObject($"Drag_{itemDef.ItemName}");
            visualObj.transform.SetParent(canvasRoot, false);
            
            // Add RectTransform
            RectTransform rt = visualObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0, 0);
            
            // Position at mouse
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot,
                mouseScreenPos,
                null,
                out Vector2 mouseCanvasPos
            );
            rt.anchoredPosition = mouseCanvasPos;
            
            // Add Image for visual
            Image image = visualObj.AddComponent<Image>();
            if (itemDef.ItemIcon != null)
            {
                image.sprite = itemDef.ItemIcon;
            }
            else
            {
                image.color = GetColorForRarity(itemDef.Rarity);
            }
            
            // Add CanvasGroup
            CanvasGroup cg = visualObj.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; // Don't block raycasts during drag
            
            // Add InventoryItemVisual component
            InventoryItemVisual visual = visualObj.AddComponent<InventoryItemVisual>();
            
            // Initialize with grid size (64px cells)
            float cellSize = 64f;
            visual.Initialize(placedItem, itemDef, cellSize, null); // null = no grid
            
            Log($"Created drag visual: {itemDef.Width}x{itemDef.Height} at {cellSize}px cells");
            
            return visual;
        }

        private Color GetColorForRarity(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return new Color(0.7f, 0.7f, 0.7f);
                case ItemRarity.Uncommon: return new Color(0.3f, 0.8f, 0.3f);
                case ItemRarity.Rare: return new Color(0.3f, 0.5f, 1f);
                case ItemRarity.Epic: return new Color(0.8f, 0.3f, 0.8f);
                case ItemRarity.Legendary: return new Color(1f, 0.6f, 0f);
                default: return Color.white;
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
