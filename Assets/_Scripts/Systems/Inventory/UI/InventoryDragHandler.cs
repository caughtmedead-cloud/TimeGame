using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Handles drag-drop operations for inventory items.
    /// Uses Code Monkey's EXACT pattern: TWO offsets + division-based snapping.
    /// 
    /// CRITICAL: Use division snapping (not grid coordinate conversion) to preserve cursor offset!
    /// </summary>
    public class InventoryDragHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryItemGhost ghost;

        [Header("Input Settings")]
        [SerializeField] private UnityEngine.InputSystem.Key rotateKey = UnityEngine.InputSystem.Key.R;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        // Drop target tracking
        private List<IInventoryDropTarget> registeredTargets = new List<IInventoryDropTarget>();
        
        // Drag state
        private bool isDragging = false;
        private IInventoryDropTarget sourceTarget;
        private PlacedItem draggedItem;
        private Vector2Int originalPosition;
        private GridDirection originalRotation;
        private GridDirection currentRotation;
        private InventorySystem sourceInventory;

        // CODE MONKEY'S TWO-OFFSET SYSTEM:
        private Vector2Int mouseDragGridPositionOffset; // Offset in grid cells
        private Vector2 mouseDragAnchoredPositionOffset; // Offset in local pixels

        public bool IsDragging => isDragging;

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

        private void Awake()
        {
            if (ghost != null)
            {
                Canvas ghostCanvas = ghost.GetComponentInParent<Canvas>();
                if (ghostCanvas != null && ghostCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    Debug.LogWarning(
                        $"[InventoryDragHandler] Ghost canvas is set to {ghostCanvas.renderMode}. " +
                        "This implementation is optimized for Screen Space - Overlay mode."
                    );
                }
            }
        }

        private void Update()
        {
            if (!isDragging) return;

            // Handle rotation input
            bool rPressed = false;
            
            if (Keyboard.current != null && Keyboard.current[rotateKey].wasPressedThisFrame)
            {
                Log("R detected via Keyboard.current");
                rPressed = true;
            }
            
            if (Input.GetKeyDown(KeyCode.R))
            {
                Log("R detected via Input.GetKeyDown");
                rPressed = true;
            }

            if (rPressed)
            {
                RotateDraggedItem();
            }

            UpdateGhostPosition();
        }

        #region Event Callbacks

        public void OnItemBeginDrag(System.Guid itemInstanceID)
        {
            sourceTarget = FindTargetContainingItem(itemInstanceID);
            
            if (sourceTarget == null)
            {
                Debug.LogWarning($"[InventoryDragHandler] Could not find source target for item {itemInstanceID}");
                return;
            }

            // Handle GRID sources
            InventoryGridVisual gridVisual = sourceTarget as InventoryGridVisual;
            if (gridVisual != null)
            {
                sourceInventory = gridVisual.InventorySystem;
                draggedItem = sourceInventory.GetItemByID(itemInstanceID);
                
                if (draggedItem == null)
                {
                    Debug.LogWarning($"[InventoryDragHandler] Could not find item {itemInstanceID}");
                    return;
                }

                originalPosition = draggedItem.AnchorPosition;
                originalRotation = draggedItem.Rotation;
                currentRotation = draggedItem.Rotation;

                InventoryItemSO itemDef = draggedItem.ItemDefinition as InventoryItemSO;
                
                // CODE MONKEY PATTERN: Get mouse in local space
                Vector2 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    gridVisual.GetRectTransform(),
                    mouseScreenPos,
                    null,
                    out Vector2 anchoredPosition
                );

                // Convert mouse to grid position
                Vector2Int mouseGridPosition = gridVisual.LocalPositionToGridPosition(anchoredPosition);

                // OFFSET #1: Grid position offset (in cells)
                mouseDragGridPositionOffset = mouseGridPosition - originalPosition;

                // OFFSET #2: Get ACTUAL RectTransform.anchoredPosition from visual
                InventoryItemVisual itemVisual = gridVisual.transform.GetComponentsInChildren<InventoryItemVisual>()
                    .FirstOrDefault(v => v.PlacedItem.InstanceID == itemInstanceID);
                
                if (itemVisual != null)
                {
                    // KEY: Use the ACTUAL anchored position, not calculated!
                    RectTransform itemRT = itemVisual.GetComponent<RectTransform>();
                    mouseDragAnchoredPositionOffset = anchoredPosition - itemRT.anchoredPosition;
                    
                    Log($"Grid drag - Grid offset: {mouseDragGridPositionOffset}, Anchor offset: {mouseDragAnchoredPositionOffset}");
                }
                else
                {
                    // Fallback: Calculate from grid position
                    Vector2 itemAnchorLocalPos = gridVisual.GridPositionToLocalPosition(originalPosition);
                    mouseDragAnchoredPositionOffset = anchoredPosition - itemAnchorLocalPos;
                    
                    Log($"Grid drag (fallback) - offset: {mouseDragAnchoredPositionOffset}");
                }

                // Initialize ghost
                ghost.Initialize(itemDef, currentRotation, gridVisual.CellSize);
                ghost.Show();

                isDragging = true;
                UpdateGhostPosition();
            }
            // Handle EQUIPMENT SLOT sources
            else
            {
                EquipmentSlot slot = sourceTarget as EquipmentSlot;
                if (slot != null && slot.EquippedItem != null)
                {
                    InventoryItemSO itemDef = slot.EquippedItem;
                    originalRotation = GridDirection.Down;
                    currentRotation = GridDirection.Down;
                    
                    // For equipment slots: no grid offset
                    mouseDragGridPositionOffset = Vector2Int.zero;
                    
                    // Calculate anchored position offset from slot center
                    Vector2 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        slot.GetRectTransform(),
                        mouseScreenPos,
                        null,
                        out Vector2 anchoredPosition
                    );
                    
                    // Slot center (equipment slots don't have grid, so use center)
                    Vector2 slotCenter = slot.GetRectTransform().rect.center;
                    mouseDragAnchoredPositionOffset = anchoredPosition - slotCenter;
                    
                    Log($"Equipment slot drag - offset: {mouseDragAnchoredPositionOffset}");

                    // Use default cell size for equipment slots
                    ghost.Initialize(itemDef, currentRotation, 64f);
                    ghost.Show();

                    isDragging = true;
                    UpdateGhostPosition();
                }
            }
        }

        public void OnItemEndDrag(System.Guid itemInstanceID)
        {
            if (!isDragging)
            {
                Log("OnItemEndDrag called but not dragging!");
                return;
            }

            Log("OnItemEndDrag called - processing drop");

            IInventoryDropTarget targetUnderMouse = GetDropTargetUnderMouse();

            if (targetUnderMouse != null)
            {
                Vector2 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetUnderMouse.GetRectTransform(),
                    mouseScreenPos,
                    null,
                    out Vector2 anchoredPosition
                );

                // CODE MONKEY PATTERN: Calculate placement position accounting for offset
                Vector2Int mouseGridPosition = Vector2Int.zero;
                
                if (targetUnderMouse is InventoryGridVisual gridTarget)
                {
                    mouseGridPosition = gridTarget.LocalPositionToGridPosition(anchoredPosition);
                    // Subtract grid offset to get actual placement position
                    mouseGridPosition = mouseGridPosition - mouseDragGridPositionOffset;
                }

                // Remove from source
                if (sourceTarget is InventoryGridVisual sourceGrid && sourceInventory != null)
                {
                    sourceInventory.RemoveItem(itemInstanceID);
                    Log($"Removed item from {sourceTarget.GetDisplayName()}");
                }
                else if (sourceTarget is EquipmentSlot sourceSlot)
                {
                    sourceSlot.UnequipItem();
                    Log($"Unequipped item from {sourceTarget.GetDisplayName()}");
                }

                // Try to place in target
                bool success = false;
                
                if (targetUnderMouse is InventoryGridVisual gridDest)
                {
                    // For grids, use the calculated grid position
                    success = gridDest.InventorySystem.TryAddItem(
                        ghost.CurrentItem,
                        mouseGridPosition,
                        currentRotation,
                        out PlacedItem placed
                    );
                }
                else
                {
                    // For equipment slots, use TryPlaceItem interface
                    success = targetUnderMouse.TryPlaceItem(
                        ghost.CurrentItem,
                        currentRotation,
                        anchoredPosition,
                        out PlacedItem placed
                    );
                }

                if (success)
                {
                    Log($"✓ Dropped {ghost.CurrentItem.name} to {targetUnderMouse.GetDisplayName()}");
                }
                else
                {
                    Log($"✗ Drop failed, returning to {sourceTarget.GetDisplayName()}");
                    
                    if (sourceTarget is InventoryGridVisual returnGrid && sourceInventory != null)
                    {
                        sourceInventory.TryAddItem(
                            ghost.CurrentItem,
                            originalPosition,
                            originalRotation,
                            out _
                        );
                    }
                    else if (sourceTarget is EquipmentSlot returnSlot)
                    {
                        returnSlot.TryEquipItem(ghost.CurrentItem);
                    }
                }
            }
            else
            {
                Log("No valid target under mouse, returning to source");
                
                if (sourceTarget is InventoryGridVisual returnGrid && sourceInventory != null)
                {
                    Log("Item stayed in original position");
                }
                else if (sourceTarget is EquipmentSlot returnSlot)
                {
                    Log("Item stayed in slot");
                }
            }

            EndDrag();
        }

        #endregion

        #region Drag Update Logic

        /// <summary>
        /// CODE MONKEY EXACT PATTERN: Division-based snapping preserves cursor offset!
        /// </summary>
        private void UpdateGhostPosition()
        {
            IInventoryDropTarget targetUnderMouse = GetDropTargetUnderMouse();
            Vector2 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

            if (targetUnderMouse != null)
            {
                // Get mouse in target's local space
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetUnderMouse.GetRectTransform(),
                    mouseScreenPos,
                    null,
                    out Vector2 targetPosition
                );

                // CODE MONKEY: Apply anchored position offset (NOTE THE NEGATION!)
                targetPosition += new Vector2(-mouseDragAnchoredPositionOffset.x, -mouseDragAnchoredPositionOffset.y);

                Vector2 snappedLocalPos;
                bool canPlace = false;

                if (targetUnderMouse is InventoryGridVisual gridTarget)
                {
                    float cellSize = gridTarget.CellSize;
                    
                    // CODE MONKEY'S DIVISION-BASED SNAPPING (preserves sub-pixel offset!)
                    // This keeps the cursor at the clicked position on the item!
                    Vector2 snappedPosition = targetPosition / cellSize;
                    snappedPosition = new Vector2(Mathf.Floor(snappedPosition.x), Mathf.Floor(snappedPosition.y));
                    snappedLocalPos = snappedPosition * cellSize;
                    
                    // For validation and placement, we still need grid coordinates
                    Vector2Int cursorGridPos = gridTarget.LocalPositionToGridPosition(targetPosition);
                    Vector2Int placementGridPos = cursorGridPos - mouseDragGridPositionOffset;
                    
                    // Validate at placement position
                    canPlace = gridTarget.InventorySystem.CanAddItem(ghost.CurrentItem, placementGridPos, currentRotation);
                    
                    Log($"Cursor grid: {cursorGridPos}, Placement grid: {placementGridPos}, Offset: {mouseDragGridPositionOffset}");
                }
                else
                {
                    // For equipment slots, center on slot
                    snappedLocalPos = targetUnderMouse.GetRectTransform().rect.center;
                    canPlace = targetUnderMouse.CanAcceptItem(ghost.CurrentItem, currentRotation, targetPosition);
                }

                // Convert snapped local position to screen space for ghost
                Vector3 ghostScreenPos = targetUnderMouse.GetRectTransform().TransformPoint(snappedLocalPos);
                ghost.transform.position = ghostScreenPos;
                ghost.SetValid(canPlace);
            }
            else
            {
                // No target - ghost follows mouse directly
                ghost.transform.position = mouseScreenPos;
                ghost.SetValid(false);
            }
        }

        private void RotateDraggedItem()
        {
            InventoryItemSO itemDef = ghost.CurrentItem;
            if (itemDef == null || !itemDef.CanRotate)
            {
                Log("Cannot rotate - item doesn't support rotation");
                return;
            }

            currentRotation = itemDef.GetNextRotation(currentRotation);
            ghost.Rotate();

            Log($"Rotated to {currentRotation}");
            UpdateGhostPosition();
        }

        #endregion

        #region Helper Methods

        private IInventoryDropTarget GetDropTargetUnderMouse()
        {
            if (Mouse.current == null) return null;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = mousePos
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (RaycastResult result in results)
            {
                foreach (IInventoryDropTarget target in registeredTargets)
                {
                    GameObject targetObj = target.GetRectTransform().gameObject;
                    
                    if (result.gameObject == targetObj ||
                        result.gameObject.transform.IsChildOf(target.GetRectTransform()))
                    {
                        if (targetObj.activeInHierarchy)
                        {
                            return target;
                        }
                    }
                }
            }

            return null;
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

        private void EndDrag()
        {
            ghost.Hide();
            isDragging = false;
            draggedItem = null;
            sourceTarget = null;
            sourceInventory = null;
            mouseDragGridPositionOffset = Vector2Int.zero;
            mouseDragAnchoredPositionOffset = Vector2.zero;
            Log("Drag ended, cleanup complete");
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
