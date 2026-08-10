# Changelog

- Fixed Genshin Body mixed materials turning purple when `_AREA_SKIN` was
  enabled: the legacy `Genshin_ReferenceSkinTone` curve is now gated by the
  authored LightMap skin mask instead of tinting the whole material. Added
  optional normal mapping to Genshin Body and Hair through `_NormalMap`,
  `_BumpScale`, and `_GENSHIN_NORMALMAP_ON`; `NormalMap` is now an accepted
  genshin texture role and the material Inspector keeps the keyword in sync.
- Fixed a compile error in the hidden `Hidden/MIKU/Endfield/PassLibrary`:
  `EndfieldEyeFlattenedLightDirection` now uses a float2 safe-normalize
  overload for the face-plane light projection. `Hidden/MIKU/Endfield/
  FullScreenColorLut` now includes URP Core.hlsl before Blit.hlsl so
  `TEXTURE2D_X` resolves. Both hidden shaders joined the ShaderHasError
  compile test.
- Corrected the opt-in Endfield tutorial path to the published article's
  formulas: Body/Skin/Face direct specular now uses the tutorial D*V response
  with the Day-blended specular envelope instead of the generic GGX lobe;
  the Specular Refine F0 LUT is sampled with the article's
  `lerp(D * roughness2, NoV^2, _RefineF0U_lerp)` / `1 - roughness * (1 - AO)`
  UVs; face SDF shadowing uses the article's width-scaled smoothstep and the
  ramp samples `lerp(sdfNoL, NoL, Refine-G)`; the eye shades with the
  face-plane projected key light and ignores scene shadow in the tutorial
  path; ramp RGB influence is luminance-preserving through
  `rampColor_control`; NoF shaping adds `_NoFPowStrength` and is disabled by
  default on Skin/Face per the reference; specular self-AO blends the
  day-zero/day-one bands; the main and top light colors desaturate inside
  shaded bands; the face rim uses the article's start/end remap, diffuse-brdf
  lerp, `min(AO, shadow)`, and one-sided camera-half; face SSS uses the
  `0.85/0.15` view remap; and Body diffuse energy uses `0.96 - 0.96 * metallic`.
  New properties are additive (`_NoFPowStrength`, `_RefineF0U_lerp`,
  `_RimLightArea`, `_RimLightDiffuseColorEffect`, hidden
  `_FaceRoughness`/`_FaceReflectivity`); Skin/Face `_NoFStrength` default is
  now zero. Legacy 2.2.x lighting and existing materials are unchanged.

## [2.3.0] - 2026-08-10

- Added Genshin tutorial-conformance controls to Body, Hair, and Face:
  `_DiffuseA` (`0` off, `1` alpha cutout, `2` flickering `diffuse.a`
  emission), `_Cutoff`, `[HDR] _Glow`, `_Flicker`, per-material
  `_LightmapA0..A4` ramp rows, `_DoubleSided`/`_Cull`/`_BackUV1` for the
  tutorial's UV1 back-face pass, and `_OutlineColor0..4` +
  `_OutlineColorMode` for the tutorial's lightmap.a five-region outline
  colors. Genshin outlines now read the vertex-color A width mask with a
  green fallback. All additions are additive public material properties; no
  schema, workflow, or texture-role change is introduced.
- Added Wuwa tutorial-compliance properties to Body, Hair, Face, and Eye:
  `_Roughness`, `_SpecularColor`, `_SpecularStrength`,
  `_ReflectionStrength`, `_VerticalGradientColor`, `_VerticalGradientStrength`,
  `_GradientUVIndex`, `_GradientInvert`, `_OutlineDistanceMode`,
  `_OutlineVertexColorMask`, `_EyeShadowStart`, `_EyeShadowEnd`,
  `_EyeLitTint`, and `_EyeShadowTint`. Body now applies MatCap onto the
  albedo before lighting at 10% saturation, adds a minimal CookTorrance
  direct-specular term and reflection-probe GI, and Body/Hair/Face sample the
  UV3 vertical-gradient channel; outlines consume the vertex-color green
  width mask and a two-segment tutorial distance response. Face enables the
  SDF soft channel and hair-shadow sampling; Eye responds to the main light
  while keeping authored emission highlights. All additions are additive
  public material properties; no schema or workflow change is introduced.
