# ⚡ Quick Reference: Terrain Workflow

**Fast lookup for the complete terrain creation process.**

---

## 🎯 3-Step Quick Start

```
1. Generate Mesh     → Menu → Thelos → Advanced Circular Terrain Generator
2. Create Material   → Assign shader + 4 textures
3. Paint Textures    → Menu → Thelos → Vertex Color Painter
```

---

## 🏗️ Step 1: Generate Mesh (2 minutes)

### Essential Settings

```
Inner Radius: 50-100       (Hole size)
Outer Radius: 200-500      (Total size)
Segments: 64-128           (Circle smoothness)
Radial Divisions: 32-64    (Detail level)

☑️ Use Vertex Colors       (CRITICAL - enables painting!)
☑️ Generate Collider       (For walkable terrain)
☑️ Optimize Mesh          (Better performance)
```

### Height Variation

```
Hills: 3-8 items
  Height: 10-20
  Radius: 20-40
  Falloff: 2-4

Valleys: 2-6 items
  Depth: -5 to -15 (negative!)
  Radius: 15-35
  Falloff: 2-3
```

---

## 🎨 Step 2: Material Setup (3 minutes)

### Shader & Textures

```
Shader: Thelos/TerrainBlend4TexturesProPixelizer

Texture 1 (Red):   Base/primary (concrete, metal)
Texture 2 (Green): Weathering (rust, decay)
Texture 3 (Blue):  Details (grating, stripes)
Texture 4 (Alpha): Ground (dirt, moss)

For each: Assign texture + normal map + smoothness
```

### Key Properties

```
Tiling: 10-15
Blend Sharpness: 1.5-2.5
Metallic: 0.0
Pixel Size: 4-7
Use Object Position: OFF ❌
```

### Color Grading (Optional)

```
☑️ Use Color Grading LUT
Color Grading LUT: Palette_NES_dither4x4_lookup
```

---

## 🖌️ Step 3: Paint Vertex Colors (10 minutes)

### Painter Setup

```
Menu → Thelos → Vertex Color Painter
Select terrain in scene
☑️ Enable Painting
☑️ Normalize Colors (CRITICAL!)

Brush Size: 5-10 (use [ and ] keys)
Brush Strength: 0.3-0.5
Brush Falloff: 1.5-2.5
```

### Painting Order

```
1. Red Channel:   Fill All with Red (base coat)
2. Green Channel: Paint edges/damage (weathering)
3. Blue Channel:  Paint features (details)
4. Alpha Channel: Paint valleys/corners (ground)
5. Save Mesh Asset! (CRITICAL!)
```

### Painting Strategy

```
Base Layer:    100% Red everywhere
Weathering:    20-50% Green on edges
Details:       30-70% Blue for features
Ground Cover:  10-30% Alpha in low areas

Build up gradually with low strength!
```

---

## 🎮 Channel Reference

```
┌─────────────────────────────────────┐
│ Vertex Color → Texture Mapping     │
├─────────────────────────────────────┤
│ Red (R)   → Texture 1 (Base)        │
│ Green (G) → Texture 2 (Weathering)  │
│ Blue (B)  → Texture 3 (Details)     │
│ Alpha (A) → Texture 4 (Ground)      │
└─────────────────────────────────────┘

Example: (0.6, 0.3, 0.1, 0.0)
= 60% Tex1 + 30% Tex2 + 10% Tex3 + 0% Tex4
```

---

## 🔧 Recommended Settings by Zone

### Inner Ring (Reactor Core)

```
MESH:
  Inner: 0, Outer: 100
  Segments: 64, Divisions: 24
  Hills: 2 (height 8-12)
  Valleys: 1 (depth -5)

MATERIAL:
  Pixel Size: 3-4
  Tiling: 12
  Palette: Palette_256_dither2x2_lookup

PAINTING:
  Base: 80% Red (clean metal)
  Warnings: 15% Blue (radiation markers)
  Decay: 5% Green (minimal)
```

### Middle Ring (Residential)

```
MESH:
  Inner: 100, Outer: 300
  Segments: 96, Divisions: 48
  Hills: 5 (height 10-20)
  Valleys: 4 (depth -8 to -12)

MATERIAL:
  Pixel Size: 5-6
  Tiling: 10
  Palette: Palette_PAL_dither4x4_lookup

PAINTING:
  Base: 60% Red (concrete)
  Decay: 40% Green (heavy rust)
  Paths: 30% Blue (asphalt)
  Nature: 20% Alpha (moss/dirt)
```

### Outer Ring (Wasteland)

