# Inventory Integration Phase 1 - Test Plan

## Overview
This document outlines the testing strategy for Phase 1 of the UGI + FishNet inventory integration. Phase 1 focuses on the networked inventory foundation layer without UI integration.

## Test Environment Setup

### Prerequisites
- Unity 6.0 LTS
- FishNet v4 installed and configured
- Ultimate Grid Inventory (UGI) installed
- ItemDataRegistry component attached to NetworkManager
- At least one ItemDataSo asset created and registered
- NetworkedPlayerInventory component attached to player prefab

### Network Setup
- Test with both Host (Server + Client) and dedicated Server + Client
- Minimum 2 clients for multiplayer validation
- Test late-join scenarios (client joining after server started)

---

## Phase 1 Test Cases

### Test 1: Component Initialization

**Objective**: Verify that all components initialize correctly in networked environment.

**Steps**:
1. Start Unity Editor in Host mode
2. Start a second client (build or ParrelSync)
3. Verify both clients connect successfully
4. Check Console for initialization logs

**Expected Results**:
- ✅ ItemDataRegistry logs: "Registry built: X valid items"
- ✅ NetworkedPlayerInventory logs on server: "Server started for Player [ClientId]"
- ✅ NetworkedPlayerInventory logs on clients: "Client started. IsOwner: true/false"
- ✅ No error messages in Console
- ✅ Player prefabs spawn with NetworkedPlayerInventory component

**Pass/Fail Criteria**:
- All components initialize without errors
- Both server and clients log successful initialization
- Each player has their own NetworkedPlayerInventory instance

---

### Test 2: Server RPC - Add Item

**Objective**: Verify that AddItem_ServerRpc correctly adds items to server-authoritative inventory.

**Steps**:
1. Start Host + 1 Client
2. On Client, call AddItem_ServerRpc via console command or debug button:
   ```csharp
   networkInventory.AddItem_ServerRpc("MedKit", 0, 0, 0, false, 1);
   ```
3. Check Console logs on both Server and Client
4. Inspect NetworkedPlayerInventory's _inventoryItems list in Inspector (server side)

**Expected Results**:
- ✅ Server logs: "[Server] Added item for Player X: [NetworkedItemData] MedKit..."
- ✅ Client logs: "[Client] Item added: [NetworkedItemData] MedKit..."
- ✅ SyncList count increases on both server and client
- ✅ Item UID is unique (GUID format)
- ✅ Item appears at correct grid position (0,0)

**Pass/Fail Criteria**:
- Item successfully added to server's SyncList
- Change automatically replicates to all clients
- OnItemAdded event fires on clients
- Item data matches requested parameters

**Edge Cases to Test**:
- Invalid item name (not in registry) → Should log warning and reject
- Negative position → Should log warning and reject
- Stack count < 1 → Should log warning and reject
- Non-owner client trying to add to another player's inventory → Should reject

---

### Test 3: SyncList Replication Across Clients

**Objective**: Verify that inventory changes replicate correctly to all observing clients.

**Setup**:
- Host (Server + Client A)
- Client B
- Client C (late-joiner)

**Steps**:
1. Host and Client B connect
2. Client A adds 3 items to their inventory
3. Verify Client B sees the updates (check Console logs)
4. Client C joins late
5. Verify Client C receives current inventory state

**Expected Results**:
- ✅ Client B receives OnItemAdded events for all 3 items
- ✅ Client B's inventory count matches server (3 items)
- ✅ Client C automatically receives current state (3 items) upon connection
- ✅ No duplicate items or missing items
- ✅ Item order preserved across all clients

**Pass/Fail Criteria**:
- All clients see identical inventory state
- Late-joiners receive full current state
- No desynchronization between server and clients

---

### Test 4: Item Removal

**Objective**: Verify RemoveItem_ServerRpc correctly removes items and replicates.

**Steps**:
1. Start Host + 1 Client
2. Add 2 items to inventory
3. Call RemoveItem_ServerRpc with first item's UID:
   ```csharp
   networkInventory.RemoveItem_ServerRpc("<item-uid>");
   ```
4. Verify removal on both server and client

**Expected Results**:
- ✅ Server logs: "[Server] Removed item for Player X: <item-uid>"
- ✅ Client logs: "[Client] Item removed: <item-uid>"
- ✅ Item count decreases by 1
- ✅ Correct item removed (verify by UID)
- ✅ Other items remain unaffected

**Pass/Fail Criteria**:
- Item successfully removed from SyncList
- Removal replicates to all clients
- OnItemRemoved event fires with correct UID

---

### Test 5: Item Movement

**Objective**: Verify MoveItem_ServerRpc correctly updates item position.

**Steps**:
1. Start Host + 1 Client
2. Add item at position (0, 0)
3. Call MoveItem_ServerRpc to move to (2, 3):
   ```csharp
   networkInventory.MoveItem_ServerRpc("<item-uid>", 2, 3);
   ```
4. Verify position update on both server and client