- Added the compatible Endfield day/top-light and three-layer character
  lighting path plus Body, Skin, Face, Eye, Hair, and transparent-overlay
  tutorial controls. Existing materials remain on the legacy path until a
  scene lighting controller opts in; Overlay separately remains legacy-unlit at
  `_LightingMode=0` and enters the lit-transparent path at `_LightingMode=1`.
- Added final-normal back-face correction for double-sided Body materials and
  deterministic part-state restoration for all nine Endfield material parts.
- Added TangentSpaceV2 UV7 smooth normals, skinned reconstruction, screen-space
  outline extrusion, finite legacy fallback, and depth-write-safe passes across
  the four fixed game families. Genshin/Endfield consume the green width mask,
  Wuwa/HSR preserve their neutral mask input, and HSR keeps its constant
  historical distance response.
- Added a validated 32-cube full-screen LUT pass before URP post processing and
  an Endfield Neutral/Bloom/Vignette profile factory. Game LUT assets are not
  distributed with the package.
- Added the Specular Refine F0/Color fixed roles and strict nine-part workflow
  dispatch without changing JSON schema versions or existing property names.
- Added structured duplicate-controller, transactional outline-mesh, and
  HairShadow offset-mesh/stencil diagnostics.
- Replaced project-wide saves in the outline-mesh and Endfield LUT installers
  with target-only asset saves, preserving unrelated dirty editor assets.

## [2.2.12] - 2026-08-08

- Restored the manifest floors to Unity 6000.0 and URP 17.0.0 so one TGZ is
  installable across Unity 6000.0-6000.5 projects.
- Require exact Unity 6000.N / URP 17.N / Shader Graph 17.N technical-line
  pairing for N=0..5 and exact equality between URP and Shader Graph versions.
- Reject prereleases, 6000.6+, 17.6+, and unknown Shader Graph minors before
  asset writes; removed adapter clamping.
- Expanded preflight to all generated graph capabilities and fixed identities
  plus actual imports of the five wrapper templates.
- Moved the warning-free target to Unity 6000.5.7f1 / URP and Shader Graph
  17.5.4 without changing stable generated IDs, public shader properties, or
  wrapper ownership.
- Fixed Source Mesh PBR material binding so original Height resources that are
  absent from the generated runtime-property contract are diagnosed and
  skipped, while reachable `_MIKU_HeightMap` properties retain hard failure.

## [2.2.11] - 2026-08-05

- Relaxed editor and render-package validation to a major-version policy:
  any Unity 6000.x (Unity 6) and any URP/Shader Graph 17.x is accepted.
  Wrong-major versions fail before any import transaction or asset write;
  in-major versions that are not exactly certified emit
  `MIKU_UNITY_VERSION_UNVALIDATED` / `MIKU_URP_VERSION_UNVALIDATED` /
  `MIKU_SHADERGRAPH_VERSION_UNVALIDATED` warnings.
- Moved the certified (warning-free) reference to Unity 6000.5.4f1 and
  URP/Shader Graph 17.5.4, and raised the package manifest minimum to
  `unity: 6000.5` with a URP dependency floor of 17.5.4.
- Added explicit Shader Graph 17.5/17.6 adapters; any higher 17.x minor
  clamps to the highest-known adapter with the generated-asset identity
  namespace left stable.

## [2.2.10] - 2026-08-04

- Removed an empty Unity Package Manager sample declaration and added
  release validation that rejects any declared sample whose directory is
  missing, empty, or references an unsafe path.

## [2.2.9] - 2026-08-03

- Allow Unity 6000.0.0f1 through 6000.4.5f1 and URP/Shader Graph 17.0.0
  through 17.4.0, while retaining 6000.4.5f1/17.4.0 as the sole validated
  tuple and recording explicit diagnostics for lower in-range versions.
- Reject editor or render-package versions above the current strict upper
  bounds before any import transaction or generated-asset write.
