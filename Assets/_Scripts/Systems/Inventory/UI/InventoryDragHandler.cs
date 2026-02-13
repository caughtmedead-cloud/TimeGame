                // Item is still in original grid - destroy drag visual and regenerate
                Destroy(draggingPlacedObject.gameObject);
                Debug.Log($"[InventoryDragHandler] Destroyed drag visual, regenerating in {originalGrid.GetDisplayName()}");
                
                // CRITICAL FIX: Regenerate visuals for all items in grid
                // The item data is still there, but we destroyed its visual
                originalGrid.RefreshAllItemVisuals();
                Debug.Log($"[InventoryDragHandler] Refreshed visuals - item should be visible again");
            }