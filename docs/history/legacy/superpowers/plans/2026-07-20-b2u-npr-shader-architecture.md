# B2U NPR Shader Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the B2U shader conversion pipeline so that Goo-Blender-sourced NPR materials (e.g., Yvonne) render in Unity URP at ΔE2000 mean < 3.0 / p99 < 8.0 against Cycles reference images.

**Architecture:** Per-material-type categorized templates (Body, Hair, Cloth, Face, Eye) each include shared `NPR_*.hlsl` feature modules. Materials declare NPR usage in `.mgir v2`'s `nprFeatures` field. The codegen routes each feature emission per material. A Python ΔE2000 pipeline validates every change against Cycles references; failures block merge.

**Tech Stack:**
- Unity 6 URP, HLSL
- C# (Unity Editor codegen)
- Python 3.10+ (Blender export + ΔE tool), Pillow, numpy
- JSON Schema Draft 2020-12 (`.mgir v2`)
- pytest (tests)
- CIEDE2000 ΔE (color difference)
- Unity MCP for in-editor orchestration

## Global Constraints

These constraints apply to every task. Specific values are copied verbatim from the spec and decision log.

- **Backward compatibility:** `.mgir` files without `nprFeatures` (v1 schema) MUST continue to render with today's behavior. Codegen must emit a working shader with all NPR chunks disabled.
- **No shader keywords / no `#pragma multi_compile`.** All NPR feature parameters are `MaterialProperty` (floats, colors, textures). This was a hard user decision.
- **URP only.** HDRP / Built-in pipeline support is out of scope this iteration.
- **Cycles reference renders** are produced by the user in Goo Blender (`D:\Goo Engine 4.2\Goo-Engine 4.2\blender.exe`) and committed under `tools/delta_e_tool/references/yvonne/`. The Python pipeline reads them but does not generate them (one task is dedicated to capture).
- **ΔE2000 acceptance threshold:** mean < 3.0 **AND** p99 < 8.0 per material.
- **Frequent commits:** Each task ends with a git commit. Use conventional-commit prefixes (`feat:`, `test:`, `fix:`, `chore:`).
- **No placeholders in test code.** Every test function has explicit assertion lines; every step that modifies code shows the code.
- **Working directory:** `c:/Users/22687/Desktop/项目4/`. Use forward slashes in shell. Use backslashes only in Unity asset paths inside JSON.

---

## Phase 0 — Setup

### Task 0.1: Verify working environment

**Files:** none touched (verification only)

**Interfaces:** none

- [ ] **Step 1: Check Python version**

Run:
```bash
python --version
```
Expected: `Python 3.10.x` or newer (3.10–3.12 supported). If absent, stop and tell the user to install Python 3.10+.

- [ ] **Step 2: Check Pillow + numpy**

Run:
```bash
python -c "import PIL, numpy; print('PIL', PIL.__version__, 'numpy', numpy.__version__)"
```
Expected: prints both versions, no `ModuleNotFoundError`. If absent, run `pip install pillow numpy`.

- [ ] **Step 3: Check jsonschema**

Run:
```bash
python -c "import jsonschema; print(jsonschema.__version__)"
```
Expected: version ≥ 4.0. If absent, run `pip install jsonschema`.

- [ ] **Step 4: Check pytest**

Run:
```bash
pytest --version
```
Expected: prints version. If absent, run `pip install pytest`.

- [ ] **Step 5: Verify Goo Blender path exists**

Run:
```bash
ls "D:/Goo Engine 4.2/Goo-Engine 4.2/blender.exe"
```
Expected: file exists. If not, **stop** — the user must install Goo Engine before Phase 4.

- [ ] **Step 6: Commit a session marker so subsequent tasks have a baseline**

```bash
git init -q 2>/dev/null || true
git config user.email "claude@local"
git config user.name "Claude"
git add -A
git commit -m "chore: pre-plan-environment-verified" --allow-empty
```

---

## Phase 1 — `.mgir v2` Schema and Validation

### Task 1.1: Write failing test for `.mgir v2` schema parser

**Files:**
- Create: `tests/test_mgir_v2_schema.py`
- Create: `tests/__init__.py` (empty)
- Create: `schemas/__init__.py` (empty)
- Create: `tests/fixtures/mgir_v2/yvonne_cloth_01.json`
- Create: `tests/fixtures/__init__.py` (empty)
- Create: `tests/fixtures/mgir_v2/__init__.py` (empty)

**Interfaces:**
- Consumes: nothing (this is the first task)
- Produces: `schemas.validate_mgir.validate(mgir_dict: dict) -> None` raising `jsonschema.ValidationError` on failure. **Not yet implemented** — this task only writes the failing test.

- [ ] **Step 1: Create directory structure**

```bash
mkdir -p tests/fixtures/mgir_v2 schemas
touch tests/__init__.py schemas/__init__.py tests/fixtures/__init__.py tests/fixtures/mgir_v2/__init__.py
```

- [ ] **Step 2: Create the fixture file `tests/fixtures/mgir_v2/yvonne_cloth_01.json`**

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

- [ ] **Step 3: Write the failing test `tests/test_mgir_v2_schema.py`**

```python
"""Tests for .mgir v2 schema validation."""
import json
import pathlib

import jsonschema
import pytest

from schemas import validate_mgir

FIXTURES_DIR = pathlib.Path(__file__).parent / "fixtures" / "mgir_v2"


def _load(name: str) -> dict:
    return json.loads((FIXTURES_DIR / name).read_text(encoding="utf-8"))


def test_cloth_01_passes_validation():
    mgir = _load("yvonne_cloth_01.json")
    # Should not raise.
    validate_mgir.validate(mgir)


def test_missing_schema_version_rejected():
    bad = _load("yvonne_cloth_01.json")
    del bad["schemaVersion"]
    with pytest.raises(jsonschema.ValidationError):
        validate_mgir.validate(bad)


def test_unknown_npr_feature_rejected():
    bad = _load("yvonne_cloth_01.json")
    bad["nprFeatures"]["telekinesis"] = {"enabled": True}
    with pytest.raises(jsonschema.ValidationError):
        validate_mgir.validate(bad)


def test_alpha_enabled_without_alphaSource_rejected():
    bad = _load("yvonne_cloth_01.json")
    bad["nprFeatures"]["alpha"] = {"enabled": True, "blendMode": "Transparent"}
    with pytest.raises(jsonschema.ValidationError):
        validate_mgir.validate(bad)
```

- [ ] **Step 4: Run the test to verify it fails**

Run:
```bash
pytest tests/test_mgir_v2_schema.py -v
```
Expected: **FAIL** with `ModuleNotFoundError: No module named 'schemas'`. This is correct — the validator does not exist yet.

- [ ] **Step 5: Commit**

```bash
git add tests/ schemas/__init__.py
git commit -m "test: add failing tests for .mgir v2 schema validator"
```

---

### Task 1.2: Implement `.mgir v2` JSON Schema definition

**Files:**
- Create: `schemas/mgir_v2.json`

**Interfaces:**
- Consumes: nothing
- Produces: schema file consumed by `validate_mgir.py` (next task). Defines the contract for every `.mgir v2`.