- Select explicit Shader Graph adapters for 17.0 through 17.4 and preflight
  the installed internal API and MultiJson serialization contract in memory.

## [2.2.8] - 2026-08-03

- Replace the template menu with `Miku > Game Toon > Materials > Create
  Material` and add explicit, localized texture inputs for the four supported
  game workflows. The creator validates every required field before creating a
  user-owned `.mat`, applies recommended profiles, synchronizes keywords, and
  binds Wuwa Body's ID / Stockings Map to both shader properties.
- Add the per-user `Miku/Settings` language selector for English and
  Simplified Chinese. The selection is stored in `EditorPrefs` and does not
  affect generated assets or the Unity Editor language.
- Localize Miku-authored editor windows, inspectors, ShaderGUI labels,
  dialogs, help boxes, and friendly status messages while keeping public menu
  paths, shader properties, diagnostics, and JSON content stable in English.
- Continue importing historical MiGR and time-dependent Bundles; new Blender
  exports reject effective time dependencies before writing output.

## [2.2.7] - 2026-08-03

- Treat Wuwa `_EyeHET` as a direct pupil/sclera emission mask and use HDMF
  red/inverse-alpha for primary highlight and pupil separation.
- Add authored upper/lower highlight textures, UV0 affine recipe transport,
  automatic main-light Fresnel EG, and mask/channel debug views.
- Add `EyeHDMF`, `EyeUpperHighlight`, and `EyeLowerHighlight` bindings while
  preserving existing material property references and user variants.
- Require re-import of 2.2.6 Eye recipes instead of retaining the incorrect
  single-HET double-highlight compatibility branch.

## [2.2.6] - 2026-08-03

- Use one Wuwa `EyeHET` mask for two independently transformed upper/lower HDR
  highlights and expose separate eye-base/highlight emission strengths.
- Add object-matrix Wuwa Face basis controls, ID-map-driven opaque sheer
  stockings, brighter Hair recommendations, and primary Effect brightness.
- Add `_BodyEmissionStrength`, scale Body emission independently of its
  texture-driven keyword, and calibrate Eye/Face/Body defaults for restrained
  iris highlights, flat face shading, and `0.15` MatCap response.
- Migrate only generated base recipes to 2.2.6; preserve user material variants,
  MaterialIR 2.0, existing texture roles, and all JSON schema versions.

## [2.2.5] - 2026-08-02

- Add authored texture-mask skin SSS and warm-pale skin grading to Genshin,
  HSR, and Wuwa Body/Face shaders; gate Endfield Face SSS by Face Refine red.
- Fix Wuwa `_FaceID` synchronization for `_WUWA_ID_ON` and add an idempotent
  generated-base material profile migration plus an explicit Undoable menu for
  ordinary material assets.
- Add hue-preserving Genshin non-emissive highlight compression with a legacy
  `_HighlightCompression=0` fallback; explicit emission remains uncompressed.
- Expand the reusable anime Volume Profile for URP 17.4.0 to a deterministic
  ten-component grading stack (Neutral Tonemapping, White Balance, Channel
  Mixer, Lift/Gamma/Gain, Shadows/Midtones/Highlights, Split Toning, Color
  Curves, Color Adjustments, Bloom, and Vignette). Keep White Balance, channel
  mixing, hue, and color filters neutral while using a luminance master curve,
  Contrast `+16`, Saturation `+8`, Exposure `+0.35`, and restrained white Bloom.
- Remove the Vertex Color Initializer and Combined Mesh Data Unity menu entries
  while retaining their public mesh types and APIs; keep Smooth Normal
  Generator available.

## [2.2.4] - 2026-08-02

- Add explicit authored-RGB/scalar-red hair specular LUT modes and an
  object-head-up strand fallback while retaining the two-lobe response.
- Add bounded surface rim controls to Endfield Body, Skin, Face, and Hair.
- Add independent Body metal direct/environment boosts and a bounded direct
  highlight band while retaining GGX, URP reflection probes, and low-AO
  specular visibility.
