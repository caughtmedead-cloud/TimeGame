using System;
using System.Collections.Generic;
using UnityEngine;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Serializable data structure for storing container inventory contents.
    /// Used to preserve items inside containers (backpacks, crates, etc.)
    /// through pickup/drop cycles and save/load operations.
    ///
    /// DESIGN:
    /// - Each container item (backpack, chest) has ONE ContainerItemData
    /// - ContainerItemData contains a list of CompartmentData (for multi-compartment containers)
    /// - Each CompartmentData stores a list of PlacedItemData (the items inside)
    /// - PlacedItemData is recursive - it can contain nested ContainerItemData
    /// </summary>
    [Serializable]
    public class ContainerItemData
    {
        [Tooltip("List of compartments in this container (usually just 1 for simple backpacks)")]
        public List<CompartmentData> Compartments = new List<CompartmentData>();

        /// <summary>
        /// Create container data from an existing InventorySystem
        /// </summary>
        public static ContainerItemData FromInventorySystem(InventorySystem inventorySystem)
        {
            if (inventorySystem == null) return null;

            ContainerItemData data = new ContainerItemData();
            CompartmentData compartment = new CompartmentData();

            compartment.GridSize = new Vector2Int(inventorySystem.Width, inventorySystem.Height);
            compartment.MaxWeight = inventorySystem.MaxWeight;

            // Serialize all items in the inventory
            foreach (PlacedItem item in inventorySystem.GetAllItems())
            {
                PlacedItemData itemData = PlacedItemData.FromPlacedItem(item);
                compartment.Items.Add(itemData);
            }

            data.Compartments.Add(compartment);
            return data;
        }

        /// <summary>
        /// Create container data for an item that provides storage
        /// </summary>
        public static ContainerItemData CreateEmpty(InventoryItemSO containerItem)
        {
            if (containerItem == null || !containerItem.ProvidesStorage)
                return null;

            ContainerItemData data = new ContainerItemData();
            CompartmentData compartment = new CompartmentData
            {
                GridSize = containerItem.StorageGridSize,
                MaxWeight = containerItem.StorageMaxWeight,
                Items = new List<PlacedItemData>()
            };
            data.Compartments.Add(compartment);
            return data;
        }

        /// <summary>
        /// Load this container data into an InventorySystem
        /// </summary>
        public void LoadIntoInventorySystem(InventorySystem inventorySystem, int compartmentIndex = 0)
        {
            if (inventorySystem == null) return;
            if (compartmentIndex < 0 || compartmentIndex >= Compartments.Count) return;

            CompartmentData compartment = Compartments[compartmentIndex];

            // Clear existing items
            inventorySystem.ClearAll();

            // Load each item
            foreach (PlacedItemData itemData in compartment.Items)
            {
                itemData.LoadIntoInventorySystem(inventorySystem);
            }
        }

        /// <summary>
        /// Check if this container has any items
        /// </summary>
        public bool IsEmpty()
        {
            foreach (CompartmentData compartment in Compartments)
            {
                if (compartment.Items.Count > 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Get total number of items across all compartments (recursively counts nested items)
        /// </summary>
        public int GetTotalItemCount(bool recursive = false)
        {
            int count = 0;
            foreach (CompartmentData compartment in Compartments)
            {
                count += compartment.Items.Count;

                if (recursive)
                {
                    foreach (PlacedItemData item in compartment.Items)
                    {
                        if (item.ContainerData != null)
                        {
                            count += item.ContainerData.GetTotalItemCount(true);
                        }
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Calculate total weight of all items in container (recursively includes nested containers)
        /// </summary>
        public float GetTotalWeight(bool includeContainerWeight = true)
        {
            float weight = 0f;

            foreach (CompartmentData compartment in Compartments)
            {
                foreach (PlacedItemData itemData in compartment.Items)
                {
                    // Add item's base weight
                    if (itemData.ItemDefinition != null)
                    {
                        weight += itemData.ItemDefinition.Weight * itemData.StackCount;
                    }

                    // Recursively add nested container contents
                    if (itemData.ContainerData != null)
                    {
                        weight += itemData.ContainerData.GetTotalWeight(includeContainerWeight: true);
                    }
                }
            }

            return weight;
        }
    }

    /// <summary>
    /// Data for one compartment of a container (drawer, pocket, section)
    /// </summary>
    [Serializable]
    public class CompartmentData
    {
        [Tooltip("Grid dimensions for this compartment")]
        public Vector2Int GridSize = new Vector2Int(6, 4);

        [Tooltip("Maximum weight capacity (0 = unlimited)")]
        public float MaxWeight = 0f;

        [Tooltip("Items stored in this compartment")]
        public List<PlacedItemData> Items = new List<PlacedItemData>();
    }

    /// <summary>
    /// Serializable representation of a PlacedItem.
    /// Contains all data needed to recreate the item, including:
    /// - Item definition reference
    /// - Position, rotation
    /// - Stack count
    /// - Individual item instances (for tracked items with durability/uses)
    /// - Nested container data (recursive)
    /// </summary>
    [Serializable]
    public class PlacedItemData
    {
        [Tooltip("Reference to the item definition ScriptableObject")]
        public InventoryItemSO ItemDefinition;

        [Tooltip("Anchor position in the grid")]
        public Vector2Int AnchorPosition;

        [Tooltip("Rotation direction")]
        public GridDirection Rotation;

        [Tooltip("Stack count for stackable items")]
        public int StackCount = 1;

        [Tooltip("Individual item instances (for items with durability/uses)")]
        public List<ItemInstance> ItemInstances;

        [Tooltip("Nested container data (if this item is a container with items inside)")]
        public ContainerItemData ContainerData;

        /// <summary>
        /// Network-only: the server-authoritative InstanceID for this item.
        /// Not serialized by Unity (Guid is not a Unity-serializable type).
        ///
        /// Populated by FromPlacedItem so the ID survives the PlacedItem → PlacedItemData → wire
        /// → PlacedItemData round-trip.  LoadIntoInventorySystem then stamps this ID onto the
        /// newly created PlacedItem so the client's item identity matches the server's, allowing
        /// SvrTakeItemFromContainer to locate the item by ID in _serverCompartments.
        ///
        /// Guid.Empty when loaded from a local save (non-networked path) — in that case
        /// LoadIntoInventorySystem falls back to generating a fresh Guid as before.
        /// </summary>
        [System.NonSerialized]
        public Guid InstanceId;

        /// <summary>
        /// Create PlacedItemData from an existing PlacedItem
        /// </summary>
        public static PlacedItemData FromPlacedItem(PlacedItem placedItem)
        {
            if (placedItem == null) return null;

            PlacedItemData data = new PlacedItemData
            {
                InstanceId     = placedItem.InstanceID,   // preserve identity for networking
                ItemDefinition = placedItem.ItemDefinition as InventoryItemSO,
                AnchorPosition = placedItem.AnchorPosition,
                Rotation       = placedItem.Rotation,
                StackCount     = placedItem.StackCount
            };

            // Copy item instances if this is a tracked item
            if (placedItem.IsInstanceTracked && placedItem.ItemInstances != null)
            {
                data.ItemInstances = new List<ItemInstance>(placedItem.ItemInstances);
            }

            // Recursively serialize nested container data
            if (data.ItemDefinition != null && data.ItemDefinition.ProvidesStorage)
            {
                // Check if this PlacedItem has container inventory stored
                if (placedItem.ContainerInventory != null)
                {
                    data.ContainerData = ContainerItemData.FromInventorySystem(placedItem.ContainerInventory);
                }
            }

            return data;
        }

        /// <summary>
        /// Load this item data into an InventorySystem.
        ///
        /// When InstanceId is non-empty (network snapshot path) the Guid-accepting TryAddItem
        /// overload is used so the newly created PlacedItem carries the server's authoritative ID.
        /// When InstanceId is Guid.Empty (local save / non-networked path) a fresh Guid is
        /// generated as before (preserves existing solo-mode behaviour).
        /// </summary>
        public PlacedItem LoadIntoInventorySystem(InventorySystem inventorySystem)
        {
            if (inventorySystem == null || ItemDefinition == null)
                return null;

            // For tracked items with instances, pass stackCount=0 to prevent creating pristine instances
            int stackCountToCreate = (ItemInstances != null && ItemInstances.Count > 0) ? 0 : StackCount;

            bool success;
            PlacedItem placedItem;

            if (InstanceId != Guid.Empty)
            {
                // NETWORK PATH: preserve the server-authoritative InstanceID so the client's
                // PlacedItem can be looked up by the server in SvrTakeItemFromContainer.
                success = inventorySystem.TryAddItem(
                    InstanceId,
                    ItemDefinition,
                    AnchorPosition,
                    Rotation,
                    out placedItem,
                    stackCountToCreate);
            }
            else
            {
                // LOCAL PATH (save/load, solo play): generate a fresh Guid as before.
                success = inventorySystem.TryAddItem(
                    ItemDefinition,
                    AnchorPosition,
                    Rotation,
                    out placedItem,
                    stackCountToCreate,
                    allowAutoStack: false // Don't auto-stack when loading saved data - preserve exact positions
                );
            }

            if (success && placedItem != null)
            {
                // Add the actual instances from saved data
                if (ItemInstances != null && ItemInstances.Count > 0)
                {
                    placedItem.AddInstances(ItemInstances);
                }

                // Recursively load nested container data
                if (ContainerData != null && ItemDefinition.ProvidesStorage)
                {
                    // Create inventory system for this container.
                    // CRITICAL: Use 64f — must match UI cell size so grid renders correctly
                    // when this system is swapped into a grid visual via Initialize().
                    InventorySystem containerInv = new InventorySystem(
                        ItemDefinition.StorageGridSize.x,
                        ItemDefinition.StorageGridSize.y,
                        64f,
                        Vector3.zero,
                        ItemDefinition.StorageMaxWeight
                    );

                    // Load saved items into container
                    ContainerData.LoadIntoInventorySystem(containerInv, 0);

                    // Attach container inventory to the placed item
                    placedItem.ContainerInventory = containerInv;
                }

                return placedItem;
            }

            return null;
        }
    }
}
