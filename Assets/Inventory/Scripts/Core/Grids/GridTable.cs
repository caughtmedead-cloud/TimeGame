using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Scripts.Core.Controllers.Save.Converters;
using Inventory.Scripts.Core.Grids.Helper;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;
using Newtonsoft.Json;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditorInternal;
#endif

namespace Inventory.Scripts.Core.Grids
{
    [Serializable]
    [JsonConverter(typeof(GridConverter))]
    public class GridTable
    {
        [JsonProperty] public int Width { get; private set; }
        [JsonProperty] public int Height { get; private set; }

        [JsonProperty] public ItemTable[,] Slots { get; private set; }

        // UI Handles
        public event Action<ItemTable> OnInsert;
        public event Action<ItemTable> OnRemove;
        private readonly List<ContainerGrids> _containerGridsInParent = new();

        public GridTable(int newWidth, int newHeight)
        {
            Width = newWidth;
            Height = newHeight;
            Slots = new ItemTable[Width, Height];
        }

        public GridResponse PlaceItem(ItemTable item, int posX, int posY)
        {
            if (!this.BoundaryCheck(posX, posY, item.Width, item.Height))
            {
                return GridResponse.OutOfBounds;
            }

            if (!OverlapCheck(posX, posY, item.Width, item.Height))
            {
                return GridResponse.Overlapping;
            }

            if (IsInsertingInsideYourself(item))
            {
                return GridResponse.InsertInsideYourself;
            }

            item.RemoveItselfFromLocation();

            for (var x = 0; x < item.Width; x++)
            {
                for (var y = 0; y < item.Height; y++)
                {
                    Slots[posX + x, posY + y] = item;
                }
            }

            item.SetGridProps(this, new Position(posX, posY), null);

            UpdateAllGridsOpened(item);

#if UNITY_EDITOR
            InternalEditorUtility.RepaintAllViews();
#endif

            return GridResponse.Inserted;
        }

        public bool OverlapCheck(int posX, int posY, int itemWidth, int itemHeight)
        {
            for (var x = 0; x < itemWidth; x++)
            {
                for (var y = 0; y < itemHeight; y++)
                {
                    var inventoryItem = GetItem(posX + x, posY + y);

                    if (inventoryItem == null) continue;

                    return false;
                }
            }

            return true;
        }

