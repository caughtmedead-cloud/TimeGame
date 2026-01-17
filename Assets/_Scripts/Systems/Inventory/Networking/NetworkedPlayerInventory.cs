using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using NewThelos.Systems.Inventory.Utils;

namespace NewThelos.Systems.Inventory.Networking
{
    /// <summary>
    /// Server-authoritative networked player inventory component.
    /// 
    /// ARCHITECTURAL DESIGN:
    /// - Server is the single source of truth for inventory state
    /// - Uses FishNet's SyncList for automatic replication to clients
    /// - Client sends actions via ServerRPCs, server validates and executes
    /// - Late-joining clients automatically receive current inventory state
    /// 
    /// INTEGRATION WITH UGI:
    /// - This component maintains the NETWORK state (what items exist)
    /// - Phase 2 will create InventoryUIBridge to connect this to UGI's UI system
    /// - Uses NetworkedItemData struct for network transmission
    /// - ItemDataRegistry resolves string names back to ItemDataSo references
    /// 
    /// PATTERN REFERENCE:
    /// - Based on TemporalStability.cs for SyncVar usage
    /// - Based on PlayerZoneTriggerHandler.cs for ServerRPC patterns
    /// </summary>
    public class NetworkedPlayerInventory : NetworkBehaviour
    {
        [Header("Inventory Configuration")]
        [Tooltip("Maximum number of grid slots in the player's main inventory")]
        [SerializeField] private int maxInventorySlots = 20;
        
        [Header("Debug Settings")]
        [SerializeField] private bool verboseLogging = false;
        
        // ===== NETWORK STATE =====
        
        /// <summary>
        /// Server-authoritative list of all items in this player's inventory.
        /// Automatically synchronized to all observing clients via FishNet.
        /// </summary>
        private readonly SyncList<NetworkedItemData> _inventoryItems = new SyncList<NetworkedItemData>(
            new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers)
        );
        
        // ===== EVENTS =====
        
        /// <summary>
        /// Fired when an item is added to the inventory.
        /// Subscribed by UI bridge in Phase 2.
        /// </summary>
        public event Action<NetworkedItemData> OnItemAdded;
        
        /// <summary>
        /// Fired when an item is removed from the inventory.
        /// Subscribed by UI bridge in Phase 2.
        /// </summary>
        public event Action<string> OnItemRemoved; // string = itemUID
        
        /// <summary>
        /// Fired when an item is moved within the inventory.
        /// Subscribed by UI bridge in Phase 2.
        /// </summary>
        public event Action<string, int, int> OnItemMoved; // itemUID, newX, newY
        
        /// <summary>
        /// Fired when an item is rotated.
        /// Subscribed by UI bridge in Phase 2.
        /// </summary>
        public event Action<string, bool> OnItemRotated; // itemUID, isRotated
        
        // ===== LIFECYCLE =====
        
        private void Awake()
        {
            // Subscribe to SyncList changes for event forwarding
            _inventoryItems.OnChange += OnInventoryItemsChanged;
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            if (verboseLogging)
            {
                Debug.Log($"[NetworkedPlayerInventory] Server started for Player {Owner.ClientId}");
            }
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (verboseLogging)
            {
                Debug.Log($"[NetworkedPlayerInventory] Client started. IsOwner: {IsOwner}, " +
                          $"Player ClientId: {Owner.ClientId}, Current inventory count: {_inventoryItems.Count}");
            }
        }
        
        // ===== SERVER RPCS (CLIENT → SERVER) =====
        
