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
    /// REFACTORED to use Code Monkey's proven LOCAL SPACE approach.
    /// 
    /// Key principle: Calculate offset in LOCAL SPACE (relative to grid container),
    /// then apply before grid snapping for accurate cursor positioning.
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
        private IInventoryDropTarget currentHoverTarget; // Track which grid we're hovering over
        private PlacedItem draggedItem;
        private Vector2Int originalPosition;
        private GridDirection originalRotation;
        private GridDirection currentRotation;
        private InventorySystem sourceInventory;

        // CODE MONKEY PATTERN: Offset in LOCAL SPACE (relative to container)
        // This is the key to fixing both grid snapping AND cursor positioning
        private Vector2 mouseDragLocalOffset; // Offset in the CURRENT target's local space
        private float ghostCellSize; // Track ghost size for positioning

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
                
                // CODE MONKEY PATTERN: Calculate offset in LOCAL SPACE
                Vector2 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    gridVisual.GetRectTransform(),
                    mouseScreenPos,
                    null,
                    out Vector2 mouseLocalPos
                );

                // Get item's anchored position in local space
                Vector2 itemAnchorLocalPos = gridVisual.GridPositionToLocalPosition(originalPosition);
                
                // Calculate offset: where on the item did the user click?
                mouseDragLocalOffset = mouseLocalPos - itemAnchorLocalPos;
                
                Log($"Grid drag start - offset: {mouseDragLocalOffset}");

                // Initialize ghost
                ghostCellSize = gridVisual.CellSize;
                ghost.Initialize(itemDef, currentRotation, ghostCellSize);
                ghost.Show();

                isDragging = true;
                currentHoverTarget = gridVisual;
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
                    
                    // For equipment slots: Calculate offset relative to slot center
                    Vector2 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        slot.GetRectTransform(),
                        mouseScreenPos,
                        null,
                        out Vector2 mouseLocalPos
                    );
                    
                    // Slot center is at rect.center
                    Vector2 slotCenter = slot.GetRectTransform().rect.center;
                    mouseDragLocalOffset = mouseLocalPos - slotCenter;
                    
                    Log($"Equipment slot drag start - offset: {mouseDragLocalOffset}");

                    // Use default cell size for equipment slots
                    ghostCellSize = 64f;
                    ghost.Initialize(itemDef, currentRotation, ghostCellSize);
                    ghost.Show();

                    isDragging = true;
                    currentHoverTarget = null;
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
                    out Vector2 mouseLocalPos
                );

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
                bool success = targetUnderMouse.TryPlaceItem(
                    ghost.CurrentItem,
                    currentRotation,
                    mouseLocalPos,
                    out PlacedItem placed
                );

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
        /// Update ghost position using CODE MONKEY'S LOCAL SPACE approach.
        /// This fixes BOTH grid snapping AND cursor positioning issues.
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
                    out Vector2 mouseLocalPos
                );

                // CODE MONKEY KEY INSIGHT: Apply offset BEFORE snapping
                // This maintains cursor position relative to item
                Vector2 itemAnchorLocalPos = mouseLocalPos - mouseDragLocalOffset;

                Vector2 snappedLocalPos;
                bool canPlace = false;

                if (targetUnderMouse is InventoryGridVisual gridTarget)
                {
                    // Convert to grid position (this does the snapping)
                    Vector2Int gridPos = gridTarget.LocalPositionToGridPosition(itemAnchorLocalPos);
                    
                    // Convert back to snapped local position
                    snappedLocalPos = gridTarget.GridPositionToLocalPosition(gridPos);
                    
                    // Validate at grid position
                    canPlace = targetUnderMouse.CanAcceptItem(ghost.CurrentItem, currentRotation, mouseLocalPos);
                }
                else
                {
                    // For equipment slots, center on slot
                    snappedLocalPos = targetUnderMouse.GetRectTransform().rect.center;
                    canPlace = targetUnderMouse.CanAcceptItem(ghost.CurrentItem, currentRotation, mouseLocalPos);
                }

                // Convert snapped local position to screen space for ghost
                Vector3 ghostScreenPos = targetUnderMouse.GetRectTransform().TransformPoint(snappedLocalPos);
                ghost.transform.position = ghostScreenPos;
                ghost.SetValid(canPlace);
                
                currentHoverTarget = targetUnderMouse;
            }
            else
            {
                // No target - ghost follows mouse directly
                ghost.transform.position = mouseScreenPos;
                ghost.SetValid(false);
                currentHoverTarget = null;
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
            currentHoverTarget = null;
            mouseDragLocalOffset = Vector2.zero;
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
