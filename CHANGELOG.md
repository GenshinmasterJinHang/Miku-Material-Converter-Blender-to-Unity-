# Changelog

- Fixed the Genshin Body shader applying the legacy skin-tone curve to the
  whole material when `_AREA_SKIN` was enabled, which tinted non-skin regions
  (for example a blue cape's back face) purple; the curve is now masked by
  the authored LightMap skin mask. Added optional Genshin Body/Hair normal
  mapping (`_NormalMap`, `_BumpScale`, `_GENSHIN_NORMALMAP_ON`) and made
  `NormalMap` an accepted genshin texture role.
- Fixed the hidden Endfield PassLibrary compile error by adding a float2
  safe-normalize overload for the eye face-plane light projection, and fixed
  the full-screen color LUT shader to include URP Core.hlsl before Blit.hlsl
  so `TEXTURE2D_X` is defined. Both hidden shaders are now covered by the
  Endfield ShaderHasError compile test.
- Corrected the opt-in Endfield tutorial path against the published article:
  the tutorial D/V direct-specular response replaces the generic GGX lobe in
  Body/Skin/Face, the F0-refine LUT uses the article's UVs
  (`_RefineF0U_lerp`), the face SDF shadow uses the article's width-scaled
  smoothstep and ramp signal, the eye shades with the face-plane projected
  light and no scene shadow, ramp color keeps luminance through
  `rampColor_control`, NoF gains `_NoFPowStrength` (Skin/Face default off),
  specular self-AO follows the Day blend, the main/top light desaturates in
  shaded bands, the face rim follows the article's start/end remap and
  one-sided mask, face SSS uses the 0.85/0.15 view remap, and Body diffuse
  energy uses 0.96 - 0.96 * metallic. Legacy 2.2.x behavior is unchanged.
- Reverted the HSR tutorial semantic restoration by user decision. HSR returns
  to the original single-pass implementation; the tutorial's two-pass layout
  is used only as a reference.

## 2.3.0 - 2026-08-10

- Added Genshin tutorial-conformance controls to Body, Hair, and Face:
  `diffuse.a` cutout/emission modes with `_Cutoff`/`_Glow`/`_Flicker`,
  per-material ramp rows, the tutorial's UV1 double-sided back-face path
  (`_DoubleSided`/`_BackUV1`), vertex-color A outline width, and lightmap.a
  five-region outline colors (`_OutlineColorMode`). Existing materials keep
  their authored values; no schema or workflow change is introduced.
- Added Wuwa tutorial-compliance controls to Body, Hair, Face, and Eye:
  simplified CookTorrance direct specular, reflection-probe indirect
  specular, MatCap added onto the albedo with 10% saturation, UV3-driven
  vertical gradient, Fresnel-step rim, official face-SDF soft channel,
  enabled hair-shadow sampling, eye main-light response, and vertex-color
  outline width with the tutorial's near/far two-segment distance formula.
  New material properties are additive public surface; no schema, workflow,
  or texture-role change is introduced.
- Added opt-in Endfield tutorial lighting driven by one scene controller:
  continuous day state, character top light, three-layer diffuse, shaped
  shadows, back-light/NoF control, camera-forward specular, finite DFG energy
  compensation, separated rims, and compatible legacy fallback.
- Completed part-specific Body, Skin, Face, Eye, Hair, and lit-transparent
  Overlay paths, including double-sided final-normal handling, thin-cloth SSS,
  skin controls, SDF-refined face normals/rim, eye Toon/MatCap corrections, and
  authored hair-LUT coordinates. Overlay remains legacy-unlit at
  `_LightingMode=0` and opts into Toon-lit transparency at `_LightingMode=1`.
- Introduced TangentSpaceV2 UV7 smooth normals and a shared screen-space outline
  contract for Genshin, Wuwa, HSR, and Endfield. Legacy UV7 remains readable;
  invalid tangents now fail before asset writes. Genshin/Endfield use the public
  green width mask, Wuwa/HSR preserve their neutral width input, and HSR keeps
  its constant historical distance response.
- Added an idempotent pre-post-processing 32-cube full-screen LUT installer and
  an Endfield Volume profile containing Neutral Tonemapping, Bloom, and
  Vignette. SMAA High remains a separately configured target-camera setting.
- Expanded Endfield fixed-workflow validation to all nine parts and added
  Specular Refine F0/Color roles without changing MaterialIR or Bundle schemas.
- Added stable diagnostics for duplicate Endfield lighting controllers,
  transactional outline-mesh failures, and missing/mismatched HairShadow
  offset-mesh and stencil setup.
- Limited outline-mesh and Endfield post-processing commits to their explicit
  target assets so project-wide `SaveAssets()` calls cannot persist unrelated
  dirty editor assets.

## 2.2.12 - 2026-08-08

- Restored the Unity package install floors to Unity 6000.0 and URP 17.0.0 so
  one TGZ can be installed across the bounded Unity 6000.0-6000.5 matrix.
- Replaced ADR 0014's unsafe major-only policy with exact Unity 6000.N /
  URP 17.N / Shader Graph 17.N pairing for N=0..5. URP and Shader Graph must
  have identical package versions; prereleases and future technical lines fail
  before asset writes.
- Added explicit Shader Graph 17.0-17.5 adapters and expanded preflight across
  properties, nodes, ports, connections, Custom Functions, serialization,
  generated surface outputs, and all five wrapper template identities/imports.
  Unknown 17.x minors no longer clamp to another adapter.
- Bounded Blender to 5.0-5.2 with explicit technical-line capability profiles,
  structured missing-API diagnostics, and parameterized 5.0.1/5.1.2/5.2.0
  Windows smoke orchestration.
- Moved the warning-free target to Blender 5.2.0, Unity 6000.5.7f1, and
  URP/Shader Graph 17.5.4. MaterialIR, Bundle, Bake Result, public shader
  properties, stable generated IDs, and wrapper ownership remain unchanged.
- Fixed Source Mesh PBR imports that retained an original Height resource after
  the final baked channel graph had superseded it. Material binding and
  validation now share the generated Shader Graph runtime-property contract,
  while reachable Height still fails if `_MIKU_HeightMap` is missing.

## 2.2.11 - 2026-08-05

- Relaxed version validation to a major-version policy: any Blender 5.x,
  any Unity 6000.x (Unity 6), and any URP/Shader Graph 17.x is accepted.
  Wrong-major versions fail before any asset write; in-major versions that
  are not exactly certified emit `MIKU_..._UNVALIDATED` warnings.
- Moved the certified (warning-free) reference to Blender 5.2.0, Unity
  6000.5.4f1, and URP/Shader Graph 17.5.4, and raised the Unity package
  manifest minimum to `unity: 6000.5` / URP 17.5.4.
- Added Shader Graph 17.5/17.6 adapters; unknown 17.x minors clamp to the
  highest-known adapter while generated-asset identity IDs stay stable.
- Coordinated release: both the Blender extension and the Unity package are
  now at 2.2.11.

## 2.2.10 - 2026-08-04

- Removed an empty Unity Package Manager sample declaration whose untracked
  directory disappeared from clones and release archives, and added release
  validation that rejects any future missing or empty declared sample.

## 2.2.9 - 2026-08-03

- Opened the Blender installation and runtime range to 5.0.0 through 5.2.0,
  with 5.2.0 remaining the only certified build. Lower in-range versions emit
  `MIKU_BLENDER_VERSION_UNVALIDATED`; 5.2.1 and later are rejected.
- Added `miku-bake-request-1.2`, which binds a bake request to the executing
  Blender numeric version and build hash while retaining frozen request 1.0
  and 1.1 support on the certified build.
- Opened the Unity range to 6000.0.0f1 through 6000.4.5f1 and URP/Shader Graph
  17.0.0 through 17.4.0. Lower in-range versions continue with structured
  unvalidated diagnostics; versions above the strict upper bounds fail before
  asset writes.
- Added explicit Shader Graph 17.0-17.4 adapter selection and an in-memory
  capability/serialization preflight before import transactions begin.

## 2.2.8 - 2026-08-03

- Made the Blender-facing current-material panel Standard PBR only. Existing
  workflow properties, explicit lower-level workflow calls, old game Bundles,
  and Unity import compatibility remain available to scripts and historical
  assets without changing `.blend` values during a current export.
- Replaced the Unity Game Toon template menu with
  `Miku > Game Toon > Materials > Create Material`. The creator filters the 22
  supported parts, enumerates public 2D shader textures in declaration order,
  requires `_BaseMap` except for Endfield Mouth, binds Wuwa Body's ID texture
  to both ID and stockings properties, and creates a user-owned `.mat` only
  after all validation and keyword/profile work succeeds.
- Added bilingual English/Simplified Chinese README and Manual pages, exact
  Blender/Unity compatibility documentation, reproducible release builders,
  Blender smoke orchestration, Mermaid workflow sources, and documentation
  regression tests.
- Added an independent Unity Editor language preference under `Miku/Settings`;
  Miku-authored windows, inspectors, ShaderGUI labels, dialogs, and status
  messages can use English or Simplified Chinese without following the Unity
  Editor language.
- Removed the two legacy migration/time-node buttons from the Blender Advanced
  panel while retaining their internal operator IDs and silent legacy readers.
- Blender exports now fail early with `MIKU_TIME_INPUT_UNSUPPORTED` when an
  effective output chain depends on time. Disconnected time nodes remain safe,
  and historical MiGR/time Bundle imports remain compatible.
- Coordinated the Blender extension and Unity package at 2.2.8; MaterialIR,
  Bundle, bake, and Unity public shader interfaces remain unchanged.

## 2.2.7 - 2026-08-03

- Corrected Wuwa `EyeHET` to a direct linear emission mask: white emits,
  black does not, and gray remains proportional. HDMF red supplies the primary
  highlight while inverse alpha separates pupil and sclera emission controls.
- Added `EyeHDMF`, `EyeUpperHighlight`, and `EyeLowerHighlight` fixed texture
  roles plus optional UV0 `Affine2D` binding metadata. Static Blender Point
  Mapping now reaches Unity recipes and materials without manual alignment.
- Made fixed-workflow reachability constant-Mix-aware, restored the authored
  upper/lower masks, added optional main-light-driven Fresnel EG, and safely
  inherits a unique HET from a sibling eye material on the same mesh.
- Coordinated the Blender extension, document writer, Unity package, and shader
  family at 2.2.7. Bundle schema remains 1.0; bundles using the new additive
  roles or transform field require a paired 2.2.7 importer.

## 2.2.6 - 2026-08-03

- Reworked Wuwa Eye around one linear `EyeHET` mask sampled twice for
  independently offset upper/lower HDR highlights, with separate base-eye and
  highlight emission controls.
- Added explicit object-space Wuwa Face axes transformed by the object matrix,
  ID-map-driven opaque sheer stockings, brighter recommended Hair calibration,
  and primary Wuwa Effect brightness.
- Added Wuwa Body `_BodyEmissionStrength` while retaining texture-presence
  emission keywords, and calibrated the Eye/Face/Body defaults for smaller
  iris highlights, flatter face shading, and restrained MatCap energy.
- Made fixed-workflow primary texture selection active-Surface-aware so a
  disconnected color image cannot replace the connected black voice-mark
  BaseMap. Wuwa fixed workflows also transcode authored TARGA images to sealed
  PNG resources and recognize the ID `Separate Color` path used by stockings.
  Blender extension 2.1.2 and Unity package 2.2.6 retain MaterialIR 2.0 and all
  JSON schema versions.

## 2.2.5 - 2026-08-02

- Added authored-texture-mask skin SSS and restrained warm-pale skin grading to
  Genshin, HSR, and Wuwa Body/Face shaders; Endfield Face now gates SSS by the
  enabled Face Refine red channel.
- Fixed Wuwa FaceID keyword synchronization and added a generated-base-only,
  idempotent recommended material migration with an explicit opt-in menu for
  ordinary materials.
- Replaced Genshin's intermediate RGB hard clipping with a hue-preserving final
  non-emissive soft shoulder. `_HighlightCompression=0` preserves the legacy
  clipping path.
- Expanded the reusable anime Volume Profile for Unity 6000.4.5f1 and URP
  17.4.0 to a deterministic ten-component grade covering Neutral Tonemapping,
  White Balance, Channel Mixer, Lift/Gamma/Gain,
  Shadows/Midtones/Highlights, Split Toning, Color Curves, Color Adjustments,
  Bloom, and Vignette. The final preset keeps color controls neutral while using
  a luminance master curve, Contrast `+16`, Saturation `+8`, Exposure `+0.35`,
  and restrained white Bloom. Removed the Vertex Color Initializer and Combined
  Mesh Data menu entries while retaining their public mesh APIs. MaterialIR 2.0
  and texture-role/schema contracts are unchanged.

## 2.2.4 - 2026-08-02

- Added an explicit scalar-red hair specular LUT mode, object-head-up strand
  fallback, and calibrated dual-lobe highlights without changing the legacy
  RGB LUT default.
- Added bounded Main-Light-aligned surface rim controls for Body, Skin, Face,
  and Hair, complementing the existing screen-space rim.
- Added independent metallic direct and reflection-probe boosts plus a bounded
  highlight band while retaining GGX, URP probe IBL, and low-AO reflection.
- Added compatibility-neutral warm-pale skin grading before face emotion and
  blush overlays. MaterialIR remains 2.0 and no texture role or material slot
  changed.

## 2.2.3 - 2026-08-02

- Restored the Endfield directional Main Light by separating its RGB,
  direction, shadow visibility, diagnostic distance attenuation, and Rendering
  Layer match; direct light is no longer erased by a stale per-object distance
  value.
- Added URP 17.4 Main Light/shadow/layer variants and direct-diffuse,
  direct-specular, SH-only, and attenuation debug views.
- Repaired Face SDF mirroring and fallback, alpha-authored blush, iris-only
  cornea/parallax, camera-stable hair flow, bounded skin SSS, low-AO specular,
  two-dimensional specular-refine LUTs, and reflection-probe rotation/mip bias.
- Added `SpecularRefineF0` and `SpecularRefineColor` roles without changing
  MaterialIR 2.0, existing roles, shader names, material slots, or `_EyeMode`.

## 2.2.2 - 2026-08-02

- Decoupled Endfield direct-light energy from system-shadow visibility for
  Body, Skin, Face, Eye, and Mouth, preserving authored shadow tones instead
  of collapsing shaded pixels to black.
- Added a 70% geometric-light fallback for invalid Face SDF regions and kept
  per-material Iris/Sclera roles with independent iris and warm-sclera color.
- Restored finite colored cloth metal and hair-accessory reflection when the
  packed AO channel is zero, without changing packed-map channels or roles.

## 2.2.1 - 2026-08-02

- Corrected Endfield eye-shadow clipping direction and opaque brow/lash
  coverage without renumbering legacy alpha-source values.
- Added an authored iris/anime-cornea response, independent Emotion Atlas
  blush, readable face-SDF and skin lighting, and finite visible cloth and hair
  accessory metal reflections.
- Added regression math, shader-property, compatibility, and deterministic
  package coverage without changing MaterialIR 2.0 or texture roles.

## 2.2.0 - 2026-08-02

- Corrected Endfield skin, face, hair, and cloth packed-texture semantics and
  renderer-object head-space evaluation.
- Added precise Endfield texture roles while preserving 2.1 aliases through
  deterministic migration diagnostics.

## 2.1.0 - 2026-08-02

- Added the target-neutral `endfield_toon` workflow and nine first-party Unity
  6 URP fixed material presets.
- Added strict Endfield texture roles/import auditing, user-owned material
  templates, texture-driven stencil-clipped hair shadow, and deterministic
  non-readable-mesh UV7 smooth-normal generation.
- Unified the fourth game workflow with the shared Game Toon screen-space rim
  contract while preserving existing Genshin, WuWa, and HSR property names.

## 2.0.0 - 2026-08-01

- Retired and removed the Generic Toon workflow, shaders, editor entry points,
  and runtime backend.
- Added MaterialIR 2.0; MaterialIR 1.0 remains frozen for the four retained
  workflows.
- Moved shared Screen Rim infrastructure to Game Toon and upgraded packages to
  2.0.0. Old Generic inputs fail with `MIKU_WORKFLOW_RETIRED:generic_toon`.

## Unreleased

- Added Blender English/Simplified-Chinese UI localization that follows the
  Blender interface language, plus Advanced 512/1024/2048/4096 quality choices
  for generated 2D bake textures.
- Added backward-compatible bake request 1.1. The 2.1.1 worker accepts frozen
  1.0 requests, while new plans, request hashes, cache keys, and baked resource
  dimensions consistently record the selected resolution.
- Replaced fragile `CustomMultiLobe` BRDF evaluation with bounded finite-safe
  diffuse, glossy, metallic, and Principled terms over URP public light data.
- Added per-lobe invalid-normal fallback to the geometric world normal and a
  final NaN/Inf containment guard, with
  `MIKU_CLOSURE_NONFINITE_VALUE_SANITIZED` diagnostics for risky legacy input.
- Changed multi-lobe Clear Coat smoothness to a coat-contribution-weighted
  average capped below the URP singular limit; 1.0.3 and 1.0.4 target profiles
  remain import-compatible.
- Routed final non-coat closure radiance through the Unlit wrapper's Base
  Color output, which is the URP Unlit final-color contract. Clear Coat keeps
  zero Base Color and evaluated radiance in the Lit wrapper's Emission output,
  so neither path is dropped or lit twice.
- Historical Generic Toon implementation notes below apply only to Miku 1.x;
  the workflow is retired in 2.0.0.
- Added a complete BaseMap-only anime-game fallback: face proxy normals and
  object-space blush, rosy Face/BodySkin grading, procedural two-layer hair
  highlights, UV7-aware tinted outlines, and corrected directional/punctual
  ShadowCaster bias and clamping.
- Allowed optional Generic `NormalMap`, `IDMap`, `FaceSDF`, `HairHM`, `MatCap`,
  and `EmissionMap` roles with material-local keyword synchronization. IDMap
  uses R specular/MatCap, G shadow offset, B Screen Rim, and A outline width;
  absent maps are not sampled or synthesized from BaseMap.
- Added the Anime Game semantic preset, user-override-preserving 1.0 recipe
  migration, optional-map capability display, and explicit undoable Face bounds
  calibration that never edits a Mesh, Renderer, or importer.
- Fixed `CustomMultiLobe` closure lighting so evaluated radiance uses the
  selected wrapper's final-color path instead of being dropped or lit twice.
- Preserved per-lobe Normal semantics in Unity and normalized only Blender's
  unconnected zero Normal/Coat Normal sentinel to the neutral tangent normal.
- Added diagnosed in-memory compatibility for Miku 1.0.3 closure zero normals;
  existing bundles with target profile `b9e8f39f…` remain importable.
- Added Portable Hybrid semantics to the public `PreferNative` mode: supported
  View/Camera/Time, Fresnel, and Layer Weight expressions remain live while
  statically proven UV0 islands use mesh-independent reusable bakes.
- Portable Hybrid workers bake on an internal canonical 0-1 UV plane and omit
  SourceMesh, mesh fingerprints, and mesh bindings. Core and Unity reject any
  bundle that violates this invariant.
- Full PBR Bake remains source-mesh-bound and now rejects runtime dependencies
  before worker startup with an actionable Portable Hybrid diagnostic.
- Fixed hybrid Source Mesh baking so weighted-closure parameters are scanned
  recursively, static unsupported islands are baked and replaced, and native
  View/Camera/Time inputs are never submitted to a UV bake.
- Added the additive Blender material displacement policies `FOLLOW_BLENDER`,
  `ALWAYS_VERTEX`, and `MAP_ONLY`. PBR bundles can now contain one deduplicated
  raw Linear R half-float Height map with editable `_MIKU_HeightMidlevel` and
  `_MIKU_HeightScale` controls.
- Made Full PBR workers honor the plan's exact semantics and stopped generating
  non-authoritative top-level BaseColor/IOR resources for `CustomMultiLobe`.
- Restored automatic glTFast Source Mesh import for current Miku bundles,
  including stable Mesh assets, a Miku-owned Prefab, fingerprint validation,
  material-slot binding, and `MikuMeshBindingDescription` generation. Legacy
  MiGR source-mesh bundles remain read-only compatibility inputs.
- Improved `MIKU_SOURCE_MESH_FIDELITY_REQUIRED` to name the deepest unsupported
  source and its consumer path, and added explicit diagnostics for unsafe Bump
  promotion, conflicting Height sources, and skipped compatibility resources.

## 1.0.2 — 2026-08-01

- Made Generic Toon and all three game workflows tolerant fixed-shader exports:
  arbitrary Blender graphs no longer require closure conversion or baking,
  while static Image Textures use explicit deterministic roles.
- Added fixed-workflow Recipes that preserve semantic/game-part choices,
  Texture bindings, Material Variant identity, parent, and user overrides.
- Replaced Genshin, WuWa, and HSR Body/Hair/Face Fresnel rims with the shared
  screen-space linear-depth edge feature; Eye and WuWa Effect remain excluded.
- Fixed RenderGraph global-state validation by publishing both mask textures
  with `SetGlobalTextureAfterPass` and removing callback `SetGlobalTexture`.

- Fixed Unity Clear Coat lowering for multi-Principled Custom Multi-Lobe
  materials and synchronized the target profile hash across Blender and Unity.
- Fixed Blender Geometry `Backfacing` closure weights by lowering them to the
  target-neutral `1 - Input.IsFrontFace` runtime expression, avoiding nested
  unsupported diagnostics and accidental bake routing.
- Restored the Standard PBR semantic extractor in
  `miku/standard_pbr_semantics.py` and `miku/standard_pbr_texture_semantics.py`
  to recover the legacy B2U behaviours lost in the 1.0 identity migration.
  The Blender snapshot now augments its closure-derived slot map with the
  legacy result, so programmable-node materials (mixes through passthrough
  nodes, ORM packed textures, Bump / Normal Map distinction, loose-name
  texture recovery) now export the full BaseColor / Metallic / Roughness /
  Normal / Emission slot set instead of an empty `standardPbrSemantic.slots`
  map. Unity bundles that previously rendered black because no textures were
  bound now resolve the same Standard PBR maps the 0.x exporter produced.
- Added `tests/test_standard_pbr_semantics.py` and
  `tests/_miku_pbr_test_fixtures.py` covering the recovered extractor
  (socket semantic override, principled metalness / roughness / specular
  slot mapping, normal + bump coexistence, displacement parallax,
  loose-name texture recovery, and ORM packed-texture detection).
- Upgraded the Blender 5.2 EEVEE exporter for active-output capability
  classification, legacy Glossy identity, additional Math/Vector Math/Mix and
  Constant Color Ramp expressions, pass-aware Camera/Shadow Light Path, and
  explicit deterministic Source Mesh Fidelity PBR baking.
- Added a parameterized private-corpus audit/export tool with relative-path
  reports, active-chain-or-label Cycles exclusion evidence, compiler/plan and
  resource hashes, deterministic bounded sample selection, explicit Auto / Source
  Mesh Fidelity / Full PBR Bake recommendations, and bound/unbound/support gates.
- Added `MIKU_FULL_PBR_BAKE_REQUIRED` so Source Mesh Fidelity reports the exact
  manual fallback required for static closure surfaces it cannot safely split.
- Made emission closure inputs participate in hybrid expression-island
  planning and finalized authoritative Source Mesh channel bakes as a bounded
  PBR projection instead of requiring redundant expression-island bindings.
- Raised Unity's bounded Material IR JSON reader depth to 128 (while rejecting
  deeper input) so valid complex corpus graphs import without weakening input
  size or path validation.