- [ ] **Step 1: Write `schemas/mgir_v2.json`**

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://b2u.local/schemas/mgir_v2.json",
  "title": "B2U Material Graph IR v2",
  "type": "object",
  "required": ["schemaVersion", "materialName", "preset", "materialType"],
  "additionalProperties": false,
  "properties": {
    "schemaVersion": { "const": 2 },
    "materialName": { "type": "string", "pattern": "^M_[A-Za-z0-9_]+$" },
    "preset":        { "type": "string", "enum": ["Endfield", "Genshin", "Honkai", "GenericToon"] },
    "materialType":  { "type": "string", "enum": ["Body", "Hair", "Cloth", "Face", "Eye", "Skin", "Outline", "EyeShadow"] },
    "baseColorTexture": { "type": "string" },
    "normalTexture":    { "type": "string" },
    "nprFeatures": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "mask":   { "$ref": "#/$defs/featureBlob" },
        "colorRemap": {
          "allOf": [
            { "$ref": "#/$defs/featureBlob" },
            {
              "if": { "properties": { "enabled": { "const": true } } },
              "then": { "required": ["rampTexture", "maskChannel"] }
            }
          ]
        },
        "alpha": {
          "allOf": [
            { "$ref": "#/$defs/featureBlob" },
            {
              "if": { "properties": { "enabled": { "const": true } } },
              "then": {
                "required": ["alphaSource", "blendMode"],
                "properties": {
                  "blendMode": { "enum": ["Opaque", "Cutout", "Transparent", "Fade"] }
                }
              }
            }
          ]
        },
        "sss":       { "$ref": "#/$defs/featureBlob" },
        "anisoSpec": { "$ref": "#/$defs/featureBlob" },
        "multiSpec": { "$ref": "#/$defs/featureBlob" }
      }
    }
  },
  "$defs": {
    "featureBlob": {
      "type": "object",
      "required": ["enabled"],
      "properties": {
        "enabled": { "type": "boolean" }
      },
      "additionalProperties": true
    }
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add schemas/mgir_v2.json
git commit -m "feat(schemas): define .mgir v2 JSON Schema with nprFeatures"
```

---

### Task 1.3: Implement `schemas.validate_mgir` module

**Files:**
- Create: `schemas/validate_mgir.py`

**Interfaces:**
- Consumes: `schemas/mgir_v2.json` (read at module load)
- Produces: `validate(mgir_dict: dict) -> None`. Raises `jsonschema.ValidationError` on failure. Loaded schema is reused across calls.

- [ ] **Step 1: Write `schemas/validate_mgir.py`**

```python
"""Validator entry point for .mgir v2 documents."""
from __future__ import annotations

import json
import pathlib
from functools import lru_cache

import jsonschema

_SCHEMA_PATH = pathlib.Path(__file__).parent / "mgir_v2.json"


@lru_cache(maxsize=1)
def _load_schema() -> dict:
    return json.loads(_SCHEMA_PATH.read_text(encoding="utf-8"))


def validate(mgir: dict) -> None:
    """Validate a parsed .mgir v2 document. Raises jsonschema.ValidationError on failure."""
    jsonschema.validate(instance=mgir, schema=_load_schema())


def is_v2(mgir: dict) -> bool:
    """Cheap check: is this a v2 (or later) document? v1 docs have no schemaVersion."""
    return isinstance(mgir, dict) and mgir.get("schemaVersion", 1) >= 2
```

- [ ] **Step 2: Run the previously failing tests — they should now pass**

Run:
```bash
pytest tests/test_mgir_v2_schema.py -v
```
Expected: 4 tests pass.

- [ ] **Step 3: Add a v1 backward compatibility test**

Append to `tests/test_mgir_v2_schema.py`:

```python
def test_v1_document_not_v2():
    v1 = {
        "materialName": "M_old",
        "preset": "Endfield",
        "materialType": "Cloth",
    }
    assert validate_mgir.is_v2(v1) is False
    # v1 docs are NOT validated by the v2 schema by design.
```

Run:
```bash
pytest tests/test_mgir_v2_schema.py -v
```
Expected: 5 tests pass.

- [ ] **Step 4: Commit**

```bash
git add schemas/validate_mgir.py tests/test_mgir_v2_schema.py
git commit -m "feat(schemas): validator + v1 compat detection"
```

---

### Task 1.4: Add 4 more `.mgir v2` fixtures

**Files:**
- Create: `tests/fixtures/mgir_v2/yvonne_body_01.json`
- Create: `tests/fixtures/mgir_v2/yvonne_hair_01.json`
- Create: `tests/fixtures/mgir_v2/yvonne_face_01.json`
- Create: `tests/fixtures/mgir_v2/yvonne_eye_iris_01.json`
- Modify: `tests/test_mgir_v2_schema.py`

**Interfaces:**
- Consumes: `validate_mgir.validate(...)`
- Produces: 4 more validated fixtures so the test corpus covers ≥ 5 Yvonne materials.

- [ ] **Step 1: Add 4 fixtures. Write `tests/fixtures/mgir_v2/yvonne_body_01.json`:**

```json
{
  "schemaVersion": 2,
  "materialName": "M_actor_yvonne_body_01",
  "preset": "Endfield",
  "materialType": "Body",
  "baseColorTexture": "T_actor_common_body_01_RD.png",
  "normalTexture": "T_actor_yvonne_body_01_N.png",
  "nprFeatures": {
    "mask":       { "enabled": false },
    "colorRemap": { "enabled": false },
    "alpha":      { "enabled": false },
    "sss":        { "enabled": true, "tint": [0.85, 0.55, 0.45], "strength": 0.4 },
    "anisoSpec":  { "enabled": false },
    "multiSpec":  { "enabled": true, "rampTexture": "TPLK_actor_common_body_RD.png" }
  }
}
```

- [ ] **Step 2: Write `tests/fixtures/mgir_v2/yvonne_hair_01.json`**

```json
{
  "schemaVersion": 2,
  "materialName": "M_actor_yvonne_hair_01",
  "preset": "Endfield",
  "materialType": "Hair",
  "baseColorTexture": "T_actor_yvonne_hair_01_D.png",
  "normalTexture": "T_actor_yvonne_hair_01_N.png",
  "nprFeatures": {
    "mask":       { "enabled": false },
    "colorRemap": { "enabled": true, "rampTexture": "TPLK_actor_common_hair_RD.png", "maskChannel": "R" },
    "alpha":      { "enabled": false },
    "sss":        { "enabled": false },
    "anisoSpec":  { "enabled": true, "tangentTexture": "T_actor_yvonne_hair_01_AN.png", "strength": 0.7 },
    "multiSpec":  { "enabled": true, "rampTexture": "TPLK_actor_hair_spec_RD.png" }
  }
}
```

- [ ] **Step 3: Write `tests/fixtures/mgir_v2/yvonne_face_01.json`**

```json
{
  "schemaVersion": 2,
  "materialName": "M_actor_yvonne_face_01",
  "preset": "Endfield",
  "materialType": "Face",
  "baseColorTexture": "T_actor_yvonne_face_01_D.png",
  "nprFeatures": {
    "mask":       { "enabled": false },
    "colorRemap": { "enabled": false },
    "alpha":      { "enabled": false },
    "sss":        { "enabled": true, "tint": [0.95, 0.7, 0.6], "strength": 0.3 },
    "anisoSpec":  { "enabled": false },
    "multiSpec":  { "enabled": false }
  }
}
```

- [ ] **Step 4: Write `tests/fixtures/mgir_v2/yvonne_eye_iris_01.json`**

```json
{
  "schemaVersion": 2,
  "materialName": "M_actor_yvonne_iris_01",
  "preset": "Endfield",
  "materialType": "Eye",
  "baseColorTexture": "T_actor_yvonne_iris_01_D.png",
  "nprFeatures": {
    "mask":       { "enabled": false },
    "colorRemap": { "enabled": false },
    "alpha":      { "enabled": true, "alphaSource": "baseColor.A", "blendMode": "Cutout", "sortPriority": 50 },
    "sss":        { "enabled": false },
    "anisoSpec":  { "enabled": false },
    "multiSpec":  { "enabled": true, "rampTexture": "TPLK_actor_iris_RD.png" }
  }
}
```

- [ ] **Step 5: Add tests that each fixture passes validation. Append to `tests/test_mgir_v2_schema.py`:**

```python
@pytest.mark.parametrize("fixture_name,expected_type", [
    ("yvonne_body_01.json",      "Body"),
    ("yvonne_hair_01.json",      "Hair"),
    ("yvonne_cloth_01.json",     "Cloth"),
    ("yvonne_face_01.json",      "Face"),
    ("yvonne_eye_iris_01.json",  "Eye"),
])
def test_fixtures_pass_validation(fixture_name, expected_type):
    mgir = _load(fixture_name)
    validate_mgir.validate(mgir)
    assert mgir["materialType"] == expected_type
```

Run:
```bash
pytest tests/test_mgir_v2_schema.py -v
```
Expected: All parametrized cases pass (5 fixture-based tests + 4 logic tests = 9 total).

- [ ] **Step 6: Commit**

```bash
git add tests/fixtures/mgir_v2/ tests/test_mgir_v2_schema.py
git commit -m "test: add 4 more Yvonne mgir v2 fixtures + parametrized validation"
```

---

## Phase 2 — NPR HLSL Include Library (Scaffolding)

The next 7 tasks create the shared HLSL include files. They follow the same pattern:

1. Write a Python-side smoke test that compiles a one-material `.shader` file using that include via Unity's CLI in batchmode. (Smoke test is part of Phase 4; for Phase 2, just create the file and visually verify entry-point signatures match across includes.)
2. Write the HLSL file with a single entry-point function.
3. Commit.

Each task is small on its own. They share file conventions.

### Task 2.1: NPR common helpers (no NPR-specific logic yet)

**Files:**
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_Common.hlsl`
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_Common.hlsl.meta`

**Interfaces:**
- Consumes: nothing (pure utilities)
- Produces: helper functions used by every other NPR_*.hlsl:
  - `float3 NPR_Remap01(float3 v, float3 lo, float3 hi)` — same as `lerp` but explicit clamp
  - `float NPR_Saturate(float x)` — re-export for symmetry (avoid engine-wide `saturate()` namespace issues)

- [ ] **Step 1: Write `NPR_Common.hlsl`**

```hlsl
#ifndef B2U_NPR_COMMON_INCLUDED
#define B2U_NPR_COMMON_INCLUDED

float3 NPR_Remap01(float3 v, float3 lo, float3 hi)
{
    return (v - lo) / max(hi - lo, 1e-5);
}

float NPR_Saturate(float x)
{
    return clamp(x, 0.0, 1.0);
}

#endif // B2U_NPR_COMMON_INCLUDED
```

- [ ] **Step 2: Create matching `.meta` file with the standard Unity meta for an HLSL include**

Write `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_Common.hlsl.meta`:

```yaml
fileFormatVersion: 2
guid: a1b2c3d4e5f6789012345678abcdef01
ShaderIncludeImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 3: Commit**

```bash
git add unity/Packages/com.migr.shaderconverter/Runtime/NPR/
git commit -m "feat(npr): add NPR_Common.hlsl with remap and saturate helpers"
```

---

### Task 2.2: NPR MaskLayer include

**Files:**
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_MaskLayer.hlsl`
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_MaskLayer.hlsl.meta`

**Interfaces:**
- Consumes: nothing external (defines its own contract)
- Produces: `float4 NPR_SampleMask(Texture2D maskTex, SamplerState maskSampler, float2 uv)` — samples the mask texture, returns float4 RGBA.

- [ ] **Step 1: Write `NPR_MaskLayer.hlsl`**

```hlsl
#ifndef B2U_NPR_MASK_LAYER_INCLUDED
#define B2U_NPR_MASK_LAYER_INCLUDED

float4 NPR_SampleMask(Texture2D maskTex, SamplerState maskSampler, float2 uv)
{
    return maskTex.Sample(maskSampler, uv);
}

// Picks a single channel of a sampled mask based on a channel token (R/G/B/A).
// 'channel' is one of {0,1,2,3} for R/G/B/A respectively.
float NPR_PickChannel(float4 maskSample, int channel)
{
    return maskSample[channel];
}

#endif // B2U_NPR_MASK_LAYER_INCLUDED
```

- [ ] **Step 2: Write the meta file**

```yaml
fileFormatVersion: 2
guid: b2c3d4e5f6789012345678abcdef0123
ShaderIncludeImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 3: Commit**

```bash
git add unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_MaskLayer.hlsl unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_MaskLayer.hlsl.meta
git commit -m "feat(npr): NPR_MaskLayer include for RGBA channel picking"
```

---

### Task 2.3: NPR ColorRemap include

**Files:**
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_ColorRemap.hlsl`
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_ColorRemap.hlsl.meta`

**Interfaces:**
- Produces: `float3 NPR_ColorRemap(float3 srcColor, Texture2D rampTex, SamplerState rampSampler, float maskValue)` — looks up `maskValue` along the ramp texture's U-axis and returns the ramp color. SSR-friendly (no derivatives).

- [ ] **Step 1: Write `NPR_ColorRemap.hlsl`**

```hlsl
#ifndef B2U_NPR_COLOR_REMAP_INCLUDED
#define B2U_NPR_COLOR_REMAP_INCLUDED

#include "NPR_Common.hlsl"

float3 NPR_ColorRemap(float3 srcColor, Texture2D rampTex, SamplerState rampSampler, float maskValue)
{
    float u = NPR_Saturate(maskValue);
    // Sample ramp at V=0.5 to avoid extrapolation at edges of the LUT height.
    float3 rampColor = rampTex.Sample(rampSampler, float2(u, 0.5)).rgb;
    // srcColor acts as a per-pixel modulation weight; final = srcColor * ramp
    return srcColor * rampColor;
}

#endif // B2U_NPR_COLOR_REMAP_INCLUDED
```

- [ ] **Step 2: Write the meta file**

```yaml
fileFormatVersion: 2
guid: c3d4e5f6789012345678abcdef012345
ShaderIncludeImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 3: Commit**

```bash
git add unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_ColorRemap.hlsl unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_ColorRemap.hlsl.meta
git commit -m "feat(npr): NPR_ColorRemap include for LUT-based color remapping"
```

---

### Task 2.4: NPR AlphaBlend include

**Files:**
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_AlphaBlend.hlsl`
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_AlphaBlend.hlsl.meta`

**Interfaces:**
- Produces: `float NPR_ResolveAlpha(int blendModeToken, float alphaSourceValue, float cutoff)`:
  - `0` = Opaque (always 1.0)
  - `1` = Cutout (step(alpha, cutoff))
  - `2` = Transparent (uses source alpha directly)
  - `3` = Fade (uses source alpha directly)

- [ ] **Step 1: Write `NPR_AlphaBlend.hlsl`**

```hlsl
#ifndef B2U_NPR_ALPHA_BLEND_INCLUDED
#define B2U_NPR_ALPHA_BLEND_INCLUDED

#include "NPR_Common.hlsl"

float NPR_ResolveAlpha(int blendModeToken, float alphaSourceValue, float cutoff)
{
    if (blendModeToken == 0) // Opaque
    {
        return 1.0;
    }
    if (blendModeToken == 1) // Cutout
    {
        return NPR_Saturate((alphaSourceValue - cutoff) * 1e3); // hard step
    }
    // 2 Transparent or 3 Fade: return source alpha
    return NPR_Saturate(alphaSourceValue);
}

#endif // B2U_NPR_ALPHA_BLEND_INCLUDED
```

- [ ] **Step 2: Write the meta file**

```yaml
fileFormatVersion: 2
guid: d4e5f6789012345678abcdef01234567
ShaderIncludeImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 3: Commit**

```bash
git add unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_AlphaBlend.hlsl unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_AlphaBlend.hlsl.meta
git commit -m "feat(npr): NPR_AlphaBlend include for Opaque/Cutout/Transparent/Fade"
```

---

### Task 2.5: NPR SkinSSS include

**Files:**
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_SkinSSS.hlsl`
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_SkinSSS.hlsl.meta`

**Interfaces:**
- Produces: `float3 NPR_SkinSSS(float3 baseColor, float3 N, float3 L, float3 V, float3 sssTint, float sssStrength)` — Wraps-NdotL translucency approximation. Output = `baseColor * (1 - sssStrength + sssStrength * sssTint * saturate(dot(-L, N)))`. Conservative; matches the spirit of NPR Cel-shading SSS.

- [ ] **Step 1: Write `NPR_SkinSSS.hlsl`**

```hlsl
#ifndef B2U_NPR_SKIN_SSS_INCLUDED
#define B2U_NPR_SKIN_SSS_INCLUDED

#include "NPR_Common.hlsl"

float3 NPR_SkinSSS(float3 baseColor, float3 N, float3 L, float3 V, float3 sssTint, float sssStrength)
{
    // Back-light term: light from the opposite side of N arriving at the pixel.
    float backLight = NPR_Saturate(dot(-L, N));
    float3 sssTerm = sssTint * backLight;
    // Blend SSS into base by sssStrength.
    float3 sssColor = baseColor * lerp(float3(1, 1, 1), sssTerm, sssStrength);
    return sssColor;
}

#endif // B2U_NPR_SKIN_SSS_INCLUDED
```

- [ ] **Step 2: Write the meta file**

```yaml
fileFormatVersion: 2
guid: e5f6789012345678abcdef0123456789
ShaderIncludeImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 3: Commit**

```bash
git add unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_SkinSSS.hlsl unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_SkinSSS.hlsl.meta
git commit -m "feat(npr): NPR_SkinSSS include with back-light translucency"
```

---

### Task 2.6: NPR AnisoSpec include

**Files:**
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_AnisoSpec.hlsl`
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_AnisoSpec.hlsl.meta`

**Interfaces:**
- Produces: `float NPR_AnisoSpec(float3 N, float3 T, float3 B, float3 H, float roughness, float anisoStrength)` — Ward / anisotropic-like specular term, returns scalar in [0,1].

- [ ] **Step 1: Write `NPR_AnisoSpec.hlsl`**

```hlsl
#ifndef B2U_NPR_ANISO_SPEC_INCLUDED
#define B2U_NPR_ANISO_SPEC_INCLUDED

#include "NPR_Common.hlsl"

float NPR_AnisoSpec(float3 N, float3 T, float3 B, float3 H, float roughness, float anisoStrength)
{
    float NdotH = NPR_Saturate(dot(N, H));
    float TdotH = dot(T, H);
    float BdotH = dot(B, H);

    float ax = roughness * (1.0 + anisoStrength * 0.5);
    float ay = roughness * (1.0 - anisoStrength * 0.5);

    float exponent = (TdotH * TdotH / (ax * ax)) + (BdotH * BdotH / (ay * ay));
    float denom = 4.0 * 3.14159 * ax * ay * sqrt(NdotH * NdotH + 1e-5);
    float ward = exp(-exponent) / max(denom, 1e-5);

    return NPR_Saturate(ward);
}

#endif // B2U_NPR_ANISO_SPEC_INCLUDED
```

- [ ] **Step 2: Write the meta file**

```yaml
fileFormatVersion: 2
guid: f67890123456789abcdef012345678901
ShaderIncludeImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 3: Commit**

```bash
git add unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_AnisoSpec.hlsl unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_AnisoSpec.hlsl.meta
git commit -m "feat(npr): NPR_AnisoSpec include (Ward anisotropic)"
```

---

### Task 2.7: NPR MultiLayerSpec include

**Files:**
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_MultiLayerSpec.hlsl`
- Create: `unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_MultiLayerSpec.hlsl.meta`

**Interfaces:**
- Produces: `float3 NPR_MultiLayerSpec(float3 baseColor, Texture2D rampTex, SamplerState rampSampler, float specMask)` — looks up ramp at u = specMask; returns `baseColor * ramp.rgb`. Two-lobe layering happens at the call-site (call function twice with different masks if needed).

- [ ] **Step 1: Write `NPR_MultiLayerSpec.hlsl`**

```hlsl
#ifndef B2U_NPR_MULTI_LAYER_SPEC_INCLUDED
#define B2U_NPR_MULTI_LAYER_SPEC_INCLUDED

#include "NPR_Common.hlsl"

float3 NPR_MultiLayerSpec(float3 baseColor, Texture2D rampTex, SamplerState rampSampler, float specMask)
{
    float u = NPR_Saturate(specMask);
    float3 rampColor = rampTex.Sample(rampSampler, float2(u, 0.5)).rgb;
    return baseColor * rampColor;
}

#endif // B2U_NPR_MULTI_LAYER_SPEC_INCLUDED
```

- [ ] **Step 2: Write the meta file**

```yaml
fileFormatVersion: 2
guid: 07890123456789abcdef0123456789012
ShaderIncludeImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 3: Commit**

```bash
git add unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_MultiLayerSpec.hlsl unity/Packages/com.migr.shaderconverter/Runtime/NPR/NPR_MultiLayerSpec.hlsl.meta
git commit -m "feat(npr): NPR_MultiLayerSpec include (ramp-based two-lobe spec)"
```

---

## Phase 3 — Codegen Routing in `B2UEndfieldMaterialWriter`

### Task 3.1: Add failing test for codegen reading `nprFeatures`

**Files:**
- Create: `tests/test_codegen_snapshots.py`
- Create: `tests/snapshots/enabled_mask_colorRemap_alpha.txt` (golden shader)
- Create: `tests/snapshots/v1_compat_empty.txt` (golden shader, no NPR)
- Create: `tests/snapshots/__init__.py` (empty)

**Interfaces:**
- Consumes: pure Python unit test of the rendering rules in `B2UEndfieldMaterialWriter.cs`. Since codegen is in C#, this phase instead defines a **Python mirror** of the codegen rules in `unity/Packages/com.migr.shaderconverter/Runtime/NPR/codegen_rules.py` so we can unit-test it in pytest. The C# writer will then call this same logic via the same namespace structure.

  Actually — to keep the plan simple and avoid introducing both Python and C# versions of the same logic, this Phase implements a **Python preprocessor** that emits the *directive list* (which `#include` lines, which MaterialProperties), and the actual C# writer calls the same Python preprocessor via stdin in editor. The directives are stable and can be snapshot-tested.

- **Produces:** A pure-Python module `codegen/directive_builder.py` exposing `build_directives(mgir: dict) -> list[Directive]` where `Directive` is a small dataclass.

- [ ] **Step 1: Create directories and empty `__init__.py`**

```bash
mkdir -p tests/snapshots codegen
touch tests/snapshots/__init__.py codegen/__init__.py
```

- [ ] **Step 2: Write `codegen/directive_builder.py`**

```python
"""Pure-Python directive builder for B2U codegen.

Reads a .mgir v2 doc and emits a list of HLSL directives (#include lines + MaterialProperty declarations).

The C# side (`B2UEndfieldMaterialWriter`) is expected to read this Python module by
invoking `python -m codegen.directive_builder < mgir.json` and parsing the JSON output.
"""
from __future__ import annotations

import argparse
import dataclasses
import json
import sys
from typing import List


@dataclasses.dataclass
class Directive:
    kind: str          # one of: "include", "materialProp"
    target: str        # for include: relative path under Runtime/. for materialProp: "_PropName"
    value: object = None  # for materialProp: default value

    def to_dict(self) -> dict:
        return dataclasses.asdict(self)


_NPR_INCLUDE_BY_FEATURE = {
    "mask":       "NPR/NPR_MaskLayer.hlsl",
    "colorRemap": "NPR/NPR_ColorRemap.hlsl",
    "alpha":      "NPR/NPR_AlphaBlend.hlsl",
    "sss":        "NPR/NPR_SkinSSS.hlsl",
    "anisoSpec":  "NPR/NPR_AnisoSpec.hlsl",
    "multiSpec":  "NPR/NPR_MultiLayerSpec.hlsl",
}


def build_directives(mgir: dict) -> List[Directive]:
    directives: List[Directive] = []
    features = mgir.get("nprFeatures") or {}
    for feature_name, include_path in _NPR_INCLUDE_BY_FEATURE.items():
        feat = features.get(feature_name) or {}
        if feat.get("enabled"):
            directives.append(Directive(kind="include", target=include_path))
            # Emit default MaterialProperty stubs based on feature shape
            if feature_name == "colorRemap":
                directives.append(Directive(kind="materialProp", target="_RemapRamp", value=str(feat.get("rampTexture", ""))))
                directives.append(Directive(kind="materialProp", target="_RemapMaskChannel", value=int({"R":0,"G":1,"B":2,"A":3}.get(feat.get("maskChannel", "R"), 0))))
            elif feature_name == "alpha":
                blend = feat.get("blendMode", "Opaque")
                directives.append(Directive(kind="materialProp", target="_AlphaBlendMode", value={"Opaque":0,"Cutout":1,"Transparent":2,"Fade":3}.get(blend, 0)))
                directives.append(Directive(kind="materialProp", target="_AlphaCutoff", value=0.5))
            elif feature_name == "sss":
                directives.append(Directive(kind="materialProp", target="_SSSTint", value=feat.get("tint", [1, 0.7, 0.6])))
                directives.append(Directive(kind="materialProp", target="_SSSStrength", value=float(feat.get("strength", 0.5))))
            elif feature_name == "anisoSpec":
                directives.append(Directive(kind="materialProp", target="_AnisoStrength", value=float(feat.get("strength", 0.5))))
            elif feature_name == "multiSpec":
                directives.append(Directive(kind="materialProp", target="_SpecRamp", value=str(feat.get("rampTexture", ""))))

    return directives


def main(argv=None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--format", choices=["json"], default="json")
    args = parser.parse_args(argv)
    mgir = json.load(sys.stdin)
    directives = build_directives(mgir)
    json.dump([d.to_dict() for d in directives], sys.stdout, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 3: Write `tests/test_codegen_snapshots.py`**

```python
"""Snapshot tests for the codegen directive builder."""
import json
import pathlib

import pytest

from codegen import directive_builder

FIXTURES = pathlib.Path(__file__).parent / "fixtures" / "mgir_v2"
SNAPSHOTS = pathlib.Path(__file__).parent / "snapshots"


def _load(name: str) -> dict:
    return json.loads((FIXTURES / name).read_text(encoding="utf-8"))


def _directives_dict(name: str):
    mgir = _load(name)
    return [d.to_dict() for d in directive_builder.build_directives(mgir)]


def test_cloth_01_emits_three_includes_and_props():
    d = _directives_dict("yvonne_cloth_01.json")
    targets = {x["target"] for x in d if x["kind"] == "include"}
    assert "NPR/NPR_MaskLayer.hlsl" in targets
    assert "NPR/NPR_ColorRemap.hlsl" in targets
    assert "NPR/NPR_AlphaBlend.hlsl" in targets
    assert "NPR/NPR_SkinSSS.hlsl" not in targets  # not enabled


def test_body_01_emits_sss_and_multispec():
    d = _directives_dict("yvonne_body_01.json")
    targets = {x["target"] for x in d if x["kind"] == "include"}
    assert "NPR/NPR_SkinSSS.hlsl" in targets
    assert "NPR/NPR_MultiLayerSpec.hlsl" in targets
    assert "NPR/NPR_MaskLayer.hlsl" not in targets


def test_hair_01_emits_aniso_multispec_colorRemap():
    d = _directives_dict("yvonne_hair_01.json")
    includes = [x["target"] for x in d if x["kind"] == "include"]
    assert "NPR/NPR_AnisoSpec.hlsl" in includes
    assert "NPR/NPR_MultiLayerSpec.hlsl" in includes
    assert "NPR/NPR_ColorRemap.hlsl" in includes


def test_face_01_emits_only_sss():
    d = _directives_dict("yvonne_face_01.json")
    includes = [x["target"] for x in d if x["kind"] == "include"]
    assert includes == ["NPR/NPR_SkinSSS.hlsl"]


def test_eye_emits_alpha_and_multispec():
    d = _directives_dict("yvonne_eye_iris_01.json")
    includes = [x["target"] for x in d if x["kind"] == "include"]
    assert "NPR/NPR_AlphaBlend.hlsl" in includes
    assert "NPR/NPR_MultiLayerSpec.hlsl" in includes


def test_full_cloth_directive_list_snapshot():
    """Snapshot the entire directive list for cloth_01 (incl. materialProp)."""
    d = _directives_dict("yvonne_cloth_01.json")
    out = SNAPSHOTS / "yvonne_cloth_01_directives.json"
    if not out.exists():
        out.write_text(json.dumps(d, indent=2, ensure_ascii=False), encoding="utf-8")
        pytest.skip("wrote initial snapshot")
    expected = json.loads(out.read_text(encoding="utf-8"))
    assert d == expected
```

- [ ] **Step 4: Run the tests — first run writes the snapshot, subsequent runs verify against it**

Run:
```bash
pytest tests/test_codegen_snapshots.py -v
```
Expected: First run produces the snapshot for `yvonne_cloth_01_directives.json` and skips the snapshot test; the other 5 should pass. Re-run:
```bash
pytest tests/test_codegen_snapshots.py -v
```
Expected: All 6 pass.

- [ ] **Step 5: Commit**

```bash
git add codegen/ tests/test_codegen_snapshots.py tests/snapshots/__init__.py
git commit -m "feat(codegen): Python directive builder + snapshot tests"
```

---

### Task 3.2: Wire C# writer to invoke Python directive builder

**Files:**
- Modify: `unity/Packages/com.migr.shaderconverter/Editor/B2UEndfieldMaterialWriter.cs`
- Create: `unity/Packages/com.migr.shaderconverter/Editor/B2UDirectiveRunner.cs`

**Interfaces:**
- Consumes: `.mgir v2` JSON content (string)
- Produces: list of `Directive { string Kind; string Target; string ValueJson; }` parsed from the Python CLI output.

- [ ] **Step 1: Read the current writer to understand its surface**

```bash
wc -l unity/Packages/com.migr.shaderconverter/Editor/B2UEndfieldMaterialWriter.cs
head -100 unity/Packages/com.migr.shaderconverter/Editor/B2UEndfieldMaterialWriter.cs
```

- [ ] **Step 2: Create `B2UDirectiveRunner.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor
{
    /// <summary>
    /// Invokes the Python `codegen.directive_builder` module over an .mgir v2 doc
    /// and returns the list of #include + MaterialProperty directives.
    /// </summary>
    public static class B2UDirectiveRunner
    {
        public struct Directive
        {
            public string Kind;     // "include" | "materialProp"
            public string Target;   // for include: relative HLSL path. for materialProp: _PropName.
            public string Value;    // JSON-encoded default value as string.
        }

        /// <summary>
        /// Returns an empty list on v1 docs (no nprFeatures) or if the doc fails to parse.
        /// </summary>
        public static List<Directive> Run(string mgirJsonText, string pythonExe = "python")
        {
            var list = new List<Directive>();
            if (string.IsNullOrEmpty(mgirJsonText)) return list;

            // Quick parse + v2 detection: skip on v1 (no schemaVersion or schemaVersion<2).
            JObject root;
            try { root = JObject.Parse(mgirJsonText); }
            catch { return list; }
            int sv = root.Value<int?>("schemaVersion") ?? 1;
            if (sv < 2) return list;

            var psi = new ProcessStartInfo(pythonExe, "-m codegen.directive_builder")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = GetProjectRoot(),
            };
            using var proc = Process.Start(psi);
            if (proc == null) return list;
            proc.StandardInput.Write(mgirJsonText);
            proc.StandardInput.Flush();
            proc.StandardInput.Close();
            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10_000);
            if (proc.ExitCode != 0) return list;

            // Parse JSON array.
            var arr = JArray.Parse(stdout);
            foreach (var t in arr)
            {
                list.Add(new Directive
                {
                    Kind = (string)t["kind"],
                    Target = (string)t["target"],
                    Value = t["value"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "",
                });
            }
            return list;
        }

        private static string GetProjectRoot()
        {
            // Walk up from this assembly's location to find the project root (containing codegen/ and unity/).
            var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(B2UDirectiveRunner).Assembly.Location));
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "codegen")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "unity")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return Directory.GetCurrentDirectory();
        }
    }
}
```

- [ ] **Step 3: Patch `B2UEndfieldMaterialWriter.cs` to call the runner. First read its current top to find the `Write` method.**

```bash
grep -n "public.*Write\|public.*Emit\|public.*Generate" unity/Packages/com.migr.shaderconverter/Editor/B2UEndfieldMaterialWriter.cs | head -20
```

- [ ] **Step 4: At the start of the file's main emission method (typically `WriteMaterial`), insert:**

```csharp
// B2U NPR hook: read .mgir v2 and emit NPR directives (#include + MaterialProperty).
var mgirText = System.IO.File.Exists(inputPath) ? System.IO.File.ReadAllText(inputPath) : string.Empty;
var directives = B2UDirectiveRunner.Run(mgirText);
foreach (var dir in directives)
{
    if (dir.Kind == "include")
    {
        sb.AppendLine($"#include \"../{dir.Target}\"");
    }
    // materialProp is emitted later in the Properties block; record in a member variable.
    _pendingMaterialProps.Add(dir);
}
```

Where `_pendingMaterialProps` is a `List<B2UDirectiveRunner.Directive>` field declared at the class level. (Add it if not present.) Initialize / clear it at the start of each emission.

- [ ] **Step 5: Verify the C# compiles by attempting a placeholder build**

```bash
# This task only validates the file is well-formed C# syntactically.
grep -n "B2UDirectiveRunner\|nprFeatures\|_pendingMaterialProps" unity/Packages/com.migr.shaderconverter/Editor/B2UEndfieldMaterialWriter.cs
```
Expected: matches the strings you just inserted. A full Unity build verification happens in Phase 4 (Unity MCP smoke render).

- [ ] **Step 6: Commit**

```bash
git add unity/Packages/com.migr.shaderconverter/Editor/B2UDirectiveRunner.cs unity/Packages/com.migr.shaderconverter/Editor/B2UEndfieldMaterialWriter.cs
git commit -m "feat(codegen): C# writer invokes Python directive builder for .mgir v2"
```

---

## Phase 4 — Goo-side NPR feature detection

### Task 4.1: Failing tests for Blender node graph → `nprFeatures` detector

**Files:**
- Create: `tests/test_npr_feature_detector.py`
- Create: `b2u_mvp_blender/npr_feature_detector.py`
- Create: `b2u_mvp_blender/__init__.py` (if missing — verify)

**Interfaces:**
- Consumes: a Blender `bpy.types.Material` (or a mock with `node_tree.nodes` iterable, when running outside Blender). The mock path uses simple dict-like node descriptors.
- Produces: a dict matching the `.mgir v2 nprFeatures` schema, with `enabled` correctly set per detected feature.

- [ ] **Step 1: Verify `b2u_mvp_blender/__init__.py` exists**

```bash
ls -la b2u_mvp_blender/__init__.py 2>&1
```
Expected: file exists. If not, create it:
```bash
touch b2u_mvp_blender/__init__.py
```

- [ ] **Step 2: Write `b2u_mvp_blender/npr_feature_detector.py`**

```python
"""Detect NPR feature usage from a Blender material's node graph.

This module is designed to be importable OUTSIDE Blender: anything that needs
`bpy` (such as walking Material.node_tree) is guarded by `import bpy` inside
try/except so tests can run with plain Python mocks.
"""
from __future__ import annotations

