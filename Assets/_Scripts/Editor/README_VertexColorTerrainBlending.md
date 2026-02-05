# Vertex Color Terrain Blending Guide

## 🎨 How Vertex Color Blending Works

Instead of using multiple submeshes with different materials, use **ONE material** that blends **4 textures** based on vertex color channels:

```
Vertex Color Channels → Texture Weights:
├── Red (R):    Texture 1 weight (0.0 - 1.0)
├── Green (G):  Texture 2 weight (0.0 - 1.0)
├── Blue (B):   Texture 3 weight (0.0 - 1.0)
└── Alpha (A):  Texture 4 weight (0.0 - 1.0)

Example vertex color (0.5, 0.3, 0.2, 0.0):
├── 50% Texture 1 (Red)
├── 30% Texture 2 (Green)
├── 20% Texture 3 (Blue)
└── 0% Texture 4 (Alpha)
```

---

## 🛠️ Complete Setup Workflow

### Step 1: Generate Terrain with Vertex Colors

The terrain generator already outputs vertex colors (height-based by default). You can use the **Vertex Color Painter** to customize them.

1. **Menu → Thelos → Advanced Circular Terrain Generator**
2. Generate terrain as usual
3. Result: Mesh with vertex colors already assigned

---

### Step 2: Paint Vertex Colors (Optional)

Use the included Vertex Color Painter to customize texture zones:

1. **Menu → Thelos → Vertex Color Painter**
2. **Target GameObject:** Drag your terrain mesh into the field
3. **Paint Channel:** Choose Red/Green/Blue/Alpha (each = one texture)
4. **Brush Settings:**
   - Brush Size: 5-10m (for terrain)
   - Brush Strength: 0.5 (build up gradually)
   - Auto Normalize: ✅ (keeps R+G+B+A = 1.0)
5. **Hold SHIFT + Click** in Scene View to paint

**Painting Strategy:**
```
Red Channel (Texture 1):    Paint reactor core areas
Green Channel (Texture 2):  Paint grass/vegetation areas
Blue Channel (Texture 3):   Paint concrete/urban areas
Alpha Channel (Texture 4):  Paint industrial/dirt areas
```

---

### Step 3: Create Terrain Blending Shader (Shader Graph)

#### Option A: Use Built-in URP Lit with Vertex Colors (Simple)

**Quick Test Material:**
1. Create Material → Shader: `Universal Render Pipeline/Lit`
2. This shader automatically displays vertex colors
3. Good for testing, but doesn't blend textures

#### Option B: Create Custom Shader Graph (Recommended)

Since you have ProPixelizer, create a Shader Graph that:
1. Samples 4 textures
2. Blends them based on vertex color
3. Applies ProPixelizer effect

**I'll create this shader graph for you below!**

---

## 🎨 Creating the Terrain Blending Shader Graph

### Manual Creation Steps:

1. **Right-click in Project → Create → Shader Graph → URP → Lit Shader Graph**
2. **Name:** `TerrainBlend4TexturesProPixelizer`

### Graph Structure:

**Properties (Textures):**
```
Texture1 (Texture2D) - "Texture 1 (Red Channel)"
Texture2 (Texture2D) - "Texture 2 (Green Channel)"
Texture3 (Texture2D) - "Texture 3 (Blue Channel)"
Texture4 (Texture2D) - "Texture 4 (Alpha Channel)"

Tiling (Vector2) - "Texture Tiling" (default: 10, 10)

Normal1 (Texture2D) - "Normal Map 1"
Normal2 (Texture2D) - "Normal Map 2"
Normal3 (Texture2D) - "Normal Map 3"
Normal4 (Texture2D) - "Normal Map 4"

Smoothness1 (Float) - "Smoothness 1" (default: 0.5)
Smoothness2 (Float) - "Smoothness 2" (default: 0.5)
Smoothness3 (Float) - "Smoothness 3" (default: 0.5)
Smoothness4 (Float) - "Smoothness 4" (default: 0.5)
```

**Graph Logic:**

```
[Vertex Color Node]
├── R → [Sample Texture1] → R weight
├── G → [Sample Texture2] → G weight
├── B → [Sample Texture3] → B weight
└── A → [Sample Texture4] → A weight

[UV Node] × [Tiling Property] → UV input for all Sample Texture nodes

Blend:
├── Texture1 × R → Result1
├── Texture2 × G → Result2
├── Texture3 × B → Result3
├── Texture4 × A → Result4
└── Result1 + Result2 + Result3 + Result4 → Final Albedo

Same for Normal Maps and Smoothness
```

