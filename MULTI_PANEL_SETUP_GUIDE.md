# Multi-Panel Inventory System - Setup Guide

## 🎯 What We Built

A flexible multi-panel inventory system supporting:
- **Grid-based storage** (left and right panels)
- **Equipment slots** (center panel)
- **Cross-panel drag-drop** (grids ↔ slots ↔ grids)

## 📋 Component Overview

### Core Components
1. **IInventoryDropTarget** - Interface for anything that accepts dropped items
2. **InventoryGridVisual** - Grid-based storage (implements IInventoryDropTarget)
3. **EquipmentSlot** - Single-item slot with type restrictions (implements IInventoryDropTarget)
4. **InventoryDragHandler** - Handles drag-drop between ANY drop targets
5. **InventoryPanelManager** - Auto-registers all drop targets

## 🏗️ Unity Setup

### Step 1: Create UI Structure

```
Canvas (ScreenSpaceOverlay)
└── InventoryPanelManager (with InventoryDragHandler + InventoryPanelManager components)
    ├── LeftPanel (loot container - enable/disable dynamically)
    │   ├── ContainerGrid1 (InventoryGridVisual)
    │   └── ContainerGrid2 (InventoryGridVisual)
    │
    ├── CenterPanel (equipment slots)
    │   ├── HelmetSlot (EquipmentSlot)
    │   ├── VestSlot (EquipmentSlot)
    │   ├── BackpackSlot (EquipmentSlot)
    │   ├── PrimaryWeaponSlot (EquipmentSlot)
    │   └── SidearmSlot (EquipmentSlot)
    │
    └── RightPanel (player inventory)
        ├── VestStorageGrid (InventoryGridVisual)
        ├── BackpackStorageGrid (InventoryGridVisual)
        └── PocketGrid (InventoryGridVisual)
```

### Step 2: Configure InventoryDragHandler

1. Create empty GameObject named "DragDropSystem"
2. Add `InventoryDragHandler` component
3. Add `InventoryPanelManager` component
4. Create `InventoryItemGhost` GameObject and assign to drag handler
5. Set "Auto Register On Start" to true in panel manager

### Step 3: Create Equipment Slots

For **each equipment slot** (Helmet, Vest, etc.):

1. Create UI Image GameObject
2. Add `EquipmentSlot` component
3. Configure:
   - **Slot Name**: "Helmet Slot"
   - **Accepted Types**: [Helmet, Hat, Mask] ← whatever types this slot accepts
   - **Item Icon Image**: Reference to child Image component
   - **Empty Slot Indicator**: Optional placeholder image

### Step 4: Configure Inventory Grids

For **each inventory grid**:

1. Create empty GameObject
2. Add `InventoryGridVisual` component
3. Create matching `InventorySystem` (in code or via manager)
4. Call `gridVisual.Initialize(inventorySystem)`

### Step 5: Set ItemType on Items

For each `InventoryItemSO`:
- Set **EquipmentType** field:
  - `None` = storage only, can't be equipped
  - `Helmet` = can only go in helmet slots
  - `PrimaryWeapon` = can only go in weapon slots
  - etc.

## 🎮 How It Works

### Drag-Drop Flow

```
1. User clicks item visual
   ↓
2. InventoryItemDragDrop.OnBeginDrag() fires
   ↓
3. Calls InventoryDragHandler.OnItemBeginDrag(itemID)
   ↓
4. DragHandler finds source target (grid or slot)
   ↓
5. Ghost appears, follows mouse
   ↓
6. Update() checks mouse position against ALL registered targets
   ↓
7. Ghost turns green/red based on target.CanAcceptItem()
   ↓
8. User releases mouse
   ↓
9. InventoryItemDragDrop.OnEndDrag() fires
   ↓
10. DragHandler finds target under mouse
    ↓
11. Removes item from source
    ↓
12. Calls target.TryPlaceItem()
    ↓
13. If failed, returns to source
```

### Type Validation

**Equipment Slot Example:**
```csharp
// HelmetSlot accepts: [Helmet, Hat, Mask]
// User drags "Modern Axe" (ItemType.Melee)
slot.CanAcceptItem(modernAxe) → FALSE (red ghost)

// User drags "Tactical Helmet" (ItemType.Helmet)
slot.CanAcceptItem(tacticalHelmet) → TRUE (green ghost)
```

**Grid Example:**
```csharp
// Grid accepts ANY item if it fits
grid.CanAcceptItem(anyItem, rotation, position)
  → checks grid bounds + collision
```

## 🔧 Code Usage

### Registering Targets Manually

```csharp
// If you spawn grids/slots dynamically:
InventoryGridVisual newGrid = Instantiate(gridPrefab);
panelManager.RegisterDropTarget(newGrid);

EquipmentSlot newSlot = Instantiate(slotPrefab);
panelManager.RegisterDropTarget(newSlot);
```

### Programmatically Equipping Items

