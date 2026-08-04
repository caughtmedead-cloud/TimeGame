using UnityEngine;
using System.Collections.Generic;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// ScriptableObject registry for looking up InventoryItemSO assets by name.
    /// Required for network synchronization where the server sends item names as
    /// strings and clients resolve them back to the actual InventoryItemSO asset.
    ///
    /// SETUP: Create one instance of this asset, place it in a Resources/ folder
    /// named exactly "InventoryItemRegistry", and populate it with all your
    /// InventoryItemSO assets (or use the "Auto-Find All Items" context menu).
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryItemRegistry", menuName = "TimeGame/Inventory/Networking/Item Registry")]
    public class InventoryItemRegistry : ScriptableObject
    {
        [Header("All Available Items")]
        [Tooltip("Add all InventoryItemSO assets here. Names must be unique.")]
        [SerializeField] private List<InventoryItemSO> allItems = new List<InventoryItemSO>();

        // Cached lookup dictionary for fast access
        private Dictionary<string, InventoryItemSO> _itemLookup;

        /// <summary>
        /// Build the lookup dictionary from the list.
        /// Called automatically on first GetItem() call.
        /// </summary>
        private void BuildLookup()
        {
            if (_itemLookup != null) return; // Already built

            _itemLookup = new Dictionary<string, InventoryItemSO>();
            
            foreach (var item in allItems)
            {
                if (item == null)
                {
                    Debug.LogWarning($"[InventoryItemRegistry] Null item in registry!", this);
                    continue;
                }

                // Check for duplicate names
                if (_itemLookup.ContainsKey(item.ItemName))
                {
                    Debug.LogError($"[InventoryItemRegistry] Duplicate item name: {item.ItemName}", this);
                    continue;
                }

                _itemLookup[item.ItemName] = item;
            }

            Debug.Log($"[InventoryItemRegistry] Loaded {_itemLookup.Count} items");
        }

        /// <summary>
        /// Get an InventoryItemSO by its name string.
        /// </summary>
        /// <param name="itemName">The ItemName field of the InventoryItemSO</param>
        /// <returns>The InventoryItemSO, or null if not found</returns>
        public InventoryItemSO GetItem(string itemName)
        {
            if (_itemLookup == null) BuildLookup();

            if (_itemLookup.TryGetValue(itemName, out InventoryItemSO item))
            {
                return item;
            }

            Debug.LogError($"[InventoryItemRegistry] Item not found: {itemName}", this);
            return null;
        }

        /// <summary>
        /// Check if an item exists in the registry.
        /// </summary>
        public bool HasItem(string itemName)
        {
            if (_itemLookup == null) BuildLookup();
            return _itemLookup.ContainsKey(itemName);
        }

        /// <summary>
        /// Get all registered item names.
        /// </summary>
        public IEnumerable<string> GetAllItemNames()
        {
            if (_itemLookup == null) BuildLookup();
            return _itemLookup.Keys;
        }

        /// <summary>
        /// Get all items in registry.
        /// </summary>
        public IReadOnlyList<InventoryItemSO> GetAllItems()
        {
            return allItems.AsReadOnly();
        }

        /// <summary>
        /// Get items by category.
        /// </summary>
        public List<InventoryItemSO> GetItemsByCategory(ItemCategory category)
        {
            List<InventoryItemSO> items = new List<InventoryItemSO>();
            
            foreach (var item in allItems)
            {
                if (item != null && item.Category == category)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        /// <summary>
        /// Get items by rarity.
        /// </summary>
        public List<InventoryItemSO> GetItemsByRarity(ItemRarity rarity)
        {
            List<InventoryItemSO> items = new List<InventoryItemSO>();
            
            foreach (var item in allItems)
            {
                if (item != null && item.Rarity == rarity)
                {
                    items.Add(item);
                }
            }

            return items;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor utility: Auto-populate registry with all InventoryItemSO assets in project.
        /// Right-click this asset in Unity and select "Auto-Find All Items"
        /// </summary>
        [ContextMenu("Auto-Find All Items")]
        private void AutoFindItems()
        {
            allItems.Clear();
            
            // Find all InventoryItemSO assets in the project
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:InventoryItemSO");
            
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                InventoryItemSO item = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItemSO>(path);
                
                if (item != null)
                {
                    allItems.Add(item);
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[InventoryItemRegistry] Auto-found {allItems.Count} items");
        }

        /// <summary>
        /// Editor utility: Validate all items have unique names
        /// </summary>
        [ContextMenu("Validate Item Names")]
        private void ValidateItemNames()
        {
            HashSet<string> names = new HashSet<string>();
            List<string> duplicates = new List<string>();

            foreach (var item in allItems)
            {
                if (item == null) continue;

                if (names.Contains(item.ItemName))
                {
                    duplicates.Add(item.ItemName);
                }
                else
                {
                    names.Add(item.ItemName);
                }
            }

            if (duplicates.Count > 0)
            {
                Debug.LogError($"[InventoryItemRegistry] Found {duplicates.Count} duplicate names: {string.Join(", ", duplicates)}", this);
            }
            else
            {
                Debug.Log($"[InventoryItemRegistry] All {allItems.Count} item names are unique!", this);
            }
        }
#endif
    }
}