- Add compatibility-neutral Skin/Face brightness and warm-pale whitening
  controls before face emotion/blush overlays. Preserve MaterialIR 2.0,
  texture roles, shader names, material slots, and existing property meanings.

## [2.2.3] - 2026-08-02

- Restore the Endfield directional Main Light independently of its diagnostic
  distance attenuation and honor URP Rendering Layers for explicit exclusion.
- Add URP 17.4 Main Light shadow/screen-shadow/light-layer/shadow-mask variants
  plus Main Light, attenuation, Direct Only, and SH Only debug outputs.
- Repair Face SDF/blush/manual basis, iris-only cornea, camera-stable hair
  highlights, bounded SSS, low-AO metal/specular, two-dimensional specular
  refinement, and rotated reflection-probe sampling.
- Add `SpecularRefineF0` and `SpecularRefineColor` roles while preserving
  MaterialIR 2.0 and all existing shader, role, slot, and `_EyeMode` contracts.

## [2.2.2] - 2026-08-02

- Separate Endfield direct-light color and distance energy from system-shadow
  visibility for Body, Skin, Face, Eye, and Mouth.
- Give Face SDF a 70% geometric-light floor while preserving `_UseFaceSDF`,
  face-area, and refine-map behavior.
- Keep `_EyeMode` as the Iris/Sclera material role: Iris samples the authored
  Base Map while Sclera uses an independent warm white.
- Prevent zero packed AO from blacking out cloth metal and inverse-mask hair
  accessories; direct specular no longer depends on material AO.
- Preserve MaterialIR 2.0, texture roles, public C# data structures, material
  slots, and existing Shader property names.

## [2.2.1] - 2026-08-02

- Fix Endfield raw-red eye-shadow clipping and opaque brow/lash overlays.
- Add non-emissive anime iris/cornea lighting and independently controlled
  authored cheek blush.
- Repair face-SDF readability, skin alpha-AO influence, cloth GGX reflection,
  and hair-accessory metal visibility.
- Preserve MaterialIR 2.0, texture roles, material slots, and legacy
  `_AlphaSource` values.

## [2.2.0] - 2026-08-02

- Repair Endfield skin, face, hair, and cloth texture semantics.
- Derive Endfield head axes from the renderer object-to-world matrix and add
  object-space head-center controls for face and head-hair shading.
- Add precise Endfield color-LUT, face-area/refine, and hair refine/shift/line
  texture roles with deterministic 2.1 role migration diagnostics.
- Correct Endfield texture import profiles for hair shift/line maps, expression
  atlases, and matcaps.

## 2.1.0 - 2026-08-02

- Added `MIKU/Endfield/*` Body, Skin, Hair, Face, Eye, Mouth, Overlay, Effect,
  and HairShadow shaders for URP 17.4.
- Added Endfield material templates, strict texture-import auditing, and
  texture-role/property binding.
- Added read-only imported Mesh acquisition for deterministic UV7 outline
  normals without importer mutation.

## 2.0.0 - 2026-08-01

- Removed the Generic Toon backend, shader family, builder, and dedicated GUI.
- Added MaterialIR 2.0 import/export handling and explicit retirement errors.
- Moved shared Screen Rim assets to `Runtime/GameToon` while preserving their
  GUIDs and game Renderer Feature deserialization.

## Unreleased

- Replaced `CustomMultiLobe` lighting with bounded finite-safe diffuse/GGX
  evaluation, per-lobe geometry-normal fallback, and final NaN/Inf containment.
- Changed Clear Coat smoothness to a normalized coat-contribution-weighted
  average below the URP singular limit and retained 1.0.3/1.0.4 profile input.
- Routed non-coat closure radiance through Unlit Base Color while retaining
  zero Base Color and Radiance-to-Emission for the Lit Clear Coat wrapper.
- Upgraded Generic Toon to shader-family `1.1.0`: monotonic three-tone diffuse,
  stable system-shadow blending, Forward/Forward+ lights, hard direct and probe
  specular, corrected ShadowCaster, and BaseMap-only Face/Skin/Hair fallbacks.
- Added optional Generic Normal/Control/FaceSDF/HairHM/MatCap/Emission bindings,
  local keyword synchronization, Anime Game presets, override-safe recipe
  migration, and explicit undoable Face bounds calibration.