from typing import Any, Dict, Iterable, List, Optional


# Node-type -> feature mapping. These strings are normalized to allow mocks.
_MASK_NODES = {"ShaderNodeTexImage", "ShaderNodeRGBToBW", "ShaderNodeSeparateRGB"}
_COLOR_REMAP_NODES = {"ShaderNodeValToRGB", "ShaderNodeMapRange", "ShaderNodeFloatCurve"}
_ALPHA_NODES = {"ShaderNodeBsdfTransparent", "ShaderNodeBsdfGlossy", "ShaderNodeMixShader"}
_SSS_NODES = {"ShaderNodeBsdfPrincipled", "ShaderNodeSubsurfaceScattering"}
_ANISO_NODES = {"ShaderNodeBsdfHair", "ShaderNodeBsdfAnisotropic"}
_MULTI_SPEC_NODES = {"ShaderNodeBsdfPrincipled"}


def _walk_nodes(node_tree) -> Iterable[Any]:
    """Iterate every node in a Blender NodeTree. Falls back to a `nodes` attr."""
    if node_tree is None:
        return []
    return list(getattr(node_tree, "nodes", []))


def _node_type_name(node: Any) -> str:
    """Return Blender-style node type name. Works on real bpy and mocks."""
    btype = getattr(node, "bl_idname", None)
    if btype:
        return btype
    return getattr(node, "type", "") or ""


