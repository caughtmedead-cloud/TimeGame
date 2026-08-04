using UnityEngine;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Interface for any UI element that can receive dropped inventory items.
    /// Implemented by both InventoryGridVisual (grid-based storage) 
    /// and EquipmentSlot (single-item slots).
    /// </summary>
    public interface IInventoryDropTarget
    {
        /// <summary>
        /// Check if this target can accept the given item at the current mouse position.
        /// </summary>
        /// <param name="item">The item being dragged</param>
        /// <param name="rotation">The current rotation of the item</param>
        /// <param name="mouseLocalPosition">Mouse position in target's local space</param>
        /// <returns>True if the item can be placed here</returns>
        bool CanAcceptItem(InventoryItemSO item, GridDirection rotation, Vector2 mouseLocalPosition);

        /// <summary>
        /// Try to place the item in this target.
        /// </summary>
        /// <param name="item">The item to place</param>
        /// <param name="rotation">The rotation to place it in</param>
        /// <param name="mouseLocalPosition">Mouse position in target's local space</param>
        /// <param name="placedItem">Output: The placed item data</param>
        /// <returns>True if placement succeeded</returns>
        bool TryPlaceItem(InventoryItemSO item, GridDirection rotation, Vector2 mouseLocalPosition, out PlacedItem placedItem);

        /// <summary>
        /// Try to place an existing item (with instances and container data) in this target.
        /// This preserves ItemInstances and ContainerInventory when dragging between targets.
        /// </summary>
        /// <param name="originalItem">The original placed item being moved (preserves instances/container data)</param>
        /// <param name="rotation">The rotation to place it in</param>
        /// <param name="mouseLocalPosition">Mouse position in target's local space</param>
        /// <param name="placedItem">Output: The placed item data</param>
        /// <returns>True if placement succeeded</returns>
        bool TryPlaceExistingItem(PlacedItem originalItem, GridDirection rotation, Vector2 mouseLocalPosition, out PlacedItem placedItem)
        {
            // Default implementation: just call the basic method
            return TryPlaceItem(originalItem.ItemDefinition as InventoryItemSO, rotation, mouseLocalPosition, out placedItem);
        }

        /// <summary>
        /// Get the RectTransform for mouse position testing.
        /// </summary>
        RectTransform GetRectTransform();

        /// <summary>
        /// Get a display name for debug logging.
        /// </summary>
        string GetDisplayName();
    }
}