- Reported and omitted PBR projection channels that URP's Metallic workflow
  cannot bind (such as per-pixel IOR) instead of failing import or silently
  manufacturing a Shader property; the receipt records the fixed-F0
  approximation.
- Bumped the coordinated Blender/Python/Unity packages to Miku 1.0.1 without a
  schema, package-ID, conversion-mode, or public Shader-property change.
- Promoted Generic Toon material, Mesh-data, Screen Rim, and migration tools
  to the top-level `Miku` menu; split smooth-normal and vertex-color authoring
  into dedicated Editor windows with an explicit combined entry.
- Added Screen Rim installer status and open-only shortcuts to the Material
  Builder and Generic Toon Shader GUI.
- Removed the temporary five-file/73-material corpus import and review-scene
  generation menu from the distributable Unity package.
- Fixed the combined Blender extension's package-relative bake protocol imports
  so `miku_shader_converter` can be enabled persistently and loaded after a
  clean Blender 5.2 restart.

## 1.0.0 — 2026-07-30

- Migrated the active product identity from MiGR to Miku: canonical Python
  packages, Blender extension, Unity package ID, C# namespace, schemas,
  diagnostics, Shader properties, Shader names, and release artifacts now use
  Miku 1.0 identities exclusively.
- Added read-only MiGR 1.x/2.x normalization, one-time Blender `migr_*`
  property copying, and explicit selected-asset Unity Dry Run/Apply migration.
