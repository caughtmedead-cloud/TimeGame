# Inventory Drag-Drop Setup Guide

## Overview
The drag-drop system uses Unity's Event System for robust click detection. This follows CodeMonkey's proven architecture from his Inventory Tetris tutorial.

## Architecture

### Components
1. **InventoryItemDragDrop** - Attached to each item visual, handles Unity Event System callbacks
2. **InventoryDragHandler** - Central system that manages the drag operation and ghost
3. **InventoryItemGhost** - Visual preview shown during drag

### Flow
```
User clicks item
    ↓
InventoryItemDragDrop receives OnBeginDrag event
    ↓
Calls InventoryDragHandler.OnItemBeginDrag()
    ↓
Handler removes item from grid, shows ghost
    ↓
Ghost follows mouse (updated in Update())
    ↓
User releases mouse
    ↓
InventoryItemDragDrop receives OnEndDrag event
    ↓
Calls InventoryDragHandler.OnItemEndDrag()
    ↓
Handler tries to place item at new position
```

## Unity Scene Setup

### 1. Create Ghost GameObject
Under your `InventoryVisual` GameObject:
- Right-click → Create Empty
- Name: "ItemGhost"
- Add Component: **RectTransform**
- Add Component: **CanvasGroup**
- Add Component: **InventoryItemGhost** script

### 2. Configure Drag Handler
Under your `InventoryVisual` GameObject:
- Right-click → Create Empty  
- Name: "DragHandler"
- Add Component: **InventoryDragHandler** script

### 3. Wire References
Select the **DragHandler** GameObject and configure:
- **Grid Visual**: Drag `InventoryVisual` GameObject here
- **Ghost**: Drag `ItemGhost` GameObject here
- **Rotate Key**: R (default)
- **Verbose Logging**: ✓ (for testing)

### 4. Configure Ghost Colors
Select the **ItemGhost** GameObject and configure:
- **Valid Color**: Semi-transparent green (default: 0.3, 1, 0.3, 0.6)
- **Invalid Color**: Semi-transparent red (default: 1, 0.3, 0.3, 0.6)

## How It Works

### Automatic Component Setup
When items are spawned, `InventoryItemVisual.Initialize()` automatically:
1. Adds **InventoryItemDragDrop** component
2. Adds **CanvasGroup** component
3. Calls `Setup()` with grid reference and item ID

**You don't need to manually add these components!** They're added automatically when items spawn.

### Event System Integration
Unity's Event System automatically detects:
- **OnBeginDrag** - User clicks and starts dragging
- **OnDrag** - User moves mouse while dragging (unused in our implementation)
- **OnEndDrag** - User releases mouse

### Visual Feedback
During drag:
- Original item becomes semi-transparent (alpha 0.7)
- Ghost appears following mouse
- Ghost is GREEN when placement is valid
- Ghost is RED when placement is blocked
- Pressing R rotates the ghost (if item can rotate)

## Testing

### Expected Behavior
1. **Click and drag** an item
   - Original item becomes semi-transparent
   - Ghost appears at mouse position
   - Ghost snaps to grid cells

2. **Move mouse**
   - Ghost follows smoothly
   - Color changes based on validity:
     - ✅ Green = can place here
     - ❌ Red = blocked

3. **Press R** (while dragging)
   - Ghost rotates (if item supports rotation)
   - Validity re-checks automatically

4. **Release mouse**
   - If valid: Item moves to new position
   - If invalid: Item returns to original position
   - Ghost disappears
   - Original item returns to full opacity

### Debug Logs to Check
With Verbose Logging enabled, you should see:
```
[InventoryDragHandler] Drag handler initialized
[InventoryItemGhost] Initialized with Bandage, size 1x2
[InventoryDragHandler] Started dragging Bandage from (2, 2)
[InventoryItemGhost] Ghost shown
[InventoryItemGhost] Rotated to Right, new size 2x1
[InventoryDragHandler] Dropped Bandage at (5, 5) facing Right
[InventoryItemGhost] Ghost hidden
```

## Troubleshooting

### Ghost Doesn't Appear
**Check:**
1. Is `ItemGhost` a child of `InventoryVisual`?
2. Does `ItemGhost` have `InventoryItemGhost` component?
3. Is ghost reference wired on `DragHandler`?
4. Check console for ghost initialization logs

### Can't Click Items
**Check:**
1. Is there an **EventSystem** in the scene? (should auto-create)
2. Does your Canvas have **Graphic Raycaster** component?
3. Are item visuals in the UI layer?
4. Is the Canvas set to "Screen Space - Overlay"?

### Ghost Doesn't Follow Mouse
**Check:**
1. Is `gridVisual` reference set on `DragHandler`?
2. Check console for Update() errors
3. Ensure Canvas is "Screen Space - Overlay" (or camera is set for World Space)

### Items Return to Original Position
This means placement validation is failing.
**Check:**
1. Is target position valid (in grid bounds)?
2. Is target position blocked by another item?
3. Check console logs for validation failures
4. Try placing in an empty area first

### Rotation Doesn't Work
**Check:**
1. Does the item's SO have `canRotate = true`?
2. Is R key being pressed while dragging?
3. Check console for rotation logs

## Key Differences from Manual Input

### Why Unity Event System?
CodeMonkey's approach uses Unity's Event System because:
1. **Automatic drag detection** - No manual timing/delay needed
2. **Built-in drag threshold** - Prevents accidental drags
3. **Works with UI** - Designed for Canvas/UI interactions
4. **Reliable** - Unity handles edge cases

### Comparison

| Manual Input (Old) | Event System (New) |
|--------------------|-------------------|
| Check Input.GetMouseButtonDown() | IBeginDragHandler interface |
| Custom delay timer | Unity handles threshold |
| Manual click detection | Automatic with raycasts |
| Prone to timing bugs | Rock solid |

## Next Steps

Once drag-drop is working:
1. **Phase 2**: Multiple inventory grids (left/right panels)
2. **Phase 3**: Cross-grid transfers
3. **Phase 4**: FishNet networking integration
4. **Phase 5**: Player prefab integration
