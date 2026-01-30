using UnityEngine;
using UnityEngine.InputSystem;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Handles drag-drop operations for inventory items using event-driven pattern.
    /// Works with InventoryItemDragDrop components on item visuals.
    /// Based on CodeMonkey's drag-drop system architecture.
    /// </summary>
    public class InventoryDragHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryGridVisual gridVisual;
        [SerializeField] private InventoryItemGhost ghost;

        [Header("Input Settings")]
        [SerializeField] private UnityEngine.InputSystem.Key rotateKey = UnityEngine.InputSystem.Key.R;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        // Drag state
        private bool isDragging = false;
        private PlacedItem draggedItem;
        private Vector2Int originalPosition;
        private GridDirection originalRotation;
        private GridDirection currentRotation;
        private InventorySystem inventorySystem;

        // Mouse offset tracking (like CodeMonkey)
        private Vector2Int mouseDragGridPositionOffset;

        /// <summary>
        /// Initialize with inventory system reference.
        /// </summary>
        public void Initialize(InventorySystem inventory)
        {
            inventorySystem = inventory;
            
            if (ghost != null)
            {
                ghost.Hide();
            }
            
            Log("Drag handler initialized");
        }

        private void Update()
        {
            if (!isDragging) return;

            // Handle rotation input
            if (Keyboard.current != null && Keyboard.current[rotateKey].wasPressedThisFrame)
            {
                RotateDraggedItem();
            }

            // Update ghost position to follow mouse
            UpdateGhostPosition();
        }

        #region Event Callbacks (called by InventoryItemDragDrop)

        /// <summary>
        /// Called when user begins dragging an item.
        /// CRITICAL: Don't remove item from inventory yet! (CodeMonkey pattern)
        /// </summary>
        public void OnItemBeginDrag(System.Guid itemInstanceID)
        {
            if (inventorySystem == null)
            {
                Debug.LogWarning("[InventoryDragHandler] InventorySystem is null!");
                return;
            }

            // Get the item being dragged
            draggedItem = inventorySystem.GetItemByID(itemInstanceID);
            if (draggedItem == null)
            {
                Debug.LogWarning($"[InventoryDragHandler] Could not find item {itemInstanceID}");
                return;
            }

            // Store original state
            originalPosition = draggedItem.AnchorPosition;
            originalRotation = draggedItem.Rotation;
            currentRotation = draggedItem.Rotation;

            // Calculate mouse offset (where on the item did the user click?)
            Vector2 mouseLocalPos = GetMouseLocalPosition();
            Vector2Int mouseGridPos = gridVisual.LocalPositionToGridPosition(mouseLocalPos);
            mouseDragGridPositionOffset = mouseGridPos - originalPosition;

            Log($"Started dragging {draggedItem.ItemDefinition.name} from {originalPosition}");

            // DON'T REMOVE YET! Item stays in inventory during drag (like CodeMonkey)
            // The visual becomes semi-transparent via InventoryItemDragDrop component

            // Show ghost
            InventoryItemSO itemDef = draggedItem.ItemDefinition as InventoryItemSO;
            ghost.Initialize(itemDef, currentRotation, gridVisual.CellSize);
            ghost.Show();

            isDragging = true;

            // Update ghost position immediately
            UpdateGhostPosition();
        }

        /// <summary>
        /// Called when user stops dragging an item.
        /// NOW we remove from inventory and try to place (CodeMonkey pattern)
        /// </summary>
        public void OnItemEndDrag(System.Guid itemInstanceID)
        {
            if (!isDragging)
            {
                Log("OnItemEndDrag called but not dragging!");
                return;
            }

            Log("OnItemEndDrag called - processing drop");

            // Calculate target position
            Vector2 mouseLocalPos = GetMouseLocalPosition();
            Vector2Int targetGridPos = gridVisual.LocalPositionToGridPosition(mouseLocalPos);
            targetGridPos -= mouseDragGridPositionOffset; // Apply offset

            // NOW remove from current position (like CodeMonkey does in StoppedDragging)
            inventorySystem.RemoveItem(itemInstanceID);
            Log($"Removed item from {originalPosition}");

            // Try to place at new position
            bool success = inventorySystem.TryAddItem(
                ghost.CurrentItem,
                targetGridPos,
                currentRotation,
                out PlacedItem placed
            );

            if (success)
            {
                Log($"✓ Dropped {ghost.CurrentItem.name} at {targetGridPos} facing {currentRotation}");
            }
            else
            {
                // Failed - return to original position (like CodeMonkey)
                Log($"✗ Drop failed, returning to {originalPosition}");
                inventorySystem.TryAddItem(
                    ghost.CurrentItem,
                    originalPosition,
                    originalRotation,
                    out _
                );
            }

            // Cleanup
            EndDrag();
        }

        #endregion

        #region Drag Update Logic

        /// <summary>
        /// Update ghost position to follow mouse.
        /// </summary>
        private void UpdateGhostPosition()
        {
            // Get mouse position in grid local space
            Vector2 mouseLocalPos = GetMouseLocalPosition();
            Vector2Int targetGridPos = gridVisual.LocalPositionToGridPosition(mouseLocalPos);
            targetGridPos -= mouseDragGridPositionOffset; // Apply offset

            // Snap to grid
            Vector2 snappedLocalPos = gridVisual.GridPositionToLocalPosition(targetGridPos);
            ghost.SetPosition(snappedLocalPos);

            // Validate placement (checking if we COULD place here)
            // Item is still in original position, so we need to temporarily ignore it
            bool canPlace = inventorySystem.CanAddItem(
                ghost.CurrentItem,
                targetGridPos,
                currentRotation
            );

            ghost.SetValid(canPlace);
        }

        /// <summary>
        /// Rotate the dragged item.
        /// </summary>
        private void RotateDraggedItem()
        {
            InventoryItemSO itemDef = draggedItem.ItemDefinition as InventoryItemSO;
            if (itemDef == null || !itemDef.CanRotate) return;

            // Get next rotation
            currentRotation = itemDef.GetNextRotation(currentRotation);

            // Update ghost
            ghost.Rotate();

            Log($"Rotated to {currentRotation}");

            // Re-validate
            UpdateGhostPosition();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get mouse position in grid local space.
        /// </summary>
        private Vector2 GetMouseLocalPosition()
        {
            if (Mouse.current == null) return Vector2.zero;

            RectTransform gridRect = gridVisual.GetComponent<RectTransform>();
            
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridRect,
                Mouse.current.position.value,
                null, // Assuming ScreenSpaceOverlay canvas
                out Vector2 localPoint
            );

            return localPoint;
        }

        /// <summary>
        /// End drag operation and cleanup.
        /// </summary>
        private void EndDrag()
        {
            ghost.Hide();
            isDragging = false;
            draggedItem = null;
            Log("Drag ended, cleanup complete");
        }

        /// <summary>
        /// Debug logging.
        /// </summary>
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[InventoryDragHandler] {message}");
            }
        }

        #endregion
    }
}
