# Drag-Drop Setup Guide

## Overview
This guide explains how to add drag-drop functionality to your inventory test scene.

## What We Built
- **InventoryItemGhost**: Visual preview that follows the mouse while dragging
- **InventoryDragHandler**: Handles mouse input and drag-drop logic
- **Valid/Invalid Feedback**: Green = can place, Red = blocked

## Setup Steps

### Step 1: Create Ghost GameObject

1. In your **InventoryTest** scene, find the **InventoryVisual** GameObject (the one with InventoryGridVisual component)
2. Right-click **InventoryVisual** → Create Empty Child
3. Rename it to **ItemGhost**
4. Add components:
   - Add Component → **Rect Transform**
   - Add Component → **Canvas Group**
   - Add Component → **InventoryItemGhost** (Scripts/Systems/Inventory/UI/)

### Step 2: Create Drag Handler GameObject

1. Right-click **InventoryVisual** → Create Empty Child
2. Rename it to **DragHandler**
3. Add Component → **InventoryDragHandler** (Scripts/Systems/Inventory/UI/)

### Step 3: Wire Up References

#### On DragHandler (InventoryDragHandler component):
1. **Grid Visual**: Drag **InventoryVisual** GameObject here
2. **Ghost**: Drag **ItemGhost** GameObject here
3. **Parent Canvas**: Drag the **Canvas** GameObject (root of UI) here
4. **Rotate Key**: Leave as R (default)
5. **Verbose Logging**: Check this for testing

### Step 4: Update Test Harness

We need to initialize the drag handler. Modify **InventoryTestHarness.cs**:

In the `Start()` method, after `gridVisual.Initialize(inventorySystem);`, add:

```csharp
// Initialize drag handler if present
InventoryDragHandler dragHandler = GetComponentInChildren<InventoryDragHandler>();
if (dragHandler != null)
{
    dragHandler.Initialize(inventorySystem);
}
```

## Testing Drag-Drop

### Basic Drag
1. Press **Play**
2. Press **1** to spawn a bandage at (2,2)
3. Press **2** to spawn fuel at (5,5)
4. **Click and hold** on an item
5. **Move mouse** - ghost follows, snapped to grid
6. Ghost turns **GREEN** when placement is valid
7. Ghost turns **RED** when placement is blocked
8. **Release mouse** to drop

### Rotation While Dragging
1. Click and hold an item
2. Press **R** while dragging - ghost rotates
3. Release to drop in rotated position

### Cancel Drag
1. Click and hold an item
2. Press **Right-Click** or **Escape** - item returns to original position

## Expected Behavior

✅ **Click item** → Visual disappears, ghost appears
✅ **Move mouse** → Ghost follows, snapped to grid cells
✅ **Valid placement** → Ghost is semi-transparent **GREEN**
✅ **Invalid placement** → Ghost is semi-transparent **RED**
✅ **Press R** → Ghost rotates (if item can rotate)
✅ **Release mouse** → Item moves to new position
✅ **Right-click** → Drag canceled, item returns

## Troubleshooting

**Ghost doesn't appear:**
- Check ItemGhost has CanvasGroup component
- Check DragHandler references are wired up
- Check console for errors

**Ghost doesn't follow mouse:**
- Check Parent Canvas reference is set
- Make sure Canvas is in Screen Space - Overlay mode

**Items don't move after drag:**
- Check console for placement errors
- Verify InventorySystem allows the placement

**Rotation doesn't work:**
- Check the item's CanRotate property (in InventoryItemSO)
- Check R key isn't blocked by other input

## Next Steps

Once drag-drop works:
1. **Phase 2**: Multiple inventories (left/right panels)
2. **Phase 3**: Scrolling for large inventories
3. **Phase 4**: Networking (FishNet integration)
4. **Phase 5**: Player integration

## Debug Tips

**Enable verbose logging** on DragHandler to see:
- When drag starts/ends
- Mouse grid position
- Validation results
- Drop success/failure

**Console output example:**
```
[InventoryDragHandler] Started dragging Bandage from (2, 2)
[InventoryDragHandler] Rotated ghost to Left
[InventoryDragHandler] Dropped Bandage at (5, 3) facing Left
```
