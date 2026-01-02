using System.Collections.Generic;
using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.ScriptableObjects;
using UnityEngine;

namespace Inventory.Scripts.Core
{
    [DefaultExecutionOrder(4)]
    public class MainPlayerInventorySettings : MonoBehaviour
    {
        [SerializeField] private InventorySo mainPlayerInventory;

        [SerializeField] private List<Holder> mainPlayerHolders;

        private void Awake()
        {
            LoadUIPlayerHolder();
        }

        private void OnEnable()
        {
            mainPlayerInventory.OnLoad += LoadUIPlayerHolder;
            StaticInventoryContext.OnOpenInventoryUI += LoadUIPlayerHolder;
        }

        private void OnDisable()
        {
            mainPlayerInventory.OnLoad -= LoadUIPlayerHolder;
            StaticInventoryContext.OnOpenInventoryUI -= LoadUIPlayerHolder;
        }

        private void LoadUIPlayerHolder()
        {
            foreach (var mainPlayerHolder in mainPlayerHolders)
            {
                var holderData = mainPlayerInventory.Holders.Find(data => data.id == mainPlayerHolder.uniqueUid.Uid);

                if (holderData == null)
                {
                    var data = new HolderData(mainPlayerHolder.uniqueUid.Uid);
                    mainPlayerInventory.AddHolder(data);
                    mainPlayerHolder.Equip(data);
                    continue;
                }

                mainPlayerHolder.Equip(holderData);
            }
        }
    }
}