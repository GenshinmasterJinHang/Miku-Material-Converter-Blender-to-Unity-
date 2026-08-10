# Miku 2.3.0 Genshin tutorial-conformance repair

## Purpose and outcome

Align the existing `MIKU/Genshin/*` ShaderLab backends with the attached
Genshin character-shader tutorial and the `URP_GenshinImpact-main` reference
project, then validate with the reference project's Furina (芙宁娜) model and
textures. The tutorial's observable contracts that were missing are added as
additive public material properties; Miku's architecture (split Body/Hair/
Face/Eye shaders, URP forward rendering, UV7 TangentSpaceV2 outlines, and
screen-depth rim) is preserved and the remaining intentional deviations are
recorded in the audit.

## Context and constraints

- Canonical source roots only:
  `unity/Packages/com.miku.shaderconverter/Runtime/Genshin/`,
  `unity/Packages/com.miku.shaderconverter/Runtime/GameToon/`,
  `unity/Packages/com.miku.shaderconverter/Editor/`,
  `unity/Packages/com.miku.shaderconverter/Tests/Editor/`, `tests/`, `docs/`.
- The worktree already contains extensive uncommitted 2.3.0 work. Preserve it;
  edits are narrowly additive.
- Validation uses the local `Desktop/unity/test` Unity project through the
  port-8080 Unity MCP server, Unity 6000.4.5f1, URP/Shader Graph 17.4.0.
- The reference project is Unity 2022.3.38f1 / URP 14.0.11. Its
  `URP-HighFidelity-Renderer` uses a `RenderObjects` feature with
  `PassNames = head/back/edge`, `Event = 300`, and LayerMask Everything.
- Furina's FBX has UV0, UV1, and a `colorSet0` corner color attribute:
  R 0..1, G 0..0.216, B ~0.216, A 0..0.502. The tutorial outline mask is the
  A channel; Miku's legacy green width mask is effectively empty on this
  asset, so the Genshin outline pass must read A with a G fallback.
- Hu Tao (existing validation asset) has no color attribute and only UVMap,
  so A/G fallback and UV1 fallback must leave it unchanged.

## Implemented changes

1. `GenshinCommon.hlsl`
   - `Genshin_OutlineVertexMask`: tutorial A-channel outline width with a
     zero-A fallback to the Miku green mask.
   - `Genshin_OutlineRegionColor`: tutorial `lightmap.a` five-region outline
     colors (`step(0.25/0.45/0.65/0.95)` lerp chain), selected by
     `_OutlineColorMode`; legacy base-color tint path stays at mode 0.
   - `Genshin_TutorialRampRow` plus `Genshin_RampRowParams`: per-material
     `_LightmapA0..A4` rows with the tutorial `a * -0.1 + 1.05` mapping and
     `_InNight` offset. `Genshin_ReferenceRampRow` remains as a wrapper with
     the tutorial defaults (1, 4, 3, 5, 2).
   - `Genshin_DiffuseAlphaEmission` and `Genshin_DiffuseAlphaClip`: tutorial
     `diffuse.a` self-emission (flicker + HDR glow) and alpha cutout modes.
   - Body/Hair/Face diffuse functions accept the ramp-row parameters.
2. `MikuGameToonOutline.hlsl`
   - Additive `MikuGameToonOutlinePositionCSWithVertexMask` overload so the
     Genshin family can supply the tutorial A-channel mask while the shared
     green-mask path remains untouched for the other families.
3. Genshin Body/Hair/Face shaders
   - `_DiffuseA` (`0` off, `1` cutout, `2` flickering emission), `_Cutoff`,
     `[HDR] _Glow`, `_Flicker`.
   - `_LightmapA0..A4` ramp rows (defaults 1/4/3/5/2).
   - `_OutlineColor0..4`, `_OutlineColorMode`, and A-channel outline mask.
   - Body and Hair: `_GENSHIN_DOUBLE_SIDED` keyword with `Cull Off` and
     per-face UV selection; `_BackUV1` selects UV1 for back faces. When
     disabled, Cull Back behavior is unchanged.
   - Outline passes also run the `_DiffuseA==1` cutout.
4. Tests and docs
   - Python source-contract test `tests/test_genshin_tutorial_conformance.py`.
   - Unity EditMode `MikuGenshinShaderMath` mirrors plus
     `MikuGenshinTutorialTests` (source contracts and keyword compilation).
   - Plan, audit, package/root changelogs, compatibility notes.

## Validation

- [x] `python tools/ci/run_checks.py --profile pr`: 275/275 tests passed,
  Ruff clean, identity 13/13, deterministic TGZ double build identical.
- [x] Unity EditMode: `MikuGenshinTutorialTests` 7/7 passed and
  `MikuGameToonOutlineTests` + `MikuGameToon225Tests` 37/37 passed;
  `ShaderUtil.ShaderHasError` false for Body/Hair/Face.
- [x] Furina imported into `Desktop/unity/test`: four Miku Genshin materials
  bound, double-sided UV1 and tutorial outline/emission modes enabled, and
  front/back/close screenshots captured for the user.
- [x] Follow-up repair: `_AREA_SKIN` skin-tone curve gated by the LightMap
  skin mask (fixes purple cape backs), Body/Hair optional normal mapping
  (`_NormalMap`/`_BumpScale`/`_GENSHIN_NORMALMAP_ON`), `NormalMap` genshin
  texture role, Hu Tao materials unified to the same double-sided/outline
  behavior without normal maps. Python PR 276/276, Unity 9/9 + 37/37, and
  Furina back/front + Hu Tao front/back screenshots captured.

## Known deviations (intentional, recorded in the audit)

- Specular/metal use Miku's graphic lobe + sphere tint instead of the
  tutorial's Blinn-Phong/MatCap formulas.
- Rim uses the shared screen-depth `MikuToonCharacterMask` pass; the tutorial
  itself approximates the game's screen-depth rim with a Fresnel step.
- Face SDF uses the Miku head-basis projected-light reconstruction; the
  tutorial's fixed-up + mirrored-SDF variant is its documented limitation.
- Outline smooth normals use UV7 TangentSpaceV2 instead of tangent-channel
  data baked by the tutorial's tool (ADR 0016).
