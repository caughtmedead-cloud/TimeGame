using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Grids.Renderer;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using Newtonsoft.Json;
using UnityEngine;

namespace Inventory.Scripts.Core.Items.Metadata
{
    [Serializable]
    public class ContainerMetadata : InventoryMetadata
    {
        [JsonProperty]
        [field: SerializeReference]
        public List<GridTable> Inventories { get; private set; }

        private List<ContainerGrids> UIGridsOpened { get; } = new();

        private RendererGrids _rendererGrids = new RendererGrids2D();

        public override void Awake()
        {
            base.Awake();

            SetProps();
        }

        private void SetProps()
        {
            Inventories ??= new List<GridTable>();

            var containerGridsPrefab = GetPrefabGrids();
            var abstractGrids = containerGridsPrefab.GetAbstractGrids();

            foreach (var abstractGrid in abstractGrids)
            {
                Inventories.Add(new GridTable(abstractGrid.GridWidth, abstractGrid.GridHeight));
            }
        }

        public ContainerGrids OpenInventory(Transform parentTransform)
        {
            if (parentTransform == null)
            {
                Debug.LogError("Opening Inventory. ParentTransform cannot be null...".Error());
                return null;
            }

            var containerGridsPrefab = GetPrefabGrids();

            var containerGrids =
                _rendererGrids.Hydrate(parentTransform, containerGridsPrefab, Inventories);

            UIGridsOpened.Add(containerGrids);

            return containerGrids;
        }

        public void CloseInventory(ContainerGrids containerGrids)
        {
            if (UIGridsOpened.Count == 0) return;

            if (containerGrids == null) return;

            _rendererGrids.Dehydrate(containerGrids, Inventories);

            UIGridsOpened.Remove(containerGrids);
        }

        public bool IsInsertingInsideYourself(GridTable grid)
        {
            var gridTablesList = Inventories;

            if (gridTablesList.Count == 0) return false;

            if (gridTablesList.Contains(grid))
            {
                return true;
            }

            foreach (var gridTable in gridTablesList)
            {
                foreach (var containerItem in gridTable.GetAllContainersFromGrid())
                {
                    var containerItemInventoryMetadata = (ContainerMetadata)containerItem.InventoryMetadata;

                    var containsGrid = containerItemInventoryMetadata.Inventories
                        .Contains(grid);

                    if (containsGrid)
                    {
                        return true;
                    }

                    if (containerItemInventoryMetadata.IsInsertingInsideYourself(grid))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public GridResponse PlaceItemInInventory(ItemTable item)
        {
            var gridTables = Inventories;

            GridResponse? status = null;

            var firstRotation = item.IsRotated;

            foreach (var gridTable in gridTables)
            {
                var posInGrid = gridTable.FindSpaceForObjectAnyDirection(item);

                if (posInGrid == null) continue;

                if (gridTable == item.CurrentGridTable)
                {
                    status = GridResponse.AlreadyInserted;
                    break;
                }

                status = gridTable.PlaceItem(item, posInGrid.Value.x, posInGrid.Value.y);

                if (status != GridResponse.Inserted) continue;

                var abstractItem = item.GetAbstractItem();

                if (abstractItem != null)
                {
                    GameObject.Destroy(abstractItem.gameObject);
                }

                break;
            }

            if (status != GridResponse.Inserted && item.IsRotated != firstRotation)
            {
                item.Rotate();
            }

            return status ?? GridResponse.InventoryFull;
        }

        public bool ContainsSpaceForItem(AbstractItem selectedInventoryItem)
        {
            var lastRotateState = selectedInventoryItem.ItemTable.IsRotated;

            var gridTables = Inventories;

            var availablePosOnGrid = gridTables
                .Select(gridTable => gridTable.FindSpaceForObjectAnyDirection(selectedInventoryItem.ItemTable))
                .FirstOrDefault(posInGrid => posInGrid != null);

            if (lastRotateState != selectedInventoryItem.ItemTable.IsRotated)
            {
                selectedInventoryItem.Rotate();
            }

            return availablePosOnGrid.HasValue;
        }

        public List<ContainerGrids> GetContainerGrids()
        {
            return UIGridsOpened;
        }

        private ContainerGrids GetPrefabGrids()
        {
            var itemContainerDataSo = (ItemContainerDataSo)ItemTable.ItemDataSo;

            return itemContainerDataSo.ContainerGrids;
        }

        public List<ItemTable> GetAllItems(bool recursive = false)
        {
            if (recursive)
            {
                var itemTables = new List<ItemTable>();

                AddAllItemsRecursive(itemTables, Inventories);
                
                return itemTables;
            }

            return Inventories.SelectMany(table => table.GetAllItemsFromGrid()).ToList();
        }

        private void AddAllItemsRecursive(List<ItemTable> itemTables, List<GridTable> gridTables)
        {
            foreach (var allItemsFromGrid in gridTables.Select(gridTable => gridTable.GetAllItemsFromGrid()))
            {
                itemTables.AddRange(allItemsFromGrid);

                foreach (var itemTable in allItemsFromGrid)
                {
                    var containerMetadata = itemTable.GetMetadata<ContainerMetadata>();
                    
                    if (containerMetadata == null) continue;

                    AddAllItemsRecursive(itemTables, containerMetadata.Inventories);
                }
            }
        }
    }
}