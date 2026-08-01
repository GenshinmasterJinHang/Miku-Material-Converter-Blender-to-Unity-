# Changelog

## Unreleased

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
