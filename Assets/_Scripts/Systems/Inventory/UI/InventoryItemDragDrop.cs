using UnityEngine;
using UnityEngine.EventSystems;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Drag-drop component that goes on each item visual.
    /// Uses Unity's Event System for robust drag detection.
    /// Also handles right-click for context menu.
    /// Based on CodeMonkey's implementation pattern.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class InventoryItemDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private InventoryGridVisual gridVisual;
        
        // Item identity
        private System.Guid itemInstanceID;
        
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>
        /// Setup this drag-drop component with references.
        /// </summary>
        public void Setup(InventoryGridVisual gridVisual, System.Guid itemInstanceID)
        {
            this.gridVisual = gridVisual;
            this.itemInstanceID = itemInstanceID;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (gridVisual == null)
            {
                Debug.LogWarning("[InventoryItemDragDrop] GridVisual reference is null!");
                return;
            }

            // Make semi-transparent during drag
            canvasGroup.alpha = 0.7f;
            canvasGroup.blocksRaycasts = false; // Don't block raycasts

            // Notify the drag handler system
            InventoryDragHandler dragHandler = gridVisual.GetComponentInParent<InventoryDragHandler>();
            if (dragHandler != null)
            {
                dragHandler.OnItemBeginDrag(itemInstanceID);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Drag logic handled by InventoryDragHandler
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Restore opacity
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            // Notify the drag handler system
            InventoryDragHandler dragHandler = gridVisual.GetComponentInParent<InventoryDragHandler>();
            if (dragHandler != null)
            {
                dragHandler.OnItemEndDrag(itemInstanceID);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Handle right-click for context menu
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (gridVisual == null)
                {
                    Debug.LogWarning("[InventoryItemDragDrop] GridVisual reference is null!");
                    return;
                }

                // Get the placed item from the inventory system
                PlacedItem item = gridVisual.InventorySystem.GetItemByID(itemInstanceID);
                if (item != null)
                {
                    // Show context menu at click position
                    InventoryContextMenu.ShowMenu(eventData.position, item, gridVisual);
                }
            }
        }
    }
}
