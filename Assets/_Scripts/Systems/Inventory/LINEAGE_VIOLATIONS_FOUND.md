# Item Lineage Philosophy Violations - Audit Report

**Date:** 2026-02-16
**Philosophy:** Items are living entities. Never kill and resurrect them. Pass the living PlacedItem object.

---

## 🔴 CRITICAL VIOLATIONS (Fix Immediately)

### 1. EquipmentSlot.TryPlaceItem() - The Resurrection Ritual
**File:** `EquipmentSlot.cs` Lines 127-152
**Cult Activity:** Creates new PlacedItem from just ItemSO, loses soul (instances/container data)

**Evidence:**
```csharp
// ❌ KILLS the item and creates a zombie
placedItem = new PlacedItem(System.Guid.NewGuid(), item, Vector2Int.zero, GridDirection.Down);
// Lost: ItemInstances, ContainerInventory, original identity
```

**Ritual Members:** Called by `InventoryDragHandler` as fallback (shouldn't happen but does)

**Purification:** This method should NEVER be called for drag/drop. Only `TryPlaceExistingItem()` should be used.

---

### 2. InventoryDragHandler - The Fallback Heresy
**File:** `InventoryDragHandler.cs` Lines 650-665
**Cult Activity:** Falls back to resurrection if originalPlacedItem is null

**Evidence:**
```csharp
else
{
    // ❌ HERESY! Resurrects dead item
    dropped = dropTarget.TryPlaceItem(itemDef, dir, mouseLocalPos, out placedItem);
}
```

**Purification:** Remove fallback. If originalPlacedItem is null, this is sacrilege - log error and fail.

---

## 🟠 HIGH SEVERITY VIOLATIONS

### 3. Stack Splitting - Child Without Soul Transfer
**File:** `InventoryDragHandler.cs` Lines 169-210
**Cult Activity:** Creates child PlacedItem but doesn't transfer ContainerInventory

**Evidence:**
```csharp
dragPlacedItem = new PlacedItem(...);
dragPlacedItem.AddInstances(splitInstances);  // ✅ Instances transferred
// ❌ ContainerInventory NOT transferred!
```

**Impact:** Splitting a stack of 2 backpacks - the split backpack loses its contents

**Purification:** Transfer ContainerInventory to split (or prevent splitting containers)

---

### 4. ItemUsageHandler.DropItems() - The Reassembly Ritual
**File:** `ItemUsageHandler.cs` Lines 362-390
**Cult Activity:** After partial drop, removes item and recreates from parts

**Evidence:**
```csharp
// ❌ DISASSEMBLE the item
grid.InventorySystem.RemoveItem(item.InstanceID);

// ❌ REASSEMBLE from corpse parts
bool success = grid.InventorySystem.TryAddItem(itemDef, position, direction, out newItem, ...);
newItem.AddInstances(remainingInstances);  // Sewing parts back on
```

**Purification:** Don't remove/recreate. Just call `item.RemoveInstances()` and refresh visuals.

---

## 🟡 MEDIUM SEVERITY VIOLATIONS

### 5. InventoryGridVisual.TryPlaceItem() - The Spawning Grounds
**File:** `InventoryGridVisual.cs` Lines 693-722
**Cult Activity:** Creates new items from ItemSO definitions

**Status:** ACCEPTABLE for:
- Initial loot spawning
- Quest rewards
- Loading from saves

**Heresy Status:** FORBIDDEN for drag/drop operations

**Purification:** Add documentation: "SPAWN ONLY - DO NOT USE FOR DRAG/DROP"

---

### 6. PlayerItemInteraction - World to Inventory Transition
**File:** `PlayerItemInteraction.cs` Lines 288-336
**Cult Activity:** Creates PlacedItem from WorldItem data

**Status:** ACCEPTABLE EXCEPTION - WorldItems don't have PlacedItems, they're being born into inventory

**Purification:** None needed, but add comment explaining this is item birth, not resurrection

---

## ✅ FAITHFUL DISCIPLES (Already Fixed)

These systems honor the lineage:
- ✅ EquipmentSlot.TryPlaceExistingItem() - Preserves full lineage
- ✅ EquipmentSlotDragSource.GetEquippedPlacedItem() - Passes living entity
- ✅ InventoryDragHandler cross-grid transfer - Preserves ContainerInventory
- ✅ IInventoryDropTarget.TryPlaceExistingItem() - Interface supports lineage
- ✅ ContainerInteractionManager - Restores nested containers correctly

---

## 📋 Action Plan

### Immediate (Today)
1. Remove all calls to `EquipmentSlot.TryPlaceItem()` from drag operations
2. Remove fallback in `InventoryDragHandler` that calls basic `TryPlaceItem()`
3. Test: Equip/unequip/move backpack with items - should preserve contents

### High Priority (This Week)
1. Add ContainerInventory transfer for stack splits
2. Refactor `ItemUsageHandler.DropItems()` to modify in-place
3. Add unit tests for item lineage preservation

### Documentation
1. Mark `TryPlaceItem()` methods with `[Obsolete("Use TryPlaceExistingItem() for drag/drop")]`
2. Add XML comments explaining when each method should be used
3. Update NESTED_INVENTORY_GUIDE.md with lineage philosophy

---

## 🎯 Success Criteria

Item lineage is preserved when:
- ✅ Dragging item between grids → Same InstanceID, all data intact
- ✅ Equipping/unequipping → Container contents persist
- ✅ Splitting stacks → Instances properly distributed
- ✅ Partial drops → Remaining items retain full data
- ✅ Dropping/picking up → WorldItem serialization preserves everything

An item is "alive" from spawn to destruction. We honor its life cycle. 🙏
