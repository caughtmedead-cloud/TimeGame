using System.Collections.Generic;
using System.Linq; // For FirstOrDefault
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Handles drag-drop operations for inventory items using event-driven pattern.
    /// Works with ANY IInventoryDropTarget (grids, equipment slots, etc.).
    /// Based on CodeMonkey's drag-drop system architecture.
    /// 
    /// IMPORTANT: This implementation assumes Screen Space - Overlay canvas mode.
    /// For other canvas modes, coordinate conversion logic needs to be updated.
    /// 
    /// FIXES:
    /// - Bug #1: Ghost only shows on valid grid boundaries (not outside)
    /// - Bug #2: Cursor centered on item (not bottom-left)
    /// - Bug #3: Rotation works from equipment slots
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
        private IInventoryDropTarget sourceTarget; // Where we dragged FROM
        private PlacedItem draggedItem;
        private Vector2Int originalPosition;
        private GridDirection originalRotation;
        private GridDirection currentRotation;
        private InventorySystem sourceInventory; // Inventory we dragged from

        // FIX #2: Store the pixel offset from GHOST center to mouse cursor
        // This is in SCREEN SPACE (pixels) not local grid space
        private Vector2 mouseOffsetFromGhostCenter;

        /// <summary>
        /// Is a drag operation currently active?
        /// </summary>
        public bool IsDragging => isDragging;

        #region Registration

        /// <summary>
        /// Register a drop target (grid or slot).
        /// Call this when creating inventory panels/slots.
        /// </summary>
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

        /// <summary>
        /// Unregister a drop target.
        /// </summary>
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
            // Validate that we're using Screen Space - Overlay
            if (ghost != null)
            {
                Canvas ghostCanvas = ghost.GetComponentInParent<Canvas>();
                if (ghostCanvas != null && ghostCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    Debug.LogWarning(
                        $"[InventoryDragHandler] Ghost canvas is set to {ghostCanvas.renderMode}. " +
                        "This implementation is optimized for Screen Space - Overlay mode. " +
                        "Other modes may require coordinate conversion adjustments."
                    );
                }
            }
        }

        private void Update()
        {
            if (!isDragging) return;

            // FIX #3: Handle rotation input (works for both grid and equipment drags)
            bool rPressed = false;
            
            // Method 1: New Input System
            if (Keyboard.current != null && Keyboard.current[rotateKey].wasPressedThisFrame)
            {
                Log("R detected via Keyboard.current[Key.R]");
                rPressed = true;
            }
            
            // Method 2: Old Input System fallback
            if (Input.GetKeyDown(KeyCode.R))
            {
                Log("R detected via Input.GetKeyDown(KeyCode.R)");
                rPressed = true;
            }

            if (rPressed)
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
            // Find which target this item is in
            sourceTarget = FindTargetContainingItem(itemInstanceID);
            
            if (sourceTarget == null)
            {
                Debug.LogWarning($"[InventoryDragHandler] Could not find source target for item {itemInstanceID}");
                return;
            }

            // For grids, get the inventory system
            InventoryGridVisual gridVisual = sourceTarget as InventoryGridVisual;
            if (gridVisual != null)
            {
                sourceInventory = gridVisual.InventorySystem;
                
                // Get the item being dragged
                draggedItem = sourceInventory.GetItemByID(itemInstanceID);
                if (draggedItem == null)
                {
                    Debug.LogWarning($"[InventoryDragHandler] Could not find item {itemInstanceID}");
                    return;
                }

                // Store original state
                originalPosition = draggedItem.AnchorPosition;
                originalRotation = draggedItem.Rotation;
                currentRotation = draggedItem.Rotation;

                // FIX #2: Calculate SCREEN SPACE offset from ghost center to mouse
                InventoryItemSO itemDef = draggedItem.ItemDefinition as InventoryItemSO;
                Vector2 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                
                // Get the item's CURRENT screen position (where the visual is)
                InventoryItemVisual itemVisual = gridVisual.transform.GetComponentsInChildren<InventoryItemVisual>()
                    .FirstOrDefault(v => v.PlacedItem.InstanceID == itemInstanceID);
                
                if (itemVisual != null)
                {
                    // Get item center in screen space
                    RectTransform itemRT = itemVisual.GetComponent<RectTransform>();
                    Vector3 itemScreenCenter = RectTransformUtility.WorldToScreenPoint(null, itemRT.position);
                    
                    // Calculate offset from center to mouse
                    mouseOffsetFromGhostCenter = mouseScreenPos - (Vector2)itemScreenCenter;
                    
                    Log($"Mouse offset from item center: {mouseOffsetFromGhostCenter}");
                }
                else
                {
                    // Fallback: no offset (center cursor)
                    mouseOffsetFromGhostCenter = Vector2.zero;
                }

                Log($"Started dragging {draggedItem.ItemDefinition.name} from {sourceTarget.GetDisplayName()}");

                // Show ghost
                ghost.Initialize(itemDef, currentRotation, gridVisual.CellSize);
                ghost.Show();

                isDragging = true;
                UpdateGhostPosition();
            }
            else
            {
                // FIX #3: Dragging from equipment slot - enable rotation
                EquipmentSlot slot = sourceTarget as EquipmentSlot;
                if (slot != null && slot.EquippedItem != null)
                {
                    InventoryItemSO itemDef = slot.EquippedItem;
                    originalRotation = GridDirection.Down;
                    currentRotation = GridDirection.Down;
                    
                    // FIX #2: For equipment slots, center the ghost on cursor (no offset)
                    mouseOffsetFromGhostCenter = Vector2.zero;

                    Log($"Started dragging {itemDef.name} from slot {sourceTarget.GetDisplayName()}");

                    // Show ghost (use default cell size for slots)
                    ghost.Initialize(itemDef, currentRotation, 64f);
                    ghost.Show();

                    isDragging = true;
                    UpdateGhostPosition();
                }
            }
        }

        /// <summary>
        /// Called when user stops dragging an item.
        /// </summary>
        public void OnItemEndDrag(System.Guid itemInstanceID)
        {
            if (!isDragging)
            {
                Log("OnItemEndDrag called but not dragging!");
                return;
            }

            Log("OnItemEndDrag called - processing drop");

            // Find target under mouse
            IInventoryDropTarget targetUnderMouse = GetDropTargetUnderMouse();

            if (targetUnderMouse != null)
            {
                // Get mouse position in target's local space
                Vector2 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetUnderMouse.GetRectTransform(),
                    mouseScreenPos,
                    null, // null camera for Screen Space - Overlay
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
                    // Failed - return to source
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
                // No valid target - return to source
                Log("No valid target under mouse, returning to source");
                
                if (sourceTarget is InventoryGridVisual returnGrid && sourceInventory != null)
                {
                    // Don't need to remove since we never did!
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
        /// Update ghost position to follow mouse and validate placement.
        /// FIX #1: Only validate against the grid actually under mouse
        /// FIX #2: Apply stored screen-space offset to keep cursor at same relative position
        /// </summary>
        private void UpdateGhostPosition()
        {
            // FIX #1: Get the specific target under mouse
            IInventoryDropTarget targetUnderMouse = GetDropTargetUnderMouse();

            Vector2 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

            if (targetUnderMouse != null)
            {
                // Get mouse position in target's local space
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetUnderMouse.GetRectTransform(),
                    mouseScreenPos,
                    null, // null camera for Screen Space - Overlay
                    out Vector2 mouseLocalPos
                );

                Vector2 snappedLocalPos;

                // For grids, snap to grid
                if (targetUnderMouse is InventoryGridVisual gridTarget)
                {
                    // Convert mouse local position to grid position
                    Vector2Int gridPos = gridTarget.LocalPositionToGridPosition(mouseLocalPos);
                    
                    // Convert back to local position (snapped to grid)
                    snappedLocalPos = gridTarget.GridPositionToLocalPosition(gridPos);
                }
                else
                {
                    // For slots, center on the slot
                    snappedLocalPos = targetUnderMouse.GetRectTransform().rect.center;
                }
                
                // Convert local UI position to screen position
                // For Screen Space - Overlay: TransformPoint returns screen space directly
                Vector3 ghostScreenPos = targetUnderMouse.GetRectTransform().TransformPoint(snappedLocalPos);
                
                // FIX #2: Apply the stored offset to keep cursor at same spot on item
                ghostScreenPos = ghostScreenPos - (Vector3)mouseOffsetFromGhostCenter;
                
                ghost.transform.position = ghostScreenPos;

                // FIX #1: Validate placement against THIS SPECIFIC grid
                bool canPlace = targetUnderMouse.CanAcceptItem(ghost.CurrentItem, currentRotation, mouseLocalPos);
                ghost.SetValid(canPlace);
            }
            else
            {
                // FIX #1: No target - follow mouse directly, mark as invalid
                // FIX #2: Apply offset here too
                Vector3 ghostScreenPos = mouseScreenPos - mouseOffsetFromGhostCenter;
                ghost.transform.position = ghostScreenPos;
                ghost.SetValid(false);
            }
        }

        /// <summary>
        /// Rotate the dragged item.
        /// FIX #3: Works for both grid and equipment slot drags
        /// </summary>
        private void RotateDraggedItem()
        {
            InventoryItemSO itemDef = ghost.CurrentItem;
            if (itemDef == null)
            {
                Log("Cannot rotate - no item in ghost");
                return;
            }
            
            if (!itemDef.CanRotate)
            {
                Log("Cannot rotate - item doesn't support rotation");
                return;
            }

            // Get next rotation
            currentRotation = itemDef.GetNextRotation(currentRotation);

            // Update ghost
            ghost.Rotate();

            // FIX #2: Recalculate offset after rotation (dimensions changed)
            // Ghost center might shift due to size change
            // For now, keep offset as-is since ghost rotates around its center

            Log($"Rotated to {currentRotation}");

            // Re-validate
            UpdateGhostPosition();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Find the drop target that is currently under the mouse.
        /// FIX #1: Only returns targets that are actually visible and interactable
        /// </summary>
        private IInventoryDropTarget GetDropTargetUnderMouse()
        {
            if (Mouse.current == null) return null;

            Vector2 mousePos = Mouse.current.position.ReadValue();

            // Use EventSystem to raycast UI
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = mousePos
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            // FIX #1: Find first registered target in raycast results
            // Check that the hit object is actually part of the target's hierarchy
            foreach (RaycastResult result in results)
            {
                foreach (IInventoryDropTarget target in registeredTargets)
                {
                    GameObject targetObj = target.GetRectTransform().gameObject;
                    
                    // Check if we hit the target itself or a child
                    if (result.gameObject == targetObj ||
                        result.gameObject.transform.IsChildOf(target.GetRectTransform()))
                    {
                        // FIX #1: Verify the target is actually enabled and visible
                        if (targetObj.activeInHierarchy)
                        {
                            return target;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Find which target contains a specific item.
        /// </summary>
        private IInventoryDropTarget FindTargetContainingItem(System.Guid itemID)
        {
            foreach (IInventoryDropTarget target in registeredTargets)
            {
                // Check grids
                if (target is InventoryGridVisual gridVisual)
                {
                    if (gridVisual.InventorySystem != null && 
                        gridVisual.InventorySystem.GetItemByID(itemID) != null)
                    {
                        return target;
                    }
                }
                
                // Check equipment slots
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

        /// <summary>
        /// End drag operation and cleanup.
        /// </summary>
        private void EndDrag()
        {
            ghost.Hide();
            isDragging = false;
            draggedItem = null;
            sourceTarget = null;
            sourceInventory = null;
            mouseOffsetFromGhostCenter = Vector2.zero;
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