- Routed evaluated `CustomMultiLobe` radiance through the selected wrapper's
  final-color contract and kept a white strength-1 material multiplier.
- Added per-lobe tangent/object-to-world normal lowering while keeping
  `Input.Normal` geometric, plus diagnosed Miku 1.0.3 zero-normal migration.
- Added Portable Hybrid import validation and runtime/UV0 mixed Shader Graph
  support for `PreferNative` bundles.
- Reject SourceMesh, `meshBinding`, and mesh-bound jobs in Portable Hybrid
  bundles with `MIKU_PORTABLE_RESOURCE_MESH_BOUND` before asset generation.

## 1.0.2

- Added persistent fixed-workflow Recipes and centralized role-to-Shader
  property binding for Generic, Genshin, WuWa, and HSR Toon imports.
- Added `MikuToonCharacterMask` MRT passes to all game Body/Hair/Face shaders
  and removed their Forward-pass Fresnel/depth rim calculations.
- Published both Screen Rim masks through RenderGraph-declared dependencies;
  raster callbacks no longer modify global texture state.

- Fixed Clear Coat lowering for Custom Multi-Lobe materials with multiple
  Principled terms by deterministically aggregating weighted coat mask and
  smoothness outputs, and selecting the matching Clear Coat wrapper template.
- Documented the native runtime lowering used for Blender Geometry
  `Backfacing` closure weights; no new Shader Graph backend node is required.

## 1.0.1

- Added runtime lowering for the EEVEE procedural corpus, including additional
  Math/Vector Math/Mix operations, Constant Color Ramp, and pass-aware Camera
  Ray/Shadow Ray expressions.
- Added explicit-HLSL-GUID Custom Function construction and deterministic
  Source Mesh Fidelity PBR resource consumption.
- Accepted valid Material IR nesting up to depth 128 and retained an explicit
  rejection above that bound.
- Treated authoritative Source Mesh PBR projection resources as the material
  bindings for complex closure exports, without requiring superseded baked
  expression-island textures to be bound twice.
- Imported Source Mesh PBR projections containing per-pixel IOR by recording
  the URP Metallic fixed-F0 approximation instead of requiring a nonexistent
  `_MIKU_IOR` material property.

## 1.0.0

- Renamed the package to `com.miku.shaderconverter` and the public root
  namespace to `Miku.ShaderConverter`.
- Added read-only MiGR bundle import and explicit selected-asset migration.
- Added the eight-shader Generic Toon family, original-Material outline/mask
  passes, the opt-in RenderGraph Screen Rim Renderer Feature, and its
  preview/deduplicating installer.
- Added Material Builder/recipe/Shader GUI and explicit cloning Mesh tools.
- Generic Toon no longer generates Shader Graph assets, source-mesh Prefabs,
  or Renderer material assignments.

## 2.2.1

- Added a force-updated generated Sub Graph dependency barrier before Wrapper
  creation and verified both the stable GUID mapping and imported main asset.
- Fixed reachable native `IsFrontFaceNode` graphs by omitting the redundant
  identity Vertex Position connection when MaterialIR has no Displacement
  expression, so ShadowCaster does not evaluate Fragment `FaceSign` code in a
  vertex program.
- Added explicit `MIKU_SUBGRAPH_GUID_SYNC_FAILED` and
  `MIKU_SUBGRAPH_IMPORT_FAILED` rollback diagnostics plus first-import and
  deterministic reimport EditMode coverage.

## 2.2.0

- Added Bundle 2.2 import with JPEG and Height Texture2D resources while
  retaining safe Bundle 2.0/2.1 compatibility.
- Added Shader Graph 17.4 direct image sampling, tangent OpenGL normal unpack
  and strength, height-normal generation, normal blending, and Roughness
  One Minus.
- Added explicit DirectX negative-Y normal import, shared packed scalar channel
  sampling, AO/Base Color composition, Alpha/Emission Mask handling, and
  explicit component extraction without filename inference.
