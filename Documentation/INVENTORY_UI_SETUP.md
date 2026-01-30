# Inventory UI Test Setup Guide

## What We Built

✅ **Manual Grid Positioning** - No Unity LayoutGroups, full control  
✅ **InventoryGridVisual** - Draws grid background, spawns item visuals  
✅ **InventoryItemVisual** - Simple sprite display  
✅ **Test Harness** - Keyboard controls for testing

---

## Quick Setup (5 minutes)

### Step 1: Create Test Items

1. **Create folder**: `Assets/_Game/Data/Items/Test/`
2. **Right-click** → Create → TimeGame/Inventory/Inventory Item
3. **Create 2-3 test items**:
   - **TestRifle**: Width=1, Height=4, CanRotate=true
   - **TestPistol**: Width=1, Height=2, CanRotate=true  
   - **TestMedkit**: Width=2, Height=2, CanRotate=false

### Step 2: Create Test Scene

1. **New Scene** → Save as `InventoryTest.unity`
2. **Add to Hierarchy**:
   ```
   Canvas (Screen Space - Overlay)
     └─ InventoryVisual (Empty GameObject)
         └─ Add Component: InventoryGridVisual
         └─ Add Component: InventoryTestHarness
   ```

3. **Configure Canvas**:
   - Canvas Scaler: Scale With Screen Size
   - Reference Resolution: 1920×1080

4. **Configure InventoryVisual RectTransform**:
   - Anchors: Center-Middle
   - Width: 500 (will auto-resize to grid size)
   - Height: 500

### Step 3: Wire Up Test Harness

Select **InventoryVisual** GameObject:

**InventoryTestHarness** component:
- Grid Width: 10
- Grid Height: 10
- Cell Size: 50
- Max Weight: 100
- **Test Items**: Drag your test items here (TestRifle, TestPistol, etc.)
- **Grid Visual**: Drag the InventoryGridVisual component here

**InventoryGridVisual** component:
- Grid Cell Prefab: Leave empty (auto-creates)
- Item Visual Prefab: Leave empty (auto-creates)
- Grid Cell Color: Dark gray
- Enable Debug Logging: ✓ True

### Step 4: Test!

**Press Play**

**Keyboard Controls**:
- **1** - Spawn item at (2,2)
- **2** - Spawn item at (5,5)
- **3** - Spawn item at (0,0)
- **R** - Rotate next spawn (Down→Left→Up→Right)
- **N** - Cycle through test items
- **X** - Remove item at (2,2)
- **C** - Clear all
- **W** - Print weight info

**What You Should See**:
- Grid background appears (10×10 cells)
- Press **1** → Item sprite appears on grid
- Press **R** then **1** → Item appears rotated
- Press **X** → Item disappears
- Console shows detailed logs

---

## Troubleshooting

### "No test items configured!"
→ Assign your InventoryItemSO assets to the Test Harness

### "No InventoryGridVisual assigned!"
→ Drag the InventoryGridVisual component to the Grid Visual field

### Grid doesn't appear
→ Check Canvas settings (Screen Space - Overlay)  
→ Check InventoryVisual is child of Canvas  
→ Check RectTransform is visible (not at 0,0 size)

### Items don't appear
→ Check test items have sprites assigned (ItemIcon field)  
→ Check console for placement errors  
→ Try different positions (1, 2, 3 keys)

---

## Expected Behavior

**Visual + Logs Together**:

```
Press '1':
[Console] "Attempting to spawn TestRifle at (2,2) facing Down"
[Console] "✓ Successfully placed TestRifle"
[Visual] Rifle sprite appears on grid at cell (2,2)

Press 'R' 3 times:
[Console] "Current rotation: Right (270°)"

Press '2':
[Console] "Attempting to spawn TestRifle at (5,5) facing Right"
[Console] "✓ Successfully placed TestRifle"
[Visual] Rifle sprite appears ROTATED at cell (5,5)

Press 'X':
[Console] "Attempting to remove item at (2,2)"
[Console] "✓ Successfully removed item"
[Visual] Rifle sprite at (2,2) disappears
```

---

## Next Steps

Once this works:
1. ✅ Core + Inventory layer validated
2. ✅ Visual feedback confirmed
3. ✅ Manual positioning working
4. → Add scroll containers (for multiple inventories)
5. → Add networking layer
6. → Add drag-drop interaction

---

**Test it and let me know what you see!**
