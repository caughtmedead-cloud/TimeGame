using UnityEngine;
using NewThelos.Inventory.Data;
using NewThelos.Inventory.Runtime;
using NewThelos.Systems.Inventory.Utils;

namespace NewThelos.Inventory.Testing
{
    /// <summary>
    /// Test script to verify InventoryGrid logic works correctly.
    /// Runs automated tests in Play Mode and logs results to console.
    /// </summary>
    public class InventoryGridTester : MonoBehaviour
    {
        [Header("Test Configuration")]
        [Tooltip("Run tests automatically on Start")]
        [SerializeField] private bool runOnStart = true;
        
        [Header("Test Items (assign in Inspector)")]
        [SerializeField] private ItemDefinitionSO testItem1x1;
        [SerializeField] private ItemDefinitionSO testItem2x1;
        [SerializeField] private ItemDefinitionSO testItem2x2;
        
        private InventoryGrid _testGrid;
        
        private void Start()
        {
            if (runOnStart)
            {
                RunAllTests();
            }
        }
        
        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            Debug.Log("========================================");
            Debug.Log("INVENTORY GRID TESTS STARTING");
            Debug.Log("========================================");
            
            // Create a fresh grid for testing
            _testGrid = new InventoryGrid(5, 4); // 5x4 grid
            
            TestBasicPlacement();
            TestBoundaryValidation();
            TestCollisionDetection();
            TestItemRotation();
            TestItemMovement();
            TestItemRemoval();
            TestFindEmptySpace();
            
            Debug.Log("========================================");
            Debug.Log("ALL TESTS COMPLETE");
            Debug.Log("========================================");
        }
        
        /// <summary>
        /// Test 1: Basic item placement
        /// </summary>
        private void TestBasicPlacement()
        {
            Debug.Log("\n--- TEST 1: Basic Placement ---");
            
            if (testItem1x1 == null)
            {
                Debug.LogError("❌ Test skipped - testItem1x1 not assigned!");
                return;
            }
            
            // Create item and try to place it at (0, 0)
            var item = new InventoryItem(testItem1x1.itemId, 0, 0);
            bool success = _testGrid.TryPlaceItem(item, testItem1x1, 0, 0, false);
            
            if (success)
            {
                Debug.Log($"✅ PASS: Placed {testItem1x1.displayName} at (0,0)");
                Debug.Log($"   Grid now has {_testGrid.GetAllItems().Count} items");
            }
            else
            {
                Debug.LogError($"❌ FAIL: Could not place {testItem1x1.displayName} at (0,0)");
            }
        }
        
        /// <summary>
        /// Test 2: Boundary validation (items can't go outside grid)
        /// </summary>
        private void TestBoundaryValidation()
        {
            Debug.Log("\n--- TEST 2: Boundary Validation ---");
            
            if (testItem2x2 == null)
            {
                Debug.LogError("❌ Test skipped - testItem2x2 not assigned!");
                return;
            }
            
            // Try to place 2x2 item at (4, 3) - should fail (goes outside 5x4 grid)
            var item = new InventoryItem(testItem2x2.itemId, 4, 3);
            bool success = _testGrid.TryPlaceItem(item, testItem2x2, 4, 3, false);
            
            if (!success)
            {
                Debug.Log($"✅ PASS: Correctly rejected out-of-bounds placement");
            }
            else
            {
                Debug.LogError($"❌ FAIL: Allowed out-of-bounds placement!");
            }
            
            // Try valid placement at (3, 2) - should succeed
            var validItem = new InventoryItem(testItem2x2.itemId, 3, 2);
            success = _testGrid.TryPlaceItem(validItem, testItem2x2, 3, 2, false);
            
            if (success)
            {
                Debug.Log($"✅ PASS: Correctly allowed in-bounds placement at (3,2)");
            }
            else
            {
                Debug.LogError($"❌ FAIL: Rejected valid placement!");
            }
        }
        
        /// <summary>
        /// Test 3: Collision detection (items can't overlap)
        /// </summary>
        private void TestCollisionDetection()
        {
            Debug.Log("\n--- TEST 3: Collision Detection ---");
            
            if (testItem2x1 == null)
            {
                Debug.LogError("❌ Test skipped - testItem2x1 not assigned!");
                return;
            }
            
            // Grid should have items from previous tests at (0,0) and (3,2)
            // Try to place 2x1 item at (0, 0) - should collide with existing item
            var collidingItem = new InventoryItem(testItem2x1.itemId, 0, 0);
            bool success = _testGrid.TryPlaceItem(collidingItem, testItem2x1, 0, 0, false);
            
            if (!success)
            {
                Debug.Log($"✅ PASS: Correctly detected collision at (0,0)");
            }
            else
            {
                Debug.LogError($"❌ FAIL: Did not detect collision!");
            }
            
            // Try to place 2x1 item at (1, 1) - should succeed (no collision)
            var nonCollidingItem = new InventoryItem(testItem2x1.itemId, 1, 1);
            success = _testGrid.TryPlaceItem(nonCollidingItem, testItem2x1, 1, 1, false);
            
            if (success)
            {
                Debug.Log($"✅ PASS: Correctly allowed non-colliding placement at (1,1)");
                Debug.Log($"   Grid now has {_testGrid.GetAllItems().Count} items");
            }
            else
            {
                Debug.LogError($"❌ FAIL: Rejected valid non-colliding placement!");
            }
        }
        
