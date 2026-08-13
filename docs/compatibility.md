# Compatibility matrix

| Blender | Unity Editor | URP | Shader Graph | Miku | OS | Status |
| --- | --- | --- | --- | --- | --- | --- |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 3.0.0 | Windows Direct3D 12 | Supported; exact final TGZ full EditMode passed 335/347 with 0 failures and 12 documented GPU/optional skips; the same TGZ passed all 10 required D3D12 tests with 0 failures, skips, or inconclusive results under `-force-d3d12` without `-nographics` |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.4.0 | Windows Direct3D 12 | Unpublished candidate; superseded by 3.0.0 |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.3.0 | Windows D3D11 | Experimental; source-linked isolated Unity 53/53 and deterministic TGZ passed; final-TGZ port-8080 suite/scene/visual acceptance pending |
| 5.0.1 | 6000.0.81f1 | 17.0.4 | 17.0.4 | 2.2.12 | Windows | Experimental; official-document adapter, exact Unity lane not executed |
| 5.1.2 | 6000.1.17f1 | 17.1.0 | 17.1.0 | 2.2.12 | Windows | Experimental; official-document adapter, exact Unity lane not executed |
| 5.2.0 | 6000.2.15f1 | 17.2.0 | 17.2.0 | 2.2.12 | Windows | Experimental; official-document adapter, exact Unity lane not executed |
| 5.2.0 | 6000.3.21f1 | 17.3.0 | 17.3.0 | 2.2.12 | Windows | Experimental; official-document adapter, exact Unity lane not executed |
| 5.2.0 | 6000.4.12f1 | 17.4.0 | 17.4.0 | 2.2.12 | Windows | Experimental; official-document adapter, exact Unity lane not executed |
| 5.2.0 | 6000.5.7f1 | 17.5.4 | 17.5.4 | 2.2.12 | Windows | Experimental; official-document adapter, exact Unity lane not executed |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 2.2.12 | Windows D3D11 | Supported; final TGZ, 215 EditMode tests, 213 passed, 0 failed, 2 skipped |
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
| Blender <5.0 or >=5.3; Unity outside 6000.0-6000.5; URP/SG outside 17.0-17.5; prerelease | Any | Any | Any | 2.2.12 through 3.0.0 | Any | Unsupported (hard fail before asset writes) |
| Mismatched Unity/URP/Shader Graph technical lines or unequal URP/SG package versions | Any | Any | Any | 2.2.12 through 3.0.0 | Any | Unsupported via `MIKU_UNITY_PACKAGE_VERSION_MISMATCH` before asset writes |
| Any other tuple | Any | Any | Any | 2.2.8 and earlier | Any | Unsupported |

## Miku 2.2.12 through 3.0.0 technical-line adapters

| Unity Editor | URP / Shader Graph | Adapter |
| --- | --- | --- |
| 6000.0.x | 17.0.x | `ShaderGraph17_0Adapter` |
| 6000.1.x | 17.1.x | `ShaderGraph17_1Adapter` |
| 6000.2.x | 17.2.x | `ShaderGraph17_2Adapter` |
| 6000.3.x | 17.3.x | `ShaderGraph17_3Adapter` |
| 6000.4.x | 17.4.x | `ShaderGraph17_4Adapter` |
| 6000.5.x | 17.5.x | `ShaderGraph17_5Adapter` |

| Blender runtime | Adapter | Windows validation |
| --- | --- | --- |
| 5.0.1 | `Blender50Adapter` | Supported; final ZIP installed, 8 public headless smoke scripts passed |
| 5.1.2 | `Blender51Adapter` | Supported; final ZIP installed, 8 public headless smoke scripts passed |
| 5.2.0 | `Blender52Adapter` | Supported; final ZIP installed, 8 public headless smoke scripts passed |

The UPM manifest declares only the install floors `unity: 6000.0` and URP
`17.0.0`, because UPM package dependencies do not support version ranges.
Each project must directly lock its matching URP and Shader Graph pair.

