using Inventory.Scripts.Core.ScriptableObjects;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers
{
    public class InventoryConfiguration : MonoBehaviour
    {
        [Header("Inventory Settings Anchor Settings")] [SerializeField]
        private InventorySettingsAnchorSo inventorySettingsAnchorSo;

        [SerializeField] private GameObject uiInventoryObject;

        [SerializeField] private InventorySettingsSo sceneInventorySettingsSo;

        [SerializeField] private InventorySo groundItemsInventorySo;

        [Header("Static Context Settings")] [SerializeField]
        private InventorySupplierSo inventorySupplierSo;

        private void Awake()
        {
            InitInventory();
        }

        private void OnEnable()
        {
            InitInventory();
        }

        private void InitInventory()
        {
            StaticInventoryContext.SetSettingsProperties(
                inventorySettingsAnchorSo,
                inventorySupplierSo,
                groundItemsInventorySo
            );
            StaticInventoryContext.SetCanvasUIGameObject(uiInventoryObject);
            inventorySettingsAnchorSo.Set(sceneInventorySettingsSo);
        }
    }
}