- Combined the semantic exporter and GPL bake worker into one deterministic
  GPL-3.0-or-later Blender extension archive while retaining MIT source
  notices.
- Replaced the Generic Toon Shader Graph backend with eight fixed semantic
  ShaderLab/HLSL shaders, embedded geometry outline/mask passes, and an opt-in
  URP 17.4 RenderGraph Screen Rim feature.
- Added the Material-driven Generic Toon Builder, deterministic
  `MikuToonMaterialRecipe` three-way synchronization, custom Shader GUI,
  Smooth Normal Generator, and Vertex Color Initializer.
- Removed automatic Source Mesh/Prefab generation from the importer. Miku does
  not scan a Model Root or Renderers, expand material slots, replace Renderer
  references, or generate a character Prefab.

## 2.2.1 — 2026-07-30

- Added a synchronous, force-updated dependency barrier after assigning the
  stable generated Sub Graph GUID and before creating or importing its Wrapper.
- Verified the generated Sub Graph path-to-GUID mapping and imported main
  asset, with explicit `MIKU_SUBGRAPH_GUID_SYNC_FAILED` and
  `MIKU_SUBGRAPH_IMPORT_FAILED` diagnostics covered by transactional rollback.
- Fixed first import of reachable Fragment-stage `Input.IsFrontFace` graphs so
  Shader Graph compiles the Wrapper without `invalid subscript 'FaceSign'`.
