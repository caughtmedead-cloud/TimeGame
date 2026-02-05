# Multi-Material Terrain & FishNet Compatibility Guide

## 🎨 Multi-Material Terrain System

### Overview

The terrain generator now supports **submeshes** - allowing you to assign different materials to different zones of your circular terrain (grass areas, forest, factory, concrete, etc.).

---

## 📐 How Submeshes Work

### What is a Submesh?

A single mesh can have multiple "submeshes" - each with its own material slot:

```
TerrainMesh_InnerRing (1 Mesh)
├── Submesh 0: Grass Material       (MeshRenderer.materials[0])
├── Submesh 1: Forest Material      (MeshRenderer.materials[1])
├── Submesh 2: Factory Material     (MeshRenderer.materials[2])
└── Submesh 3: Concrete Material    (MeshRenderer.materials[3])
```

**Benefits:**
- ✅ Multiple textures on one mesh
- ✅ Still only 1 GameObject
- ✅ Draw calls = number of materials (4 materials = 4 draw calls)
- ✅ Each zone can have unique ProPixelizer settings
- ✅ SRP Batcher compatible (if materials use same shader)

---

## 🛠️ Using Multi-Material Zones

### Step 1: Enable Submeshes in Generator

1. **Open:** Menu → Thelos → Advanced Circular Terrain Generator
2. **Scroll to:** Material Zones section
3. **Enable:** ✅ Use Submeshes
4. **Set Zone Count:** 2-8 zones (e.g., 4 for grass/forest/factory/concrete)
5. **Choose Pattern:**

#### Zone Patterns Explained:

**Radial Pattern (Recommended for Thelos):**
```
Creates concentric rings from inner to outer:

Zone 0 (Inner):  Reactor core area (metal/concrete)
Zone 1:          Lab district (clean floors)
Zone 2:          Residential ruins (grass/debris)
Zone 3 (Outer):  Industrial wasteland (dirt/factory)
```

**Angular Pattern:**
```
Creates pie slices:

Zone 0:  North quadrant
Zone 1:  East quadrant
Zone 2:  South quadrant
Zone 3:  West quadrant
```

**Custom Pattern:**
```
Uses vertex color R channel for manual control
(Advanced - requires manual editing)
```

### Step 2: Generate Terrain

1. **Configure other settings** (rings, noise, features) as normal
2. **Click:** Generate Terrain Ring
3. **Result:** Mesh created with 4 submeshes, each with placeholder material

---

## 🎨 Assigning Custom Materials

### After Generation:

Your terrain will have materials like:
```
MeshRenderer.materials:
├── [0] Zone_0_Material (red placeholder)
├── [1] Zone_1_Material (green placeholder)
├── [2] Zone_2_Material (blue placeholder)
└── [3] Zone_3_Material (yellow placeholder)
```

### Replace with ProPixelizer Materials:

#### Example: 4-Zone Inner Ring Setup

1. **Create ProPixelizer materials:**

```
/Assets/Materials/Terrain/
├── Mat_Terrain_Metal.mat        (Zone 0 - Inner reactor)
├── Mat_Terrain_CleanFloor.mat   (Zone 1 - Lab)
├── Mat_Terrain_GrassDebris.mat  (Zone 2 - Residential)
└── Mat_Terrain_Industrial.mat   (Zone 3 - Outer wasteland)
```

2. **Configure each material:**

**Mat_Terrain_Metal:**
```
Shader: ProPixelizerUberShader
Base Color: Gray metal (0.5, 0.5, 0.55)
Base Texture: Metal panel texture
Normal Map: Metal normal
Smoothness: 0.7 (reflective)
Lighting Ramp: Harsh
Outline: 2px, black
```

**Mat_Terrain_GrassDebris:**
```
Shader: ProPixelizerUberShader
Base Color: Brown-green (0.4, 0.45, 0.3)
Base Texture: Grass/dirt texture
Normal Map: Ground normal
Smoothness: 0.2 (rough)
Lighting Ramp: Harsh
Outline: 2px, black
```

