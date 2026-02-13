        /// <summary>
        /// End drag operation
        /// </summary>
        public void OnItemEndDrag(System.Guid itemInstanceID)
        {
            if (draggingPlacedObject == null)
            {
                Debug.LogWarning("[InventoryDragHandler] OnItemEndDrag called but not dragging!");
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

                // Try to place the item FIRST
                dropped = dropTarget.TryPlaceItem(itemDef, dir, mouseLocalPos, out PlacedItem placedItem);

                if (dropped)
                {
                    // SUCCESS: Remove from original grid NOW
                    if (originalGrid != null)
                    {
                        originalGrid.InventorySystem.RemoveItem(itemInstanceID);
                        Debug.Log($"[InventoryDragHandler] ✓ Moved from {originalGrid.GetDisplayName()} → {dropTarget.GetDisplayName()}");
                    }
                    else
                    {
                        Debug.Log($"[InventoryDragHandler] ✓ Placed in {dropTarget.GetDisplayName()}");
                    }
                    
                    // Destroy the temp visual (target created its own)
                    Destroy(draggingPlacedObject.gameObject);
                }
                else
                {
                    // FAILED: Return to original position (item never removed)
                    Debug.Log($"[InventoryDragHandler] ✗ Cannot place - returning to original");
                    ReturnToOriginalPosition();
                }
            }
            else
            {
                // No target: Return to original position
                Debug.Log($"[InventoryDragHandler] ✗ No target - returning to original");
                ReturnToOriginalPosition();
            }

            // Clear drag state
            draggingPlacedObject = null;
            currentGrid = null;
            originalSource = null;
        }