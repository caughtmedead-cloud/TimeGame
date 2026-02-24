using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using TimeGame.Systems.GridPlacement;
using TimeGame.Systems.Inventory;
using TimeGame.Systems.Inventory.UI;
using TimeGame.Systems.Networking.Inventory;

/// <summary>
/// Phase 6 — NetworkedWorldLootContainer
///
/// Attach alongside WorldLootContainer on any world chest / crate / bag that should
/// be shared across all players in a multiplayer session.
///
/// DESIGN
/// ──────
/// • Server is the sole authority for container contents.
/// • Multiple players may VIEW the same container simultaneously — no lock is held.
/// • When a player takes an item, the server validates path + existence then removes it
///   from the server-side InventorySystems and pushes an ObserversRpc so every viewer
///   sees the item disappear.
/// • Loot is rolled on the server during OnStartServer (or on first open request if lazy).
/// • A full contents snapshot is sent to a requesting client via TargetRpc.
///
/// PATH-BASED ITEM ADDRESSING
/// ──────────────────────────
/// Each "take" request includes a Guid[] containerPath describing the nesting from the
/// root container's first compartment all the way down to the direct parent holding the
/// target item:
///
///   [chestRootGuid]                      → item is directly in the chest's compartment
///   [chestRootGuid, backpackInstanceGuid] → item is inside a backpack sitting in the chest
///
/// The server walks this path through the live InventorySystems to locate the exact
/// PlacedItem.  If any ancestor is missing (taken by another player) the walk fails
/// gracefully.
///
/// REPLICATED STATE
/// ────────────────
/// • SyncVar _isOpen — cosmetic door-open/close animation for all clients
/// • SyncVar _hasBeenLooted — prevents re-rolling loot after the first open
/// • Item removal is pushed via ObserversRpc so all viewers stay in sync
/// </summary>
namespace TimeGame.Systems.Networking.Inventory
{
    [RequireComponent(typeof(FishNet.Object.NetworkObject))]
    [RequireComponent(typeof(WorldLootContainer))]
    public class NetworkedWorldLootContainer : NetworkBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private InventoryItemRegistry itemRegistry;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // ─── SyncVars ─────────────────────────────────────────────────────────
        // FishNet V4: use SyncVar<T> generic field, NOT the [SyncVar] attribute.

        private readonly SyncVar<bool> _isOpen       = new SyncVar<bool>(false);
        private readonly SyncVar<bool> _hasBeenLooted = new SyncVar<bool>(false);

        // ─── Server state ─────────────────────────────────────────────────────

        /// <summary>
        /// Server-side live inventory systems, indexed by compartment index.
        /// Built once during OnStartServer from WorldLootContainer's loot table.
        /// After that all mutations go through SvrTakeItemFromContainer.
        /// </summary>
        private List<InventorySystem> _serverCompartments = new List<InventorySystem>();

        private WorldLootContainer _worldLootContainer;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity / FishNet lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _worldLootContainer = GetComponent<WorldLootContainer>();

            if (itemRegistry == null)
                itemRegistry = Resources.Load<InventoryItemRegistry>("InventoryItemRegistry");

