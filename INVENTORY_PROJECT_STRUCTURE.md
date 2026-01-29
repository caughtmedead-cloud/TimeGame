# Inventory System Project Structure

## Overview
The inventory system is organized into multiple layers: **Core Runtime**, **Networking**, **Data/Definition**, **UI**, **Utilities**, and **Debug**. This structure supports a grid-based inventory with networked multiplayer functionality.

---

## Directory Structure

```
Assets/
├── _Scripts/
│   ├── Systems/Inventory/
│   │   ├── Core/
│   │   │   ├── Data/
│   │   │   │   └── ItemDefinitionSO.cs
│   │   │   ├── Networking/
│   │   │   │   ├── NetworkedItemData.cs
│   │   │   │   └── NetworkedPlayerInventory.cs
│   │   │   └── Runtime/
│   │   │       ├── InventoryGrid.cs
│   │   │       └── InventoryItem.cs
│   │   └── Utils/
│   │       └── ItemDefinitionRegistry.cs
│   ├── UI/
│   │   ├── InventoryGridUI.cs
│   │   ├── InventoryItemUI.cs
│   │   ├── InventoryItemDragHandler.cs
│   │   ├── InventoryPanelManager.cs
│   │   ├── GridCell.cs
│   │   ├── InputModeIndicator.cs
│   │   ├── PlayerCanvasSetup.cs
│   │   └── Temporal/
│   └── Debug/
│       ├── InventoryDebugger.cs
│       ├── InventoryGridTester.cs
│       └── NetworkInventoryTester.cs
```

---

## Core Components

### **1. Data Layer** (`Core/Data/`)

#### `ItemDefinitionSO.cs`
- **Purpose**: ScriptableObject defining item properties and metadata
- **Key Responsibilities**:
  - Store display name, icon, and visual properties
  - Define item dimensions (width, height)
  - Store item-specific data (rarity, type, description, etc.)
- **Namespace**: `NewThelos.Inventory.Data`
- **Dependencies**: Unity.Engine, ScriptableObject

---

### **2. Runtime Layer** (`Core/Runtime/`)

#### `InventoryItem.cs`
- **Purpose**: Represents a single item instance in the inventory
- **Key Properties**:
  - `instanceId`: Unique identifier for this item instance
  - `itemDefinitionId`: Reference to ItemDefinitionSO
  - `posX, posY`: Grid position (0-based)
  - `isRotated`: Whether item is rotated (affects width/height calculation)
  - `quantity`: Stack count for stackable items
- **Namespace**: `NewThelos.Inventory.Runtime`
- **Used By**: InventoryGrid, UI components

#### `InventoryGrid.cs`
- **Purpose**: Core grid-based inventory system managing item placement
- **Key Responsibilities**:
  - Maintain grid state (Width, Height)
  - Store and manage InventoryItem instances
  - Validate item placement (collision detection, bounds checking)
  - Calculate rotated dimensions for items
  - Support item swapping and repositioning
- **Namespace**: `NewThelos.Inventory.Runtime`
- **Key Methods**:
  - `CanPlaceItem()`: Check if item can fit at position with rotation
  - `PlaceItem()`: Add item to grid at position
  - `RemoveItem()`: Remove item from grid
  - `GetAllItems()`: Retrieve all items in grid
  - `GetItemAtPosition()`: Query item at specific grid cell
- **Dependencies**: InventoryItem, ItemDefinitionRegistry

---

### **3. Networking Layer** (`Core/Networking/`)

#### `NetworkedItemData.cs`
- **Purpose**: Serializable container for item data across network
- **Key Responsibilities**:
  - Serialize/deserialize InventoryItem for network transmission
  - Maintain position, rotation, and stack count
  - Support multiplayer synchronization
- **Namespace**: `NewThelos.Inventory.Networking`
- **Used By**: NetworkedPlayerInventory

