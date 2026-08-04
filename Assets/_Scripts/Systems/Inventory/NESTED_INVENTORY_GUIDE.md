# Nested Inventory System Guide

## Overview

The nested inventory system allows containers (backpacks, chests, crates, etc.) to store items inside them. This supports:
- **Backpacks on players** storing their contents
- **World items** (dropped backpacks) preserving their contents
- **Storage containers** with loot
- **Recursive nesting** (backpacks within backpacks) with all ItemInstance data preserved

## Architecture

### Core Components

1. **ContainerItemData** (`ContainerItemData.cs`)
   - Serializable data structure for storing container contents
   - Contains list of `CompartmentData` (for multi-compartment containers)
   - Each compartment contains list of `PlacedItemData` (the items inside)
   - Supports recursive serialization (containers within containers)

2. **PlacedItem.ContainerInventory** (`PlacedItem.cs`)
   - Runtime `InventorySystem` attached to PlacedItem
   - Null for non-container items
   - Stores live inventory data during gameplay
   - NOT automatically serialized (use ContainerItemData for that)

3. **WorldItem.ContainerData** (`WorldItem.cs`)
   - Stores serialized container data when item is in world
   - Preserved through pickup/drop cycles
   - Loaded back into PlacedItem.ContainerInventory on pickup

4. **ContainerHelper** (`ContainerHelper.cs`)
   - Static utility class for container operations
   - `InitializeContainerInventory()` - Create inventory for container items
   - `OpenContainer()` - Open container in UI
   - `GetTotalWeight()` - Calculate recursive weight including nested containers

## How It Works

### Creating Container Items

Items that provide storage are marked in their InventoryItemSO:

```csharp
// In InventoryItemSO Inspector:
ProvidesStorage = true
StorageGridSize = (6, 4)  // 6x4 grid
StorageMaxWeight = 20f    // Max 20kg capacity
```

### Container Lifecycle

1. **Initial Creation**
   When a container item is first created (looted, crafted, purchased):
   ```csharp
   // ContainerInventory starts as null
   // It's created when first opened or when items are added
   ```

2. **Opening Container**
   ```csharp
   // User right-clicks backpack in inventory
   // Context menu shows "Open" option
   // ItemUsageHandler.HandleOpenItem() is called
   // ContainerHelper.OpenContainer() creates/loads the inventory
   // ContainerInteractionManager displays the grid UI
   ```

3. **Dropping Container**
   ```csharp
   // User drops backpack from inventory
   // ItemUsageHandler.HandleDropAllItems() checks if item has ContainerInventory
   // ContainerItemData.FromInventorySystem() serializes all contents
   // WorldItem spawned with containerData parameter
   // All items inside are preserved in serialized form
   ```

4. **Picking Up Container**
   ```csharp
   // User picks up backpack from world
   // PlayerItemInteraction.TryAddToAnyInventoryGrid() receives containerData
   // PlacedItem created in inventory
   // containerData.LoadIntoInventorySystem() recreates the inventory
   // PlacedItem.ContainerInventory is populated with all items
   // ItemInstances are preserved (durability, uses, etc.)
   ```

### Recursive Nesting

Containers can hold containers, which can hold containers, etc:

```
Backpack (20kg capacity)
├── Bandage x3
├── Small Pouch (5kg capacity)
│   ├── Ammo x30
│   └── Grenade x2
└── Medical Kit (10kg capacity)
    ├── Morphine x1
    └── Bandage x5
```

Weight is calculated recursively:
```csharp
float totalWeight = ContainerHelper.GetTotalWeight(backpackItem);
// Returns: backpack weight + all contents + nested container contents
```

## Implementation Details

### Data Structures

**PlacedItemData** - Serializable representation of a PlacedItem:
```csharp
public class PlacedItemData
{
    public InventoryItemSO ItemDefinition;
    public Vector2Int AnchorPosition;
    public GridDirection Rotation;
    public int StackCount;
    public List<ItemInstance> ItemInstances;  // For tracked items
    public ContainerItemData ContainerData;    // Recursive nesting
}
```

**ContainerItemData** - Stores all compartments and their items:
```csharp
public class ContainerItemData
{
    public List<CompartmentData> Compartments;

    // Helper methods:
    public static ContainerItemData FromInventorySystem(InventorySystem inv);
    public void LoadIntoInventorySystem(InventorySystem inv);
    public float GetTotalWeight(bool includeContainerWeight);
}
```

### Key Methods

**Serialization** (Inventory → Data):
```csharp
ContainerItemData data = ContainerItemData.FromInventorySystem(placedItem.ContainerInventory);
```

**Deserialization** (Data → Inventory):
```csharp
containerData.LoadIntoInventorySystem(inventorySystem, compartmentIndex: 0);
```

**Opening Container**:
```csharp
ContainerHelper.OpenContainer(placedItem, containerInteractionManager);
```

### Weight Calculation

The system supports recursive weight calculation:

```csharp
// Calculate weight of just the container and direct contents
float contentWeight = ContainerHelper.GetContainerContentWeight(backpack);

// Calculate total weight including nested containers
float totalWeight = ContainerHelper.GetTotalWeight(backpack);
```

This is important for:
- Enforcing weight limits in parent containers
- Calculating player encumbrance
- Displaying accurate container weights in UI

## Usage Examples

### Example 1: Creating a Backpack with Items

