# Networked Inventory System

This folder contains the networking layer for integrating Ultimate Grid Inventory (UGI) with FishNet Pro.

## Quick Start

### 1. Setup ItemDataRegistry

1. Find your **NetworkManager** GameObject in the scene
2. Add Component → `ItemDataRegistry`
3. In Inspector, populate the **All Items** array:
   - Add all your ItemDataSo assets here
   - Find them in `Assets/Inventory/Resources/Items/` (or wherever you store them)
4. **Enable Verbose Logging** during initial testing

### 2. Setup Player Prefab

1. Open your networked player prefab
2. Add Component → `NetworkedPlayerInventory`
3. Configure settings:
   - **Max Inventory Slots**: 20 (or your desired size)
   - **Verbose Logging**: ✅ Enable for testing

### 3. Create Test Items

If you don't have ItemDataSo assets yet:

1. Right-click in Project → Create → Ultimate Grid Inventory → Item Data
2. Name it something like "TestMedkit"
3. Configure the item properties in Inspector
4. Add it to ItemDataRegistry's array

### 4. Test in Play Mode

1. Start Host (or dedicated Server)
2. Start a Client (use ParrelSync or a build)
3. Select the player GameObject
4. Right-click NetworkedPlayerInventory → "Debug: Add Test Item"
5. Check Console for success messages
6. Verify the item appears in both Server and Client logs

## Architecture Overview

```
┌────────────────────────┐
│  NetworkedPlayerInventory  │  ← YOU ARE HERE (Phase 1)
│  (Server Authority Layer)  │
└────────────┬───────────┘
             │
             │ FishNet SyncList
             │
┌────────────┴───────────┐
│   NetworkedItemData Struct   │
│   ItemDataRegistry          │
│   (Translation Layer)       │
└────────────┬───────────┘
             │
             │ Events (Phase 2)
             │
┌────────────┴───────────┐
│   InventoryUIBridge         │  ← COMING IN PHASE 2
│   (UI Integration Layer)    │
└────────────┬───────────┘
             │
┌────────────┴───────────┐
│   UGI (Ultimate Grid       │
│   Inventory) UI System     │
└────────────────────────┘
```

## Folder Structure

```
Inventory/
├── Networking/
│   ├── NetworkedPlayerInventory.cs    Main component
│   └── NetworkedItemData.cs           Data struct for network
│
├── Utils/
│   └── ItemDataRegistry.cs            SO name → SO reference
│
└── UI/                               (Phase 2)
    └── InventoryUIBridge.cs           (Coming soon)
```

## How It Works

### Adding an Item (Example Flow)

```csharp
// 1. CLIENT: User picks up a medkit in the game world
void OnPickupMedkit()
{
    // Client requests server to add item
    networkInventory.AddItem_ServerRpc(
        "Medkit",  // ItemDataSo name (from registry)
        0,         // Grid index (0 = main inventory)
        2, 3,      // Position in grid
        false,     // Not rotated
        1          // Stack count
    );
}

// 2. SERVER: Validates and adds item
[ServerRpc]
public void AddItem_ServerRpc(string itemName, ...)
{
    // Server checks if item exists
    if (!ItemDataRegistry.HasItem(itemName))
        return; // Reject invalid item
    
    // Server generates unique UID
    string uid = Guid.NewGuid().ToString();
    
    // Server adds to SyncList
    _inventoryItems.Add(new NetworkedItemData(...));
    // ↑ FishNet automatically replicates this to all clients
}

// 3. ALL CLIENTS: Receive update
private void OnInventoryItemsChanged(...)
{
    // SyncList callback fires
    OnItemAdded?.Invoke(newItem);
    // ↑ Phase 2 UI Bridge will subscribe to this
}
```

## Component Reference

### NetworkedPlayerInventory

**Attach to**: Player prefab (networked)  
**Purpose**: Server-authoritative inventory state

