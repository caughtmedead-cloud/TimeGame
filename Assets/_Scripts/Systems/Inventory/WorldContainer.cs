using UnityEngine;
using TimeGame.Systems.Inventory.UI;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Represents a dropped container item in the 3D world (backpack, bag, etc.)
    /// Unlike WorldLootContainer, this is dynamically created when containers are dropped
    /// and links to the container item's actual inventory.
    /// 
    /// Reuses the ContainerInteractionManager left panel UI to display contents.
    /// </summary>
    [RequireComponent(typeof(WorldItem))]
    public class WorldContainer : MonoBehaviour
    {
        private WorldItem worldItem;
        private LootContainer lootContainer;
        private bool isCurrentlyOpen = false;

        public string DisplayName => worldItem?.ItemDefinition?.ItemName ?? "Container";
        public float InteractionRange => 3f; // Match PlayerItemInteraction default
        public bool IsCurrentlyOpen => isCurrentlyOpen;

        private void Awake()
        {
            worldItem = GetComponent<WorldItem>();
            
            if (worldItem == null)
            {
                Debug.LogError("[WorldContainer] Missing WorldItem component!");
                return;
            }

            // Build LootContainer from the WorldItem's container data
            BuildLootContainer();
        }

        private void BuildLootContainer()
        {
            if (worldItem == null || worldItem.ItemDefinition == null)
            {
                Debug.LogError("[WorldContainer] Cannot build loot container - missing WorldItem or definition!");
                return;
            }

            InventoryItemSO itemDef = worldItem.ItemDefinition;

            if (!itemDef.ProvidesStorage)
            {
                Debug.LogWarning($"[WorldContainer] Item {itemDef.ItemName} doesn't provide storage!");
                return;
            }

            // Create a single compartment matching the item's storage grid
            var compartment = new ContainerCompartment
            {
                Label = "Contents",
                GridSize = itemDef.StorageGridSize,
                MaxWeight = itemDef.StorageMaxWeight,
                InventorySystem = null // Will be populated from ContainerData
            };

            lootContainer = LootContainer.CreateMultiCompartment(itemDef.ItemName, new System.Collections.Generic.List<ContainerCompartment> { compartment });

            // Load existing inventory from ContainerData if it exists
            if (worldItem.ContainerData != null)
            {
                // Create the inventory system
                compartment.InventorySystem = new InventorySystem(
                    itemDef.StorageGridSize.x,
                    itemDef.StorageGridSize.y,
                    64f, // Cell size (must match UI)
                    Vector3.zero,
                    itemDef.StorageMaxWeight
                );

                // Load items from the saved data
                worldItem.ContainerData.LoadIntoInventorySystem(compartment.InventorySystem, 0);
            }
            else
            {
                // Empty container
                compartment.InventorySystem = new InventorySystem(
                    itemDef.StorageGridSize.x,
                    itemDef.StorageGridSize.y,
                    64f,
                    Vector3.zero,
                    itemDef.StorageMaxWeight
                );
            }
        }

        /// <summary>
        /// Open this container in the left panel.
        /// Called by PlayerItemInteraction when the player uses the "Open" context menu option.
        /// </summary>
        public void Open()
        {
            if (isCurrentlyOpen) return;

            if (lootContainer == null)
            {
                Debug.LogError("[WorldContainer] LootContainer is null! Cannot open.");
                return;
            }

            isCurrentlyOpen = true;

            // Show in left panel using ContainerInteractionManager
            if (ContainerInteractionManager.Instance != null)
            {
                ContainerInteractionManager.Instance.OpenContainer(lootContainer, this);
            }
            else
            {
                Debug.LogError("[WorldContainer] ContainerInteractionManager.Instance is null!");
            }
        }

        /// <summary>
        /// Close this container. Called by ContainerInteractionManager.
        /// </summary>
        public void Close()
        {
            if (!isCurrentlyOpen) return;
            isCurrentlyOpen = false;

            // Save the container's inventory back to the WorldItem
            SaveContainerData();
        }

        /// <summary>
        /// Save the current inventory state back to the WorldItem's ContainerData
        /// so it persists when picked up again.
        /// </summary>
        private void SaveContainerData()
        {
            if (worldItem == null || lootContainer == null) return;

            var compartments = lootContainer.GetCompartments();
            if (compartments.Count > 0 && compartments[0].InventorySystem != null)
            {
                // Serialize the inventory system
                worldItem.ContainerData = ContainerItemData.FromInventorySystem(compartments[0].InventorySystem);
            }
        }

        private void OnDestroy()
        {
            // Ensure we save data before destruction
            if (isCurrentlyOpen)
            {
                SaveContainerData();
                
                // Close the panel if it's still open
                if (ContainerInteractionManager.Instance != null)
                {
                    ContainerInteractionManager.Instance.CloseContainer();
                }
            }
        }
    }
}
