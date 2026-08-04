using System;
using System.Collections.Generic;
using UnityEngine;
using TimeGame.Systems.GridPlacement;
using TimeGame.Systems.Inventory.UI;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Stateless placement helpers shared by <see cref="PlayerItemInteraction"/>
    /// and <c>ClientInventoryReconciler</c>.
    ///
    /// Owns:
    ///   • <see cref="FindFirstAvailablePosition"/> — first free cell scan.
    ///   • <see cref="TryAddToAnyGrid"/> — stack-merge then free-cell placement.
    ///   • <see cref="TryAddSpeculativeToAnyGrid"/> — same but stamps a caller-supplied Guid.
    /// </summary>
    public static class InventoryPlacementHelper
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Position scan
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the first grid cell that can accept <paramref name="item"/> at
        /// <paramref name="rotation"/>, or null when no position is available.
        /// </summary>
        public static Vector2Int? FindFirstAvailablePosition(
            InventoryGridVisual grid,
            InventoryItemSO     item,
            GridDirection       rotation)
        {
            if (grid?.InventorySystem == null || item == null) return null;

            int width      = grid.InventorySystem.Width;
            int height     = grid.InventorySystem.Height;
            int itemWidth  = item.GetRotatedWidth(rotation);
            int itemHeight = item.GetRotatedHeight(rotation);

            for (int y = 0; y <= height - itemHeight; y++)
            for (int x = 0; x <= width  - itemWidth;  x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (grid.InventorySystem.CanAddItem(item, pos, rotation))
                    return pos;
            }

            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Non-speculative add (solo / offline path)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Add an item to the first available grid across <paramref name="grids"/>.
        /// Tries stack-merging first (all grids), then falls back to a free cell placement.
        /// Returns true when the item was successfully placed.
        /// </summary>
        public static bool TryAddToAnyGrid(
            IEnumerable<InventoryGridVisual> grids,
            InventoryItemSO                  itemDef,
            int                              quantity,
            ItemInstance                     itemInstance,
            ContainerItemData                containerData)
        {
            if (grids == null || itemDef == null) return false;

            // ── Step 1: stack-merge ───────────────────────────────────────────
            if (itemDef.IsStackable)
            {
                foreach (InventoryGridVisual grid in grids)
                {
                    if (grid?.InventorySystem == null) continue;
                    foreach (PlacedItem existing in grid.InventorySystem.GetAllItems())
                    {
                        if (existing.ItemDefinition != itemDef) continue;
                        if (existing.StackCount >= itemDef.MaxStackSize) continue;

                        if (itemInstance != null && itemDef.TrackIndividualItems)
                            existing.AddInstances(new List<ItemInstance> { itemInstance });
                        else
                            existing.AddToStack(quantity);

                        grid.RefreshAllItemVisuals();
                        return true;
                    }
                }
            }

            // ── Step 2: free-cell placement ───────────────────────────────────
            GridDirection[] rotations = BuildRotations(itemDef);

            foreach (InventoryGridVisual grid in grids)
            {
                if (grid?.InventorySystem == null) continue;
                foreach (GridDirection rotation in rotations)
                {
                    Vector2Int? pos = FindFirstAvailablePosition(grid, itemDef, rotation);
                    if (!pos.HasValue) continue;

                    int stackCount = (itemInstance != null && itemDef.TrackIndividualItems) ? 0 : quantity;

                    bool ok = grid.InventorySystem.TryAddItem(
                        itemDef, pos.Value, rotation,
                        out PlacedItem placed,
                        stackCount, allowAutoStack: false);

                    if (ok && placed != null)
                    {
                        AttachExtras(placed, itemInstance, itemDef, containerData);
                        grid.RefreshAllItemVisuals();
                        return true;
                    }
                }
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Speculative add (networked path — stamps a caller-provided Guid)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Like <see cref="TryAddToAnyGrid"/> but stamps the supplied <paramref name="instanceId"/>
        /// onto the new <see cref="PlacedItem"/> so the server can confirm or roll it back
        /// without a remove-and-re-add round trip.
        /// Does NOT attempt stack merging (speculative items always create a new slot).
        /// </summary>
        public static bool TryAddSpeculativeToAnyGrid(
            IEnumerable<InventoryGridVisual> grids,
            InventoryItemSO                  itemDef,
            int                              quantity,
            ItemInstance                     itemInstance,
            ContainerItemData                containerData,
            Guid                             instanceId)
        {
            if (grids == null || itemDef == null) return false;

            GridDirection[] rotations = BuildRotations(itemDef);

            foreach (InventoryGridVisual grid in grids)
            {
                if (grid?.InventorySystem == null) continue;
                foreach (GridDirection rotation in rotations)
                {
                    Vector2Int? pos = FindFirstAvailablePosition(grid, itemDef, rotation);
                    if (!pos.HasValue) continue;

                    int stackCount = (itemInstance != null && itemDef.TrackIndividualItems) ? 0 : quantity;

                    bool ok = grid.InventorySystem.TryAddItem(
                        instanceId, itemDef, pos.Value, rotation,
                        out PlacedItem placed,
                        stackCount);

                    if (ok && placed != null)
                    {
                        AttachExtras(placed, itemInstance, itemDef, containerData);
                        grid.RefreshAllItemVisuals();
                        return true;
                    }
                }
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Confirmed add (network-granted — stamps a server-confirmed Guid)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Add a confirmed item to the first available grid with the server-confirmed
        /// <paramref name="instanceId"/>. Tries stack-merging first, then free-cell placement.
        /// </summary>
        public static bool TryAddConfirmedToAnyGrid(
            IEnumerable<InventoryGridVisual> grids,
            InventoryItemSO                  itemDef,
            int                              quantity,
            ItemInstance                     itemInstance,
            ContainerItemData                containerData,
            Guid                             instanceId)
        {
            if (grids == null || itemDef == null) return false;

            // ── Stack merge (preserve existing slots) ─────────────────────────
            if (itemDef.IsStackable)
            {
                foreach (InventoryGridVisual grid in grids)
                {
                    if (grid?.InventorySystem == null) continue;
                    foreach (PlacedItem existing in grid.InventorySystem.GetAllItems())
                    {
                        if (existing.ItemDefinition != itemDef) continue;
                        if (existing.StackCount >= itemDef.MaxStackSize) continue;

                        if (itemInstance != null && itemDef.TrackIndividualItems)
                            existing.AddInstances(new List<ItemInstance> { itemInstance });
                        else
                            existing.AddToStack(quantity);

                        grid.RefreshAllItemVisuals();
                        return true;
                    }
                }
            }

            // ── Free-cell with confirmed Guid ─────────────────────────────────
            GridDirection[] rotations = BuildRotations(itemDef);

            foreach (InventoryGridVisual grid in grids)
            {
                if (grid?.InventorySystem == null) continue;
                foreach (GridDirection rotation in rotations)
                {
                    Vector2Int? pos = FindFirstAvailablePosition(grid, itemDef, rotation);
                    if (!pos.HasValue) continue;

                    int stackCount = (itemInstance != null && itemDef.TrackIndividualItems) ? 0 : quantity;

                    bool ok = grid.InventorySystem.TryAddItem(
                        instanceId, itemDef, pos.Value, rotation,
                        out PlacedItem placed,
                        stackCount);

                    if (ok && placed != null)
                    {
                        AttachExtras(placed, itemInstance, itemDef, containerData);
                        grid.RefreshAllItemVisuals();
                        return true;
                    }
                }
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Private helpers
        // ─────────────────────────────────────────────────────────────────────

        private static GridDirection[] BuildRotations(InventoryItemSO itemDef) =>
            itemDef.CanRotate
                ? new[] { GridDirection.Down, GridDirection.Right, GridDirection.Up, GridDirection.Left }
                : new[] { GridDirection.Down };

        private static void AttachExtras(
            PlacedItem        placed,
            ItemInstance      itemInstance,
            InventoryItemSO   itemDef,
            ContainerItemData containerData)
        {
            if (itemInstance != null && itemDef.TrackIndividualItems)
                placed.AddInstances(new List<ItemInstance> { itemInstance });

            if (containerData != null && itemDef.ProvidesStorage)
            {
                InventorySystem containerInv = new InventorySystem(
                    itemDef.StorageGridSize.x, itemDef.StorageGridSize.y,
                    64f, Vector3.zero, itemDef.StorageMaxWeight);
                containerData.LoadIntoInventorySystem(containerInv, 0);
                placed.ContainerInventory = containerInv;
            }
        }
    }
}
