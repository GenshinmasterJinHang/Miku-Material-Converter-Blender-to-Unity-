# B2U NPR Shader Architecture — Design Spec

- **Date:** 2026-07-20
- **Topic:** Refactor B2U (Blender-to-Unity) shader conversion pipeline to support full NPR (Non-Photorealistic Rendering) feature set
- **Status:** Approved — pending implementation plan

---

## 1. Problem Statement

The current B2U shader pipeline emits only a **simplified template** that multiplies base color by raw lighting. This causes Yvonne (and other Goo-Blender-sourced characters) to render in-engine with visible defects:

- Hard **wireframe/grid patterns** in the clothing region, present in BaseColor but never smoothed by the shader
- **No color remapping** — flat material appearance instead of soft NPR cloth tones
- **No transparency control** — semi-transparent pink sleeves in the reference cannot be reproduced
- **No SSS / anisotropic / multi-layer specular** — skin, hair, and fabric deviation from the Cycles reference

A second, deeper defect: the system has **no way to express NPR intent** in its intermediate representation (`.mgir`), so the codegen has no signal to emit richer shaders.

The reference image (Cycles render of the Goo Blender file) and the current URP render diverge pixel-by-pixel. We need a converter that can express and emit the full NPR feature set so that downstream URP renders match the Cycles reference under strict numeric measurement (ΔE2000).

---

## 2. Goals and Non-Goals

### Goals

1. Augment `.mgir` (material graph IR) schema to **v2** with a first-class `nprFeatures` field that encodes the Goo Blender node graph's NPR intent.
2. Add six **shared HLSL Include files** under `com.migr.shaderconverter/Runtime/NPR/` that each implement one NPR feature (mask, color remap, alpha, SSS, anisotropic spec, multi-layer spec).
3. Update the B2U codegen (`B2UEndfieldMaterialWriter.cs` and analogues) to **route per material type** (Body/Hair/Cloth/Face/Eye) and **conditionally `#include`** the relevant NPR chunks based on `nprFeatures`.
4. Update the Goo-side export (`b2u_mvp_blender/generic_toon_bake.py`) to **probe the Blender node graph** for NPR-relevant nodes and emit corresponding `nprFeatures` entries.
5. Add a **ΔE2000-based validation pipeline** (Python + Pillow) that compares Unity-rendered PNGs against Cycles reference PNGs and rejects material changes whose mean ΔE2000 exceeds the threshold.
6. Maintain **backward compatibility**: existing `.mgir v1` assets (no `nprFeatures` field) must continue to work via the simplified template path.

### Non-Goals (this iteration)

- Building a unified parametric shader (one mega-shader handling all material types). Out of scope — per material-type templates remain.
- Auto-baking NPR features that the source Blender file does not encode. (e.g., synthesizing SSS from non-SSS materials)
- Real-time editor preview parity with Cycles. We compare PNGs against Cycles, not interactive parity.
- HDRP / Built-in pipeline support. URP only.
- Multi-frame ΔE evaluation. Single still-frame per material is sufficient for v1.

---

## 3. Architecture Overview

