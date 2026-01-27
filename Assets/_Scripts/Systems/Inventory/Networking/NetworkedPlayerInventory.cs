using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using NewThelos.Systems.Inventory.Utils;
using Inventory.Scripts.Core.ScriptableObjects.Items;

namespace NewThelos.Systems.Inventory.Networking
{
    /// <summary>
    /// Server-authoritative networked player inventory component.
    /// 
    /// PHASE 1.5 ENHANCEMENTS:
    /// - Automatic grid configuration (no manual entry needed)
    /// - Grid boundary validation with item dimensions
    /// - AABB collision detection for item overlap prevention
    /// - Capacity management with automatic calculation
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
        [Tooltip("Grid width (columns) - Set to match your UI grid")]
        [SerializeField] private int gridWidth = 5;
        
        [Tooltip("Grid height (rows) - Set to match your UI grid")]
        [SerializeField] private int gridHeight = 4;
        
        [Header("Validation Settings")]
        [Tooltip("Enable item collision detection (prevents overlapping items)")]
        [SerializeField] private bool enableCollisionDetection = true;
        
        [Tooltip("Enable boundary validation (prevents items outside grid)")]
        [SerializeField] private bool enableBoundaryValidation = true;
        
        [Header("Debug Settings")]
        [SerializeField] private bool verboseLogging = false;
        
        // ===== CACHED PROPERTIES =====
        
        /// <summary>
        /// Maximum number of items based on grid size.
        /// Calculated as gridWidth * gridHeight.
        /// </summary>
        public int MaxInventorySlots => gridWidth * gridHeight;
        
        /// <summary>
        /// Current grid width.
        /// </summary>
        public int GridWidth => gridWidth;
        
        /// <summary>
        /// Current grid height.
        /// </summary>
        public int GridHeight => gridHeight;
        
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
            
