# UGI + FishNet Inventory Integration

## Overview
This document tracks the integration of Ultimate Grid Inventory (UGI) with FishNet Pro networking for the New Thelos cooperative survival horror game.

**Last Updated**: January 16, 2026  
**Current Phase**: Phase 1 - Network Foundation (COMPLETE ✅)  
**Location**: `Assets/_Scripts/Systems/Inventory/`

---

## Integration Architecture

### The Challenge
**UGI's Architecture**:
- Uses ScriptableObjects (ItemDataSo) for item definitions
- StaticInventoryContext as a singleton manager
- ItemTable for runtime item instances
- GridTable for grid state management

**FishNet's Requirements**:
- Server-authoritative networking
- Can only serialize primitive types and structs
- ScriptableObject references cannot be sent over network
- Each player needs their own inventory instance

### The Solution
**Three-Layer Architecture**:
1. **Network Layer** (Phase 1) - `NetworkedPlayerInventory`
   - Server-authoritative SyncList of items
   - ServerRPCs for Add/Remove/Move/Rotate
   - Automatic replication to all clients

2. **Translation Layer** - `NetworkedItemData` struct + `ItemDataRegistry`
   - String identifiers for ScriptableObject references
   - Registry converts strings ↔ ItemDataSo
   - Efficient network transmission

3. **UI Bridge Layer** (Phase 2) - `InventoryUIBridge`
   - Connects Network Layer to UGI's UI system
   - Subscribes to NetworkedPlayerInventory events
   - Updates UGI's ItemTable/GridTable when items change

---

## Phase 1: Network Foundation ✅ COMPLETE

### Components Implemented

#### 1. NetworkedItemData.cs
**Location**: `Assets/_Scripts/Systems/Inventory/Networking/NetworkedItemData.cs`  
**Namespace**: `NewThelos.Systems.Inventory.Networking`

**Purpose**: Serializable struct for transmitting item data over FishNet.

**Key Fields**:
- `itemUID` - Unique instance identifier (GUID)
- `itemDataSOName` - String name of ItemDataSo (resolved by registry)
- `gridIndex` - Which grid (0 = player inventory)
- `posX`, `posY` - Grid position
- `isRotated` - Rotation state
- `stackCount` - Stack quantity

**Why It Exists**: FishNet can only serialize structs with primitive fields. This converts ItemTable's complex state into network-friendly format.

---

#### 2. ItemDataRegistry.cs
**Location**: `Assets/_Scripts/Systems/Inventory/Utils/ItemDataRegistry.cs`  
**Namespace**: `NewThelos.Systems.Inventory.Utils`

**Purpose**: Singleton that maps item names (strings) to ItemDataSo references.

**How It Works**:
```
Server: ItemDataSo → itemDataSo.name (string) → Network
Client: string → ItemDataRegistry.GetItemByName() → ItemDataSo
```

**Setup Requirements**:
1. Attach to NetworkManager GameObject
2. Populate `allItems` array with all ItemDataSo assets in Inspector
3. Initialize before any NetworkedPlayerInventory components

**API**:
- `GetItemByName(string)` - Returns ItemDataSo or null
- `HasItem(string)` - Check if item exists
- `GetRegisteredItemCount()` - Total registered items

---

#### 3. NetworkedPlayerInventory.cs
**Location**: `Assets/_Scripts/Systems/Inventory/Networking/NetworkedPlayerInventory.cs`  
**Namespace**: `NewThelos.Systems.Inventory.Networking`

**Purpose**: Server-authoritative networked player inventory component.

**Key Features**:
- `SyncList<NetworkedItemData>` - Automatically replicates to all observers
- ServerRPCs for all inventory operations
- Events for UI integration (Phase 2)
- Late-join support (new clients automatically receive current state)

**ServerRPCs**:
- `AddItem_ServerRpc()` - Add item to inventory
- `RemoveItem_ServerRpc()` - Remove item by UID
- `MoveItem_ServerRpc()` - Update item position
- `RotateItem_ServerRpc()` - Toggle item rotation

**Events** (Client-side):
- `OnItemAdded` - New item added
- `OnItemRemoved` - Item removed (provides UID)
- `OnItemMoved` - Item position changed
- `OnItemRotated` - Item rotation toggled

**Public API**:
- `GetAllItems()` - Returns readonly list of inventory items
- `GetItemByUID(string)` - Get specific item
- `GetItemCount()` - Current item count

---

### File Structure