#### `NetworkedPlayerInventory.cs`
- **Purpose**: Server-authoritative inventory manager for networked players
- **Key Responsibilities**:
  - Handle player inventory grid state
  - Manage inventory ownership (ownership validation)
  - Execute networked RPC calls for item movements
  - Synchronize inventory across clients
  - Validate moves on server-side
- **Namespace**: `NewThelos.Inventory.Networking`
- **Key RPC Methods**:
  - `MoveItemWithSwap_ServerRpc()`: Move item with potential swap handling
  - `RotateItem_ServerRpc()`: Rotate item in place
  - `DropItem_ServerRpc()`: Drop item from inventory
- **Inheritance**: NetworkBehaviour (Fish-Net networking)
- **Dependencies**: FishNet, InventoryGrid, InventoryItem, ItemDefinitionRegistry

---

### **4. Utilities** (`Utils/`)

#### `ItemDefinitionRegistry.cs`
- **Purpose**: Centralized registry for all ItemDefinitionSO assets
- **Key Responsibilities**:
  - Maintain dictionary of item ID → ItemDefinitionSO mappings
  - Lazy load item definitions
  - Cache loaded definitions for performance
  - Support item lookup by ID at runtime
- **Namespace**: `NewThelos.Inventory.Utils` or `NewThelos.Systems.Inventory.Utils`
- **Key Methods**:
  - `GetItemDefinition(string itemId)`: Retrieve definition by ID
  - `RegisterItemDefinition()`: Add item to registry
  - `GetAllDefinitions()`: Get all registered items

---

## UI Layer Components

### **5. UI Hierarchy** (`UI/`)

#### `InventoryGridUI.cs`
- **Purpose**: Visual representation of InventoryGrid
- **Key Responsibilities**:
  - Render grid cells with proper sizing
  - Manage item UI elements positioning
  - Convert between screen/world/grid coordinates
  - Display grid state and validate placements visually
- **Key Methods**:
  - `WorldToGridPosition()`: Convert world position to grid coordinates
  - `GridToWorldPosition()`: Convert grid coordinates to world position
  - `GetCellSize()`: Return pixel size of grid cell
- **Namespace**: `NewThelos.UI.Inventory`
- **Dependencies**: InventoryGrid, ItemDefinitionRegistry

#### `InventoryItemUI.cs`
- **Purpose**: Visual representation of a single inventory item
- **Key Responsibilities**:
  - Display item sprite/icon on grid
  - Track UI position and grid position
  - Respond to input (click, hover)
  - Update visual state (highlight, damage, etc.)
- **Key Properties**:
  - `Item`: Reference to InventoryItem data
  - `Definition`: ItemDefinitionSO reference
  - `GridPosition`: Current position in grid
- **Namespace**: `NewThelos.UI.Inventory`
- **Interfaces Implemented**: IPointerEnterHandler, IPointerExitHandler

#### `InventoryItemDragHandler.cs`
- **Purpose**: Handle drag-and-drop functionality for inventory items
- **Key Responsibilities**:
  - Track drag state (original position, rotation, parent)
  - Display preview of item at drop location
  - Validate placement during drag
  - Support item rotation (R key during drag)
  - Execute item swaps
  - Send network move requests on drop
- **Key Features**:
  - Real-time collision detection during drag
  - Visual feedback (valid/invalid placement colors)
  - Rotation support (changes item dimensions)
  - Item swap validation (bidirectional fit checking)
  - Alpha blending during drag (transparency feedback)
- **Namespace**: `NewThelos.UI.Inventory`
- **Interfaces Implemented**:
  - IBeginDragHandler
  - IDragHandler
  - IEndDragHandler
  - IPointerEnterHandler
  - IPointerExitHandler
