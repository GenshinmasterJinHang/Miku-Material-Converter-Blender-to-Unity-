# Compatibility matrix

| Blender | Unity Editor | URP | Shader Graph | Miku | OS | Status |
| --- | --- | --- | --- | --- | --- | --- |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.2.8 | Windows D3D11/D3D12 | Experimental |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.2.7 | Windows D3D11/D3D12 | Experimental |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.2.6 | Windows D3D11 | Experimental |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.2.5 | Windows D3D11 | Experimental |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.2.4 | Windows D3D11 | Experimental |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.2.3 | Windows D3D11 | Experimental |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.2.2 | Windows D3D11 | Experimental |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.2.1 | Windows D3D11 | Experimental |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.2.0 | Windows D3D11 | Experimental |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.1.0 | Windows D3D11 | Experimental |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.0.0 | Windows D3D11 | Experimental |
| Any other tuple | Any | Any | Any | 2.2.8 | Any | Unsupported |

## Workflow matrix

| `workflow.kind` | Backend | Outputs |
| --- | --- | --- |
| `standard_pbr` | Shader Graph 17.4 adapter | Editable wrapper, generated Sub Graph, base material, optional user variant |
| `genshin_toon` | Static game Toon ShaderLab/HLSL | Game material, recipe, user variant; Body/Hair/Face Screen Rim |
| `wuwa_toon` | Static game Toon ShaderLab/HLSL | Game material, recipe, user variant; Body/Hair/Face Screen Rim; Effect excluded |
| `hsr_toon` | Static game Toon ShaderLab/HLSL | Game material, recipe, user variant; Body/Hair/Face Screen Rim |
| `endfield_toon` | Static game Toon ShaderLab/HLSL | User-owned templates for Body/Skin/Hair/Face/Eye/Mouth/Overlay/Effect/HairShadow; Body/Skin/Hair/Face Screen Rim |

Endfield 2.2.5 retains the 2.2.4 validation with local axes Right `+X`, Forward `-Y`, and Up
`+Z`. The renderer object-to-world matrix supplies the world-space basis, so
whole-object rotation, non-uniform scale, and negative scale do not require a
head-bone binder. The supplied 1024x32 skin and cloth LUTs are interpreted as
flattened 32x32x32 color LUTs.

Endfield 2.2.5 retains all 2.2 texture roles, including the 2.2.3
`SpecularRefineF0` and `SpecularRefineColor` additions. A valid matching
directional Main Light remains usable
when URP reports zero per-object distance attenuation; Rendering Layers still
exclude the light. Face SDF falls back to geometric lighting when invalid,
Iris/Sclera remain explicit per-material roles, and specular AO has a bounded
floor. The anime cornea remains a single-layer shading approximation rather
than refractive eye geometry. Hair can explicitly interpret a red-only lookup
as a scalar highlight weight; Body metal exposes independent direct/probe
boosts; Body, Skin, Face, and Hair expose a bounded surface rim; and Skin/Face
expose compatibility-neutral warm-pale grading.

Miku 2.2.5 adds mask-driven SSS to Genshin, HSR, and Wuwa without adding IR or
texture roles. It is validated only against the exact Unity/URP/Shader Graph
tuple above. The shared anime Volume Profile is a scene-authoring asset, not a
runtime installer, and uses only URP 17.4 public Volume components.

Miku 2.2.6 repairs Wuwa material fidelity. Eye uses one linear RGB `EyeHET`
mask sampled twice with independent upper/lower UV transforms and separate
base/highlight emission controls. Face exposes object-space Right/Up/Forward
axes transformed by the renderer object matrix while preserving runtime
world-space binder overrides. Body recognizes the authored linear
`ID -> Greater Than 0.5` stocking chain and samples that ID texture only once.
No new texture role or schema version is introduced.

Miku 2.2.7 supersedes the 2.2.6 Wuwa Eye approximation. `EyeHET` is direct
emission; `EyeHDMF`, `EyeUpperHighlight`, and `EyeLowerHighlight` are additive
fixed-workflow roles. Material bindings may carry a static UV0 `Affine2D`
matrix. Bundle schema remains 1.0, but a bundle containing these additions
requires the paired 2.2.7 importer. Optional EG follows the directional Main
Light in tangent space and falls back to zero movement when tangents are
invalid.

The shared 2.2.7 URP Volume Profile keeps White Balance, channel mixing, hue,
and color filters neutral. Its master luminance curve, Exposure `+0.35`,
Contrast `+16`, Saturation `+8`, and white Bloom at threshold `0.85` are
validated on the same Unity/URP tuple and remain scene-authoring controls.

Generic Toon is retired in Miku 2.0. Old Generic Toon inputs fail explicitly
with `MIKU_WORKFLOW_RETIRED:generic_toon`; no automatic visual conversion is
provided. Existing project assets are not deleted, although their old shaders
may appear as Missing Shader after package upgrade.

## Format policy

New writes use `miku-material-ir-2.0`. MaterialIR 1.0 is frozen and remains
importable only for `standard_pbr`, `genshin_toon`, `wuwa_toon`, and `hsr_toon`.
Bundle, conversion plan, manifest, source-map, and target-profile schemas remain
1.0. Unknown schema versions are rejected.

Blender extension 2.2.8 writes `miku-bake-request-1.1` with a certified 512,
1024, 2048, or 4096 2D bake resolution. Its bundled worker also accepts frozen
`miku-bake-request-1.0` requests at 1024. Older workers reject request 1.1 and
must not be paired with the 2.2.8 exporter. Bake result 1.0 and all Unity-facing
schemas remain unchanged.

Miku 2.2.8 Blender exports reject effective `Input.Time.*` dependencies before
creating output or bake-request files. Disconnected time nodes are allowed.
The Unity importer continues to accept historical MiGR and time-dependent
Bundles; this policy does not upgrade MaterialIR, Bundle, or Unity shader
schemas.

Unity 2.2.8's Miku editor language is a per-user `EditorPrefs` choice at
`com.miku.shaderconverter.editorLanguage` (`en_US` or `zh_HANS`). It is
independent of the Unity Editor locale and is not serialized into projects or
generated assets.

Legacy MiGR documents are normalized only when they describe one of the frozen
supported workflows. MiGR Generic Toon is rejected without semantic guessing.
