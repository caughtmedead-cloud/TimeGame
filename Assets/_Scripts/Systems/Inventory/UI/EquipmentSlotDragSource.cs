using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TimeGame.Systems.GridPlacement;
using System.Collections.Generic;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Makes equipment slot items draggable.
    /// Attach this to the itemIconImage GameObject of an EquipmentSlot.
    /// Handles unequipping on drag start and re-equipping on drag cancel.
    /// 
    /// Note: This manually manages the ghost and drop logic because equipment slots
    /// don't have persistent instance IDs like grid items do.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class EquipmentSlotDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private EquipmentSlot equipmentSlot;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private InventoryItemGhost ghost;
        
        // Drag state
        private InventoryItemSO draggedItem;
        private GridDirection currentRotation;
        private bool isDragging = false;

        [Header("Input Settings")]
        [SerializeField] private UnityEngine.InputSystem.Key rotateKey = UnityEngine.InputSystem.Key.R;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true; // Enable by default for debugging

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            
            // Find parent equipment slot
            equipmentSlot = GetComponentInParent<EquipmentSlot>();
            if (equipmentSlot == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] No EquipmentSlot found in parent!", this);
            }
            else
            {
                Log($"Found parent equipment slot: {equipmentSlot.GetDisplayName()}");
            }
            
            // Find ghost (should be in the scene on InventoryDragHandler)
            InventoryDragHandler dragHandler = GetComponentInParent<InventoryDragHandler>();
            if (dragHandler != null)
            {
                ghost = dragHandler.GetComponentInChildren<InventoryItemGhost>(true);
                Log($"Found InventoryDragHandler and ghost");
            }
            else
            {
                Debug.LogError("[EquipmentSlotDragSource] No InventoryDragHandler found in parent hierarchy!", this);
            }
            
            if (ghost == null)
            {
                Debug.LogError("[EquipmentSlotDragSource] No InventoryItemGhost found!", this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Log($"OnBeginDrag triggered! Mouse button: {eventData.button}");
            
            if (equipmentSlot == null || ghost == null || !equipmentSlot.IsOccupied)
            {
                Log($"Cannot begin drag - equipmentSlot null: {equipmentSlot == null}, ghost null: {ghost == null}, occupied: {equipmentSlot?.IsOccupied}");
                return;
            }

            // Get the equipped item
            draggedItem = equipmentSlot.EquippedItem;
            currentRotation = GridDirection.Down; // Equipment items don't rotate in slots
            
            Log($"Begin drag: {draggedItem.ItemName} from {equipmentSlot.GetDisplayName()}");

            // Unequip from slot (removes visual)
            equipmentSlot.UnequipItem();

            // Initialize and show ghost
            ghost.Initialize(draggedItem, currentRotation, 64f); // Use default cell size
            ghost.Show();
            
            isDragging = true;
            UpdateGhostPosition(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || ghost == null)
            {
                return;
            }

            // Handle rotation
            bool rPressed = false;
            
            if (Keyboard.current != null && Keyboard.current[rotateKey].wasPressedThisFrame)
            {
                rPressed = true;
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                rPressed = true;
            }

            if (rPressed && draggedItem.CanRotate)
            {
                currentRotation = draggedItem.GetNextRotation(currentRotation);
                ghost.Rotate();
                Log($"Rotated to {currentRotation}");
            }

            // Update ghost position
            UpdateGhostPosition(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || ghost == null)
            {
                Log("OnEndDrag called but not dragging!");
                return;
            }

            Log($"End drag: {draggedItem.ItemName}");

            // Find target under mouse
            IInventoryDropTarget targetUnderMouse = GetDropTargetUnderMouse(eventData.position);

            bool dropped = false;

            if (targetUnderMouse != null)
            {
                Log($"Target under mouse: {targetUnderMouse.GetDisplayName()}");
                
                // Get mouse position in target's local space
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetUnderMouse.GetRectTransform(),
                    eventData.position,
                    null, // null camera for Screen Space - Overlay
                    out Vector2 mouseLocalPos
                );

                // Try to place in target
                dropped = targetUnderMouse.TryPlaceItem(
                    draggedItem,
                    currentRotation,
                    mouseLocalPos,
                    out PlacedItem placedItem
                );

                if (dropped)
                {
                    Log($"Successfully dropped {draggedItem.ItemName} to {targetUnderMouse.GetDisplayName()}");
                }
                else
                {
                    Log($"Failed to drop {draggedItem.ItemName} to {targetUnderMouse.GetDisplayName()}");
                }
            }
            else
            {
                Log("No target under mouse");
            }

            if (!dropped)
            {
                // Drop failed - re-equip to original slot
                Log($"Drop failed - re-equipping {draggedItem.ItemName} to {equipmentSlot.GetDisplayName()}");
                equipmentSlot.TryEquipItem(draggedItem);
            }

            // Hide ghost and cleanup
            ghost.Hide();
            isDragging = false;
            draggedItem = null;
        }

        /// <summary>
        /// Update ghost position and validate placement.
        /// </summary>
        private void UpdateGhostPosition(Vector2 screenPosition)
        {
            IInventoryDropTarget targetUnderMouse = GetDropTargetUnderMouse(screenPosition);

            if (targetUnderMouse != null)
            {
                // Get mouse position in target's local space
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetUnderMouse.GetRectTransform(),
                    screenPosition,
                    null,
                    out Vector2 mouseLocalPos
                );

                Vector2 snappedLocalPos;

                // For grids, snap to grid
                if (targetUnderMouse is InventoryGridVisual gridTarget)
                {
                    Vector2Int gridPos = gridTarget.LocalPositionToGridPosition(mouseLocalPos);
                    snappedLocalPos = gridTarget.GridPositionToLocalPosition(gridPos);
                }
                else
                {
                    // For slots, center on the slot
                    snappedLocalPos = targetUnderMouse.GetRectTransform().rect.center;
                }

                // Convert to screen position
                Vector3 worldPos = targetUnderMouse.GetRectTransform().TransformPoint(snappedLocalPos);
                ghost.transform.position = worldPos;

                // Validate placement
                bool canPlace = targetUnderMouse.CanAcceptItem(draggedItem, currentRotation, mouseLocalPos);
                ghost.SetValid(canPlace);
            }
            else
            {
                // No target - follow mouse, mark as invalid
                ghost.transform.position = screenPosition;
                ghost.SetValid(false);
            }
        }

        /// <summary>
        /// Find the drop target currently under the mouse.
        /// </summary>
        private IInventoryDropTarget GetDropTargetUnderMouse(Vector2 screenPosition)
        {
            // Use EventSystem to raycast UI
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            // Find first InventoryGridVisual or EquipmentSlot
            foreach (RaycastResult result in results)
            {
                // Check for grid
                InventoryGridVisual gridVisual = result.gameObject.GetComponentInParent<InventoryGridVisual>();
                if (gridVisual != null)
                {
                    return gridVisual;
                }

                // Check for equipment slot
                EquipmentSlot slot = result.gameObject.GetComponentInParent<EquipmentSlot>();
                if (slot != null)
                {
                    return slot;
                }
            }

            return null;
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[EquipmentSlotDragSource] {message}");
            }
        }
    }
}