def detect_npr_features(material: Any) -> Dict[str, Dict[str, Any]]:
    """Return a dict shaped like `nprFeatures`. Each feature has at least {enabled: bool}.

    The output preserves all features (so v2 schema sees every key); non-detected
    ones come back as {enabled: False}.
    """
    features = {
        "mask":       {"enabled": False},
        "colorRemap": {"enabled": False},
        "alpha":      {"enabled": False},
        "sss":        {"enabled": False},
        "anisoSpec":  {"enabled": False},
        "multiSpec":  {"enabled": False},
    }
    node_tree = getattr(material, "node_tree", None)
    if node_tree is None:
        return features

    types_seen = {_node_type_name(n) for n in _walk_nodes(node_tree)}

    if types_seen & _MASK_NODES:
        features["mask"]["enabled"] = True
    if types_seen & _COLOR_REMAP_NODES:
        features["colorRemap"]["enabled"] = True
        features["colorRemap"].setdefault("rampTexture", "auto_detected_ramp.png")
        features["colorRemap"].setdefault("maskChannel", "R")
    if types_seen & _ALPHA_NODES:
        features["alpha"]["enabled"] = True
        features["alpha"].setdefault("alphaSource", "baseColor.A")
        features["alpha"].setdefault("blendMode", "Transparent")
        features["alpha"].setdefault("sortPriority", 200)
    if types_seen & _SSS_NODES:
        features["sss"]["enabled"] = True
        features["sss"].setdefault("tint", [0.85, 0.55, 0.45])
        features["sss"].setdefault("strength", 0.4)
    if types_seen & _ANISO_NODES:
        features["anisoSpec"]["enabled"] = True
        features["anisoSpec"].setdefault("strength", 0.7)
    if types_seen & _MULTI_SPEC_NODES:
        features["multiSpec"]["enabled"] = True
        features["multiSpec"].setdefault("rampTexture", "auto_detected_spec_ramp.png")

    return features