```
Assets/
├── _Scripts/
│   ├── Systems/
│   │   └── Inventory/                          ✅ NEW LOCATION
│   │       ├── Networking/
│   │       │   ├── NetworkedPlayerInventory.cs ✅ Created
│   │       │   └── NetworkedItemData.cs        ✅ Created
│   │       ├── Utils/
│   │       │   └── ItemDataRegistry.cs         ✅ Created
│   │       └── README.md                       ✅ Quick reference
│   ├── Player/
│   │   ├── Temporal/
│   │   │   ├── TemporalStability.cs            (Reference pattern)
│   │   │   └── TimelineManager.cs
│   │   └── Control/
│   │       └── PlayerZoneTriggerHandler.cs     (Reference pattern)
│   └── Core/
│       ├── Extensions/
│       ├── Interfaces/
│       └── ScriptableObjects/
├── Inventory/ (UGI Asset - READ ONLY)
│   └── Scripts/
│       ├── Core/
│       │   ├── Items/ItemTable.cs
│       │   └── Grids/GridTable.cs
│       └── ...
└── Docs/
    ├── Testing/
    │   └── InventoryPhase1Tests.md             ✅ Created
    └── Integration/
        └── InventoryIntegration.md             ✅ This file
```

---

## Phase 1 Completion Checklist

### Code Implementation
- ✅ NetworkedItemData struct created
- ✅ ItemDataRegistry component created
- ✅ NetworkedPlayerInventory component created
- ✅ All ServerRPCs implemented
- ✅ SyncList callbacks implemented
- ✅ Events defined for Phase 2
- ✅ XML documentation on all public methods
- ✅ Debug logging with toggle
- ✅ ContextMenu debug commands
- ✅ Reorganized into Systems/Inventory folder
- ✅ Updated namespaces to NewThelos.Systems.Inventory

### Setup Requirements
- ⏳ ItemDataRegistry attached to NetworkManager
- ⏳ ItemDataRegistry.allItems populated with ItemDataSo assets
- ⏳ NetworkedPlayerInventory attached to player prefab
- ⏳ Test ItemDataSo assets created

### Testing
- ⏳ Component initialization test
- ⏳ Add item test
- ⏳ Remove item test
- ⏳ Move item test
- ⏳ Rotate item test
- ⏳ SyncList replication test
- ⏳ Late-join client test
- ⏳ Multi-player isolation test

**Legend**: ✅ Complete | ⏳ In Progress | ❌ Not Started

---

## Known Limitations (Phase 1)

These features are **intentionally not implemented** in Phase 1:

1. **No UI Integration**
   - NetworkedPlayerInventory maintains network state only
   - UGI's visual UI is not yet connected
   - Phase 2 will add InventoryUIBridge

2. **No Collision Detection**
   - Items can overlap in the grid
   - No validation of item placement
   - Phase 1.5 will add spatial validation

3. **No Grid Size Validation**
   - Items can be placed outside grid bounds
   - maxInventorySlots is defined but not enforced
   - Phase 1.5 will add bounds checking

4. **No Pickup System**
   - Cannot interact with world items yet
   - Phase 3 will add pickup mechanics

5. **No Item Stacking Logic**
   - stackCount field exists but not utilized
   - Cannot merge stackable items
   - Phase 4 will add stacking logic

6. **No Timeline Persistence**
   - Inventory resets on timeline transitions
   - Phase 5 will add persistence across scenes

---

## Integration Notes

### Why Not Modify UGI Directly?

**Decision**: Keep UGI's code unmodified and wrap it with networking layer.

**Reasoning**:
- Preserves UGI's update path
- Easier to debug (clear separation of concerns)
- Can swap inventory systems if needed
- UGI remains usable in single-player contexts

### Server Authority Pattern

All inventory operations follow this flow:
```
1. Client initiates action (e.g., "Add Medkit")
2. Client calls ServerRPC
3. Server validates request
4. Server modifies SyncList (if valid)
5. FishNet automatically replicates to all clients
6. Clients receive OnChange callback
7. Clients fire events for UI update
```

