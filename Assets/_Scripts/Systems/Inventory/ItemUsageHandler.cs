using UnityEngine;
using TimeGame.Systems.GridPlacement;
using TimeGame.Systems.Inventory.UI;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Handles consuming items and executing their effects.
    /// Removes depleted items from inventory.
    /// </summary>
    public class ItemUsageHandler : MonoBehaviour
    {
        private void Start()
        {
            // Subscribe to context menu use event
            InventoryContextMenu contextMenu = FindObjectOfType<InventoryContextMenu>();
            if (contextMenu != null)
            {
                contextMenu.OnUseItem += HandleUseItem;
                contextMenu.OnInspectItem += HandleInspectItem;
                contextMenu.OnDropItem += HandleDropItem;
                Debug.Log("[ItemUsageHandler] Subscribed to context menu events");
            }
            else
            {
                Debug.LogWarning("[ItemUsageHandler] Could not find InventoryContextMenu in scene!");
            }
        }

        private void HandleUseItem(PlacedItem item, InventoryGridVisual grid)
        {
            if (item == null || grid == null) return;

            // Tracked items only
            if (!item.IsInstanceTracked || item.ItemInstances == null || item.ItemInstances.Count == 0)
            {
                Debug.LogWarning("[ItemUsageHandler] Cannot use non-tracked item!");
                return;
            }

            // Get first item instance (FIFO - first in, first out)
            ItemInstance instance = item.ItemInstances[0];

            // Use the item
            bool depleted = instance.UseItem();

            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            Debug.Log($"[ItemUsageHandler] Used {itemDef.ItemName}. Uses remaining: {instance.UsesRemaining}");

            // Execute item effect (bandages heal, etc.)
            ExecuteItemEffect(itemDef);

            // If depleted, remove from stack
            if (depleted)
            {
                Debug.Log($"[ItemUsageHandler] Item depleted! Removing from stack.");

                // Remove the depleted instance
                item.ItemInstances.RemoveAt(0);

                // If stack is now empty, remove entire item
                if (item.ItemInstances.Count == 0)
                {
                    grid.InventorySystem.RemoveItem(item.InstanceID);
                    Debug.Log($"[ItemUsageHandler] Removed depleted item from inventory.");
                }
                else
                {
                    // Still items in stack - just refresh visual
                    grid.RefreshAllItemVisuals();
                    Debug.Log($"[ItemUsageHandler] Stack still has {item.ItemInstances.Count} items remaining.");
                }
            }
            else
            {
                // Item still has uses - refresh visual to update uses counter
                grid.RefreshAllItemVisuals();
            }
        }

        private void HandleInspectItem(PlacedItem item, InventoryGridVisual grid)
        {
            if (item == null) return;

            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            if (itemDef == null) return;

            Debug.Log($"[ItemUsageHandler] INSPECT: {itemDef.ItemName}");
            Debug.Log($"  Description: {itemDef.Description}");
            Debug.Log($"  Size: {itemDef.Width}x{itemDef.Height}");
            Debug.Log($"  Weight: {itemDef.Weight}kg");
            Debug.Log($"  Value: {itemDef.Value}");
            Debug.Log($"  Category: {itemDef.Category}");
            Debug.Log($"  Rarity: {itemDef.Rarity}");
            Debug.Log($"  Stack Count: {item.StackCount}");

            if (item.IsInstanceTracked && item.ItemInstances != null && item.ItemInstances.Count > 0)
            {
                ItemInstance firstItem = item.ItemInstances[0];
                Debug.Log($"  Uses: {firstItem.UsesRemaining}/{itemDef.MaxUses}");
                Debug.Log($"  Durability: {firstItem.Durability}%");
                Debug.Log($"  Condition: {firstItem.Condition:F2}");
            }

            // TODO: Show actual inspect UI panel
        }

        private void HandleDropItem(PlacedItem item, InventoryGridVisual grid)
        {
            if (item == null || grid == null) return;

            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            Debug.Log($"[ItemUsageHandler] DROP: {itemDef.ItemName} (not implemented yet)");

            // TODO: Implement drop to world functionality
            // For now, just log the action
        }

        private void ExecuteItemEffect(InventoryItemSO itemDef)
        {
            if (itemDef == null) return;

            // Placeholder for item effects
            // In future, this could call itemDef.ExecuteEffect() or trigger event system
            Debug.Log($"[ItemUsageHandler] Executing effect for: {itemDef.ItemName}");

            // Example effects based on category:
            switch (itemDef.Category)
            {
                case ItemCategory.Medical:
                    Debug.Log($"  → Healing effect (restore health)");
                    // TODO: Actually heal player
                    break;

                case ItemCategory.Consumable:
                    Debug.Log($"  → Consumable effect (restore stamina/hunger)");
                    // TODO: Apply consumable effect
                    break;

                default:
                    Debug.Log($"  → Generic use effect");
                    break;
            }
        }
    }
}
