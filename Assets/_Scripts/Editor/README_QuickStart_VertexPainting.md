# Quick Start: Vertex Color Terrain Blending

## ✅ What You Now Have

1. **Vertex Color Painter** - Tool to paint textures onto terrain
2. **TerrainBlend4Textures Shader** - Blends 4 textures based on vertex colors
3. **Terrain Generator** - Already outputs vertex colors

---

## 🚀 5-Minute Setup

### Step 1: Generate Terrain (2 min)

1. **Menu → Thelos → Advanced Circular Terrain Generator**
2. **Ring Type:** Inner Ring (or any)
3. **Material Zones:** Keep "Use Submeshes" **OFF** (we're using vertex colors instead)
4. **Click:** Generate Terrain Ring
5. **Result:** Terrain mesh with vertex colors already set

---

### Step 2: Create Blending Material (2 min)

1. **Right-click in Project → Create → Material**
2. **Name:** `Mat_TerrainBlend_InnerRing`
3. **Shader:** Select `Thelos/TerrainBlend4Textures`

4. **Assign 4 textures** (use any textures you have, we'll refine later):
   - **Texture 1 (Red):** Metal/concrete texture
   - **Texture 2 (Green):** Grass/vegetation texture
   - **Texture 3 (Blue):** Dirt/ground texture
   - **Texture 4 (Alpha):** Rubble/industrial texture

5. **Set Tiling:** 10-20 (prevents repetition)

6. **Assign material to terrain:**
   - Select terrain GameObject
   - Mesh Renderer → Materials → Drag your material here

**Result:** Terrain now shows blended textures!

---

### Step 3: Paint Vertex Colors (1 min)

1. **Menu → Thelos → Vertex Color Painter**
2. **Target GameObject:** Drag your terrain into the field
3. **Choose a channel** (Red/Green/Blue/Alpha = Texture 1/2/3/4)
4. **Hold SHIFT + Click** in Scene View to paint

**Painting Tips:**
- Start with **Red channel** (Texture 1) - paint core areas
- Switch to **Green channel** (Texture 2) - paint grass areas
- Switch to **Blue channel** (Texture 3) - paint paths/roads
- Switch to **Alpha channel** (Texture 4) - paint industrial zones

**Result:** Textures blend smoothly where you paint!

---

## 🎨 Understanding the System

### Vertex Colors = Texture Weights

```
Each vertex has a color (R, G, B, A):
├── R = 1.0, G = 0, B = 0, A = 0  →  100% Texture 1
├── R = 0.5, G = 0.5, B = 0, A = 0  →  50% Texture 1 + 50% Texture 2
├── R = 0, G = 0, B = 1.0, A = 0  →  100% Texture 3
└── R = 0.25, G = 0.25, B = 0.25, A = 0.25  →  25% each (all 4 textures)
```

### Shader Does the Blending:

```hlsl
finalColor = 
    Texture1 * vertexColor.r +
    Texture2 * vertexColor.g +
    Texture3 * vertexColor.b +
    Texture4 * vertexColor.a;
```

---

## 🔧 Vertex Painter Controls

### Brush Settings:

**Brush Size:**
- Small (1-5m): Detail work, paths, small areas
- Medium (5-15m): General terrain painting
- Large (15-50m): Large zones, broad strokes

**Brush Strength:**
- Low (0.1-0.3): Subtle blending, build up gradually
- Medium (0.4-0.6): Standard painting
- High (0.7-1.0): Quick, hard strokes

**Brush Falloff:**
- Low (0-0.3): Hard edges, distinct boundaries
- Medium (0.4-0.6): Natural transitions
- High (0.7-1.0): Very soft, gradual blending

**Auto Normalize:**
- ✅ Enabled: Keeps R+G+B+A = 1.0 (recommended)
- ❌ Disabled: Allows over-bright areas (advanced)

---

## 📝 Workflow Example: Inner Ring

### Goal: Create lab/reactor aesthetic

**Textures to use:**
1. **Texture 1 (Red):** Metal grating / Reactor floor
2. **Texture 2 (Green):** Clean concrete / Lab floor
3. **Texture 3 (Blue):** Damaged floor / Rust
4. **Texture 4 (Alpha):** Hazard markings / Warning stripes

**Painting Process:**

1. **Start with Red** (reactor core):
   - Paint center area (0-50m radius)
   - Use large brush (20m)
   - High strength (0.8)

2. **Switch to Green** (lab floors):
   - Paint middle ring (50-120m radius)
   - Medium brush (10m)
   - Medium strength (0.6)

3. **Add Blue** (damage/wear):
   - Paint edges of lab areas
   - Small brush (5m)
   - Low strength (0.3) - blend with green

4. **Add Alpha** (hazard markings):
   - Paint specific spots (radiation warnings)
   - Small brush (2m)
   - High strength (0.9)

**Result:** Realistic lab environment with blended textures!

---

## 🎯 ProPixelizer Integration

### Option 1: Camera-Based (Easiest)

1. **Select Main Camera**
2. **Add Component → ProPixelizerCamera**
3. **Pixelisation Method:** Entire Screen
4. **Screen Height:** 360

**Result:** Everything pixelated, including blended terrain!

### Option 2: Per-Material (More control)

**To integrate ProPixelizer into the terrain shader:**
- You'd need to modify the shader or create Shader Graph version
- Use ProPixelizer sub-graphs
- More complex, camera-based is recommended

---

## 💾 Saving Your Work

### Save Painted Mesh:

1. **In Vertex Color Painter window**
2. **Click:** "Save Mesh as Asset"
3. **Choose location:** `/Assets/_Meshes/Terrain/`
4. **Name:** `TerrainMesh_InnerRing_Painted`

**Important:** This saves the vertex colors permanently!

---

## 📊 Performance

### Vertex Color Blending:

```
✅ Pros:
├── 1 draw call (vs 4 with submeshes)
├── Smooth transitions
├── Artistic control
└── FishNet compatible

⚠️ Cons:
├── All 4 textures loaded always (~4-16MB)
└── Slightly more complex shader
```

**For Thelos: This is the BEST approach!**

---

## 🔍 Troubleshooting

**"Vertex colors not showing on terrain"**
- Material shader must be `Thelos/TerrainBlend4Textures`
- Check terrain has MeshFilter component
- Check mesh has vertex colors (should be automatic)

**"Painting doesn't work"**
- Terrain must have MeshCollider (for raycasting)
- Hold SHIFT while clicking
- Check brush size isn't too small

**"Textures look weird/bright"**
- Enable "Auto Normalize" in Vertex Painter
- Or click "Fill Red" to reset and start over

**"Shader compile errors"**
- Unity might be caching old version
- Window → General → Asset Store (force refresh)
- Or restart Unity Editor

**"Textures are too tiled/repetitive"**
- Increase "Tiling" value in material (10 → 20)
- Use higher resolution textures
- Or add variation with detail textures

---

## 🎨 Advanced Tips

### Blend 3 Textures at Once:

Paint vertex color like:
- R = 0.5, G = 0.3, B = 0.2, A = 0

**Result:** Transition area between 3 different textures!

### Create Roads/Paths:

1. Paint **Blue channel** (road texture)
2. Use **low falloff** (0.2) for hard edges
3. Paint along desired path
4. Add **Green channel** (grass) on sides with soft falloff

### Random Variation:

1. Click "Randomize Colors" in Vertex Painter
2. Terrain gets chaotic random blend
3. Repaint areas you want controlled
4. Leaves natural variation elsewhere

---

## 📚 Next Steps

### To Complete Your Terrain:

1. ✅ Generate terrain
2. ✅ Create blending material
3. ✅ Paint vertex colors
4. ⏳ Add ProPixelizer (camera-based)
5. ⏳ Generate grass instances
6. ⏳ Add to SECTR sector
7. ⏳ Wire to FishNet

---

## 🎮 Full Thelos City Workflow

### For All 3 Rings:

**Inner Ring:**
- Generate with terrain tool
- Paint: Metal (R), Concrete (G), Damage (B), Hazard (A)
- Blend material with 4 lab/reactor textures

**Middle Ring:**
- Generate with terrain tool
- Paint: Grass (R), Road (G), Dirt (B), Rubble (A)
- Blend material with 4 urban/residential textures

**Outer Ring:**
- Generate with terrain tool
- Paint: Factory (R), Wasteland (G), Contaminated (B), Gravel (A)
- Blend material with 4 industrial textures

**Result:** Complete circular city with smooth texture transitions!

---

Good luck with your horror game! 🏙️☢️