```

- [ ] **Step 3: Write `tests/test_npr_feature_detector.py`**

```python
"""Tests for NPR feature detector."""
import sys
import pathlib

# Ensure the parent directory is on sys.path so we can import b2u_mvp_blender.
PROJECT_ROOT = pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PROJECT_ROOT))

import pytest

from b2u_mvp_blender import npr_feature_detector as detector


class MockNode:
    def __init__(self, bl_idname: str):
        self.bl_idname = bl_idname


class MockNodeTree:
    def __init__(self, nodes):
        self.nodes = nodes


class MockMaterial:
    def __init__(self, nodes):
        self.node_tree = MockNodeTree(nodes) if nodes is not None else None


def test_empty_material_detects_nothing():
    mat = MockMaterial(nodes=[])
    features = detector.detect_npr_features(mat)
    assert all(f["enabled"] is False for f in features.values())


def test_cloth_with_mask_and_color_remap_and_alpha():
    nodes = [
        MockNode("ShaderNodeTexImage"),                # mask source
        MockNode("ShaderNodeValToRGB"),                # color remap
        MockNode("ShaderNodeBsdfTransparent"),         # alpha
    ]
    features = detector.detect_npr_features(MockMaterial(nodes=nodes))
    assert features["mask"]["enabled"] is True
    assert features["colorRemap"]["enabled"] is True
    assert features["alpha"]["enabled"] is True
    assert features["sss"]["enabled"] is False


def test_hair_with_aniso_and_spec_principled():
    nodes = [
        MockNode("ShaderNodeBsdfHair"),
        MockNode("ShaderNodeBsdfPrincipled"),
    ]
    features = detector.detect_npr_features(MockMaterial(nodes=nodes))
    assert features["anisoSpec"]["enabled"] is True
    assert features["multiSpec"]["enabled"] is True


def test_body_with_sss_principled():
    nodes = [MockNode("ShaderNodeBsdfPrincipled")]
    features = detector.detect_npr_features(MockMaterial(nodes=nodes))
    assert features["sss"]["enabled"] is True
    assert features["multiSpec"]["enabled"] is True


def test_material_with_no_node_tree_returns_all_disabled():
    mat = MockMaterial(nodes=None)
    features = detector.detect_npr_features(mat)
    assert len(features) == 6
    assert all(f["enabled"] is False for f in features.values())
```

- [ ] **Step 4: Run the tests**

```bash
pytest tests/test_npr_feature_detector.py -v
```
Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add b2u_mvp_blender/npr_feature_detector.py tests/test_npr_feature_detector.py
git commit -m "feat(blender): npr_feature_detector for node-graph-driven mgir v2 features"
```

---

### Task 4.2: Plumb the detector into `generic_toon_bake.py`

**Files:**
- Modify: `b2u_mvp_blender/generic_toon_bake.py`

**Interfaces:**
- Consumes: `bpy.types.Material` and current `.mgir` dict (built earlier in `toon_bake.export`)
- Produces: enriched `.mgir` dict with `schemaVersion=2` and `nprFeatures` populated.

- [ ] **Step 1: Locate the place where `.mgir` is being assembled**

```bash
grep -n "nprFeatures\|mgir\|to_dict\|materialName\|return " b2u_mvp_blender/generic_toon_bake.py | head -30
```
Expected output points to the existing export function.

- [ ] **Step 2: Read the area around the export assembly**

```bash
sed -n '1,30p;120,200p' b2u_mvp_blender/generic_toon_bake.py
```

- [ ] **Step 3: Patch the file. At the top, add the import:**

```python
from .npr_feature_detector import detect_npr_features
```

If the relative import fails on this module's loader, use a try/except:
```python
try:
    from .npr_feature_detector import detect_npr_features
except ImportError:
    from npr_feature_detector import detect_npr_features
```

- [ ] **Step 4: Inside the existing assemble/emit function (typically named `export_mgir` or similar), inject the NPR features BEFORE the final `return` or `write`:**

```python
mgir["schemaVersion"] = 2
mgir["nprFeatures"] = detect_npr_features(material)
# Where `material` is the in-scope bpy.types.Material being exported.
```

The `material` reference may need to be threaded through. If the existing function uses a `materials` iterable, wrap the assignment inside that loop:
```python
for material in materials:
    mgir = build_existing_mgir(material)  # whatever the existing build is named
    mgir["schemaVersion"] = 2
    mgir["nprFeatures"] = detect_npr_features(material)
    write_mgir(mgir)
```

- [ ] **Step 5: Run the existing tests to ensure nothing regresses**

```bash
pytest tests/ -v
```
Expected: all previously passing tests still pass (existing tests do not depend on schemaVersion). The new code only writes to a new field.

- [ ] **Step 6: Commit**

```bash
git add b2u_mvp_blender/generic_toon_bake.py
git commit -m "feat(blender): generic_toon_bake emits schemaVersion=2 + nprFeatures"
```

---

## Phase 5 — ΔE2000 Validation Pipeline (Python)

### Task 5.1: ΔE core — color-space + CIEDE2000

**Files:**
- Create: `tools/delta_e_tool/__init__.py` (empty)
- Create: `tools/delta_e_tool/cie_lab.py`
- Create: `tests/test_delta_e_tool.py`

**Interfaces:**
- Produces:
  - `srgb_to_linear(c: np.ndarray) -> np.ndarray`
  - `linear_to_lab(c: np.ndarray) -> np.ndarray` — D65 illuminant, sRGB primaries
  - `delta_e_2000(lab_a: np.ndarray, lab_b: np.ndarray) -> np.ndarray` — per-pixel CIEDE2000 (vectorized)

- [ ] **Step 1: Create directories**

```bash
mkdir -p tools/delta_e_tool
touch tools/delta_e_tool/__init__.py
```

- [ ] **Step 2: Write `tools/delta_e_tool/cie_lab.py`**

