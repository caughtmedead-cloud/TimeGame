# Circular Terrain System - Complete Guide

**Complete documentation for the Thelos circular ring terrain workflow**

Unity 6 | URP | ProPixelizer Compatible

---

## 📋 Table of Contents

1. [Quick Start](#quick-start)
2. [Terrain Generator](#terrain-generator)
3. [Vertex Color Painting](#vertex-color-painting)
4. [Underground Holes](#underground-holes)
5. [Grass Placement](#grass-placement)
6. [Shaders & Materials](#shaders--materials)
7. [Presets](#presets)
8. [Workflows](#workflows)
9. [Troubleshooting](#troubleshooting)

---

## 🚀 Quick Start

### 30-Second Workflow

```
1. Thelos → Advanced Circular Terrain Generator
2. Load preset or configure settings
3. Vertex Color Mode: WHITE ✅
4. Generate Advanced Terrain
5. Thelos → Vertex Color Painter
6. Paint textures (R/G/B/A channels)
7. Done! ✅
```

### Tools Menu

```
Thelos/
  ├─ Advanced Circular Terrain Generator  (Main tool)
  ├─ Vertex Color Painter                 (Texture painting)
  ├─ Terrain Hole Placer                  (Underground access)
  └─ Grass Instance Placer                (Grass decoration)
```

---

## 🏗️ Terrain Generator

**Path:** `Thelos → Advanced Circular Terrain Generator`

### Overview

Generates circular ring terrain meshes with:
- Master Size mode (600m city, percentage-based rings)
- Preset mode (custom radius)
- Layered noise (large/medium/small features)
- Radial elevation curves
- Procedural hills and valleys
- Multi-material zones (optional)
- Vertex color initialization
- Mesh colliders
- Asset saving

### Ring Types

#### Master Size Mode (Recommended)

```
Total City: 600m radius
  Inner Ring:  0 - 200m   (33.33%)  Reactor/Lab
  Middle Ring: 200 - 400m (33.33%)  Residential
  Outer Ring:  400 - 600m (33.34%)  Industrial

Advantages:
  ✅ Consistent sizing across rings
  ✅ Perfect alignment at boundaries
  ✅ Easy to plan city layout
  ✅ Percentage-based control
```

#### Preset Mode

```
Custom radius per ring:
  Inner Radius: 0m
  Outer Radius: 200m

Advantages:
  ✅ Full manual control
  ✅ Non-standard sizes
  ✅ One-off special rings
```

### Settings Reference

#### Mesh Quality

| Setting | Prototype | Standard | High | Ultra |
|---------|-----------|----------|------|-------|
| Circle Segments | 64 | 128 | 196 | 256 |
| Radial Divisions | 24 | 48 | 64 | 80 |
| Use Case | Testing | Production | Important | Hero |

**Note:** Higher quality = smoother curves but slower generation.

#### Noise Layers

```
Large Features:
  Scale: 0.02 (huge terrain forms)
  Strength: 15m (major elevation changes)
  
Medium Features:
  Scale: 0.05 (medium hills/valleys)
  Strength: 8m (noticeable variation)
  
Small Details:
  Scale: 0.15 (fine surface detail)
  Strength: 2m (subtle bumps)
  
Layer them for natural terrain! ✅
```

#### Radial Elevation

```
Uses AnimationCurve for height by distance:
  
Default curve:
  Inner edge (0.0): 1.0 (high)
  Outer edge (1.0): 0.5 (low)
  
Result: Terrain slopes down toward outer edge
Max Difference: 20m elevation change

Customize for:
  - Bowl shapes (outer higher)
  - Plateau shapes (flat center)
  - Mountain shapes (center peak)
```

#### Hills & Valleys

```
Hills:
  Count: 5 (random placement)
  Radius: 30m
  Height: 10m
  
Valleys:
  Count: 3 (random placement)
  Depth: 8m
  
Procedurally placed, adds variation!
```

#### Vertex Color Modes

**WHITE Mode (Default)** ⭐
```
✅ Use this for painting!
All vertices = (1, 1, 1, 1)
Ready for Vertex Color Painter
R/G/B/A channels available
Perfect for texture blending
```

**HEIGHT GRADIENT Mode**
```
⚠️ Special use only
Grayscale based on elevation
NOT for manual painting
For height-based shader effects
```

#### Output Options

```
Save Mesh Asset:
  ✅ Saves to Assets/_Meshes/Terrain/
  Auto-generates unique name
  
Generate Collider:
  ✅ Adds MeshCollider component
  Uses generated mesh
  
Optimize Mesh:
  ✅ Runs mesh optimization
  Better performance
```

### Generation Tips

```
Inner Ring:
  196×64 segments
  Moderate noise (reactor area)
  Elevation toward center
  White vertex colors
  
Middle Ring:
  196×64 segments
  Gentle noise (residential)
  Subtle slopes
  White vertex colors
  
Outer Ring:
  128×48 segments (performance)
  Rough noise (wasteland)
  More hills/valleys
  White vertex colors
```

---

## 🎨 Vertex Color Painting

**Path:** `Thelos → Vertex Color Painter`

### Overview

Paint vertex colors on generated terrain meshes to blend up to 4 textures.

**Channels:**
- R = Texture 1 weight
- G = Texture 2 weight
- B = Texture 3 weight
- A = Texture 4 weight

### Basic Usage

```
Step 1: Generate Terrain
  Use Advanced Circular Terrain Generator
  Vertex Color Mode: WHITE ✅
  Generate mesh
  
Step 2: Open Painter
  Thelos → Vertex Color Painter
  
Step 3: Select Mesh
  Drag terrain GameObject to "Target Object" field
  Or click and select in scene
  
Step 4: Configure Brush
  Brush Size: 5m (radius)
  Brush Strength: 0.5 (50% opacity)
  Brush Falloff: 0.5 (soft edges)
  
Step 5: Choose Channel
  Current Channel: R (texture 1)
  
Step 6: Paint Mode
  Mode: Paint (not Erase/View)
  
Step 7: Paint!
  Click and drag in Scene view
  Hold Shift for continuous painting
  
Step 8: Switch Channels
  Channel: G (texture 2)
  Paint different areas
  
Step 9: Apply
  Click "Apply Changes"
  Vertex colors saved to mesh!
```

### Brush Settings

```
Brush Size: 0.1 - 50m
  Small: Detail work (paths, edges)
  Medium: General painting (5-10m)
  Large: Broad strokes (20m+)
  
Brush Strength: 0.0 - 1.0
  Low (0.1-0.3): Subtle blending
  Medium (0.4-0.6): Normal painting
  High (0.7-1.0): Full coverage
  
Brush Falloff: 0.0 - 2.0
  0.0: Hard edges
  0.5: Soft natural fade
  1.0+: Very soft, gradual
  
Normalize Colors:
  ✅ Keep enabled!
  Ensures R+G+B+A = 1.0
  Required for proper texture blending
```

### Painting Modes

```
Paint:
  Adds selected channel color
  Increases channel weight
  Default mode
  
Erase:
  Removes selected channel color
  Decreases channel weight
  Sets channel to 0
  
View:
  Visualizes channel as grayscale
  White = full weight
  Black = no weight
  Great for checking coverage
```

### Channel Workflow

```
Example: 4-Texture Terrain

Texture 1 (R channel): Grass
  - Paint open areas
  - General ground cover
  - 60% of terrain
  
Texture 2 (G channel): Dirt
  - Paint paths
  - Worn areas
  - 20% of terrain
  
Texture 3 (B channel): Rock
  - Paint cliffs, slopes
  - Elevation changes
  - 15% of terrain
  
Texture 4 (A channel): Sand/Gravel
  - Paint edges, transitions
  - Special areas
  - 5% of terrain
  
Total coverage: 100% ✅
Normalized automatically!
```

### Painting Tips

```
✅ Start with base texture (R channel)
   Paint entire terrain
   
✅ Add secondary texture (G channel)
   Paint paths, roads, worn areas
   
✅ Add accent textures (B/A channels)
   Details, variations, special areas
   
✅ Use View mode to check coverage
   Switch channels, verify painting
   
✅ Blend at edges
   Lower strength for smooth transitions
   Higher falloff for natural look
   
✅ Save frequently
   Click "Apply Changes" often
   Vertex colors saved to mesh asset
```

### Keyboard Shortcuts

```
While Painting:
  Shift + Drag: Continuous painting
  Ctrl + Z: Undo (use Unity's undo)
  
  [ / ]: Decrease/Increase brush size
  (Configure in painter if needed)
```

---

## 🕳️ Underground Holes

**Path:** `Thelos → Terrain Hole Placer`

### Overview

Create holes in terrain meshes for underground access (metro stations, bunkers, tunnels).

**Features:**
- Circle, rectangle, polygon shapes
- Paint/Erase/Select modes
- Visual gizmo preview
- Snap to grid
- Wall visualization
- Save/load hole placements
- Non-destructive (creates new mesh asset)

### Basic Workflow

```
Step 1: Select Terrain
  Click on terrain mesh in scene
  Inspector shows MeshFilter
  
Step 2: Open Hole Placer
  Thelos → Terrain Hole Placer
  
Step 3: Target Mesh
  Drag terrain GameObject to "Target Mesh" field
  
Step 4: Choose Shape
  Hole Shape: Circle (or Rectangle/Polygon)
  
Step 5: Set Size
  Hole Size: 10m (diameter for circles)
  
Step 6: Paint Mode
  Current Mode: Paint
  
Step 7: Place Holes
  Click in Scene view to place
  Red gizmo shows preview
  
Step 8: Apply Holes
  Click "Apply Holes to Mesh"
  New mesh created: [Original]_WithHoles
  Original preserved!
  
Step 9: Done!
  Terrain has holes
  Underground area accessible ✅
```

### Hole Shapes

#### Circle

```
Best for:
  ✅ Metro station entrances
  ✅ Manholes
  ✅ Circular shafts
  ✅ Round bunker access
  
Settings:
  Hole Size: Diameter in meters
  Example: 10m = 10m diameter circle
```

#### Rectangle

```
Best for:
  ✅ Building entrances
  ✅ Tunnel openings
  ✅ Loading docks
  ✅ Rectangular shafts
  
Settings:
  Width: X dimension
  Length: Z dimension
  Example: 8m × 12m entrance
```

#### Polygon (Advanced)

```
Best for:
  ✅ Irregular shapes
  ✅ Custom access points
  ✅ Complex underground structures
  
Workflow:
  1. Select Polygon mode
  2. Click to place vertices
  3. Double-click to close shape
  4. Hole created in polygon area
```

### Modes

```
Paint:
  Add new holes
  Click to place
  Red gizmo shows preview
  
Erase:
  Remove existing holes
  Click on hole to delete
  Blue gizmo shows erased
  
Select:
  Inspect hole details
  View placement info
  No editing
```

### Visualization

```
Gizmos (Scene View):
  Red Circle/Rectangle: Hole preview
  Yellow Wireframe: Existing holes
  Blue Outline: Wall depth
  
Show Walls:
  ✅ Visualize hole depth
  See underground structure
  Adjust wall height
  
Wall Height:
  Default: 5m underground
  Adjust for deeper holes
```

### Saving & Loading

```
Save Holes:
  Click "Save Holes"
  Saves to EditorPrefs
  Per-mesh storage
  Reload Unity = holes persist
  
Load Holes:
  Automatic on tool open
  Click "Load Holes" to refresh
  
Clear Holes:
  Click "Clear All Holes"
  Removes all placements
  Clears saved data
```

### Apply Process

```
Click "Apply Holes to Mesh":

1. Creates new mesh
   Name: [Original]_WithHoles
   
2. Removes triangles
   Any triangle center inside hole
   Triangle completely removed
   
3. Preserves data
   ✅ Vertex colors maintained
   ✅ UVs maintained
   ✅ Normals recalculated
   
4. Saves asset
   Location: Assets/_Meshes/Terrain/
   Unique name generated
   
5. Updates scene
   MeshFilter → new mesh
   MeshCollider → new mesh
   
6. Original preserved
   Original mesh unchanged
   Can revert anytime!
```

### Underground Setup Tips

```
Step 1: Plan Placement
  Decide entrance locations
  Mark on terrain (use markers)
  
Step 2: Create Holes
  Use appropriate shape
  Size for player + clearance
  10m+ diameter recommended
  
Step 3: Build Underground
  Create rooms, tunnels below
  Align with hole positions
  
Step 4: Add Walls
  Build vertical shaft walls
  Match hole shape
  Connect to underground area
  
Step 5: Add Details
  Ladders, stairs, ramps
  Lighting for entrances
  Collision geometry
  
✅ Complete underground access!
```

### Common Setups

```
Metro Station:
  Shape: Circle
  Size: 12m diameter
  Wall Height: 8m
  Leads to: Underground platform
  
Bunker Entrance:
  Shape: Rectangle
  Size: 6m × 8m
  Wall Height: 5m
  Leads to: Shelter entrance
  
Maintenance Access:
  Shape: Circle
  Size: 3m diameter
  Wall Height: 4m
  Leads to: Service tunnels
  
Tunnel Portal:
  Shape: Rectangle
  Size: 10m × 15m
  Wall Height: 10m
  Leads to: Main tunnel network
```

---

## 🌿 Grass Placement

**Path:** `Thelos → Grass Instance Placer`

### Overview

Place grass prefab instances on terrain with:
- Density control
- Slope filtering
- Random rotation/scale
- Batching optimization
- Parent organization

### Basic Usage

```
Step 1: Prepare Grass Prefab
  Create grass prefab
  Optimized mesh (low poly)
  LOD setup recommended
  
Step 2: Open Placer
  Thelos → Grass Instance Placer
  
Step 3: Select Terrain
  Target Terrain: Drag terrain mesh
  
Step 4: Assign Prefab
  Grass Prefab: Your grass prefab
  
Step 5: Set Density
  Placement Density: 0.5 (instances per sq meter)
  
Step 6: Configure Filters
  Min Slope: 0° (flat)
  Max Slope: 30° (gentle slopes)
  
Step 7: Randomization
  Rotation Randomness: 1.0 (full 360°)
  Scale Variation: 0.2 (±20%)
  
Step 8: Place Grass
  Click "Place Grass Instances"
  Processing... ⏳
  Done! ✅
```

### Settings Guide

```
Placement Density: 0.1 - 2.0
  0.1 - 0.3: Sparse (wasteland)
  0.4 - 0.6: Normal (grassland)
  0.7 - 1.0: Dense (meadow)
  1.0+: Very dense (jungle)
  
Slope Filtering:
  Min Slope: 0° (flat ground)
  Max Slope: 45° (steep hills)
  
  Grass only on slopes within range
  Prevents grass on cliffs
  
Randomization:
  Rotation: 0.0 - 1.0
    0.0 = No rotation
    1.0 = Full 360° random
    
  Scale: 0.0 - 0.5
    0.0 = Uniform size
    0.2 = ±20% variation
    0.5 = ±50% variation
```

### Performance Tips

```
✅ Use LOD groups on grass prefabs
✅ Keep poly count low (50-200 tris)
✅ Enable GPU instancing on material
✅ Use Density 0.3-0.5 for performance
✅ Parent to organized hierarchy
✅ Consider occlusion culling
✅ Use texture atlases

⚠️ Avoid:
❌ High poly grass (1000+ tris)
❌ Too high density (2.0+)
❌ No LODs
❌ Individual GameObjects scattered
```

### Batching Optimization

```
SRP Batcher (URP):
  ✅ Use same material on all grass
  ✅ Enable GPU Instancing
  ✅ Shader Graph compatible
  ✅ Best for many small objects
  
GPU Instancing:
  ✅ Enable on material
  ✅ Works with ProPixelizer
  ✅ Automatic batching
  ✅ Thousands of instances
  
Static Batching:
  ✅ Mark grass parent as static
  ✅ Combine at build time
  ✅ Good for immobile grass
```

---

## 🎨 Shaders & Materials

### Terrain Blend Shader

**Path:** `/Assets/Materials/Shaders/TerrainBlend4Textures.shader`

#### Features

```
✅ 4 texture blending via vertex colors
✅ R/G/B/A channels = 4 textures
✅ ProPixelizer integration
✅ URP compatible
✅ SRP Batcher compatible
✅ Tiling control per texture
```

#### Usage

```
Step 1: Create Material
  Right-click in Project
  Create → Material
  Name: "M_Terrain_MiddleRing"
  
Step 2: Assign Shader
  Shader: Custom/TerrainBlend4TexturesProPixelizer
  
Step 3: Assign Textures
  Texture 1 (R): Grass_Albedo
  Texture 2 (G): Dirt_Albedo
  Texture 3 (B): Rock_Albedo
  Texture 4 (A): Sand_Albedo
  
Step 4: Set Tiling
  Tiling 1: 10 (grass repeats 10× per unit)
  Tiling 2: 10
  Tiling 3: 15 (rock more detail)
  Tiling 4: 8
  
Step 5: Apply to Terrain
  Drag material to terrain mesh
  
✅ Textures blend based on vertex colors!
```

#### Texture Slots

```
Texture 1 (R Channel):
  Base/primary texture
  Usually grass or ground
  Covers majority of terrain
  
Texture 2 (G Channel):
  Secondary texture
  Paths, worn areas
  20-40% coverage
  
Texture 3 (B Channel):
  Accent texture
  Rock, cliffs, special areas
  10-20% coverage
  
Texture 4 (A Channel):
  Detail/variation texture
  Sand, gravel, edge details
  5-10% coverage
```

### ProPixelizer Integration

```
All shaders support ProPixelizer:
  ✅ LUT color grading
  ✅ Dithering
  ✅ Outline support
  ✅ Resolution scaling
  
No extra setup needed!
Works with standard ProPixelizer workflow.
```

### Material Setup Tips

```
Texture Selection:
  ✅ Similar style/theme
  ✅ Matching color palette
  ✅ Consistent resolution
  ✅ ProPixelizer-processed textures
  
Tiling Guidelines:
  Ground textures: 5-15
  Detail textures: 10-20
  Large patterns: 2-5
  
  Adjust to avoid repetition
  Higher = more detail, more repeat
```

---

## 💾 Presets

### Overview

Save and load terrain generator settings instantly.

**Saved Data:**
- Ring type and size mode
- Mesh quality settings
- All noise parameters
- Hills/valleys configuration
- Vertex color mode
- Output options

### Save Preset

```
Step 1: Configure Perfect Settings
  Set up everything exactly right
  Test generation
  Verify results
  
Step 2: Open Presets Section
  Scroll to bottom of generator
  Find "Presets" section
  
Step 3: Save
  Click "💾 Save Preset"
  File dialog opens
  Default: Assets/_TerrainPresets/
  
Step 4: Name It
  Enter: "MiddleRing_Standard_v1"
  Click Save
  
Step 5: Confirmation
  "Preset saved" dialog
  Ready to use! ✅
```

### Load Preset

```
Step 1: Open Generator
  Thelos → Advanced Circular Terrain Generator
  
Step 2: Load
  Click "📂 Load Preset"
  Navigate to preset file
  
Step 3: Select
  Choose: "MiddleRing_Standard_v1.json"
  Click Open
  
Step 4: Settings Restored
  All settings updated instantly
  Ready to generate! ✅
```

### Preset Library

Create a complete library:

```
Standard Presets:
  InnerRing_Standard.json
    196×64, moderate terrain, white colors
    
  MiddleRing_Standard.json
    196×64, gentle terrain, white colors
    
  OuterRing_Standard.json
    128×48, rough terrain, white colors

High Quality Presets:
  MiddleRing_HighQuality.json
    256×80, extra detail
    
  InnerRing_UltraDetail.json
    256×80, maximum quality

Prototype Presets:
  Prototype_Fast.json
    64×24, quick testing
    
  Prototype_Flat.json
    64×24, no noise, flat

Special Purpose:
  OuterRing_Combat.json
    Extra rough for battle areas
    
  MiddleRing_Flat.json
    Minimal variation for buildings
```

### Preset Naming

```
Good Names:
  ✅ [Ring]_[Purpose]_v[Number]
  ✅ MiddleRing_Standard_v1
  ✅ OuterRing_Industrial_v2
  ✅ InnerRing_Reactor_Smooth
  
Bad Names:
  ❌ test
  ❌ terrain
  ❌ final_final
  ❌ asdf123
```

### Managing Presets

```
List All Presets:
  Click "📋 List Presets"
  Shows all saved presets
  Displays key settings
  
Delete Preset:
  Click "🗑️ Delete Preset"
  Select preset file
  Confirm deletion
  ⚠️ Cannot undo!
  
Organize Presets:
  Create subfolders:
    _TerrainPresets/
      Standard/
      HighQuality/
      Prototype/
      Special/
```

---

## 🔄 Workflows

### Complete Terrain Creation

```
Step 1: Plan Ring
  Decide: Inner, Middle, or Outer ring
  Purpose: Residential, industrial, reactor
  Quality needed: Standard or high
  
Step 2: Generate Terrain
  Load or configure preset
  Vertex Color Mode: WHITE ✅
  Generate mesh
  
Step 3: Create Underground (Optional)
  Open Terrain Hole Placer
  Place entrance holes
  Apply to mesh
  Build underground areas
  
Step 4: Paint Textures
  Open Vertex Color Painter
  Paint base texture (R)
  Add paths (G)
  Add rocks/details (B/A)
  Apply changes
  
Step 5: Create Material
  New material
  TerrainBlend4TexturesProPixelizer shader
  Assign 4 textures
  Set tiling values
  Apply to terrain
  
Step 6: Add Grass (Optional)
  Open Grass Instance Placer
  Assign grass prefab
  Set density and slopes
  Place instances
  
Step 7: Final Polish
  Adjust lighting
  Add props/details
  Test underground access
  Optimize performance
  
✅ Complete ring terrain ready! ✅
```

### Quick Prototype Workflow

```
Goal: Fast terrain for testing

Step 1: Load Prototype Preset
  Load: "Prototype_Fast.json"
  
Step 2: Generate
  Click Generate
  Low quality (64×24)
  Fast generation ⚡
  
Step 3: Test Gameplay
  Run game
  Test movement
  Verify layout
  
Step 4: Iterate
  Adjust settings if needed
  Regenerate quickly
  
⏱️ Total time: 2 minutes!
```

### Production Quality Workflow

```
Goal: Final high-quality terrain

Step 1: Configure High Quality
  Segments: 256
  Divisions: 80
  Layered noise
  Careful hill/valley placement
  
Step 2: Generate & Verify
  Generate mesh
  Check quality in scene
  Verify no errors
  
Step 3: Save Preset
  Save as "[Ring]_Final_v1"
  Document settings
  
Step 4: Detailed Painting
  High-detail vertex painting
  Multiple texture layers
  Careful blending
  
Step 5: Material Setup
  High-quality textures
  Fine-tuned tiling
  ProPixelizer settings
  
Step 6: Underground Access
  Carefully placed holes
  Detailed underground
  Proper connections
  
Step 7: Grass & Props
  Optimized grass placement
  Additional props
  Final details
  
✅ Production-ready terrain!
```

### Multi-Ring City Workflow

```
Goal: Complete city with 3 rings

Step 1: Generate Inner Ring
  Load: "InnerRing_Standard"
  Vertex Colors: White
  Generate
  Save scene
  
Step 2: Generate Middle Ring
  Load: "MiddleRing_Standard"
  Align with inner ring boundary
  Generate
  Save scene
  
Step 3: Generate Outer Ring
  Load: "OuterRing_Standard"
  Align with middle ring boundary
  Generate
  Save scene
  
Step 4: Paint Each Ring
  Inner: Tech textures
  Middle: Urban textures
  Outer: Industrial/wasteland textures
  
Step 5: Underground Network
  Place holes in all rings
  Connect with tunnels
  Metro system links rings
  
Step 6: Populate
  Add buildings (procedural or manual)
  Place props and details
  Grass/vegetation per ring
  
✅ Complete city terrain! 🏗️
```

---

## 🔧 Troubleshooting

### Terrain Generation Issues

**"Mesh looks flat/boring"**
```
Problem: Not enough noise variation
Solution:
  ✅ Enable "Use Layered Noise"
  ✅ Increase layer strengths
  ✅ Add more hills/valleys
  ✅ Adjust elevation curve
```

**"Generation is too slow"**
```
Problem: Too high quality settings
Solution:
  ✅ Reduce segments (256→128)
  ✅ Reduce divisions (80→48)
  ✅ Use prototype preset for testing
  ✅ Disable optimization during testing
```

**"Mesh has holes/errors"**
```
Problem: Mesh generation bug
Solution:
  ✅ Check console for errors
  ✅ Verify settings are reasonable
  ✅ Try lower quality first
  ✅ Check Unity version compatibility
```

### Vertex Painting Issues

**"Can't paint on terrain"**
```
Problem: Vertex colors not initialized
Solution:
  ✅ Regenerate with WHITE mode ✅
  ✅ Verify mesh has vertex colors
  ✅ Check mesh is not null
  ✅ Select correct mesh in painter
```

**"Painting doesn't show up"**
```
Problem: Material/shader issue
Solution:
  ✅ Use TerrainBlend4Textures shader
  ✅ Assign textures to material slots
  ✅ Check material is applied to mesh
  ✅ Verify URP is active
```

**"Colors look wrong"**
```
Problem: Not normalized or wrong channel
Solution:
  ✅ Enable "Normalize Colors" ✅
  ✅ Check current channel (R/G/B/A)
  ✅ Use View mode to verify
  ✅ Ensure total weight = 1.0
```

**"Brush too small/large"**
```
Problem: Brush size not configured
Solution:
  ✅ Adjust "Brush Size" slider
  ✅ Try 5-10m for general painting
  ✅ Small (1m) for details
  ✅ Large (20m) for broad strokes
```

### Underground Hole Issues

**"Holes not appearing"**
```
Problem: Not applied to mesh
Solution:
  ✅ Click "Apply Holes to Mesh"
  ✅ Check console for errors
  ✅ Verify hole size is reasonable
  ✅ Ensure holes are placed
```

**"Holes in wrong location"**
```
Problem: Placement or transform issue
Solution:
  ✅ Use gizmos to verify position
  ✅ Check terrain transform is correct
  ✅ Try snap to grid
  ✅ Clear and replace holes
```

**"Mesh looks broken after holes"**
```
Problem: Too many/overlapping holes
Solution:
  ✅ Reduce number of holes
  ✅ Increase spacing between holes
  ✅ Avoid overlap
  ✅ Use reasonable sizes
```

**"Original mesh destroyed"**
```
Problem: Original should be preserved
Solution:
  ✅ Check for "_WithHoles" mesh asset
  ✅ Original in Assets/_Meshes/Terrain/
  ✅ Revert MeshFilter if needed
  ✅ Always keep backups
```

### Grass Placement Issues

**"Grass not placing"**
```
Problem: Settings or prefab issue
Solution:
  ✅ Assign grass prefab
  ✅ Check terrain mesh is valid
  ✅ Verify slope settings
  ✅ Increase density if too sparse
```

**"Too much grass / performance issues"**
```
Problem: Density too high
Solution:
  ✅ Reduce density (0.3-0.5)
  ✅ Add LODs to grass prefab
  ✅ Enable GPU instancing
  ✅ Reduce grass poly count
```

**"Grass on cliffs"**
```
Problem: Slope filtering not set
Solution:
  ✅ Set Max Slope to 30-35°
  ✅ Adjust Min Slope if needed
  ✅ Grass only on valid slopes
```

### Material/Shader Issues

**"Textures not blending"**
```
Problem: Shader or vertex color issue
Solution:
  ✅ Use TerrainBlend4TexturesProPixelizer
  ✅ Verify mesh has vertex colors
  ✅ Check textures are assigned
  ✅ Paint vertex colors properly
```

**"ProPixelizer not working"**
```
Problem: Shader integration or URP setup
Solution:
  ✅ Verify ProPixelizer installed
  ✅ Check URP asset settings
  ✅ Use ProPixelizer-compatible shader
  ✅ Check LUT is assigned
```

**"Textures too stretched/tiled"**
```
Problem: Tiling values
Solution:
  ✅ Adjust tiling per texture
  ✅ Try 5-15 for most textures
  ✅ Higher = more repeat
  ✅ Lower = larger texture scale
```

### Preset Issues

**"Preset won't load"**
```
Problem: File corrupted or wrong format
Solution:
  ✅ Check file is .json
  ✅ Open in text editor, verify format
  ✅ Re-save preset if needed
  ✅ Check for error messages
```

**"Preset loads but settings wrong"**
```
Problem: Version mismatch or corruption
Solution:
  ✅ Re-create preset
  ✅ Verify all fields present
  ✅ Check for Unity console warnings
```

---

## 📊 Quick Reference Tables

### Recommended Settings by Ring

| Ring | Segments | Divisions | Noise | Hills | Valleys | Use Case |
|------|----------|-----------|-------|-------|---------|----------|
| Inner | 196 | 64 | Moderate | 3-5 | 2-3 | Reactor core |
| Middle | 196 | 64 | Gentle | 5-7 | 3-5 | Residential |
| Outer | 128 | 48 | Rough | 8-10 | 5-7 | Industrial |
| Prototype | 64 | 24 | Basic | 2 | 1 | Testing |

### Quality Presets

| Level | Segments | Divisions | Gen Time | Use Case |
|-------|----------|-----------|----------|----------|
| Low | 64 | 24 | 1s | Prototyping |
| Medium | 128 | 48 | 3s | Production |
| High | 196 | 64 | 8s | Important areas |
| Ultra | 256 | 80 | 15s | Hero showcases |

### Vertex Color Channel Usage

| Channel | Typical Texture | Coverage | Priority |
|---------|-----------------|----------|----------|
| R | Grass/Ground | 50-70% | Primary |
| G | Dirt/Paths | 20-30% | Secondary |
| B | Rock/Cliffs | 10-20% | Accent |
| A | Sand/Details | 5-10% | Variation |

### Brush Settings Guide

| Task | Size | Strength | Falloff |
|------|------|----------|---------|
| Base coverage | 15-20m | 0.8-1.0 | 0.3 |
| Path painting | 5-10m | 0.6-0.8 | 0.5 |
| Detail work | 1-3m | 0.3-0.5 | 0.7 |
| Soft blending | 10-15m | 0.2-0.4 | 1.0+ |

---

## 🎯 Best Practices

### Terrain Generation

```
✅ Start with presets, customize if needed
✅ Use WHITE vertex colors for painting
✅ Test with low quality first
✅ Save presets for reuse
✅ Always enable mesh optimization
✅ Generate colliders for gameplay terrain
✅ Save mesh assets for backup
```

### Vertex Painting

```
✅ Paint base texture first (R channel)
✅ Add details progressively (G/B/A)
✅ Keep normalization enabled
✅ Use View mode to check coverage
✅ Save/apply changes frequently
✅ Start with lower strength, build up
✅ Use soft falloff for natural blending
```

### Underground Holes

```
✅ Plan hole locations before placing
✅ Use appropriate hole sizes (8-12m)
✅ Keep holes spaced reasonably apart
✅ Save hole placements
✅ Test hole alignment with underground
✅ Original mesh always preserved
```

### Performance

```
✅ Use LODs on grass and props
✅ Enable GPU instancing on materials
✅ Keep grass poly count low (50-200)
✅ Use occlusion culling
✅ Static batch immobile objects
✅ Optimize mesh quality for distance
✅ Use URP/SRP Batcher
```

### Workflow

```
✅ Build preset library first
✅ Test with prototypes early
✅ Iterate quickly, polish later
✅ Save work frequently
✅ Document custom settings
✅ Keep backups of mesh assets
```

---

## 🎓 Advanced Tips

### Custom Elevation Curves

```
Create interesting terrain shapes:

Bowl Shape:
  Inner (0.0): 0.5 (low center)
  Middle (0.5): 0.7
  Outer (1.0): 1.0 (high edges)
  
Mountain Peak:
  Inner (0.0): 1.0 (high center)
  Middle (0.5): 0.6
  Outer (1.0): 0.3 (low edges)
  
Plateau:
  Inner (0.0-0.7): 1.0 (flat high)
  Edge (0.7-1.0): 0.3 (sharp drop)
```

### Multi-Texture Blending

```
Create rich terrain with 4 textures:

Layer 1 (R - 50%): Base grass
Layer 2 (G - 25%): Worn dirt paths
Layer 3 (B - 15%): Rocky outcrops
Layer 4 (A - 10%): Gravel/sand edges

Paint order:
  1. Full R coverage (grass everywhere)
  2. Paint G on paths (dirt replaces grass)
  3. Paint B on slopes (rock on hills)
  4. Paint A at edges (gravel transitions)
  
Normalization keeps total = 100%!
```

### Underground Network Planning

```
Metro System:
  1. Place main station holes (12m)
  2. Build underground platforms
  3. Create connecting tunnels
  4. Add secondary access (6m holes)
  5. Link all rings together
  
Tip: Draw map first, then place holes!
```

### Procedural Variation

```
Add randomness:
  - Use different random seeds
  - Adjust noise offsets slightly
  - Vary hill placement per generation
  - Small tiling value changes
  
Creates unique but consistent terrain!
```

---

## ✅ Complete Workflow Checklist

### New Ring Terrain

```
☐ Plan ring type and purpose
☐ Load or create preset
☐ Set vertex color mode to WHITE
☐ Configure quality settings
☐ Generate terrain mesh
☐ Verify mesh quality in scene
☐ Save preset if custom settings
☐ (Optional) Place underground holes
☐ (Optional) Apply holes to mesh
☐ Paint base texture (R channel)
☐ Paint secondary textures (G/B/A)
☐ Apply vertex color changes
☐ Create material with shader
☐ Assign 4 textures to material
☐ Set tiling values
☐ Apply material to terrain
☐ (Optional) Place grass instances
☐ Test in play mode
☐ Optimize performance
☐ Save scene
☐ Done! ✅
```

---

## 📁 File Locations

### Tools

```
/Assets/_Scripts/Editor/
  AdvancedCircularTerrainGenerator.cs   (Main generator)
  VertexColorPainter.cs                 (Painter tool)
  TerrainHolePlacer.cs                  (Hole tool)
  GrassInstancePlacer.cs                (Grass tool)
```

### Shaders

```
/Assets/Materials/Shaders/
  TerrainBlend4Textures.shader               (Base shader)
  TerrainBlend4TexturesProPixelizer.shader   (ProPixelizer version)
```

### Assets

```
/Assets/_Meshes/Terrain/
  Generated terrain meshes
  [Name]_WithHoles meshes
  
/Assets/_TerrainPresets/
  Saved preset .json files
  Organized by type/quality
```

---

## 🔗 Related Systems

This terrain system integrates with:
- **ProPixelizer** - Retro rendering
- **URP** - Universal Render Pipeline
- **Input System** - Player controls
- **NavMesh** - AI navigation
- **Cinemachine** - Camera control

---

## 📚 Additional Resources

### Unity Documentation
- Mesh API
- Vertex Colors
- URP Shaders
- Editor Tools

### ProPixelizer
- LUT Color Grading
- Dithering Setup
- Material Configuration

---

**Complete terrain system ready for Thelos! 🎮🏗️**

**Generate → Paint → Populate → Play! ✅**