        /// <summary>
        /// Test 4: Item rotation (dimensions swap when rotated)
        /// </summary>
        private void TestItemRotation()
        {
            Debug.Log("\n--- TEST 4: Item Rotation ---");
            
            if (testItem2x1 == null)
            {
                Debug.LogError("❌ Test skipped - testItem2x1 not assigned!");
                return;
            }
            
            // Place 2x1 item rotated (becomes 1x2) at (0, 2)
            var rotatedItem = new InventoryItem(testItem2x1.itemId, 0, 2);
            bool success = _testGrid.TryPlaceItem(rotatedItem, testItem2x1, 0, 2, true);
            
            if (success)
            {
                Debug.Log($"✅ PASS: Placed rotated {testItem2x1.displayName} at (0,2)");
                Debug.Log($"   Original: {testItem2x1.width}x{testItem2x1.height}, Rotated: {testItem2x1.GetWidth(true)}x{testItem2x1.GetHeight(true)}");
            }
            else
            {
                Debug.LogError($"❌ FAIL: Could not place rotated item!");
            }
        }
        
        /// <summary>
        /// Test 5: Moving items to new positions
        /// </summary>
        private void TestItemMovement()
        {
            Debug.Log("\n--- TEST 5: Item Movement ---");
            
            // Get the first item we placed at (0, 0)
            var firstItem = _testGrid.GetItemAt(0, 0);
            
            if (firstItem == null)
            {
                Debug.LogError("❌ Test skipped - no item at (0,0)!");
                return;
            }
            
            if (testItem1x1 == null)
            {
                Debug.LogError("❌ Test skipped - testItem1x1 not assigned!");
                return;
            }
            
            // Try to move it to (4, 0)
            bool success = _testGrid.TryMoveItem(firstItem.instanceId, testItem1x1, 4, 0, false);
            
            if (success)
            {
                Debug.Log($"✅ PASS: Moved item from (0,0) to (4,0)");
                
                // Verify it's no longer at old position
                var itemAtOldPos = _testGrid.GetItemAt(0, 0);
                if (itemAtOldPos == null)
                {
                    Debug.Log($"✅ PASS: Confirmed item removed from old position");
                }
                
                // Verify it's at new position
                var itemAtNewPos = _testGrid.GetItemAt(4, 0);
                if (itemAtNewPos != null && itemAtNewPos.instanceId == firstItem.instanceId)
                {
                    Debug.Log($"✅ PASS: Confirmed item at new position");
                }
            }
            else
            {
                Debug.LogError($"❌ FAIL: Could not move item!");
            }
        }
        
        /// <summary>
        /// Test 6: Removing items from grid
        /// </summary>
        private void TestItemRemoval()
        {
            Debug.Log("\n--- TEST 6: Item Removal ---");
            
            int itemCountBefore = _testGrid.GetAllItems().Count;
            Debug.Log($"   Items before removal: {itemCountBefore}");
            
            // Get an item to remove
            var itemToRemove = _testGrid.GetAllItems()[0];
            bool success = _testGrid.TryRemoveItem(itemToRemove.instanceId);
            
            if (success)
            {
                int itemCountAfter = _testGrid.GetAllItems().Count;
                Debug.Log($"✅ PASS: Removed item. Count: {itemCountBefore} → {itemCountAfter}");
            }
            else
            {
                Debug.LogError($"❌ FAIL: Could not remove item!");
            }
            
            // Try to remove non-existent item
            success = _testGrid.TryRemoveItem("fake-id-12345");
            if (!success)
            {
                Debug.Log($"✅ PASS: Correctly rejected removal of non-existent item");
            }
        }
        
        /// <summary>
        /// Test 7: Finding empty space automatically
        /// </summary>
        private void TestFindEmptySpace()
        {
            Debug.Log("\n--- TEST 7: Find Empty Space ---");
            
            if (testItem1x1 == null)
            {
                Debug.LogError("❌ Test skipped - testItem1x1 not assigned!");
                return;
            }
            
            // Try to find space for 1x1 item
            bool found = _testGrid.TryFindEmptySpace(testItem1x1, out int posX, out int posY, false);
            
            if (found)
            {
                Debug.Log($"✅ PASS: Found empty space at ({posX},{posY}) for {testItem1x1.displayName}");
                
                // Try to actually place it there
                var autoItem = new InventoryItem(testItem1x1.itemId, posX, posY);
                bool success = _testGrid.TryPlaceItem(autoItem, testItem1x1, posX, posY, false);
                
                if (success)
                {
                    Debug.Log($"✅ PASS: Successfully placed item at auto-found position");
                }
            }
            else
            {
                Debug.LogWarning($"⚠️ No empty space found (grid might be full)");
            }
        }
        
        /// <summary>
        /// Manual test you can call from Inspector
        /// </summary>
        [ContextMenu("Print Current Grid State")]
        public void PrintGridState()
        {
            if (_testGrid == null)
            {
                Debug.Log("No test grid exists yet. Run tests first.");
                return;
            }
            
            Debug.Log($"\n=== GRID STATE ({_testGrid.Width}x{_testGrid.Height}) ===");
            Debug.Log($"Total items: {_testGrid.GetAllItems().Count}");
            
            foreach (var item in _testGrid.GetAllItems())
            {
                Debug.Log($"  {item}");
            }
        }
    }
}