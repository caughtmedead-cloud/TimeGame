# Inventory UI Player Integration Guide

## 🎯 Overview
This guide walks you through integrating the inventory UI into your player prefab with Tab key toggle functionality.

---

## 📋 What We've Done (Automated)

✅ **Created `InventoryUIController.cs`**
- Located at: `Assets/_Scripts/Inventory/InventoryUIController.cs`
- Handles Tab key input
- Controls inventory UI visibility
- Manages cursor lock/unlock

✅ **Updated Input Actions**
- Added "Inventory" action to `PlayerInputActions.inputactions`
- Bound to: Tab, I, and Gamepad Select button

---

## 🛠️ What You Need To Do (In Unity Editor)

### **Step 1: Regenerate Input Actions Code**

Unity needs to regenerate the C# code from the updated `.inputactions` file:

1. Open Unity
2. Locate `Assets/Settings/PlayerInputActions.inputactions` in Project window
3. **Click on it** to select it
4. In the Inspector, click **"Generate C# Class"** (or it may auto-generate on import)
5. Wait for Unity to recompile

> **Note:** If you don't see changes, try: Right-click → Reimport

---

### **Step 2: Prepare Your Player Prefab**

Find your actual player prefab. Based on your repo, it's likely in one of these locations:
- `Assets/_Prefabs/`
- `Assets/Resources/Prefabs/`
- Or search for "Player" in Project window

**Open the prefab for editing:**
1. Double-click the prefab in Project window, OR
2. Drag it into the scene and edit there (remember to Apply changes!)

---

### **Step 3: Add InventoryUIController Component**

1. With your player prefab selected
2. In Inspector, click **"Add Component"**
3. Search for **"InventoryUIController"**
4. Add it to the player

---

### **Step 4: Move Inventory Canvas Into Player Prefab**

Currently your inventory UI is in the scene. We need to move it into the player prefab:

#### **Option A: If Working in Scene**
1. Find your **Inventory Canvas** in the Hierarchy (the one with InventoryUIManager)
2. **Drag it** onto your Player prefab in the Hierarchy
   - It should become a child of the Player
3. The hierarchy should look like:
   ```
   Player (Prefab)
   ├── Camera
   ├── Inventory Canvas  ← NEW
   │   ├── Background
   │   ├── Storage Panel
   │   ├── Backpack Panel
   │   └── ... (rest of UI)
   └── ... (other player components)
   ```

#### **Option B: If Working in Prefab Editor**
1. Open the Player prefab in Prefab mode
2. Right-click in Hierarchy → Create → UI → Canvas
3. Rename to "Inventory Canvas"
4. Copy all your inventory UI elements from the scene into this canvas
5. Delete the old scene canvas

---

### **Step 5: Configure Canvas Settings**

The inventory canvas needs special settings to work with the player:

1. Select the **Inventory Canvas** (now under Player)
2. In Inspector, configure:
   - **Render Mode:** Screen Space - Overlay (or Camera if you prefer)
   - **Canvas Scaler:**
     - UI Scale Mode: Scale With Screen Size
     - Reference Resolution: 1920 x 1080
     - Match: 0.5 (or your preference)
   - **Graphic Raycaster:** Make sure it's present

---

### **Step 6: Wire Up References**

1. Select your **Player** prefab root
2. Find the **InventoryUIController** component
3. In Inspector:
   - **Inventory Canvas:** Drag the "Inventory Canvas" GameObject here
   - **Debug Mode:** Check this if you want console logs

---

### **Step 7: Apply Prefab Changes**

If you edited the prefab in the scene:
1. Select the Player in Hierarchy
2. In Inspector, at the top: **Overrides → Apply All**

If in Prefab mode:
1. Just save (Ctrl+S or File → Save)

---

### **Step 8: Remove Scene Inventory UI**

Now that each player has their own inventory:
1. Delete the **old Inventory Canvas** from your scene (if still there)
2. The inventory UI should ONLY exist inside the player prefab

---

### **Step 9: Test Solo (Single Player)**

1. Enter Play Mode
2. Press **Tab** (or **I**)
3. **Expected Result:**
   - Inventory opens
   - Cursor becomes visible
   - You can drag items
4. Press **Tab** again
   - Inventory closes
   - Cursor locks back for gameplay

---

### **Step 10: Test Multiplayer (Host + Client)**

1. Start as **Host**
2. Press Tab → Your inventory opens
3. Start a **Client** build or second instance
4. On Client, press Tab → Client's inventory opens
5. **Verify:**
   - Each player sees only THEIR inventory
   - Inventories are independent
   - No errors in console

---

## 🐛 Troubleshooting

### **Problem: "Inventory" action not found**
**Solution:** 
- Make sure you regenerated the C# class from the .inputactions file
- Check that Unity recompiled (bottom-right should show no compilation)
- Try closing and reopening Unity

### **Problem: Inventory doesn't open**
**Solution:**
- Check Console for errors
- Make sure InventoryUIController has the canvas reference assigned
- Verify the canvas GameObject is actually there in the prefab

### **Problem: Both players see the same inventory**
**Solution:**
- Make sure you deleted the scene's inventory canvas
- Each player should have their OWN canvas as a child
- Check that the canvas is inside the player prefab, not outside

### **Problem: Canvas doesn't appear**
**Solution:**
- Check Canvas render mode (Screen Space - Overlay is safest)
- Make sure Canvas is enabled
- Check that the canvas has an EventSystem in the scene (only one needed globally)

---

## ✨ Next Steps (Phase 2 - Network Sync)

After local testing works, we'll add:
- **Network synchronization** of inventory state
- **Server-authoritative inventory** 
- **Inventory UI visible to both players** (if you want that)

But first, let's get the basic Tab toggle working!

---

## 📁 Files Modified

- ✅ `Assets/_Scripts/Inventory/InventoryUIController.cs` (NEW)
- ✅ `Assets/Settings/PlayerInputActions.inputactions` (UPDATED)
- ⏳ `Assets/Settings/PlayerInputActions.cs` (Will auto-regenerate)
- ⏳ Your Player Prefab (YOU need to edit)

---

## 🎮 Controls Summary

| Action | Keyboard | Gamepad |
|--------|----------|---------|
| Toggle Inventory | **Tab** or **I** | **Select/Back** |
| Move | WASD | Left Stick |
| Look | Mouse | Right Stick |
| Jump | Space | A/Cross |
| Sprint | Left Shift | Left Trigger |
| Crouch | C or Left Ctrl | Right Stick Press |

---

**Ready to integrate? Start with Step 1!** 🚀