- **Key Methods**:
  - `OnBeginDrag()`: Initialize drag state
  - `OnDrag()`: Update position and preview
  - `OnEndDrag()`: Execute or cancel drop
  - `ToggleRotation()`: Handle R key press
  - `CheckPlacementValidity()`: Validate grid placement
  - `CheckCanSwap()`: Validate bidirectional swap
  - `ExecuteDrop()`: Send network RPC for confirmed move
- **Dependencies**: InventoryGridUI, InventoryItemUI, NetworkedPlayerInventory

#### `InventoryPanelManager.cs`
- **Purpose**: Manage overall inventory panel UI
- **Key Responsibilities**:
  - Spawn/despawn inventory grids
  - Manage panel visibility
  - Coordinate between multiple grids (if applicable)
  - Handle panel input and navigation
- **Namespace**: `NewThelos.UI.Inventory`

#### `GridCell.cs`
- **Purpose**: Individual grid cell UI component
- **Key Responsibilities**:
  - Render single cell background
  - Display hover/selection state
  - Support cell-specific interactions
- **Namespace**: `NewThelos.UI.Inventory`

#### `PlayerCanvasSetup.cs`
- **Purpose**: Initialize player's inventory UI on spawn
- **Key Responsibilities**:
  - Set up Canvas hierarchy
  - Instantiate InventoryGridUI components
  - Connect UI to NetworkedPlayerInventory
  - Configure input handling
- **Namespace**: `NewThelos.UI.Inventory`

#### `InputModeIndicator.cs`
- **Purpose**: Display current input mode status
- **Key Responsibilities**:
  - Show keyboard vs controller mode
  - Update input prompts dynamically
- **Namespace**: `NewThelos.UI.Inventory`

#### `Temporal/` Folder
- **Purpose**: Contains temporary or experimental UI components
- **Note**: May contain work-in-progress features or deprecated components

---

## Debug & Testing

### **6. Debug Components** (`Debug/`)

#### `InventoryDebugger.cs`
- **Purpose**: Runtime debug visualization and logging
- **Key Responsibilities**:
  - Log inventory state changes
  - Display grid collision debug information
  - Visualize item positions and rotations
  - Print statistics and validation results

#### `InventoryGridTester.cs`
- **Purpose**: Unit test component for grid logic
- **Key Responsibilities**:
  - Test item placement validation
  - Test collision detection
  - Test rotation calculations
  - Verify bounds checking

#### `NetworkInventoryTester.cs`
- **Purpose**: Test networked inventory functionality
- **Key Responsibilities**:
  - Simulate network moves
  - Test RPC calls
  - Verify synchronization between clients
  - Test ownership validation

---

## Data Flow

### **Item Placement Flow**
```
User Drag → InventoryItemDragHandler.OnBeginDrag()
  ↓
Store original state, show preview
  ↓
InventoryItemDragHandler.OnDrag()
  ↓
Update preview position → CheckPlacementValidity()
  ↓
InventoryGrid.CanPlaceItem() (via registry lookup)
  ↓
Check collision with GetAllItems()
  ↓
InventoryItemDragHandler.OnEndDrag()
  ↓
ExecuteDrop() → NetworkedPlayerInventory.MoveItemWithSwap_ServerRpc()
  ↓
Server validates → InventoryGrid.PlaceItem()
  ↓
Broadcast to all clients → Update InventoryItemUI positions
```

### **Item Definition Resolution**
```
InventoryItem (has itemDefinitionId)
  ↓
ItemDefinitionRegistry.GetItemDefinition(itemDefinitionId)
  ↓
Return ItemDefinitionSO (contains icon, dimensions, etc.)
```

---

## Key Design Patterns

### **1. Grid-Based Positioning**
- Items occupy rectangular cells (width × height)
- Rotation swaps width/height dimensions
- Collision detection uses AABB (Axis-Aligned Bounding Box)

### **2. Server Authority**
- All moves validated on server via NetworkedPlayerInventory
- Client sends intent, server confirms or rejects
- Grid state is source of truth on server