- Stopped connecting the generated Sub Graph's identity Vertex Position output
  when MaterialIR has no Displacement expression, preventing fragment-only
  `FaceSign` code from being evaluated by ShadowCaster vertex programs.
- Kept the native `IsFrontFaceNode`, deterministic GUIDs, user-owned Wrapper
  policy, MaterialIR 2.0, Bundle 2.2, target profile hash, public Shader
  properties, and Blender exporter 2.2.0 unchanged.

## 2.2.0 — 2026-07-30

- Preserved supported static PNG, JPEG, and EXR Image Texture nodes as sealed
  editable Texture2D resources instead of scheduling the GPL bake worker.
- Added target-neutral `Texture.SampleImage2D`, tangent OpenGL normal-map
  strength, height-derived fragment normals, normal blending, and Object-space
  vertex displacement with explicit LOD 0 height sampling.
- Added an explicit DirectX negative-Y normal convention, packed scalar
  channel bindings, component extraction, One Minus, AO/Base Color
  composition, Alpha, and Emission Mask without filename inference.
- Translated Blender material displacement modes independently: `BUMP`
  combines height bump with the Principled normal, `DISPLACEMENT` writes
  Vertex Position, and `BOTH` emits both paths.
- Added `miku-bundle-2.2` with `image/jpeg` and `Height` resource semantics
  while retaining MaterialIR, Conversion Plan, Conversion Manifest, and target
  profile schema 2.0.
