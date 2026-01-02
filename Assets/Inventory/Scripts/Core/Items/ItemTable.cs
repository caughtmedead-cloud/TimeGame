using System;
using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.Items.Options;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using Newtonsoft.Json;
using UnityEngine;

namespace Inventory.Scripts.Core.Items
{
    [Serializable]
    public class ItemTable
    {
        public ItemDataSo ItemDataSo { get; private set; }

        [JsonProperty] public bool IsRotated { get; private set; }

        [JsonIgnore] public int Width => !IsRotated ? ItemDataSo.DimensionsSo.Width : ItemDataSo.DimensionsSo.Height;

        [JsonIgnore] public int Height => !IsRotated ? ItemDataSo.DimensionsSo.Height : ItemDataSo.DimensionsSo.Width;

        [JsonProperty] public Position Position { get; private set; }

        [JsonIgnore]
        [field: SerializeReference]
        public GridTable CurrentGridTable { get; private set; }

        [JsonProperty]
        [field: SerializeReference]
        public HolderData CurrentHolder { get; private set; }

        [JsonProperty]
        [field: SerializeReference]
        public InventoryMetadata InventoryMetadata { get; internal set; }

        [JsonIgnore] public ItemOptions ItemOptions;

        public event Action<ItemUIUpdater> OnUpdateUI;

        /// <summary>
        /// Constructor to create an Item in the inventory.
        /// </summary>
        /// <param name="itemDataSo">The data that this item will represent.</param>
        /// <param name="isDragRepresentation">Avoid creating metadata if is a drag representation</param>
        public ItemTable(ItemDataSo itemDataSo, bool isDragRepresentation = false)
        {
            ItemDataSo = itemDataSo;
            ItemOptions = new ItemOptions(this);

            if (ItemDataSo != null && InventoryMetadata == null && !isDragRepresentation)
            {
                InventoryMetadata = StaticMetadataCreator.CreateMetadata(this);
            }
        }

        [JsonConstructor]
        public ItemTable(ItemDataSo itemDataSo, bool isRotated, Position position, HolderData currentHolder,
            InventoryMetadata inventoryMetadata)
        {
            ItemDataSo = itemDataSo;
            IsRotated = isRotated;
            Position = position;
            CurrentHolder = currentHolder;
            InventoryMetadata = inventoryMetadata;
            ItemOptions = new ItemOptions(this);

            InventoryMetadata.ItemTable = this;
            InventoryMetadata.EvictLoad();
            ItemOptions.UpdateOptions();
        }

        public void SetGridProps(GridTable gridTable, Position position, HolderData holderData)
        {
            CurrentGridTable = gridTable;
            Position = position;
            CurrentHolder = holderData;
            UpdateUI();
        }

        public bool IsContainer()
        {
            return InventoryMetadata is ContainerMetadata;
        }

        public bool IsEquipped()
        {
            return CurrentHolder is { isEquipped: true };
        }

        public void Rotate()
        {
            IsRotated = !IsRotated;
        }

        public void RemoveItselfFromLocation()
        {
            RemoveItselfFromGrid();
            RemoveItselfFromHolder();
        }

        public void RemoveItselfFromGrid()
        {
            CurrentGridTable?.RemoveItem(this);
            CurrentGridTable = null;
        }

        public void RemoveItselfFromHolder()
        {
            if (CurrentHolder == null) return;

            CurrentHolder.UnEquip();
            CurrentHolder = null;
        }

        public AbstractItem GetAbstractItem()
        {
            var abstractGridSelectedAnchorSo = StaticInventoryContext.GetAbstractGridSelectedAnchorSo();

            if (abstractGridSelectedAnchorSo.isSet && abstractGridSelectedAnchorSo.Value != null)
            {
                return abstractGridSelectedAnchorSo.Value.GetAbstractItem(this);
            }

            var itemHolderSelectedAnchorSo = StaticInventoryContext.GetItemHolderSelectedAnchorSo();

            return itemHolderSelectedAnchorSo.isSet && itemHolderSelectedAnchorSo.Value != null
                ? itemHolderSelectedAnchorSo.Value.GetAbstractItem()
                : null;
        }

        /**
         * Will grab the InventoryMetadata based on the type you pass... If not correct type, will return null.
         */
        public T GetMetadata<T>() where T : InventoryMetadata
        {
            if (InventoryMetadata is T correctMetadata)
            {
                return correctMetadata;
            }

            return null;
        }

        /**
         * Will grab the ItemData based on the type you pass... If not correct type, will return null.
         */
        public T GetItemData<T>() where T : ItemDataSo
        {
            if (ItemDataSo is T correctItemType)
            {
                return correctItemType;
            }

            return null;
        }

        public void UpdateUI()
        {
            OnUpdateUI?.Invoke(new ItemUIUpdater());
        }

        public void UpdateUI(ItemUIUpdater updater)
        {
            OnUpdateUI?.Invoke(updater);
        }

        public override string ToString()
        {
            return ItemDataSo.DisplayName + " (" + ItemDataSo.uniqueUid.Uid + ")";
        }
    }
}