---

## 📝 Shader Graph Code Template (For Reference)

Since Shader Graph is visual, here's the **HLSL code equivalent** (for understanding):

```hlsl
// Sample all 4 textures
float4 tex1 = SAMPLE_TEXTURE2D(Texture1, UV * Tiling);
float4 tex2 = SAMPLE_TEXTURE2D(Texture2, UV * Tiling);
float4 tex3 = SAMPLE_TEXTURE2D(Texture3, UV * Tiling);
float4 tex4 = SAMPLE_TEXTURE2D(Texture4, UV * Tiling);

// Get vertex color weights
float4 weights = IN.VertexColor; // (R, G, B, A)

// Blend textures based on weights
float4 finalColor = 
    tex1 * weights.r +
    tex2 * weights.g +
    tex3 * weights.b +
    tex4 * weights.a;

// Normalize weights (optional, if not done in vertex colors)
float totalWeight = weights.r + weights.g + weights.b + weights.a;
finalColor /= totalWeight;

// Output to albedo
Albedo = finalColor.rgb;
```

---

## 🚀 Alternative: HLSL Custom Function (Faster Setup)

If you don't want to create the full Shader Graph manually, I can create a **simple unlit shader** for you that does the blending. Would you like that?

Or, you can use this **Custom Function node** in Shader Graph:

**Create Custom Function Node:**

Name: `BlendFourTextures`

**Inputs:**
- Texture1-4 (Texture2D)
- UV (Vector2)
- Tiling (Vector2)
- VertexColor (Vector4)

**Output:**
- OutColor (Vector4)

**Code:**
```hlsl
void BlendFourTextures_float(
    Texture2D Texture1, SamplerState Sampler1,
    Texture2D Texture2, SamplerState Sampler2,
    Texture2D Texture3, SamplerState Sampler3,
    Texture2D Texture4, SamplerState Sampler4,
    float2 UV, float2 Tiling, float4 VertexColor,
    out float4 OutColor)
{
    float2 tiledUV = UV * Tiling;
    
    float4 tex1 = SAMPLE_TEXTURE2D(Texture1, Sampler1, tiledUV);
    float4 tex2 = SAMPLE_TEXTURE2D(Texture2, Sampler2, tiledUV);
    float4 tex3 = SAMPLE_TEXTURE2D(Texture3, Sampler3, tiledUV);
    float4 tex4 = SAMPLE_TEXTURE2D(Texture4, Sampler4, tiledUV);
    
    OutColor = 
        tex1 * VertexColor.r +
        tex2 * VertexColor.g +
        tex3 * VertexColor.b +
        tex4 * VertexColor.a;
}
```

---

## 🎯 ProPixelizer Integration

### Option 1: Apply ProPixelizer at Camera Level (Recommended)

**Easiest approach:**
1. Use standard terrain blending shader (no ProPixelizer)
2. Add `ProPixelizerCamera` component to Main Camera
3. Set Pixelisation Method: **Entire Screen**
4. Everything gets pixelated, including blended terrain

**Benefits:**
- ✅ Simpler shader
- ✅ Consistent pixelation across all objects
- ✅ Less work

### Option 2: ProPixelizer Shader Graph Sub-Graph

ProPixelizer includes Shader Graph sub-graphs you can use:

**Check:** `/Assets/ProPixelizer/ShaderGraph/SubGraphs/`

**Look for sub-graphs like:**
- `ProPixelizerEffect.shadersubgraph`
- `OutlineEffect.shadersubgraph`

**Add to your terrain shader:**
1. After blending the 4 textures
2. Pass result through ProPixelizer sub-graph
3. Outputs pixelated + outlined result

**I'd need to inspect ProPixelizer's sub-graphs to give exact instructions.**

---

## 📊 Performance Comparison

### Submeshes (Old Method):

```
Draw Calls: 4 (one per material)
Texture Memory: Only loaded textures used per zone
Shader Complexity: Low (4 simple shaders)
```

### Vertex Color Blending (New Method):

```
Draw Calls: 1 (single material!)
Texture Memory: All 4 textures loaded always
Shader Complexity: Medium (blending logic)
```

**For your use case (terrain rings), vertex blending is MUCH better:**
- **1 draw call vs 4 = 4× fewer draw calls!**
- Texture memory is negligible for 4 textures
- Smooth transitions look better

---

## 🎨 Example Material Setup

### Textures You'll Need:

**For Inner Ring (Reactor):**
```
Texture 1 (Red):    Metal panels / Grid floor
Texture 2 (Green):  Clean concrete
Texture 3 (Blue):   Damaged floor / Rust
Texture 4 (Alpha):  Warning stripes / Hazard markings
```

**For Middle Ring (Residential):**
```
Texture 1 (Red):    Grass / Overgrown vegetation
Texture 2 (Green):  Cracked asphalt / Road
Texture 3 (Blue):   Dirt / Mud
Texture 4 (Alpha):  Rubble / Debris
```

**For Outer Ring (Industrial):**
```
Texture 1 (Red):    Factory floor / Metal grating
Texture 2 (Green):  Dirt / Wasteland
Texture 3 (Blue):   Contaminated ground / Radiation
Texture 4 (Alpha):  Gravel / Industrial waste
```

### Material Settings:

**Create Material:**
1. Shader: `Your Custom TerrainBlend4Textures` shader
2. Assign 4 base textures
3. Assign 4 normal maps (optional)
4. Set tiling: 10-20 (prevents repetition)
5. Set smoothness per texture

**Assign to terrain:**
- MeshRenderer → Material → Your blending material
- **Result: 1 draw call for entire terrain ring!**

---

## 🖌️ Painting Workflow

### Recommended Approach:

1. **Start with procedural vertex colors** (terrain generator outputs)
2. **Refine with Vertex Color Painter:**
   - Paint grass areas (Green channel)
   - Paint roads/paths (Blue channel)
   - Paint industrial zones (Alpha channel)
   - Leave reactor core as-is (Red channel)

3. **Preview in real-time** (vertex color preview in editor)
4. **Adjust shader** (change textures, tiling, smoothness)
5. **Save mesh** when happy with result

### Painting Tips:

**For realistic terrain:**
- Use **low brush strength** (0.2-0.5) and build up gradually
- Use **high falloff** (0.7-1.0) for soft transitions
- **Enable Auto Normalize** to prevent over-bright areas
- Paint in **layers** (rough areas first, details later)

**For Thelos horror aesthetic:**
- Sharp transitions between zones (lower falloff)
- Irregular patterns (paint chaotically)
- Mix channels (some areas have 2-3 textures blended)
- Use randomize for initial chaos, then refine

---

## 🔧 Troubleshooting

**"Vertex colors not showing"**
- Check mesh has vertex colors (should be automatic)
- Verify material shader reads vertex colors
- Try "Fill Red" in Vertex Painter to test

**"Textures look blurry"**
- Increase tiling value (10 → 20)
- Use higher resolution textures
- Enable trilinear filtering

**"Painting not working"**
- Make sure GameObject has MeshCollider (for raycasting)
- Hold SHIFT while clicking
- Check brush size is appropriate

**"Colors don't sum to 1.0"**
- Enable "Auto Normalize" in Vertex Painter
- Or manually click "Utilities → Clear All" then repaint

**"ProPixelizer not working"**
- Use camera-based pixelation (easier)
- Or check ProPixelizer sub-graph integration

---

## 🚀 Next Steps

### To Complete the Setup:

1. ✅ **Vertex Color Painter** (already created above)
2. ⏳ **Terrain Blending Shader Graph** (I can create this for you)
3. ⏳ **Example textures/materials** (I can set these up)
4. ⏳ **Updated terrain generator** (auto-set better vertex colors)

**Would you like me to:**

1. 🎨 **Create the complete Shader Graph** for you?
   - I'll create a working `.shadergraph` file
   - With 4 texture blending
   - ProPixelizer compatible

2. 📝 **Create a simple HLSL shader** instead?
   - Faster setup
   - Works immediately
   - Easier to customize

3. 🔍 **Inspect your ProPixelizer sub-graphs**?
   - See what's available
   - Integrate into custom shader
   - Give exact instructions

Let me know which approach you prefer!

---

## 📚 Summary

**Vertex Color Blending = BEST solution for Thelos:**

✅ **1 draw call per terrain ring** (vs 4 with submeshes)
✅ **Smooth texture transitions** (no hard edges)
✅ **Artistic control** (paint textures where you want)
✅ **FishNet compatible** (single material, streamable)
✅ **SRP Batcher friendly** (one shader, batches with grass)
✅ **ProPixelizer compatible** (camera-based or shader-based)

**Workflow:**
1. Generate terrain (already has vertex colors)
2. Paint vertex colors (Vertex Color Painter tool)
3. Create blending shader (Shader Graph or HLSL)
4. Assign textures and material
5. Done!

---

Perfect for your horror game aesthetic! 🏙️☢️
