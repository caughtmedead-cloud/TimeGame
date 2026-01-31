using System.Collections.Generic;
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

        // Mouse offset tracking (like CodeMonkey)
        private Vector2 mouseDragLocalOffset; // Offset in local space

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

            // Handle rotation input
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

                // Calculate mouse offset (where on the item did the user click?)
                Vector2 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    gridVisual.GetRectTransform(),
                    mouseScreenPos,
                    null, // null camera for Screen Space - Overlay
                    out Vector2 mouseLocalPos
                );

                Vector2Int mouseGridPos = gridVisual.LocalPositionToGridPosition(mouseLocalPos);
                Vector2Int itemGridPos = draggedItem.AnchorPosition;
                mouseDragLocalOffset = mouseLocalPos - gridVisual.GridPositionToLocalPosition(itemGridPos);

                Log($"Started dragging {draggedItem.ItemDefinition.name} from {sourceTarget.GetDisplayName()}");

                // Show ghost
                InventoryItemSO itemDef = draggedItem.ItemDefinition as InventoryItemSO;
                ghost.Initialize(itemDef, currentRotation, gridVisual.CellSize);
                ghost.Show();

                isDragging = true;
                UpdateGhostPosition();
            }
            else
            {
                // Dragging from equipment slot
                EquipmentSlot slot = sourceTarget as EquipmentSlot;
                if (slot != null && slot.EquippedItem != null)
                {
                    InventoryItemSO itemDef = slot.EquippedItem;
                    originalRotation = GridDirection.Down;
                    currentRotation = GridDirection.Down;

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
        /// 
        /// SCREEN SPACE - OVERLAY COORDINATE CONVERSION:
        /// In Overlay mode, UI elements' transform.position equals screen pixels.
        /// TransformPoint converts local UI coords → screen space directly.
        /// This is Unity's standard pattern for Overlay canvases.
        /// </summary>
        private void UpdateGhostPosition()
        {
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

                Vector2 snappedLocalPos;

                // For grids, snap to grid and apply offset
                if (targetUnderMouse is InventoryGridVisual gridTarget)
                {
                    Vector2Int gridPos = gridTarget.LocalPositionToGridPosition(mouseLocalPos - mouseDragLocalOffset);
                    snappedLocalPos = gridTarget.GridPositionToLocalPosition(gridPos);
                }
                else
                {
                    // For slots, center on the slot
                    snappedLocalPos = targetUnderMouse.GetRectTransform().rect.center;
                }
                
                // Convert local UI position to screen position
                // For Screen Space - Overlay: TransformPoint returns screen space directly
                Vector3 screenPos = targetUnderMouse.GetRectTransform().TransformPoint(snappedLocalPos);
                ghost.transform.position = screenPos;

                // Validate placement
                bool canPlace = targetUnderMouse.CanAcceptItem(ghost.CurrentItem, currentRotation, mouseLocalPos);
                ghost.SetValid(canPlace);
            }
            else
            {
                // No target - follow mouse, mark as invalid
                Vector2 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                ghost.transform.position = mouseScreenPos;
                ghost.SetValid(false);
            }
        }

        /// <summary>
        /// Rotate the dragged item.
        /// </summary>
        private void RotateDraggedItem()
        {
            InventoryItemSO itemDef = ghost.CurrentItem;
            if (itemDef == null || !itemDef.CanRotate)
            {
                Log("Cannot rotate - item doesn't support rotation");
                return;
            }

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
        /// Find the drop target that is currently under the mouse.
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

            // Find first registered target in raycast results
            foreach (RaycastResult result in results)
            {
                foreach (IInventoryDropTarget target in registeredTargets)
                {
                    if (result.gameObject == target.GetRectTransform().gameObject ||
                        result.gameObject.transform.IsChildOf(target.GetRectTransform()))
                    {
                        return target;
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