            // Validate configuration
            ValidateConfiguration();
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            if (verboseLogging)
            {
                Debug.Log($"[NetworkedPlayerInventory] Server started for Player {Owner.ClientId} " +
                          $"(Grid: {gridWidth}x{gridHeight}, Max Slots: {MaxInventorySlots})");
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
        
        /// <summary>
        /// Validates the inventory configuration on startup.
        /// Logs warnings if configuration seems incorrect.
        /// </summary>
        private void ValidateConfiguration()
        {
            if (gridWidth <= 0 || gridHeight <= 0)
            {
                Debug.LogError($"[NetworkedPlayerInventory] Invalid grid dimensions: {gridWidth}x{gridHeight}. " +
                               $"Grid dimensions must be positive!");
            }
            
            if (MaxInventorySlots <= 0)
            {
                Debug.LogError($"[NetworkedPlayerInventory] Calculated MaxInventorySlots is {MaxInventorySlots}. " +
                               $"Check your grid dimensions!");
            }
            
            if (verboseLogging)
            {
                Debug.Log($"[NetworkedPlayerInventory] Configuration validated: " +
                          $"Grid {gridWidth}x{gridHeight}, Max {MaxInventorySlots} slots, " +
                          $"Collision Detection: {enableCollisionDetection}, " +
                          $"Boundary Validation: {enableBoundaryValidation}");
            }
        }
        
        // ===== SERVER RPCS (CLIENT → SERVER) =====
        
        /// <summary>
        /// Request from client to add an item to their inventory.
        /// Server validates and adds if valid.
        /// 
        /// PHASE 1.5 VALIDATION:
        /// - Checks grid boundaries based on item dimensions
        /// - Detects collisions with existing items (AABB)
        /// - Enforces capacity limits
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
            // ===== OWNERSHIP VALIDATION =====
            if (sender != Owner)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem_ServerRpc called by non-owner! " +
                                 $"Sender: {sender.ClientId}, Owner: {Owner.ClientId}");
                return;
            }
            
            // ===== ITEM REGISTRY VALIDATION =====
            if (!ItemDataRegistry.HasItem(itemDataSOName))
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem failed - item not found in registry: '{itemDataSOName}'");
                return;
            }
            
            // ===== GRID INDEX VALIDATION =====
            if (gridIndex < 0)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem failed - invalid grid index: {gridIndex}");
                return;
            }
            
            // ===== BASIC POSITION VALIDATION =====
            if (posX < 0 || posY < 0)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem failed - negative position: [{posX}, {posY}]");
                return;
            }
            
            // ===== STACK COUNT VALIDATION =====
            if (stackCount < 1)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem failed - invalid stack count: {stackCount}");
                return;
            }
            
            // ===== PHASE 1.5: CAPACITY CHECK =====
            if (_inventoryItems.Count >= MaxInventorySlots)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem failed - inventory full " +
                                 $"({_inventoryItems.Count}/{MaxInventorySlots})");
                return;
            }
            
            // ===== PHASE 1.5: BOUNDARY VALIDATION =====
            if (enableBoundaryValidation && !IsValidGridPosition(itemDataSOName, posX, posY, isRotated, gridIndex))
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem failed - position out of bounds or invalid");
                return;
            }
            
            // ===== PHASE 1.5: COLLISION DETECTION =====
            if (enableCollisionDetection && WouldCollideWithExistingItems(itemDataSOName, posX, posY, isRotated, gridIndex))
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ AddItem failed - position occupied or collision detected");
                return;
            }
            
            // ===== ALL VALIDATIONS PASSED - ADD ITEM =====
            
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
                Debug.Log($"[NetworkedPlayerInventory] [Server] ✅ Added item for Player {Owner.ClientId}: {newItem}");
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
                Debug.Log($"[NetworkedPlayerInventory] [Server] ✅ Removed item for Player {Owner.ClientId}: {itemUID}");
            }
        }
        
        /// <summary>
        /// Request from client to move an item to a new position.
        /// Server validates and moves if valid.
        /// 
        /// PHASE 1.5 VALIDATION:
        /// - Checks new position boundaries
        /// - Detects collisions at new position
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
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ MoveItem failed - negative position: [{newPosX}, {newPosY}]");
                return;
            }
            
            // Find item index
            int itemIndex = FindItemIndexByUID(itemUID);
            
            if (itemIndex < 0)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ MoveItem failed - item not found: {itemUID}");
                return;
            }
            
            // Get current item data
            NetworkedItemData item = _inventoryItems[itemIndex];
            
            // ===== PHASE 1.5: BOUNDARY VALIDATION FOR NEW POSITION =====
            if (enableBoundaryValidation && !IsValidGridPosition(item.itemDataSOName, newPosX, newPosY, item.isRotated, item.gridIndex))
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ MoveItem failed - new position out of bounds");
                return;
            }
            
            // ===== PHASE 1.5: COLLISION DETECTION AT NEW POSITION =====
            // Temporarily remove item from collision checks (can't collide with itself)
            if (enableCollisionDetection && WouldCollideWithExistingItems(item.itemDataSOName, newPosX, newPosY, item.isRotated, item.gridIndex, itemUID))
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ MoveItem failed - new position occupied");
                return;
            }
            
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
                Debug.Log($"[NetworkedPlayerInventory] [Server] ✅ Moved item for Player {Owner.ClientId}: " +
                          $"{itemUID} to [{newPosX}, {newPosY}]");
            }
        }
        
        /// <summary>
        /// Request from client to rotate an item.
        /// Server validates and rotates if valid.
        /// 
        /// PHASE 1.5 VALIDATION:
        /// - Checks if rotated item still fits in grid
        /// - Detects collisions after rotation
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
            bool newRotation = !item.isRotated;
            
            // ===== PHASE 1.5: BOUNDARY VALIDATION AFTER ROTATION =====
            if (enableBoundaryValidation && !IsValidGridPosition(item.itemDataSOName, item.posX, item.posY, newRotation, item.gridIndex))
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ RotateItem failed - rotated item would be out of bounds");
                return;
            }
            
            // ===== PHASE 1.5: COLLISION DETECTION AFTER ROTATION =====
            if (enableCollisionDetection && WouldCollideWithExistingItems(item.itemDataSOName, item.posX, item.posY, newRotation, item.gridIndex, itemUID))
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] ⚠️ RotateItem failed - rotated item would collide");
                return;
            }
            
            // Create updated item data with toggled rotation
            NetworkedItemData updatedItem = new NetworkedItemData(
                item.itemUID,
                item.itemDataSOName,
                item.gridIndex,
                item.posX,
                item.posY,
                newRotation,
                item.stackCount
            );
            
            // Update in synced list (automatically replicates to clients)
            _inventoryItems[itemIndex] = updatedItem;
            
            if (verboseLogging)
            {
                Debug.Log($"[NetworkedPlayerInventory] [Server] ✅ Rotated item for Player {Owner.ClientId}: " +
                          $"{itemUID} to {updatedItem.isRotated}");
            }
        }
        
        // ===== PHASE 1.5: VALIDATION METHODS =====
        
        /// <summary>
        /// Validates that an item at the given position would fit within grid boundaries.
        /// Takes into account item dimensions and rotation.
        /// </summary>
        /// <param name="itemDataSOName">Name of the item to validate</param>
        /// <param name="posX">X position</param>
        /// <param name="posY">Y position</param>
        /// <param name="isRotated">Whether item is rotated</param>
        /// <param name="gridIndex">Grid index (for future multi-grid support)</param>
        /// <returns>True if position is valid and within bounds</returns>
        private bool IsValidGridPosition(string itemDataSOName, int posX, int posY, bool isRotated, int gridIndex = 0)
        {
            // Validate grid index (future-proofing for multiple grids)
            if (gridIndex < 0)
            {
                if (verboseLogging)
                    Debug.LogWarning($"[NetworkedPlayerInventory] Invalid grid index: {gridIndex}");
                return false;
            }
            
            // Get item dimensions
            ItemDataSo itemData = ItemDataRegistry.GetItemByName(itemDataSOName);
            if (itemData == null || itemData.DimensionsSo == null)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] Cannot validate position - item or dimensions not found: {itemDataSOName}");
                return false;
            }
            
            // Get width/height (swap if rotated)
            int itemWidth = isRotated ? itemData.DimensionsSo.Height : itemData.DimensionsSo.Width;
            int itemHeight = isRotated ? itemData.DimensionsSo.Width : itemData.DimensionsSo.Height;
            
            // Check if item fits within grid boundaries
            if (posX + itemWidth > gridWidth)
            {
                if (verboseLogging)
                    Debug.LogWarning($"[NetworkedPlayerInventory] Item {itemDataSOName} width {itemWidth} at X={posX} " +
                                     $"exceeds grid width {gridWidth}");
                return false;
            }
            
            if (posY + itemHeight > gridHeight)
            {
                if (verboseLogging)
                    Debug.LogWarning($"[NetworkedPlayerInventory] Item {itemDataSOName} height {itemHeight} at Y={posY} " +
                                     $"exceeds grid height {gridHeight}");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Checks if adding an item at the given position would collide with existing items.
        /// Uses AABB (Axis-Aligned Bounding Box) collision detection.
        /// </summary>
        /// <param name="itemDataSOName">Name of the item to check</param>
        /// <param name="posX">X position</param>
        /// <param name="posY">Y position</param>
        /// <param name="isRotated">Whether item is rotated</param>
        /// <param name="gridIndex">Grid index</param>
        /// <param name="excludeItemUID">Optional UID to exclude from collision (for MoveItem/RotateItem)</param>
        /// <returns>True if collision would occur, False if position is clear</returns>
        private bool WouldCollideWithExistingItems(string itemDataSOName, int posX, int posY, bool isRotated, int gridIndex = 0, string excludeItemUID = null)
        {
            // Get item dimensions
            ItemDataSo itemData = ItemDataRegistry.GetItemByName(itemDataSOName);
            if (itemData == null || itemData.DimensionsSo == null)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] Cannot check collision - item or dimensions not found: {itemDataSOName}");
                return true; // Treat as collision to be safe
            }
            
            // Get width/height (swap if rotated)
            int itemWidth = isRotated ? itemData.DimensionsSo.Height : itemData.DimensionsSo.Width;
            int itemHeight = isRotated ? itemData.DimensionsSo.Width : itemData.DimensionsSo.Height;
            
            // Check collision with each existing item
            foreach (var existingItem in _inventoryItems)
            {
                // Skip items in different grids
                if (existingItem.gridIndex != gridIndex)
                    continue;
                
                // Skip the item we're moving/rotating (can't collide with itself)
                if (excludeItemUID != null && existingItem.itemUID == excludeItemUID)
                    continue;
                
                // Get existing item dimensions
                ItemDataSo existingItemData = ItemDataRegistry.GetItemByName(existingItem.itemDataSOName);
                if (existingItemData == null || existingItemData.DimensionsSo == null)
                    continue;
                
                int existingWidth = existingItem.isRotated ? existingItemData.DimensionsSo.Height : existingItemData.DimensionsSo.Width;
                int existingHeight = existingItem.isRotated ? existingItemData.DimensionsSo.Width : existingItemData.DimensionsSo.Height;
                
                // AABB (Axis-Aligned Bounding Box) collision detection
                bool xOverlap = posX < existingItem.posX + existingWidth && posX + itemWidth > existingItem.posX;
                bool yOverlap = posY < existingItem.posY + existingHeight && posY + itemHeight > existingItem.posY;
                
                if (xOverlap && yOverlap)
                {
                    if (verboseLogging)
                        Debug.LogWarning($"[NetworkedPlayerInventory] Collision detected: {itemDataSOName} at [{posX}, {posY}] " +
                                         $"would collide with {existingItem.itemDataSOName} at [{existingItem.posX}, {existingItem.posY}]");
                    return true; // Collision detected
                }
            }
            
            return false; // No collision
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
            
            if (!IsOwner)
            {
                Debug.LogWarning($"[NetworkedPlayerInventory] Debug command only works on LOCAL player! " +
                                 $"This is Player {Owner.ClientId}'s inventory.");
                return;
            }
            
            // Add test items at different positions
            AddItem_ServerRpc("TestItem_1", 0, 0, 0, false, 1, Owner);
            AddItem_ServerRpc("TestItem_2", 0, 1, 0, false, 1, Owner);
            AddItem_ServerRpc("TestItem_3", 0, 2, 0, false, 1, Owner);
        }
        
        [ContextMenu("Debug: Print Configuration")]
        private void DebugPrintConfiguration()
        {
            Debug.Log($"[NetworkedPlayerInventory] === Configuration ===");
            Debug.Log($"  Grid Size: {gridWidth}x{gridHeight}");
            Debug.Log($"  Max Slots: {MaxInventorySlots}");
            Debug.Log($"  Collision Detection: {enableCollisionDetection}");
            Debug.Log($"  Boundary Validation: {enableBoundaryValidation}");
            Debug.Log($"  Verbose Logging: {verboseLogging}");
            Debug.Log($"  Current Items: {_inventoryItems.Count}/{MaxInventorySlots}");
        }
    }
}