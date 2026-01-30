using UnityEngine;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Simple test harness for the new inventory system.
    /// Tests both logic and visuals together.
    /// 
    /// Controls:
    /// - 1: Spawn test item at (2,2)
    /// - 2: Spawn test item at (5,5)
    /// - R: Rotate last spawned item
    /// - X: Remove item at (2,2)
    /// - C: Clear all items
    /// - W: Print weight info
    /// </summary>
    public class InventoryTestHarness : MonoBehaviour
    {
        [Header("Test Configuration")]
        [Tooltip("Grid width in cells")]
        [SerializeField] private int gridWidth = 10;

        [Tooltip("Grid height in cells")]
        [SerializeField] private int gridHeight = 10;

        [Tooltip("Cell size in pixels")]
        [SerializeField] private float cellSize = 50f;

        [Tooltip("Maximum weight capacity (0 = unlimited)")]
        [SerializeField] private float maxWeight = 100f;

        [Header("Test Items")]
        [Tooltip("Test items to spawn (create some InventoryItemSO assets)")]
        [SerializeField] private InventoryItemSO[] testItems;

        [Header("References")]
        [Tooltip("The InventoryGridVisual component to visualize with")]
        [SerializeField] private InventoryGridVisual gridVisual;

        // Runtime
        private InventorySystem inventorySystem;
        private int currentTestItemIndex = 0;
        private GridDirection currentRotation = GridDirection.Down;

        void Start()
        {
            // Create inventory system
            Vector3 gridOrigin = Vector3.zero; // Origin doesn't matter for UI
            inventorySystem = new InventorySystem(gridWidth, gridHeight, cellSize, gridOrigin, maxWeight);
            inventorySystem.EnableDebugLogging = true;

            // Initialize visual
            if (gridVisual != null)
            {
                gridVisual.Initialize(inventorySystem);
                Debug.Log("[InventoryTestHarness] Inventory system initialized and connected to visual");
            }
            else
            {
                Debug.LogError("[InventoryTestHarness] No InventoryGridVisual assigned!");
            }

            PrintInstructions();
        }

        void Update()
        {
            // Spawn test item at (2,2)
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                TestSpawnItem(new Vector2Int(2, 2));
            }

            // Spawn test item at (5,5)
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                TestSpawnItem(new Vector2Int(5, 5));
            }

            // Spawn test item at (0,0)
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                TestSpawnItem(new Vector2Int(0, 0));
            }

            // Rotate current rotation
            if (Input.GetKeyDown(KeyCode.R))
            {
                RotateCurrentDirection();
            }

            // Remove item at (2,2)
            if (Input.GetKeyDown(KeyCode.X))
            {
                TestRemoveItem(new Vector2Int(2, 2));
            }

            // Remove item at (5,5)
            if (Input.GetKeyDown(KeyCode.Z))
            {
                TestRemoveItem(new Vector2Int(5, 5));
            }

            // Clear all
            if (Input.GetKeyDown(KeyCode.C))
            {
                TestClearAll();
            }

            // Print weight info
            if (Input.GetKeyDown(KeyCode.W))
            {
                PrintWeightInfo();
            }

            // Cycle test item
            if (Input.GetKeyDown(KeyCode.N))
            {
                CycleTestItem();
            }

            // Print help
            if (Input.GetKeyDown(KeyCode.H))
            {
                PrintInstructions();
            }
        }

        private void TestSpawnItem(Vector2Int position)
        {
            if (testItems == null || testItems.Length == 0)
            {
                Debug.LogError("[InventoryTestHarness] No test items configured!");
                return;
            }

            InventoryItemSO item = testItems[currentTestItemIndex];
            if (item == null)
            {
                Debug.LogError($"[InventoryTestHarness] Test item at index {currentTestItemIndex} is null!");
                return;
            }

            Debug.Log($"[InventoryTestHarness] Attempting to spawn {item.ItemName} at {position} facing {currentRotation}");

            bool success = inventorySystem.TryAddItem(item, position, currentRotation, out PlacedItem placedItem);

            if (success)
            {
                Debug.Log($"[InventoryTestHarness] ✓ Successfully placed {item.ItemName} (ID: {placedItem.InstanceID})");
            }
            else
            {
                Debug.LogWarning($"[InventoryTestHarness] ✗ Failed to place {item.ItemName} - space occupied or out of bounds");
            }
        }

        private void TestRemoveItem(Vector2Int position)
        {
            Debug.Log($"[InventoryTestHarness] Attempting to remove item at {position}");

            PlacedItem item = inventorySystem.GetItemAt(position);
            if (item != null)
            {
                Debug.Log($"[InventoryTestHarness] Found {item.ItemDefinition.ItemName} at {position}");
            }

            bool success = inventorySystem.RemoveItemAt(position);

            if (success)
            {
                Debug.Log($"[InventoryTestHarness] ✓ Successfully removed item at {position}");
            }
            else
            {
                Debug.LogWarning($"[InventoryTestHarness] ✗ No item found at {position}");
            }
        }

        private void TestClearAll()
        {
            Debug.Log("[InventoryTestHarness] Clearing all items from inventory");
            int count = inventorySystem.GetItemCount();
            inventorySystem.ClearAll();
            Debug.Log($"[InventoryTestHarness] ✓ Cleared {count} items");
        }

        private void RotateCurrentDirection()
        {
            currentRotation = (GridDirection)(((int)currentRotation + 1) % 4);
            Debug.Log($"[InventoryTestHarness] Current rotation: {currentRotation} ({(int)currentRotation * 90}°)");
        }

        private void CycleTestItem()
        {
            if (testItems == null || testItems.Length == 0) return;

            currentTestItemIndex = (currentTestItemIndex + 1) % testItems.Length;
            InventoryItemSO item = testItems[currentTestItemIndex];
            Debug.Log($"[InventoryTestHarness] Current test item: {(item != null ? item.ItemName : "NULL")} ({currentTestItemIndex + 1}/{testItems.Length})");
        }

        private void PrintWeightInfo()
        {
            float current = inventorySystem.GetCurrentWeight();
            float max = inventorySystem.MaxWeight;
            float percentage = inventorySystem.GetWeightPercentage() * 100f;
            
            Debug.Log($"[InventoryTestHarness] Weight: {current:F1}/{max:F1}kg ({percentage:F0}%)");
            Debug.Log($"[InventoryTestHarness] Items in inventory: {inventorySystem.GetItemCount()}");
        }

        private void PrintInstructions()
        {
            Debug.Log("=== INVENTORY TEST HARNESS ===");
            Debug.Log("1 - Spawn item at (2,2)");
            Debug.Log("2 - Spawn item at (5,5)");
            Debug.Log("3 - Spawn item at (0,0)");
            Debug.Log("R - Rotate next spawn");
            Debug.Log("N - Cycle test item");
            Debug.Log("X - Remove item at (2,2)");
            Debug.Log("Z - Remove item at (5,5)");
            Debug.Log("C - Clear all");
            Debug.Log("W - Print weight info");
            Debug.Log("H - Print this help");
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            // On-screen instructions
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.Label("=== INVENTORY TEST ===", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(5);
            GUILayout.Label("1/2/3 - Spawn at position");
            GUILayout.Label("R - Rotate (" + currentRotation + ")");
            GUILayout.Label("N - Cycle item (" + (testItems != null && testItems.Length > 0 && testItems[currentTestItemIndex] != null ? testItems[currentTestItemIndex].ItemName : "NONE") + ")");
            GUILayout.Label("X/Z - Remove at position");
            GUILayout.Label("C - Clear all");
            GUILayout.Label("W - Weight info");
            GUILayout.Space(10);
            GUILayout.Label($"Items: {(inventorySystem != null ? inventorySystem.GetItemCount().ToString() : "0")}");
            if (inventorySystem != null)
            {
                GUILayout.Label($"Weight: {inventorySystem.GetCurrentWeight():F1}/{inventorySystem.MaxWeight:F1}kg");
            }
            GUILayout.EndArea();
        }
#endif
    }
}