```csharp
// Create backpack item in player inventory
InventoryItemSO backpackDef = /* backpack ScriptableObject */;
bool success = inventorySystem.TryAddItem(
    backpackDef,
    new Vector2Int(0, 0),
    GridDirection.Down,
    out PlacedItem backpackItem
);

if (success)
{
    // Initialize its container inventory
    ContainerHelper.InitializeContainerInventory(backpackItem);

    // Add items to the backpack
    InventoryItemSO bandageDef = /* bandage ScriptableObject */;
    backpackItem.ContainerInventory.TryAddItem(
        bandageDef,
        new Vector2Int(0, 0),
        GridDirection.Down,
        out PlacedItem bandage,
        stackCount: 3
    );
}
```

### Example 2: Dropping Backpack with Contents

```csharp
// When dropping, container data is automatically serialized
// This happens in ItemUsageHandler.HandleDropAllItems()

ContainerItemData containerData = null;
if (itemDef.ProvidesStorage && item.ContainerInventory != null)
{
    containerData = ContainerItemData.FromInventorySystem(item.ContainerInventory);
}

WorldItem droppedItem = WorldItem.Spawn(
    itemDef,
    spawnPos,
    Quaternion.identity,
    1,
    instance: null,
    targetScene,
    containerData  // Preserved here
);
```

### Example 3: Opening Container from Inventory

```csharp
// User right-clicks backpack → Context Menu "Open"
// ItemUsageHandler.HandleOpenItem() is called:

private void HandleOpenItem(PlacedItem item, InventoryGridVisual grid)
{
    ContainerInteractionManager containerManager = FindObjectOfType<ContainerInteractionManager>();
    ContainerHelper.OpenContainer(item, containerManager);
}

// Container UI appears showing backpack contents
// Player can drag items in/out
// Changes are live in PlacedItem.ContainerInventory
```

### Example 4: Nested Containers

```csharp
// Create large backpack
PlacedItem largeBackpack = /* ... */;
ContainerHelper.InitializeContainerInventory(largeBackpack);

// Add small pouch inside large backpack
InventoryItemSO pouchDef = /* ... with ProvidesStorage = true */;
largeBackpack.ContainerInventory.TryAddItem(
    pouchDef,
    new Vector2Int(0, 0),
    GridDirection.Down,
    out PlacedItem pouch
);

// Initialize pouch's container
ContainerHelper.InitializeContainerInventory(pouch);

// Add items to the pouch (nested)
InventoryItemSO ammoDef = /* ... */;
pouch.ContainerInventory.TryAddItem(
    ammoDef,
    new Vector2Int(0, 0),
    GridDirection.Down,
    out PlacedItem ammo,
    stackCount: 30
);

// Drop the large backpack
// ALL nested contents are preserved through serialization!
```

## Integration Points

### ItemUsageHandler
- `HandleOpenItem()` - Opens container when "Open" is clicked
- `HandleDropAllItems()` / `HandleDropOneItem()` - Serializes container data when dropping

### PlayerItemInteraction
- `TryAddToAnyInventoryGrid()` - Restores container inventory when picking up

### InventoryContextMenu
- `OnOpenItem` event - Triggers when user clicks "Open" button
- Shows "Open" button for items with `ProvidesStorage = true`

### ContainerInteractionManager
- `OpenContainer()` - Displays container grid in UI
- `CloseContainer()` - Hides container UI
- Handles loading existing inventory into grid visuals

## Testing Checklist

- [ ] Create backpack in inventory
- [ ] Open backpack (should show empty grid)
- [ ] Add items to backpack
- [ ] Close and reopen backpack (items should persist)
- [ ] Drop backpack on ground
- [ ] Pick up backpack (items should still be inside)
- [ ] Create nested containers (pouch in backpack)
- [ ] Add items to nested pouch
- [ ] Drop and pickup nested container (all items preserved)
- [ ] Check weight calculation includes nested items
- [ ] Verify ItemInstance data preserved (durability, uses)

## Known Limitations

1. **No UI indicator for container contents** - Can't see if backpack has items without opening it
   - TODO: Add item count badge to container visuals
   - TODO: Add weight display for containers

2. **No auto-stacking when loading saved containers** - Items are placed at exact saved positions
   - This is intentional to preserve player organization

3. **Container weight not included in parent weight limit during creation**
   - When creating a container inventory, it doesn't check parent weight limit
   - This is handled at the InventorySystem level for the parent

## Future Enhancements

1. **Container Quick Preview** - Hover tooltip showing container contents
2. **Container Filters** - Only allow specific item types in containers
3. **Container Durability** - Containers can degrade/break
4. **Locked Containers** - Require keys or lockpicking
5. **Network Sync** - FishNet integration for multiplayer
6. **Container Sound Effects** - Open/close sounds based on container type
7. **Container Animations** - Visual feedback when opening/closing

## Performance Notes

- Container serialization is done only when dropping/saving
- Runtime operations use live InventorySystem (fast)
- Recursive weight calculation is cached where possible
- No performance impact for non-container items
- Deep nesting (10+ levels) may impact serialization time

## Debugging

Enable debug logs in:
```csharp
// ItemUsageHandler
[SerializeField] private bool debugMode = true;

// ContainerInteractionManager
[SerializeField] private bool verboseLogging = true;
```

Check console for:
- `[ItemUsageHandler] Container has X items inside` (when dropping)
- `[ContainerInteractionManager] Spawned container grid` (when opening)
- `[ContainerHelper]` prefixed messages for container operations