**Public Methods**:
- `AddItem_ServerRpc()` - Client requests to add item
- `RemoveItem_ServerRpc()` - Client requests to remove item
- `MoveItem_ServerRpc()` - Client requests to move item
- `RotateItem_ServerRpc()` - Client requests to rotate item
- `GetAllItems()` - Get readonly list of items
- `GetItemByUID(string)` - Get specific item
- `GetItemCount()` - Current item count

**Events** (Client-side):
- `OnItemAdded` - New item added
- `OnItemRemoved` - Item removed
- `OnItemMoved` - Item position changed
- `OnItemRotated` - Item rotation toggled

### ItemDataRegistry

**Attach to**: NetworkManager GameObject  
**Purpose**: Convert item names (string) ↔ ItemDataSo references

**Static Methods**:
- `GetItemByName(string)` - Lookup ItemDataSo
- `HasItem(string)` - Check if item exists
- `GetRegisteredItemCount()` - Total registered items

### NetworkedItemData

**Type**: Struct (serializable)  
**Purpose**: Network-friendly item data

**Fields**:
- `itemUID` - Unique instance ID (GUID)
- `itemDataSOName` - Name of ItemDataSo
- `gridIndex` - Which grid (0 = player)
- `posX`, `posY` - Grid position
- `isRotated` - Rotation state
- `stackCount` - Stack quantity

## Debug Commands

### NetworkedPlayerInventory Context Menu

Right-click component in Inspector:

- **Debug: Print Inventory** - Dumps current items to Console
- **Debug: Add Test Item** (Server only) - Adds a test item

### ItemDataRegistry Context Menu

- **Debug: List All Registered Items** - Shows all items in registry

### Console Logging

Enable `verboseLogging` in both components for detailed output:

```
[ItemDataRegistry] Registry built: 5 valid items
[NetworkedPlayerInventory] Server started for Player 1
[NetworkedPlayerInventory] [Server] Added item for Player 1: [NetworkedItemData] Medkit (UID: abc-123) at Grid0[2,3]
[NetworkedPlayerInventory] [Client] Item added: [NetworkedItemData] Medkit...
```

## Common Issues

### "Item not found in registry"
**Fix**: Add the ItemDataSo to ItemDataRegistry's `allItems` array.

### "Registry not initialized"
**Fix**: Attach ItemDataRegistry to NetworkManager GameObject.

### Items not replicating
**Fix**: Verify NetworkedPlayerInventory is on the networked player prefab, not added at runtime.

### "AddItem called by non-owner"
**This is correct behavior!** Only the item's owner can modify their inventory.

## Testing

See comprehensive test plan: `Docs/Testing/InventoryPhase1Tests.md`

Quick test:
1. Start Host + Client
2. On Host, select player GameObject
3. Right-click NetworkedPlayerInventory → "Debug: Add Test Item"
4. Check both Host and Client consoles
5. Verify item appears in both logs

## Current Limitations

Phase 1 focuses on **network foundation only**:

❌ No UI integration (Phase 2)  
❌ No grid collision detection (Phase 1.5)  
❌ No grid bounds validation (Phase 1.5)  
❌ No world item pickups (Phase 3)  
❌ No item stacking logic (Phase 4)  
❌ No timeline persistence (Phase 5)  

## Next Phase Preview

**Phase 2: UI Bridge**
- `InventoryUIBridge` component
- Connects NetworkedPlayerInventory → UGI's visual UI
- Handles drag-and-drop
- Updates UI when items change over network

## Documentation

Full documentation:
- **Integration Guide**: `Docs/Integration/InventoryIntegration.md`
- **Test Plan**: `Docs/Testing/InventoryPhase1Tests.md`
- **Code Reference**: `TemporalStability.cs`, `PlayerZoneTriggerHandler.cs`

## Questions?

1. Read the integration guide
2. Check test plan
3. Enable verbose logging
4. Check FishNet docs for networking questions

---

**Status**: Phase 1 Complete ✅  
**Next**: Phase 2 - UI Bridge  
**Updated**: January 16, 2026
