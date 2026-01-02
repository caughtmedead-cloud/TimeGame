using System;
using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using Newtonsoft.Json;
using UnityEngine;

namespace Inventory.Scripts.Core.Items.Metadata
{
    [Serializable]
    public class CountableMetadata : InventoryMetadata
    {
        [SerializeField] private double stack;
        [SerializeField] private double maxStack;

        [SerializeField] private double minStack;

        [SerializeField] private bool treatAsInteger;

        public double Stack => stack;

        public double MaxStack => maxStack;

        public CountableMetadata()
        {
        }

        [JsonConstructor]
        public CountableMetadata(double stack, double maxStack)
        {
            this.stack = stack;
            this.maxStack = maxStack;
        }

        public override void Awake()
        {
            base.Awake();

            stack = 1;

            var itemStackableDataSo = ItemTable.GetItemData<ItemStackableDataSo>();

            minStack = itemStackableDataSo.MinStack;
            maxStack = itemStackableDataSo.MaxStack;
            treatAsInteger = itemStackableDataSo.TreatAsInteger;
        }

        public override void EvictLoad()
        {
            base.EvictLoad();

            var itemStackableDataSo = ItemTable.GetItemData<ItemStackableDataSo>();

            minStack = itemStackableDataSo.MinStack;
            treatAsInteger = itemStackableDataSo.TreatAsInteger;
        }

        public bool CanCombineEntirely(ItemTable itemToCombine)
        {
            var countableMetadata = itemToCombine.GetMetadata<CountableMetadata>();

            if (countableMetadata == null)
            {
                return false;
            }

            if (ItemTable.ItemDataSo != itemToCombine.ItemDataSo) return false;

            var stackCombined = stack + countableMetadata.stack;

            if (stackCombined > maxStack)
            {
                return false;
            }

            return true;
        }

        public bool Combine(ItemTable itemToCombine)
        {
            var countableMetadata = itemToCombine.GetMetadata<CountableMetadata>();

            if (countableMetadata == null)
            {
                return false;
            }

            if (ItemTable.ItemDataSo != itemToCombine.ItemDataSo) return false;

            var stackCombined = stack + countableMetadata.stack;

            if (stackCombined > maxStack)
            {
                var spaceAvailable = maxStack - stack;

                if (spaceAvailable > 0)
                {
                    // There are space available to fit some items
                    stack += spaceAvailable;
                    countableMetadata.stack -= spaceAvailable;
                    itemToCombine.UpdateUI();
                    ItemTable.UpdateUI();
                    return false;
                }

                // No space available, cannot stack further
                Debug.LogWarning("Cannot stack in item. Max Stack reached...".DraggableSystem());
                return false;
            }

            stack += countableMetadata.stack;

            itemToCombine.RemoveItselfFromLocation();

            ItemTable.UpdateUI();

            return true;
        }

        public void SplitHalf()
        {
            var half = stack / 2;

            // Treat as integer if the flag is true
            if (treatAsInteger)
            {
                // Round down the value to an integer for the split
                half = Mathf.FloorToInt((float)half);
            }

            Split(half);
        }

        /// <summary>
        /// Split the stack into a new item.
        /// </summary>
        /// <param name="amount">Amount that will be separated from the original item</param>
        public void Split(double amount)
        {
            // If treatAsInteger is true, convert amount and stack to integers
            if (treatAsInteger)
            {
                // Round down the amount to an integer
                amount = Mathf.FloorToInt((float)amount);
            }

            // First, check if the amount is smaller than the minimum allowed value or if the split would leave the original stack below the minimum value.
            if (amount < minStack || amount >= stack || stack - amount < minStack)
            {
                Debug.LogWarning(
                    "Cannot split item: either the amount is too small, or it would result in a stack below the minimum value.");
                return;
            }

            var itemDataSo = ItemTable.ItemDataSo;

            var newItem = StaticInventoryContext.CreateItemTable(itemDataSo);

            var countableMetadata = newItem.GetMetadata<CountableMetadata>();

            // Set the stack for the new item
            countableMetadata.stack = amount;

            var gridResponse = AddItemToPlayerInventory(newItem);

            if (gridResponse != GridResponse.Inserted)
            {
                Debug.Log($"Could not split item... Grid Response: {gridResponse}");
                return;
            }

            // Subtract the amount from the original stack
            stack -= amount;

            // Update the UI
            ItemTable.UpdateUI();
        }

        private GridResponse AddItemToPlayerInventory(ItemTable newItem)
        {
            var currentGridTable = ItemTable.CurrentGridTable;

            return currentGridTable?.PlaceItemNearTo(newItem, ItemTable.Position) ??
                   StaticInventoryContext.AddItemToPlayerInventory(newItem);
        }
    }
}