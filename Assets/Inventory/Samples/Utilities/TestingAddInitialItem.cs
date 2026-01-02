using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using UnityEngine;

namespace Inventory.Samples.Utilities
{
    [DefaultExecutionOrder(100)]
    public class TestingAddInitialItem : MonoBehaviour
    {
        [Header("Configs")] [SerializeField] private ItemHolder itemHolder;

        [SerializeField] private Holder[] replicationHolders;

        [SerializeField] private ItemDataSo initialItemDataSo;

        private void Start()
        {
            var supplierSo = StaticInventoryContext.InventorySupplierSo;

            var holderResponse = supplierSo.EquipItem(initialItemDataSo, itemHolder);
            
            var holderData = itemHolder.GetHolderData();
            
            foreach (var replicationHolder in replicationHolders)
            {
                replicationHolder.Equip(holderData);
            }

            if (holderResponse == HolderResponse.Equipped)
            {
                Debug.Log("Equipped initial item!".Info());
            }
        }
    }
}