using System.Collections.Generic;
using UnityEngine;
using NewThelos.Inventory.Data;

namespace NewThelos.Systems.Inventory.Utils
{
    /// <summary>
    /// Singleton registry that maps string identifiers to ItemDefinitionSO ScriptableObjects.
    /// Updated to use our new ItemDefinitionSO instead of UGI's ItemDataSo.
    /// </summary>
    public class ItemDefinitionRegistry : MonoBehaviour
    {
        [Header("Registry Configuration")]
        [Tooltip("Array of all ItemDefinitionSO assets in the project")]
        [SerializeField] private ItemDefinitionSO[] allItems;
        
        [Header("Debug Settings")]
        [SerializeField] private bool verboseLogging = false;
        
        // Singleton instance
        private static ItemDefinitionRegistry _instance;
        
        // Dictionary for O(1) lookups
        private Dictionary<string, ItemDefinitionSO> _itemLookup;
        
        public static ItemDefinitionRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.LogError("[ItemDefinitionRegistry] ❌ No ItemDefinitionRegistry found!");
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            // Singleton pattern
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[ItemDefinitionRegistry] ⚠️ Multiple instances found! Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            BuildRegistry();
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        private void BuildRegistry()
        {
            _itemLookup = new Dictionary<string, ItemDefinitionSO>();
            
            if (allItems == null || allItems.Length == 0)
            {
                Debug.LogWarning("[ItemDefinitionRegistry] ⚠️ No items assigned to registry!");
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
                
                string itemId = item.itemId;
                
                if (_itemLookup.ContainsKey(itemId))
                {
                    duplicates++;
                    Debug.LogWarning($"[ItemDefinitionRegistry] ⚠️ Duplicate item ID: '{itemId}'");
                    continue;
                }
                
                _itemLookup.Add(itemId, item);
                validItems++;
                
                if (verboseLogging)
                {
                    Debug.Log($"[ItemDefinitionRegistry] ✅ Registered: '{itemId}'");
                }
            }
            
            Debug.Log($"[ItemDefinitionRegistry] Registry built: {validItems} valid, {nullItems} null, {duplicates} duplicates");
        }
        
        /// <summary>
        /// Get ItemDefinitionSO by its ID
        /// </summary>
        public static ItemDefinitionSO GetItemDefinition(string itemId)
        {
            if (Instance == null)
                return null;
            
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning("[ItemDefinitionRegistry] ⚠️ Cannot get item - ID is null/empty!");
                return null;
            }
            
            if (Instance._itemLookup.TryGetValue(itemId, out ItemDefinitionSO definition))
            {
                return definition;
            }
            
            Debug.LogWarning($"[ItemDefinitionRegistry] ⚠️ Item not found: '{itemId}'");
            return null;
        }
        
        public static bool HasItem(string itemId)
        {
            return Instance?._itemLookup?.ContainsKey(itemId) ?? false;
        }
        
        public static int GetRegisteredItemCount()
        {
            return Instance?._itemLookup?.Count ?? 0;
        }
    }
}