using System;
using System.Collections.Generic;
using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Environment;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using UnityEngine;

namespace Inventory.Scripts.Core.ScriptableObjects
{
    // [CreateAssetMenu(menuName = "Inventory/Supplier/InventorySupplierSo")]
    public class InventorySupplierSo : ScriptableObject
    {
        /// <summary>
        /// Will place the item inside the grid, and also remove from the previous place (if was on a holder, going to unequipped and then place inside the grid)
        /// </summary>
        /// <param name="itemDataSo">The item data so which will be placed (Will create an ItemTable)</param>
        /// <param name="gridTable">The grid which will be placed</param>
        /// <returns>ItemTable created and also the Grid Response to see if was inserted or not</returns>
        public (ItemTable, GridResponse) PlaceItem(ItemDataSo itemDataSo, GridTable gridTable)
        {
            var itemTable = StaticInventoryContext.CreateItemTable(itemDataSo);

            var inserted = PlaceItem(itemTable, gridTable);

            return (itemTable, inserted);
        }

        /// <summary>
        /// Will place the item inside the grid, and also remove from the previous place (if was on a holder, going to unequipped and then place inside the grid)
        /// </summary>
        /// <param name="itemTable">Item Table that is the item serialization</param>
        /// <param name="gridTable">The grid which will be placed</param>
        /// <returns>Grid Response with the result if was inserted or not</returns>
        public GridResponse PlaceItem(ItemTable itemTable, GridTable gridTable)
        {
            if (gridTable == null)
            {
                return GridResponse.NoGridTableSelected;
            }

            var posOnGrid = gridTable.FindSpaceForObjectAnyDirection(itemTable);

            if (posOnGrid == null)
            {
                return GridResponse.InventoryFull;
            }

            var inventoryMessage = gridTable.PlaceItem(itemTable, posOnGrid.Value.x, posOnGrid.Value.y);

            return inventoryMessage;
        }

        public GridResponse PlaceItemInPlayerInventory(ItemTable itemTable)
        {
            var playerInventory = StaticInventoryContext.GetPlayerInventory();

            var gridAvailable = playerInventory.FindGridAvailableToFitItem(itemTable);

            var combinedWithSomeItem = TryToCombineWithSameItemInsideGrid(itemTable, gridAvailable);

            if (combinedWithSomeItem)
            {
                return GridResponse.Inserted;
            }

            return PlaceItem(itemTable, gridAvailable);
        }

        private bool TryToCombineWithSameItemInsideGrid(ItemTable itemTable, GridTable gridAvailable)
        {
            var itemStackableDataSo = itemTable.GetItemData<ItemStackableDataSo>();

            if (itemStackableDataSo == null)
            {
                return false;
            }

            var itemFoundToCombineIntoIt = gridAvailable.FindItemByPredicate(item =>
                item.ItemDataSo == itemStackableDataSo &&
                (item.GetMetadata<CountableMetadata>()?.CanCombineEntirely(itemTable) ?? false)
            );

            if (itemFoundToCombineIntoIt == null)
            {
                return false;
            }

            var countableMetadata = itemFoundToCombineIntoIt.GetMetadata<CountableMetadata>();

            return countableMetadata != null && countableMetadata.Combine(itemTable);
        }

        /// <summary>
        /// Will find a space for the item inside the list of grids.
        /// </summary>
        /// <param name="itemDataSo">Item will be inserted</param>
        /// <param name="grids">Grids to find a place for the item</param>
        /// <returns>GridResponse if the item was inserted or not</returns>
        public (ItemTable item, GridResponse gridResponse) FindPlaceForItemInGrids(ItemDataSo itemDataSo,
            List<GridTable> grids)
        {
            foreach (var gridTable in grids)
            {
                var (item, gridResponse) = PlaceItem(itemDataSo, gridTable);

                if (gridResponse == GridResponse.Inserted)
                {
                    return (item, gridResponse);
                }
            }

            return (null, GridResponse.InventoryFull);
        }

        /// <summary>
        /// Remove the item from grid or holder.
        /// </summary>
        /// <param name="itemTable">The item table will be removed/unequipped</param>
        public void RemoveItem(ItemTable itemTable)
        {
            itemTable.RemoveItselfFromGrid();
            itemTable.RemoveItselfFromHolder();
        }

