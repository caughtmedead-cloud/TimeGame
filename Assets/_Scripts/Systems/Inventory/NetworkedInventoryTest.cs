using UnityEngine;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Simple test script to verify NetworkedInventoryTetris is working.
    /// 
    /// Controls:
    /// - T: Place Rifle at (2, 2)
    /// - G: Place Pistol at (5, 5)
    /// - Y: Remove item at (2, 2)
    /// - H: Remove item at (5, 5)
    /// 
    /// What to test:
    /// 1. Start as Host
    /// 2. Press T - rifle should appear
    /// 3. Start a client in another instance
    /// 4. Client should see the rifle automatically
    /// 5. Press G on client - pistol should appear on both
    /// 6. Press Y on host - rifle should disappear on both
    /// 
    /// This verifies:
    /// ✓ Server authority works (only server validates)
    /// ✓ Client replication works (clients see changes)
    /// ✓ Bidirectional sync works (host sees client changes)
    /// </summary>
    public class NetworkedInventoryTest : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Drag NetworkedInventoryTetris component here")]
        [SerializeField] private NetworkedInventoryTetris networkedInventory;

        [Tooltip("Drag InventoryTetrisRegistry asset here")]
        [SerializeField] private InventoryTetrisRegistry itemRegistry;

        [Header("Test Configuration")]
        [Tooltip("Show console messages when pressing test keys")]
        [SerializeField] private bool showTestMessages = true;

        void Update()
        {
            // Test: Place Rifle at (2, 2)
            if (Input.GetKeyDown(KeyCode.T))
            {
                TestPlaceItem("Rifle", new Vector2Int(2, 2), PlacedObjectTypeSO.Dir.Down);
            }

            // Test: Place Pistol at (5, 5)
            if (Input.GetKeyDown(KeyCode.G))
            {
                TestPlaceItem("Pistol", new Vector2Int(5, 5), PlacedObjectTypeSO.Dir.Down);
            }

            // Test: Remove item at (2, 2)
            if (Input.GetKeyDown(KeyCode.Y))
            {
                TestRemoveItem(new Vector2Int(2, 2));
            }

            // Test: Remove item at (5, 5)
            if (Input.GetKeyDown(KeyCode.H))
            {
                TestRemoveItem(new Vector2Int(5, 5));
            }

            // Test: Place Medkit at (7, 3) rotated left
            if (Input.GetKeyDown(KeyCode.U))
            {
                TestPlaceItem("Medkit", new Vector2Int(7, 3), PlacedObjectTypeSO.Dir.Left);
            }
        }

        private void TestPlaceItem(string itemName, Vector2Int position, PlacedObjectTypeSO.Dir direction)
        {
            if (networkedInventory == null)
            {
                Debug.LogError("[NetworkedInventoryTest] NetworkedInventory not assigned!");
                return;
            }

            if (itemRegistry == null)
            {
                Debug.LogError("[NetworkedInventoryTest] ItemRegistry not assigned!");
                return;
            }

            // Look up the item
            ItemTetrisSO item = itemRegistry.GetItem(itemName);
            if (item == null)
            {
                Debug.LogError($"[NetworkedInventoryTest] Item not found: {itemName}");
                return;
            }

            // Request placement through networking system
            networkedInventory.RequestPlaceItem(item, position, direction);

            if (showTestMessages)
            {
                Debug.Log($"[NetworkedInventoryTest] Requested place: {itemName} at {position} facing {direction}");
            }
        }

        private void TestRemoveItem(Vector2Int position)
        {
            if (networkedInventory == null)
            {
                Debug.LogError("[NetworkedInventoryTest] NetworkedInventory not assigned!");
                return;
            }

            // Request removal through networking system
            networkedInventory.RequestRemoveItem(position);

            if (showTestMessages)
            {
                Debug.Log($"[NetworkedInventoryTest] Requested remove at: {position}");
            }
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            // Display instructions on screen
            GUILayout.BeginArea(new Rect(10, 10, 400, 300));
            GUILayout.Label("=== NETWORKED INVENTORY TEST ===", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(10);
            GUILayout.Label("Press T - Place Rifle at (2,2)");
            GUILayout.Label("Press G - Place Pistol at (5,5)");
            GUILayout.Label("Press U - Place Medkit at (7,3) rotated");
            GUILayout.Label("Press Y - Remove item at (2,2)");
            GUILayout.Label("Press H - Remove item at (5,5)");
            GUILayout.Space(10);
            GUILayout.Label("TO TEST MULTIPLAYER:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUILayout.Label("1. Start as Host (in Game view)");
            GUILayout.Label("2. Place items with T/G/U");
            GUILayout.Label("3. Open second Unity instance");
            GUILayout.Label("4. Start as Client - items appear!");
            GUILayout.Label("5. Place/remove from either side");
            GUILayout.EndArea();
        }
#endif
    }
}
