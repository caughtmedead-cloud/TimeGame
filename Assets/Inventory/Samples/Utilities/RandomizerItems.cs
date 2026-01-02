using System.Collections.Generic;
using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Environment;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.ScriptableObjects.Datastores;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using UnityEngine;

namespace Inventory.Samples.Utilities
{
    [RequireComponent(typeof(EnvironmentContainerHolder))]
    public class RandomizerItems : MonoBehaviour
    {
        [Header("Configuration")] [SerializeField]
        private DatastoreItems datastoreItems;

        [SerializeField,
         Tooltip(
             "The probability of generating an item. If x value is 0 will possibly not generate any item, but also could generate 2/3 items if you set 3/4 on y value.")]
        private Vector2Int minMaxItemsGeneration = new(1, 4);

        [SerializeField] private bool regenerateOnReopen;

        private EnvironmentContainerHolder _environmentContainerHolder;

        private bool _alreadyGeneratedItems;

        private void Awake()
        {
            _environmentContainerHolder = GetComponent<EnvironmentContainerHolder>();
            _alreadyGeneratedItems = false;
        }

        private void Update()
        {
            if (_environmentContainerHolder.IsContainerOpened())
            {
                GenerateRandomItems();
            }
        }

        private void GenerateRandomItems()
        {
            if (_alreadyGeneratedItems && !regenerateOnReopen) return;

            var itemTable = _environmentContainerHolder.GetItemEquipped();

            var metadata = itemTable.GetMetadata<ContainerMetadata>();
            var itemsInside = metadata.GetAllItems() ?? new List<ItemTable>();

            if (itemsInside.Count > 1)
            {
                return;
            }

            var itemDataSos = GetRandomItems();

            if (itemTable.InventoryMetadata is not ContainerMetadata containerMetadata) return;

            var grids = containerMetadata.Inventories;

            var inventorySupplierSo = StaticInventoryContext.InventorySupplierSo;

            if (regenerateOnReopen)
            {
                inventorySupplierSo.ClearGridTables(grids);
            }

            foreach (var itemDataSo in itemDataSos)
            {
                inventorySupplierSo.FindPlaceForItemInGrids(itemDataSo, grids);
            }

            _alreadyGeneratedItems = true;
        }

        private List<ItemDataSo> GetRandomItems()
        {
            var itemsToGet = Random.Range(minMaxItemsGeneration.x, minMaxItemsGeneration.y);

            return datastoreItems.GetRandomItems(itemsToGet);
        }
    }
}