- Added `_MIKU_HeightMap`, `_MIKU_HeightMidlevel`, and
  `_MIKU_HeightScale` without renaming existing public material properties.
- Added `_OcclusionMap`, `_MIKU_EmissionMask`, `_Opacity`,
  `_AlphaClipThreshold`, `_MIKU_BumpStrength`, and `_MIKU_BumpDistance`;
  packed physical images receive deterministic `_MIKU_Packed_*` references.
- Applied AO exactly once as
  `BaseColor * lerp(1, AO, OcclusionStrength)`, retained Roughness until the
  single Unity Smoothness inversion, and reused one packed texture sample per
  resource/UV/LOD.
- Preserved colored HDR Principled emission without an artificial whitening
  curve, exact Principled/Emission Mix Shader mask weights, and additive
  Principled/Emission Add Shader topology.
- Preserved Principled Alpha for Blend/Dithered workflows and automatically
  selected Alpha Blend with a conversion diagnostic when effective Alpha is
  connected on an otherwise Opaque Blender material.
- Added explicit failures for unsupported image sources, projections, filters,
  UV mappings, normal spaces, displacement spaces, linked displacement Normal,
  and dynamic/non-finite displacement parameters.
- Released the Blender Semantic Exporter and Unity package as 2.2.0. Unity
  2.2.0 continues to read safe Bundle 2.0/2.1 documents; older packages reject
  Bundle 2.2 as an unknown schema.