- Added LOD 0 vertex height sampling and Object-space Vertex Position output
  with `_MIKU_HeightMap`, `_MIKU_HeightMidlevel`, and
  `_MIKU_HeightScale`.
- Added a neutral Vertex Position path to newly generated Standard PBR graphs
  and preserved user-modified wrappers unless Full Regeneration is explicit.

## 2.1.0

- Added native Object Position and file-backed Point Mapping/3D Noise Factor
  Custom Function generation using the package-owned fixed-GUID
  `MikuBlenderNoise.hlsl`.
- Added Bundle 2.1 SourceMesh validation and transactional glTFast 6.19.0
  import into stable Mesh, Prefab, and mesh-binding description assets.
- Refused legacy Bundle 2.0 mesh-bound textures without a sealed source mesh
  and rejected mismatched hash, mesh count, vertex/index/UV, renderer-slot, or
  selected Renderer fingerprints.
- Retained bounded compatibility for safe Bundle 2.0 and known 2.0.x target
  profiles while keeping Wrapper ownership unchanged.

## 2.0.3

- Connected MaterialIR Normal expressions to closure-composite Normal TS and
  transformed the final tangent normal to world space for multi-lobe lighting,
  Layer Weight, and Fresnel.
- Used geometric normal only while recursively constructing the Normal channel,
  preventing self-referential Normal expression graphs.
- Added `MIKU_GENERATED_RESOURCE_UNREFERENCED:<bindingKey>` validation for
  sealed baked resources that do not produce Shader Graph properties.
- Reset closure-composite `_BaseMap`/`_BaseColor` authoring inputs to a neutral
  final tint so source Base Color cannot black out evaluated Transparent
  Emission, Transparent Lit, or Custom Multi Lobe radiance.
- Added import/generation coverage for 2.0.2 Transparent Emission Normal
  bundles and baked closure weights, while retaining Wrapper ownership,
  stable GUID, rollback, and deterministic generation behavior.
- Added explicit compatibility for the coordinated 2.0.2 target profile.

## 2.0.2

- Added the Unity-authored URP/Shader Graph 17.4 Clear Coat wrapper and
  selected it only for MaterialIR 2.0 plans declaring `Urp17ClearCoat`.
- Added deterministic Coat Mask and Coat Smoothness Sub Graph outputs, with
  the required `Coat Smoothness = 1 - Coat Roughness` conversion.
- Included `m_ClearCoat` and the two Coat Master Stack blocks in wrapper
  compatibility checks. Existing user-owned non-Coat wrappers now require
  explicit Full Regeneration and are never overwritten automatically.
- Added bounded compatibility for non-Coat 2.0.0/2.0.1 target profiles and
  rejects a forged old-profile Coat plan with
  `MIKU_COAT_PROFILE_REEXPORT_REQUIRED_2_0_2`.

## 2.0.1

- Reused the single authoritative generated-asset identity anywhere under
  `Assets`, even when a re-exported bundle requests a different output root.
- Added bounded, reparse-safe identity discovery and stable-GUID ownership
  preflight so collisions fail before generated assets are written.
- Propagated MaterialIR 2.0 blend modes to Shader Graph 17.4 Alpha Mode,
  including Premultiply, and derived generated Material `RenderType` tags from
  the render contract.
- Preserved legacy transparent contracts without `blendMode` as Alpha mode.

## 2.0.0

- Added MaterialIR/Bundle 2.0 validation and six registered surface graph
  generators.
- Added transparent emission, transparent lit, refractive glass, and custom
  multi-lobe Shader Graph generation without averaging independent lobe inputs.
- Added deterministic symbolic closure-weight lowering, explicit
  Color-to-Float luminance and Vector-to-Float average conversions.
- Added URP 17.4 custom lighting for main/additional lights, shadows, cookies,
  SH/probes, Forward/Forward+, and target-pass fog.
- Added single-premultiply scalar transparency and Scene Color colored
  transmittance with project-setup diagnostics.
- Added real Shader Graph import/compiler/determinism EditMode coverage for v2
  transparent and multi-lobe bundles.
- Declared the custom-lighting SSAO boundary and linked per-lobe normal
  limitation instead of silently degrading either feature.