The Unity adapter mapping follows Unity's official Shader Graph documentation
version selector (`17.0 -> 6000.0` through `17.5 -> 6000.5`). Unity's package
manifest documentation defines `unity` as a minimum Editor version and states
that dependency values are exact SemVer values rather than range syntax:

- [Unity 6000.0 package manifest](https://docs.unity3d.com/cn/6000.0/Manual/upm-manifestPkg.html)
- [Shader Graph 17.0 documentation](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/index.html)
- [Shader Graph 17.5 documentation](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.5/manual/index.html)
- [Blender extension manifest rules](https://docs.blender.org/manual/en/latest/advanced/extensions/getting_started.html)

Per the requested documentation-based acceptance boundary, exact Unity editor
installs are not required for the six new technical-line adapters. Those rows
remain Experimental until an external matrix executes them. The existing local
6000.4.5f1 regression is the only Unity row marked Supported.

Miku 2.3.0 retains the same bounded adapter/version policy. Its current row is
Experimental because the 53/53 Unity result was produced in a source-linked
isolated project, not by installing the final TGZ into the port-8080 target and
executing the scene/D3D11 acceptance. Python 262/262, Ruff, identity 13/13,
Blender 5.2.0 8/8 plus final-ZIP installed smoke, and two byte-identical TGZ
builds are recorded in the 2.3.0 release note; they do not promote the Unity
scene row to Supported.

## Public workflow matrix

| Surface | Supported choices | Contract |
| --- | --- | --- |
| Blender current-material panel | Standard PBR only | Emits `standard_pbr`; no game-workflow or texture-role guessing |
| Unity Game Toon creator | Genshin, Wuwa, HSR, Endfield | Explicit public 2D texture fields, filtered material parts, user-owned `.mat` |
| Python/legacy API | Explicit historical workflow values | Retained for scripts, old fixtures, and historical Bundle imports |

The Unity creator uses these ordered parts: Genshin (Body, Hair, Face, Eye),
Wuwa (Body, Hair, Face, Eye, Effect), HSR (Body, Hair, Face, Eye), and Endfield
(Body, Skin, Hair, Face, Eye, Mouth, Overlay, Effect, HairShadow). `_BaseMap`
is required except for Endfield Mouth. Wuwa Body's visible ID / Stockings Map
binds both `_IDMap` and `_StockingsMap`.

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

The current HSR tutorial-alignment work remains Experimental on Unity
6000.4.5f1 with URP/Shader Graph 17.4.0. Body/Hair LightMap green now follows
the literal `saturate(4 * HL * G)` Shadow AO signal and the fixed
`0.85 * signal + 0.15` ramp coordinate. LightMap blue is an inverted smooth
threshold shared by metal and non-metal Blinn-Phong masks. Face adds a
skin-gated parametric Toon highlight without a LightMap, while FaceMap blue
continues to author the `pow(NdotV, power)` nose line with independent strength
and color. Retained Body/Hair threshold and ramp-offset properties deserialize
but no longer drive the corrected equations. No MaterialIR, Bundle, schema,
shader-name, material-part, or fixed texture-role contract changes.

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

Miku 2.3.0 adds Wuwa tutorial-compliance controls without schema changes.
Body uses a simplified CookTorrance direct-specular term (`_Roughness`,
`_SpecularColor`, `_SpecularStrength`) plus reflection-probe indirect
specular (`_ReflectionStrength`), applies MatCap onto the albedo before
lighting with a 10% saturation default, and samples the model's UV3 channel
for the vertical gradient (`_GradientUVIndex=3` default). Body/Hair/Face
outlines consume the vertex-color green width mask (`_OutlineVertexColorMask`)
and default to the tutorial's near/far two-segment distance response
(`_OutlineDistanceMode=1`). Face enables the SDF soft channel and hair-shadow
sampling; Eye adds a main-light response between `_EyeShadowTint` and
`_EyeLitTint`. Materials without vertex colors or UV3 fall back to a neutral
mask and UV0 respectively. These properties are additive public surface;
existing materials keep their authored values unless the explicit
recommended-profile action is applied.

Miku 3.0.0 repairs the Genshin and WuWa Forward+ main-light paths. Unity
6000.0 / URP 17.0 selects `_FORWARD_PLUS`; Unity 6000.1+ selects
`_CLUSTER_LIGHT_LOOP` using the official `UNITY_VERSION >= 60010000` boundary.
WuWa Body, Hair, Face, and Eye pass normalized screen UV to the Forward+-safe
reflection-probe overload. Genshin Body and Hair Forward/backface plus Face
Forward compile the matching main-light variant, so a zero per-object
attenuation no longer suppresses their direct light or Face SDF. Genshin Eye
remains intentionally unlit. Realtime shadows continue to select the authored
WuWa shadow tint instead of erasing the complete direct-light contribution.

WuWa Face SDF diagnostics are read-only and never rewrite authored material or
texture-import settings. Identical main/soft channels remain valid and produce
an informational diagnostic. These changes affect diagnostics, shader variants,
tests, and rendering behavior. `_FaceSdfMirrorBlendWidth` is an additive
material property; no existing property is renamed, and no interchange schema,
shader name, or texture-role contract changes. Exact GPU validation
remains limited to Unity 6000.4.5f1 with URP/Shader Graph 17.4.0 on Direct3D 12;
the other technical lines remain Experimental.

Genshin and WuWa main-shadow coordinates use URP `GetShadowCoord`, including
the screen-space shadow variant, and declare generic plus Low/Medium/High
soft-shadow quality variants. This is shader-variant correctness only; the
automated light-yaw probes disable cast shadows and therefore do not claim a
GPU occluder/penumbra image comparison.

Miku 2.3.0 also adds Genshin tutorial-conformance controls without schema
changes. Body, Hair, and Face accept the tutorial's `diffuse.a` cutout and
flickering emission modes (`_DiffuseA`, `_Cutoff`, `_Glow`, `_Flicker`),
per-material ramp rows (`_LightmapA0..A4`), and lightmap.a five-region
outline colors (`_OutlineColor0..4`, `_OutlineColorMode`). Body and Hair
opt into the tutorial's UV1 back-face rendering with
`_DoubleSided`/`_Cull`/`_BackUV1`; Genshin outlines read vertex-color A as
the width mask with a green fallback so imported game assets (Furina) and
Miku-baked meshes (Hu Tao) both work. These properties are additive public
surface; existing materials remain on the legacy path unless the new
controls are enabled.

Genshin Body and Hair additionally accept an optional normal map
(`_NormalMap`, `_BumpScale`, keyword `_GENSHIN_NORMALMAP_ON`) and
`NormalMap` is an accepted genshin texture role, matching the Wuwa/Endfield
binding convention. When `_AREA_SKIN` is enabled, the legacy
`Genshin_ReferenceSkinTone` curve applies only to regions whose LightMap
alpha marks skin, so mixed body materials (skin + cloth/cape) no longer get
the whole material tinted; pure-skin materials keep the previous response.

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

Blender extension 2.2.9 writes `miku-bake-request-1.2`. It records the actual
Blender numeric version and build hash and accepts `>=5.0.0,<5.3.0`.
The bundled worker retains frozen request 1.0/1.1 support on the certified
5.2.0 build. In-range versions other than certified 5.2.0 emit
`MIKU_BLENDER_VERSION_UNVALIDATED`; 5.0.1 and 5.1.2 are nevertheless recorded
as Supported Windows compatibility lanes because their final-ZIP evidence was
executed successfully.

Miku 2.2.12 supersedes the unsafe 2.2.11 major-only policy. Blender is bounded
to `>=5.0.0,<5.3.0`. Unity 6000.N requires URP 17.N and Shader Graph 17.N for
N=0..5, and the two package versions must be identical. Stable in-range patches
that are not in the matrix emit `MIKU_..._VERSION_UNVALIDATED` and run complete
Blender or Shader Graph capability preflight. Alpha, Beta, RC, Blender 5.3+,
Unity 6000.6+, and package 17.6+ are rejected before generated assets are
written. The warning-free target is Blender 5.2.0, Unity 6000.5.7f1, and
URP/Shader Graph 17.5.4. A row is marked Supported only after its actual Windows
execution record exists; macOS and Linux remain Unknown.

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