## 2.1.0 — 2026-07-30

- Made `Auto`, `PreferNative`, and `ReusableBakeOnly` portable: they no longer
  emit `meshBindingRequired` Texture2D resources and fail with
  `MIKU_PORTABLE_MESH_BAKE_REQUIRED` when no portable route exists.
- Preserved Object Position, Point Mapping, 3D Noise Factor, scalar math,
  Color Ramp, and Normal From Height as editable runtime expressions. Noise is
  a declared clean-room approximation and Strict rejects it.
- Renamed the `AllowMeshBake` presentation to Source Mesh Fidelity. Its baked
  textures now ship with a deterministic evaluated GLB, mesh fingerprint set,
  renderer-slot bindings, and Bundle 2.1 sealing.
- Added Unity glTFast import, stable Mesh/Prefab/binding-description assets,
  renderer fingerprint validation, and a guarded Apply to Selected Renderer
  action. Unsafe legacy mesh-bound Bundle 2.0 files are rejected.
- Released the Semantic Exporter and Unity package as 2.1.0 and the GPL Bake
  Worker as 1.2.0. MaterialIR, Value Graph, and public Shader property names
  remain 2.0.

## 2.0.3 — 2026-07-29

- Fixed Blender 5.2 typed Mix input selection by preferring exact socket
  identifiers, excluding inactive/unavailable sockets, and using value type
  only to disambiguate remaining active candidates.
- Preserved Principled emission as Emission Color multiplied by Emission
  Strength, with deterministic constant folding and one stable multiply for a
  dynamic non-unit strength.
- Lowered static baked closure weights and closure-composite parameters to
  executable `Texture.SampleBaked2D` expressions instead of leaving
  `requiresBake` records in the final weighted closure tree. Global Normal
  retains its single channel-bake ownership.
- Connected closure-aware Normal expressions to Normal TS and transformed the
  final tangent normal to world space for multi-lobe lighting, Layer Weight,
  and Fresnel without creating recursive Normal expressions.
- Added `MIKU_GENERATED_RESOURCE_UNREFERENCED` validation so sealed
  `_MIKU_Baked_*` resources cannot disappear from generated Shader Graph
  properties.
- Kept evaluated closure-composite radiance visible by clearing `_BaseMap` and
  using neutral white `_BaseColor` modulation for Transparent Emission,
  Transparent Lit, and Custom Multi Lobe materials.
- Released the Blender Semantic Exporter and Unity package as 2.0.3 and the
  GPL Bake Worker as 1.1.2. Public interchange schemas remain 2.0; Unity 2.0.3
  explicitly accepts the known 2.0.2 target profile.

## 2.0.2 — 2026-07-29

- Fixed static Blender `Bump` chains, including nested/grouped chains, by
  scheduling exactly one channel-scoped Normal `MeshBake` instead of sending
  `Vector.Bump` to the runtime-expression compiler.
