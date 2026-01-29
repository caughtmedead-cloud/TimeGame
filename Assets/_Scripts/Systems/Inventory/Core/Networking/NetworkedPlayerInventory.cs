using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using NewThelos.Inventory.Data;
using NewThelos.Inventory.Runtime;
using NewThelos.Systems.Inventory.Utils;

namespace NewThelos.Inventory.Networking
{
    /// <summary>
    /// Server-authoritative networked inventory system.
    /// 
    /// ARCHITECTURE:
    /// - Server owns authoritative InventoryGrid instances
    /// - SyncList replicates changes to all clients
    /// - Clients maintain read-only mirror grids for UI
    /// - All mutations go through ServerRPCs
    /// </summary>
    public class NetworkedPlayerInventory : NetworkBehaviour
    {
        [Header("Grid Configuration")]
        [Tooltip("Grid configurations: gridId → (width, height)")]
        [SerializeField] private GridConfig[] gridConfigs = new GridConfig[]
        {
            new GridConfig { gridId = "main_inventory", width = 5, height = 4 }
        };
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;
        
        /// <summary>
        /// Public access to grid configurations
        /// </summary>
        public GridConfig[] GridConfigs => gridConfigs;
        
        // ===== SERVER STATE =====
        /// <summary>
        /// Server-authoritative grids. Key = gridId
        /// </summary>
        private Dictionary<string, InventoryGrid> _serverGrids = new Dictionary<string, InventoryGrid>();
        
        // ===== CLIENT STATE =====
        /// <summary>
        /// Client read-only mirror grids. Key = gridId
        /// </summary>
        private Dictionary<string, InventoryGrid> _clientGrids = new Dictionary<string, InventoryGrid>();
        
        // ===== NETWORK SYNC =====
        /// <summary>
        /// Synced list of all items across all grids.
        /// Server writes, clients read.
        /// </summary>
        private readonly SyncList<NetworkedItemData> _syncedItems = new SyncList<NetworkedItemData>(
            new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers)
        );
        
        // ===== EVENTS =====
        public event Action<NetworkedItemData> OnItemAdded;
        public event Action<string> OnItemRemoved; // instanceId
        public event Action<NetworkedItemData> OnItemMoved;
        
        // ===== LIFECYCLE =====
        
        private void Awake()
        {
            _syncedItems.OnChange += OnSyncedItemsChanged;
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            InitializeServerGrids();
            
            if (verboseLogging)
                Debug.Log($"[NetworkedPlayerInventory] Server initialized for {Owner.ClientId}");
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (!IsOwner)
            {
                if (verboseLogging)
                    Debug.Log($"[NetworkedPlayerInventory] Client observer - not owner");
                return;
            }
            
            InitializeClientGrids();
            
            if (verboseLogging)
                Debug.Log($"[NetworkedPlayerInventory] Client initialized (IsOwner: {IsOwner})");
        }
        
        // ===== SERVER INITIALIZATION =====
        
        [Server]
        private void InitializeServerGrids()
        {
            foreach (var config in gridConfigs)
            {
                _serverGrids[config.gridId] = new InventoryGrid(config.width, config.height);
                
                if (verboseLogging)
                    Debug.Log($"[Server] Created grid '{config.gridId}' ({config.width}x{config.height})");
            }
        }
        
        // ===== CLIENT INITIALIZATION =====
        
        [Client]
        private void InitializeClientGrids()
        {
            foreach (var config in gridConfigs)
            {
                _clientGrids[config.gridId] = new InventoryGrid(config.width, config.height);
                
                if (verboseLogging)
                    Debug.Log($"[Client] Created mirror grid '{config.gridId}' ({config.width}x{config.height})");
            }
        }
        
        // ===== SERVER RPCS (CLIENT → SERVER) =====
        
