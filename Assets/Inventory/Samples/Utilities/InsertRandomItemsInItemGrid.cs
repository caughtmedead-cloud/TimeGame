using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Environment;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.ScriptableObjects.Datastores;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using UnityEngine;

namespace Inventory.Samples.Utilities
{
    public class InsertRandomItemsInItemGrid : MonoBehaviour
    {
        [SerializeField] private DatastoreItems datastoreItems;

        [SerializeField, Tooltip("The quantity of items that will be randomly inserted in the Environment Container.")]
        private int countRandomItemsInsert;

        [Header("Container Configuration")] [SerializeField]
        private bool tryGetComponentInUpdateIfNotFound = true;


        private EnvironmentContainerHolder _environmentContainerHolder;
        private ContainerMetadata _containerMetadata;

        private bool _notFoundEnvironmentContainer;

        private void Start()
        {
            _environmentContainerHolder = GetComponent<EnvironmentContainerHolder>();

            // Get the grid from environment container holder.
            _containerMetadata = GetContainerMetadataFromEnvironmentContainer();
        }

        private void Update()
        {
            if (tryGetComponentInUpdateIfNotFound && _notFoundEnvironmentContainer)
            {
                _containerMetadata = GetContainerMetadataFromEnvironmentContainer();
                _notFoundEnvironmentContainer = false;
            }
        }

        private ContainerMetadata GetContainerMetadataFromEnvironmentContainer()
        {
            var itemTable = _environmentContainerHolder.GetItemEquipped();

            if (itemTable == null)
            {
                _notFoundEnvironmentContainer = true;

                if (!tryGetComponentInUpdateIfNotFound)
                {
                    Debug.LogWarning(
                        "Item table not found in the environment container. It might be a problem of start method order of execution... " +
                        "Active the property 'tryGetComponentInUpdateIfNotFound' in InsertRandomItemsInItemGrid to find in update method.");
                }

                return null;
            }

            if (itemTable.InventoryMetadata is ContainerMetadata containerMetadata)
            {
                return containerMetadata;
            }

            return null;
        }

        public void InsertRandomItemsInsideTheContainer()
        {
            for (var i = 0; i < countRandomItemsInsert; i++)
            {
                var itemDataSo = datastoreItems.GetRandomItem();

                InsertInsideContainerEnvironment(itemDataSo);
            }
        }

        private void InsertInsideContainerEnvironment(ItemDataSo itemDataSo)
        {
            var inventorySupplierSo = StaticInventoryContext.InventorySupplierSo;

            foreach (var gridTable in _containerMetadata.Inventories)
            {
                var (_, inventoryMessages) = inventorySupplierSo.PlaceItem(itemDataSo, gridTable);

                if (inventoryMessages == GridResponse.Inserted)
                {
                    break;
                }
            }
        }
    }
}