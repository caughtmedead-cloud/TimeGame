using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Drag-drop handler copying Code Monkey's approach: move actual items with lerp.
    /// Adapted to work with OUR inventory system APIs.
    /// </summary>
    public class InventoryDragHandler : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        // Drop target tracking
        private List<IInventoryDropTarget> registeredTargets = new List<IInventoryDropTarget>();
        
        // Drag state (Code Monkey's pattern)
        private IInventoryDropTarget draggingInventoryTetris;  // Which grid we're dragging from
        private InventoryItemVisual draggingPlacedObject;      // The actual visual we're dragging
        private Vector2Int mouseDragGridPositionOffset;        // Grid offset
        private Vector2 mouseDragAnchoredPositionOffset;       // Pixel offset
        private GridDirection dir;                             // Current rotation
        
        // Original state for returning item if drop fails
        private IInventoryDropTarget originalInventory;
        private Vector2Int originalGridPosition;
        private GridDirection originalDir;
        private PlacedItem originalPlacedItem;

        public bool IsDragging => draggingPlacedObject != null;

        #region Registration

        public void RegisterDropTarget(IInventoryDropTarget target)
        {
            if (target == null)
            {
                Debug.LogWarning("[InventoryDragHandler] Tried to register null drop target");
                return;
            }

            if (!registeredTargets.Contains(target))
            {
                registeredTargets.Add(target);
                Log($"Registered drop target: {target.GetDisplayName()}");
            }
        }

        public void UnregisterDropTarget(IInventoryDropTarget target)
        {
            if (registeredTargets.Contains(target))
            {
                registeredTargets.Remove(target);
                Log($"Unregistered drop target: {target.GetDisplayName()}");
            }
        }

        #endregion

        private void Update()
        {
            // CODE MONKEY: Handle rotation
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                if (draggingPlacedObject != null)
                {
                    InventoryItemSO itemDef = draggingPlacedObject.PlacedItem.ItemDefinition as InventoryItemSO;
                    if (itemDef != null && itemDef.CanRotate)
                    {
                        dir = itemDef.GetNextRotation(dir);
                        Log($"Rotated to {dir}");
                    }
                }
            }

            // CODE MONKEY: Move dragged item with LERP
            if (draggingPlacedObject != null && draggingInventoryTetris is InventoryGridVisual gridVisual)
            {
                // Calculate target position to move the dragged item
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    gridVisual.GetRectTransform(),
                    Mouse.current.position.ReadValue(),
                    null,
                    out Vector2 targetPosition
                );
                
                // CODE MONKEY: Apply anchored position offset
                targetPosition += new Vector2(-mouseDragAnchoredPositionOffset.x, -mouseDragAnchoredPositionOffset.y);

                // NOTE: We don't have GetRotationOffset() in our system
                // Code Monkey uses it to shift the pivot based on rotation
                // For now, skip this - our rotation system might handle it differently

                // CODE MONKEY: Snap position using division
                float cellSize = gridVisual.CellSize;
                targetPosition /= cellSize;
                targetPosition = new Vector2(Mathf.Floor(targetPosition.x), Mathf.Floor(targetPosition.y));
                targetPosition *= cellSize;

                // CODE MONKEY: LERP to target position (smooth movement!)
                RectTransform itemRT = draggingPlacedObject.GetComponent<RectTransform>();
                itemRT.anchoredPosition = Vector2.Lerp(itemRT.anchoredPosition, targetPosition, Time.deltaTime * 20f);
                
                // CODE MONKEY: LERP rotation (smooth rotation!)
                InventoryItemSO itemDef = draggingPlacedObject.PlacedItem.ItemDefinition as InventoryItemSO;
                float rotationAngle = itemDef != null ? itemDef.GetRotationAngle(dir) : 0f;
                draggingPlacedObject.transform.rotation = Quaternion.Lerp(
                    draggingPlacedObject.transform.rotation,
                    Quaternion.Euler(0, 0, -rotationAngle),
                    Time.deltaTime * 15f
                );
            }
        }

        #region Event Callbacks

        public void OnItemBeginDrag(System.Guid itemInstanceID)
        {
            // Find which grid contains this item
            IInventoryDropTarget sourceTarget = FindTargetContainingItem(itemInstanceID);
            
            if (sourceTarget == null)
            {
                Debug.LogWarning($"[InventoryDragHandler] Could not find source target for item {itemInstanceID}");
                return;
            }

            // Only support grid sources for now (equipment slots handled separately)
            InventoryGridVisual gridVisual = sourceTarget as InventoryGridVisual;
            if (gridVisual == null)
            {
                Debug.LogWarning("[InventoryDragHandler] Only grid sources supported for drag");
                return;
            }

            // Get the placed item
            PlacedItem placedItem = gridVisual.InventorySystem.GetItemByID(itemInstanceID);
            if (placedItem == null)
            {
                Debug.LogWarning($"[InventoryDragHandler] Could not find item {itemInstanceID}");
                return;
            }

            // Get the visual component
            InventoryItemVisual itemVisual = gridVisual.transform.GetComponentsInChildren<InventoryItemVisual>()
                .FirstOrDefault(v => v.PlacedItem.InstanceID == itemInstanceID);
            
            if (itemVisual == null)
            {
                Debug.LogWarning($"[InventoryDragHandler] Could not find visual for item {itemInstanceID}");
                return;
            }

            // CODE MONKEY: Save drag state
            draggingInventoryTetris = gridVisual;
            draggingPlacedObject = itemVisual;
            
            // Save original state for return if drop fails
            originalInventory = gridVisual;
            originalGridPosition = placedItem.AnchorPosition;
            originalDir = placedItem.Rotation;
            originalPlacedItem = placedItem;

            // CODE MONKEY: Hide cursor
            Cursor.visible = false;

            // CODE MONKEY: Calculate mouse position in local space
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridVisual.GetRectTransform(),
                Mouse.current.position.ReadValue(),
                null,
                out Vector2 anchoredPosition
            );
            Vector2Int mouseGridPosition = gridVisual.LocalPositionToGridPosition(anchoredPosition);

            // CODE MONKEY: Calculate grid position offset
            mouseDragGridPositionOffset = mouseGridPosition - placedItem.AnchorPosition;

            // CODE MONKEY: Calculate anchored position offset (where exactly player clicked)
            mouseDragAnchoredPositionOffset = anchoredPosition - itemVisual.GetComponent<RectTransform>().anchoredPosition;

            // CODE MONKEY: Save initial direction
            dir = placedItem.Rotation;

            // NOTE: Code Monkey applies rotation offset here
            // We don't have GetRotationOffset() so skipping for now
            // Our rotation system might handle pivot differently

            Log($"Started dragging - Grid offset: {mouseDragGridPositionOffset}, Anchor offset: {mouseDragAnchoredPositionOffset}");
        }

        public void OnItemEndDrag(System.Guid itemInstanceID)
        {
            if (draggingPlacedObject == null)
            {
                Log("OnItemEndDrag called but not dragging!");
                return;
            }

            // CODE MONKEY: Show cursor
            Cursor.visible = true;

            InventoryGridVisual fromGrid = draggingInventoryTetris as InventoryGridVisual;
            if (fromGrid == null)
            {
                Log("Source is not a grid, aborting");
                draggingPlacedObject = null;
                draggingInventoryTetris = null;
                return;
            }

            // CODE MONKEY: Remove item from current inventory
            fromGrid.InventorySystem.RemoveItem(itemInstanceID);
            Log($"Removed item from {fromGrid.GetDisplayName()}");

            IInventoryDropTarget toInventoryTetris = null;

            // CODE MONKEY: Find which inventory is under mouse
            foreach (IInventoryDropTarget inventoryTetris in registeredTargets)
            {
                if (!(inventoryTetris is InventoryGridVisual gridTarget)) continue;

                Vector3 screenPoint = Mouse.current.position.ReadValue();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    gridTarget.GetRectTransform(),
                    screenPoint,
                    null,
                    out Vector2 anchoredPosition
                );
                
                Vector2Int placedObjectOrigin = gridTarget.LocalPositionToGridPosition(anchoredPosition);
                placedObjectOrigin = placedObjectOrigin - mouseDragGridPositionOffset;

                // Check if it's a valid grid position using our InventorySystem
                InventoryItemSO itemDef = originalPlacedItem.ItemDefinition as InventoryItemSO;
                if (gridTarget.InventorySystem.CanAddItem(itemDef, placedObjectOrigin, dir))
                {
                    toInventoryTetris = gridTarget;
                    break;
                }
            }

            // CODE MONKEY: Try to place item
            if (toInventoryTetris != null && toInventoryTetris is InventoryGridVisual toGrid)
            {
                Vector3 screenPoint = Mouse.current.position.ReadValue();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    toGrid.GetRectTransform(),
                    screenPoint,
                    null,
                    out Vector2 anchoredPosition
                );
                
                Vector2Int placedObjectOrigin = toGrid.LocalPositionToGridPosition(anchoredPosition);
                placedObjectOrigin = placedObjectOrigin - mouseDragGridPositionOffset;

                InventoryItemSO itemDef = originalPlacedItem.ItemDefinition as InventoryItemSO;
                bool tryPlaceItem = toGrid.InventorySystem.TryAddItem(itemDef, placedObjectOrigin, dir, out PlacedItem placed);

                if (tryPlaceItem)
                {
                    Log($"✓ Item placed at {placedObjectOrigin}");
                }
                else
                {
                    // CODE MONKEY: Cannot drop here, return to original
                    Log("✗ Cannot drop item here, returning to original position");
                    ReturnToOriginalPosition();
                }
            }
            else
            {
                // CODE MONKEY: Not on any inventory, return to original
                Log("✗ Not on any inventory, returning to original position");
                ReturnToOriginalPosition();
            }

            // Clear drag state
            draggingPlacedObject = null;
            draggingInventoryTetris = null;
        }

        #endregion

        #region Helper Methods

        private void ReturnToOriginalPosition()
        {
            if (originalInventory is InventoryGridVisual originalGrid)
            {
                InventoryItemSO itemDef = originalPlacedItem.ItemDefinition as InventoryItemSO;
                originalGrid.InventorySystem.TryAddItem(itemDef, originalGridPosition, originalDir, out _);
            }
        }

        private IInventoryDropTarget FindTargetContainingItem(System.Guid itemID)
        {
            foreach (IInventoryDropTarget target in registeredTargets)
            {
                if (target is InventoryGridVisual gridVisual)
                {
                    if (gridVisual.InventorySystem != null && 
                        gridVisual.InventorySystem.GetItemByID(itemID) != null)
                    {
                        return target;
                    }
                }
                
                if (target is EquipmentSlot slot)
                {
                    if (slot.GetEquippedItemID() == itemID)
                    {
                        return target;
                    }
                }
            }

            return null;
        }

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