**Why Server Authority?**
- Prevents cheating (clients can't fake items)
- Single source of truth
- Late-joiners automatically sync
- Handles network edge cases consistently

### Pattern References

NetworkedPlayerInventory follows patterns from existing components:

**From TemporalStability.cs** (`Assets/_Scripts/Player/Temporal/`):
- SyncVar usage with WritePermission.ServerOnly
- OnChange callback pattern
- Server-side validation in [Server] methods

**From PlayerZoneTriggerHandler.cs** (`Assets/_Scripts/Player/Control/`):
- ServerRPC validation (sender == Owner check)
- NetworkConnection sender parameter
- Debug logging with client/server context

---

## Next Steps: Phase 2 - UI Bridge

### Planned Components

#### InventoryUIBridge.cs
**Purpose**: Connect NetworkedPlayerInventory to UGI's UI system.  
**Location**: `Assets/_Scripts/Systems/Inventory/UI/InventoryUIBridge.cs`

**Responsibilities**:
1. Subscribe to NetworkedPlayerInventory events
2. Convert NetworkedItemData → UGI's ItemTable
3. Update UGI's GridTable when items change
4. Handle drag-and-drop UI interactions
5. Send user actions to NetworkedPlayerInventory via ServerRPCs

**Key Challenges**:
- UGI's StaticInventoryContext is static (needs per-player instance)
- ItemTable creation from NetworkedItemData
- Synchronizing UGI's UI state with network state

### Phase 2 Tasks
1. Create InventoryUIBridge component
2. Implement event handlers for OnItemAdded/Removed/Moved/Rotated
3. Create ItemTable from NetworkedItemData
4. Hook into UGI's drag-and-drop system
5. Test UI updates in multiplayer

---

## Phase 3-5 Preview

### Phase 3: World Item Pickups
- NetworkedWorldItem component
- Interaction system (E key to pickup)
- Server-side pickup validation
- Item spawning and despawning

### Phase 4: Item Stacking
- Stack merging logic
- Split stack functionality
- Stack count UI updates

### Phase 5: Timeline Persistence
- Inventory survives timeline transitions
- NetworkedPlayerInventory persists across scene changes
- Test with FishNet's scene stacking

---

## Troubleshooting

### Common Issues

#### "Item not found in registry"
**Cause**: ItemDataRegistry.allItems array missing the ItemDataSo.
**Solution**: Add the ItemDataSo to ItemDataRegistry's array in Inspector.

#### "Registry not initialized"
**Cause**: ItemDataRegistry component not attached to NetworkManager.
**Solution**: Attach ItemDataRegistry to NetworkManager GameObject.

#### "AddItem_ServerRpc called by non-owner"
**Cause**: Client trying to modify another player's inventory.
**Solution**: This is working as intended - inventory operations are owner-only.

#### Items not replicating to clients
**Cause**: SyncList permissions incorrect.
**Solution**: Verify SyncTypeSettings has WritePermission.ServerOnly, ReadPermission.Observers.

#### Late-joiners not receiving inventory
**Cause**: NetworkedPlayerInventory not persisting across spawns.
**Solution**: Ensure component is on the networked player prefab, not dynamically added.

---

## References

### FishNet Documentation
- [SyncTypes Overview](https://fish-networking.gitbook.io/docs/manual/guides/synctypes)
- [SyncList Documentation](https://fish-networking.gitbook.io/docs/manual/guides/synctypes/synclist)
- [ServerRpc Documentation](https://fish-networking.gitbook.io/docs/manual/guides/rpcs/server-rpc)
- [NetworkBehaviour Lifecycle](https://fish-networking.gitbook.io/docs/manual/guides/networkbehaviour)

### UGI Documentation
- Check Assets/Inventory/Documentation/ for UGI-specific docs

### Project Patterns
- See `Assets/_Scripts/Player/Temporal/TemporalStability.cs` for SyncVar patterns
- See `Assets/_Scripts/Player/Control/PlayerZoneTriggerHandler.cs` for ServerRPC patterns
- See `Assets/_Scripts/Player/Temporal/TimelineManager.cs` for scene persistence patterns

---

## Change Log

### 2026-01-16 - Phase 1 Implementation & Reorganization
- Created NetworkedItemData struct
- Created ItemDataRegistry component
- Created NetworkedPlayerInventory component
- Implemented all Phase 1 ServerRPCs
- Added comprehensive documentation
- Created test plan (InventoryPhase1Tests.md)
- **Reorganized**: Moved from `Assets/_Scripts/Inventory/` to `Assets/_Scripts/Systems/Inventory/`
- **Updated namespaces**: From `NewThelos.Inventory` to `NewThelos.Systems.Inventory`
- Aligned with project-wide script reorganization

---

## Contact & Support

For questions or issues:
1. Check this documentation
2. Review test plan (Docs/Testing/InventoryPhase1Tests.md)
3. Check quick reference (Assets/_Scripts/Systems/Inventory/README.md)
4. Enable verbose logging for debugging
5. Check FishNet documentation for networking questions