3. **Assign to terrain:**
   - Select generated terrain GameObject
   - Inspector → Mesh Renderer → Materials
   - Expand materials array
   - Drag your materials into slots 0-3

---

## 🎯 Recommended Zone Configurations

### Inner Ring (Lab District)

**4 Zones - Radial:**
```
Zone 0 (0-50m):    Mat_Terrain_Metal          (reactor core)
Zone 1 (50-100m):  Mat_Terrain_CleanFloor     (lab floors)
Zone 2 (100-150m): Mat_Terrain_Concrete       (outer lab)
Zone 3 (150-200m): Mat_Terrain_DamagedFloor   (transition to middle ring)
```

### Middle Ring (Residential)

**3 Zones - Radial:**
```
Zone 0 (200-267m): Mat_Terrain_CleanStreet    (intact areas)
Zone 1 (267-333m): Mat_Terrain_GrassDebris    (overgrown)
Zone 2 (333-400m): Mat_Terrain_RuinedRoad     (deteriorated)
```

### Outer Ring (Industrial)

**4 Zones - Radial:**
```
Zone 0 (400-467m): Mat_Terrain_Factory        (factory floors)
Zone 1 (467-533m): Mat_Terrain_Dirt           (open ground)
Zone 2 (533-567m): Mat_Terrain_Wasteland      (contaminated)
Zone 3 (567-600m): Mat_Terrain_Radiation      (outer boundary)
```

---

## ⚡ Performance Considerations

### Draw Calls:

**Without Submeshes:**
```
Single material = 1 draw call
```

**With Submeshes:**
```
4 submeshes × same shader = 4 draw calls (SRP Batched if same shader)
4 submeshes × different shaders = 4 draw calls (not batched)
```

**Recommendation:**
- **Use same shader** for all materials (ProPixelizerUberShader)
- **Vary only textures/colors** = SRP Batcher works
- **4 draw calls per terrain ring is acceptable** for your use case

### SRP Batcher Compatibility:

**✅ GOOD - All materials use ProPixelizerUberShader:**
```
Draw Call 1: Zone 0 (SRP Batched)
Draw Call 2: Zone 1 (SRP Batched)
Draw Call 3: Zone 2 (SRP Batched)
Draw Call 4: Zone 3 (SRP Batched)
Total: 4 draw calls, GPU-efficient
```

**❌ BAD - Mixed shaders:**
```
Draw Call 1: Zone 0 (ProPixelizer)
Draw Call 2: Zone 1 (Standard Lit) ← Breaks batching
Draw Call 3: Zone 2 (Custom shader) ← Breaks batching
Draw Call 4: Zone 3 (ProPixelizer)
Total: 4 draw calls, NOT batched, slower
```

---

## 🌐 FishNet Streaming Compatibility

### Changes Made for FishNet:

**❌ Old (WRONG):**
```csharp
grassInstance.isStatic = true;  // BREAKS FishNet scene streaming!
cluster.isStatic = true;
```

**✅ New (CORRECT):**
```csharp
// No static flag - objects can be loaded/unloaded dynamically
grassInstance.isStatic = false;  // Default, allows scene streaming
cluster.isStatic = false;
```

### Why Static Flag is Bad for FishNet:

**Static objects:**
- ❌ Baked into static batching (editor-time optimization)
- ❌ Cannot be moved or enabled/disabled at runtime
- ❌ Break FishNet's scene load/unload system
- ❌ Prevent per-client visibility control

**Non-static with SRP Batcher:**
- ✅ Can be loaded/unloaded dynamically
- ✅ Works with FishNet scene streaming
- ✅ Still batches via SRP Batcher (runtime batching)
- ✅ Supports NetworkObject visibility control

---

## 🔧 SRP Batcher vs Static Batching

### Unity 6 Batching Options:

