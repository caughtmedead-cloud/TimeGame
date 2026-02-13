using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Hybrid drag handler supporting both grid-snapping and free-floating modes.
    /// 
    /// Grid Mode (inside inventory grids):
    /// - Smooth lerping to grid cells
    /// - Boundary clamping
    /// - Grid-to-grid transitions
    /// 
    /// Free-Float Mode (outside grids):
    /// - Item locked to cursor position
    /// - No grid snapping
    /// - Can be dropped on equipment slots or back into grids
    /// 
    /// Based on Code Monkey's pattern with multi-grid and free-float extensions.
    /// </summary>
    public class InventoryDragHandler : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Canvas root for free-floating items (auto-finds if not assigned)")]
        [SerializeField] private RectTransform canvasRoot;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        // Drop target tracking
        private List<IInventoryDropTarget> registeredTargets = new List<IInventoryDropTarget>();
        
        // Drag state
        private InventoryGridVisual currentGrid;               // Which grid the item is currently in (null if free-floating)
        private InventoryItemVisual draggingPlacedObject;      // The actual visual we're dragging
        private Vector2Int mouseDragGridPositionOffset;        // Grid offset (for grid mode)
        private Vector2 mouseDragCanvasOffset;                 // Offset in canvas space (mouse - item position)
        private GridDirection dir;                             // Current rotation
        
        // Original state for returning item if drop fails
        private InventoryGridVisual originalGrid;              // Null if dragged from equipment slot
        private IInventoryDropTarget originalSource;           // Could be grid or equipment slot
        private Vector2Int originalGridPosition;
        private GridDirection originalDir;
        private PlacedItem originalPlacedItem;

        public bool IsDragging => draggingPlacedObject != null;

        private void Awake()
        {
            // Find canvas root if not assigned
            if (canvasRoot == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    canvasRoot = canvas.GetComponent<RectTransform>();
                    Log($"Auto-found canvas root: {canvasRoot.name}");
                }
                else
                {
                    Debug.LogError("[InventoryDragHandler] Cannot find Canvas! Assign canvasRoot manually.", this);
                }
            }
        }

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

        #region Public Drag Methods

        /// <summary>
        /// Start dragging an item from a grid (existing behavior)
        /// </summary>
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

            // Calculate mouse position in grid's local space
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                sourceGrid.GetRectTransform(),
                mouseScreenPos,
                null,
                out Vector2 mouseGridLocalPos
            );
            Vector2Int mouseGridPosition = sourceGrid.LocalPositionToGridPosition(mouseGridLocalPos);

            // CODE MONKEY: Calculate grid position offset
            mouseDragGridPositionOffset = mouseGridPosition - placedItem.AnchorPosition;

            // Calculate canvas offset for when we transition to free-float
            // This ensures seamless transition when leaving grid
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot,
                mouseScreenPos,
                null,
                out Vector2 mouseCanvasPos
            );
            
            // Get item's position in canvas space via world position
            Vector3 itemWorldPos = itemVisual.transform.position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot,
                itemWorldPos,
                null,
                out Vector2 itemCanvasPos
            );
            
            mouseDragCanvasOffset = mouseCanvasPos - itemCanvasPos;
            
            Log($"Grid drag started - Grid offset: {mouseDragGridPositionOffset}, Canvas offset: {mouseDragCanvasOffset}");

            // Start drag
            StartDragInternal(itemVisual, placedItem, sourceGrid, sourceGrid, placedItem.AnchorPosition, placedItem.Rotation);
        }

        /// <summary>
        /// Start dragging with an existing visual (for equipment slots, external sources)
        /// </summary>
        public void StartDragWithExistingVisual(
            InventoryItemVisual visual, 
            PlacedItem placedItem, 
            IInventoryDropTarget sourceTarget,
            Vector2 clickOffsetInCanvasSpace)
        {
            if (visual == null || placedItem == null)
            {
                Debug.LogError("[InventoryDragHandler] Cannot start drag with null visual or item!");
                return;
            }

            // Store click offset for free-floating mode
            mouseDragCanvasOffset = clickOffsetInCanvasSpace;
            
            Log($"Equipment drag started with offset: {clickOffsetInCanvasSpace}");
            
            // Grid offset is zero (equipment slots don't have grid positions)
            mouseDragGridPositionOffset = Vector2Int.zero;

            // Start drag in free-float mode (currentGrid = null)
            StartDragInternal(visual, placedItem, null, sourceTarget, Vector2Int.zero, placedItem.Rotation);
        }

        /// <summary>
        /// End drag operation
        /// </summary>
        public void OnItemEndDrag(System.Guid itemInstanceID)
        {
            if (draggingPlacedObject == null)
            {
                Log("OnItemEndDrag called but not dragging!");
                return;
            }

            // Remove item from original source (if it was a grid)
            if (originalGrid != null)
            {
                originalGrid.InventorySystem.RemoveItem(itemInstanceID);
                Log($"Removed item from {originalGrid.GetDisplayName()}");
            }

            // Try to find a drop target under mouse
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            IInventoryDropTarget dropTarget = GetDropTargetUnderMouse(mouseScreenPos);

            bool dropped = false;

            if (dropTarget != null)
            {
                // Calculate local mouse position for this target
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dropTarget.GetRectTransform(),
                    mouseScreenPos,
                    null,
                    out Vector2 mouseLocalPos
                );

                InventoryItemSO itemDef = originalPlacedItem.ItemDefinition as InventoryItemSO;

                // Try to place the item
                dropped = dropTarget.TryPlaceItem(itemDef, dir, mouseLocalPos, out PlacedItem placedItem);

                if (dropped)
                {
                    Log($"✓ Item placed in {dropTarget.GetDisplayName()}");
                    
                    // Destroy the temp visual (target will create its own)
                    Destroy(draggingPlacedObject.gameObject);
                }
                else
                {
                    Log($"✗ Cannot drop item here, returning to original position");
                    ReturnToOriginalPosition();
                }
            }
            else
            {
                Log($"✗ Not on any drop target, returning to original position");
                ReturnToOriginalPosition();
            }

            // Clear drag state
            draggingPlacedObject = null;
            currentGrid = null;
            originalSource = null;
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (draggingPlacedObject == null) return;

            // Handle rotation
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                InventoryItemSO itemDef = draggingPlacedObject.PlacedItem.ItemDefinition as InventoryItemSO;
                if (itemDef != null && itemDef.CanRotate)
                {
                    dir = itemDef.GetNextRotation(dir);
                    Log($"Rotated to {dir}");
                }
            }

            // Determine drag mode based on mouse position
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            InventoryGridVisual targetGrid = GetGridUnderMouse(mouseScreenPos);

            if (targetGrid != null)
            {
                // GRID MODE: Snap to cells and lerp
                UpdateGridMode(targetGrid, mouseScreenPos);
            }
            else
            {
                // FREE-FLOATING MODE: Lock to cursor
                UpdateFreeFloatMode(mouseScreenPos);
            }

            // Smooth rotation (works in both modes)
            UpdateRotation();
        }

        private void UpdateGridMode(InventoryGridVisual targetGrid, Vector2 mouseScreenPos)
        {
            // Transition from free-float → grid OR switching grids?
            if (currentGrid != targetGrid)
            {
                if (currentGrid == null)
                {
                    Log($"Entered grid: {targetGrid.GetDisplayName()}");
                }
                else
                {
                    Log($"Switched from {currentGrid.GetDisplayName()} to {targetGrid.GetDisplayName()}");
                }
                
                draggingPlacedObject.transform.SetParent(targetGrid.GetRectTransform(), worldPositionStays: true);
                currentGrid = targetGrid;
            }

            // Calculate placement position with grid snapping
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                currentGrid.GetRectTransform(),
                mouseScreenPos,
                null,
                out Vector2 mouseLocalPos
            );
            
            // Calculate where item will land
            Vector2Int mouseGridPos = currentGrid.LocalPositionToGridPosition(mouseLocalPos);
            Vector2Int placementGridPos = mouseGridPos - mouseDragGridPositionOffset;
            
            // Clamp to grid boundaries
            InventoryItemSO itemDef = draggingPlacedObject.PlacedItem.ItemDefinition as InventoryItemSO;
            if (itemDef != null)
            {
                Vector2Int itemSize = new Vector2Int(itemDef.GetRotatedWidth(dir), itemDef.GetRotatedHeight(dir));
                placementGridPos.x = Mathf.Clamp(placementGridPos.x, 0, currentGrid.InventorySystem.Width - itemSize.x);
                placementGridPos.y = Mathf.Clamp(placementGridPos.y, 0, currentGrid.InventorySystem.Height - itemSize.y);
            }
            
            // Convert to local position
            Vector2 targetPosition = currentGrid.GridPositionToLocalPosition(placementGridPos);

            // Apply rotation offset
            if (itemDef != null)
            {
                Vector2Int rotationOffset = itemDef.GetRotationOffset(dir);
                targetPosition += new Vector2(rotationOffset.x, rotationOffset.y) * currentGrid.CellSize;
            }

            // Smooth lerp to target
            RectTransform itemRT = draggingPlacedObject.GetComponent<RectTransform>();
            itemRT.anchoredPosition = Vector2.Lerp(itemRT.anchoredPosition, targetPosition, Time.deltaTime * 20f);
        }

        private void UpdateFreeFloatMode(Vector2 mouseScreenPos)
        {
            RectTransform itemRT = draggingPlacedObject.GetComponent<RectTransform>();
            
            // Transition from grid → free-float?
            if (currentGrid != null)
            {
                Log("Exited grid - entering free-float mode");
                
                // Reparent to canvas maintaining world position
                draggingPlacedObject.transform.SetParent(canvasRoot, worldPositionStays: true);
                
                // Recalculate offset to maintain current visual relationship
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRoot,
                    mouseScreenPos,
                    null,
                    out Vector2 currentMousePos
                );
                
                Vector2 itemCanvasPos = itemRT.anchoredPosition;
                mouseDragCanvasOffset = currentMousePos - itemCanvasPos;
                
                Log($"Free-float offset recalculated: {mouseDragCanvasOffset}");
                
                currentGrid = null;
            }

            // CRITICAL: Position item AT mouse cursor minus offset
            // Offset is: where we clicked relative to item's bottom-left corner
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot,
                mouseScreenPos,
                null,
                out Vector2 mouseCanvasPos
            );

            // Item position = mouse position - offset
            // This keeps the click point fixed under the cursor
            itemRT.anchoredPosition = mouseCanvasPos - mouseDragCanvasOffset;
        }

        private void UpdateRotation()
        {
            InventoryItemSO itemDef = draggingPlacedObject.PlacedItem.ItemDefinition as InventoryItemSO;
            float rotationAngle = itemDef != null ? itemDef.GetRotationAngle(dir) : 0f;
            
            draggingPlacedObject.transform.rotation = Quaternion.Lerp(
                draggingPlacedObject.transform.rotation,
                Quaternion.Euler(0, 0, -rotationAngle),
                Time.deltaTime * 15f
            );
        }

        #endregion

        #region Helper Methods

        private void StartDragInternal(
            InventoryItemVisual visual, 
            PlacedItem placedItem, 
            InventoryGridVisual startGrid,
            IInventoryDropTarget sourceTarget,
            Vector2Int gridPosition,
            GridDirection rotation)
        {
            draggingPlacedObject = visual;
            currentGrid = startGrid;
            dir = rotation;

            // Save original state
            originalGrid = startGrid;
            originalSource = sourceTarget;
            originalGridPosition = gridPosition;
            originalDir = rotation;
            originalPlacedItem = placedItem;

            // If starting in free-float mode, parent to canvas immediately
            if (currentGrid == null && canvasRoot != null)
            {
                visual.transform.SetParent(canvasRoot, worldPositionStays: true);
                Log("Started drag in free-float mode");
            }
            else if (currentGrid != null)
            {
                Log($"Started drag in grid mode: {currentGrid.GetDisplayName()}");
            }
        }

        private void ReturnToOriginalPosition()
        {
            InventoryItemSO itemDef = originalPlacedItem.ItemDefinition as InventoryItemSO;
            
            if (originalGrid != null)
            {
                // Return to grid
                if (draggingPlacedObject.transform.parent != originalGrid.GetRectTransform())
                {
                    draggingPlacedObject.transform.SetParent(originalGrid.GetRectTransform(), worldPositionStays: true);
                }
                
                originalGrid.InventorySystem.TryAddItem(itemDef, originalGridPosition, originalDir, out _);
                Log($"Returned item to grid: {originalGrid.GetDisplayName()}");
            }
            else if (originalSource != null)
            {
                // Return to equipment slot (or other non-grid source)
                originalSource.TryPlaceItem(itemDef, originalDir, Vector2.zero, out _);
                
                // Destroy temp visual since equipment slot will create its own
                Destroy(draggingPlacedObject.gameObject);
                Log($"Returned item to: {originalSource.GetDisplayName()}");
            }
            else
            {
                // No original source - just destroy the visual
                Debug.LogWarning("[InventoryDragHandler] No original source to return to! Destroying visual.");
                Destroy(draggingPlacedObject.gameObject);
            }
        }

        private IInventoryDropTarget GetDropTargetUnderMouse(Vector2 screenPosition)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            // Check all registered drop targets
            foreach (RaycastResult result in results)
            {
                // Skip if it's the dragging item itself
                if (draggingPlacedObject != null && result.gameObject == draggingPlacedObject.gameObject)
                    continue;

                foreach (IInventoryDropTarget target in registeredTargets)
                {
                    // Check if this GameObject is part of the target
                    if (target is MonoBehaviour targetMono)
                    {
                        if (result.gameObject == targetMono.gameObject ||
                            result.gameObject.transform.IsChildOf(targetMono.transform))
                        {
                            return target;
                        }
                    }
                }
            }

            return null;
        }

        private InventoryGridVisual GetGridUnderMouse(Vector2 screenPosition)
        {
            IInventoryDropTarget target = GetDropTargetUnderMouse(screenPosition);
            return target as InventoryGridVisual;
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
