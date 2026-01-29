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
    /// How it works:
    /// 1. Client wants to place item → calls RequestPlaceItem()
    /// 2. → Sends ServerRpc to server
    /// 3. Server validates using CodeMonkey's TryPlaceItem()
    /// 4. If valid → adds to SyncList
    /// 5. SyncList automatically replicates to all clients
    /// 6. Clients receive callback → place item locally using CodeMonkey
    /// 
    /// CodeMonkey code is NEVER modified - we just wrap it!
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedInventoryTetris : NetworkBehaviour
    {
        [Header("References")]
        [Tooltip("The local InventoryTetris instance (CodeMonkey system, unchanged)")]
        [SerializeField] private InventoryTetris _localInventory;

        [Tooltip("Registry for looking up ItemTetrisSO by name")]
        [SerializeField] private InventoryTetrisRegistry _itemRegistry;

        [Header("Configuration")]
        [Tooltip("Enable detailed logging for debugging network issues")]
        [SerializeField] private bool _verboseLogging = false;

        /// <summary>
        /// Network-synchronized list of all items in this inventory.
        /// FishNet automatically replicates changes to all clients.
        /// </summary>
        [SyncObject]
        private readonly SyncList<NetworkedItemPlacementData> _syncedItems = new SyncList<NetworkedItemPlacementData>();

        /// <summary>
        /// Tracks which PlacedObject corresponds to which UID.
        /// Used for fast removal lookups.
        /// </summary>
        private Dictionary<Guid, PlacedObject> _uidToPlacedObject = new Dictionary<Guid, PlacedObject>();

        #region Unity Lifecycle

        private void Awake()
        {
            // Validate that references are assigned
            if (_localInventory == null)
            {
                Debug.LogError("[NetworkedInventoryTetris] Missing InventoryTetris reference! Assign in inspector.", this);
            }

            if (_itemRegistry == null)
            {
                Debug.LogError("[NetworkedInventoryTetris] Missing InventoryTetrisRegistry reference! Assign in inspector.", this);
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Subscribe to SyncList changes - this is how clients learn about inventory updates
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

        #region Server RPCs (Client → Server Requests)

        /// <summary>
        /// Client requests to place an item in the inventory.
        /// Server validates and executes if placement is valid.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerPlaceItem(string itemName, Vector2Int gridPosition, int directionIndex)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[NetworkedInventoryTetris] ServerPlaceItem called but not server!");
                return;
            }

            // Look up the item in our registry
            ItemTetrisSO itemSO = _itemRegistry.GetItem(itemName);
            if (itemSO == null)
            {
                Debug.LogError($"[NetworkedInventoryTetris] Server cannot find item: {itemName}");
                return;
            }

            // Convert direction index back to enum
            PlacedObjectTypeSO.Dir direction = (PlacedObjectTypeSO.Dir)directionIndex;

            // Use CodeMonkey's validation logic - if it says no, we don't place it
            bool canPlace = _localInventory.TryPlaceItem(itemSO, gridPosition, direction, out PlacedObject placedObject);

            if (canPlace && placedObject != null)
            {
                // Success! Generate a unique ID for this item instance
                Guid itemUID = Guid.NewGuid();

                // Track it locally for when we need to remove it
                _uidToPlacedObject[itemUID] = placedObject;

                // Add to synced list - FishNet automatically sends this to all clients
                NetworkedItemPlacementData itemData = new NetworkedItemPlacementData(
                    itemName,
                    gridPosition,
                    directionIndex,
                    itemUID
                );

                _syncedItems.Add(itemData);

                if (_verboseLogging)
                {
                    Debug.Log($"[NetworkedInventoryTetris] Server placed: {itemName} at {gridPosition} dir={direction} uid={itemUID}");
                }
            }
            else
            {
                if (_verboseLogging)
                {
                    Debug.LogWarning($"[NetworkedInventoryTetris] Server rejected placement: {itemName} at {gridPosition} (collision or out of bounds)");
                }
            }
        }

        /// <summary>
        /// Client requests to remove an item from the inventory.
        /// Server validates and executes if item exists.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerRemoveItem(Vector2Int gridPosition)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[NetworkedInventoryTetris] ServerRemoveItem called but not server!");
                return;
            }

            // Find which item (if any) occupies this grid position
            NetworkedItemPlacementData? foundItem = null;
            int foundIndex = -1;

            for (int i = 0; i < _syncedItems.Count; i++)
            {
                NetworkedItemPlacementData item = _syncedItems[i];
                
                // Get the item SO to check its occupied cells
                ItemTetrisSO itemSO = _itemRegistry.GetItem(item.itemSOName);
                if (itemSO == null) continue;

                PlacedObjectTypeSO.Dir direction = (PlacedObjectTypeSO.Dir)item.directionIndex;
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
                // Remove from CodeMonkey's inventory
                _localInventory.RemoveItemAt(gridPosition);

                // Remove from our UID tracking
                if (_uidToPlacedObject.ContainsKey(foundItem.Value.itemUID))
                {
                    _uidToPlacedObject.Remove(foundItem.Value.itemUID);
                }

                // Remove from synced list - FishNet automatically sends removal to all clients
                _syncedItems.RemoveAt(foundIndex);

                if (_verboseLogging)
                {
                    Debug.Log($"[NetworkedInventoryTetris] Server removed: {foundItem.Value.itemSOName} uid={foundItem.Value.itemUID}");
                }
            }
            else
            {
                if (_verboseLogging)
                {
                    Debug.LogWarning($"[NetworkedInventoryTetris] Server found no item to remove at {gridPosition}");
                }
            }
        }

        /// <summary>
        /// Client requests to move an item within the inventory.
        /// Implemented as remove + place for simplicity.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerMoveItem(Vector2Int fromPosition, Vector2Int toPosition, int newDirectionIndex)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[NetworkedInventoryTetris] ServerMoveItem called but not server!");
                return;
            }

            // Find the item at the source position
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

                // Place at new position with potentially new rotation
                ServerPlaceItem(foundItem.Value.itemSOName, toPosition, newDirectionIndex);
            }
        }

        #endregion

        #region SyncList Callbacks (Server → Client Replication)

        /// <summary>
        /// Called automatically by FishNet when the synced items list changes.
        /// This is how clients learn about inventory changes and replicate them locally.
        /// </summary>
        private void OnSyncedItemsChanged(SyncListOperation op, int index, NetworkedItemPlacementData oldItem, NetworkedItemPlacementData newItem, bool asServer)
        {
            // Server already updated locally in the ServerRpc methods, don't do it twice
            if (asServer) return;

            // Client-side replication based on what changed
            switch (op)
            {
                case SyncListOperation.Add:
                    ClientReplicateAdd(newItem);
                    break;

                case SyncListOperation.RemoveAt:
                    ClientReplicateRemove(oldItem);
                    break;

                case SyncListOperation.Set:
                    // Item was updated (shouldn't happen in our use case, but handle it)
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
        /// Client replication: Add an item to local CodeMonkey inventory.
        /// </summary>
        private void ClientReplicateAdd(NetworkedItemPlacementData itemData)
        {
            // Look up the item SO
            ItemTetrisSO itemSO = _itemRegistry.GetItem(itemData.itemSOName);
            if (itemSO == null)
            {
                Debug.LogError($"[NetworkedInventoryTetris] Client cannot find item: {itemData.itemSOName}");
                return;
            }

            // Convert direction
            PlacedObjectTypeSO.Dir direction = (PlacedObjectTypeSO.Dir)itemData.directionIndex;

            // Place using CodeMonkey's logic
            bool placed = _localInventory.TryPlaceItem(itemSO, itemData.gridPosition, direction, out PlacedObject placedObject);

            if (placed && placedObject != null)
            {
                // Track for future removal
                _uidToPlacedObject[itemData.itemUID] = placedObject;

                if (_verboseLogging)
                {
                    Debug.Log($"[NetworkedInventoryTetris] Client replicated add: {itemData.itemSOName} at {itemData.gridPosition}");
                }
            }
            else
            {
                Debug.LogError($"[NetworkedInventoryTetris] Client failed to replicate add: {itemData.itemSOName} at {itemData.gridPosition}");
            }
        }

        /// <summary>
        /// Client replication: Remove an item from local CodeMonkey inventory.
        /// </summary>
        private void ClientReplicateRemove(NetworkedItemPlacementData itemData)
        {
            // Remove using CodeMonkey's logic
            _localInventory.RemoveItemAt(itemData.gridPosition);

            // Remove from UID tracking
            if (_uidToPlacedObject.ContainsKey(itemData.itemUID))
            {
                _uidToPlacedObject.Remove(itemData.itemUID);
            }

            if (_verboseLogging)
            {
                Debug.Log($"[NetworkedInventoryTetris] Client replicated remove: {itemData.itemSOName} uid={itemData.itemUID}");
            }
        }

        /// <summary>
        /// Client replication: Clear all items from local inventory.
        /// </summary>
        private void ClientReplicateClear()
        {
            _uidToPlacedObject.Clear();

            if (_verboseLogging)
            {
                Debug.Log("[NetworkedInventoryTetris] Client replicated clear");
            }
        }

        #endregion

        #region Public API (For UI/Input Systems)

        /// <summary>
        /// Request to place an item. Call this from your UI/input code.
        /// Routes to server for validation.
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
        /// Request to remove an item. Call this from your UI/input code.
        /// Routes to server for validation.
        /// </summary>
        public void RequestRemoveItem(Vector2Int position)
        {
            ServerRemoveItem(position);
        }

        /// <summary>
        /// Request to move/rotate an item. Call this from your UI/input code.
        /// Routes to server for validation.
        /// </summary>
        public void RequestMoveItem(Vector2Int fromPosition, Vector2Int toPosition, PlacedObjectTypeSO.Dir newDirection)
        {
            ServerMoveItem(fromPosition, toPosition, (int)newDirection);
        }

        /// <summary>
        /// Get the local InventoryTetris instance.
        /// Use this for read-only operations like checking what's in the inventory.
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