```csharp
// Equip item in code (bypassing drag-drop)
EquipmentSlot helmetSlot = GetHelmetSlot();
InventoryItemSO helmet = GetHelmetItem();

if (helmetSlot.TryEquipItem(helmet))
{
    Debug.Log("Helmet equipped!");
}
```

### Checking What's Equipped

```csharp
EquipmentSlot slot = GetSlot();

if (slot.IsOccupied)
{
    InventoryItemSO equipped = slot.EquippedItem;
    Debug.Log($"Equipped: {equipped.ItemName}");
}
```

### Unequipping

```csharp
EquipmentSlot slot = GetSlot();
InventoryItemSO unequipped = slot.UnequipItem();

if (unequipped != null)
{
    // Do something with the unequipped item
}
```

## 📐 Layout Tips

### DayZ-Style Three-Panel Layout

```
[Loot Container]  [Equipment]  [Player Storage]
[Left Panel]      [Center]     [Right Panel]
    
┌─────────────┐  ┌────────┐  ┌──────────────┐
│ Container   │  │ Helmet │  │ Vest Storage │
│ Drawer 1    │  │ Vest   │  │ ┌──────────┐ │
│ ┌─────────┐ │  │ Weapon │  │ │          │ │
│ │         │ │  │ Side   │  │ │  6x4     │ │
│ │  5x6    │ │  │ Back   │  │ │          │ │
│ │         │ │  └────────┘  │ └──────────┘ │
│ └─────────┘ │              │              │
│             │              │ Backpack     │
│ Drawer 2    │              │ ┌──────────┐ │
│ ┌─────────┐ │              │ │          │ │
│ │         │ │              │ │  8x6     │ │
│ │  5x4    │ │              │ │          │ │
│ └─────────┘ │              │ └──────────┘ │
└─────────────┘              └──────────────┘
```

## ✅ Testing Checklist

### Basic Functionality
- [ ] Drag item from grid → drops in different position
- [ ] Drag item from grid → drops in equipment slot (if type matches)
- [ ] Drag item from slot → drops in grid
- [ ] Drag item from slot → drops in different slot (if type matches)
- [ ] Rotation works during drag (R key)

### Validation
- [ ] Can't place item outside grid bounds
- [ ] Can't place item overlapping other items
- [ ] Can't place wrong item type in equipment slot
- [ ] Can't place item in occupied slot
- [ ] Ghost turns green when valid, red when invalid

### Edge Cases
- [ ] Drag to invalid location → returns to source
- [ ] Drag rotated item near grid edge → validates correctly
- [ ] Multiple grids work independently
- [ ] Can drag between any combination of targets

## 🚀 Next Steps (Future Enhancements)

### Dynamic Grid Spawning
When equipping a backpack, spawn its storage grid:
```csharp
void OnBackpackEquipped(InventoryItemSO backpack)
{
    // Create grid based on backpack properties
    InventorySystem storage = new InventorySystem(
        backpack.StorageWidth, 
        backpack.StorageHeight
    );
    
    InventoryGridVisual gridVisual = Instantiate(gridPrefab);
    gridVisual.Initialize(storage);
    panelManager.RegisterDropTarget(gridVisual);
}
```

### Left Panel Context
Show/hide loot panel based on what player is interacting with:
```csharp
void OpenContainer(Container container)
{
    leftPanel.SetActive(true);
    
    // Spawn grids for container's drawers
    foreach (var drawer in container.Drawers)
    {
        var grid = SpawnGrid(drawer.InventorySystem);
        panelManager.RegisterDropTarget(grid);
    }
}

void CloseContainer()
{
    leftPanel.SetActive(false);
    // Unregister and destroy container grids
}
```

### Gameplay Integration
```csharp
// Hook up equipment events
helmetSlot.OnItemEquipped += (item) => {
    player.ApplyArmorBonus(item.ArmorValue);
};

weaponSlot.OnItemEquipped += (weapon) => {
    player.EquipWeapon(weapon);
};
```

## 🐛 Common Issues

### Ghost not following mouse
- Check that InventoryItemGhost has RectTransform
- Verify Canvas is set to ScreenSpaceOverlay

### Can't drop anywhere
- Check that targets are registered with DragHandler
- Verify InventoryPanelManager.RegisterAllDropTargets() was called
- Enable verbose logging on DragHandler

### Items disappearing
- Check that InventoryGridVisual.Initialize() was called
- Verify InventorySystem is not null
- Check item visuals are spawning in itemContainer, not gridCellContainer

### Slot not accepting items
- Verify item's EquipmentType matches slot's AcceptedTypes
- Check that slot is not already occupied
- Enable verbose logging on EquipmentSlot

## 📚 Architecture Summary

```
InventoryDragHandler (central coordinator)
    ↓ registers
IInventoryDropTarget (interface)
    ↓ implemented by
┌──────────────────────┬──────────────────────┐
│                      │                      │
InventoryGridVisual    EquipmentSlot          (future: other types)
└──────────────────────┴──────────────────────┘
```

This allows **any** component that implements `IInventoryDropTarget` to participate in the drag-drop system!
