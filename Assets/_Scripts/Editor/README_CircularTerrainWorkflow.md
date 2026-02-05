# Circular Terrain Generator - Quick Start Guide

## 📦 What You Have

Two powerful editor tools for creating Thelos circular city terrain:

1. **Advanced Circular Terrain Generator** - Creates mesh-based circular terrain rings
2. **Grass Instance Placer** - Places optimized grass instances on terrain

---

## 🎯 Quick Workflow

### Step 1: Generate Terrain Ring (5 minutes)

1. **Open the generator:**
   - Menu → Thelos → Advanced Circular Terrain Generator

2. **Select ring type:**
   - **Inner Ring**: 0-200m (Lab District)
   - **Middle Ring**: 200-400m (Residential)
   - **Outer Ring**: 400-600m (Industrial)
   - **Custom**: Set your own radii

3. **Adjust mesh quality:**
   - Segments: 128 (good balance)
   - Radial Divisions: 32 (good detail)
   - Higher = more detail, more vertices

4. **Configure noise layers:**
   - Enable "Use Layered Noise"
   - Large Features: Hills/mountains (Scale: 0.02, Strength: 15)
   - Medium Features: Slopes (Scale: 0.05, Strength: 8)
   - Small Details: Surface variation (Scale: 0.15, Strength: 2)

5. **Add elevation:**
   - Enable "Add Radial Elevation"
   - Use curve to control inner vs outer height
   - Example: Inner elevated (lab), outer lowered (wasteland)

6. **Add features:**
   - Hills: Random elevated areas (5-10 hills)
   - Valleys: Depressions/craters (3-5 valleys)

7. **Click "Generate Terrain Ring"**

**Result:** Circular mesh terrain saved to `/Assets/_Meshes/Terrain/`

---

### Step 2: Apply ProPixelizer Material (2 minutes)

1. **Create ProPixelizer material:**
   - Right-click → Create → Material
   - Name: `Mat_Terrain_ProPixelizer`
   - Shader: `Shader Graphs/ProPixelizerUberShader`

2. **Configure material:**
   ```
   Base Color: Gray-brown (0.4, 0.35, 0.3)
   Smoothness: 0.2
   Lighting Ramp: ProPixelizer/Ramps/LightingRamp_harsh.png
   Outline Width: 2 pixels
   Outline Color: Black
   ```

3. **Assign to terrain:**
   - Select generated terrain GameObject
   - Mesh Renderer → Materials → Assign your ProPixelizer material

---

### Step 3: Place Grass Instances (5 minutes)

1. **Create simple grass mesh:**
   - Use a quad or import grass asset
   - Make it a prefab

2. **Create grass material:**
   - Right-click → Create → Material
   - Name: `Mat_Grass_ProPixelizer`
   - Shader: `Shader Graphs/ProPixelizerUberShader`
   - Color: Green-gray
   - **Important: ONE material for ALL grass = SRP Batching!**

3. **Open grass placer:**
   - Menu → Thelos → Grass Instance Placer

4. **Configure placement:**
   - Grass Prefab: Your grass mesh prefab
   - Shared Material: `Mat_Grass_ProPixelizer`
   - Grass Count: 1000-5000 (start small)
   - Area Radius: 50m
   - Ground Layer: Select "Ground" or "Default"

5. **Enable clustering:**
   - ✅ Cluster Grass
   - Blades Per Cluster: 50
   - ✅ Add LOD Groups

6. **Click "Place Grass Instances"**

**Result:** Grass placed in clusters, optimized for SRP Batcher and SECTR streaming

---

## 🎨 ProPixelizer Setup

### Option A: Per-Material (What you just did)
- Terrain has ProPixelizer material
- Grass has ProPixelizer material
- Props have ProPixelizer materials
- Only these objects get pixelated

### Option B: Entire Screen (Recommended)
- Terrain can use ANY material
- Add `ProPixelizerCamera` to Main Camera
- Set Pixelisation Method: **Entire Screen**
- Everything gets pixelated (unified look)

**For Thelos, use Option B!** It's easier and creates consistent horror aesthetic.