        /// <summary>
        /// Request from client to add an item to their inventory.
        /// Server validates and adds if valid.
        /// </summary>
        /// <param name="itemDataSOName">Name of the ItemDataSo to add</param>
        /// <param name="gridIndex">Which grid to add to (0 = main inventory)</param>
        /// <param name="posX">Desired X position</param>
        /// <param name="posY">Desired Y position</param>
        /// <param name="isRotated">Should item be rotated</param>
        /// <param name="stackCount">Initial stack count</param>
        /// <param name="sender">Automatically filled by FishNet</param>
        [ServerRpc(RequireOwnership = false)]
        public void AddItem_ServerRpc(string itemDataSOName, int gridIndex, int posX, int posY, 
                                      bool isRotated, int stackCount, NetworkConnection sender = null)
        {
            if (sender != Owner)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem_ServerRpc called by non-owner! " +
                                 $"Sender: {sender.ClientId}, Owner: {Owner.ClientId}");
                return;
            }
            
            // Validate item exists in registry
            if (!ItemDataRegistry.HasItem(itemDataSOName))
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem failed - item not found in registry: '{itemDataSOName}'");
                return;
            }
            
            // Validate grid index
            if (gridIndex < 0)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem failed - invalid grid index: {gridIndex}");
                return;
            }
            
            // Validate position
            if (posX < 0 || posY < 0)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem failed - invalid position: [{posX}, {posY}]");
                return;
            }
            
            // Validate stack count
            if (stackCount < 1)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem failed - invalid stack count: {stackCount}");
                return;
            }
            
            // TODO Phase 1.5: Check if position is occupied / validate grid space
            
            // Generate unique item UID
            string itemUID = Guid.NewGuid().ToString();
            
            // Create network item data
            NetworkedItemData newItem = new NetworkedItemData(
                itemUID,
                itemDataSOName,
                gridIndex,
                posX,
                posY,
                isRotated,
                stackCount
            );
            
            // Add to synced list (automatically replicates to clients)
            _inventoryItems.Add(newItem);
            
            if (verboseLogging)
            {
                Debug.Log($"[NetworkedPlayerInventory] [Server] Added item for Player {Owner.ClientId}: {newItem}");
            }
        }
        
        /// <summary>
        /// Request from client to remove an item from their inventory.
        /// Server validates and removes if valid.
        /// </summary>
        /// <param name="itemUID">Unique ID of item to remove</param>
        /// <param name="sender">Automatically filled by FishNet</param>
        [ServerRpc(RequireOwnership = false)]
        public void RemoveItem_ServerRpc(string itemUID, NetworkConnection sender = null)
        {
            if (sender != Owner)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ RemoveItem_ServerRpc called by non-owner! " +
                                 $"Sender: {sender.ClientId}, Owner: {Owner.ClientId}");
                return;
            }
            
            if (string.IsNullOrEmpty(itemUID))
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ RemoveItem failed - itemUID is null or empty");
                return;
            }
            
            // Find item index
            int itemIndex = FindItemIndexByUID(itemUID);
            
            if (itemIndex < 0)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ RemoveItem failed - item not found: {itemUID}");
                return;
            }
            
            // Remove from synced list (automatically replicates to clients)
            _inventoryItems.RemoveAt(itemIndex);
            
            if (verboseLogging)
            {
                Debug.Log($"[NetworkedPlayerInventory] [Server] Removed item for Player {Owner.ClientId}: {itemUID}");
            }
        }
        
        /// <summary>
        /// Request from client to move an item to a new position.
        /// Server validates and moves if valid.
        /// </summary>
        /// <param name="itemUID">Unique ID of item to move</param>
        /// <param name="newPosX">New X position</param>
        /// <param name="newPosY">New Y position</param>
        /// <param name="sender">Automatically filled by FishNet</param>
        [ServerRpc(RequireOwnership = false)]
        public void MoveItem_ServerRpc(string itemUID, int newPosX, int newPosY, NetworkConnection sender = null)
        {
            if (sender != Owner)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ MoveItem_ServerRpc called by non-owner! " +
                                 $"Sender: {sender.ClientId}, Owner: {Owner.ClientId}");
                return;
            }
            
            if (string.IsNullOrEmpty(itemUID))
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ MoveItem failed - itemUID is null or empty");
                return;
            }
            
            // Validate new position
            if (newPosX < 0 || newPosY < 0)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ MoveItem failed - invalid position: [{newPosX}, {newPosY}]");
                return;
            }
            
            // Find item index
            int itemIndex = FindItemIndexByUID(itemUID);
            
            if (itemIndex < 0)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ MoveItem failed - item not found: {itemUID}");
                return;
            }
            
            // TODO Phase 1.5: Check if new position is valid / not occupied
            
            // Get current item data
            NetworkedItemData item = _inventoryItems[itemIndex];
            
            // Create updated item data with new position
            NetworkedItemData updatedItem = new NetworkedItemData(
                item.itemUID,
                item.itemDataSOName,
                item.gridIndex,
                newPosX,
                newPosY,
                item.isRotated,
                item.stackCount
            );
            
            // Update in synced list (automatically replicates to clients)
            _inventoryItems[itemIndex] = updatedItem;
            
            if (verboseLogging)
            {
                Debug.Log($"[NetworkedPlayerInventory] [Server] Moved item for Player {Owner.ClientId}: " +
                          $"{itemUID} to [{newPosX}, {newPosY}]");
            }
        }
        
        /// <summary>
        /// Request from client to rotate an item.
        /// Server validates and rotates if valid.
        /// </summary>
        /// <param name="itemUID">Unique ID of item to rotate</param>
        /// <param name="sender">Automatically filled by FishNet</param>
        [ServerRpc(RequireOwnership = false)]
        public void RotateItem_ServerRpc(string itemUID, NetworkConnection sender = null)
        {
            if (sender != Owner)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ RotateItem_ServerRpc called by non-owner! " +
                                 $"Sender: {sender.ClientId}, Owner: {Owner.ClientId}");
                return;
            }
            
            if (string.IsNullOrEmpty(itemUID))
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ RotateItem failed - itemUID is null or empty");
                return;
            }
            
            // Find item index
            int itemIndex = FindItemIndexByUID(itemUID);
            
            if (itemIndex < 0)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ RotateItem failed - item not found: {itemUID}");
                return;
            }
            
            // Get current item data
            NetworkedItemData item = _inventoryItems[itemIndex];
            
            // Create updated item data with toggled rotation
            NetworkedItemData updatedItem = new NetworkedItemData(
                item.itemUID,
                item.itemDataSOName,
                item.gridIndex,
                item.posX,
                item.posY,
                !item.isRotated, // Toggle rotation
                item.stackCount
            );
            
            // Update in synced list (automatically replicates to clients)
            _inventoryItems[itemIndex] = updatedItem;
            
            if (verboseLogging)
            {
                Debug.Log($"[NetworkedPlayerInventory] [Server] Rotated item for Player {Owner.ClientId}: " +
                          $"{itemUID} to {updatedItem.isRotated}");
            }
        }
        
        // ===== SYNCLIST CALLBACKS =====
        
        /// <summary>
        /// Called when the _inventoryItems SyncList changes.
        /// Fires appropriate events for UI bridge to consume.
        /// </summary>
        private void OnInventoryItemsChanged(SyncListOperation op, int index, 
                                            NetworkedItemData oldItem, NetworkedItemData newItem, bool asServer)
        {
            // Only fire events on clients (UI subscribes client-side)
            if (asServer)
                return;
            
            switch (op)
            {
                case SyncListOperation.Add:
                    if (verboseLogging && IsOwner)
                    {
                        Debug.Log($"[NetworkedPlayerInventory] [Client] Item added: {newItem}");
                    }
                    OnItemAdded?.Invoke(newItem);
                    break;
                
                case SyncListOperation.RemoveAt:
                    if (verboseLogging && IsOwner)
                    {
                        Debug.Log($"[NetworkedPlayerInventory] [Client] Item removed: {oldItem.itemUID}");
                    }
                    OnItemRemoved?.Invoke(oldItem.itemUID);
                    break;
                
                case SyncListOperation.Set:
                    // Check what changed
                    if (oldItem.posX != newItem.posX || oldItem.posY != newItem.posY)
                    {
                        if (verboseLogging && IsOwner)
                        {
                            Debug.Log($"[NetworkedPlayerInventory] [Client] Item moved: {newItem.itemUID} " +
                                      $"to [{newItem.posX}, {newItem.posY}]");
                        }
                        OnItemMoved?.Invoke(newItem.itemUID, newItem.posX, newItem.posY);
                    }
                    
                    if (oldItem.isRotated != newItem.isRotated)
                    {
                        if (verboseLogging && IsOwner)
                        {
                            Debug.Log($"[NetworkedPlayerInventory] [Client] Item rotated: {newItem.itemUID} " +
                                      $"to {newItem.isRotated}");
                        }
                        OnItemRotated?.Invoke(newItem.itemUID, newItem.isRotated);
                    }
                    break;
                
                case SyncListOperation.Clear:
                    if (verboseLogging && IsOwner)
                    {
                        Debug.Log($"[NetworkedPlayerInventory] [Client] Inventory cleared");
                    }
                    // TODO: Add OnInventoryCleared event if needed
                    break;
            }
        }
        
        // ===== UTILITY METHODS =====
        
        /// <summary>
        /// Finds the index of an item in the inventory by its UID.
        /// </summary>
        /// <param name="itemUID">Unique item identifier</param>
        /// <returns>Index in _inventoryItems list, or -1 if not found</returns>
        private int FindItemIndexByUID(string itemUID)
        {
            for (int i = 0; i < _inventoryItems.Count; i++)
            {
                if (_inventoryItems[i].itemUID == itemUID)
                {
                    return i;
                }
            }
            return -1;
        }
        
        /// <summary>
        /// Gets a read-only list of all items in the inventory.
        /// Useful for UI bridge to initialize display.
        /// </summary>
        public IReadOnlyList<NetworkedItemData> GetAllItems()
        {
            return _inventoryItems;
        }
        
        /// <summary>
        /// Gets a specific item by UID.
        /// </summary>
        /// <param name="itemUID">Unique item identifier</param>
        /// <returns>The item data, or default if not found</returns>
        public NetworkedItemData? GetItemByUID(string itemUID)
        {
            int index = FindItemIndexByUID(itemUID);
            if (index >= 0)
            {
                return _inventoryItems[index];
            }
            return null;
        }
        
        /// <summary>
        /// Gets the current number of items in the inventory.
        /// </summary>
        public int GetItemCount()
        {
            return _inventoryItems.Count;
        }
        
        // ===== DEBUG COMMANDS =====
        
        [ContextMenu("Debug: Print Inventory")]
        private void DebugPrintInventory()
        {
            if (_inventoryItems.Count == 0)
            {
                Debug.Log($"[NetworkedPlayerInventory] Inventory is empty.");
                return;
            }
            
            Debug.Log($"[NetworkedPlayerInventory] === Inventory ({_inventoryItems.Count} items) ===");
            foreach (var item in _inventoryItems)
            {
                Debug.Log($"  {item}");
            }
        }
        
        [ContextMenu("Debug: Add Test Item (Server Only)")]
        private void DebugAddTestItem()
        {
            if (!IsServerStarted)
            {
                Debug.LogWarning("[NetworkedPlayerInventory] Debug command only works on server!");
                return;
            }
            
            // Add TestItem_1, TestItem_2, and TestItem_3 for testing
            AddItem_ServerRpc("TestItem_1", 0, 0, 0, false, 1, Owner);
            AddItem_ServerRpc("TestItem_2", 0, 1, 0, false, 1, Owner);
            AddItem_ServerRpc("TestItem_3", 0, 2, 0, false, 1, Owner);
        }
    }
}