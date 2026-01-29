using UnityEngine;
using NewThelos.Inventory.Runtime;
using NewThelos.Inventory.Networking;
using System.Linq;

namespace NewThelos
{
    /// <summary>
    /// Add this to your NetworkedPlayerInventory to debug position issues
    /// </summary>
    public class InventoryDebugger : MonoBehaviour
    {
        [Header("Add this component to debug inventory issues")]
        [SerializeField] private NetworkedPlayerInventory _inventory;
        
        [ContextMenu("Debug Grid State")]
        public void DebugGridState()
        {
            if (_inventory == null)
            {
                UnityEngine.Debug.LogError("[InventoryDebugger] No inventory assigned!");
                return;
            }
            
            var grid = _inventory.GetClientGrid("main_inventory");
            if (grid == null)
            {
                UnityEngine.Debug.LogError("[InventoryDebugger] Grid 'main_inventory' not found!");
                return;
            }
            
            UnityEngine.Debug.Log("=== GRID STATE DEBUG ===");
            UnityEngine.Debug.Log($"Grid Size: {grid.Width}x{grid.Height}");
            
            // Get all items using reflection since GetAllItems might not be public
            var itemsField = typeof(InventoryGrid).GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var items = itemsField?.GetValue(grid) as System.Collections.Generic.List<InventoryItem>;
            
            if (items == null)
            {
                UnityEngine.Debug.LogError("[InventoryDebugger] Could not access items list!");
                return;
            }
            
            UnityEngine.Debug.Log($"Total items in grid: {items.Count}");
            
            foreach (var item in items)
            {
                UnityEngine.Debug.Log($"  Item: {item.itemDefinitionId} | ID: {item.instanceId.Substring(0, 8)}... | Pos: ({item.posX},{item.posY}) | Rot: {item.isRotated}");
            }
            
            // Check for duplicates
            var duplicates = items.GroupBy(i => i.instanceId).Where(g => g.Count() > 1);
            foreach (var dup in duplicates)
            {
                UnityEngine.Debug.LogError($"  ⚠️ DUPLICATE INSTANCE ID: {dup.Key.Substring(0, 8)}... appears {dup.Count()} times!");
                foreach (var item in dup)
                {
                    UnityEngine.Debug.LogError($"     - At position ({item.posX},{item.posY})");
                }
            }
        }
        
        private void OnEnable()
        {
            if (_inventory == null)
                _inventory = GetComponent<NetworkedPlayerInventory>();
        }
    }
}