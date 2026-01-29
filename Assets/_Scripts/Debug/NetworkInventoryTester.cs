using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using NewThelos.Inventory.Networking;

namespace NewThelos.Inventory.Testing
{
    /// <summary>
    /// Simple UI to test networked inventory operations.
    /// Attach to a UI button or GameObject with NetworkedPlayerInventory reference.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkInventoryTester : NetworkBehaviour
    {
        [Header("References")]
        [Tooltip("The player's NetworkedPlayerInventory component")]
        [SerializeField] private NetworkedPlayerInventory inventory;
        
        [Header("Test Items")]
        [Tooltip("Item to spawn when button is clicked")]
        [SerializeField] private string testItemId = "Item_Test_1";
        
        [SerializeField] private string gridId = "main_inventory";
        
        [Header("UI (Optional)")]
        [SerializeField] private Button addItemButton;
        [SerializeField] private Text statusText;
        
        private int _nextTestPosition = 0;
        
        public override void OnStartClient()
        {
            base.OnStartClient();

            // Only setup UI for owner
            if (!IsOwner)
            {
                if (addItemButton != null)
                    addItemButton.gameObject.SetActive(false);
                if (statusText != null)
                    statusText.gameObject.SetActive(false);
                return;
            }

            // Hook up button
            if (addItemButton != null)
            {
                addItemButton.onClick.AddListener(OnAddItemButtonClicked);
            }

            UpdateStatus("Ready! Click button to add items.");
        }
        
        /// <summary>
        /// Called when the "Add Item" button is clicked
        /// </summary>
        public void OnAddItemButtonClicked()
        {
            if (inventory == null)
            {
                Debug.LogError("[NetworkInventoryTester] Inventory reference is null!");
                return;
            }
            
            // Get a random test item from the registry
            string[] testItems = { "Item_Test_1", "Item_Test_2", "Item_Test_3" };
            string randomItemId = testItems[Random.Range(0, testItems.Length)];
            
            // Let server auto-position the item
            inventory.AddItem_ServerRpc(randomItemId, gridId, -1, -1, false, 1);
            
            _nextTestPosition++;
            UpdateStatus($"Requested add: {randomItemId} (auto-position)");
            
            Debug.Log($"[NetworkInventoryTester] Sent AddItem request: {randomItemId} (auto-position)");
        }
        
        /// <summary>
        /// Manual test you can call from inspector or other scripts
        /// </summary>
        [ContextMenu("Add Test Item")]
        public void AddTestItem()
        {
            OnAddItemButtonClicked();
        }
        
        /// <summary>
        /// Test adding item to specific position
        /// </summary>
        public void AddItemAt(string itemId, int x, int y)
        {
            if (!IsOwner || inventory == null)
                return;
            
            inventory.AddItem_ServerRpc(itemId, gridId, x, y, false, 1);
            UpdateStatus($"Requested add: {itemId} at ({x},{y})");
        }
        
        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}