using System.Collections.Generic;
using UnityEngine;
using TimeGame.Systems.GridPlacement;
using TimeGame.Systems.Inventory.UI;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Handles equipping and unequipping items between inventory grids and equipment slots.
    /// Lives on the player prefab alongside <see cref="ItemUsageHandler"/>.
    /// Remote-player instances are disabled by the networking layer so only the local
    /// player's handler ever subscribes to context-menu events.
    /// All mutations that originate from a world container are forwarded to the server
    /// via <see cref="NetworkedInventoryComponent.LocalInstance"/>.
    /// </summary>
    public class ItemEquipHandler : MonoBehaviour
    {
        private void Start()
        {
            InventoryContextMenu contextMenu = FindObjectOfType<InventoryContextMenu>();
            if (contextMenu != null)
            {
                contextMenu.OnEquipItem            += HandleEquipItem;
                contextMenu.OnEquipmentSlotUnequip += HandleEquipmentSlotUnequip;
                contextMenu.OnEquipmentSlotInspect += HandleEquipmentSlotInspect;
            }
            else
            {
                Debug.LogWarning("[ItemEquipHandler] InventoryContextMenu not found in scene during Start().");
            }
        }

        private void OnDestroy()
        {
            InventoryContextMenu contextMenu = FindObjectOfType<InventoryContextMenu>();
            if (contextMenu == null) return;
            contextMenu.OnEquipItem            -= HandleEquipItem;
            contextMenu.OnEquipmentSlotUnequip -= HandleEquipmentSlotUnequip;
            contextMenu.OnEquipmentSlotInspect -= HandleEquipmentSlotInspect;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Equip from grid
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Equip an item from an inventory grid into the first compatible equipment slot.
        /// Removes the item from the grid and places it in the slot, preserving full lineage.
        /// If the slot rejects the item it is returned to its original grid position.
        /// </summary>
        private void HandleEquipItem(PlacedItem item, InventoryGridVisual grid, EquipmentSlot targetSlot)
        {
            if (item == null || grid == null || targetSlot == null) return;

            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            if (itemDef == null) return;

            // Optimistic local removal — server confirms or the item goes back.
            grid.InventorySystem.RemoveItem(item.InstanceID);
            grid.RefreshAllItemVisuals();

            bool equipped = targetSlot.TryPlaceExistingItem(item, GridDirection.Down, Vector2.zero, out _);
            if (!equipped)
            {
                Debug.LogWarning($"[ItemEquipHandler] Slot rejected {itemDef.ItemName} — returning to grid.");
                grid.InventorySystem.TryAddItem(item.InstanceID, itemDef, item.AnchorPosition, item.Rotation, out _, item.StackCount);
                grid.RefreshAllItemVisuals();
                return;
            }

            Debug.Log($"[ItemEquipHandler] Equipped {itemDef.ItemName} via context menu.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Unequip from slot
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Unequip an item from an equipment slot and move it to the first available
        /// position in any of the player's inventory grids.
        /// </summary>
        private void HandleEquipmentSlotUnequip(EquipmentSlot slot)
        {
            if (slot == null || !slot.IsOccupied) return;

            InventoryItemSO itemDef   = slot.EquippedItem;
            PlacedItem       placedItem = slot.GetEquippedPlacedItem();

            slot.UnequipItem(); // fires OnItemUnequipped → saves ContainerInventory

            if (placedItem == null)
            {
                Debug.LogWarning($"[ItemEquipHandler] Unequipped {itemDef?.ItemName} but had no PlacedItem data.");
                return;
            }

            PlayerInventoryManager inventoryManager = PlayerInventoryManager.Instance;
            if (inventoryManager == null)
            {
                Debug.LogWarning($"[ItemEquipHandler] No PlayerInventoryManager — unequipped {itemDef?.ItemName} has nowhere to go.");
                return;
            }

            foreach (InventoryGridVisual grid in inventoryManager.GetAllGrids())
            {
                if (grid == null || grid.InventorySystem == null) continue;

                Vector2Int? freePos = FindFirstFreePosition(grid.InventorySystem, itemDef, GridDirection.Down);
                if (freePos == null) continue;

                bool placed = grid.InventorySystem.TryAddItem(
                    placedItem.InstanceID,
                    itemDef,
                    freePos.Value,
                    GridDirection.Down,
                    out PlacedItem restoredItem
                );

                if (placed && restoredItem != null)
                {
                    if (placedItem.ContainerInventory != null)
                        restoredItem.ContainerInventory = placedItem.ContainerInventory;
                    if (placedItem.IsInstanceTracked && placedItem.ItemInstances?.Count > 0)
                        restoredItem.AddInstances(placedItem.ItemInstances);

                    grid.RefreshAllItemVisuals();
                    Debug.Log($"[ItemEquipHandler] Unequipped {itemDef.ItemName} → {grid.name} at {freePos.Value}.");
                    return;
                }
            }

            Debug.LogWarning($"[ItemEquipHandler] No space found for unequipped {itemDef?.ItemName}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Inspect equipped item
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Inspect an item currently sitting in an equipment slot.</summary>
        private void HandleEquipmentSlotInspect(EquipmentSlot slot)
        {
            if (slot == null || !slot.IsOccupied) return;
            ItemInspectPanel.Instance?.Show(slot.EquippedItem, slot.GetEquippedPlacedItem());
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the first grid position where <paramref name="item"/> fits at
        /// <paramref name="rotation"/>, or <c>null</c> if no space is available.
        /// </summary>
        private static Vector2Int? FindFirstFreePosition(InventorySystem system, InventoryItemSO item, GridDirection rotation)
        {
            if (system == null || item == null) return null;

            int itemWidth  = item.GetRotatedWidth(rotation);
            int itemHeight = item.GetRotatedHeight(rotation);

            for (int y = 0; y <= system.Height - itemHeight; y++)
                for (int x = 0; x <= system.Width - itemWidth; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (system.CanAddItem(item, pos, rotation))
                        return pos;
                }

            return null;
        }
    }
}