### **3. Separation of Concerns**
- **Data Layer**: Item definitions (SO)
- **Runtime Layer**: Grid logic and item instances
- **Network Layer**: Multiplayer synchronization
- **UI Layer**: Visual representation and interaction
- **Utils**: Shared lookup/registry functionality

### **4. Rotation Support**
- Items can be rotated (2 states: original, rotated 90°)
- Rotation swaps item width and height
- Rotation is stored per-item and validated on placement

### **5. Item Swapping**
- When dragging onto occupied slot, system validates bidirectional fit
- Both items must fit in their new positions
- Swap is atomic (either both move or neither moves)

---

## Namespace Organization

```
NewThelos.Inventory.Data
  └── ItemDefinitionSO

NewThelos.Inventory.Runtime
  └── InventoryItem
  └── InventoryGrid

NewThelos.Inventory.Networking
  └── NetworkedItemData
  └── NetworkedPlayerInventory

NewThelos.Systems.Inventory.Utils
  └── ItemDefinitionRegistry

NewThelos.UI.Inventory
  └── InventoryGridUI
  └── InventoryItemUI
  └── InventoryItemDragHandler
  └── InventoryPanelManager
  └── GridCell
  └── PlayerCanvasSetup
  └── InputModeIndicator
```

---

## Dependencies Map

```
UI Layer
├── InventoryItemDragHandler
│   ├── InventoryGridUI
│   ├── InventoryItemUI
│   ├── NetworkedPlayerInventory
│   └── ItemDefinitionRegistry
├── InventoryGridUI
│   ├── InventoryGrid
│   ├── ItemDefinitionRegistry
│   └── InventoryItemUI
├── InventoryPanelManager
│   ├── InventoryGridUI
│   └── PlayerCanvasSetup
└── PlayerCanvasSetup
    └── NetworkedPlayerInventory

Networking Layer
└── NetworkedPlayerInventory
    ├── InventoryGrid
    ├── InventoryItem
    └── ItemDefinitionRegistry

Runtime Layer
├── InventoryGrid
│   ├── InventoryItem
│   └── ItemDefinitionRegistry
└── InventoryItem

Utilities
└── ItemDefinitionRegistry
    └── ItemDefinitionSO

Data Layer
└── ItemDefinitionSO (standalone)
```

---

## File Count Summary

| Layer | Files | Purpose |
|-------|-------|---------|
| Core/Data | 1 | Item definitions |
| Core/Runtime | 2 | Grid and item state |
| Core/Networking | 2 | Network synchronization |
| Utils | 1 | Item lookup registry |
| UI | 8 | Visual and interaction |
| Debug | 3 | Testing and debugging |
| **Total** | **17** | **Main inventory system** |

---

## Configuration Points

### In ItemDefinitionSO:
- Item display name and description
- Item icon sprite
- Width and height (base dimensions)
- Item type/category
- Rarity or other metadata

### In InventoryGrid:
- Grid width and height (cell count)
- Cell size in pixels (configured in UI)

### In Drag Handler:
- Drag alpha transparency
- Valid placement color (green)
- Invalid placement color (red)
- Rotation key (R)

### In NetworkedPlayerInventory:
- Network ownership validation
- RPC method parameters
- Network update frequency

---

## Future Extension Points

1. **Item Stacking**: Enhance InventoryItem with quantity and stack behavior
2. **Multi-Grid Support**: Handle multiple inventory grids per player
3. **Persistent Storage**: Add serialization to save/load inventory state
4. **Advanced Filtering**: Filter and sort items in UI
5. **Crafting Integration**: Connect inventory to crafting system
6. **Item Loot**: Add item spawning and pickup mechanics
7. **Equipment Slots**: Add slots outside main grid (armor, weapons)
8. **Item Attributes**: Extend ItemDefinitionSO for dynamic properties

---

**Last Updated**: January 2026
**System Status**: Active (Drag & Drop, Networking, Rotation Support Implemented)