**Expected Results**:
- ✅ Server logs: "[Server] Moved item for Player X: <uid> to [2, 3]"
- ✅ Client logs: "[Client] Item moved: <uid> to [2, 3]"
- ✅ Item's posX and posY updated correctly
- ✅ Other item properties (rotation, stack count) unchanged

**Pass/Fail Criteria**:
- Position update replicates correctly
- OnItemMoved event fires with new coordinates
- Item UID remains unchanged

---

### Test 6: Item Rotation

**Objective**: Verify RotateItem_ServerRpc correctly toggles item rotation.

**Steps**:
1. Start Host + 1 Client
2. Add item with isRotated = false
3. Call RotateItem_ServerRpc:
   ```csharp
   networkInventory.RotateItem_ServerRpc("<item-uid>");
   ```
4. Verify rotation toggled to true
5. Call again, verify rotation toggles back to false

**Expected Results**:
- ✅ First call: isRotated changes from false → true
- ✅ Second call: isRotated changes from true → false
- ✅ Server and client logs match rotation state
- ✅ OnItemRotated event fires each time

**Pass/Fail Criteria**:
- Rotation toggles correctly
- State replicates to all clients
- Multiple rotations work consecutively

---

### Test 7: Multi-Player Isolation

**Objective**: Verify each player's inventory is isolated from others.

**Steps**:
1. Start Host + 2 Clients (Player A, Player B, Player C)
2. Player A adds 2 items to their inventory
3. Player B adds 3 items to their inventory
4. Player C observes both

**Expected Results**:
- ✅ Player A's inventory count = 2
- ✅ Player B's inventory count = 3
- ✅ Player C sees both inventories correctly
- ✅ Changes to Player A's inventory don't affect Player B
- ✅ Each player can only modify their own inventory

**Pass/Fail Criteria**:
- Inventories are completely isolated per-player
- Non-owners cannot add/remove items from other players
- Observer clients see all player inventories correctly

---

### Test 8: ItemDataRegistry Lookup

**Objective**: Verify ItemDataRegistry correctly resolves string names to ItemDataSo.

**Steps**:
1. Create 3 test ItemDataSo assets: "Medkit", "Ammo", "Flashlight"
2. Populate ItemDataRegistry's allItems array
3. Start PlayMode
4. Call ItemDataRegistry.GetItemByName("Medkit")
5. Verify returned ItemDataSo is not null
6. Test with invalid name: ItemDataRegistry.GetItemByName("InvalidItem")

**Expected Results**:
- ✅ Valid names return correct ItemDataSo references
- ✅ Invalid names return null and log warning
- ✅ Registry logs: "Registry built: 3 valid items"
- ✅ ItemDataRegistry.GetRegisteredItemCount() returns 3

**Pass/Fail Criteria**:
- All registered items are accessible by name
- Invalid lookups handled gracefully
- No duplicate items in registry

---

## Known Limitations (Phase 1)

The following features are **intentionally not implemented** in Phase 1:
- ✋ UI integration (Phase 2)
- ✋ Collision detection (items overlapping in grid)
- ✋ Grid size validation (items exceeding grid bounds)
- ✋ Pickup system (interacting with world items)
- ✋ Item stacking logic (combining stackable items)
- ✋ Persistence across timeline transitions (Phase 5)

## Debugging Tips

### Enable Verbose Logging
Set `verboseLogging = true` in:
- ItemDataRegistry
- NetworkedPlayerInventory

This provides detailed Console output for all operations.

### Inspector Debugging
In PlayMode, select a player's NetworkObject and expand:
- NetworkedPlayerInventory → _inventoryItems (SyncList)

You can see the live inventory state on both server and clients.

### Console Commands
Use these ContextMenu commands on NetworkedPlayerInventory:
- "Debug: Print Inventory" - Dumps current inventory to Console
- "Debug: Add Test Item" (Server only) - Adds a test item

---

## Test Results Log Template

```
=== Phase 1 Test Results ===
Date: [YYYY-MM-DD]
Tester: [Name]
Unity Version: [6.0.XX]
FishNet Version: [4.X.X]

[ ] Test 1: Component Initialization - PASS/FAIL
Notes: 

[ ] Test 2: Add Item RPC - PASS/FAIL
Notes:

[ ] Test 3: SyncList Replication - PASS/FAIL
Notes:

[ ] Test 4: Item Removal - PASS/FAIL
Notes:

[ ] Test 5: Item Movement - PASS/FAIL
Notes:

[ ] Test 6: Item Rotation - PASS/FAIL
Notes:

[ ] Test 7: Multi-Player Isolation - PASS/FAIL
Notes:

[ ] Test 8: ItemDataRegistry Lookup - PASS/FAIL
Notes:

=== Overall Assessment ===
Phase 1 Status: READY FOR PHASE 2 / NEEDS FIXES
Critical Issues:
Minor Issues:
```

---

## Next Steps After Phase 1 Passes

Once all tests pass:
1. ✅ Create Phase 2 branch: `feature/inventory-ui-bridge`
2. ✅ Implement InventoryUIBridge component
3. ✅ Connect NetworkedPlayerInventory events to UGI's UI system
4. ✅ Test UI updates when inventory changes
5. ✅ Implement pickup interaction system