```
┌──────────────────────┐     ┌──────────────────┐     ┌──────────────────────┐
│ Goo Blender .blend   │ ──► │  B2U exporter    │ ──► │ .mgir v2             │
│ (Materials, Nodes,   │     │  (Python)        │     │ (with nprFeatures)   │
│  Textures)           │     │                  │     │                      │
└──────────────────────┘     └──────────────────┘     └─────────┬────────────┘
                                                                │
            ┌───────────────────────────────────────────────────┼─────────────────────┐
            │                                                   │                     │
            ▼                                                   ▼                     ▼
   ┌────────────────────┐                          ┌──────────────────────┐  ┌────────────────────┐
   │ Endfield_Body.sh   │                          │ Endfield_Hair.sh     │  │ ... (Cloth/Face/  │
   │ #include NPR_*     │                          │ #include NPR_*       │  │      Eye/Skin)     │
   └─────────┬──────────┘                          └──────────┬───────────┘  └────────────────────┘
             │                                                  │
             ▼                                                  ▼
   ┌─────────────────────────────────────────────────────────────────────────────┐
   │ Shared HLSL Include Library (Runtime/NPR/)                                  │
   │   NPR_MaskLayer.hlsl       NPR_ColorRemap.hlsl                              │
   │   NPR_AlphaBlend.hlsl      NPR_SkinSSS.hlsl                                │
   │   NPR_AnisoSpec.hlsl       NPR_MultiLayerSpec.hlsl                         │
   └─────────────────────────────────────────────────────────────────────────────┘
                                                                 │
                                                                 ▼
                ┌─────────────────────────┐    ┌──────────────────────────┐
                │ Unity RenderTarget / PNG│ ─► │ Python ΔE2000 comparison │
                │ (in-engine render)      │    │ vs Cycles reference PNG  │
                └─────────────────────────┘    └──────────────────────────┘
```

The pipeline answers four questions:

1. **What NPR features does this material use?** — `nprFeatures` field in `.mgir v2`.
2. **How is each feature implemented on the GPU?** — Shared `NPR_*.hlsl` files.
3. **How is the right template selected?** — Existing `EndfieldPreset` (and analogues) routes by material type.
4. **How is correctness validated?** — ΔE2000 between URP render and Cycles reference.

---

## 4. Components

### 4.1 `.mgir v2` schema

- **Location:** `schemas/mgir_v2.json` (new)
- **Change:** Add `schemaVersion: 2` and `nprFeatures` object. v1 files (`schemaVersion` absent or `1`) keep working as before.

```json
{
  "schemaVersion": 2,
  "materialName": "M_actor_yvonne_cloth_01",
  "preset": "Endfield",
  "materialType": "Cloth",
  "baseColorTexture": "T_actor_yvonne_cloth_01_D.png",
  "normalTexture": "T_actor_yvonne_cloth_01_N.png",
  "nprFeatures": {
    "mask": {
      "enabled": true,
      "maskTexture": "T_actor_yvonne_cloth_05_RS.png",
      "channels": ["R", "G", "B", "A"]
    },
    "colorRemap": {
      "enabled": true,
      "rampTexture": "TPLK_actor_common_cloth_03_RD.png",
      "maskChannel": "R"
    },
    "alpha": {
      "enabled": true,
      "alphaSource": "maskTexture.G",
      "blendMode": "Transparent",
      "sortPriority": 200
    },
    "sss":         { "enabled": false },
    "anisoSpec":   { "enabled": false },
    "multiSpec":   { "enabled": false }
  }
}
```

### 4.2 Shared HLSL Include Library

- **Location:** `unity/Packages/com.migr.shaderconverter/Runtime/NPR/*.hlsl` (new)
- **Files (one per feature):**

| File | Inputs | Output |
|---|---|---|
| `NPR_MaskLayer.hlsl` | BaseColor tex, Mask tex (RGBA), channel selector per role | Color from chosen channel |
| `NPR_ColorRemap.hlsl` | Source color, Ramp 1D LUT, mask | Color remapped through LUT |
| `NPR_AlphaBlend.hlsl` | Lit color, alpha, blendMode | Final composited color (transparent / fade / cutout) |
| `NPR_SkinSSS.hlsl` | BaseColor, normal, lit color, SSS tint, SSS strength | Color with screen-space translucency approximation |
| `NPR_AnisoSpec.hlsl` | Tangent, bitangent, normal, light, specular tint | Anisotropic GGX/Dual-anisotropic spec |
| `NPR_MultiLayerSpec.hlsl` | BaseColor, Ramp LUT, spec lobe1, lobe2 | Two-step ramp-mapped spec output |

- Each file defines a single entry-point function with a stable signature; templates stitch them together.
- All feature parameters are exposed as **MaterialProperties** (no shader keywords / multi_compile).

