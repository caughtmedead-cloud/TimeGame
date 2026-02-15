using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Handles drag and drop for inventory items.
    /// Enables dragging items between grids and equipment slots.
    /// Shows WhiteTile background during drag for preview.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class InventoryItemDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private InventoryGridVisual sourceGrid;
        private Guid itemInstanceID;
        private InventoryItemVisual itemVisual;
        
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Transform originalParent;
        private int originalSiblingIndex;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            itemVisual = GetComponent<InventoryItemVisual>();
        }

        /// <summary>
        /// Setup the drag-drop handler with grid and item references.
        /// </summary>
        public void Setup(InventoryGridVisual grid, Guid instanceID)
        {
            sourceGrid = grid;
            itemInstanceID = instanceID;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (sourceGrid == null) return;

            // Store original position
            originalParent = transform.parent;
            originalSiblingIndex = transform.GetSiblingIndex();

            // Make semi-transparent during drag
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false; // Allow raycasts to pass through

            // TASK 2: Enable white tile background for drag preview
            if (itemVisual != null && sourceGrid.TileSprites != null)
            {
                itemVisual.EnableDragBackground(sourceGrid.TileSprites);
            }

            // Move to top of hierarchy for rendering on top
            transform.SetParent(sourceGrid.transform.parent);
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (rectTransform == null) return;

            // Follow mouse position
            rectTransform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Restore alpha and raycasts
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // TASK 2: Disable drag background
            if (itemVisual != null)
            {
                itemVisual.DisableDragBackground();
            }

            // Try to drop on target
            bool dropped = TryDropOnTarget(eventData);

            if (!dropped)
            {
                // Return to original position
                transform.SetParent(originalParent);
                transform.SetSiblingIndex(originalSiblingIndex);
                rectTransform.anchoredPosition = Vector2.zero; // Reset position
            }
        }

        private bool TryDropOnTarget(PointerEventData eventData)
        {
            // Placeholder for actual drop logic
            // This would integrate with your existing drag-drop system
            // to handle placement on different grids/slots
            
            // For now, items return to original position
            return false;
        }
    }
}