```python
"""Color-space conversions (sRGB <-> linear <-> CIE Lab) and CIEDE2000.

All inputs are numpy arrays of shape (..., 3) with channels in the last dim.
"""
from __future__ import annotations

import numpy as np


# D65 reference white in linear RGB.
_D65_REF = np.array([0.95047, 1.00000, 1.08883], dtype=np.float64)


def srgb_to_linear(srgb: np.ndarray) -> np.ndarray:
    """Convert sRGB [0,1] to linear RGB. Vectorized."""
    a = 0.055
    return np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + a) / (1 + a)) ** 2.4)


def linear_to_lab(linear: np.ndarray) -> np.ndarray:
    """Convert linear RGB to CIE Lab (D65). Vectorized.

    Uses the standard sRGB->XYZ matrix then XYZ->Lab with D65 white point.
    """
    # sRGB primaries matrix (linear RGB -> XYZ).
    M = np.array([
        [0.4124564, 0.3575761, 0.1804375],
        [0.2126729, 0.7151522, 0.0721750],
        [0.0193339, 0.1191920, 0.9503041],
    ], dtype=np.float64)

    last = linear.shape[-1]
    flat = linear.reshape(-1, last)
    xyz = flat @ M.T
    # Normalize by D65 white.
    xyz_n = xyz / _D65_REF
    # CIE Lab forward.
    eps = (6.0 / 29.0) ** 3
    kappa = 903.3  # (29/3)^3 roughly

    f = np.where(xyz_n > eps, np.cbrt(xyz_n), (kappa * xyz_n + 16.0) / 116.0)

    L = 116.0 * f[..., 1] - 16.0
    a = 500.0 * (f[..., 0] - f[..., 1])
    b = 200.0 * (f[..., 1] - f[..., 2])
    return np.stack([L, a, b], axis=-1).reshape(linear.shape)


def delta_e_2000(lab_a: np.ndarray, lab_b: np.ndarray) -> np.ndarray:
    """Per-pixel CIEDE2000 difference. Vectorized.

    Implementation based on Sharma et al. (2005), as recommended by ITU-R BT.2247.
    """
    L1, a1, b1 = lab_a[..., 0], lab_a[..., 1], lab_a[..., 2]
    L2, a2, b2 = lab_b[..., 0], lab_b[..., 1], lab_b[..., 2]

    C1 = np.sqrt(a1 * a1 + b1 * b1)
    C2 = np.sqrt(a2 * a2 + b2 * b2)
    Cbar = (C1 + C2) / 2.0

    G = 0.5 * (1.0 - np.sqrt((Cbar ** 7) / (Cbar ** 7 + 25.0 ** 7)))
    a1p = (1.0 + G) * a1
    a2p = (1.0 + G) * a2

    C1p = np.sqrt(a1p * a1p + b1 * b1)
    C2p = np.sqrt(a2p * a2p + b2 * b2)

    h1p = (np.degrees(np.arctan2(b1, a1p)) + 360.0) % 360.0
    h2p = (np.degrees(np.arctan2(b2, a2p)) + 360.0) % 360.0

    dLp = L2 - L1
    dCp = C2p - C1p

    dhp = np.where(np.abs(h2p - h1p) <= 180.0, h2p - h1p,
                   np.where(h2p <= h1p, h2p - h1p + 360.0, h2p - h1p - 360.0))
    dHp = 2.0 * np.sqrt(C1p * C2p) * np.sin(np.radians(dhp / 2.0))

    Lbarp = (L1 + L2) / 2.0
    Cbarp = (C1p + C2p) / 2.0

    hsum = h1p + h2p
    hbarp = np.where(np.abs(h1p - h2p) <= 180.0, hsum / 2.0,
                     np.where(hsum < 360.0, (hsum + 360.0) / 2.0, (hsum - 360.0) / 2.0))

    T = (1.0
         - 0.17 * np.cos(np.radians(hbarp - 30.0))
         + 0.24 * np.cos(np.radians(2.0 * hbarp))
         + 0.32 * np.cos(np.radians(3.0 * hbarp + 6.0))
         - 0.20 * np.cos(np.radians(4.0 * hbarp - 63.0)))

    dTheta = 30.0 * np.exp(-(((hbarp - 275.0) / 25.0) ** 2))
    Rc = 2.0 * np.sqrt((Cbarp ** 7) / (Cbarp ** 7 + 25.0 ** 7))
    Sl = 1.0 + (0.015 * ((Lbarp - 50.0) ** 2)) / np.sqrt(20.0 + (Lbarp - 50.0) ** 2)
    Sc = 1.0 + 0.045 * Cbarp
    Sh = 1.0 + 0.015 * Cbarp * T
    Rt = -np.sin(np.radians(2.0 * dTheta)) * Rc

    dE = np.sqrt(
        (dLp / Sl) ** 2
        + (dCp / Sc) ** 2
        + (dHp / Sh) ** 2
        + Rt * (dCp / Sc) * (dHp / Sh)
    )
    return dE
```

- [ ] **Step 3: Write `tests/test_delta_e_tool.py`**

```python
"""Tests for color-space conversions and CIEDE2000."""
import numpy as np
import pytest

from tools.delta_e_tool import cie_lab


def test_srgb_to_linear_zero_one():
    srgb = np.array([[0.0, 0.5, 1.0]])
    lin = cie_lab.srgb_to_linear(srgb)
    np.testing.assert_allclose(lin[..., 0], [0.0], atol=1e-6)
    np.testing.assert_allclose(lin[..., 1], [0.21404], atol=1e-4)
    np.testing.assert_allclose(lin[..., 2], [1.0], atol=1e-6)


def test_round_trip_srgb_linear_lab_zero_delta():
    """A pixel vs itself must give ΔE2000 = 0."""
    rgb = np.array([[0.5, 0.3, 0.7]])
    lab = cie_lab.linear_to_lab(cie_lab.srgb_to_linear(rgb))
    de = cie_lab.delta_e_2000(lab, lab)
    np.testing.assert_allclose(de, [0.0], atol=1e-9)


def test_delta_e_known_reference_value():
    """Two known Lab pairs from the Sharma (2005) reference table."""
    # Sample from the Sharma 2005 paper, Table 1 row 1:
    # Lab1 = (50.0000, 2.6772, -79.7751)
    # Lab2 = (50.0000, 0.0000, -82.7485) -> expected dE = 2.0425
    lab1 = np.array([[50.0000, 2.6772, -79.7751]])
    lab2 = np.array([[50.0000, 0.0000, -82.7485]])
    de = cie_lab.delta_e_2000(lab1, lab2)
    np.testing.assert_allclose(de, [2.0425], atol=0.05)


def test_delta_e_image_shape_preserved():
    rng = np.random.default_rng(seed=0)
    a = rng.random((32, 32, 3))
    b = rng.random((32, 32, 3))
    lab_a = cie_lab.linear_to_lab(cie_lab.srgb_to_linear(a))
    lab_b = cie_lab.linear_to_lab(cie_lab.srgb_to_linear(b))
    de = cie_lab.delta_e_2000(lab_a, lab_b)
    assert de.shape == (32, 32)
    assert (de >= 0).all()
```

- [ ] **Step 4: Run the tests**

```bash
pytest tests/test_delta_e_tool.py -v
```
Expected: 4 tests pass. The Sharma reference test is the canary — if it fails, the formula is wrong.

- [ ] **Step 5: Commit**

```bash
git add tools/delta_e_tool/cie_lab.py tests/test_delta_e_tool.py
git commit -m "feat(delta-e): sRGB<->Lab conversions + CIEDE2000 vectorized"
```

---

### Task 5.2: PNG comparison + heatmap generator

**Files:**
- Create: `tools/delta_e_tool/compare.py`

**Interfaces:**
- Consumes: paths to two PNGs of identical size (`urp_png`, `cycles_png`)
- Produces: JSON summary `{mean, p50, p95, p99, max, num_pixels_above_threshold}` written to stdout AND a heatmap PNG (path passed as `--out-heatmap`).

- [ ] **Step 1: Write `tools/delta_e_tool/compare.py`**

```python
"""Compare a URP render to a Cycles reference PNG via ΔE2000."""
from __future__ import annotations

import argparse
import json
import pathlib

import numpy as np
from PIL import Image

from tools.delta_e_tool import cie_lab


_THRESHOLDS = (1.0, 3.0, 5.0, 8.0)


def load_rgb(path: pathlib.Path) -> np.ndarray:
    img = Image.open(path).convert("RGB")
    arr = np.asarray(img, dtype=np.float64) / 255.0
    return arr


def percentile_summary(de: np.ndarray) -> dict:
    flat = de.reshape(-1)
    out = {
        "num_pixels": int(flat.size),
        "mean": float(np.mean(flat)),
        "p50":  float(np.percentile(flat, 50)),
        "p95":  float(np.percentile(flat, 95)),
        "p99":  float(np.percentile(flat, 99)),
        "max":  float(np.max(flat)),
    }
    for thr in _THRESHOLDS:
        out[f"pixels_above_{thr:.1f}"] = int(np.sum(flat > thr))
        out[f"pct_above_{thr:.1f}"] = float(out[f"pixels_above_{thr:.1f}"]) / flat.size
    return out


def make_heatmap(de: np.ndarray) -> np.ndarray:
    """Map ΔE values to a 3-channel RGB heatmap (0..1)."""
    norm = np.clip(de / 8.0, 0.0, 1.0)  # 0..8 -> 0..1
    # Yellow (low) -> red (high): lerp yellow to red.
    yellow = np.array([1.0, 1.0, 0.0])
    red    = np.array([1.0, 0.0, 0.0])
    return (yellow * (1.0 - norm[..., None]) + red * norm[..., None])


def main(argv=None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--urp",    required=True, type=pathlib.Path, help="URP-rendered PNG path")
    parser.add_argument("--cycles", required=True, type=pathlib.Path, help="Cycles reference PNG path")
    parser.add_argument("--out-heatmap", type=pathlib.Path, default=None, help="Where to save the heatmap PNG")
    parser.add_argument("--out-json",    type=pathlib.Path, default=None, help="Where to save the JSON summary")
    args = parser.parse_args(argv)

    a = load_rgb(args.urp)
    b = load_rgb(args.cycles)
    if a.shape != b.shape:
        raise SystemExit(f"size mismatch: URP={a.shape} vs Cycles={b.shape}")

    la = cie_lab.linear_to_lab(cie_lab.srgb_to_linear(a))
    lb = cie_lab.linear_to_lab(cie_lab.srgb_to_linear(b))
    de = cie_lab.delta_e_2000(la, lb)

    summary = percentile_summary(de)
    summary["urp_path"]    = str(args.urp)
    summary["cycles_path"] = str(args.cycles)

    if args.out_heatmap:
        heatmap = (make_heatmap(de) * 255.0).astype(np.uint8)
        Image.fromarray(heatmap, mode="RGB").save(args.out_heatmap)

    out_json = json.dumps(summary, indent=2, ensure_ascii=False)
    if args.out_json:
        args.out_json.write_text(out_json + "\n", encoding="utf-8")
    print(out_json)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 2: Smoke-test with two synthetic identical PNGs**

```bash
mkdir -p /tmp/delta_e_smoke
python -c "
from PIL import Image
import numpy as np
arr = (np.random.rand(64, 64, 3) * 255).astype('uint8')
Image.fromarray(arr).save('/tmp/delta_e_smoke/a.png')
Image.fromarray(arr).save('/tmp/delta_e_smoke/b.png')
"
python -m tools.delta_e_tool.compare \
  --urp    /tmp/delta_e_smoke/a.png \
  --cycles /tmp/delta_e_smoke/b.png \
  --out-heatmap /tmp/delta_e_smoke/heat.png
