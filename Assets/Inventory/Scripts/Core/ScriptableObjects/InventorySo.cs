using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Controllers.Save;
using Inventory.Scripts.Core.Displays.Filler;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors.Display;
using UnityEngine;

namespace Inventory.Scripts.Core.ScriptableObjects
{
    [CreateAssetMenu(menuName = "Inventory/New Inventory Group")]
    public class InventorySo : ScriptableObject
    {
        [Header("Save Settings")] [SerializeField]
        private bool saveable = true;

        [SerializeField] private SaveStrategySo saveStrategySo;

        [SerializeField] private UniqueUid saveFileName;

        [Space]
        [SerializeField,
         Tooltip(
             "The pre equipments are the items which will be initially equipment for this inventory. Example the initial backpack item for a player.")]
        private List<HolderPreEquipment> preEquipments;

        /// <summary>
        /// Will hold all the equipments and containers from the player.
        /// Or if another instance, will hold the holders and the containers you want to save in a specific type of inventory so.
        /// You can create multiple inventory so to save like "dropped-items-inventory-so"
        /// </summary>
        private DisplayData _displayData;

        [Space] [SerializeReference] private List<HolderData> holders = new();

        public bool Saveable => saveable;

        public SaveStrategySo SaveStrategySo => saveStrategySo;

        public List<HolderData> Holders => holders;

        public event Action OnLoad;

        private void OnEnable()
        {
            holders.Clear();

            foreach (var holderPreEquipment in preEquipments)
            {
                var holderData = new HolderData(holderPreEquipment.holderId);
                AddHolder(holderData);
                var itemTable = StaticInventoryContext.CreateItemTable(holderPreEquipment.item);
                holderData.Equip(itemTable);
            }
        }

        /**
         * Will grab the items equipped in this inventory.
         */
        private List<ItemTable> GetEquippedItems()
        {
            return holders
                .Where(data => data.isEquipped)
                .Select(data => data.GetItemEquipped())
                .ToList();
        }

        internal void AddHolder(HolderData holderData)
        {
            if (holders.Contains(holderData)) return;

            holders.Add(holderData);
        }

        public void RemoveHolder(HolderData holderData)
        {
            holders.Remove(holderData);
        }

        /**
         * Will find space and place the item in the inventory.
         */
        public void AddItem(ItemTable itemTable)
        {
            var gridTable = FindGridAvailableToFitItem(itemTable);

            var posOnGrid = gridTable.FindSpaceForObjectAnyDirection(itemTable)!;

            gridTable.PlaceItem(itemTable, posOnGrid.Value.x, posOnGrid.Value.y);
        }

        /**
         * Will find the item and remove from the inventory
         */
        public void RemoveItem(ItemTable inventoryItem)
        {
            var gridTable = FindInventory(inventoryItem);

            gridTable.RemoveItem(inventoryItem);
        }

        public GridTable FindInventory(ItemTable inventoryItem)
        {
            // TODO: Create a GetEntireGrids. which will look into all the grids considering grids inside grids.
            var gridTables = GetGrids();

            return gridTables.Find(grid => grid.FindItem(inventoryItem) != null);
        }

        public bool HasItems()
        {
            var items = GetEquippedItems();

            return items is { Count: > 0 };
        }

        public List<GridTable> GetGrids()
        {
            var items = GetEquippedItems();

            return items.Where(item => item.IsContainer())
                .Select(item => (ContainerMetadata)item.InventoryMetadata)
                .SelectMany(metadata => metadata.Inventories)
                .ToList();
        }

        public GridTable FindGridAvailableToFitItem(ItemTable itemTable)
        {
            if (itemTable == null)
            {
                return null;
            }

            var gridTables = GetGrids();

            if (!itemTable.IsContainer())
                return (from gridTable in gridTables
                    let posOnGrid = gridTable.FindSpaceForObjectAnyDirection(itemTable)
                    where posOnGrid != null
                    select gridTable).FirstOrDefault();

            var containerMetadata = itemTable.GetMetadata<ContainerMetadata>();

            var itemInventory = containerMetadata.Inventories;

            var gridTablesFilteredFromItem = gridTables.Where(table => !itemInventory.Contains(table));

            return (from gridTable in gridTablesFilteredFromItem
                let posOnGrid = gridTable.FindSpaceForObjectAnyDirection(itemTable)
                where posOnGrid != null
                select gridTable).FirstOrDefault();
        }

        public void Save()
        {
            Save(saveStrategySo);
        }

        public void Save(SaveStrategySo strategy)
        {
            if (!saveable)
            {
                Debug.LogWarning(
                    "Cannot save inventory so if saveable is false... Activate the property to save this inventory so."
                        .SaveSystem());
                return;
            }

            Debug.Log($"Saving inventory {name}...".SaveSystem());

            strategy.Save(
                GetSaveKey(),
                JsonInventoryUtility.GetJson(GetEquippedItems())
            );
        }

        public bool Load()
        {
            if (saveStrategySo == null)
            {
                return false;
            }

            return Load(saveStrategySo);
        }

        public bool Load(SaveStrategySo strategy)
        {
            Debug.Log($"Loading inventory {name}...".SaveSystem());

            var loadedJsonItems = strategy.Load(GetSaveKey() ?? name);

            if (string.IsNullOrEmpty(loadedJsonItems))
            {
                return false;
            }

            var loadedItems = JsonInventoryUtility.ParseJson(loadedJsonItems);

            holders.Clear();
            foreach (var itemTable in loadedItems)
            {
                if (itemTable.CurrentHolder == null) continue;

                itemTable.CurrentHolder.Equip(itemTable);
                holders.Add(itemTable.CurrentHolder);
            }

            OnLoad?.Invoke();

            return true;
        }

        public string GetSaveKey()
        {
            if (saveFileName == null) return name;

            return string.IsNullOrEmpty(saveFileName.Uid) ? name : $"{name}_{saveFileName.Uid}";
        }

        public void OpenInventory()
        {
            _displayData = new DisplayData(this);

            GetEnvironmentDisplayAnchorSo().OpenDisplay(_displayData);
        }

        public void CloseInventory()
        {
            GetEnvironmentDisplayAnchorSo().CloseDisplay(_displayData);
        }

        public void ToggleInventory()
        {
            var isInventoryOpened = GetEnvironmentDisplayAnchorSo().IsInventoryOpened(this);

            if (isInventoryOpened)
            {
                CloseInventory();
                return;
            }

            OpenInventory();
        }

        private static InventoryDisplayAnchorSo GetEnvironmentDisplayAnchorSo()
        {
            return StaticInventoryContext.InventorySettingsAnchorSo.InventorySettingsSo.EnvironmentDisplayAnchorSo;
        }
    }
}