using System.Collections.Generic;
using UnityEngine;
using Inventory.Scripts.Core.ScriptableObjects.Items;

namespace NewThelos.Systems.Inventory.Utils
{
    /// <summary>
    /// Singleton registry that maps string identifiers to ItemDataSo ScriptableObjects.
    /// 
    /// WHY THIS EXISTS:
    /// - ScriptableObject references can't be sent over the network
    /// - FishNet can only serialize primitive types and structs
    /// - Solution: Send item names as strings, resolve to ItemDataSo on clients via this registry
    /// 
    /// USAGE:
    /// 1. Attach this component to your NetworkManager GameObject
    /// 2. Populate the allItems array in the Inspector with all ItemDataSo assets
    /// 3. Call ItemDataRegistry.GetItemByName("ItemName") to resolve string → ItemDataSo
    /// 
    /// IMPORTANT: 
    /// - This must be populated BEFORE any NetworkedPlayerInventory components initialize
    /// - All clients need the same ItemDataSo assets in their project
    /// </summary>
    public class ItemDataRegistry : MonoBehaviour
    {
        [Header("Registry Configuration")]
        [Tooltip("Array of all ItemDataSo assets in the project. Populate this in the Inspector.")]
        [SerializeField] private ItemDataSo[] allItems;
        
        [Header("Debug Settings")]
        [SerializeField] private bool verboseLogging = false;
        
        // Singleton instance
        private static ItemDataRegistry _instance;
        
        // Dictionary for O(1) lookups
        private Dictionary<string, ItemDataSo> _itemLookup;
        
        /// <summary>
        /// Singleton accessor. Returns null if not initialized.
        /// </summary>
        public static ItemDataRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.LogError("[ItemDataRegistry] ❌ No ItemDataRegistry found! " +
                                   "Attach ItemDataRegistry component to NetworkManager GameObject.");
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            // Singleton pattern
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[ItemDataRegistry] ⚠️ Multiple ItemDataRegistry instances found! Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            
            // Build lookup dictionary
            BuildRegistry();
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        /// <summary>
        /// Builds the internal dictionary from the allItems array.
        /// Called automatically on Awake().
        /// </summary>
        private void BuildRegistry()
        {
            _itemLookup = new Dictionary<string, ItemDataSo>();
            
            if (allItems == null || allItems.Length == 0)
            {
                Debug.LogWarning("[ItemDataRegistry] ⚠️ No items assigned to registry! " +
                                 "Populate the 'All Items' array in the Inspector.");
                return;
            }
            
            int validItems = 0;
            int nullItems = 0;
            int duplicates = 0;
            
            foreach (var item in allItems)
            {
                if (item == null)
                {
                    nullItems++;
                    continue;
                }
                
                string itemName = item.name;
                
                if (_itemLookup.ContainsKey(itemName))
                {
                    duplicates++;
                    Debug.LogWarning($"[ItemDataRegistry] ⚠️ Duplicate item name detected: '{itemName}'. " +
                                     "Only the first instance will be used.");
                    continue;
                }
                
                _itemLookup.Add(itemName, item);
                validItems++;
                
                if (verboseLogging)
                {
                    Debug.Log($"[ItemDataRegistry] ✅ Registered item: '{itemName}'");
                }
            }
            
            Debug.Log($"[ItemDataRegistry] Registry built: {validItems} valid items, " +
                      $"{nullItems} null entries, {duplicates} duplicates.");
        }
        
        /// <summary>
        /// Gets an ItemDataSo by its name.
        /// </summary>
        /// <param name="itemName">The name of the ItemDataSo asset (e.g., "Medkit", "AmmoCrate")</param>
        /// <returns>The ItemDataSo if found, null otherwise</returns>
        public static ItemDataSo GetItemByName(string itemName)
        {
            if (Instance == null)
            {
                Debug.LogError("[ItemDataRegistry] ❌ Cannot get item - registry not initialized!");
                return null;
            }
            
            if (string.IsNullOrEmpty(itemName))
            {
                Debug.LogWarning("[ItemDataRegistry] ⚠️ Cannot get item - name is null or empty!");
                return null;
            }
            
            if (Instance._itemLookup.TryGetValue(itemName, out ItemDataSo itemData))
            {
                return itemData;
            }
            
            Debug.LogWarning($"[ItemDataRegistry] ⚠️ Item not found: '{itemName}'. " +
                             "Make sure it's in the registry array.");
            return null;
        }
        
        /// <summary>
        /// Checks if an item exists in the registry.
        /// </summary>
        /// <param name="itemName">The name of the ItemDataSo to check</param>
        /// <returns>True if the item exists in the registry</returns>
        public static bool HasItem(string itemName)
        {
            if (Instance == null || string.IsNullOrEmpty(itemName))
                return false;
            
            return Instance._itemLookup.ContainsKey(itemName);
        }
        
        /// <summary>
        /// Gets the total number of registered items.
        /// </summary>
        public static int GetRegisteredItemCount()
        {
            return Instance?._itemLookup?.Count ?? 0;
        }
        
        /// <summary>
        /// Debug method to print all registered items.
        /// </summary>
        [ContextMenu("Debug: List All Registered Items")]
        private void DebugListAllItems()
        {
            if (_itemLookup == null || _itemLookup.Count == 0)
            {
                Debug.Log("[ItemDataRegistry] No items registered.");
                return;
            }
            
            Debug.Log($"[ItemDataRegistry] === Registered Items ({_itemLookup.Count}) ===");
            foreach (var kvp in _itemLookup)
            {
                Debug.Log($"  - {kvp.Key}");
            }
        }
    }
}