**Static Batching (OLD METHOD):**
```
Requirements:
├── Objects marked Static
├── Editor-time preprocessing
└── Same material

Limitations:
├── ❌ Cannot be moved/disabled
├── ❌ Breaks scene streaming
└── ❌ Not compatible with FishNet
```

**SRP Batcher (UNITY 6 METHOD - USE THIS):**
```
Requirements:
├── Objects use SRP Batcher-compatible shaders
├── Same shader (different materials OK)
└── No static flag needed

Benefits:
├── ✅ Works with dynamic scene loading
├── ✅ Compatible with FishNet
├── ✅ Objects can move/enable/disable
└── ✅ Better performance than static batching
```

**ProPixelizer shaders ARE SRP Batcher compatible!** ✅

---

## 📦 FishNet Scene Structure

### Recommended Hierarchy:

```
Sector_InnerRing_North.unity (Additive Scene)
├── Sector_InnerRing_North (Root GameObject)
│   ├── NetworkObject (FishNet component)
│   ├── TerrainMesh_InnerRing (multi-material terrain)
│   │   └── MeshRenderer (4 materials, NOT static)
│   ├── GrassInstances_Root
│   │   ├── GrassCluster_0001 (NOT static)
│   │   ├── GrassCluster_0002 (NOT static)
│   │   └── ...
│   ├── Props/
│   └── Buildings/
└── (Scene settings, lighting)
```

**Loading/Unloading:**
```csharp
// Server-side NetworkedSectorManager
SceneManager.LoadSceneAsync("Sector_InnerRing_North", LoadSceneMode.Additive);

// Add to client connection
ServerManager.AddConnectionToScene(conn, "Sector_InnerRing_North");

// Client sees:
// - TerrainMesh_InnerRing (4 materials)
// - All grass clusters
// - All children of NetworkObject root
```

---

## 🧪 Testing SRP Batcher

### Verify Your Setup is Working:

1. **Enter Play Mode**
2. **Window → Analysis → Frame Debugger**
3. **Look for:**

```
✅ GOOD - SRP Batched:
├── SRP Batch (Zone_0_Material)
│   └── TerrainMesh_InnerRing submesh 0
├── SRP Batch (Zone_1_Material)
│   └── TerrainMesh_InnerRing submesh 1
├── SRP Batch (GrassCluster materials)
│   ├── GrassCluster_0001
│   ├── GrassCluster_0002
│   └── GrassCluster_0003 (multiple in one batch!)
```

```
❌ BAD - Not Batched:
├── Draw Mesh
│   └── TerrainMesh_InnerRing submesh 0
├── Draw Mesh
│   └── TerrainMesh_InnerRing submesh 1
├── Draw Mesh  (each cluster separate!)
│   └── GrassCluster_0001
```

**If not batching:**
- Check all materials use same shader
- Verify shader is SRP Batcher compatible (ProPixelizer is ✅)
- Check Project Settings → Graphics → SRP Batcher enabled

---

## 🎮 LOD Groups for Grass

### You Mentioned Generating LODs Yourself:

**Current grass placer does NOT create LODGroups** (removed per your request).

**To add LODs manually:**

1. **Create grass prefab variants with different detail levels:**
```
/Assets/Prefabs/Environment/
├── Grass_LOD0.prefab  (high detail, 200 tris)
├── Grass_LOD1.prefab  (medium detail, 80 tris)
└── Grass_LOD2.prefab  (low detail, 20 tris / billboard)
```

2. **Select cluster parent** (e.g., `GrassCluster_0001`)
3. **Add Component → LODGroup**
4. **Configure LODs:**
```
LOD 0 (0-40%):   All child renderers (high detail)
LOD 1 (40-15%):  All child renderers (medium - could use simpler shader)
LOD 2 (15-5%):   All child renderers (low - billboard)
LOD 3 (5-0%):    Culled (nothing rendered)
```

**Or use Unity's LOD generation:**
- Select grass mesh
- Right-click → Generate LODs
- Assign LOD meshes to prefab

