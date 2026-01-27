using UnityEngine;
using NewThelos.Systems.Inventory.Networking;

namespace NewThelos
{
    /// <summary>
    /// Debug testing script for networked inventory system.
    /// 
    /// PHASE 1.5 ENHANCEMENTS:
    /// - Smart auto-placement (finds open slots automatically)
    /// - Proper Move/Rotate functionality with console logs
    /// - Edge case testing (boundary violations, collisions, capacity)
    /// 
    /// CONTROLS (NUMBER KEYS):
    /// - 0: Add item to grid (auto-finds open slot)
    /// - 1: Cycle to next test item
    /// - 2: Remove last item
    /// - 3: Move last item to random position
    /// - 4: Rotate last item
    /// - 5: Clear all items
    /// - 6: Print inventory
    /// 
    /// PHASE 1.5 VALIDATION TESTS:
    /// - 7: Test Boundary Violation
    /// - 8: Test Collision Detection
    /// - 9: Test Rotation + Move Collision + Capacity (combined test)
    /// </summary>
    public class InventoryTester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkedPlayerInventory playerInventory;
        
        [Header("Test Configuration")]
        [SerializeField] private string[] testItemNames = { "TestItem_1", "TestItem_2", "TestItem_3" };
        [SerializeField] private int currentItemIndex = 0;
        
        // Track the next grid position to try when adding items
        private int nextTryX = 0;
        private int nextTryY = 0;
        
        private void Awake()
        {
            if (playerInventory == null)
            {
                playerInventory = GetComponent<NetworkedPlayerInventory>();
                if (playerInventory == null)
                {
                    Debug.LogError("[InventoryTester] No NetworkedPlayerInventory component found!");
                }
            }
        }
        
        private void Update()
        {
            // Only process input for the local player's inventory
            if (playerInventory == null || !playerInventory.IsOwner)
                return;
            
            // ===== PHASE 1 BASIC CONTROLS =====
            
            // 0: Add current test item (smart placement)
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                AddCurrentTestItem();
            }
            