        /// <summary>
        /// Will try to equip on a Holder.
        /// </summary>
        /// <param name="itemDataSo">The item data so which will be created the ItemTable and then equip.</param>
        /// <param name="itemHolder">The holder will be equipped</param>
        /// <returns>ItemTable created and the HolderResponse with the result if was equipped or not</returns>
        public (ItemTable, HolderResponse) TryEquipItem(ItemDataSo itemDataSo, Holder itemHolder)
        {
            var itemTable = StaticInventoryContext.CreateItemTable(itemDataSo);

            if (itemHolder == null)
            {
                return (null, HolderResponse.NoItemHolderSelected);
            }

            var holderResponse = TryEquipItem(itemTable, itemHolder);

            return (itemTable, holderResponse);
        }

        /// <summary>
        /// Will try to equip on a Holder.
        /// </summary>
        /// <param name="itemTable">The item which will be equipped</param>
        /// <param name="itemHolder">The holder which to equip</param>
        /// <returns>HolderResponse with the result if was equipped or not</returns>
        public HolderResponse TryEquipItem(ItemTable itemTable, Holder itemHolder)
        {
            return itemHolder == null ? HolderResponse.NoItemHolderSelected : itemHolder.TryEquipItem(itemTable);
        }

        /// <summary>
        /// Will equip the item on a Holder, even if there is another item equipped.
        /// </summary>
        /// <param name="itemDataSo">The item data so which will be equipped</param>
        /// <param name="itemHolder">The holder to equip</param>
        /// <returns>HolderResponse with the result if was equipped or not</returns>
        public HolderResponse EquipItem(ItemDataSo itemDataSo, Holder itemHolder)
        {
            var itemTable = StaticInventoryContext.CreateItemTable(itemDataSo);

            return EquipItem(itemTable, itemHolder);
        }

        /// <summary>
        /// Will equip the item on a Holder, even if there is another item equipped.
        /// </summary>
        /// <param name="itemTable">The item which will be equipped</param>
        /// <param name="itemHolder">The holder to equip</param>
        /// <returns>HolderResponse with the result if was equipped or not</returns>
        public HolderResponse EquipItem(ItemTable itemTable, Holder itemHolder)
        {
            return itemHolder == null ? HolderResponse.NoItemHolderSelected : itemHolder.Equip(itemTable);
        }


        /// <summary>
        /// Will clear all items inside a grid.
        /// </summary>
        /// <param name="grids">Grids which will be cleared</param>
        public void ClearGridTables(List<GridTable> grids)
        {
            foreach (var gridTable in grids)
            {
                ClearGridTable(gridTable);
            }
        }

        /// <summary>
        /// Clear the entire grid. Remove all items
        /// </summary>
        /// <param name="gridTable">Grid which will be cleared</param>
        public void ClearGridTable(GridTable gridTable)
        {
            gridTable.RemoveAllItems();
        }

        public EnvironmentItemHolder DropItem(AbstractItem item, Transform dropTransform)
        {
            var itemTable = item.ItemTable;

            var environmentItem = DropItem(itemTable, dropTransform);

            // Destroy Item from UI.
            Destroy(item.gameObject);

            return environmentItem;
        }

        public EnvironmentItemHolder DropItem(ItemTable itemTable, Transform dropTransform)
        {
            var prefabItem = itemTable.ItemDataSo.Prefab;

            if (prefabItem == null)
            {
                throw new ArgumentException(
                    "Cannot drop item if has no prefab set. Please define a prefab on the 'prefab' property inside " +
                    nameof(ItemDataSo));
            }

            var environmentItem = StaticInventoryContext.DropItemToEnvironmentItemHolder(itemTable, dropTransform);

            return environmentItem;
        }

        public ItemTable PickEnvironmentItem(GameObject gameObjectItem)
        {
            return gameObjectItem.TryGetComponent(out Holder environmentItem)
                ? PickEnvironmentItem(environmentItem)
                : null;
        }

        public ItemTable PickEnvironmentItem(Holder itemHolder)
        {
            var itemTable = itemHolder.GetItemEquipped();

            var gridResponse = PlaceItemInPlayerInventory(itemTable);

            if (gridResponse != GridResponse.Inserted)
            {
                return null;
            }

            Destroy(itemHolder.gameObject);

            return itemTable;
        }
    }
}