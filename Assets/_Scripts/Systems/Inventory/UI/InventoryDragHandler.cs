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
        [SerializeField] private bool verboseLogging = false; // REDUCED LOGGING

        // Drop target tracking
        private List<IInventoryDropTarget> registeredTargets = new List<IInventoryDropTarget>();
        
        // Drag state
        private InventoryGridVisual currentGrid;               // Which grid the item is currently in (null if free-floating)
        private InventoryItemVisual draggingPlacedObject;      // The actual visual we're dragging
        private Vector2Int mouseDragGridPositionOffset;        // Grid offset (for grid mode)
        private Vector2 mouseDragCanvasOffset;                 // Offset in canvas space (NEVER changes during drag!)
        private GridDirection dir;                             // Current rotation
        private Vector2Int lastTargetGridPosition;             // Where the visual is LERPING TO (for placement)
        
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
            }
        }

        public void UnregisterDropTarget(IInventoryDropTarget target)
        {
            if (registeredTargets.Contains(target))
            {
                registeredTargets.Remove(target);
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
            
            Debug.Log($"[InventoryDragHandler] BEGIN DRAG - mouseGridPos={mouseGridPosition}, itemAnchor={placedItem.AnchorPosition}, CALCULATED OFFSET={mouseDragGridPositionOffset}");

            // Calculate canvas offset - CRITICAL: work in the SAME coordinate system!
            // Parent to canvas FIRST so we can use anchoredPosition directly
            RectTransform itemRT = itemVisual.GetComponent<RectTransform>();
            itemRT.SetParent(canvasRoot, worldPositionStays: true);
            
            // NOW both mouse and item are in canvas anchored position space
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot,
                mouseScreenPos,
                null,
                out Vector2 mouseCanvasPos
            );
            
            // Get item's anchored position (same coordinate system as mouseCanvasPos)
            Vector2 itemCanvasPos = itemRT.anchoredPosition;
            
            // This offset is set ONCE and never changes during the drag!
            mouseDragCanvasOffset = mouseCanvasPos - itemCanvasPos;

            // CRITICAL: Visual is canvas-parented, so currentGrid = null
            // But preserve sourceGrid as originalGrid for return-to-original logic
            StartDragInternal(itemVisual, placedItem, null, sourceGrid, sourceGrid, placedItem.AnchorPosition, placedItem.Rotation);
            
            // FIX: Set rotation immediately to match stored rotation (don't wait for lerp)
            InventoryItemSO itemDef = placedItem.ItemDefinition as InventoryItemSO;
            if (itemDef != null)
            {
                float rotationAngle = itemDef.GetRotationAngle(placedItem.Rotation);
                itemVisual.transform.rotation = Quaternion.Euler(0, 0, -rotationAngle);
            }
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

            // Store click offset - this NEVER changes during the drag!
            mouseDragCanvasOffset = clickOffsetInCanvasSpace;
            
            // Grid offset is zero (equipment slots don't have grid positions)
            mouseDragGridPositionOffset = Vector2Int.zero;

            // Equipment slots: no grids involved
            StartDragInternal(visual, placedItem, null, null, sourceTarget, Vector2Int.zero, placedItem.Rotation);
            
            // FIX: Set rotation immediately to match stored rotation
            InventoryItemSO itemDef = placedItem.ItemDefinition as InventoryItemSO;
            if (itemDef != null)
            {
                float rotationAngle = itemDef.GetRotationAngle(placedItem.Rotation);
                visual.transform.rotation = Quaternion.Euler(0, 0, -rotationAngle);
            }
        }

        /// <summary>
        /// End drag operation
        /// CRITICAL FIX: Only remove from original grid AFTER successful placement
        /// </summary>
        public void OnItemEndDrag(System.Guid itemInstanceID)
        {
            if (draggingPlacedObject == null)
            {
                Debug.LogWarning($"[InventoryDragHandler] OnItemEndDrag({itemInstanceID}) called but NOT DRAGGING!");
                return;
            }

            // CRITICAL: Check if this is a duplicate call
            if (draggingPlacedObject.PlacedItem.InstanceID != itemInstanceID)
            {
                Debug.LogError($"[InventoryDragHandler] ID MISMATCH! Dragging {draggingPlacedObject.PlacedItem.InstanceID} but OnItemEndDrag called with {itemInstanceID}");
                return;
            }

            Debug.Log($"[InventoryDragHandler] >>> BEGIN OnItemEndDrag({itemInstanceID}) <<<");

            // Try to find a drop target under mouse
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            IInventoryDropTarget dropTarget = GetDropTargetUnderMouse(mouseScreenPos);

            bool dropped = false;

            if (dropTarget != null)
            {
                Debug.Log($"[InventoryDragHandler] Drop target found: {dropTarget.GetDisplayName()}");
                
                // Calculate local mouse position for this target
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dropTarget.GetRectTransform(),
                    mouseScreenPos,
                    null,
                    out Vector2 mouseLocalPos
                );

                InventoryItemSO itemDef = originalPlacedItem.ItemDefinition as InventoryItemSO;
                PlacedItem placedItem = null;

                // CRITICAL FIX: Same-grid movement issue
                // If moving within the same grid, remove item BEFORE checking placement
                // Otherwise it will overlap with itself!
                bool isSameGridMove = (dropTarget == originalGrid);
                if (isSameGridMove && originalGrid != null)
                {
                    Debug.Log($"[InventoryDragHandler] Same-grid move detected - removing item before placement check");
                    originalGrid.InventorySystem.RemoveItem(itemInstanceID);
                }

                // WYSIWYG - THE RIGHT WAY: Use the TARGET position, not the lerping visual!
                // The visual is smoothly lerping toward the target, so reading its position gives mid-lerp coords
                if (dropTarget is InventoryGridVisual targetGrid)
                {
                    // Use the last calculated target position (where visual is lerping TO)
                    // NOT the visual's current position (which is mid-lerp)
                    Vector2Int placementPos = lastTargetGridPosition;
                    
                    Debug.Log($"[InventoryDragHandler] PLACEMENT - Using target grid position: {placementPos}");
                    
                    // Call inventory system DIRECTLY with the target position
                    dropped = targetGrid.InventorySystem.TryAddItem(itemDef, placementPos, dir, out placedItem);
                }
                else
                {
                    // Non-grid drop target (equipment slot, etc.) - use interface method
                    dropped = dropTarget.TryPlaceItem(itemDef, dir, mouseLocalPos, out placedItem);
                }

                if (dropped)
                {
                    Debug.Log($"[InventoryDragHandler] PLACEMENT SUCCESSFUL in {dropTarget.GetDisplayName()}");
                    
                    // SUCCESS: Remove from original grid (if not already removed)
                    if (!isSameGridMove && originalGrid != null)
                    {
                        bool removed = originalGrid.InventorySystem.RemoveItem(itemInstanceID);
                        Debug.Log($"[InventoryDragHandler] ✓ Removed from original grid {originalGrid.GetDisplayName()}: {removed}");
                    }
                    else if (isSameGridMove)
                    {
                        Debug.Log($"[InventoryDragHandler] ✓ Same-grid move - item already removed");
                    }
                    else
                    {
                        Debug.Log($"[InventoryDragHandler] No original grid to remove from");
                    }
                    
                    // Destroy the temp visual (target created its own)
                    Debug.Log($"[InventoryDragHandler] Destroying drag visual...");
                    Destroy(draggingPlacedObject.gameObject);
                }
                else
                {
                    // FAILED: Return to original (item never removed, so data is safe)
                    Debug.Log($"[InventoryDragHandler] ✗ PLACEMENT FAILED in {dropTarget.GetDisplayName()} - returning to original");
                    ReturnToOriginalPosition();
                }
            }
            else
            {
                // No target: Return to original
                Debug.Log($"[InventoryDragHandler] ✗ NO DROP TARGET - returning to original");
                ReturnToOriginalPosition();
            }

            // Clear drag state
            Debug.Log($"[InventoryDragHandler] Clearing drag state...");
            draggingPlacedObject = null;
            currentGrid = null;
            originalSource = null;
            
            Debug.Log($"[InventoryDragHandler] >>> END OnItemEndDrag({itemInstanceID}) <<<");
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
                // FREE-FLOATING MODE: Lock to cursor using ORIGINAL offset
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
            
            // Get item definition once
            InventoryItemSO itemDef = draggingPlacedObject.PlacedItem.ItemDefinition as InventoryItemSO;
            
            // Calculate where item will land
            Vector2Int mouseGridPos = currentGrid.LocalPositionToGridPosition(mouseLocalPos);
            
            // FIX: When offset is (0,0) (dragging from equipment), center item under cursor
            Vector2Int dragOffset = mouseDragGridPositionOffset;
            if (dragOffset == Vector2Int.zero && itemDef != null)
            {
                // Center the item: offset = itemSize / 2
                Vector2Int itemSize = new Vector2Int(itemDef.GetRotatedWidth(dir), itemDef.GetRotatedHeight(dir));
                dragOffset = new Vector2Int(itemSize.x / 2, itemSize.y / 2);
            }
            
            Vector2Int placementGridPos = mouseGridPos - dragOffset;
            
            if (verboseLogging)
            {
                Debug.Log($"[InventoryDragHandler] UPDATE VISUAL - mouseGridPos={mouseGridPos}, dragOffset={dragOffset}, visualPlacement={placementGridPos}");
            }
            
            // Clamp to grid boundaries
            if (itemDef != null)
            {
                Vector2Int itemSize = new Vector2Int(itemDef.GetRotatedWidth(dir), itemDef.GetRotatedHeight(dir));
                placementGridPos.x = Mathf.Clamp(placementGridPos.x, 0, currentGrid.InventorySystem.Width - itemSize.x);
                placementGridPos.y = Mathf.Clamp(placementGridPos.y, 0, currentGrid.InventorySystem.Height - itemSize.y);
            }
            
            // CRITICAL: Store this for placement! The visual lerps toward it, so reading visual position gives mid-lerp coords
            lastTargetGridPosition = placementGridPos;
            
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
                // Reparent to canvas maintaining world position
                draggingPlacedObject.transform.SetParent(canvasRoot, worldPositionStays: true);
                currentGrid = null;
            }

            // Position item using the ORIGINAL offset (set at drag start)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot,
                mouseScreenPos,
                null,
                out Vector2 mouseCanvasPos
            );

            // Item position = mouse position - ORIGINAL offset
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
            InventoryGridVisual startGrid,        // Current grid (visual parent)
            InventoryGridVisual sourceGrid,       // Original grid (for return)
            IInventoryDropTarget sourceTarget,
            Vector2Int gridPosition,
            GridDirection rotation)
        {
            draggingPlacedObject = visual;
            currentGrid = startGrid;  // Can be null if visual is canvas-parented
            dir = rotation;

            // Save original state
            originalGrid = sourceGrid;  // Where drag started from (can differ from currentGrid)
            originalSource = sourceTarget;
            originalGridPosition = gridPosition;
            originalDir = rotation;
            originalPlacedItem = placedItem;

            // If starting in free-float mode, parent to canvas immediately
            if (currentGrid == null && canvasRoot != null)
            {
                visual.transform.SetParent(canvasRoot, worldPositionStays: true);
            }
        }

        private void ReturnToOriginalPosition()
        {
            Debug.Log($"[InventoryDragHandler] >>> ReturnToOriginalPosition called <<<");
            
            InventoryItemSO itemDef = originalPlacedItem.ItemDefinition as InventoryItemSO;
            
            if (originalGrid != null)
            {
                Debug.Log($"[InventoryDragHandler] Returning to GRID: {originalGrid.GetDisplayName()}");
                
                // Check if item is still in the grid
                PlacedItem stillThere = originalGrid.InventorySystem.GetItemByID(originalPlacedItem.InstanceID);
                if (stillThere != null)
                {
                    Debug.Log($"[InventoryDragHandler] Item STILL IN GRID - just destroying visual");
                }
                else
                {
                    // Item was removed (same-grid move) - need to add it back!
                    Debug.Log($"[InventoryDragHandler] Item NOT IN GRID - re-adding at original position");
                    
                    // Re-add item at original position WITH SAME INSTANCE ID
                    bool readded = originalGrid.InventorySystem.TryAddItem(
                        originalPlacedItem.InstanceID,
                        itemDef,
                        originalGridPosition,
                        originalDir,
                        out PlacedItem restoredItem
                    );
                    
                    if (!readded)
                    {
                        Debug.LogError($"[InventoryDragHandler] CRITICAL: Failed to restore item to original position {originalGridPosition}!");
                    }
                }
                
                // Return to grid
                if (draggingPlacedObject.transform.parent != originalGrid.GetRectTransform())
                {
                    draggingPlacedObject.transform.SetParent(originalGrid.GetRectTransform(), worldPositionStays: true);
                }
                
                // Item is still in original grid - destroy drag visual and regenerate
                Destroy(draggingPlacedObject.gameObject);
                Debug.Log($"[InventoryDragHandler] Destroyed drag visual, regenerating in {originalGrid.GetDisplayName()}");
                
                // CRITICAL FIX: Regenerate visuals for all items in grid
                // The item data is still there, but we destroyed its visual
                originalGrid.RefreshAllItemVisuals();
                Debug.Log($"[InventoryDragHandler] Refreshed visuals - item should be visible again");
            }
            else if (originalSource != null)
            {
                Debug.Log($"[InventoryDragHandler] Returning to EQUIPMENT: {originalSource.GetDisplayName()}");
                
                // Return to equipment slot (or other non-grid source)
                originalSource.TryPlaceItem(itemDef, originalDir, Vector2.zero, out _);
                
                // Destroy temp visual since equipment slot will create its own
                Destroy(draggingPlacedObject.gameObject);
                Debug.Log($"[InventoryDragHandler] Re-equipped item in {originalSource.GetDisplayName()}");
            }
            else
            {
                // No original source - just destroy the visual
                Debug.LogError("[InventoryDragHandler] NO ORIGINAL SOURCE! Item data will be lost!");
                Destroy(draggingPlacedObject.gameObject);
            }
            
            Debug.Log($"[InventoryDragHandler] >>> ReturnToOriginalPosition complete <<<");
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

        #endregion
    }
}