            // FishNet V4: subscribe SyncVar OnChange callbacks here, not via attribute.
            _isOpen.OnChange += OnIsOpenChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerBuildCompartments();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Server — compartment construction
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Build server-side InventorySystems that mirror WorldLootContainer's compartment
        /// definitions.
        ///
        /// Uses WorldLootContainer.GetLootContainer() which delegates to EnsureLootPopulated()
        /// to roll loot exactly once before returning the data — a UI-free path that is safe to
        /// call on the server which has no ContainerInteractionManager.
        ///
        /// In a full multiplayer session, ServerWorldInitializer.InitializeLootContainers() will
        /// have already called EnsureLootPopulated() on every WorldLootContainer before this runs,
        /// so loot rolling is effectively a no-op here and happens in a single, controlled phase.
        /// </summary>
        [Server]
        private void ServerBuildCompartments()
        {
            if (_worldLootContainer == null) return;

            // Populate loot and grab the LootContainer without touching any UI.
            LootContainer loot = _worldLootContainer.GetLootContainer();

            _serverCompartments.Clear();

            if (loot != null)
            {
                foreach (ContainerCompartment compartment in loot.GetCompartments())
                {
                    if (compartment.InventorySystem != null)
                        _serverCompartments.Add(compartment.InventorySystem);
                }
            }

            _hasBeenLooted.Value = false;
            Log($"[Server] Built {_serverCompartments.Count} compartment(s) for '{_worldLootContainer.DisplayName}'.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SyncVar callbacks
        // ─────────────────────────────────────────────────────────────────────

        private void OnIsOpenChanged(bool prev, bool next, bool asServer)
        {
            // Drive door open/close animation here (Phase 7+)
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API — called by NetworkedInventoryComponent RPCs
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Server-side: snapshot the entire container and return it so
        /// NetworkedInventoryComponent can send it to the requesting client.
        /// </summary>
        [Server]
        public NetContainerSnapshot ServerGetSnapshot()
        {
            NetCompartmentSnapshot[] compartmentSnaps =
                new NetCompartmentSnapshot[_serverCompartments.Count];

            for (int c = 0; c < _serverCompartments.Count; c++)
            {
                InventorySystem inv = _serverCompartments[c];
                IReadOnlyCollection<PlacedItem> items = inv.GetAllItems();

                NetPlacedItemData[] netItems = new NetPlacedItemData[items.Count];
                int idx = 0;
                foreach (PlacedItem item in items)
                {
                    PlacedItemData snapshot = PlacedItemData.FromPlacedItem(item);
                    netItems[idx++] = InventoryNetConverter.ToNetPlacedItem(snapshot);
                }

                compartmentSnaps[c] = new NetCompartmentSnapshot
                {
                    GridSize  = new Vector2Int(inv.Width, inv.Height),
                    MaxWeight = inv.MaxWeight,
                    Items     = netItems
                };
            }

            return new NetContainerSnapshot { Compartments = compartmentSnaps };
        }

        /// <summary>
        /// Server-side: peek at an item in the container and return its InventoryItemSO name
        /// WITHOUT removing or modifying it.
        ///
        /// Used by NetworkedInventoryComponent.SvrTakeItemFromContainer to pre-validate that
        /// the item's definition exists in the registry BEFORE committing the removal.
        /// This mirrors the early-exit pattern in SvrPickupItem and prevents the silent data
        /// corruption where an item is removed from _serverCompartments but never added to
        /// _serverManifest (which would cause all subsequent PUT operations to fail with
        /// "Item not in your inventory" and the item to vanish on container reopen).
        ///
        /// Returns null if the compartment index is out of range, the path walk fails, the
        /// target item is not found, or the item's definition is not an InventoryItemSO.
        /// </summary>
        [Server]
        public string ServerGetItemDefName(Guid[] path, Guid targetId, int compartmentIndex)
        {
            if (compartmentIndex < 0 || compartmentIndex >= _serverCompartments.Count)
                return null;

            InventorySystem currentInv = _serverCompartments[compartmentIndex];

            if (path != null)
            {
                foreach (Guid pathId in path)
                {
                    PlacedItem ancestor = FindItemById(currentInv, pathId);
                    if (ancestor?.ContainerInventory == null) return null;
                    currentInv = ancestor.ContainerInventory;
                }
            }

            PlacedItem target = FindItemById(currentInv, targetId);
            InventoryItemSO itemDef = target?.ItemDefinition as InventoryItemSO;
            return itemDef?.ItemName;
        }

        /// <summary>
        /// Server-side: attempt to remove a specific item using a path + target InstanceID.
        ///
        /// Returns a NetTakeResult describing success or denial.
        ///
        /// PATH WALK:
        ///   - containerPath[0] is the InstanceID of a PlacedItem in compartment 0 of THIS container.
        ///   - Each subsequent element is the InstanceID of a PlacedItem inside the previous item's
        ///     ContainerInventory.
        ///   - The final step looks up targetInstanceId in the last inventory encountered.
        ///   - A path of length 0 means the item is directly in this container's compartment 0.
        /// </summary>
        [Server]
        public NetTakeResult ServerTryTakeItem(
            Guid[] containerPath,
            Guid   targetInstanceId,
            int    compartmentIndex = 0,
            int    splitCount = -1)    // -1 = remove whole item; >0 = reduce stack by this amount
        {
            if (_serverCompartments.Count == 0)
                return NetTakeResult.Deny("Container has no compartments.");

            if (compartmentIndex < 0 || compartmentIndex >= _serverCompartments.Count)
                return NetTakeResult.Deny("Invalid compartment index.");

            InventorySystem currentInv = _serverCompartments[compartmentIndex];

            // Walk the path (may be empty — item is directly in root compartment)
            if (containerPath != null && containerPath.Length > 0)
            {
                foreach (Guid pathId in containerPath)
                {
                    PlacedItem ancestor = FindItemById(currentInv, pathId);
                    if (ancestor == null)
                        return NetTakeResult.Deny($"Path ancestor {ShortGuid(pathId)} not found — item may have been taken.");

                    if (ancestor.ContainerInventory == null)
                        return NetTakeResult.Deny($"Path ancestor {ShortGuid(pathId)} is not a container.");

                    currentInv = ancestor.ContainerInventory;
                }
            }

            // Find target in the resolved inventory
            PlacedItem target = FindItemById(currentInv, targetInstanceId);
            if (target == null)
                return NetTakeResult.Deny($"Target item {ShortGuid(targetInstanceId)} not found — may have been taken by another player.");

            bool isPartialTake = splitCount > 0 && splitCount < target.StackCount;
            int takenCount = isPartialTake ? splitCount : target.StackCount;

            // Snapshot BEFORE modifying the item so full metadata (instances, container, etc.)
            // is captured.  We override StackCount below to reflect only the taken portion.
            PlacedItemData snapshot = PlacedItemData.FromPlacedItem(target);
            NetPlacedItemData netData = InventoryNetConverter.ToNetPlacedItem(snapshot);
            netData.StackCount = takenCount; // return only the taken portion to the caller

            if (isPartialTake)
            {
                // Partial take (stack split): reduce the stack in place — item stays in container.
                target.SetStackCount(target.StackCount - splitCount);

                Log($"[Server] TakeItem (partial) — {snapshot.ItemDefinition?.ItemName} " +
                    $"id={ShortGuid(targetInstanceId)}  took={splitCount}  remaining={target.StackCount}" +
                    $"  from '{_worldLootContainer.DisplayName}'.");

                // Notify all observers that the count decreased (item is NOT fully removed).
                RpcItemCountChangedInContainer(
                    compartmentIndex,
                    containerPath ?? Array.Empty<Guid>(),
                    targetInstanceId,
                    target.StackCount);
            }
            else
            {
                // Full take: remove the item entirely from the server inventory.
                currentInv.RemoveItem(targetInstanceId);

                Log($"[Server] TakeItem (full) — {snapshot.ItemDefinition?.ItemName} " +
                    $"id={ShortGuid(targetInstanceId)} removed from '{_worldLootContainer.DisplayName}'.");

                // Notify all observers so every viewer sees the item disappear.
                RpcItemRemovedFromContainer(compartmentIndex, containerPath ?? Array.Empty<Guid>(), targetInstanceId);
            }

            return NetTakeResult.Grant(netData);
        }

        /// <summary>
        /// Server-side: reposition an item that is already inside this container to a new
        /// compartment and/or grid position without removing it from the container.
        ///
        /// Called by NetworkedInventoryComponent.SvrMoveItemInContainer for two cases:
        ///   • Same-compartment drag  (sourceCompartmentIndex == targetCompartmentIndex)
        ///   • Cross-compartment drag within the same container
        ///
        /// On success, broadcasts RpcItemMovedInContainer so every viewing client's local
        /// InventorySystem (and UI) stays in sync with the server-authoritative position.
        ///
        /// Returns false if the source item cannot be found or the target position is occupied;
        /// in the failure case the item is restored to its original position.
        /// </summary>
        [Server]
        public bool ServerTryMoveItem(
            int           sourceCompartmentIndex,
            int           targetCompartmentIndex,
            Guid          itemInstanceId,
            Vector2Int    newGridPosition,
            GridDirection newRotation,
            Guid[]        containerPath = null)   // path from root compartment to the inventory holding the item
        {
            if (sourceCompartmentIndex < 0 || sourceCompartmentIndex >= _serverCompartments.Count)
            {
                Log($"[Server] ServerTryMoveItem FAIL — invalid source compartment {sourceCompartmentIndex}.");
                return false;
            }

            if (targetCompartmentIndex < 0 || targetCompartmentIndex >= _serverCompartments.Count)
            {
                Log($"[Server] ServerTryMoveItem FAIL — invalid target compartment {targetCompartmentIndex}.");
                return false;
            }

            InventorySystem sourceInv = _serverCompartments[sourceCompartmentIndex];

            // Walk the container path to reach the nested InventorySystem (if any).
            if (containerPath != null && containerPath.Length > 0)
            {
                foreach (Guid pathId in containerPath)
                {
                    PlacedItem ancestor = FindItemById(sourceInv, pathId);
                    if (ancestor?.ContainerInventory == null)
                    {
                        Log($"[Server] ServerTryMoveItem FAIL — path walk failed at {ShortGuid(pathId)}.");
                        return false;
                    }
                    sourceInv = ancestor.ContainerInventory;
                }
            }

            // For nested moves, source and target are the same inventory.
            InventorySystem targetInv = (containerPath != null && containerPath.Length > 0)
                ? sourceInv
                : _serverCompartments[targetCompartmentIndex];

            // Locate the item and capture its current state BEFORE removal.
            PlacedItem item = FindItemById(sourceInv, itemInstanceId);
            if (item == null)
            {
                Log($"[Server] ServerTryMoveItem FAIL — item {ShortGuid(itemInstanceId)} " +
                    $"not found (pathDepth={containerPath?.Length ?? 0}).");
                return false;
            }

            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            if (itemDef == null)
            {
                Log($"[Server] ServerTryMoveItem FAIL — item {ShortGuid(itemInstanceId)} " +
                    $"has no InventoryItemSO definition.");
                return false;
            }

            int           stackCount  = item.StackCount;
            Vector2Int    oldPosition = item.AnchorPosition;
            GridDirection oldRotation = item.Rotation;

            // Remove from source (clears its old cells so they don't block the new position).
            sourceInv.RemoveItem(itemInstanceId);

            // Attempt placement at the requested position.
            bool success = targetInv.TryAddItem(
                itemInstanceId, itemDef, newGridPosition, newRotation,
                out _, stackCount);

            if (!success)
            {
                // Target position is occupied or out of bounds — restore to original position.
                bool restored = sourceInv.TryAddItem(
                    itemInstanceId, itemDef, oldPosition, oldRotation,
                    out _, stackCount);

                if (!restored)
                {
                    Debug.LogError($"[NetworkedWorldLootContainer] ServerTryMoveItem — CRITICAL: " +
                        $"could not restore item {ShortGuid(itemInstanceId)} to original " +
                        $"position {oldPosition} after failed move!");
                }

                Log($"[Server] ServerTryMoveItem FAIL — target pos={newGridPosition} occupied.");
                return false;
            }

            Log($"[Server] ServerTryMoveItem — {itemDef.ItemName} id={ShortGuid(itemInstanceId)} " +
                $"comp {sourceCompartmentIndex}→{targetCompartmentIndex} " +
                $"pos={newGridPosition} rot={newRotation} pathDepth={containerPath?.Length ?? 0}.");

            // Broadcast to all clients so every viewer's UI reflects the new position.
            RpcItemMovedInContainer(
                sourceCompartmentIndex, targetCompartmentIndex,
                itemInstanceId, newGridPosition, newRotation,
                containerPath ?? Array.Empty<Guid>());

            return true;
        }

        /// <summary>
        /// Server-side: attempt to add an item to a container compartment.
        /// Called by NetworkedInventoryComponent.SvrPutItemIntoContainer after verifying the
        /// player owns the item in their server manifest.
        ///
        /// Returns true and broadcasts RpcItemAddedToContainer on success.
        /// Returns false if the compartment index is invalid or the grid position is occupied.
        /// </summary>
        [Server]
        public bool ServerTryPutItem(
            Guid                 instanceId,
            InventoryItemSO      itemDef,
            int                  stackCount,
            int                  compartmentIndex,
            Vector2Int           gridPos,
            GridDirection        rotation,
            bool                 hasContainerSnapshot = false,
            NetContainerSnapshot containerSnapshot    = default,
            Guid[]               containerPath        = null)
        {
            if (itemDef == null) return false;

            if (compartmentIndex < 0 || compartmentIndex >= _serverCompartments.Count)
            {
                Log($"[Server] ServerTryPutItem FAIL — invalid compartment {compartmentIndex}.");
                return false;
            }

            InventorySystem inv = _serverCompartments[compartmentIndex];

            // Walk path to reach the nested InventorySystem when item is dropped into a
            // nested container (floating window).  Empty path = root compartment.
            if (containerPath != null && containerPath.Length > 0)
            {
                foreach (Guid pathId in containerPath)
                {
                    PlacedItem ancestor = FindItemById(inv, pathId);
                    if (ancestor?.ContainerInventory == null)
                    {
                        Log($"[Server] ServerTryPutItem FAIL — path walk failed at {ShortGuid(pathId)}.");
                        return false;
                    }
                    inv = ancestor.ContainerInventory;
                }
            }

            bool success = inv.TryAddItem(instanceId, itemDef, gridPos, rotation, out PlacedItem placed, stackCount);

            if (!success)
            {
                Log($"[Server] ServerTryPutItem FAIL — {itemDef.ItemName} id={ShortGuid(instanceId)} " +
                    $"rejected at {gridPos} in compartment {compartmentIndex} (no space or ID conflict).");
                return false;
            }

            // If the item provides storage and we received a container snapshot, reconstruct the
            // nested inventory on the server's authoritative PlacedItem now.  This ensures that
            // the next container snapshot (sent on reopen) includes the nested contents.
            if (hasContainerSnapshot && placed != null && itemDef.ProvidesStorage)
                RestoreContainerInventory(placed, containerSnapshot, itemDef);

            Log($"[Server] ServerTryPutItem — {itemDef.ItemName} id={ShortGuid(instanceId)} " +
                $"placed at {gridPos} rot={rotation} in compartment {compartmentIndex} " +
                $"pathDepth={containerPath?.Length ?? 0}.");

            // Notify all clients that have this container open
            RpcItemAddedToContainer(compartmentIndex, instanceId, itemDef.ItemName, gridPos, rotation, stackCount,
                containerPath ?? Array.Empty<Guid>());
            return true;
        }

        /// <summary>
        /// Reconstruct a live ContainerInventory on a server-side PlacedItem from a
        /// NetContainerSnapshot that was stored in the player's server manifest.
        ///
        /// Called by ServerTryPutItem when a container item (backpack, crate, etc.) is
        /// placed into a world loot container compartment.  Without this, the nested
        /// InventorySystem would remain null and the backpack's contents would be omitted
        /// from the next TgtReceiveContainerSnapshot, making them disappear on reopen.
        /// </summary>
        [Server]
        private void RestoreContainerInventory(
            PlacedItem          placed,
            NetContainerSnapshot snapshot,
            InventoryItemSO     containerDef)
        {
            if (itemRegistry == null)
                itemRegistry = Resources.Load<InventoryItemRegistry>("InventoryItemRegistry");

            ContainerItemData data = InventoryNetConverter.FromNetSnapshot(snapshot, itemRegistry);
            if (data == null || data.Compartments == null || data.Compartments.Count == 0) return;

            InventorySystem containerInv = new InventorySystem(
                containerDef.StorageGridSize.x,
                containerDef.StorageGridSize.y,
                64f,            // cell size — matches the UI cell size used everywhere else
                Vector3.zero,
                containerDef.StorageMaxWeight);

            data.LoadIntoInventorySystem(containerInv, compartmentIndex: 0);
            placed.ContainerInventory = containerInv;

            Log($"[Server] RestoreContainerInventory — nested contents restored to " +
                $"'{containerDef.ItemName}' id={ShortGuid(placed.InstanceID)}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Observers RPCs — keep all viewers in sync
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Broadcast to ALL clients that a player put an item into this container.
        /// Each client adds the item to its local UI copy of the compartment.
        /// The placing client's speculative drag already put the item in its local
        /// InventorySystem, so AddItemToCompartmentGrid detects the duplicate and
        /// just refreshes the visual without re-adding.
        ///
        /// <paramref name="containerPath"/> is the ancestor chain walked on the server.
        /// Empty = item added to a root compartment.
        /// Non-empty = item added to a nested container inside a floating window.
        /// </summary>
        [ObserversRpc]
        private void RpcItemAddedToContainer(
            int           compartmentIndex,
            Guid          instanceId,
            string        itemDefName,
            Vector2Int    gridPos,
            GridDirection rotation,
            int           stackCount,
            Guid[]        containerPath)
        {
            Log($"[Client] RpcItemAddedToContainer  compartment={compartmentIndex}  " +
                $"id={ShortGuid(instanceId)}  item={itemDefName}  pathLen={containerPath?.Length ?? 0}");

            if (ContainerInteractionManager.Instance == null) return;

            // Only update the UI when THIS container is the one the client currently has open.
            // Without this guard a client viewing Container X would incorrectly apply item-add
            // updates that originated from a different Container Y.
            if (ContainerInteractionManager.Instance.CurrentContainerNetId != NetworkObject.ObjectId) return;

            bool isNested = containerPath != null && containerPath.Length > 0;

            if (!isNested)
            {
                // ── Root-level add ────────────────────────────────────────────
                if (ContainerInteractionManager.Instance.CurrentContainer == null) return;

                if (itemRegistry == null)
                    itemRegistry = Resources.Load<InventoryItemRegistry>("InventoryItemRegistry");

                InventoryItemSO itemDef = itemRegistry != null ? itemRegistry.GetItem(itemDefName) : null;
                if (itemDef == null)
                {
                    Debug.LogError($"[NetworkedWorldLootContainer] RpcItemAddedToContainer — unknown item '{itemDefName}'.");
                    return;
                }

                ContainerInteractionManager.Instance.AddItemToCompartmentGrid(
                    compartmentIndex, instanceId, itemDef, gridPos, rotation, stackCount);
            }
            else
            {
                // ── Nested add (item dropped into an open floating window) ────
                // Walk the client-side compartment to find the nested InventorySystem,
                // add the item there, then refresh the floating window so all viewers see it.
                LootContainer currentOpen = ContainerInteractionManager.Instance.CurrentContainer;
                if (currentOpen == null) return;

                List<ContainerCompartment> compartments = currentOpen.GetCompartments();
                if (compartmentIndex >= compartments.Count) return;

                InventorySystem inv = compartments[compartmentIndex].InventorySystem;

                foreach (Guid pathId in containerPath)
                {
                    PlacedItem ancestor = FindItemById(inv, pathId);
                    if (ancestor?.ContainerInventory == null)
                    {
                        Log($"[Client] RpcItemAddedToContainer — path walk failed at {ShortGuid(pathId)}.");
                        return;
                    }
                    inv = ancestor.ContainerInventory;
                }

                if (itemRegistry == null)
                    itemRegistry = Resources.Load<InventoryItemRegistry>("InventoryItemRegistry");

                InventoryItemSO itemDef = itemRegistry != null ? itemRegistry.GetItem(itemDefName) : null;
                if (itemDef == null)
                {
                    Debug.LogError($"[NetworkedWorldLootContainer] RpcItemAddedToContainer — unknown item '{itemDefName}'.");
                    return;
                }

                // Duplicate-safe: TryAddItem returns false (and does nothing) if the item
                // is already present — the placing client's speculative drag already added it.
                inv.TryAddItem(instanceId, itemDef, gridPos, rotation, out _, stackCount);

                // Refresh the floating window displaying this nested container.
                RefreshFloatingWindowForPath(containerPath);
            }
        }

        /// <summary>
        /// Broadcast to ALL clients viewing this container that an item was removed.
        /// Each client removes the item from its local UI copy of the container.
        /// </summary>
        [ObserversRpc]
        private void RpcItemRemovedFromContainer(
            int    compartmentIndex,
            Guid[] containerPath,
            Guid   removedInstanceId)
        {
            Log($"[Client] RpcItemRemovedFromContainer  compartment={compartmentIndex}  " +
                $"pathLen={containerPath?.Length}  id={ShortGuid(removedInstanceId)}");

            // Walk the client-side ContainerInteractionManager's current open container
            // and remove the item from the displayed grid, then refresh.
            if (ContainerInteractionManager.Instance == null) return;

            // Only mutate the UI when THIS container is the one the client currently has open.
            if (ContainerInteractionManager.Instance.CurrentContainerNetId != NetworkObject.ObjectId) return;

            LootContainer currentOpen = ContainerInteractionManager.Instance.CurrentContainer;
            if (currentOpen == null) return;

            List<ContainerCompartment> compartments = currentOpen.GetCompartments();
            if (compartmentIndex >= compartments.Count) return;

            InventorySystem targetInv = compartments[compartmentIndex].InventorySystem;

            // Walk path on client
            if (containerPath != null)
            {
                foreach (Guid pathId in containerPath)
                {
                    PlacedItem ancestor = FindItemById(targetInv, pathId);
                    if (ancestor?.ContainerInventory == null) return;
                    targetInv = ancestor.ContainerInventory;
                }
            }

            targetInv.RemoveItem(removedInstanceId);

            // Refresh only the affected visual.
            // For nested containers (path non-empty) refresh the floating window.
            // For root compartments refresh the compartment grid via CIM.
            // Using OpenContainer would trigger CloseContainer first, clearing
            // _currentContainerNetId and breaking subsequent drag RPC calls.
            if (containerPath != null && containerPath.Length > 0)
                RefreshFloatingWindowForPath(containerPath);
            else
                ContainerInteractionManager.Instance.RefreshCompartmentGrid(compartmentIndex);
        }

        /// <summary>
        /// Broadcast to ALL clients viewing this container that an item's stack count
        /// decreased due to a partial take (stack split).  The item was NOT removed —
        /// only its count changed.  Each client sets its local UI copy to the new count.
        ///
        /// The taking client's speculative drag already reduced the count in BeginDrag,
        /// so this RPC acts as an authoritative confirmation for it and as the first
        /// notification for every other viewer.
        /// </summary>
        [ObserversRpc]
        private void RpcItemCountChangedInContainer(
            int    compartmentIndex,
            Guid[] containerPath,
            Guid   itemInstanceId,
            int    newStackCount)
        {
            Log($"[Client] RpcItemCountChangedInContainer  compartment={compartmentIndex}  " +
                $"id={ShortGuid(itemInstanceId)}  newCount={newStackCount}");

            if (ContainerInteractionManager.Instance == null) return;

            // Only mutate the UI when THIS container is the one the client currently has open.
            if (ContainerInteractionManager.Instance.CurrentContainerNetId != NetworkObject.ObjectId) return;

            LootContainer currentOpen = ContainerInteractionManager.Instance.CurrentContainer;
            if (currentOpen == null) return;

            List<ContainerCompartment> compartments = currentOpen.GetCompartments();
            if (compartmentIndex >= compartments.Count) return;

            InventorySystem targetInv = compartments[compartmentIndex].InventorySystem;

            // Walk the container path (same pattern as RpcItemRemovedFromContainer).
            if (containerPath != null)
            {
                foreach (Guid pathId in containerPath)
                {
                    PlacedItem ancestor = FindItemById(targetInv, pathId);
                    if (ancestor?.ContainerInventory == null) return;
                    targetInv = ancestor.ContainerInventory;
                }
            }

            // Find the item and set its stack count to the server-authoritative value.
            PlacedItem item = FindItemById(targetInv, itemInstanceId);
            if (item != null)
            {
                item.SetStackCount(newStackCount);

                // For nested containers refresh the floating window; for root compartments
                // refresh via the ContainerInteractionManager grid visual.
                if (containerPath != null && containerPath.Length > 0)
                    RefreshFloatingWindowForPath(containerPath);
                else
                    ContainerInteractionManager.Instance.RefreshCompartmentGrid(compartmentIndex);
            }
        }

        /// <summary>
        /// Broadcast to ALL clients viewing this container that an item was repositioned.
        /// Handles both same-compartment repositioning and cross-compartment moves within
        /// the same container.
        ///
        /// <paramref name="containerPath"/> is the ancestor chain walked on the server to
        /// find the InventorySystem that holds the item.  Empty = root compartment.
        /// Non-empty = nested container inside a floating window.
        ///
        /// The placing client's speculative drag has already moved the item in its local
        /// InventorySystem; this RPC acts as server confirmation for it and as the first
        /// (and only) notification for every other viewer.
        /// </summary>
        [ObserversRpc]
        private void RpcItemMovedInContainer(
            int           sourceCompartmentIndex,
            int           targetCompartmentIndex,
            Guid          itemInstanceId,
            Vector2Int    newGridPosition,
            GridDirection newRotation,
            Guid[]        containerPath)
        {
            Log($"[Client] RpcItemMovedInContainer  " +
                $"comp {sourceCompartmentIndex}→{targetCompartmentIndex}  " +
                $"id={ShortGuid(itemInstanceId)}  pos={newGridPosition}  " +
                $"pathDepth={containerPath?.Length ?? 0}");

            bool isNested = containerPath != null && containerPath.Length > 0;

            if (!isNested)
            {
                // ── Root-level move ───────────────────────────────────────────
                if (ContainerInteractionManager.Instance == null) return;
                if (ContainerInteractionManager.Instance.CurrentContainerNetId != NetworkObject.ObjectId) return;

                LootContainer currentOpen = ContainerInteractionManager.Instance.CurrentContainer;
                if (currentOpen == null) return;

                List<ContainerCompartment> compartments = currentOpen.GetCompartments();
                if (sourceCompartmentIndex >= compartments.Count ||
                    targetCompartmentIndex >= compartments.Count) return;

                InventorySystem sourceInv = compartments[sourceCompartmentIndex].InventorySystem;
                InventorySystem targetInv = compartments[targetCompartmentIndex].InventorySystem;

                PlacedItem item        = FindItemById(sourceInv, itemInstanceId);
                InventorySystem holder = sourceInv;

                if (item == null && sourceInv != targetInv)
                {
                    item   = FindItemById(targetInv, itemInstanceId);
                    holder = targetInv;
                }

                if (item == null)
                {
                    Debug.LogWarning($"[NetworkedWorldLootContainer] RpcItemMovedInContainer — " +
                        $"root item {ShortGuid(itemInstanceId)} not found.");
                    return;
                }

                bool alreadyCorrect = holder == targetInv
                                   && item.AnchorPosition == newGridPosition
                                   && item.Rotation       == newRotation;
                if (alreadyCorrect)
                {
                    ContainerInteractionManager.Instance.RefreshCompartmentGrid(targetCompartmentIndex);
                    return;
                }

                InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
                if (itemDef == null) return;

                int stackCount = item.StackCount;
                holder.RemoveItem(itemInstanceId);

                bool success = targetInv.TryAddItem(
                    itemInstanceId, itemDef, newGridPosition, newRotation, out _, stackCount);

                if (!success)
                    Debug.LogWarning($"[NetworkedWorldLootContainer] RpcItemMovedInContainer — " +
                        $"could not place root item {ShortGuid(itemInstanceId)} at {newGridPosition}.");

                ContainerInteractionManager.Instance.RefreshCompartmentGrid(sourceCompartmentIndex);
                if (targetCompartmentIndex != sourceCompartmentIndex)
                    ContainerInteractionManager.Instance.RefreshCompartmentGrid(targetCompartmentIndex);
            }
            else
            {
                // ── Nested move (item is inside a floating window) ────────────
                // Walk the path through the client-side LootContainer to find the nested
                // InventorySystem, apply the move, then refresh the floating window.

                if (ContainerInteractionManager.Instance == null) return;
                if (ContainerInteractionManager.Instance.CurrentContainerNetId != NetworkObject.ObjectId) return;

                LootContainer currentOpen = ContainerInteractionManager.Instance.CurrentContainer;
                if (currentOpen == null) return;

                List<ContainerCompartment> compartments = currentOpen.GetCompartments();
                if (sourceCompartmentIndex >= compartments.Count) return;

                InventorySystem inv = compartments[sourceCompartmentIndex].InventorySystem;

                // Walk path to reach the nested InventorySystem.
                foreach (Guid pathId in containerPath)
                {
                    PlacedItem ancestor = FindItemById(inv, pathId);
                    if (ancestor?.ContainerInventory == null)
                    {
                        Log($"[Client] RpcItemMovedInContainer — path walk failed at {ShortGuid(pathId)}.");
                        return;
                    }
                    inv = ancestor.ContainerInventory;
                }

                // Apply the move to the nested inventory.
                PlacedItem nestedItem = FindItemById(inv, itemInstanceId);
                if (nestedItem == null)
                {
                    Log($"[Client] RpcItemMovedInContainer — nested item {ShortGuid(itemInstanceId)} not found.");
                    return;
                }

                bool alreadyCorrect = nestedItem.AnchorPosition == newGridPosition
                                   && nestedItem.Rotation       == newRotation;
                if (!alreadyCorrect)
                {
                    InventoryItemSO itemDef = nestedItem.ItemDefinition as InventoryItemSO;
                    if (itemDef == null) return;

                    int stackCount = nestedItem.StackCount;
                    inv.RemoveItem(itemInstanceId);

                    bool success = inv.TryAddItem(
                        itemInstanceId, itemDef, newGridPosition, newRotation, out _, stackCount);

                    if (!success)
                        Debug.LogWarning($"[NetworkedWorldLootContainer] RpcItemMovedInContainer — " +
                            $"could not place nested item {ShortGuid(itemInstanceId)} at {newGridPosition}.");
                }

                // Refresh the floating window whose container item ID is the last element of the path.
                RefreshFloatingWindowForPath(containerPath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Refresh the floating window that is displaying the nested container at the end
        /// of <paramref name="containerPath"/>.  The last element of the path is the
        /// InstanceID of the container item whose window should be refreshed.
        ///
        /// Called by RpcItemMovedInContainer after applying nested moves on non-placing
        /// clients so their floating window visuals stay in sync with the server state.
        /// </summary>
        private static void RefreshFloatingWindowForPath(Guid[] containerPath)
        {
            if (containerPath == null || containerPath.Length == 0) return;

            FloatingContainerWindowManager mgr = FloatingContainerWindowManager.Instance;
            if (mgr == null) return;

            // The last ID in the path is the container whose window needs refreshing.
            Guid containerId = containerPath[containerPath.Length - 1];
            FloatingContainerWindow window = mgr.FindWindowByItemId(containerId);
            window?.RefreshGrid();
        }

        private static PlacedItem FindItemById(InventorySystem inv, Guid id)
        {
            if (inv == null) return null;
            foreach (PlacedItem item in inv.GetAllItems())
                if (item.InstanceID == id) return item;
            return null;
        }

        /// <summary>
        /// Server-side: return the InventorySystem for one compartment by index.
        /// Returns null when the index is out of range.
        /// Called by NetworkedInventoryComponent.SvrSyncNestedContainer to locate
        /// the nested container item that the player just closed in the floating window.
        /// </summary>
        [Server]
        public InventorySystem GetServerCompartment(int compartmentIndex)
        {
            if (compartmentIndex < 0 || compartmentIndex >= _serverCompartments.Count)
                return null;
            return _serverCompartments[compartmentIndex];
        }

        private void Log(string msg)
        {
            if (debugMode)
                Debug.Log($"[NetworkedWorldLootContainer] {msg}");
        }

        private static string ShortGuid(Guid g) => g.ToString("N").Substring(0, 8);
    }

    // ─── Result type returned by ServerTryTakeItem ───────────────────────────

    public struct NetTakeResult
    {
        public bool            Success;
        public string          DenyReason;
        public NetPlacedItemData ItemData; // only valid when Success == true

        public static NetTakeResult Deny(string reason) =>
            new NetTakeResult { Success = false, DenyReason = reason };

        public static NetTakeResult Grant(NetPlacedItemData data) =>
            new NetTakeResult { Success = true, ItemData = data };
    }
}