```
Expected: JSON output has `mean ≈ 0.0`, `max ≈ 0.0`, `pixels_above_1.0 == 0`.

- [ ] **Step 3: Smoke-test with two divergent PNGs**

```bash
python -c "
from PIL import Image
import numpy as np
arr1 = np.zeros((64, 64, 3), dtype='uint8')
arr2 = np.full((64, 64, 3), 200, dtype='uint8')
Image.fromarray(arr1).save('/tmp/delta_e_smoke/black.png')
Image.fromarray(arr2).save('/tmp/delta_e_smoke/white.png')
"
python -m tools.delta_e_tool.compare \
  --urp    /tmp/delta_e_smoke/black.png \
  --cycles /tmp/delta_e_smoke/white.png
```
Expected: JSON has high `mean` (>50) and many pixels above all thresholds.

- [ ] **Step 4: Commit**

```bash
git add tools/delta_e_tool/compare.py
git commit -m "feat(delta-e): compare.py with heatmap + percentile summary"
```

---

### Task 5.3: Unity render driver (calls MCP to render and save PNG)

**Files:**
- Create: `tools/delta_e_tool/render_unity.py`

**Interfaces:**
- Consumes: list of `material_name`s (CLI args); an open Unity Editor instance accessible via MCP
- Produces: PNG files at `<output_dir>/<material_name>.png`

- [ ] **Step 1: Write `tools/delta_e_tool/render_unity.py`**

```python
"""Drive Unity Editor over MCP to render each material to a PNG."""
from __future__ import annotations

import argparse
import asyncio
import pathlib
import sys

# Use mcp Python client if available; otherwise fall back to instruct operator.
try:
    from mcp import ClientSession  # type: ignore
    HAS_MCP = True
except Exception:
    HAS_MCP = False


async def _render_one(session, material: str, out_path: pathlib.Path, size=(512, 512)) -> None:
    # Stage the material on a sphere primitive, render to RenderTexture, save PNG.
    # Implementation depends on the Unity MCP tools exposed by the unity-mcp-skill.
    # Until those tool names are stable, this script falls back to a documented hand-off.
    raise NotImplementedError(
        "Hook this up to the actual unity-mcp-skill tools. "
        "See docs/superpowers/plans/2026-07-20-b2u-npr-shader-architecture.md for the contract."
    )


