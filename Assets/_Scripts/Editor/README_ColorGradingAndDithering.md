# 🎨 ProPixelizer Color Grading & Dithering Guide

## Overview

The **TerrainBlend4TexturesProPixelizer** shader now supports **ProPixelizer's color grading LUT** and **dither patterns**! This allows you to:

- ✅ Apply **retro color palettes** (NES, Game Boy, custom)
- ✅ Add **dithering effects** for smooth color transitions
- ✅ Create **consistent art style** across terrain and objects
- ✅ Match **horror aesthetic** with limited color palettes

---

## 🎯 Quick Start

### 1. Enable Color Grading

In your terrain material:
1. Check **Use Color Grading LUT** ☑️
2. Assign a **Color Grading LUT** texture

### 2. Use Built-in Palettes

ProPixelizer includes several pre-made palettes:

**Location:** `/Assets/ProPixelizer/Palettes/`

**Available Palettes:**
- `Palette_NES_dither4x4_lookup.png` - NES palette with 4x4 dither
- `Palette_GB_dither4x4_lookup.png` - Game Boy palette
- `Palette_256_dither4x4_lookup.png` - 256-color palette
- `Palette_PAL_dither4x4_lookup.png` - PAL system palette
- `Palette_micro_dither4x4_lookup.png` - Micro palette (very limited colors)

**Dither Variations:**
- `*_dither2x2_lookup.png` - Subtle 2x2 dithering
- `*_dither4x4_lookup.png` - Standard 4x4 dithering (recommended)
- `*_lookup.png` - No dither (hard color snapping)

---

## 📖 How It Works

### Color Grading LUT

ProPixelizer uses a **3D Lookup Table (LUT)** to map colors:

```
Input RGB → LUT Lookup → Quantized Output RGB
```

The LUT is a special texture where:
- **Horizontal**: Red and Blue channels
- **Vertical**: Green channel + Dither pattern rows
- **Result**: Limited palette colors

### Dithering

Dithering creates **smooth gradients** using a limited palette by alternating colors in a pattern:

```
Without Dither:  ████ ████ ████
With Dither:     █▓▒░ █▓▒░ █▓▒░
```

The shader uses screen-space position to select dither pattern rows, creating smooth transitions between palette colors.

---

## 🛠️ Creating Custom Palettes

### Method 1: Use ProPixelizer Tools

1. **Create Palette Asset:**
   ```
   Right-click in Project → Create → ProPixelizer → Palette
   ```

2. **Configure Palette:**
   - **Source:** Your palette texture (must be Readable!)
   - **Method:** Choose color matching method:
     - `HSV_Nearest` - Best for artistic palettes
     - `RGB_Nearest` - Best for technical palettes
     - `V_Nearest` - Best for grayscale/value-based

3. **Add Dither Pattern (Optional):**
   - Check **Use Dither Pattern** ☑️
   - Assign a dither pattern from `/Assets/ProPixelizer/Palettes/DitherPatterns/`
   
   **Available Patterns:**
   - `ordered4x4.asset` - Standard Bayer matrix (recommended)
   - `ordered2x2.asset` - Subtle dithering
   - `ProP_Arcane.asset` - Stylized arcane pattern
   - `ProP_Hatch.asset` - Hatching effect
   - `ProP_Temple.asset` - Temple pattern

4. **Generate LUT:**
   - In the Palette Inspector, click **Generate**
   - This creates a `*_LUT.png` texture

5. **Use in Material:**
   - Assign the generated `*_LUT.png` to **Color Grading LUT**

### Method 2: Use Existing LUTs

Just drag any `*_lookup.png` file from `/Assets/ProPixelizer/Palettes/` directly into your material's **Color Grading LUT** slot!

---

## 🎨 Recommended Palettes for Thelos

### Horror Atmosphere

**Option 1: Dark & Grainy (NES-style)**
```
LUT: Palette_NES_dither4x4_lookup.png
Effect: Limited colors, heavy dithering, retro horror
Best For: Outer ring (industrial wasteland)
```

**Option 2: Monochrome Dread (Game Boy)**
```
LUT: Palette_GB_dither4x4_lookup.png
Effect: 4-color grayscale, classic horror
Best For: Underground areas, fog zones
```

**Option 3: Minimal Color (Micro)**
```
LUT: Palette_micro_dither4x4_lookup.png
Effect: Very limited palette, high contrast
Best For: Reactor core, anomaly zones
```

### Zone-Specific Recommendations

**Inner Ring (Reactor Core):**
- Palette: `Palette_256_dither2x2_lookup.png`
- Pixel Size: 3-4
- Reason: More colors for important area, subtle dither

**Middle Ring (Residential):**
- Palette: `Palette_PAL_dither4x4_lookup.png`
- Pixel Size: 4-6
- Reason: Retro TV aesthetic, medium dither

**Outer Ring (Industrial):**
- Palette: `Palette_NES_dither4x4_lookup.png`
- Pixel Size: 6-8
- Reason: Heavy degradation, strong retro feel

---

## 🔧 Material Setup Example

### Complete Material Configuration