        public ItemTable GetItem(int x, int y)
        {
            try
            {
                return Slots[x, y];
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
            catch (NullReferenceException)
            {
                Debug.LogError(
                    $"{nameof(Slots)} not initialized, maybe you change something in the Editor and the reference turns null. Restart the game, everything should fix."
                        .Error());
                return null;
            }
        }

        public bool IsInsertingInsideYourself(ItemTable item)
        {
            if (item == null)
                return false;

            if (!item.IsContainer())
                return false;

            var containerMetadata = (ContainerMetadata)item.InventoryMetadata;

            var gridTables = containerMetadata.Inventories;

            if ((from gridTable in gridTables
                    from inventoryItem in gridTable.GetAllContainersFromGrid()
                    select ((ContainerMetadata)inventoryItem.InventoryMetadata).IsInsertingInsideYourself(this))
                .Any(insertingInside => insertingInside))
            {
                return true;
            }

            // TODO: Improve this to not depend on the ContainerGrids MonoBehavior.
            return _containerGridsInParent.Count != 0 &&
                   _containerGridsInParent.Any(containerGrid =>
                       containerMetadata.GetContainerGrids().Contains(containerGrid));
        }

        public void RemoveItem(ItemTable itemTable)
        {
            if (itemTable == null)
            {
                Debug.Log("Cannot remove null item...".Error());
                return;
            }

            PickUpItem(itemTable.Position.x, itemTable.Position.y);
            RemoveItemFromPreviousGrids(itemTable);
            itemTable.SetGridProps(null, null, null);

#if UNITY_EDITOR
            InternalEditorUtility.RepaintAllViews();
#endif
        }

        public void RemoveAllItems()
        {
            var allItemsFromGrid = GetAllItemsFromGrid();

            foreach (var itemTable in allItemsFromGrid)
            {
                RemoveItem(itemTable);
            }
        }

        public ItemTable PickUpItem(int x, int y)
        {
            var abstractItem = GetItem(x, y);

            if (abstractItem == null) return null;

            for (var ix = 0; ix < abstractItem.Width; ix++)
            {
                for (var iy = 0; iy < abstractItem.Height; iy++)
                {
                    Slots[abstractItem.Position.x + ix, abstractItem.Position.y + iy] = null;
                }
            }

#if UNITY_EDITOR
            InternalEditorUtility.RepaintAllViews();
#endif

            return abstractItem;
        }

        private void UpdateAllGridsOpened(ItemTable itemTable)
        {
            OnInsert?.Invoke(itemTable);
        }

        public void RemoveItemFromPreviousGrids(ItemTable itemTable)
        {
            OnRemove?.Invoke(itemTable);
        }

        public Vector2Int? FindSpaceForObjectAnyDirection(ItemTable inventoryItemToInsert)
        {
            var posOnGrid = FindSpaceForObject(inventoryItemToInsert);

            if (posOnGrid != null) return posOnGrid;

            var lastRotateState = inventoryItemToInsert.IsRotated;

            inventoryItemToInsert.Rotate();
            posOnGrid = FindSpaceForObject(inventoryItemToInsert);

            if (posOnGrid == null && lastRotateState != inventoryItemToInsert.IsRotated)
            {
                inventoryItemToInsert.Rotate();
            }

            return posOnGrid;
        }

        private Vector2Int? FindSpaceForObject(ItemTable itemToInsert)
        {
            var normalizedHeight = Height - itemToInsert.Height + 1;
            var normalizedWidth = Width - itemToInsert.Width + 1;

            for (var y = 0; y < normalizedHeight; y++)
            {
                for (var x = 0; x < normalizedWidth; x++)
                {
                    if (OverlapCheck(x, y, itemToInsert.Width,
                            itemToInsert.Height))
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }

            return null;
        }

        public ItemTable[] GetAllContainersFromGrid()
        {
            var containerItems = new HashSet<ItemTable>();

            if (Slots == null)
            {
                return containerItems.ToArray();
            }

            foreach (var inventoryItem in Slots)
            {
                if (inventoryItem?.InventoryMetadata is ContainerMetadata)
                {
                    containerItems.Add(inventoryItem);
                }
            }

            return containerItems.ToArray();
        }

        public ItemTable[] GetAllItemsFromGrid()
        {
            var inventoryItems = new HashSet<ItemTable>();

            if (Slots == null)
            {
                return inventoryItems.ToArray();
            }

            foreach (var inventoryItem in Slots)
            {
                if (inventoryItem != null)
                {
                    inventoryItems.Add(inventoryItem);
                }
            }

            return inventoryItems.ToArray();
        }

        /// <summary>
        /// Will place the item near to the position passed.
        /// </summary>
        /// <param name="itemToPlace">Item will be placed.</param>
        /// <param name="positionToBeNear">The position that will evaluate to place the item near.</param>
        public GridResponse PlaceItemNearTo(ItemTable itemToPlace, Position positionToBeNear)
        {
            var nearestPosition = NearPositionHelper.FindNearestPosition(itemToPlace, positionToBeNear, Slots);

            if (nearestPosition == null) return PlaceNearDefaultStrategy(itemToPlace);

            var gridResponse = PlaceItem(itemToPlace, nearestPosition.x, nearestPosition.y);

            return gridResponse == GridResponse.Overlapping ? PlaceNearDefaultStrategy(itemToPlace) : gridResponse;
        }

        private GridResponse PlaceNearDefaultStrategy(ItemTable itemToPlace)
        {
            var posOnGrid = FindSpaceForObjectAnyDirection(itemToPlace);

            return posOnGrid == null
                ? GridResponse.InventoryFull
                : PlaceItem(itemToPlace, posOnGrid.Value.x, posOnGrid.Value.y);
        }

        public void AddGrid(AbstractGrid abstractGrid)
        {
            var containerGrids = abstractGrid.GetComponentInParent<ContainerGrids>(true);

            if (containerGrids != null)
            {
                _containerGridsInParent.Add(containerGrids);
            }
        }

        public override string ToString()
        {
            return "Grid (" + Width + ", " + Height + ") ContainerGrids: " + _containerGridsInParent + ", Matrix: " +
                   Slots;
        }

        public ItemTable FindItem(ItemTable inventoryItem)
        {
            var allItemsFromGrid = GetAllItemsFromGrid();

            return allItemsFromGrid.FirstOrDefault(item => item == inventoryItem);
        }

        public ItemTable FindItemByPredicate(Func<ItemTable, bool> predicate)
        {
            var allItemsFromGrid = GetAllItemsFromGrid();

            return allItemsFromGrid.FirstOrDefault(item => item != null && predicate.Invoke(item));
        }
    }
}