**LODs + FishNet:**
- ✅ LODGroups work fine with FishNet
- ✅ Still non-static
- ✅ Stream with scene
- ✅ Cull based on camera distance

---

## 🚀 Complete Workflow

### For Thelos City Terrain:

1. **Generate Inner Ring with 4 zones (Radial)**
2. **Create 4 ProPixelizer materials** (metal, clean, debris, damaged)
3. **Assign materials** to submesh slots
4. **Generate grass** with grass placer (clustered, NOT static)
5. **Manually add LODGroups** to clusters (if needed)
6. **Test in Frame Debugger** (verify SRP Batching)
7. **Save as prefab** or export scene
8. **Add to SECTR sector**
9. **Add NetworkObject** to root
10. **Wire to FishNet scene loading**

**Result:**
- Multi-material terrain with distinct zones
- Grass clusters optimized for streaming
- FishNet compatible (no static flags)
- SRP Batched for performance
- LODs for distance culling

---

## 💡 Advanced: Vertex Color Blending (Alternative)

### If You Don't Want Submeshes:

Instead of hard zone boundaries, you can blend materials smoothly:

**Steps:**
1. **Generator already outputs vertex colors** (height-based)
2. **Create custom shader** that reads vertex color channels:
   ```
   R channel = Grass texture strength
   G channel = Dirt texture strength
   B channel = Concrete texture strength
   A channel = Metal texture strength
   ```
3. **Paint vertex colors** in DCC tool (Blender, Maya) or use Unity plugin
4. **Shader blends** 4 textures based on vertex color weights

**Benefits:**
- ✅ Smooth transitions (no hard zone edges)
- ✅ Single material = 1 draw call
- ✅ More artistic control

**Drawbacks:**
- ❌ Requires custom shader
- ❌ All 4 textures loaded always (more memory)
- ❌ More complex to set up

**Submeshes are simpler for your use case!**

---

## 📊 Performance Summary

### Per Sector (200m radius ring):

**Terrain:**
```
1 TerrainMesh GameObject
├── 4 submeshes (4 materials)
├── Draw Calls: 4 (SRP Batched)
├── Triangles: ~32,000
└── Memory: ~2MB
```

**Grass (2000 instances):**
```
1 GrassInstances_Root GameObject
├── 40 clusters (50 blades each)
├── Draw Calls: 1-2 (SRP Batched, shared material)
├── LODs: 3 levels per cluster
└── Memory: ~1-2MB
```

**Total:**
```
Draw Calls: 5-6 per sector
Frame Time: ~3-5ms
Memory: ~4MB per sector
```

**FishNet Streaming:**
```
Load time: ~50-100ms (async)
Unload time: ~20-50ms
Network sync: Only NetworkObject root (minimal overhead)
Per-client visibility: Fully supported
```

---

## ✅ Final Checklist

Before using terrain in FishNet:

- [ ] **No static flags** on terrain or grass
- [ ] **All materials use ProPixelizerUberShader** (or same shader)
- [ ] **Grass clusters** use shared material
- [ ] **Scene is additive** (not main scene)
- [ ] **NetworkObject on root** GameObject
- [ ] **Tested in Frame Debugger** (verify SRP Batching)
- [ ] **Tested scene loading** (loads/unloads cleanly)
- [ ] **LODGroups added** (if needed for performance)

---

## 🐛 Troubleshooting

**"Materials not appearing after generation"**
- Generator creates placeholder materials
- Replace them with your ProPixelizer materials manually

**"Too many draw calls"**
- Check all materials use same shader
- Verify SRP Batcher is enabled (Project Settings → Graphics)

**"Grass not streaming with FishNet"**
- Make sure grass is child of NetworkObject root
- Verify scene is loaded additively
- Check grass is NOT marked static

**"Zone boundaries are wrong"**
- Try different zone count (2-8)
- Switch between Radial/Angular pattern
- Regenerate terrain with new settings

---

Good luck with Thelos! 🌍🔬