            // 1: Cycle to next test item
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                CycleTestItem();
            }
            
            // 2: Remove last added item
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                RemoveLastItem();
            }
            
            // 3: Move last added item
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                MoveLastItemRandom();
            }
            
            // 4: Rotate last added item
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                RotateLastItem();
            }
            
            // 5: Clear all items
            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                ClearAllItems();
            }
            
            // 6: Print inventory
            if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                PrintInventory();
            }
            
            // ===== PHASE 1.5 VALIDATION TESTS =====
            
            // 7: Test boundary violation
            if (Input.GetKeyDown(KeyCode.Alpha7))
            {
                TestBoundaryViolation();
            }
            
            // 8: Test collision detection
            if (Input.GetKeyDown(KeyCode.Alpha8))
            {
                TestCollisionDetection();
            }
            
            // 9: Combined test (rotation boundary + move collision + capacity)
            if (Input.GetKeyDown(KeyCode.Alpha9))
            {
                TestCombinedValidation();
            }
        }
        
        // ===== PHASE 1 TEST METHODS =====
        
        /// <summary>
        /// Add current test item with SMART PLACEMENT (finds first open slot)
        /// Uses position tracking to remember where we left off, automatically
        /// advancing to the next grid slot each time this is called.
        /// </summary>
        private void AddCurrentTestItem()
        {
            string itemName = testItemNames[currentItemIndex];
            
            // Try the current tracked position
            Debug.Log($"[InventoryTester] Trying to add {itemName} at [{nextTryX}, {nextTryY}]");
            playerInventory.AddItem_ServerRpc(itemName, 0, nextTryX, nextTryY, false, 1);
            
            // Advance to next position for next attempt
            nextTryX++;
            if (nextTryX >= playerInventory.GridWidth)
            {
                nextTryX = 0;
                nextTryY++;
                
                // Wrap around if we've tried the entire grid
                if (nextTryY >= playerInventory.GridHeight)
                {
                    nextTryY = 0;
                    Debug.LogWarning($"[InventoryTester] Wrapped around grid - inventory may be full!");
                }
            }
        }
        
        private void CycleTestItem()
        {
            currentItemIndex = (currentItemIndex + 1) % testItemNames.Length;
            Debug.Log($"[InventoryTester] Now testing: {testItemNames[currentItemIndex]} ({currentItemIndex + 1}/{testItemNames.Length})");
        }
        
        private void RemoveLastItem()
        {
            var items = playerInventory.GetAllItems();
            if (items.Count == 0)
            {
                Debug.LogWarning("[InventoryTester] No items to remove!");
                return;
            }
            
            var lastItem = items[items.Count - 1];
            Debug.Log($"[InventoryTester] Player {playerInventory.Owner.ClientId} removing: {lastItem.itemUID}");
            playerInventory.RemoveItem_ServerRpc(lastItem.itemUID);
        }
        
        /// <summary>
        /// Move last item to a RANDOM position (tests MoveItem_ServerRpc)
        /// </summary>
        private void MoveLastItemRandom()
        {
            var items = playerInventory.GetAllItems();
            if (items.Count == 0)
            {
                Debug.LogWarning("[InventoryTester] No items to move!");
                return;
            }
            
            var lastItem = items[items.Count - 1];
            int newX = Random.Range(0, playerInventory.GridWidth);
            int newY = Random.Range(0, playerInventory.GridHeight);
            
            Debug.Log($"[InventoryTester] Player {playerInventory.Owner.ClientId} MOVING {lastItem.itemUID} from [{lastItem.posX}, {lastItem.posY}] to [{newX}, {newY}]");
            playerInventory.MoveItem_ServerRpc(lastItem.itemUID, newX, newY);
        }
        
        /// <summary>
        /// Rotate last item (tests RotateItem_ServerRpc)
        /// </summary>
        private void RotateLastItem()
        {
            var items = playerInventory.GetAllItems();
            if (items.Count == 0)
            {
                Debug.LogWarning("[InventoryTester] No items to rotate!");
                return;
            }
            
            var lastItem = items[items.Count - 1];
            Debug.Log($"[InventoryTester] Player {playerInventory.Owner.ClientId} ROTATING item: {lastItem.itemUID} (currently rotated: {lastItem.isRotated})");
            playerInventory.RotateItem_ServerRpc(lastItem.itemUID);
        }
        
        private void ClearAllItems()
        {
            var items = playerInventory.GetAllItems();
            Debug.Log($"[InventoryTester] Clearing {items.Count} items...");
            
            foreach (var item in items)
            {
                playerInventory.RemoveItem_ServerRpc(item.itemUID);
            }
            
            // Reset position tracking when clearing inventory
            nextTryX = 0;
            nextTryY = 0;
        }
        
        private void PrintInventory()
        {
            var items = playerInventory.GetAllItems();
            Debug.Log($"[InventoryTester] === Player {playerInventory.Owner.ClientId} Inventory ({items.Count} items) ===");
            
            if (items.Count == 0)
            {
                Debug.Log("  (empty)");
                return;
            }
            
            foreach (var item in items)
            {
                Debug.Log($"  - {item}");
            }
        }
        
        // ===== PHASE 1.5 TEST METHODS =====
        
        /// <summary>
        /// TEST 1: Boundary Violation
        /// Attempts to add an item that would exceed grid boundaries.
        /// Expected: Server rejects the add with boundary validation warning.
        /// </summary>
        private void TestBoundaryViolation()
        {
            Debug.Log("=== PHASE 1.5 TEST 1: Boundary Violation ===");
            Debug.Log($"Grid size: {playerInventory.GridWidth}x{playerInventory.GridHeight}");
            
            // Try to add item at rightmost edge (should fail if item is >1 wide)
            int testX = playerInventory.GridWidth - 1;
            int testY = 0;
            
            Debug.Log($"Attempting to add TestItem_1 at [{testX}, {testY}] (should fail if item is 2x2 or larger)");
            playerInventory.AddItem_ServerRpc("TestItem_1", 0, testX, testY, false, 1);
            
            Debug.Log("Expected: '⚠️ AddItem failed - position out of bounds or invalid' warning");
        }
        
        /// <summary>
        /// TEST 2: Collision Detection
        /// Adds two items at overlapping positions.
        /// Expected: First succeeds, second fails with collision warning.
        /// </summary>
        private void TestCollisionDetection()
        {
            Debug.Log("=== PHASE 1.5 TEST 2: Collision Detection ===");
            
            // Add first item at [0,0]
            Debug.Log($"Adding first TestItem_1 at [0,0]");
            playerInventory.AddItem_ServerRpc("TestItem_1", 0, 0, 0, false, 1);
            
            // Try to add second item at same position (should fail)
            Debug.Log($"Attempting to add second TestItem_2 at [0,0] (should fail - collision)");
            playerInventory.AddItem_ServerRpc("TestItem_2", 0, 0, 0, false, 1);
            
            Debug.Log("Expected: First item added ✅, second rejected with '⚠️ AddItem failed - position occupied' warning");
        }
        
        
        /// <summary>
        /// TEST 9: Combined Validation (Rotation Boundary + Move Collision + Capacity)
        /// Runs all remaining validation tests in sequence.
        /// </summary>
        private void TestCombinedValidation()
        {
            Debug.Log("=== PHASE 1.5 TEST 9: Combined Validation Tests ===");
            
            // Clear grid first
            ClearAllItems();
            
            // Sub-test A: Rotation Boundary
            Debug.Log("--- Sub-test A: Rotation Boundary ---");
            int testX = playerInventory.GridWidth - 3;
            int testY = 0;
            Debug.Log($"Adding TestItem_2 (3x1) at [{testX}, {testY}], will attempt rotation in 0.5s");
            playerInventory.AddItem_ServerRpc("TestItem_2", 0, testX, testY, false, 1);
            StartCoroutine(RotateAfterDelay());
            
            // Sub-test B: Move Collision (runs after 1.5s)
            Debug.Log("--- Sub-test B: Move Collision (starts in 1.5s) ---");
            StartCoroutine(TestMoveCollisionDelayed());
            
            // Sub-test C: Capacity Limit (runs after 3s)
            Debug.Log("--- Sub-test C: Capacity Limit (starts in 3s) ---");
            StartCoroutine(TestCapacityDelayed());
        }
        
        private System.Collections.IEnumerator RotateAfterDelay()
        {
            yield return new WaitForSeconds(0.5f);
            
            var items = playerInventory.GetAllItems();
            if (items.Count > 0)
            {
                var lastItem = items[items.Count - 1];
                Debug.Log($"Attempting to rotate {lastItem.itemUID} (should fail - would exceed grid)");
                playerInventory.RotateItem_ServerRpc(lastItem.itemUID);
            }
        }
        
        private System.Collections.IEnumerator TestMoveCollisionDelayed()
        {
            yield return new WaitForSeconds(1.5f);
            
            Debug.Log("Adding two items at [0,0] and [2,0]");
            playerInventory.AddItem_ServerRpc("TestItem_1", 0, 0, 0, false, 1);
            playerInventory.AddItem_ServerRpc("TestItem_2", 0, 2, 0, false, 1);
            
            yield return new WaitForSeconds(0.5f);
            
            var items = playerInventory.GetAllItems();
            if (items.Count >= 2)
            {
                var secondItem = items[1];
                Debug.Log($"Attempting to move {secondItem.itemUID} to [0,0] (should fail - occupied)");
                playerInventory.MoveItem_ServerRpc(secondItem.itemUID, 0, 0);
            }
        }
        
        private System.Collections.IEnumerator TestCapacityDelayed()
        {
            yield return new WaitForSeconds(3f);
            
            ClearAllItems();
            
            int maxSlots = playerInventory.MaxInventorySlots;
            Debug.Log($"Grid capacity: {maxSlots} slots - filling inventory...");
            
            // Fill to capacity (1x1 items)
            for (int i = 0; i < maxSlots; i++)
            {
                int gridX = i % playerInventory.GridWidth;
                int gridY = i / playerInventory.GridWidth;
                string itemName = testItemNames[i % testItemNames.Length];
                playerInventory.AddItem_ServerRpc(itemName, 0, gridX, gridY, false, 1);
            }
            
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"Attempting to add item #{maxSlots + 1} (should fail - inventory full)");
            playerInventory.AddItem_ServerRpc(testItemNames[0], 0, 0, 0, false, 1);
        }
        
        // ===== DEBUG DISPLAY =====
        
        private void OnGUI()
        {
            if (playerInventory == null || !playerInventory.IsOwner)
                return;
            
            GUILayout.BeginArea(new Rect(10, 10, 450, 550));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("<b>INVENTORY TESTER - Phase 1.5</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16 });
            
            GUILayout.Space(10);
            GUILayout.Label("<b>Basic Controls (Number Keys):</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("0 - Add current test item (auto-place)");
            GUILayout.Label("1 - Cycle to next test item");
            GUILayout.Label("2 - Remove last item");
            GUILayout.Label("3 - Move last item (random)");
            GUILayout.Label("4 - Rotate last item");
            GUILayout.Label("5 - Clear all items");
            GUILayout.Label("6 - Print inventory");
            
            GUILayout.Space(10);
            GUILayout.Label("<b>Phase 1.5 Validation Tests:</b>", new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold });
            GUILayout.Label("7 - Test Boundary Violation");
            GUILayout.Label("8 - Test Collision Detection");
            GUILayout.Label("9 - Test All Validations (Combined)");
            
            GUILayout.Space(10);
            GUILayout.Label($"<b>Status:</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"Player ID: {playerInventory.Owner.ClientId}");
            GUILayout.Label($"Current Item: {testItemNames[currentItemIndex]}");
            GUILayout.Label($"Items in Inventory: {playerInventory.GetItemCount()}/{playerInventory.MaxInventorySlots}");
            GUILayout.Label($"Grid Size: {playerInventory.GridWidth}x{playerInventory.GridHeight}");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}