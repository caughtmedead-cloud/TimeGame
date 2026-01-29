using UnityEngine;
using System.Collections.Generic;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// ScriptableObject registry for looking up ItemTetrisSO assets by name.
    /// 
    /// How it works:
    /// - Server sends item name as string: "Rifle"
    /// - Clients look up the actual ItemTetrisSO using this registry
    /// - This avoids sending entire ScriptableObject data over network
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryTetrisRegistry", menuName = "TimeGame/Inventory/Item Registry")]
    public class InventoryTetrisRegistry : ScriptableObject
    {
        [Header("All Available Items")]
        [Tooltip("Add all ItemTetrisSO assets here. Names must be unique.")]
        [SerializeField] private List<ItemTetrisSO> allItems = new List<ItemTetrisSO>();

        // Cached lookup dictionary for fast access
        private Dictionary<string, ItemTetrisSO> _itemLookup;

        /// <summary>
        /// Build the lookup dictionary from the list.
        /// Called automatically on first GetItem() call.
        /// </summary>
        private void BuildLookup()
        {
            if (_itemLookup != null) return; // Already built

            _itemLookup = new Dictionary<string, ItemTetrisSO>();
            
            foreach (var item in allItems)
            {
                if (item == null)
                {
                    Debug.LogWarning($"[InventoryTetrisRegistry] Null item in registry!", this);
                    continue;
                }

                // Check for duplicate names
                if (_itemLookup.ContainsKey(item.nameString))
                {
                    Debug.LogError($"[InventoryTetrisRegistry] Duplicate item name: {item.nameString}", this);
                    continue;
                }

                _itemLookup[item.nameString] = item;
            }

            Debug.Log($"[InventoryTetrisRegistry] Loaded {_itemLookup.Count} items");
        }

        /// <summary>
        /// Get an ItemTetrisSO by its name string.
        /// </summary>
        /// <param name="itemName">The nameString field of the ItemTetrisSO (e.g., "Rifle")</param>
        /// <returns>The ItemTetrisSO, or null if not found</returns>
        public ItemTetrisSO GetItem(string itemName)
        {
            if (_itemLookup == null) BuildLookup();

            if (_itemLookup.TryGetValue(itemName, out ItemTetrisSO item))
            {
                return item;
            }

            Debug.LogError($"[InventoryTetrisRegistry] Item not found: {itemName}", this);
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

#if UNITY_EDITOR
        /// <summary>
        /// Editor utility: Auto-populate registry with all ItemTetrisSO assets in project.
        /// Right-click this asset in Unity and select "Auto-Find All Items"
        /// </summary>
        [ContextMenu("Auto-Find All Items")]
        private void AutoFindItems()
        {
            allItems.Clear();
            
            // Find all ItemTetrisSO assets in the project
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemTetrisSO");
            
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                ItemTetrisSO item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemTetrisSO>(path);
                
                if (item != null)
                {
                    allItems.Add(item);
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[InventoryTetrisRegistry] Auto-found {allItems.Count} items");
        }
#endif
    }
}