---

## 📊 Performance Tips

### ✅ DO:
- Use ONE material per grass type (enables SRP Batching)
- Cluster grass in groups of 50-100 blades
- Add LOD Groups to clusters
- Mark grass objects as Static
- Use mesh terrain instead of Unity Terrain for flexibility

### ❌ DON'T:
- Don't use Unity Terrain Details system (breaks SRP Batcher)
- Don't give each grass blade unique material (kills batching)
- Don't place grass individually without clusters (bad culling)
- Don't forget mesh colliders on terrain

---

## 🔧 Customization

### Terrain Ring Variations

**Inner Ring (Lab District):**
```
Base Height: 10m
Large Features: Low (ruins/debris)
Radial Elevation: Elevated inner (curve starting high)
Hills: 3-5 (reactor debris)
Valleys: 2-3 (impact craters from anomaly)
```

**Middle Ring (Residential):**
```
Base Height: 5m
Large Features: Moderate (ruined buildings)
Radial Elevation: Gentle slope
Hills: 5-8 (building foundations)
Valleys: 3-4 (collapsed areas)
```

**Outer Ring (Industrial):**
```
Base Height: 2m
Large Features: High (factories, warehouses)
Radial Elevation: Lower outer edge (wasteland)
Hills: 8-12 (industrial structures)
Valleys: 5-8 (quarries, pits)
```

---

## 🌬️ Adding Wind (Future Enhancement)

Grass currently doesn't have wind. To add:

1. Create custom Shader Graph based on ProPixelizer
2. Add vertex displacement in vertex shader
3. Use global wind parameters
4. Maintain SRP Batcher compatibility

I can create this shader if you need wind animation!

---

## 🗺️ SECTR Integration

### Organizing for Streaming

```
Sector_InnerRing_North/
├── TerrainMesh_InnerRing (mesh terrain)
├── GrassInstances_Root (parent)
│   ├── GrassCluster_0001
│   ├── GrassCluster_0002
│   └── ...
└── NetworkObject (for FishNet)
```

**Benefits:**
- Entire sector loads/unloads as one unit
- Grass clusters cull based on camera distance
- NetworkObject streams with sector
- All grass shares material = SRP Batched

---

## 🐛 Troubleshooting

**"Grass not appearing"**
- Check Ground Layer matches terrain layer
- Increase Area Radius
- Make sure prefab has MeshRenderer

**"Too many vertices warning"**
- Reduce Segments or Radial Divisions
- Script auto-switches to 32-bit index format if needed

**"Grass not batching"**
- Make sure ALL grass uses same material
- Check Frame Debugger: Window → Analysis → Frame Debugger
- Look for "SRP Batch" in draw call list

**"Mesh asset not saving"**
- Check `/Assets/_Meshes/Terrain/` folder exists
- Script creates it automatically if missing

---

## 📁 File Locations

- **Scripts:** `/Assets/_Scripts/Editor/`
- **Generated Meshes:** `/Assets/_Meshes/Terrain/`
- **Materials:** `/Assets/_Materials/` (create this folder)
- **Prefabs:** `/Assets/_Prefabs/` (per your project rules)

---

## 🚀 Next Steps

1. Generate all 3 rings (Inner, Middle, Outer)
2. Apply ProPixelizer materials OR use camera-based pixelation
3. Place grass/detail meshes
4. Test in Play mode
5. Check performance in Profiler
6. Organize into SECTR sectors
7. Add NetworkObject components for FishNet

---

## 💡 Advanced Usage

### Multiple Terrain Textures

Create multiple ProPixelizer materials with different textures:
- `Mat_Terrain_Dirt` (outer wasteland)
- `Mat_Terrain_Concrete` (middle urban)
- `Mat_Terrain_Metal` (inner lab)

Manually paint/assign to different areas of mesh terrain.

### Vertex Color Painting

Mesh has vertex colors (height-based by default).

You can use these in custom shaders for:
- Texture blending
- Wetness maps
- Damage/wear
- Vegetation masks

---

Good luck building Thelos! 🏙️