```
MESH:
  Inner: 300, Outer: 500
  Segments: 128, Divisions: 64
  Hills: 8 (height 15-25)
  Valleys: 6 (depth -10 to -15)

MATERIAL:
  Pixel Size: 7-8
  Tiling: 8
  Palette: Palette_NES_dither4x4_lookup

PAINTING:
  Base: 30% Red (corroded)
  Decay: 60% Green (extreme rust)
  Details: 20% Blue (old floors)
  Toxic: 30% Alpha (contamination)
```

---

## 🎨 Built-in Palettes Quick Pick

```
Palette_256_dither4x4_lookup     → Most colors, subtle effect
Palette_PAL_dither4x4_lookup     → Retro TV look
Palette_NES_dither4x4_lookup     → Classic 8-bit horror
Palette_GB_dither4x4_lookup      → Monochrome dread
Palette_micro_dither4x4_lookup   → Extreme minimal

Location: /Assets/ProPixelizer/Palettes/
```

---

## ⌨️ Keyboard Shortcuts

### Vertex Color Painter

```
[            Decrease brush size
]            Increase brush size
Shift+Paint  Erase/reduce channel
```

### Scene View

```
F            Frame selected object
Q            Pan tool
W            Move tool
E            Rotate tool
R            Scale tool
```

---

## ⚠️ Critical Reminders

```
☑️ ALWAYS check "Use Vertex Colors" when generating mesh
☑️ ALWAYS check "Normalize Colors" when painting
☑️ ALWAYS click "Save Mesh Asset" before closing Unity
☑️ ALWAYS assign material to GameObject
☑️ ALWAYS use falloff > 1.0 for smooth brushes
```

---

## 🐛 Quick Fixes

### Terrain is black
```
→ Fill All with Red in Vertex Color Painter
→ Save Mesh Asset
```

### Textures don't blend
```
→ Lower Blend Sharpness in material (1.0-2.0)
→ Use lower brush strength when painting (0.3-0.5)
```

### Changes disappeared
```
→ You forgot to Save Mesh Asset!
→ Always save after painting
```

### Pixelation too strong/weak
```
→ Adjust Pixel Size in material
→ Lower = sharper, Higher = more pixelated
```

### Can't see vertex colors
```
→ Check material is assigned
→ Check shader is TerrainBlend4TexturesProPixelizer
→ Check textures are assigned to all 4 slots
```

---

## 📋 Workflow Checklist

```
☐ Generate mesh with vertex colors enabled
☐ Create material with ProPixelizer shader
☐ Assign 4 textures + normals
☐ Set tiling, pixel size, blend sharpness
☐ (Optional) Assign color grading LUT
☐ Assign material to terrain GameObject
☐ Open Vertex Color Painter
☐ Enable painting + normalize colors
☐ Fill All with Red (base coat)
☐ Paint other channels gradually
☐ Save Mesh Asset!
☐ Test in Play mode
☐ Save scene
```

---

## 🚀 Performance Tips

```
✅ Use Optimize Mesh checkbox
✅ Lower divisions on distant rings (32 vs 64)
✅ Reuse materials across similar terrains
✅ Use mipmaps on all textures
✅ Keep pixel size reasonable (4-8)
❌ Don't make segments too high (128 max)
❌ Don't make divisions too high (64 max)
```

---

## 🎯 Painting Tips

```
✅ Start with low strength (0.3-0.5)
✅ Build up gradually
✅ Use soft falloff (1.5-2.5)
✅ Paint in layers (base → weather → detail)
✅ Save frequently
❌ Don't paint with 1.0 strength
❌ Don't forget to normalize
❌ Don't paint too much detail (pixelation hides it)
```

---

## 📊 Typical Values

```
MESH:
  Segments: 64-128 (higher = smoother circle)
  Divisions: 32-64 (higher = more detail)
  Hills: 3-8 items
  Valleys: 2-6 items

MATERIAL:
  Tiling: 8-15 (higher = smaller repeats)
  Blend Sharpness: 1.0-3.0
  Pixel Size: 3-8
  Smoothness: 0.2-0.6

PAINTING:
  Brush Size: 5-15
  Strength: 0.2-0.6 (rarely 1.0!)
  Falloff: 1.5-3.0
```

---

## 🔗 Full Documentation

```
Complete Tutorial:
  /Assets/_Scripts/Editor/TUTORIAL_CompleteTerrainWorkflow.md

ProPixelizer Details:
  /Assets/_Scripts/Editor/README_ProPixelizerIntegration.md

Color Grading:
  /Assets/_Scripts/Editor/README_ColorGradingAndDithering.md

Vertex Painting:
  /Assets/_Scripts/Editor/README_QuickStart_VertexPainting.md
```

---

**Print this for quick reference while working! 📄**