## 1.2.1

- Fixed deterministic multi-stop Color Ramp expansion re-adding element nodes
  with duplicate Shader Graph IDs.
- Added structured duplicate-node/connection diagnostics and recursive
  reflection-exception unwrapping for actionable automatic-import errors.
- Changed the dielectric IOR fallback to `1.5` and retained all six public
  optical property references and defaults.
- Added bounded import support for 1.2.0 non-dielectric surface contracts.
  Version 1.2.0 dielectric contracts now report
  `MIKU_DIELECTRIC_REEXPORT_REQUIRED_1_2_1`.

## 1.2.0

- Added strict `miku-surface-1.0` validation and versioned Opaque, Alpha Blend,
  Dithered, and dielectric wrapper selection.
- Added Unity-authored Shader Graph 17.4 Alpha Blend, Dithered Lit, and
  transparent Unlit dielectric templates.
- Added Scene Color screen refraction, Schlick Fresnel, Reflection Probe,
  Overlay, Dither, Color Ramp, and Normal From Height structured node
  expansion.
- Added Camera Opaque Texture and Linear color-space setup diagnostics without
  mutating project settings.
- Added render-contract mismatch protection for user-owned wrapper graphs and
  deterministic public optical properties.

- Normalized the legacy unconnected Blender Normal sentinel `[0, 0, 0]` to
  neutral tangent normal `[0, 0, 1]` before Shader Graph generation and
  material binding, with an explicit compatibility diagnostic.
- Added a Standard PBR-only semantic material panel with twelve connected map,
  tint, and strength controls; generated Blender defaults remain hidden but
  preserved in the editable graph.
- Split the Standard PBR and Generic Toon wrapper templates while retaining the
  same generated Sub Graph contract.
- Fixed Standard PBR constant binding, including raw Roughness followed by the
  single required `Smoothness = 1 - Roughness` conversion.
- Added exact-match wrapper presentation migration and a bounded compatibility
  path for the immediately preceding target profile hash.
- Added identity-pair lookup for shared output roots and collision-safe
  `<material>__<12-character-material-id>` directories.
- Preserved recorded wrapper, generated base, and user Material Variant paths
  across Blender material renames without changing stable asset GUIDs.
- Added explicit conflicting-directory and duplicate-identity diagnostics plus
  EditMode coverage for same-name sources, legacy directories, Prefab/Scene
  references, and Material Variants.

## 1.1.1

- Added native Shader Graph Hue/Saturation/Value expansion.
- Added deterministic UV0 Sample Texture 2D properties for baked static
  expression islands, including tangent-normal import and sampling.
- Added compatibility for known 1.1.0 and 1.0 target-profile hashes while
  retaining strict rejection and actionable diagnostics for unknown hashes.
- Preserved user-owned wrappers while adding generated texture properties to
  Miku-owned Sub Graphs and first-created wrappers.

## 1.1.0

- Added the deterministic Shader Graph 17.4 runtime-expression backend for
  View Direction, Camera Data, Miku Time v1, physical Fresnel, and Layer Weight.
- Added stable Time control properties for playback, offset, pause, seek, and
  synchronization from scripts, Animator, or Timeline.
- Added fragment-to-vertex stage validation and byte-stable native-node
  serialization without Custom Function nodes.
- Preserved user-owned wrappers while regenerating only Miku-owned Sub Graphs,
  mappings, receipts, and generated base Materials.

## 1.0.0

- Added automatic `.migrbundle` import and recoverable delayed generation.
- Unified five workflows behind one Miku 1.0 contract and internal registry.
- Added Console plus diagnostic-asset errors and diagnostic-only `.miku`
  rejection.
- Added Miku-owned generated base Materials and user-owned Material Variants.
- Added ASCII-safe generated Sub Graph names and strict Shader Graph compiler
  message validation for Unicode material names.
- Removed MIKU 2/3/4/5, B2U bundle, compiler, importer, and compatibility APIs.
- Restricted the backend to Unity 6000.4.5f1, URP 17.4.0, and Shader Graph
  17.4.0.
