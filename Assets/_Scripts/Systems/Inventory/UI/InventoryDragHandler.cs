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
        private GridDirection dir;                             // Current rotation
        private Vector2Int lastTargetGridPosition;             // Where the visual is LERPING TO (for placement)
        private InventoryGridVisual lastGhostGrid;             // Track which grid is showing ghost preview
        
        // Original state for returning item if drop fails
        private InventoryGridVisual originalGrid;              // Null if dragged from equipment slot
        private IInventoryDropTarget originalSource;           // Could be grid or equipment slot
        private Vector2Int originalGridPosition;
        private GridDirection originalDir;
        private PlacedItem originalPlacedItem;

        // Stack splitting state
        private int draggedStackCount;                         // How many items are being dragged
        private bool isStackSplit;                             // True if we're dragging a split stack
        private System.Guid originalItemInstanceID;            // Original item's ID (for stack splits)

        public bool IsDragging => draggingPlacedObject != null;

        // Singleton — O(1) access, avoids FindObjectOfType at runtime
        public static InventoryDragHandler Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[InventoryDragHandler] Duplicate instance detected — destroying self.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

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

        /// <summary>
        /// Remove any destroyed/null targets from the registered list.
        /// Called defensively before iterating the list, since floating windows
        /// can be destroyed while still technically registered.
        /// </summary>
        private void PruneDestroyedTargets()
        {
            registeredTargets.RemoveAll(t => t == null || (t is MonoBehaviour mb && mb == null));
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

            // Determine stack split mode based on modifiers
            InventoryItemSO itemDef = placedItem.ItemDefinition as InventoryItemSO;
            isStackSplit = false;
            draggedStackCount = placedItem.StackCount;
            originalItemInstanceID = itemInstanceID; // Save original ID for restoration

            if (itemDef != null && itemDef.IsStackable && placedItem.StackCount > 1)
            {
                bool shiftHeld = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
                bool ctrlHeld = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;

                if (shiftHeld)
                {
                    // Shift: Split half
                    draggedStackCount = Mathf.CeilToInt(placedItem.StackCount / 2f);
                    isStackSplit = true;
                }
                else if (ctrlHeld)
                {
                    // Ctrl: Take one
                    draggedStackCount = 1;
                    isStackSplit = true;
                }
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

            InventoryItemVisual dragVisual;
            PlacedItem dragPlacedItem;

            // If we're splitting the stack, we need different logic
            if (isStackSplit)
            {
                // STACK MASTER PATTERN: Handle tracked vs homogeneous stacks differently
                List<ItemInstance> splitInstances = null;

                if (placedItem.IsInstanceTracked)
                {
                    // TRACKED STACK: Remove specific instances from the original
                    splitInstances = placedItem.RemoveInstances(draggedStackCount);
                }
                else
                {
                    // HOMOGENEOUS STACK: Just reduce the count
                    int remainingCount = placedItem.StackCount - draggedStackCount;
                    placedItem.SetStackCount(remainingCount);
                }

                // Refresh the grid to update the original visual's stack count
                sourceGrid.RefreshAllItemVisuals();

                // Create a NEW PlacedItem for the dragged portion (not in any grid yet)
                dragPlacedItem = new PlacedItem(itemDef, placedItem.AnchorPosition, placedItem.Rotation, draggedStackCount);

                // Track the split relationship for save/load and durability systems
                dragPlacedItem.SplitFromInstanceID = placedItem.InstanceID;

                // Transfer the split instances to the new PlacedItem
                if (splitInstances != null && splitInstances.Count > 0)
                {
                    // Clear auto-generated instances and add the split ones
                    dragPlacedItem.ItemInstances.Clear();
                    dragPlacedItem.AddInstances(splitInstances);
                }

                // DEPRECATED: Clone item data if the original has any (for backward compatibility)
                #pragma warning disable CS0618
                if (placedItem.ItemData != null)
                {
                    dragPlacedItem.ItemData = placedItem.ItemData;
                }
                #pragma warning restore CS0618

                // Get the original item's visual to copy its world position
                InventoryItemVisual originalVisual = sourceGrid.transform.GetComponentsInChildren<InventoryItemVisual>()
                    .FirstOrDefault(v => v.PlacedItem.InstanceID == itemInstanceID);

                // Create a NEW visual for dragging
                dragVisual = sourceGrid.CreateStandaloneVisual(dragPlacedItem, itemDef);
                RectTransform dragRT = dragVisual.GetComponent<RectTransform>();

                // FIXED APPROACH: Position the visual in the source grid first, THEN reparent to canvas
                if (originalVisual != null)
                {
                    // Parent to source grid temporarily
                    dragRT.SetParent(sourceGrid.GetRectTransform(), worldPositionStays: false);

                    // Copy the original visual's exact position in grid space
                    RectTransform originalRT = originalVisual.GetComponent<RectTransform>();
                    dragRT.anchoredPosition = originalRT.anchoredPosition;

                    // NOW reparent to canvas, maintaining the world position
                    Vector2 worldPos = dragRT.position;
                    dragRT.SetParent(canvasRoot, worldPositionStays: false);
                    // Normalize anchors to canvas center so free-float coordinate math is correct
                    dragRT.anchorMin = new Vector2(0.5f, 0.5f);
                    dragRT.anchorMax = new Vector2(0.5f, 0.5f);
                    dragRT.position = worldPos;
                }
                else
                {
                    // Fallback: position at mouse in canvas space
                    dragRT.SetParent(canvasRoot, worldPositionStays: false);
                    // Normalize anchors to canvas center so free-float coordinate math is correct
                    dragRT.anchorMin = new Vector2(0.5f, 0.5f);
                    dragRT.anchorMax = new Vector2(0.5f, 0.5f);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRoot,
                        mouseScreenPos,
                        null,
                        out Vector2 mouseCanvasPos
                    );
                    // Center item on cursor immediately
                    dragRT.anchoredPosition = mouseCanvasPos - dragRT.sizeDelta * 0.5f;
                }
            }
            else
            {
                // Normal drag: move the existing visual
                InventoryItemVisual itemVisual = sourceGrid.transform.GetComponentsInChildren<InventoryItemVisual>()
                    .FirstOrDefault(v => v.PlacedItem.InstanceID == itemInstanceID);

                if (itemVisual == null)
                {
                    Debug.LogWarning($"[InventoryDragHandler] Could not find visual for item {itemInstanceID}");
                    return;
                }

                dragVisual = itemVisual;
                dragPlacedItem = placedItem;

                // Parent to canvas so the visual floats freely during drag
                RectTransform itemRT = itemVisual.GetComponent<RectTransform>();
                Vector2 worldPos = itemRT.position;
                itemRT.SetParent(canvasRoot, worldPositionStays: false);
                // Normalize anchors to canvas center so free-float coordinate math is correct
                itemRT.anchorMin = new Vector2(0.5f, 0.5f);
                itemRT.anchorMax = new Vector2(0.5f, 0.5f);
                itemRT.position = worldPos; // Restore world position after anchor change
            }

            // CRITICAL: Visual is canvas-parented, so currentGrid = null
            // But preserve sourceGrid as originalGrid for return-to-original logic
            StartDragInternal(dragVisual, dragPlacedItem, null, sourceGrid, sourceGrid, placedItem.AnchorPosition, placedItem.Rotation);
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

            // Equipment slots: no grids involved — item is always centered on cursor during drag
            StartDragInternal(visual, placedItem, null, null, sourceTarget, Vector2Int.zero, placedItem.Rotation);
            
            // NOTE: Visual should ALREADY have correct rotation from when it was equipped
            // If not, the equipment slot needs to set rotation when creating the visual
            // UpdateRotation() will lerp only when user presses R during drag
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
            // For split stacks, itemInstanceID is the dragged PlacedItem's ID (which is new)
            // For normal drags, itemInstanceID comes from the drag-drop component (original ID)
            if (!isStackSplit && draggingPlacedObject.PlacedItem.InstanceID != itemInstanceID)
            {
                Debug.LogError($"[InventoryDragHandler] ID MISMATCH! Dragging {draggingPlacedObject.PlacedItem.InstanceID} but OnItemEndDrag called with {itemInstanceID}");
                return;
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
                PlacedItem placedItem = null;

                // CRITICAL FIX: Same-grid movement issue
                // If moving within the same grid, we may need to remove the item before placement
                bool isSameGridMove = (dropTarget == originalGrid);

                // WYSIWYG - THE RIGHT WAY: Use the TARGET position, not the lerping visual!
                // The visual is smoothly lerping toward the target, so reading its position gives mid-lerp coords
                if (dropTarget is InventoryGridVisual targetGrid)
                {
                    // Use the last calculated target position (where visual is lerping TO)
                    // NOT the visual's current position (which is mid-lerp)
                    Vector2Int placementPos = lastTargetGridPosition;

                    // Check if we're dropping onto an existing stack of the same item at this EXACT position
                    PlacedItem itemAtPosition = targetGrid.InventorySystem.GetItemAt(placementPos);
                    bool isDroppingOnSameStack = false;

                    if (itemAtPosition != null && itemDef.IsStackable)
                    {
                        // CRITICAL: Don't try to merge with ourselves!
                        // If this is the same item we're dragging (same InstanceID), skip merging
                        bool isSameItem = (itemAtPosition.InstanceID == itemInstanceID);

                        // Check if it's the same item type and can merge (but NOT the same item!)
                        if (itemAtPosition.ItemDefinition == itemDef && !isSameItem)
                        {
                            isDroppingOnSameStack = true;

                            // STACK MASTER PATTERN: Merge tracked or homogeneous stacks
                            int amountToAdd = draggedStackCount;
                            int spaceInStack = itemDef.MaxStackSize - itemAtPosition.StackCount;
                            int actualAdded = Mathf.Min(amountToAdd, spaceInStack);

                            if (actualAdded > 0)
                            {
                                // TRACKED STACKS: Transfer specific instances
                                if (itemAtPosition.IsInstanceTracked)
                                {
                                    // CRITICAL: For full stack moves, get instances from originalPlacedItem BEFORE removing it!
                                    // For splits, get from drag visual (which already has the split instances)
                                    List<ItemInstance> instancesToMerge = new List<ItemInstance>();

                                    if (isStackSplit && draggingPlacedObject != null)
                                    {
                                        // SPLIT: Get from drag visual
                                        InventoryItemVisual dragVisual = draggingPlacedObject.GetComponent<InventoryItemVisual>();
                                        if (dragVisual != null && dragVisual.PlacedItem != null && dragVisual.PlacedItem.ItemInstances != null)
                                        {
                                            // Take the instances from split stack
                                            int instancesToTake = Mathf.Min(actualAdded, dragVisual.PlacedItem.ItemInstances.Count);
                                            for (int i = 0; i < instancesToTake; i++)
                                            {
                                                instancesToMerge.Add(dragVisual.PlacedItem.ItemInstances[i]);
                                            }
                                        }
                                    }
                                    else if (!isStackSplit && originalGrid != null)
                                    {
                                        // FULL STACK MOVE: Get instances from originalPlacedItem (still in grid!)
                                        PlacedItem originalItem = originalGrid.InventorySystem.GetItemByID(itemInstanceID);
                                        if (originalItem != null && originalItem.ItemInstances != null)
                                        {
                                            // Take the instances we can fit from the original
                                            int instancesToTake = Mathf.Min(actualAdded, originalItem.ItemInstances.Count);
                                            for (int i = 0; i < instancesToTake; i++)
                                            {
                                                instancesToMerge.Add(originalItem.ItemInstances[i]);
                                            }
                                        }
                                    }

                                    // Now add the instances to the target
                                    if (instancesToMerge.Count > 0)
                                    {
                                        itemAtPosition.AddInstances(instancesToMerge);
                                    }
                                    else
                                    {
                                        // Fallback: Just add count (creates new pristine instances)
                                        itemAtPosition.AddToStack(actualAdded);
                                        Debug.LogWarning($"[InventoryDragHandler] TRACKED MERGE (fallback): Created {actualAdded} new pristine instances");
                                    }
                                }
                                else
                                {
                                    // HOMOGENEOUS STACKS: Just add count
                                    itemAtPosition.AddToStack(actualAdded);
                                }

                                // CRITICAL FIX: Remove the original item for BOTH tracked AND homogeneous stacks
                                // This was previously only inside the tracked stack block, causing homogeneous
                                // stack duplication when merging!
                                if (!isStackSplit && originalGrid != null)
                                {
                                    originalGrid.InventorySystem.RemoveItem(itemInstanceID);
                                }

                                dropped = true;
                                placedItem = itemAtPosition;

                                // Check if there are leftover items that didn't fit
                                int leftover = amountToAdd - actualAdded;
                                if (leftover > 0)
                                {

                                    // STACK MASTER: Handle leftover instances
                                    if (isStackSplit && originalGrid != null)
                                    {
                                        // For split stacks, add the leftover back to the original
                                        PlacedItem originalItem = originalGrid.InventorySystem.GetItemByID(originalItemInstanceID);
                                        if (originalItem != null)
                                        {
                                            if (originalItem.IsInstanceTracked && draggingPlacedObject != null)
                                            {
                                                // Return specific instances
                                                InventoryItemVisual dragVisual = draggingPlacedObject.GetComponent<InventoryItemVisual>();
                                                if (dragVisual != null && dragVisual.PlacedItem != null && dragVisual.PlacedItem.ItemInstances != null)
                                                {
                                                    // Get remaining instances from drag visual
                                                    List<ItemInstance> remainingInstances = new List<ItemInstance>();
                                                    for (int i = actualAdded; i < dragVisual.PlacedItem.ItemInstances.Count; i++)
                                                    {
                                                        remainingInstances.Add(dragVisual.PlacedItem.ItemInstances[i]);
                                                    }
                                                    originalItem.AddInstances(remainingInstances);
                                                }
                                            }
                                            else
                                            {
                                                // Homogeneous: Just add count
                                                originalItem.AddToStack(leftover);
                                            }
                                        }
                                    }
                                    else if (!isStackSplit && originalGrid != null)
                                    {
                                        // For full stack moves, recreate the original item with leftover count
                                        bool recreated = originalGrid.InventorySystem.TryAddItem(itemDef, originalGridPosition, originalDir, out PlacedItem leftoverItem, leftover, false);
                                        if (!recreated)
                                        {
                                            Debug.LogError($"[InventoryDragHandler] CRITICAL: Failed to place {leftover} leftover items!");
                                        }
                                    }
                                }

                                // Refresh visuals to show updated count
                                targetGrid.RefreshAllItemVisuals();

                                // If we had original grid operations, refresh that too
                                if (originalGrid != null && originalGrid != targetGrid)
                                {
                                    originalGrid.RefreshAllItemVisuals();
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"[InventoryDragHandler] Stack is full - cannot merge");
                                dropped = false;

                                // CRITICAL: We removed the original item, but merge failed!
                                // Need to restore it to the original grid with the SAME stack count AND instances
                                if (!isStackSplit && originalGrid != null)
                                {
                                    // STACK MASTER: Get the instances from the drag visual
                                    List<ItemInstance> draggedInstances = null;
                                    if (draggingPlacedObject != null)
                                    {
                                        InventoryItemVisual dragVisual = draggingPlacedObject.GetComponent<InventoryItemVisual>();
                                        if (dragVisual != null && dragVisual.PlacedItem != null && dragVisual.PlacedItem.IsInstanceTracked)
                                        {
                                            // Get the instances we were dragging
                                            draggedInstances = new List<ItemInstance>(dragVisual.PlacedItem.ItemInstances);
                                        }
                                    }

                                    // Re-add the item we removed WITH its original stack count
                                    bool restored = originalGrid.InventorySystem.TryAddItem(
                                        itemInstanceID,
                                        itemDef,
                                        originalGridPosition,
                                        originalDir,
                                        out PlacedItem restoredItem,
                                        draggedStackCount
                                    );

                                    if (restored && restoredItem != null)
                                    {
                                        // CRITICAL: Restore the instances or stack count
                                        if (draggedInstances != null && draggedInstances.Count > 0)
                                        {
                                            // TRACKED STACK: Restore the actual instances
                                            restoredItem.ItemInstances.Clear();
                                            restoredItem.AddInstances(draggedInstances);
                                        }
                                        else
                                        {
                                            // HOMOGENEOUS STACK: Just set the count
                                            restoredItem.SetStackCount(draggedStackCount);
                                        }

                                        originalGrid.RefreshAllItemVisuals();
                                    }
                                    else
                                    {
                                        Debug.LogError($"[InventoryDragHandler] CRITICAL: Failed to restore item after failed merge!");
                                    }
                                }
                            }
                        }
                    }

                    // PARADOX PREVENTION: Block container from being placed inside itself
                    // Must check BEFORE the isDroppingOnSameStack block to get a proper early exit
                    if (!isDroppingOnSameStack && originalPlacedItem != null && targetGrid.IsOwnerItem(originalPlacedItem))
                    {
                        Debug.LogWarning($"[InventoryDragHandler] Prevented '{itemDef.ItemName}' from being placed inside itself!");
                        dropped = false;
                        // Fall through to ReturnToOriginalPosition by leaving dropped = false and isDroppingOnSameStack = false
                    }
                    // If we didn't merge, try normal placement (no auto-stacking)
                    else if (!isDroppingOnSameStack)
                    {
                        // STACK MASTER: Get instances from drag visual for tracked stacks (both splits and full moves)
                        List<ItemInstance> draggedInstances = null;
                        if (draggingPlacedObject != null)
                        {
                            InventoryItemVisual dragVisual = draggingPlacedObject.GetComponent<InventoryItemVisual>();
                            if (dragVisual != null && dragVisual.PlacedItem != null && dragVisual.PlacedItem.IsInstanceTracked)
                            {
                                draggedInstances = new List<ItemInstance>(dragVisual.PlacedItem.ItemInstances);
                            }
                        }

                        // For same-grid moves (not merging), remove item BEFORE placement
                        // Otherwise it will overlap with itself!
                        if (!isStackSplit && isSameGridMove && originalGrid != null)
                        {
                            originalGrid.InventorySystem.RemoveItem(itemInstanceID);
                        }

                        // Call inventory system with the ORIGINAL InstanceID for same-grid moves to preserve identity
                        // For cross-grid moves, let it create a new ID
                        if (!isStackSplit && isSameGridMove)
                        {
                            // SAME GRID: Preserve InstanceID and stack count
                            dropped = targetGrid.InventorySystem.TryAddItem(
                                itemInstanceID,  // Preserve original ID
                                itemDef,
                                placementPos,
                                dir,
                                out placedItem,
                                draggedStackCount
                            );

                            // Restore instances for tracked stacks
                            if (dropped && placedItem != null && draggedInstances != null && draggedInstances.Count > 0)
                            {
                                placedItem.ItemInstances.Clear();
                                placedItem.AddInstances(draggedInstances);
                            }

                            // NESTED INVENTORY: Transfer container inventory
                            if (dropped && placedItem != null && originalPlacedItem != null && originalPlacedItem.ContainerInventory != null)
                            {
                                placedItem.ContainerInventory = originalPlacedItem.ContainerInventory;
                            }

                            // CRITICAL FIX: Refresh visuals to update stack count display
                            if (dropped)
                            {
                                targetGrid.RefreshAllItemVisuals();
                            }
                        }
                        else
                        {
                            // CROSS GRID or SPLIT: Preserve InstanceID for item lineage, carry over stack count
                            dropped = targetGrid.InventorySystem.TryAddItem(
                                originalPlacedItem?.InstanceID ?? System.Guid.NewGuid(),
                                itemDef, placementPos, dir, out placedItem,
                                draggedStackCount);

                            // CRITICAL: Transfer instances from the drag visual for splits and tracked cross-grid moves
                            if (dropped && placedItem != null && draggedInstances != null && draggedInstances.Count > 0)
                            {
                                placedItem.ItemInstances.Clear();
                                placedItem.AddInstances(draggedInstances);
                            }

                            // NESTED INVENTORY: Transfer container inventory for cross-grid moves
                            if (dropped && placedItem != null && originalPlacedItem != null && originalPlacedItem.ContainerInventory != null)
                            {
                                placedItem.ContainerInventory = originalPlacedItem.ContainerInventory;

                                // WINDOW SYNC: Update any open floating window for this container
                                FloatingContainerWindowManager windowManager = FloatingContainerWindowManager.Instance;
                                if (windowManager != null)
                                {
                                    windowManager.SyncContainerReference(placedItem);
                                }
                            }

                            // Refresh visuals to show correct uses/stack counts
                            if (dropped)
                            {
                                targetGrid.RefreshAllItemVisuals();
                            }
                        }
                    }
                }
                else
                {
                    // Non-grid drop target (equipment slot, etc.) - use interface method
                    // Equipment slots don't support stacking, so only allow if draggedStackCount == 1
                    if (draggedStackCount == 1)
                    {
                        // ITEM LINEAGE: Always use TryPlaceExistingItem to preserve item identity
                        if (originalPlacedItem != null)
                        {
                            dropped = dropTarget.TryPlaceExistingItem(originalPlacedItem, dir, mouseLocalPos, out placedItem);
                        }
                        else
                        {
                            // CRITICAL ERROR: originalPlacedItem should NEVER be null during drag operations
                            // This would mean we're trying to resurrect a dead item - FORBIDDEN!
                            Debug.LogError("[InventoryDragHandler] CRITICAL: originalPlacedItem is null! Cannot place item without lineage. This is a bug.");
                            dropped = false;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[InventoryDragHandler] Cannot drop stack of {draggedStackCount} on non-grid target");
                        dropped = false;
                    }
                }

                if (dropped)
                {
                    // SUCCESS: Handle cleanup based on whether this was a split or full move
                    if (!isStackSplit && !isSameGridMove && originalGrid != null)
                    {
                        // Full stack move from different grid - remove original
                        originalGrid.InventorySystem.RemoveItem(itemInstanceID);

                        // Refresh the original grid to remove the visual
                        originalGrid.RefreshAllItemVisuals();
                    }
                    else if (isStackSplit)
                    {
                        // Original stack was already reduced when we started the drag
                        // Just refresh the original grid's visuals to update stack count
                        if (originalGrid != null)
                        {
                            originalGrid.RefreshAllItemVisuals();
                        }
                    }

                    // Destroy the temp visual (target created its own OR merged into existing)
                    Destroy(draggingPlacedObject.gameObject);
                }
                else
                {
                    // FAILED: Return to original
                    // If we split the stack, we need to restore it
                    if (isStackSplit && originalGrid != null)
                    {
                        PlacedItem originalItem = originalGrid.InventorySystem.GetItemByID(originalItemInstanceID);
                        if (originalItem != null)
                        {
                            originalItem.AddToStack(draggedStackCount);
                            originalGrid.RefreshAllItemVisuals();
                        }
                    }

                    ReturnToOriginalPosition();
                }
            }
            else
            {
                // No target: Return to original
                // If we split the stack, we need to restore it
                if (isStackSplit && originalGrid != null)
                {
                    PlacedItem originalItem = originalGrid.InventorySystem.GetItemByID(originalItemInstanceID);
                    if (originalItem != null)
                    {
                        originalItem.AddToStack(draggedStackCount);
                        originalGrid.RefreshAllItemVisuals();
                    }
                }

                ReturnToOriginalPosition();
            }

            // Clear ghost preview
            ClearAllGhostPreviews();

            // Clear drag state
            draggingPlacedObject = null;
            currentGrid = null;
            originalSource = null;
            isStackSplit = false;
            draggedStackCount = 0;
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (draggingPlacedObject == null) return;

            // Check for mouse release (for split stacks that don't have drag-drop component)
            if (isStackSplit && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                OnItemEndDrag(draggingPlacedObject.PlacedItem.InstanceID);
                return;
            }

            // Handle rotation
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                InventoryItemSO itemDef = draggingPlacedObject.PlacedItem.ItemDefinition as InventoryItemSO;
                if (itemDef != null && itemDef.CanRotate)
                {
                    float cellSize = originalGrid != null ? originalGrid.CellSize : 60f;
                    dir = itemDef.GetNextRotation(dir);
                    draggingPlacedObject.ResizeForRotation(itemDef, dir, cellSize);
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
                RectTransform transitionRT = draggingPlacedObject.GetComponent<RectTransform>();
                Vector2 worldPos = transitionRT.position;
                draggingPlacedObject.transform.SetParent(targetGrid.GetRectTransform(), worldPositionStays: false);
                // CRITICAL: Reset anchors to (0,0) so anchoredPosition = offset from grid bottom-left,
                // which is what GridPositionToLocalPosition returns and what ScreenPointToLocalPoint
                // for the grid RectTransform measures from.
                transitionRT.anchorMin = Vector2.zero;
                transitionRT.anchorMax = Vector2.zero;
                transitionRT.position = worldPos;
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

            // Snap in float cell-space so both odd and even item sizes feel correct:
            //   mouseCellF  = mouse position in fractional cell units
            //   anchorCellF = mouseCellF - itemSize/2  (float, so a 2-wide item gets -1.0, not -1)
            //   placementGridPos = Round(anchorCellF)
            //
            // For odd sizes  (1x1, 1x3 …) the half offset is e.g. 0.5, so the snap point
            //   is exactly in the middle of whichever cell the mouse is in — snaps only on
            //   cell-centre crossings, identical feel to before.
            // For even sizes (2x2, 2x4 …) the half offset is a whole number (e.g. 1.0), so
            //   the snap point sits on a cell boundary and switches when the mouse crosses
            //   the midpoint between two cells — the item moves one column/row at a time and
            //   the visual centre tracks the cursor symmetrically.
            float cellSize = currentGrid.CellSize;
            Vector2 mouseCellF  = mouseLocalPos / cellSize;
            Vector2Int placementGridPos = Vector2Int.zero;
            if (itemDef != null)
            {
                Vector2Int itemSize = new Vector2Int(itemDef.GetRotatedWidth(dir), itemDef.GetRotatedHeight(dir));
                Vector2 anchorCellF = mouseCellF - new Vector2(itemSize.x * 0.5f, itemSize.y * 0.5f);
                placementGridPos = new Vector2Int(Mathf.RoundToInt(anchorCellF.x), Mathf.RoundToInt(anchorCellF.y));
            }
            else
            {
                placementGridPos = new Vector2Int(Mathf.FloorToInt(mouseCellF.x), Mathf.FloorToInt(mouseCellF.y));
            }

            if (verboseLogging)
            {
                Debug.Log($"[InventoryDragHandler] UPDATE VISUAL - mouseCellF={mouseCellF}, visualPlacement={placementGridPos}");
            }

            // Clamp to grid boundaries
            if (itemDef != null)
            {
                int itemW = itemDef.GetRotatedWidth(dir);
                int itemH = itemDef.GetRotatedHeight(dir);
                placementGridPos.x = Mathf.Clamp(placementGridPos.x, 0, currentGrid.InventorySystem.Width - itemW);
                placementGridPos.y = Mathf.Clamp(placementGridPos.y, 0, currentGrid.InventorySystem.Height - itemH);
            }

            // CRITICAL: Store this for placement! The visual lerps toward it, so reading visual position gives mid-lerp coords
            lastTargetGridPosition = placementGridPos;

            // Update ghost preview
            if (itemDef != null)
            {
                int itemWidth = itemDef.GetRotatedWidth(dir);
                int itemHeight = itemDef.GetRotatedHeight(dir);

                // FIX: For same-grid movement, ignore the dragging item's current cells when checking placement
                // Otherwise the item's current cells will block itself
                bool isSameGrid = (currentGrid == originalGrid);
                System.Guid ignoreItemID = (isSameGrid && originalPlacedItem != null) ? originalPlacedItem.InstanceID : System.Guid.Empty;

                // Check if we can place normally OR if we're hovering over a compatible stack
                bool canPlace = currentGrid.CanAcceptItemAtGridPosition(itemDef, dir, placementGridPos, ignoreItemID);

                // SPECIAL CASE: If hovering over an existing stackable item of the same type, show green
                if (!canPlace && itemDef.IsStackable)
                {
                    PlacedItem itemAtPosition = currentGrid.InventorySystem.GetItemAt(placementGridPos);
                    if (itemAtPosition != null && itemAtPosition.ItemDefinition == itemDef)
                    {
                        // Check if there's space in the stack
                        int spaceInStack = itemDef.MaxStackSize - itemAtPosition.StackCount;
                        if (spaceInStack > 0)
                        {
                            canPlace = true; // Show green - we can merge!
                            // Removed verbose logging for stack hover
                        }
                    }
                }

                // PARADOX PREVENTION: If dragging a container over its own floating window grid,
                // force red ghost preview to signal the drop will be rejected
                if (canPlace && originalPlacedItem != null && currentGrid.IsOwnerItem(originalPlacedItem))
                {
                    canPlace = false;
                }

                currentGrid.ShowGhostPreview(placementGridPos, itemWidth, itemHeight, canPlace);
                lastGhostGrid = currentGrid;
            }

            // Convert to local position
            Vector2 targetPosition = currentGrid.GridPositionToLocalPosition(placementGridPos);

            // REMOVED: Don't apply rotation offset during drag!
            // The rotation offset is for PLACED items to center them.
            // During dragging, we want items to rotate IN PLACE without shifting!
            // if (itemDef != null)
            // {
            //     Vector2Int rotationOffset = itemDef.GetRotationOffset(dir);
            //     targetPosition += new Vector2(rotationOffset.x, rotationOffset.y) * currentGrid.CellSize;
            // }

            // Smooth lerp to target — use a high factor so the item snaps quickly
            // and the cursor visually sits at the center rather than lagging noticeably.
            RectTransform itemRT = draggingPlacedObject.GetComponent<RectTransform>();
            itemRT.anchoredPosition = Vector2.Lerp(itemRT.anchoredPosition, targetPosition, Time.deltaTime * 30f);
        }

        private void UpdateFreeFloatMode(Vector2 mouseScreenPos)
        {
            RectTransform itemRT = draggingPlacedObject.GetComponent<RectTransform>();

            // Transition from grid → free-float?
            if (currentGrid != null)
            {
                // Reparent to canvas maintaining world position
                draggingPlacedObject.transform.SetParent(canvasRoot, worldPositionStays: true);

                // CRITICAL: Normalize anchors to canvas center (0.5, 0.5) so that anchoredPosition
                // uses the same coordinate origin as ScreenPointToLocalPointInRectangle(canvasRoot).
                // Without this, anchorMin=(0,0) means positions are relative to canvas bottom-left,
                // but ScreenPointToLocalPoint returns coords relative to the canvas pivot (center),
                // causing the item to teleport to the bottom-left corner of the screen.
                Vector2 worldPos = itemRT.position;
                itemRT.anchorMin = new Vector2(0.5f, 0.5f);
                itemRT.anchorMax = new Vector2(0.5f, 0.5f);
                // Re-apply world position after anchor change so item doesn't jump
                itemRT.position = worldPos;

                currentGrid = null;
            }

            // Clear ghost preview when not over a grid
            ClearAllGhostPreviews();

            // Center the item on the cursor in canvas space.
            // ScreenPointToLocalPointInRectangle with canvasRoot returns coords relative to the
            // canvas pivot — matching the anchor (0.5, 0.5) coordinate system set above.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot,
                mouseScreenPos,
                null,
                out Vector2 mouseCanvasPos
            );

            // sizeDelta is always (width * cellSize, height * cellSize) — set by Initialize and
            // kept up to date by ResizeForRotation.  Use it directly; no cellSize lookup needed.
            itemRT.anchoredPosition = mouseCanvasPos - itemRT.sizeDelta * 0.5f;
        }

        private void UpdateRotation()
        {
            InventoryItemSO itemDef = draggingPlacedObject.PlacedItem.ItemDefinition as InventoryItemSO;
            float rotationAngle = itemDef != null ? itemDef.GetRotationAngle(dir) : 0f;
            
            // CRITICAL: Rotate the VISUAL CHILD, not the root transform!
            // The root is grid-aligned (no rotation), the child holds the visual rotation
            draggingPlacedObject.VisualTransform.localRotation = Quaternion.Lerp(
                draggingPlacedObject.VisualTransform.localRotation,
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
                RectTransform visualRT = visual.GetComponent<RectTransform>();
                Vector2 worldPos = visualRT.position;
                visual.transform.SetParent(canvasRoot, worldPositionStays: false);
                // Normalize anchors to canvas center so free-float coordinate math is correct
                visualRT.anchorMin = new Vector2(0.5f, 0.5f);
                visualRT.anchorMax = new Vector2(0.5f, 0.5f);
                visualRT.position = worldPos;
            }
        }

        /// <summary>
        /// Forcibly cancel an in-progress drag and return the item to its origin.
        /// Called when a floating window is closed mid-drag, or when the inventory closes.
        /// Handles the case where originalGrid has been destroyed (e.g. the window it lived in
        /// was closed), in which case the item is simply destroyed to avoid data loss.
        /// </summary>
        public void CancelDrag()
        {
            if (draggingPlacedObject == null) return;

            // If the original grid was destroyed (window closed mid-drag), we can't return there.
            // The item data is still live in originalPlacedItem — just destroy the visual.
            // The item remains in whatever InventorySystem it was registered to.
            bool originalGridDestroyed = originalGrid != null && (originalGrid as MonoBehaviour) == null;

            if (originalGridDestroyed)
            {
                // Grid is gone — just clean up the visual. Item data survives in its system.
                ClearAllGhostPreviews();
                if (draggingPlacedObject != null)
                    Destroy(draggingPlacedObject.gameObject);
            }
            else
            {
                // Grid still alive — do a normal return
                if (isStackSplit && originalGrid != null)
                {
                    PlacedItem originalItem = originalGrid.InventorySystem.GetItemByID(originalItemInstanceID);
                    if (originalItem != null)
                    {
                        originalItem.AddToStack(draggedStackCount);
                        originalGrid.RefreshAllItemVisuals();
                    }
                }

                ClearAllGhostPreviews();
                ReturnToOriginalPosition();
            }

            // Clear all drag state
            draggingPlacedObject = null;
            currentGrid = null;
            originalGrid = null;
            originalSource = null;
            isStackSplit = false;
            draggedStackCount = 0;
        }

        private void ReturnToOriginalPosition()
        {
            InventoryItemSO itemDef = originalPlacedItem.ItemDefinition as InventoryItemSO;
            
            if (originalGrid != null)
            {
                // Check if item is still in the grid
                PlacedItem stillThere = originalGrid.InventorySystem.GetItemByID(originalPlacedItem.InstanceID);
                if (stillThere != null)
                {
                    // Item still in grid - just destroy visual
                }
                else
                {
                    // Item was removed (same-grid move) - need to add it back!
                    
                    // Re-add item at original position WITH SAME INSTANCE ID and stack count
                    bool readded = originalGrid.InventorySystem.TryAddItem(
                        originalPlacedItem.InstanceID,
                        itemDef,
                        originalGridPosition,
                        originalDir,
                        out PlacedItem restoredItem,
                        draggedStackCount
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
                
                // CRITICAL FIX: Regenerate visuals for all items in grid
                // The item data is still there, but we destroyed its visual
                originalGrid.RefreshAllItemVisuals();
            }
            else if (originalSource != null)
            {
                // Return to equipment slot (or other non-grid source)
                // ITEM LINEAGE: Use TryPlaceExistingItem to preserve item soul!
                if (originalPlacedItem != null)
                {
                    originalSource.TryPlaceExistingItem(originalPlacedItem, originalDir, Vector2.zero, out _);
                }
                else
                {
                    // Fallback for items that don't have PlacedItem data
                    Debug.LogWarning("[InventoryDragHandler] Returning item without PlacedItem data - container contents may be lost");
                    originalSource.TryPlaceItem(itemDef, originalDir, Vector2.zero, out _);
                }

                // Destroy temp visual since equipment slot will create its own
                Destroy(draggingPlacedObject.gameObject);
            }
            else
            {
                // No original source - just destroy the visual
                Debug.LogError("[InventoryDragHandler] NO ORIGINAL SOURCE! Item data will be lost!");
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

            // Remove any targets whose GameObjects were destroyed (e.g. closed floating windows)
            PruneDestroyedTargets();

            // Check all registered drop targets
            foreach (RaycastResult result in results)
            {
                // Skip if it's the dragging item itself
                if (draggingPlacedObject != null && result.gameObject == draggingPlacedObject.gameObject)
                    continue;

                foreach (IInventoryDropTarget target in registeredTargets)
                {
                    // Check if this GameObject is part of the target
                    if (target is MonoBehaviour targetMono && targetMono != null)
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
                if (target is InventoryGridVisual gridVisual && gridVisual != null)
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

        /// <summary>
        /// Clear ghost previews on all registered grids.
        /// </summary>
        private void ClearAllGhostPreviews()
        {
            if (lastGhostGrid != null)
            {
                lastGhostGrid.ClearGhostPreview();
                lastGhostGrid = null;
            }

            // Also clear on all registered grids to be safe
            foreach (IInventoryDropTarget target in registeredTargets)
            {
                if (target is InventoryGridVisual gridVisual)
                {
                    gridVisual.ClearGhostPreview();
                }
            }
        }

        #endregion
    }
}
