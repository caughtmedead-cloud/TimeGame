using UnityEngine;
using NewThelos.Systems.Inventory.Networking;

namespace NewThelos
{
    /// <summary>
    /// Phase 1 inventory testing script.
    /// Attach to player prefab for keyboard-driven testing.
    /// Each player controls their own inventory independently.
    /// </summary>
    public class InventoryTester : MonoBehaviour
    {
        [Header("References")]
        private NetworkedPlayerInventory inventory;
        
        [Header("Test Items")]
        [Tooltip("Array of ItemDataSo Display Names to cycle through")]
        [SerializeField] private string[] testItemNames = new string[] 
        { 
            "TestItem_1", 
            "TestItem_2", 
            "TestItem_3" 
        };
        private int currentItemIndex = 0;
        
        [Header("Test Controls")]
        [Tooltip("Add current test item to inventory")]
        [SerializeField] private KeyCode addItemKey = KeyCode.Alpha1;
        
        [Tooltip("Cycle to next test item")]
        [SerializeField] private KeyCode nextItemKey = KeyCode.Alpha2;
        
        [Tooltip("Remove last item from inventory")]
        [SerializeField] private KeyCode removeLastItemKey = KeyCode.Alpha3;
        
        [Tooltip("Print current inventory to console")]
        [SerializeField] private KeyCode printInventoryKey = KeyCode.Alpha4;
        
        [Tooltip("Clear entire inventory")]
        [SerializeField] private KeyCode clearInventoryKey = KeyCode.Alpha0;
        
        [Header("Test Configuration")]
        [Tooltip("Enable verbose logging for this tester")]
        [SerializeField] private bool verboseLogging = true;
        
        private void Awake()
        {
            // Automatically get inventory from this player GameObject
            inventory = GetComponent<NetworkedPlayerInventory>();
            
            if (inventory == null)
            {
                Debug.LogError("[InventoryTester] No NetworkedPlayerInventory found on this GameObject!");
                enabled = false;
            }
        }
        
        private void Update()
        {
            // Only process input for the local player (owner)
            if (!inventory.IsOwner)
                return;
            
            // Add current test item
            if (Input.GetKeyDown(addItemKey))
            {
                AddCurrentTestItem();
            }
            
            // Cycle to next test item
            if (Input.GetKeyDown(nextItemKey))
            {
                CycleToNextItem();
            }
            
            // Remove last item
            if (Input.GetKeyDown(removeLastItemKey))
            {
                RemoveLastItem();
            }
            
            // Print inventory
            if (Input.GetKeyDown(printInventoryKey))
            {
                PrintInventory();
            }
            
            // Clear inventory
            if (Input.GetKeyDown(clearInventoryKey))
            {
                ClearInventory();
            }
        }
        
        private void AddCurrentTestItem()
        {
            if (testItemNames.Length == 0)
            {
                Debug.LogWarning("[InventoryTester] No test items configured!");
                return;
            }
            
            string itemName = testItemNames[currentItemIndex];
            
            if (verboseLogging)
            {
                Debug.Log($"[InventoryTester] Player {inventory.Owner.ClientId} adding: {itemName}");
            }
            
            inventory.AddItem_ServerRpc(
                itemDataSOName: itemName,
                gridIndex: 0,
                posX: 0, // TODO: Could make this randomize or increment
                posY: 0,
                isRotated: false,
                stackCount: 1
            );
        }
        
        private void CycleToNextItem()
        {
            currentItemIndex = (currentItemIndex + 1) % testItemNames.Length;
            
            Debug.Log($"[InventoryTester] Now testing: {testItemNames[currentItemIndex]} " +
                      $"({currentItemIndex + 1}/{testItemNames.Length})");
        }
        
        private void RemoveLastItem()
        {
            var items = inventory.GetAllItems();
            
            if (items.Count == 0)
            {
                Debug.Log($"[InventoryTester] Player {inventory.Owner.ClientId} has no items to remove!");
                return;
            }
            
            string lastItemUID = items[items.Count - 1].itemUID;
            
            if (verboseLogging)
            {
                Debug.Log($"[InventoryTester] Player {inventory.Owner.ClientId} removing: {lastItemUID}");
            }
            
            inventory.RemoveItem_ServerRpc(lastItemUID);
        }
        
        private void PrintInventory()
        {
            int itemCount = inventory.GetItemCount();
            
            Debug.Log($"[InventoryTester] === Player {inventory.Owner.ClientId} Inventory ({itemCount} items) ===");
            
            if (itemCount == 0)
            {
                Debug.Log("  (empty)");
                return;
            }
            
            foreach (var item in inventory.GetAllItems())
            {
                Debug.Log($"  - {item}");
            }
        }
        
        private void ClearInventory()
        {
            var items = inventory.GetAllItems();
            int count = items.Count;
            
            if (count == 0)
            {
                Debug.Log($"[InventoryTester] Player {inventory.Owner.ClientId} inventory already empty!");
                return;
            }
            
            Debug.Log($"[InventoryTester] Player {inventory.Owner.ClientId} clearing {count} items...");
            
            // Remove all items (iterate backwards to avoid index issues)
            for (int i = items.Count - 1; i >= 0; i--)
            {
                inventory.RemoveItem_ServerRpc(items[i].itemUID);
            }
        }
    }
}
