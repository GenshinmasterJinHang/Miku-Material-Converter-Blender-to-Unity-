# Changelog

## Unreleased

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