        /// <summary>
        /// Client requests to add item to inventory.
        /// Server validates and places item.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void AddItem_ServerRpc(string itemDefinitionId, string gridId, int posX, int posY, 
                                      bool isRotated = false, int stackCount = 1)
        {
            // Validate grid exists
            if (!_serverGrids.ContainsKey(gridId))
            {
                Debug.LogWarning($"[Server] AddItem rejected - invalid grid: {gridId}");
                return;
            }
            
            // Get item definition
            ItemDefinitionSO definition = ItemDefinitionRegistry.GetItemDefinition(itemDefinitionId);
            if (definition == null)
            {
                Debug.LogError($"[Server] Item definition '{itemDefinitionId}' not found");
                return;
            }
            
            // Auto-find position if not specified
            if (posX < 0 || posY < 0)
            {
                Debug.Log($"[Server] Auto-positioning {itemDefinitionId} (size: {definition.width}x{definition.height})");
                
                InventoryGrid searchGrid = _serverGrids[gridId];
                if (!searchGrid.TryFindEmptySpace(definition, out posX, out posY, isRotated))
                {
                    Debug.LogWarning($"[Server] No space found for {itemDefinitionId}");
                    return;
                }
                
                Debug.Log($"[Server] Found space at ({posX},{posY})");
            }
            
            // Create item instance
            InventoryItem newItem = new InventoryItem(itemDefinitionId, posX, posY, isRotated, stackCount);
            
            // Try to place in grid
            InventoryGrid grid = _serverGrids[gridId];
            if (grid.TryPlaceItem(newItem, definition, posX, posY, isRotated))
            {
                // Add to synced list for replication
                NetworkedItemData itemData = new NetworkedItemData(
                    newItem.instanceId,
                    itemDefinitionId,
                    gridId,
                    posX,
                    posY,
                    isRotated,
                    stackCount
                );
                
                _syncedItems.Add(itemData);
                
                Debug.Log($"[Server] ✅ Added {itemDefinitionId} (size: {definition.width}x{definition.height}) to {gridId} at ({posX},{posY})");
            }
            else
            {
                Debug.LogWarning($"[Server] Failed to place {itemDefinitionId} at ({posX},{posY}) in {gridId}");
            }
        }
        
        /// <summary>
        /// Client requests to move item.
        /// Server validates and moves item.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void MoveItem_ServerRpc(string instanceId, string targetGridId, int newPosX, int newPosY, bool newRotation)
        {
            // Find item in synced list
            int itemIndex = FindSyncedItemIndex(instanceId);
            if (itemIndex < 0)
            {
                Debug.LogWarning($"[Server] MoveItem rejected - item not found: {instanceId}");
                return;
            }
            
            NetworkedItemData oldData = _syncedItems[itemIndex];
            
            // Get source and target grids
            if (!_serverGrids.ContainsKey(oldData.gridId) || !_serverGrids.ContainsKey(targetGridId))
            {
                Debug.LogWarning($"[Server] MoveItem rejected - invalid grid");
                return;
            }
            
            InventoryGrid sourceGrid = _serverGrids[oldData.gridId];
            InventoryGrid targetGrid = _serverGrids[targetGridId];
            
            // Get item definition
            ItemDefinitionSO itemDef = ItemDefinitionRegistry.GetItemDefinition(oldData.itemDefinitionId);
            if (itemDef == null)
                return;
            
            // Get item from source grid
            InventoryItem item = sourceGrid.GetItemById(instanceId);
            if (item == null)
                return;
            
            // If moving to same grid, use TryMoveItem
            if (oldData.gridId == targetGridId)
            {
                bool success = sourceGrid.TryMoveItem(instanceId, itemDef, newPosX, newPosY, newRotation);
                
                if (success)
                {
                    // Update synced data
                    NetworkedItemData newData = new NetworkedItemData(
                        instanceId,
                        oldData.itemDefinitionId,
                        targetGridId,
                        newPosX,
                        newPosY,
                        newRotation,
                        oldData.stackCount
                    );
                    
                    _syncedItems[itemIndex] = newData;
                    
                    if (verboseLogging)
                        Debug.Log($"[Server] ✅ Moved {instanceId} to ({newPosX},{newPosY})");
                }
            }
            else
            {
                // Moving between grids: remove from source, add to target
                sourceGrid.TryRemoveItem(instanceId);
                
                InventoryItem newItem = new InventoryItem(
                    instanceId, 
                    oldData.itemDefinitionId, 
                    newPosX, 
                    newPosY, 
                    newRotation, 
                    oldData.stackCount
                );
                
                bool success = targetGrid.TryPlaceItem(newItem, itemDef, newPosX, newPosY, newRotation);
                
                if (success)
                {
                    NetworkedItemData newData = new NetworkedItemData(
                        instanceId,
                        oldData.itemDefinitionId,
                        targetGridId,
                        newPosX,
                        newPosY,
                        newRotation,
                        oldData.stackCount
                    );
                    
                    _syncedItems[itemIndex] = newData;
                    
                    if (verboseLogging)
                        Debug.Log($"[Server] ✅ Moved {instanceId} from {oldData.gridId} to {targetGridId}");
                }
                else
                {
                    // Failed - put back in source grid
                    sourceGrid.TryPlaceItem(item, itemDef, item.posX, item.posY, item.isRotated);
                }
            }
        }
        
        /// <summary>
        /// Client requests to remove item.
        /// Server validates and removes item.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void RemoveItem_ServerRpc(string instanceId)
        {
            int itemIndex = FindSyncedItemIndex(instanceId);
            if (itemIndex < 0)
            {
                Debug.LogWarning($"[Server] RemoveItem rejected - item not found: {instanceId}");
                return;
            }
            
            NetworkedItemData itemData = _syncedItems[itemIndex];
            
            // Remove from server grid
            if (_serverGrids.ContainsKey(itemData.gridId))
            {
                _serverGrids[itemData.gridId].TryRemoveItem(instanceId);
            }
            
            // Remove from SyncList
            _syncedItems.RemoveAt(itemIndex);
            
            if (verboseLogging)
                Debug.Log($"[Server] ✅ Removed {instanceId}");
        }
        
        // ===== SYNCLIST CALLBACKS =====
        
        /// <summary>
        /// Called when SyncList changes.
        /// Updates client mirror grids and fires events.
        /// </summary>
        private void OnSyncedItemsChanged(SyncListOperation op, int index, 
                                          NetworkedItemData oldItem, NetworkedItemData newItem, bool asServer)
        {
            // Only process on clients (not server)
            if (asServer)
                return;
            
            if (!IsOwner)
                return; // Only owner processes their own inventory
            
            switch (op)
            {
                case SyncListOperation.Add:
                    HandleClientItemAdded(newItem);
                    break;
                
                case SyncListOperation.RemoveAt:
                    HandleClientItemRemoved(oldItem);
                    break;
                
                case SyncListOperation.Set:
                    HandleClientItemMoved(oldItem, newItem);
                    break;
                
                case SyncListOperation.Clear:
                    HandleClientInventoryCleared();
                    break;
            }
        }
        
        [Client]
        private void HandleClientItemAdded(NetworkedItemData netData)
        {
            if (!_clientGrids.ContainsKey(netData.gridId))
            {
                Debug.LogWarning($"[Client] Item added to unknown grid: {netData.gridId}");
                return;
            }
            
            // Get item definition
            ItemDefinitionSO itemDef = ItemDefinitionRegistry.GetItemDefinition(netData.itemDefinitionId);
            if (itemDef == null)
                return;
            
            // Create item instance
            InventoryItem item = new InventoryItem(
                netData.instanceId,
                netData.itemDefinitionId,
                netData.posX,
                netData.posY,
                netData.isRotated,
                netData.stackCount
            );
            
            // Place in client mirror grid
            InventoryGrid clientGrid = _clientGrids[netData.gridId];
            bool success = clientGrid.TryPlaceItem(item, itemDef, netData.posX, netData.posY, netData.isRotated);
            
            if (success)
            {
                OnItemAdded?.Invoke(netData);
                
                if (verboseLogging)
                    Debug.Log($"[Client] ✅ Item added: {netData}");
            }
            else
            {
                Debug.LogError($"[Client] ❌ Failed to add item to mirror grid: {netData}");
            }
        }
        
        [Client]
        private void HandleClientItemRemoved(NetworkedItemData netData)
        {
            if (_clientGrids.ContainsKey(netData.gridId))
            {
                _clientGrids[netData.gridId].TryRemoveItem(netData.instanceId);
            }
            
            OnItemRemoved?.Invoke(netData.instanceId);
            
            if (verboseLogging)
                Debug.Log($"[Client] ✅ Item removed: {netData.instanceId}");
        }
        
        [Client]
        private void HandleClientItemMoved(NetworkedItemData oldData, NetworkedItemData newData)
        {
            // Determine which grid to work with
            string sourceGridId = string.IsNullOrEmpty(oldData.gridId) ? newData.gridId : oldData.gridId;
            
            ItemDefinitionSO itemDef = ItemDefinitionRegistry.GetItemDefinition(newData.itemDefinitionId);
            if (itemDef == null)
            {
                Debug.LogError($"[Client] Item definition '{newData.itemDefinitionId}' not found");
                return;
            }

            // Case 1: Moving within the same grid
            if (sourceGridId == newData.gridId)
            {
                if (_clientGrids.TryGetValue(newData.gridId, out InventoryGrid grid))
                {
                    // Use TryMoveItem to update the EXISTING InventoryItem object's position
                    bool moved = grid.TryMoveItem(newData.instanceId, itemDef, newData.posX, newData.posY, newData.isRotated);
                    
                    if (verboseLogging)
                    {
                        if (moved)
                            Debug.Log($"[Client] ✅ Moved {newData.itemDefinitionId} within grid '{newData.gridId}' to ({newData.posX},{newData.posY})");
                        else
                            Debug.LogWarning($"[Client] ⚠️ Failed to move {newData.itemDefinitionId} within grid '{newData.gridId}'");
                    }
                }
            }
            // Case 2: Moving between different grids
            else
            {
                // Remove from source grid
                if (_clientGrids.TryGetValue(sourceGridId, out InventoryGrid sourceGrid))
                {
                    sourceGrid.TryRemoveItem(newData.instanceId);
                    
                    if (verboseLogging)
                        Debug.Log($"[Client] Removed {newData.itemDefinitionId} from grid '{sourceGridId}'");
                }

                // Add to destination grid
                if (_clientGrids.TryGetValue(newData.gridId, out InventoryGrid destGrid))
                {
                    InventoryItem item = new InventoryItem(
                        newData.instanceId,
                        newData.itemDefinitionId,
                        newData.posX,
                        newData.posY,
                        newData.isRotated,
                        newData.stackCount
                    );
        
                    destGrid.TryPlaceItem(item, itemDef, newData.posX, newData.posY, newData.isRotated);
        
                    if (verboseLogging)
                        Debug.Log($"[Client] Placed {newData.itemDefinitionId} in grid '{newData.gridId}' at ({newData.posX},{newData.posY})");
                }
            }

            OnItemMoved?.Invoke(newData);

            if (verboseLogging)
                Debug.Log($"[Client] ✅ Item moved: {newData}");
        }
        
        [Client]
        private void HandleClientInventoryCleared()
        {
            foreach (var grid in _clientGrids.Values)
            {
                grid.Clear();
            }
            
            if (verboseLogging)
                Debug.Log($"[Client] ✅ Inventory cleared");
        }
        
        // ===== UTILITY METHODS =====
        
        private int FindSyncedItemIndex(string instanceId)
        {
            for (int i = 0; i < _syncedItems.Count; i++)
            {
                if (_syncedItems[i].instanceId == instanceId)
                    return i;
            }
            return -1;
        }
        
        /// <summary>
        /// Get client grid for UI access (owner only)
        /// </summary>
        public InventoryGrid GetClientGrid(string gridId)
        {
            return _clientGrids.ContainsKey(gridId) ? _clientGrids[gridId] : null;
        }
        
        /// <summary>
        /// Get all items (for UI display)
        /// </summary>
        public IReadOnlyList<NetworkedItemData> GetAllItems()
        {
            return _syncedItems;
        }
        
        #region Enhanced Drag & Drop Support

        /// <summary>
        /// Simple wrapper for drag & drop - maintains current rotation
        /// </summary>
        public void MoveItem(string gridId, string instanceId, int newX, int newY)
        {
            // Find current item data to preserve rotation
            int itemIndex = FindSyncedItemIndex(instanceId);
            if (itemIndex >= 0)
            {
                NetworkedItemData data = _syncedItems[itemIndex];
                MoveItem_ServerRpc(instanceId, gridId, newX, newY, data.isRotated);
            }
            else
            {
                Debug.LogWarning($"[Client] MoveItem failed - item not found: {instanceId}");
            }
        }

        /// <summary>
        /// Enhanced server move with automatic swap detection and execution
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void MoveItemWithSwap_ServerRpc(string instanceId, string targetGridId, int newPosX, int newPosY, bool newRotation)
        {
            // Find item in synced list
            int itemIndex = FindSyncedItemIndex(instanceId);
            if (itemIndex < 0)
            {
                Debug.LogWarning($"[Server] MoveItem rejected - item not found: {instanceId}");
                return;
            }
            
            NetworkedItemData oldData = _syncedItems[itemIndex];
            
            // Get grids
            if (!_serverGrids.ContainsKey(oldData.gridId) || !_serverGrids.ContainsKey(targetGridId))
            {
                Debug.LogWarning($"[Server] MoveItem rejected - invalid grid");
                return;
            }
            
            // Currently only support moving within same grid for swaps
            if (oldData.gridId != targetGridId)
            {
                // Fall back to standard move for cross-grid moves
                MoveItem_ServerRpc(instanceId, targetGridId, newPosX, newPosY, newRotation);
                return;
            }
            
            InventoryGrid grid = _serverGrids[oldData.gridId];
            
            // Get item definition
            ItemDefinitionSO itemDef = ItemDefinitionRegistry.GetItemDefinition(oldData.itemDefinitionId);
            if (itemDef == null)
                return;
            
            // Get the item
            InventoryItem item = grid.GetItemById(instanceId);
            if (item == null)
                return;
            
            int itemWidth = newRotation ? itemDef.height : itemDef.width;
            int itemHeight = newRotation ? itemDef.width : itemDef.height;
            
            // Check if target position has collision
            InventoryItem collidingItem = FindCollidingItem(grid, item, newPosX, newPosY, itemWidth, itemHeight);
            
            if (collidingItem != null)
            {
                // Attempt swap
                Debug.Log($"[Server] Detected collision with {collidingItem.itemDefinitionId}, attempting swap");
                ExecuteSwap(grid, item, collidingItem, itemDef, newPosX, newPosY, newRotation);
            }
            else
            {
                // Simple move
                bool success = grid.TryMoveItem(instanceId, itemDef, newPosX, newPosY, newRotation);
                
                if (success)
                {
                    // Update synced data
                    NetworkedItemData newData = new NetworkedItemData(
                        instanceId,
                        oldData.itemDefinitionId,
                        targetGridId,
                        newPosX,
                        newPosY,
                        newRotation,
                        oldData.stackCount
                    );
                    
                    _syncedItems[itemIndex] = newData;
                    
                    Debug.Log($"[Server] ✅ Moved {oldData.itemDefinitionId} to ({newPosX},{newPosY})");
                }
                else
                {
                    Debug.LogWarning($"[Server] ❌ Move failed for {oldData.itemDefinitionId}");
                }
            }
        }

        /// <summary>
        /// Find an item that would collide with the placement
        /// </summary>
        private InventoryItem FindCollidingItem(InventoryGrid grid, InventoryItem movingItem, 
            int targetX, int targetY, int width, int height)
        {
            foreach (InventoryItem otherItem in grid.GetAllItems())
            {
                // Skip self
                if (otherItem.instanceId == movingItem.instanceId)
                    continue;
                
                // Get other item dimensions
                ItemDefinitionSO otherDef = ItemDefinitionRegistry.GetItemDefinition(otherItem.itemDefinitionId);
                if (otherDef == null) continue;
                
                int otherWidth = otherItem.isRotated ? otherDef.height : otherDef.width;
                int otherHeight = otherItem.isRotated ? otherDef.width : otherDef.height;
                
                // AABB collision check
                if (targetX < otherItem.posX + otherWidth &&
                    targetX + width > otherItem.posX &&
                    targetY < otherItem.posY + otherHeight &&
                    targetY + height > otherItem.posY)
                {
                    return otherItem;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Execute a swap between two items
        /// </summary>
        private void ExecuteSwap(InventoryGrid grid, InventoryItem itemA, InventoryItem itemB, 
            ItemDefinitionSO itemADef, int newPosA_X, int newPosA_Y, bool newRotationA)
        {
            ItemDefinitionSO itemBDef = ItemDefinitionRegistry.GetItemDefinition(itemB.itemDefinitionId);
            if (itemBDef == null)
            {
                Debug.LogError("[Server] Cannot swap - itemB definition not found");
                return;
            }
            
            // Store original positions
            int originalPosA_X = itemA.posX;
            int originalPosA_Y = itemA.posY;
            bool originalRotationA = itemA.isRotated;
            
            int originalPosB_X = itemB.posX;
            int originalPosB_Y = itemB.posY;
            bool originalRotationB = itemB.isRotated;
            
            // Check if itemB can fit at itemA's original position
            int itemBWidth = originalRotationB ? itemBDef.height : itemBDef.width;
            int itemBHeight = originalRotationB ? itemBDef.width : itemBDef.height;
            
            // Bounds check for itemB at original position of itemA
            if (originalPosA_X + itemBWidth > grid.Width ||
                originalPosA_Y + itemBHeight > grid.Height)
            {
                Debug.LogWarning("[Server] ❌ Swap rejected - itemB won't fit at itemA's position (bounds)");
                return;
            }
            
            // Check collision for itemB at itemA's original position (excluding both swap items)
            foreach (InventoryItem gridItem in grid.GetAllItems())
            {
                if (gridItem.instanceId == itemA.instanceId ||
                    gridItem.instanceId == itemB.instanceId)
                    continue;
                
                ItemDefinitionSO gridItemDef = ItemDefinitionRegistry.GetItemDefinition(gridItem.itemDefinitionId);
                if (gridItemDef == null) continue;
                
                int gridItemWidth = gridItem.isRotated ? gridItemDef.height : gridItemDef.width;
                int gridItemHeight = gridItem.isRotated ? gridItemDef.width : gridItemDef.height;
                
                // Check collision
                if (originalPosA_X < gridItem.posX + gridItemWidth &&
                    originalPosA_X + itemBWidth > gridItem.posX &&
                    originalPosA_Y < gridItem.posY + gridItemHeight &&
                    originalPosA_Y + itemBHeight > gridItem.posY)
                {
                    Debug.LogWarning("[Server] ❌ Swap rejected - itemB would collide at itemA's position");
                    return;
                }
            }
            
            // Remove both items temporarily
            grid.TryRemoveItem(itemA.instanceId);
            grid.TryRemoveItem(itemB.instanceId);
            
            // Update positions
            itemA.posX = newPosA_X;
            itemA.posY = newPosA_Y;
            itemA.isRotated = newRotationA;
            
            itemB.posX = originalPosA_X;
            itemB.posY = originalPosA_Y;
            // itemB keeps its rotation
            
            // Try to place both back
            bool successA = grid.TryPlaceItem(itemA, itemADef, newPosA_X, newPosA_Y, newRotationA);
            bool successB = grid.TryPlaceItem(itemB, itemBDef, originalPosA_X, originalPosA_Y, originalRotationB);
            
            if (successA && successB)
            {
                // Update both in SyncList
                int indexA = FindSyncedItemIndex(itemA.instanceId);
                int indexB = FindSyncedItemIndex(itemB.instanceId);
                
                if (indexA >= 0)
                {
                    NetworkedItemData dataA = _syncedItems[indexA];
                    _syncedItems[indexA] = new NetworkedItemData(
                        dataA.instanceId,
                        dataA.itemDefinitionId,
                        dataA.gridId,
                        newPosA_X,
                        newPosA_Y,
                        newRotationA,
                        dataA.stackCount
                    );
                }
                
                if (indexB >= 0)
                {
                    NetworkedItemData dataB = _syncedItems[indexB];
                    _syncedItems[indexB] = new NetworkedItemData(
                        dataB.instanceId,
                        dataB.itemDefinitionId,
                        dataB.gridId,
                        originalPosA_X,
                        originalPosA_Y,
                        originalRotationB,
                        dataB.stackCount
                    );
                }
                
                Debug.Log($"[Server] ✅ Swapped {itemA.itemDefinitionId} and {itemB.itemDefinitionId}");
            }
            else
            {
                // Swap failed - revert everything
                Debug.LogError("[Server] ❌ Swap failed - reverting");
                
                grid.TryRemoveItem(itemA.instanceId);
                grid.TryRemoveItem(itemB.instanceId);
                
                itemA.posX = originalPosA_X;
                itemA.posY = originalPosA_Y;
                itemA.isRotated = originalRotationA;
                
                itemB.posX = originalPosB_X;
                itemB.posY = originalPosB_Y;
                itemB.isRotated = originalRotationB;
                
                grid.TryPlaceItem(itemA, itemADef, originalPosA_X, originalPosA_Y, originalRotationA);
                grid.TryPlaceItem(itemB, itemBDef, originalPosB_X, originalPosB_Y, originalRotationB);
            }
        }

        #endregion
        
        // ===== DEBUG =====
        
        [ContextMenu("Debug: Print Inventory (Server)")]
        private void DebugPrintServerInventory()
        {
            if (!IsServerStarted)
            {
                Debug.Log("Not running on server!");
                return;
            }
            
            Debug.Log($"=== SERVER INVENTORY ({Owner.ClientId}) ===");
            foreach (var kvp in _serverGrids)
            {
                Debug.Log($"Grid '{kvp.Key}': {kvp.Value.GetAllItems().Count} items");
                foreach (var item in kvp.Value.GetAllItems())
                {
                    Debug.Log($"  {item}");
                }
            }
        }
        
        [ContextMenu("Debug: Print Inventory (Client)")]
        private void DebugPrintClientInventory()
        {
            if (!IsClientStarted)
            {
                Debug.Log("Not running on client!");
                return;
            }
            
            Debug.Log($"=== CLIENT INVENTORY (IsOwner: {IsOwner}) ===");
            foreach (var kvp in _clientGrids)
            {
                Debug.Log($"Grid '{kvp.Key}': {kvp.Value.GetAllItems().Count} items");
                foreach (var item in kvp.Value.GetAllItems())
                {
                    Debug.Log($"  {item}");
                }
            }
        }
    }
    
    /// <summary>
    /// Grid configuration for Inspector
    /// </summary>
    [Serializable]
    public struct GridConfig
    {
        public string gridId;
        public int width;
        public int height;
    }
}