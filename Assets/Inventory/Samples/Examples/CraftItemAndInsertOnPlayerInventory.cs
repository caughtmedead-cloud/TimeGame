using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using UnityEngine;

namespace Inventory.Samples.Examples
{
    public class CraftItemAndInsertOnPlayerInventory : MonoBehaviour
    {
        /**
        * You can call this method and pass your Recipe object to craft the item.
        */
        public void CraftItem(Recipe recipe)
        {
            var playerInventorySo = StaticInventoryContext.GetPlayerInventory();
            var supplierSo = StaticInventoryContext.InventorySupplierSo;

            //... here you can get the list of ItemDataSo (items) that are required to build the item.
            var recipeRequirements = recipe.RecipeRequirements;

            var (canCraftItem, itemsToRemove) =
                ContainsAllItemsInPlayerInventory(recipeRequirements, playerInventorySo);

            // Cannot craft item, so just returning...
            if (!canCraftItem)
            {
                Debug.Log("Cannot craft because player doesn't have all items needed.");
                return;
            }

            // Can craft item...
            // If you want to insert the item on a player inventory, you can use the code below:
            var (item, gridResponse) =
                supplierSo.FindPlaceForItemInGrids(recipe.ItemWithBeCraft, playerInventorySo.GetGrids());

            if (gridResponse != GridResponse.Inserted) return;

            // Remove items which were used for crafting...
            foreach (var itemTable in itemsToRemove)
            {
                supplierSo.RemoveItem(itemTable);
            }

            // Item crafted was inserted on player inventory inventory...
            Debug.Log("Item crafted and inserted on player inventory! Item: " + item.ItemDataSo.DisplayName);
        }

        private (bool, List<ItemTable>) ContainsAllItemsInPlayerInventory(List<ItemDataSo> itemsRecipe,
            InventorySo playerInventorySo)
        {
            var allItemsFromGrids =
                playerInventorySo.GetGrids().SelectMany(grid => grid.GetAllItemsFromGrid()).ToList();

            return CheckRecipe(allItemsFromGrids, itemsRecipe);
        }

        private static (bool, List<ItemTable>) CheckRecipe(List<ItemTable> playerItems, List<ItemDataSo> recipeItems)
        {
            var allItemsNeededToCraft = new List<ItemTable>();

            foreach (var playerItem in recipeItems.SelectMany(recipeItem => playerItems.Where(playerItem =>
                         playerItem.ItemDataSo.Equals(recipeItem) && !allItemsNeededToCraft.Contains(playerItem))))
            {
                allItemsNeededToCraft.Add(playerItem);
            }

            // If not equal, can't craft because the player has not all items needed.
            var containsAllItemsToBuild = recipeItems.Count == allItemsNeededToCraft.Count;

            return (containsAllItemsToBuild, allItemsNeededToCraft);
        }

        /**
        * Your recipe class, here is just an example
        */
        [Serializable]
        public class Recipe
        {
            [SerializeField] private ItemDataSo itemWithBeCraft;

            [SerializeField] private List<ItemDataSo> recipeRequirements;

            public ItemDataSo ItemWithBeCraft => itemWithBeCraft;

            public List<ItemDataSo> RecipeRequirements => recipeRequirements;
        }
    }
}