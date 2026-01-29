using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// FishNet NetworkBehaviour wrapper for InventoryTetris.
    /// 
    /// This script sits next to InventoryTetris and adds networking.
    /// It does NOT modify any CodeMonkey code - just wraps it.
    /// 
    /// How it works:
    /// 1. Client wants to place item → calls RequestPlaceItem()
    /// 2. RequestPlaceItem() sends ServerRpc to server
    /// 3. Server validates using CodeMonkey's TryPlaceItem()
    /// 4. If valid, server adds to SyncList
    /// 5. SyncList automatically replicates to all clients
    /// 6. Clients receive callback and place item locally using CodeMonkey
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedInventoryTetris : NetworkBehaviour
    {
        [Header("References")]
        [Tooltip("Drag the InventoryTetris component here (CodeMonkey system)")]
        [SerializeField] private InventoryTetris _localInventory;

        [Tooltip("Drag the InventoryTetrisRegistry asset here")]
        [SerializeField] private InventoryTetrisRegistry _itemRegistry;

        [Header("Debug")]
        [Tooltip("Enable to see detailed logs in console")]
        [SerializeField] private bool _verboseLogging = false;

        /// <summary>
        /// Network-synchronized list of all items in this inventory.
        /// FishNet automatically replicates changes to all clients.
        /// </summary>
        [SyncObject]
        private readonly SyncList<NetworkedItemPlacementData> _syncedItems = new SyncList<NetworkedItemPlacementData>();

        /// <summary>
        /// Local tracking: Maps item UID to the actual PlacedObject.
        /// Used for fast removal lookups.
        /// </summary>
        private Dictionary<Guid, PlacedObject> _uidToPlacedObject = new Dictionary<Guid, PlacedObject>();

        #region Unity Lifecycle

        private void Awake()
        {
            // Validate references are assigned
            if (_localInventory == null)
            {
                Debug.LogError("[NetworkedInventoryTetris] Missing InventoryTetris reference! Drag it in the inspector.", this);
            }

            if (_itemRegistry == null)
            {
                Debug.LogError("[NetworkedInventoryTetris] Missing InventoryTetrisRegistry reference! Drag it in the inspector.", this);
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Subscribe to SyncList changes
            // This callback fires whenever the list changes (add/remove/etc)
            _syncedItems.OnChange += OnSyncedItemsChanged;

            if (_verboseLogging)
            {
                Debug.Log($"[NetworkedInventoryTetris] Network started. IsServer={IsServerInitialized}, IsClient={IsClientInitialized}");
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            // Unsubscribe to prevent memory leaks
            _syncedItems.OnChange -= OnSyncedItemsChanged;
        }

        #endregion

        #region Server Authority - Validation & Execution

        /// <summary>
        /// SERVER ONLY: Client requests to place an item.
        /// Server validates using CodeMonkey logic, then adds to SyncList if valid.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerPlaceItem(string itemName, Vector2Int gridPosition, int directionIndex)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[NetworkedInventoryTetris] ServerPlaceItem called but not server!");
                return;
            }

            // Step 1: Look up the actual ItemTetrisSO from the registry
            ItemTetrisSO itemSO = _itemRegistry.GetItem(itemName);
            if (itemSO == null)
            {
                Debug.LogError($"[NetworkedInventoryTetris] Server cannot find item: {itemName}");
                return;
            }

            // Step 2: Convert direction int back to enum
            PlacedObjectTypeSO.Dir direction = (PlacedObjectTypeSO.Dir)directionIndex;

            // Step 3: Validate placement using CodeMonkey's logic (unchanged!)
            bool canPlace = _localInventory.TryPlaceItem(itemSO, gridPosition, direction, out PlacedObject placedObject);

            if (canPlace && placedObject != null)
            {
                // Success! Generate unique ID for this item instance
                Guid itemUID = Guid.NewGuid();

                // Track locally for removal operations
                _uidToPlacedObject[itemUID] = placedObject;

                // Add to synced list - FishNet automatically sends to all clients
                NetworkedItemPlacementData itemData = new NetworkedItemPlacementData(
                    itemName,
                    gridPosition,
                    directionIndex,
                    itemUID
                );

                _syncedItems.Add(itemData);

                if (_verboseLogging)
                {
                    Debug.Log($"[NetworkedInventoryTetris] ✅ Server placed: {itemName} at {gridPosition} dir={direction} uid={itemUID}");
                }
            }
            else
            {
                // Placement failed - CodeMonkey validation rejected it
                if (_verboseLogging)
                {
                    Debug.LogWarning($"[NetworkedInventoryTetris] ❌ Server rejected placement: {itemName} at {gridPosition}");
                }
            }
        }

        /// <summary>
        /// SERVER ONLY: Client requests to remove an item.
        /// Server finds the item, removes it using CodeMonkey logic, then removes from SyncList.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerRemoveItem(Vector2Int gridPosition)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[NetworkedInventoryTetris] ServerRemoveItem called but not server!");
                return;
            }

            // Find which item occupies this grid position
            NetworkedItemPlacementData? foundItem = null;
            int foundIndex = -1;

            for (int i = 0; i < _syncedItems.Count; i++)
            {
                NetworkedItemPlacementData item = _syncedItems[i];
                
                // Look up the item to get its occupied cells
                ItemTetrisSO itemSO = _itemRegistry.GetItem(item.itemSOName);
                if (itemSO == null) continue;

                PlacedObjectTypeSO.Dir direction = (PlacedObjectTypeSO.Dir)item.directionIndex;
                
                // Get all cells this item occupies (CodeMonkey handles multi-cell)
                List<Vector2Int> occupiedCells = itemSO.GetGridPositionList(item.gridPosition, direction);

                // Check if the clicked position is in this item's cells
                if (occupiedCells.Contains(gridPosition))
                {
                    foundItem = item;
                    foundIndex = i;
                    break;
                }
            }

            if (foundItem.HasValue && foundIndex >= 0)
            {
                // Remove from local inventory using CodeMonkey logic
                _localInventory.RemoveItemAt(gridPosition);

                // Remove from tracking dictionary
                if (_uidToPlacedObject.ContainsKey(foundItem.Value.itemUID))
                {
                    _uidToPlacedObject.Remove(foundItem.Value.itemUID);
                }

                // Remove from synced list - FishNet sends removal to all clients
                _syncedItems.RemoveAt(foundIndex);

                if (_verboseLogging)
                {
                    Debug.Log($"[NetworkedInventoryTetris] ✅ Server removed: {foundItem.Value.itemSOName} uid={foundItem.Value.itemUID}");
                }
            }
            else
            {
                if (_verboseLogging)
                {
                    Debug.LogWarning($"[NetworkedInventoryTetris] ❌ Server found no item to remove at {gridPosition}");
                }
            }
        }

        /// <summary>
        /// SERVER ONLY: Client requests to move an item (drag and drop).
        /// Implemented as remove from old position + place at new position.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerMoveItem(Vector2Int fromPosition, Vector2Int toPosition, int newDirectionIndex)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[NetworkedInventoryTetris] ServerMoveItem called but not server!");
                return;
            }

            // Find item at source position
            NetworkedItemPlacementData? foundItem = null;

            for (int i = 0; i < _syncedItems.Count; i++)
            {
                NetworkedItemPlacementData item = _syncedItems[i];
                
                ItemTetrisSO itemSO = _itemRegistry.GetItem(item.itemSOName);
                if (itemSO == null) continue;

                PlacedObjectTypeSO.Dir direction = (PlacedObjectTypeSO.Dir)item.directionIndex;
                List<Vector2Int> occupiedCells = itemSO.GetGridPositionList(item.gridPosition, direction);

                if (occupiedCells.Contains(fromPosition))
                {
                    foundItem = item;
                    break;
                }
            }

            if (foundItem.HasValue)
            {
                // Remove from old position
                ServerRemoveItem(fromPosition);

                // Place at new position with new rotation
                ServerPlaceItem(foundItem.Value.itemSOName, toPosition, newDirectionIndex);

                if (_verboseLogging)
                {
                    Debug.Log($"[NetworkedInventoryTetris] ✅ Server moved: {foundItem.Value.itemSOName} from {fromPosition} to {toPosition}");
                }
            }
        }

        #endregion

        #region Client Replication - SyncList Callbacks

        /// <summary>
        /// Called on clients when the SyncList changes.
        /// Replicates the server's changes to the local CodeMonkey inventory.
        /// </summary>
        private void OnSyncedItemsChanged(SyncListOperation op, int index, NetworkedItemPlacementData oldItem, NetworkedItemPlacementData newItem, bool asServer)
        {
            // Server already updated locally in ServerRpc methods, skip double-update
            if (asServer) return;

            // Client replication based on operation type
            switch (op)
            {
                case SyncListOperation.Add:
                    ClientReplicateAdd(newItem);
                    break;

                case SyncListOperation.RemoveAt:
                    ClientReplicateRemove(oldItem);
                    break;

                case SyncListOperation.Set:
                    // Item was replaced - remove old, add new
                    ClientReplicateRemove(oldItem);
                    ClientReplicateAdd(newItem);
                    break;

                case SyncListOperation.Clear:
                    ClientReplicateClear();
                    break;

                case SyncListOperation.Insert:
                    ClientReplicateAdd(newItem);
                    break;
            }
        }

        /// <summary>
        /// CLIENT ONLY: Replicate item addition from server.
        /// </summary>
        private void ClientReplicateAdd(NetworkedItemPlacementData itemData)
        {
            // Look up the ItemTetrisSO from registry
            ItemTetrisSO itemSO = _itemRegistry.GetItem(itemData.itemSOName);
            if (itemSO == null)
            {
                Debug.LogError($"[NetworkedInventoryTetris] Client cannot find item: {itemData.itemSOName}");
                return;
            }

            // Convert direction
            PlacedObjectTypeSO.Dir direction = (PlacedObjectTypeSO.Dir)itemData.directionIndex;

            // Place item using CodeMonkey logic (same code as server!)
            bool placed = _localInventory.TryPlaceItem(itemSO, itemData.gridPosition, direction, out PlacedObject placedObject);

            if (placed && placedObject != null)
            {
                // Track for removal
                _uidToPlacedObject[itemData.itemUID] = placedObject;

                if (_verboseLogging)
                {
                    Debug.Log($"[NetworkedInventoryTetris] 📥 Client replicated add: {itemData.itemSOName} at {itemData.gridPosition}");
                }
            }
            else
            {
                Debug.LogError($"[NetworkedInventoryTetris] ❌ Client failed to replicate add: {itemData.itemSOName} at {itemData.gridPosition}");
            }
        }

        /// <summary>
        /// CLIENT ONLY: Replicate item removal from server.
        /// </summary>
        private void ClientReplicateRemove(NetworkedItemPlacementData itemData)
        {
            // Remove using CodeMonkey logic
            _localInventory.RemoveItemAt(itemData.gridPosition);

            // Remove from tracking
            if (_uidToPlacedObject.ContainsKey(itemData.itemUID))
            {
                _uidToPlacedObject.Remove(itemData.itemUID);
            }

            if (_verboseLogging)
            {
                Debug.Log($"[NetworkedInventoryTetris] 📥 Client replicated remove: {itemData.itemSOName} uid={itemData.itemUID}");
            }
        }

        /// <summary>
        /// CLIENT ONLY: Clear all items (rarely used).
        /// </summary>
        private void ClientReplicateClear()
        {
            _uidToPlacedObject.Clear();

            if (_verboseLogging)
            {
                Debug.Log("[NetworkedInventoryTetris] 📥 Client replicated clear");
            }
        }

        #endregion

        #region Public API - For UI/Input Systems

        /// <summary>
        /// Request to place an item. Routes to server for validation.
        /// Call this from your UI or input code.
        /// </summary>
        public void RequestPlaceItem(ItemTetrisSO item, Vector2Int position, PlacedObjectTypeSO.Dir direction)
        {
            if (item == null)
            {
                Debug.LogWarning("[NetworkedInventoryTetris] Cannot place null item");
                return;
            }

            ServerPlaceItem(item.nameString, position, (int)direction);
        }

        /// <summary>
        /// Request to remove an item. Routes to server for validation.
        /// Call this from your UI when player clicks to remove.
        /// </summary>
        public void RequestRemoveItem(Vector2Int position)
        {
            ServerRemoveItem(position);
        }

        /// <summary>
        /// Request to move/rotate an item. Routes to server for validation.
        /// Call this from drag-drop UI code.
        /// </summary>
        public void RequestMoveItem(Vector2Int fromPosition, Vector2Int toPosition, PlacedObjectTypeSO.Dir newDirection)
        {
            ServerMoveItem(fromPosition, toPosition, (int)newDirection);
        }

        /// <summary>
        /// Get the local InventoryTetris instance.
        /// Use this for read-only operations like checking if space is available.
        /// </summary>
        public InventoryTetris GetLocalInventory()
        {
            return _localInventory;
        }

        #endregion

        #region Debug Utilities

#if UNITY_EDITOR
        [ContextMenu("Debug: Print Synced Items")]
        private void DebugPrintSyncedItems()
        {
            Debug.Log($"[NetworkedInventoryTetris] Synced Items Count: {_syncedItems.Count}");
            for (int i = 0; i < _syncedItems.Count; i++)
            {
                var item = _syncedItems[i];
                Debug.Log($"  [{i}] {item.itemSOName} at {item.gridPosition} dir={item.directionIndex} uid={item.itemUID}");
            }
        }
#endif

        #endregion
    }
}
