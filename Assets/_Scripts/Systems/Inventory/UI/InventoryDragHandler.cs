using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Drag-drop handler adapting Code Monkey's lerp approach for our multi-grid system.
    /// Fixes:
    /// 1. Clamp item to grid boundaries (can't escape visually)
    /// 2. Account for rotation pivot offset
    /// </summary>
    public class InventoryDragHandler : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        // Drop target tracking
        private List<IInventoryDropTarget> registeredTargets = new List<IInventoryDropTarget>();
        
        // Drag state
        private InventoryGridVisual currentGrid;               // Which grid the item is currently in
        private InventoryItemVisual draggingPlacedObject;      // The actual visual we're dragging
        private Vector2Int mouseDragGridPositionOffset;        // Grid offset
        private Vector2 mouseDragAnchoredPositionOffset;       // Pixel offset
        private GridDirection dir;                             // Current rotation
        
        // Original state for returning item if drop fails
        private InventoryGridVisual originalGrid;
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
            // Handle rotation
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

            // Move dragged item with LERP
            if (draggingPlacedObject != null)
            {
                // Find which grid is under mouse
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                InventoryGridVisual targetGrid = GetGridUnderMouse(mouseScreenPos);

                // If we crossed into a different grid, reparent the item
                if (targetGrid != null && targetGrid != currentGrid)
                {
                    Log($"Crossed into {targetGrid.GetDisplayName()}");
                    draggingPlacedObject.transform.SetParent(targetGrid.GetRectTransform(), true);
                    currentGrid = targetGrid;
                }

                // Calculate target position in the CURRENT grid
                if (currentGrid != null)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        currentGrid.GetRectTransform(),
                        mouseScreenPos,
                        null,
                        out Vector2 mouseLocalPos
                    );
                    
                    // Calculate where the item will ACTUALLY BE PLACED
                    Vector2Int mouseGridPos = currentGrid.LocalPositionToGridPosition(mouseLocalPos);
                    Vector2Int placementGridPos = mouseGridPos - mouseDragGridPositionOffset;
                    
                    // FIX #1: CLAMP to grid boundaries so item can't escape visually
                    InventoryItemSO itemDef = draggingPlacedObject.PlacedItem.ItemDefinition as InventoryItemSO;
                    Vector2Int itemSize = itemDef.GetSizeForRotation(dir);
                    
                    // Clamp placement position to keep item within grid
                    placementGridPos.x = Mathf.Clamp(placementGridPos.x, 0, currentGrid.InventorySystem.Width - itemSize.x);
                    placementGridPos.y = Mathf.Clamp(placementGridPos.y, 0, currentGrid.InventorySystem.Height - itemSize.y);
                    
                    // Convert placement grid position to local position
                    Vector2 targetPosition = currentGrid.GridPositionToLocalPosition(placementGridPos);
                    
                    // FIX #2: Add rotation pivot offset to keep visual position stable during rotation
                    // When an item rotates, its pivot shifts - we need to compensate
                    Vector2 rotationPivotOffset = CalculateRotationPivotOffset(itemDef, dir, currentGrid.CellSize);
                    targetPosition += rotationPivotOffset;

                    // LERP to target position (smooth movement!)
                    RectTransform itemRT = draggingPlacedObject.GetComponent<RectTransform>();
                    itemRT.anchoredPosition = Vector2.Lerp(itemRT.anchoredPosition, targetPosition, Time.deltaTime * 20f);
                    
                    // LERP rotation (smooth rotation!)
                    float rotationAngle = itemDef != null ? itemDef.GetRotationAngle(dir) : 0f;
                    draggingPlacedObject.transform.rotation = Quaternion.Lerp(
                        draggingPlacedObject.transform.rotation,
                        Quaternion.Euler(0, 0, -rotationAngle),
                        Time.deltaTime * 15f
                    );
                }
            }
        }

        #region Event Callbacks

        public void OnItemBeginDrag(System.Guid itemInstanceID)
        {
            // Find which grid contains this item
            InventoryGridVisual sourceGrid = FindGridContainingItem(itemInstanceID);
            
            if (sourceGrid == null)
            {
                Debug.LogWarning($"[InventoryDragHandler] Could not find grid containing item {itemInstanceID}");
                return;
            }

            // Get the placed item
            PlacedItem placedItem = sourceGrid.InventorySystem.GetItemByID(itemInstanceID);
            if (placedItem == null)
            {
                Debug.LogWarning($"[InventoryDragHandler] Could not find item {itemInstanceID}");
                return;
            }

            // Get the visual component
            InventoryItemVisual itemVisual = sourceGrid.transform.GetComponentsInChildren<InventoryItemVisual>()
                .FirstOrDefault(v => v.PlacedItem.InstanceID == itemInstanceID);
            
            if (itemVisual == null)
            {
                Debug.LogWarning($"[InventoryDragHandler] Could not find visual for item {itemInstanceID}");
                return;
            }

            // Save drag state
            currentGrid = sourceGrid;
            draggingPlacedObject = itemVisual;
            
            // Save original state for return if drop fails
            originalGrid = sourceGrid;
            originalGridPosition = placedItem.AnchorPosition;
            originalDir = placedItem.Rotation;
            originalPlacedItem = placedItem;

            // Calculate mouse position in local space
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                sourceGrid.GetRectTransform(),
                Mouse.current.position.ReadValue(),
                null,
                out Vector2 anchoredPosition
            );
            Vector2Int mouseGridPosition = sourceGrid.LocalPositionToGridPosition(anchoredPosition);

            // Calculate grid position offset
            mouseDragGridPositionOffset = mouseGridPosition - placedItem.AnchorPosition;

            // Calculate anchored position offset (where exactly player clicked)
            mouseDragAnchoredPositionOffset = anchoredPosition - itemVisual.GetComponent<RectTransform>().anchoredPosition;

            // Save initial direction
            dir = placedItem.Rotation;

            Log($"Started dragging - Grid offset: {mouseDragGridPositionOffset}, Anchor offset: {mouseDragAnchoredPositionOffset}");
        }

        public void OnItemEndDrag(System.Guid itemInstanceID)
        {
            if (draggingPlacedObject == null)
            {
                Log("OnItemEndDrag called but not dragging!");
                return;
            }

            // Remove item from original inventory
            originalGrid.InventorySystem.RemoveItem(itemInstanceID);
            Log($"Removed item from {originalGrid.GetDisplayName()}");

            // Find which grid is under mouse for final placement
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            InventoryGridVisual targetGrid = GetGridUnderMouse(mouseScreenPos);

            bool dropped = false;

            if (targetGrid != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetGrid.GetRectTransform(),
                    mouseScreenPos,
                    null,
                    out Vector2 anchoredPosition
                );
                
                // EXACT SAME CALCULATION AS UPDATE()
                Vector2Int mouseGridPos = targetGrid.LocalPositionToGridPosition(anchoredPosition);
                Vector2Int placedObjectOrigin = mouseGridPos - mouseDragGridPositionOffset;

                InventoryItemSO itemDef = originalPlacedItem.ItemDefinition as InventoryItemSO;
                dropped = targetGrid.InventorySystem.TryAddItem(itemDef, placedObjectOrigin, dir, out PlacedItem placed);

                if (dropped)
                {
                    Log($"✓ Item placed at {placedObjectOrigin} in {targetGrid.GetDisplayName()}");
                }
                else
                {
                    Log($"✗ Cannot drop item here, returning to original position");
                    ReturnToOriginalPosition();
                }
            }
            else
            {
                Log($"✗ Not on any grid, returning to original position");
                ReturnToOriginalPosition();
            }

            // Clear drag state
            draggingPlacedObject = null;
            currentGrid = null;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Calculate the visual pivot offset needed to keep item visually stable during rotation.
        /// This compensates for the item's pivot point shifting when it rotates.
        /// </summary>
        private Vector2 CalculateRotationPivotOffset(InventoryItemSO itemDef, GridDirection rotation, float cellSize)
        {
            if (itemDef == null) return Vector2.zero;
            
            // Get the item's size in both rotations
            Vector2Int originalSize = itemDef.GetSizeForRotation(GridDirection.Down);
            Vector2Int rotatedSize = itemDef.GetSizeForRotation(rotation);
            
            // Calculate offset based on rotation
            // The pivot stays at bottom-left, but visual dimensions change
            Vector2 offset = Vector2.zero;
            
            switch (rotation)
            {
                case GridDirection.Down:
                    // No offset for default rotation
                    break;
                    
                case GridDirection.Right:
                    // When rotating right (90°), width becomes height
                    // Pivot shifts to compensate
                    offset.x = (rotatedSize.x - originalSize.x) * cellSize * 0.5f;
                    offset.y = (rotatedSize.y - originalSize.y) * cellSize * 0.5f;
                    break;
                    
                case GridDirection.Up:
                    // 180° rotation
                    offset.x = (rotatedSize.x - originalSize.x) * cellSize * 0.5f;
                    offset.y = (rotatedSize.y - originalSize.y) * cellSize * 0.5f;
                    break;
                    
                case GridDirection.Left:
                    // 270° rotation
                    offset.x = (rotatedSize.x - originalSize.x) * cellSize * 0.5f;
                    offset.y = (rotatedSize.y - originalSize.y) * cellSize * 0.5f;
                    break;
            }
            
            return offset;
        }

        private void ReturnToOriginalPosition()
        {
            InventoryItemSO itemDef = originalPlacedItem.ItemDefinition as InventoryItemSO;
            
            // Reparent back to original grid if needed
            if (draggingPlacedObject.transform.parent != originalGrid.GetRectTransform())
            {
                draggingPlacedObject.transform.SetParent(originalGrid.GetRectTransform(), true);
            }
            
            originalGrid.InventorySystem.TryAddItem(itemDef, originalGridPosition, originalDir, out _);
        }

        private InventoryGridVisual GetGridUnderMouse(Vector2 screenPosition)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (RaycastResult result in results)
            {
                InventoryGridVisual grid = result.gameObject.GetComponentInParent<InventoryGridVisual>();
                if (grid != null && registeredTargets.Contains(grid))
                {
                    return grid;
                }
            }

            return null;
        }

        private InventoryGridVisual FindGridContainingItem(System.Guid itemID)
        {
            foreach (IInventoryDropTarget target in registeredTargets)
            {
                if (target is InventoryGridVisual gridVisual)
                {
                    if (gridVisual.InventorySystem != null && 
                        gridVisual.InventorySystem.GetItemByID(itemID) != null)
                    {
                        return gridVisual;
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