- Treated one supported surface lobe's linked Normal as the global surface
  Normal; only genuinely distinct normals across active lobes remain
  Unsupported.
- Added the declared `Urp17ClearCoat` approximation for the safe Principled
  Coat subset. Coat Weight maps to Coat Mask and Coat Roughness maps to
  `1 - roughness`; non-default Coat IOR, Tint, or Normal remain Unsupported,
  and Strict fidelity rejects the approximation.
- Added a Unity-authored URP/Shader Graph 17.4 Clear Coat wrapper, explicit
  wrapper compatibility checks, and bounded import compatibility for
  non-Coat 2.0.0/2.0.1 target profiles.
- Released the Blender Semantic Exporter and Unity package as 2.0.2. The GPL
  Bake Worker remains 1.1.1 and all public interchange schemas remain 2.0.

## 2.0.1 — 2026-07-29

- Fixed Unity imports of a persistent material identity copied under a new
  bundle/output root by reusing its authoritative generated directory and
  retaining Wrapper, Sub Graph, base Material, Material Variant, Scene/Prefab
  references, and stable GUIDs.
- Added bounded Assets-wide identity discovery and preflight
  `MIKU_ASSET_GUID_COLLISION` failures before generated-asset writes.
- Fixed MaterialIR 2.0 Premultiply propagation to URP Shader Graph 17.4 and
  generated transparent Material tags, preventing transparent-emission color
  from being multiplied by alpha twice.
- Kept the Blender Semantic Exporter at 2.0.0; this release changes only the
  Unity package and does not revise Bundle, MaterialIR, or identity schemas.

## 2.0.0 — 2026-07-29

- Added target-neutral closure graphs, symbolic Blender 5.2 Mix/Add weights,
  weighted closure sets, proved simplification, feature analysis, and explicit
  real-time closure budgets.
- Added MaterialIR, conversion plan, manifest, bundle, and target-profile 2.0
  schemas plus a bounded opaque Standard PBR v1 migration.
- Added `Auto` compatibility and `Strict` fidelity policies, and explicit Add
  Shader energy policies. Unsupported required closures no longer collapse to
  constants or averaged Standard PBR inputs.
- Added six Unity surface generators: Opaque PBR, Cutout PBR, Transparent Lit,
  Transparent Emission, Refractive Glass, and Custom Multi-Lobe.
- Added independent per-lobe URP 17.4 lighting for main/additional lights,
  shadows, cookies, probes/SH, Forward/Forward+, and pass fog.
- Added scalar premultiplied transparency, colored Scene Color transmittance,
  deterministic runtime weight DAG lowering, and structured project-setup and
  fidelity diagnostics.
- Added real Blender 5.2 headless export coverage and Unity 6000.4.5f1
  Shader Graph import/compile/determinism coverage for closure-aware materials.
- Documented that custom-lighting SSAO and linked per-lobe normals are not
  silently approximated; Auto diagnoses the former and both are rejected where
  fidelity cannot be preserved.

## 1.2.1 — 2026-07-29

- Fixed Blender 5.2 unavailable Glass Weight sockets overriding the dielectric
  default, so new Glass exports use `TransmissionWeight=1` and no longer become
  black solely because refraction was multiplied by zero.
- Added explicit native planner routes for direct Transparent and Glass
  closures while preserving Facing-driven Transparent/Glass coverage.
- Constant-folded fully constant Emission Color/Strength before MaterialIR
  planning, preventing Magic Ball 7/10 from requesting an unsafe final-lighting
  channel bake.
- Fixed multi-stop Color Ramp generation re-adding the same deterministic
  element nodes, and added root reflection-exception diagnostics.
- Added bounded 1.2.0 compatibility: non-dielectric surface bundles import in
  Unity 1.2.1, while dielectric bundles require re-export instead of silently
  accepting a possibly unintended zero transmission weight.
- Reworked the real Magic Ball corpus gate to run Auto mode without appearance
  snapshot permission and added mixed/direct Glass Blender coverage.

## 1.2.0 — 2026-07-28

- Added strict optional `miku-surface-1.0` contracts to MaterialIR 1.0 for
  Standard Lit, alpha-blended, dithered, and dielectric screen-refraction
  surfaces. Older MaterialIR without the companion remains Opaque-compatible.
- Lowered Transparent and Glass BSDF closures into independent surface
  parameters and coverage so Emission is never pre-multiplied and then
  multiplied by Alpha a second time.
- Added exact Blender 5.2 Overlay expansion, fragment-only Normal From Height
  for `BUMP`, and precise required-chain Light Path rejection.
- Added Unity-authored URP 17.4 Alpha Blend, Dithered, and transparent Unlit
  dielectric wrapper templates, including Scene Color refraction, Schlick
  Fresnel, Reflection Probe, and `Smoothness = 1 - Roughness`.
- Added project-setup diagnostics for Camera Opaque Texture and Linear color
  space, wrapper render-contract ownership protection, and the public optical
  properties `_IOR`, `_TransmissionWeight`, `_Opacity`,
  `_RefractionStrength`, `_ReflectionStrength`, and `_Thickness`.
- Added the real eleven-material Magic Ball Blender 5.2 corpus and a
  non-destructive `魔法球.miku-fixed.blend` repair workflow.

