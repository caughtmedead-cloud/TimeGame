using UnityEngine;
using TimeGame.Systems.GridPlacement;
using TimeGame.Systems.Inventory.UI;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Helper class for working with container items (backpacks, chests, crates).
    /// Provides utilities for:
    /// - Creating container inventory for items that provide storage
    /// - Opening containers in the UI
    /// - Checking if items are containers
    /// - Getting/setting container contents
    /// </summary>
    public static class ContainerHelper
    {
        /// <summary>
        /// Check if an item is a container (provides storage)
        /// </summary>
        public static bool IsContainer(InventoryItemSO item)
        {
            return item != null && item.ProvidesStorage;
        }

        /// <summary>
        /// Check if a PlacedItem is a container with inventory
        /// </summary>
        public static bool IsContainer(PlacedItem placedItem)
        {
            if (placedItem == null) return false;
            InventoryItemSO itemDef = placedItem.ItemDefinition as InventoryItemSO;
            return IsContainer(itemDef);
        }

        /// <summary>
        /// Initialize container inventory for a newly created PlacedItem.
        /// Call this after creating a container item to give it storage capability.
        /// </summary>
        public static void InitializeContainerInventory(PlacedItem placedItem)
        {
            if (placedItem == null) return;

            InventoryItemSO itemDef = placedItem.ItemDefinition as InventoryItemSO;
            if (itemDef == null || !itemDef.ProvidesStorage) return;

            // Don't reinitialize if already has inventory
            if (placedItem.ContainerInventory != null) return;

            // Create inventory system for this container.
            // CRITICAL: Use 64f cell size — this matches the UI grid cell size and is read back
            // by InventoryGridVisual.Initialize() to set the grid's pixel dimensions.
            // Using a wrong value (e.g. 10f) will cause the grid to render tiny when this
            // ContainerInventory is later swapped into a grid visual via Initialize().
            placedItem.ContainerInventory = new InventorySystem(
                itemDef.StorageGridSize.x,
                itemDef.StorageGridSize.y,
                64f,
                Vector3.zero,
                itemDef.StorageMaxWeight
            );
        }

        /// <summary>
        /// Get or create container inventory for a PlacedItem.
        /// If the item doesn't have container inventory yet, creates it.
        /// Returns null if item doesn't provide storage.
        /// </summary>
        public static InventorySystem GetOrCreateContainerInventory(PlacedItem placedItem)
        {
            if (placedItem == null) return null;

            InventoryItemSO itemDef = placedItem.ItemDefinition as InventoryItemSO;
            if (itemDef == null || !itemDef.ProvidesStorage) return null;

            if (placedItem.ContainerInventory == null)
            {
                InitializeContainerInventory(placedItem);
            }

            return placedItem.ContainerInventory;
        }

        /// <summary>
        /// Open a container PlacedItem using the ContainerInteractionManager.
        /// Creates a temporary LootContainer from the PlacedItem's inventory.
        /// </summary>
        public static void OpenContainer(PlacedItem placedItem, ContainerInteractionManager containerManager)
        {
            if (placedItem == null || containerManager == null) return;

            InventoryItemSO itemDef = placedItem.ItemDefinition as InventoryItemSO;
            if (itemDef == null || !itemDef.ProvidesStorage)
            {
                Debug.LogWarning("[ContainerHelper] Cannot open - item doesn't provide storage!");
                return;
            }

            // Get or create container inventory
            InventorySystem containerInv = GetOrCreateContainerInventory(placedItem);

            // Create LootContainer representation
            LootContainer lootContainer = new LootContainer();
            ContainerCompartment compartment = new ContainerCompartment
            {
                Label = itemDef.ItemName,
                GridSize = itemDef.StorageGridSize,
                MaxWeight = itemDef.StorageMaxWeight,
                InventorySystem = containerInv
            };

            lootContainer.AddCompartment(compartment);

            // Open in UI
            containerManager.OpenContainer(lootContainer);
        }

        /// <summary>
        /// Check if a container has any items inside
        /// </summary>
        public static bool IsContainerEmpty(PlacedItem placedItem)
        {
            if (placedItem == null) return true;
            if (placedItem.ContainerInventory == null) return true;
            return placedItem.ContainerInventory.IsEmpty();
        }

        /// <summary>
        /// Get total number of items inside a container
        /// </summary>
        public static int GetContainerItemCount(PlacedItem placedItem)
        {
            if (placedItem == null || placedItem.ContainerInventory == null) return 0;
            return placedItem.ContainerInventory.GetItemCount();
        }

        /// <summary>
        /// Get current weight of items inside a container
        /// </summary>
        public static float GetContainerContentWeight(PlacedItem placedItem)
        {
            if (placedItem == null || placedItem.ContainerInventory == null) return 0f;
            return placedItem.ContainerInventory.GetCurrentWeight();
        }

        /// <summary>
        /// Calculate total weight of a container item including its contents (recursively)
        /// </summary>
        public static float GetTotalWeight(PlacedItem placedItem)
        {
            if (placedItem == null) return 0f;

            InventoryItemSO itemDef = placedItem.ItemDefinition as InventoryItemSO;
            if (itemDef == null) return 0f;

            float weight = itemDef.Weight * placedItem.StackCount;

            // Add contents weight recursively
            if (placedItem.ContainerInventory != null)
            {
                weight += GetContainerInventoryWeight(placedItem.ContainerInventory);
            }

            return weight;
        }

        /// <summary>
        /// Recursively calculate weight of an inventory system including nested containers
        /// </summary>
        private static float GetContainerInventoryWeight(InventorySystem inventory)
        {
            if (inventory == null) return 0f;

            float weight = 0f;

            foreach (PlacedItem item in inventory.GetAllItems())
            {
                InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
                if (itemDef != null)
                {
                    // Add item's base weight
                    weight += itemDef.Weight * item.StackCount;

                    // Recursively add nested container weight
                    if (item.ContainerInventory != null)
                    {
                        weight += GetContainerInventoryWeight(item.ContainerInventory);
                    }
                }
            }

            return weight;
        }

        /// <summary>
        /// Clear all items from a container
        /// </summary>
        public static void ClearContainer(PlacedItem placedItem)
        {
            if (placedItem == null || placedItem.ContainerInventory == null) return;
            placedItem.ContainerInventory.ClearAll();
        }
    }
}