```
Shader: Thelos/TerrainBlend4TexturesProPixelizer

[Textures]
Texture 1 (Red): Concrete_BaseColor
Texture 2 (Green): Rust_BaseColor
Texture 3 (Blue): Metal_BaseColor
Texture 4 (Alpha): Dirt_BaseColor

[Tiling & Blending]
Tiling: 12
Blend Sharpness: 2.0

[Lighting]
Metallic: 0.0
Smoothness 1-4: 0.3-0.5

[ProPixelizer]
Pixel Size: 5
Use Object Position: OFF ❌
Alpha Clip Threshold: 0.5

[Color Grading]
Use Color Grading LUT: ON ☑️
Color Grading LUT: Palette_NES_dither4x4_lookup
```

---

## ⚙️ Technical Details

### LUT Texture Format

ProPixelizer LUTs are **256 x 256** textures:
- **Width:** 256 pixels (16 × 16 cells for RGB)
- **Height:** 256 pixels (16 dither rows × 16 green levels)
- **Format:** sRGB (gamma-corrected)

### How the Shader Uses LUT

1. **Convert to Gamma Space** (if linear)
2. **Quantize RGB** to 16 levels each
3. **Calculate Dither Offset** from screen position
4. **Sample LUT** at `(R,B,dither_row,G)`
5. **Output** quantized color

### Performance

- **Cost:** ~2-3 texture samples per fragment
- **Impact:** Minimal (~0.1ms on mid-range GPU)
- **SRP Batcher:** ✅ Still compatible!

---

## 🎭 Combining with Vertex Color Blending

You can paint textures AND apply color grading:

**Workflow:**
1. Generate terrain mesh
2. Paint vertex colors (4 textures)
3. Assign material with ProPixelizer shader
4. Enable color grading
5. Result: Blended textures + retro palette!

**Example:**
```
Vertex Paint: Concrete (R) + Rust (G) at seams
Color Grading: NES palette with dither
Final Look: Smooth texture blend, quantized to NES colors, dithered transitions
```

---

## 🐛 Troubleshooting

### "LUT doesn't work / no effect"

**Solution 1:** Make sure **Use Color Grading LUT** is checked ☑️

**Solution 2:** Verify the LUT texture is assigned (not empty)

**Solution 3:** Check the LUT is marked as **sRGB** in import settings

### "Dither pattern looks wrong"

**Cause:** Pixel Size affects dither pattern alignment

**Solution:** Use Pixel Size that's a **multiple of 2 or 4** (e.g., 4, 6, 8)

### "Colors are too dark / too bright"

**Cause:** LUT might be for different color space

**Solution:** Try a different palette or adjust **Smoothness** values

### "Dither is too subtle / too strong"

**Solution:** Switch between dither variants:
- Too subtle? Use `*_dither4x4_lookup.png`
- Too strong? Use `*_dither2x2_lookup.png`
- Want none? Use `*_lookup.png`

---

## 📚 Advanced: Creating Dither Patterns

### Create Custom Dither Pattern

1. **Create Asset:**
   ```
   Right-click → Create → ProPixelizer → Dither Pattern
   ```

2. **Configure Pattern:**
   - Edit the **4x4 matrix** values (0.0 - 1.0)
   - Lower values = darker threshold
   - Higher values = lighter threshold

3. **Use in Palette:**
   - Assign to **Dither Pattern** in your Palette asset
   - Regenerate the LUT

### Example: Ordered 4x4 Bayer Matrix

```
 0/16   8/16   2/16  10/16
12/16   4/16  14/16   6/16
 3/16  11/16   1/16   9/16
15/16   7/16  13/16   5/16
```

This creates the classic "screen door" dithering effect.

---

## 🎯 Best Practices

### For Horror Atmosphere

1. **Use limited palettes** (8-32 colors)
2. **Enable dithering** for unsettling texture
3. **Combine with fog** for depth
4. **Match all materials** to same palette

### For Performance

1. **Reuse LUT textures** across materials
2. **Use lower resolution LUTs** if needed (128x128)
3. **Disable color grading** on distant objects (LOD)

### For Consistency

1. **Pick ONE palette** per zone
2. **Use same dither pattern** across zone
3. **Test in different lighting** conditions

---

## 📦 What's Included

**New Shader Features:**
- `USE_COLOR_GRADING` toggle
- `_ColorGradingLUT` texture property
- Inline color grading functions
- Dither UV calculation

**Compatible With:**
- ✅ Vertex color blending (4 textures)
- ✅ Per-object pixelation
- ✅ PBR lighting (normals, smoothness)
- ✅ SRP Batcher
- ✅ FishNet/SECTR streaming

---

## 🚀 Next Steps

1. **Experiment** with built-in palettes
2. **Create** custom palettes for your game
3. **Apply** to grass materials for consistency
4. **Test** different dither patterns
5. **Combine** with post-processing for final look

---

**Your terrain now has:**
- ✅ Per-object pixelation
- ✅ Vertex color blending
- ✅ Color grading LUT
- ✅ Dithering support
- ✅ Full PBR lighting

**Complete retro horror terrain system!** ☢️🎨👾