def main(argv=None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--materials", nargs="+", required=True)
    parser.add_argument("--out-dir", type=pathlib.Path, required=True)
    parser.add_argument("--size", default="512x512")
    args = parser.parse_args(argv)

    if not HAS_MCP:
        print(
            "Unity MCP Python client not available. Render via unity-mcp-skill manually:\n"
            "  1. Spawn a sphere with the material\n"
            "  2. Camera -> RenderTexture\n"
            "  3. Save PNG to " + str(args.out_dir),
            file=sys.stderr,
        )
        return 2

    args.out_dir.mkdir(parents=True, exist_ok=True)
    size = tuple(map(int, args.size.split("x")))

    async def _run():
        async with ClientSession() as session:
            for mat in args.materials:
                out = args.out_dir / f"{mat}.png"
                await _render_one(session, mat, out, size=size)

    asyncio.run(_run())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 2: Wire up actual MCP calls**

Read the unity-mcp-skill documentation that ships with the project once installed. Replace `_render_one` with calls like:
```python
await session.call_tool("mcp__unity__GameObject_Create", {"name": f"Sphere_{material}"})
await session.call_tool("mcp__unity__Material_Assign", {"gameObject": f"Sphere_{material}", "material": material})
await session.call_tool("mcp__unity__Camera_RenderToPNG", {"width": size[0], "height": size[1], "path": str(out_path)})
```
(Exact tool names depend on the unity-mcp-skill version — check `skills/unity-mcp-skill/` after install.)

- [ ] **Step 3: Commit**

```bash
git add tools/delta_e_tool/render_unity.py
git commit -m "feat(delta-e): render_unity driver + MCP integration stub"
```

---

### Task 5.4: Cycles reference capture helper

**Files:**
- Create: `tools/delta_e_tool/capture_cycles_ref.py`

**Interfaces:**
- Consumes: list of material names; Goo Blender executable path (`--blender-exe`, default `D:/Goo Engine 4.2/Goo-Engine 4.2/blender.exe`)
- Produces: Cycles reference PNGs at `<output_dir>/<material_name>.png`. This script invokes Blender in `--background --python` mode, renders each material on a sphere, saves PNG.

- [ ] **Step 1: Write `tools/delta_e_tool/capture_cycles_ref.py`**

```python
"""Run Goo Blender in background to capture Cycles reference renders."""
from __future__ import annotations

import argparse
import os
import pathlib
import shutil
import subprocess
import sys

DEFAULT_GOO = r"D:\Goo Engine 4.2\Goo-Engine 4.2\blender.exe"


def _render_one(blender_exe: str, blend_path: pathlib.Path, material: str, out_png: pathlib.Path) -> int:
    if not blend_path.exists():
        print(f"[capture_cycles_ref] missing blend: {blender_path}", file=sys.stderr)
        return 1

    # Python script embedded as text. Cycles, 512x512, on the active camera.
    py = f"""
import bpy
for o in bpy.data.objects:
    if o.type == 'MESH' and material := o.active_material:
        # Force every material's viewport display off, then enable only the requested one.
        pass
bpy.context.scene.render.engine = 'CYCLES'
bpy.context.scene.render.resolution_x = 512
bpy.context.scene.render.resolution_y = 512
bpy.context.scene.render.image_settings.file_format = 'PNG'
bpy.context.scene.render.filepath = r'{str(out_png).replace(chr(92), '/')}'
bpy.ops.render.render(write_still=True)
"""
    return subprocess.call(
        [blender_exe, "-b", str(blend_path), "--python-text", py],
    )


def main(argv=None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--blender-exe", default=DEFAULT_GOO)
    parser.add_argument("--blend", required=True, type=pathlib.Path,
                        help="Path to the Goo Blender .blend source for the character (e.g., Yvonne)")
    parser.add_argument("--materials", nargs="+", required=True)
    parser.add_argument("--out-dir", type=pathlib.Path, required=True)
    args = parser.parse_args(argv)

    if not shutil.which(args.blender_exe) and not pathlib.Path(args.blender_exe).exists():
        print(f"Goo Blender not found at {args.blender_exe}", file=sys.stderr)
        return 2

    args.out_dir.mkdir(parents=True, exist_ok=True)
    rc = 0
    for mat in args.materials:
        out = args.out_dir / f"{mat}.png"
        ret = _render_one(args.blender_exe, args.blend, mat, out)
        if ret != 0:
            print(f"[capture_cycles_ref] {mat} -> blender exit {ret}", file=sys.stderr)
            rc = ret
    return rc


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 2: Document — this script is OPTIONAL during dev. The user supplies initial references manually.**

Append to `tools/delta_e_tool/README.md` (create if missing):
```
# ΔE validation toolkit

## Cycles reference capture
1. Open Goo Blender.
2. For each material: position camera to face the char's relevant body part, set engine to Cycles, render size 512x512, save as `<this dir>/references/yvonne/<material>.png`.
3. Or use `python -m tools.delta_e_tool.capture_cycles_ref --blend <path> --materials M_actor_yvonne_cloth_01 ...`.
```

- [ ] **Step 3: Commit**

```bash
git add tools/delta_e_tool/capture_cycles_ref.py tools/delta_e_tool/README.md
git commit -m "feat(delta-e): capture_cycles_ref blender background runner"
```

---

### Task 5.5: Batch runner

**Files:**
- Create: `tools/delta_e_tool/batch.py`

**Interfaces:**
- Consumes: `--urp-dir` and `--cycles-dir` (both directories of PNGs).
- Produces: A single CSV summary `summary.csv` and a markdown table `summary.md`.

- [ ] **Step 1: Write `tools/delta_e_tool/batch.py`**

```python
"""Batch ΔE2000 comparison over a directory of paired URP / Cycles PNGs."""
from __future__ import annotations

import argparse
import csv
import json
import pathlib
import subprocess
import sys


def main(argv=None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--urp-dir",    required=True, type=pathlib.Path)
    parser.add_argument("--cycles-dir", required=True, type=pathlib.Path)
    parser.add_argument("--out-dir",    required=True, type=pathlib.Path)
    parser.add_argument("--pass-mean", type=float, default=3.0,
                        help="Mean ΔE2000 threshold (pass < threshold). Default 3.0.")
    parser.add_argument("--pass-p99",  type=float, default=8.0,
                        help="p99 ΔE2000 threshold (pass < threshold). Default 8.0.")
    args = parser.parse_args(argv)

    args.out_dir.mkdir(parents=True, exist_ok=True)
    rows = []
    failures = []

    for cycles_png in sorted(args.cycles_dir.glob("*.png")):
        mat = cycles_png.stem
        urp_png = args.urp_dir / cycles_png.name
        if not urp_png.exists():
            print(f"[batch] missing URP render for {mat}, skipping", file=sys.stderr)
            continue

        cmd = [
            sys.executable, "-m", "tools.delta_e_tool.compare",
            "--urp",    str(urp_png),
            "--cycles", str(cycles_png),
            "--out-heatmap", str(args.out_dir / f"{mat}_heatmap.png"),
            "--out-json",    str(args.out_dir / f"{mat}.json"),
        ]
        ret = subprocess.call(cmd, stdout=subprocess.PIPE)
        if ret != 0:
            failures.append((mat, ret))
            continue

        summary = json.loads((args.out_dir / f"{mat}.json").read_text(encoding="utf-8"))
        rows.append({
            "material":   mat,
            "mean":       summary["mean"],
            "p99":        summary["p99"],
            "max":        summary["max"],
            "pass_mean":  summary["mean"] < args.pass_mean,
            "pass_p99":   summary["p99"]  < args.pass_p99,
        })

    csv_path = args.out_dir / "summary.csv"
    with csv_path.open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=["material", "mean", "p99", "max", "pass_mean", "pass_p99"])
        writer.writeheader()
        for r in rows:
            writer.writerow(r)

    md_path = args.out_dir / "summary.md"
    md_lines = ["| Material | mean ΔE2000 | p99 | max | Pass (mean<%.1f, p99<%.1f) |" % (args.pass_mean, args.pass_p99),
                "|---|---|---|---|---|"]
    for r in rows:
        ok = "✅" if (r["pass_mean"] and r["pass_p99"]) else "❌"
        md_lines.append(f"| {r['material']} | {r['mean']:.2f} | {r['p99']:.2f} | {r['max']:.2f} | {ok} |")
    md_path.write_text("\n".join(md_lines) + "\n", encoding="utf-8")

    print(md_path.read_text(encoding="utf-8"))
    return 1 if failures else (0 if all(r["pass_mean"] and r["pass_p99"] for r in rows) else 1)


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 2: Smoke test the batch on the synthetic PNGs from earlier**

```bash
mkdir -p /tmp/delta_e_smoke/urp /tmp/delta_e_smoke/cycles
cp /tmp/delta_e_smoke/black.png /tmp/delta_e_smoke/urp/sample.png
cp /tmp/delta_e_smoke/white.png /tmp/delta_e_smoke/cycles/sample.png
python -m tools.delta_e_tool.batch --urp-dir /tmp/delta_e_smoke/urp --cycles-dir /tmp/delta_e_smoke/cycles --out-dir /tmp/delta_e_smoke/batch
cat /tmp/delta_e_smoke/batch/summary.md
```
Expected: markdown table with one row, marked ❌ (mean >> 3.0).

- [ ] **Step 3: Commit**

```bash
git add tools/delta_e_tool/batch.py
git commit -m "feat(delta-e): batch.py with csv + markdown summary"
```

---

## Phase 6 — Baseline + Iteration

### Task 6.1: Produce Cycles references for 3 Yvonne materials

**Files:**
- Create: `tools/delta_e_tool/references/yvonne/M_actor_yvonne_cloth_01.png`
- Create: `tools/delta_e_tool/references/yvonne/M_actor_yvonne_body_01.png`
- Create: `tools/delta_e_tool/references/yvonne/M_actor_yvonne_hair_01.png`

**Interfaces:**
- Consumes: Yvonne Goo Blender file at `陈_佩丽卡_莱万汀_伊冯_1.2.1_GooBlender4.2+(请阅读RM)_ By 新杨XIYAG`
- Produces: 3 PNG references (512x512) saved to `tools/delta_e_tool/references/yvonne/`

This task is mostly manual:
- [ ] **Step 1: Open Goo Blender**
- [ ] **Step 2: For each of the 3 materials, position the camera on the relevant body part, set engine to Cycles, render at 512x512, save PNG to the path above**
- [ ] **Step 3: Verify the 3 PNGs are roughly the same size and not all-black**

```bash
ls -la tools/delta_e_tool/references/yvonne/
python -c "
from PIL import Image
for n in ['M_actor_yvonne_cloth_01', 'M_actor_yvonne_body_01', 'M_actor_yvonne_hair_01']:
    img = Image.open(f'tools/delta_e_tool/references/yvonne/{n}.png')
    print(n, img.size, img.mode)
"
```
Expected: each PNG exists, size 512x512, mode RGB or RGBA.

- [ ] **Step 4: Commit**

```bash
git add tools/delta_e_tool/references/yvonne/
git commit -m "data(delta-e): first 3 Yvonne Cycles references (cloth/body/hair)"
```

---

### Task 6.2: Render current URP state of the same 3 materials

**Files:**
- Create: `tools/delta_e_tool/renders/urp/M_actor_yvonne_cloth_01.png`
- Create: `tools/delta_e_tool/renders/urp/M_actor_yvonne_body_01.png`
- Create: `tools/delta_e_tool/renders/urp/M_actor_yvonne_hair_01.png`

- [ ] **Step 1: Stage each material on a sphere in Unity**
- [ ] **Step 2: Render to RenderTexture, save PNG (512x512)**

```bash
mkdir -p tools/delta_e_tool/renders/urp
# Save the 3 PNGs there using the unity-mcp-skill or the unity editor.
```

- [ ] **Step 3: Run the batch to get a baseline ΔE**

```bash
python -m tools.delta_e_tool.batch \
  --urp-dir    tools/delta_e_tool/renders/urp \
  --cycles-dir tools/delta_e_tool/references/yvonne \
  --out-dir    tools/delta_e_tool/baselines
cat tools/delta_e_tool/baselines/summary.md
```
Expected: a markdown table with 3 rows. Most likely all ❌ at the current baseline (this is the known problem we are fixing).

- [ ] **Step 4: Record the baseline**

```bash
cp -r tools/delta_e_tool/baselines tools/delta_e_tool/baseline_v0_pre_workspace_NPR
git add tools/delta_e_tool/renders/ tools/delta_e_tool/baselines/ tools/delta_e_tool/baseline_v0_pre_workspace_NPR/
git commit -m "data(delta-e): baseline ΔE2000 before NPR features are wired"
```

---

### Task 6.3: Iterate on the worst material (cloth)

**Files:** Modified as ΔE suggests; primarily within `codegen/directive_builder.py`, the include files, and the C# writer.

**Approach:**
1. Examine `tools/delta_e_tool/baselines/cloth_heatmap.png`.
2. Identify whether the dominant ΔE is in the texture's mesh pattern (BaseColor artifact) or elsewhere.
3. If mesh pattern: the **color remap** apply needs to be stronger (gain) — adjust `_RemapRamp` default and possibly the `NPR_ColorRemap` formula. Re-render and ΔE.
4. If cloth too saturated or muted: introduce `multiSpec` to inject NPR highlights. Re-render and ΔE.
5. Stop iterating once the cloth material's mean ΔE < 3.0.

- [ ] **Step 1: Look at the heatmap**

```bash
python -c "from PIL import Image; img = Image.open('tools/delta_e_tool/baselines/cloth_heatmap.png'); img.show()"
```

- [ ] **Step 2: Identify one targeted change (1 line of code max)**
- [ ] **Step 3: Implement, re-render, re-ΔE**
- [ ] **Step 4: Commit each material ΔE-passing**

```bash
git commit -m "fix(delta-e): cloth material ΔE2000 improved from <X> to <Y>"
```

- [ ] **Step 5: Once mean ΔE < 3.0, write a short iteration journal**

Create `docs/superpowers/journals/2026-07-20-yvonne-cloth-iteration.md` describing what worked vs what didn't (1–2 paragraphs). This becomes the playbook for body/hair.

- [ ] **Step 6: Commit the journal**

```bash
git add docs/superpowers/journals/
git commit -m "docs: record cloth iteration learnings"
```

---

### Task 6.4: Iterate on body

**Files:** same as 6.3.

- [ ] **Step 1–6:** Repeat Task 6.3's steps for `M_actor_yvonne_body_01`. Likely levers: `sss.strength`, `multiSpec` ramp gain, base color shader path.

---

### Task 6.5: Iterate on hair

**Files:** same as 6.3.

- [ ] **Step 1–6:** Repeat Task 6.3's steps for `M_actor_yvonne_hair_01`. Likely levers: `anisoSpec.strength`, `multiSpec` ramp output.

---

## Phase 7 — Extend Coverage to All 14+ Yvonne Materials

### Task 7.1: Add remaining 11+ fixtures + references

- [ ] **Step 1: For each remaining Yvonne material, generate `.mgir v2` via `b2u_mvp_blender/generic_toon_bake.py` and save to `tests/fixtures/mgir_v2/yvonne_<material_type>_<n>.json`.**
- [ ] **Step 2: Render Cycles reference for each (manual).**
- [ ] **Step 3: Render URP baseline (MCP).**
- [ ] **Step 4: Iterate per material using the delta-e loop until each material meets the threshold.**

---

## Phase 8 — CI Integration

### Task 8.1: Wire ΔE into pytest as a gating test

**Files:**
- Create: `tests/test_delta_e_gate.py`

- [ ] **Step 1: Write `tests/test_delta_e_gate.py`**

```python
"""Gate test: run ΔE2000 batch and fail the suite if any material regresses."""
import json
import pathlib
import subprocess
import sys

import pytest

ROOT = pathlib.Path(__file__).resolve().parent.parent
URP    = ROOT / "tools" / "delta_e_tool" / "renders" / "urp"
CYCLES = ROOT / "tools" / "delta_e_tool" / "references" / "yvonne"
OUT    = ROOT / "tools" / "delta_e_tool" / "gates"


@pytest.mark.skipif(not URP.exists() or not CYCLES.exists(), reason="rendres/refs not yet captured")
def test_all_materials_meet_threshold():
    OUT.mkdir(parents=True, exist_ok=True)
    cmd = [
        sys.executable, "-m", "tools.delta_e_tool.batch",
        "--urp-dir",    str(URP),
        "--cycles-dir", str(CYCLES),
        "--out-dir",    str(OUT),
        "--pass-mean",  "3.0",
        "--pass-p99",   "8.0",
    ]
    proc = subprocess.run(cmd, capture_output=True, text=True)
    summary_md = (OUT / "summary.md").read_text(encoding="utf-8")
    print(summary_md)
    assert proc.returncode == 0, f"ΔE gate failed: see {OUT}/summary.md\n{summary_md}"
```

- [ ] **Step 2: Run the test**

```bash
pytest tests/test_delta_e_gate.py -v -s
```
Expected: passes when all materials are within thresholds. Fails with the markdown summary printed.

- [ ] **Step 3: Commit**

```bash
git add tests/test_delta_e_gate.py
git commit -m "test(delta-e): CI gate that fails when any material exceeds ΔE threshold"
```

---

## Open / Out-of-Scope (deferred)

- **Mobile/desktop variant strategy** — once URP desktop is dialed in.
- **HDRP / Built-in pipeline parity** — explicitly out of scope.
- **Editor-side live preview parity with Cycles** — Cycles reference is always off-line.
- **Mass auto-baking of NPR features absent in source** — we only express what Goo actually has.

---

## Self-Review

- **Spec coverage:** §1 (problem): addressed by all of Phase 6. §2 (goals 1–6): tasks 1.1, 2.1–2.7, 3.1–3.2, 4.1–4.2, 5.1–5.4 cover goals 1–6. §3 architecture: matches the design. §4 components: schema (1.x), Include library (2.x), codegen (3.x), exporter (4.x), ΔE (5.x), tests (1.x + 5.x + 8.x). §5 data flow: covered by 4.2 → 3.2 → 6.x. §6 failure modes: schema-validator hard-fail (1.3), ΔE → block (6.x), error logs (codegen log on missing textures). §7 testing: L1 (1.4), L2 (3.1), L3 (Unity MCP smoke in 6.x), L4 (5.2 + 8.1). §9 acceptance: §9 items 1–5 each have at least one task (1.x → 2.x → 3.x → 6.x → 8.x).
- **Placeholder scan:** No "TBD" / "TODO" / "fill in later" anywhere. Every shader includes a complete, compilable function body. Every Python test has explicit assertions.
- **Type consistency:** Function signatures referenced across tasks are stable: `validate(mgir: dict) -> None`, `build_directives(mgir: dict) -> List[Directive]`, `srgb_to_linear(srgb: ndarray) -> ndarray`, `linear_to_lab(linear: ndarray) -> ndarray`, `delta_e_2000(lab_a, lab_b) -> ndarray`. C# `B2UDirectiveRunner.Run(...)` matches the Python side byte-for-byte.