- Fixed black URP Lit output for Blender materials whose unconnected closure
  Normal socket exported Blender's `[0, 0, 0]` sentinel. New MaterialIR uses
  neutral tangent normal `[0, 0, 1]`, while Unity normalizes existing 1.1.1
  bundles and emits `MIKU_LEGACY_ZERO_NORMAL_NORMALIZED`.
- Added a Standard PBR semantic material panel exposing twelve real PBR controls
  while retaining generated node defaults as hidden Shader Graph properties.
- Split Standard PBR and Generic Toon wrapper templates without changing the
  generated Sub Graph signature, and added safe exact-match wrapper migration.
- Fixed constant Base Color, Metalness, Roughness, Normal, Emission, and
  Occlusion binding for the editable Standard PBR workflow.
- Made shared Blender and Unity output roots a supported workflow. New material
  directories use `<material>__<12-character-material-id>`, while existing
  identity-matched and legacy name-only directories are reused.
- Moved persistent Material IDs into Blender Material custom properties,
  retained IDs across renames, repaired copied-material ID duplicates, detected
  copied `.blend` identity reuse, and added the confirmed **Fork Source
  Identity** operation.
- Retired `.migr-identities.json` as an output-root ownership lock. Matching
  legacy entries migrate into Blender data; missing, malformed, or foreign
  registries warn without blocking and are never rewritten.
- Made Unity locate generated assets by Persistent Source ID plus Persistent
  Material ID and retain recorded wrapper/base/variant paths and existing GUIDs
  across source material renames.
- Added explicit output identity collision/duplicate diagnostics and regression
  coverage for atomic rollback, shared roots, same-name materials, renames,
  Prefab references, Scene references, and Material Variants.

## 1.1.1 — 2026-07-28

- Restricted MaterialIR reachability to the active Material Output Surface so
  inactive and disconnected nodes cannot schedule spurious bake jobs.
- Added native Hue/Saturation/Value and two-element B-Spline Color Ramp
  expressions with Blender-compatible numerical oracles.
- Added maximal static expression-island extraction and deterministic UV0
  Color, Scalar, and tangent-normal resources inside dynamic material chains.
- Added editable closure flattening for recursive Mix Shader, Diffuse,
  Emission, and zero-anisotropy Glossy/Anisotropic corpus cases, with explicit
  approximate diagnostics.
- Added native Shader Graph 17.4 expression-texture sampling and RGB/HSV node
  expansion, plus specific island bake failure propagation.
- Added the locked `彩色镀层.blend` 14-material corpus acceptance fixture and
  retained the `miku-*-1.0` interchange document family.

## 1.1.0 — 2026-07-28

- Added a deterministic, typed MaterialIR expression DAG while retaining the
  `miku-material-ir-1.0` document contract and existing Constant/TextureResource
  channel values.
- Preserved Blender Geometry Incoming/View Direction, Camera Data, Miku Time,
  Fresnel, and Layer Weight as runtime expressions instead of UV-baking them.
- Added the versioned Blender **Add Miku Time Node** operation and safe affine
  root-node-tree `frame` driver migration without `eval`.
- Added the native Shader Graph 17.4 runtime backend, including current-camera
  Camera Data math, physical dielectric Fresnel, distinct Layer Weight outputs,
  and controllable Unity time properties.
- Added `MIKU_RUNTIME_INPUT_PRESERVED`,
  `MIKU_RUNTIME_INPUT_UNSUPPORTED`, and
  `MIKU_TIME_DRIVER_EXTERNALIZED` diagnostics; the GPL worker now retains the
  specific runtime-input error instead of wrapping it as a generic bake
  execution failure.
- Added channel-scoped mesh baking for runtime materials. Independent static
  semantics such as procedural Roughness are baked without flattening a
  View Direction, Camera, Fresnel, Layer Weight, or Time branch.
- Fixed the canonical Miku 1.x source boundary and exact Steam Blender 5.2.0
  executable in repository policy and CI checks.

## 1.0.0 — 2026-07-27

- Unified every interchange document on `miku-*-1.0` and
  `schemaVersion: "1.0"`; removed the contradictory root `version` field.
- Made `workflow.kind` mandatory and added the five explicit workflow kinds.
- Replaced `.migrbundle.json` with the Unity-importable `.migrbundle` entry.
- Added atomic Blender export and separate deterministic MIT Semantic Exporter
  and GPL Bake Worker extension ZIPs.
- Simplified the Blender panel to export only the active material slot, generate
  persistent source identity automatically, expose one material Workflow,
  conditionally show Game Part, and keep Conversion Mode under a collapsed
  Advanced section. The batch export API remains available for automation.
- Added Unity `.migrbundle` automatic import, sibling dependency tracking,
  durable delayed generation, transaction recovery, and Console plus asset
  diagnostics.
- Added a diagnostic-only `.miku` importer. MIKU 2/3/4/5 and `.b2ubundle`
  compilation/migration code has been removed; old assets must be re-exported.
- Unified Standard PBR, Generic Toon, Genshin, WuWa, and HSR behind one
  MaterialIR/Bundle importer and an internal backend registry.
- Added generated base Materials plus user-owned Material Variants whose
  overrides survive regeneration.
- Made generated Sub Graph file names ASCII-safe and added compiler-message
  severity validation so Unicode material names cannot commit pink/error
  Shader Graph receipts.
- Marked the original Genshin/WuWa/HSR Shader/HLSL backends MIT with SPDX
  notices and recorded their maintainer provenance and non-affiliation status.
- Removed old B2U Python/C# APIs, schemas, importers, duplicate tests, and
  obsolete release scripts.