### 4.3 Codegen routing

- **Location:** `unity/Packages/com.migr.shaderconverter/Editor/B2UEndfieldMaterialWriter.cs` (and `B2UGenericToonMaterialWriter.cs`, `B2UGenshinMaterialWriter.cs` if needed)
- **Change:** Read `.mgir v2`'s `nprFeatures`. For each enabled feature, emit:
  - The `#include "../NPR/NPR_<Feature>.hlsl"` line
  - A call to the feature's entry function in the lit pipeline
  - The MaterialProperties block with defaults wired from `nprFeatures`
- **Compat:** If `nprFeatures` missing, emit no NPR chunks (preserves today's behavior).

### 4.4 Goo-side exporter

- **Location:** `b2u_mvp_blender/generic_toon_bake.py` (modify)
- **Change:** Walk the Goo Blender node graph. For each material:
  - Look for mask channel assignments (separate mask textures or in-BaseColor masked regions) → `nprFeatures.mask.enabled`
  - Look for color ramp / LUT nodes → `nprFeatures.colorRemap.enabled`
  - Look for transparency nodes → `nprFeatures.alpha.enabled`
  - Look for SSS / translucent shader nodes → `nprFeatures.sss.enabled`
  - Look for anisotropic / hair-specific nodes → `nprFeatures.anisoSpec.enabled`
  - Look for layered spec / ramp spec nodes → `nprFeatures.multiSpec.enabled`
- These features live behind a uniform feature-detection interface — adding a new Goo node type requires only one Python function update.

### 4.5 ΔE2000 validation pipeline

- **Location:** `tools/delta_e_tool/` (new directory)
- **Components:**
  - `compare.py` — given a URP-rendered PNG and a Cycles reference PNG, compute per-pixel CIEDE2000 in Lab space. Emits a heatmap PNG and a JSON summary (mean, p95, p99, max).
  - `batch.py` — iterate over all materials under a directory, run `compare.py`, produce a summary table.
  - `references/yvonne/<material>.png` — Cycles reference PNGs (to be produced by the user before validation runs).
- **Tooling:** Python 3.10+, Pillow, numpy. No GPU dependency.

### 4.6 Test Harness

- **Location:** `tests/test_npr_features.py` (new)
- **Coverage:**
  - Schema validation: every test `.mgir` in the test fixture parses against `schemas/mgir_v2.json`.
  - Codegen snapshot: given fixture `.mgir`, the emitted `.shader` text matches the recorded snapshot.
  - ΔE smoke: for any material with a Cycles reference, render via Unity MCP and run `compare.py`. **Mean ΔE2000 < 3.0** required for the test to pass; **p99 ΔE2000 < 8.0** for tail control.

---

## 5. Data Flow

End-to-end for a single material (e.g., Yvonne cloth):

1. User opens `伊冯.blend` in Goo Blender (`D:\Goo Engine 4.2\Goo-Engine 4.2\blender.exe`).
2. `B2U` add-on runs `generic_toon_bake.py`:
   - Reads node graph for `M_actor_yvonne_cloth_01`.
   - Detects mask, colorRemap, alpha features.
   - Emits `.mgir` with `schemaVersion: 2` and `nprFeatures` populated.
3. Unity Editor's `B2UBundleImporter` reads `.mgir`, calls `B2UEndfieldMaterialWriter`:
   - Selects `Endfield_Cloth.shader` template based on `materialType`.
   - For each enabled `nprFeatures` entry, emits the corresponding `#include` and callsite.
   - Material's Properties default values come from `.mgir`'s `nprFeatures` block.
4. Unity renders the character into a RenderTexture (via a fixed test camera + lighting).
5. PNG is exported.
6. `tools/delta_e_tool/compare.py` is invoked against `references/yvonne/<material>.png`.
7. Test passes if mean ΔE2000 < 3.0 and p99 < 8.0; otherwise the heatmap pinpoints misaligned pixels.

---

## 6. Failure Modes and Error Handling

| Failure | Detection | Handling |
|---|---|---|
| `.mgir` missing `nprFeatures` | Codegen sees `schemaVersion == 2` but no `nprFeatures` | Log warning, codegen proceeds with all features disabled |
| `.nprFeatures.X.enabled` but no texture ref | Schema validator | **Hard fail** at codegen — refuse to generate the material, write `Library/B2UImporter/{mat}.log` |
| Cycles reference PNG missing | `compare.py` finds no file | Skip ΔE validation for that material; surface in CI summary as "no reference" |
| ΔE2000 exceeds threshold | `compare.py` numeric test | Test fail, save heatmap PNG to `tests/artifacts/`, summary in CI log |
| User enables contradictory features (e.g., Cloth with `sss`) | Schema validator (cross-field rule) | Warning, not error — user must explicitly opt in |
| Codegen produces invalid HLSL | Unity Editor import error | Test fails at smoke render step; codegen-written shader is checked into a snapshot for review |

---

## 7. Testing Strategy

Four-layer pyramid. **Top of the pyramid is the only acceptance criterion**; lower layers are guard rails.

| Layer | Tool | Acceptance |
|---|---|---|
| **L1 Schema** | `jsonschema` validator | 100% of fixture `.mgir` files conform |
| **L2 Codegen snapshot** | pytest + Pillow text diff | Generated `.shader` matches snapshot for each fixture |
| **L3 Unity smoke** | Unity MCP via `mcp__unity__*` tools | GPU compiles & renders without errors |
| **L4 ΔE2000** | Python/Pillow + CIEDE2000 | mean < 3.0, p99 < 8.0 against Cycles reference |

ΔE2000 thresholds (CIEDE2000 in Lab space, IT8.7/1 standard):
- **< 1.0** — imperceptible
- **1.0 – 3.0** — acceptable, used as the **acceptance target**
- **3.0 – 5.0** — visible on close inspection; flagged as regression if newly introduced
- **5.0 – 8.0** — clearly wrong tail (p99 threshold); must drop to < 8.0
- **> 8.0** — material rejected; iteration required

---

## 8. Open Questions Deferred

These are intentionally **out of scope** for this design but identified during brainstorming:

- Per-platform shader variant strategy (mobile vs. desktop URP) — defer until DX12 features are required
- Time-of-day / dynamic-lighting NPR adaptation — likely a future feature
- SSS via render pipeline buffer (Thin Film / Translucency Volume) — currently approximated in fragment shader
- Live editor preview parity with Cycles — N/A for v1 (Cycles off-line reference is the target)

---

## 9. Acceptance Criteria (this design)

This design is **approved** when the user confirms the spec file matches their intent. Implementation begins only after the user reviews this file.

After implementation, this design is **met** when:
1. Six `NPR_*.hlsl` files exist in `Runtime/NPR/` and each renders correctly in isolation (smoke).
2. `schemas/mgir_v2.json` exists and validates ≥ 5 Yvonne materials.
3. `B2UBundleImporter` consumes `.mgir v2` end-to-end without human fixup.
4. At least three Yvonne materials (one Body, one Cloth, one Hair/Outline) render at ΔE2000 **mean < 3.0 AND p99 < 8.0** against Cycles reference.
5. `tests/test_npr_features.py` runs in CI and gates merge.

---

## 10. References

- `blender5_shader_nodes_to_unity6_urp_mapping.md` — project-level shader node mapping spec
- `unity/Packages/com.migr.shaderconverter/Runtime/Endfield/` — current shader templates
- `陈_佩丽卡_莱万汀_伊冯_1.2.1_GooBlender4.2+(请阅读RM)_ By 新杨XIYAG/` — Yvonne Goo Blender source
- `Generated/B2U_Materials/*/M_actor_yvonne_*.mgir` — current export IR for Yvonne
- `outputs/yvonne_blender_saved_camera.png` — Cycles reference render (existing)
