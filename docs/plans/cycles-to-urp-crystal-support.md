# Cycles crystal materials to Unity URP Shader Graph

## Purpose and outcome

Implement the first production-oriented vertical slice for Blender Cycles
dielectrics: resolve the Cycles Material Output, lower Glass BSDF, Principled
Transmission, Refraction BSDF, supported closure combinations, and Volume
Absorption into target-neutral MiGR optical semantics, then generate an
editable Unity 6000.4.5f1 / URP 17.4.0 / Shader Graph 17.4.0 wrapper plus
MiGR-owned subgraph. Validate the implementation with the real `.blend` files
under `材质库/玻璃` and `材质库/宝石` in addition to small deterministic fixtures.

The result is an explicitly approximate real-time URP translation, not a port
of Cycles and not a claim of path-traced equivalence.

## Context and constraints

- Repository root is intentionally dirty: the initial inspection found 592
  deleted, 253 modified, and 157 untracked status entries. Existing work is
  user-owned and must not be reverted or reformatted.
- No nested `AGENTS.md` exists. Root `AGENTS.md` and `PLANS.md` govern this work.
- Canonical core graph contract is `mgir-2.0`. New optical data will be an
  additive, versioned companion object so legacy consumers can continue to read
  unaffected fields. Schema, migration, compatibility, diagnostics, and
  changelog documentation must move together.
- Core code remains pure Python and must not import `bpy` or Unity types.
- Unity internal Shader Graph types and slot IDs remain isolated under
  `Editor/ShaderGraph`.
- Generated `*.generated.shadersubgraph`, map, and report files are MiGR-owned.
  Wrapper `.shadergraph` is user-owned after initial creation and is overwritten
  only in explicit Full Regeneration mode.
- Installed target is Unity `6000.4.5f1`, URP `17.4.0`, Shader Graph `17.4.0`.
  Installed Blender used by the existing material-library runner is `5.2.0
  LTS`; Blender `5.0.x` is not currently installed and cannot be reported as
  executed.
- Real source corpus contains four files: `材质库/玻璃/玻璃.blend`,
  `材质库/宝石/宝石/宝石.blend`, `材质库/宝石/宝石库/宝石库1.blend`, and
  `材质库/宝石/魔法发光宝石/魔法发光宝石.blend`.
- URP screen-space refraction requires the Camera Opaque Texture and Linear
  color space. The generator reports requirements; it does not mutate project
  settings.

## Progress

- [x] 2026-07-22: Read root instructions, `PLANS.md`, architecture/ownership/
  diagnostics/schema documents, compatibility matrix, node matrix, existing
  tests, and current Git status.
- [x] 2026-07-22: Confirmed exact Unity/URP/Shader Graph versions and inspected
  the installed Shader Graph 17.4.0 source for Scene Color, Screen Position,
  Fresnel, Reflection Probe, and arithmetic node slots.
- [x] 2026-07-22: Enumerated the requested real material corpus and inspected
  prior export evidence.
- [x] 2026-07-22: Implement Cycles Material Output resolution and Surface/Volume/
  Displacement entry export.
- [x] 2026-07-22: Implement target-neutral closure, optical, thickness, feature, mesh, and
  structured diagnostic lowering.
- [x] 2026-07-22: Extend the public schema and Unity model/import validation for the
  additive optical contract.
- [x] 2026-07-22: Implement the URP crystal Shader Graph path and project-requirement report.
- [x] 2026-07-22: Add unit, negative, determinism, Blender, and Unity EditMode tests.
- [x] 2026-07-22: Re-exported and inspected all four requested glass/gem `.blend` files with Blender 5.2.0 LTS: 36 MiGR materials total.
- [x] 2026-07-22: Imported the seven supported real glass optical materials into Unity and verified every generated Shader Graph has no compiler messages.
- [x] 2026-07-22: Imported the five supported real gem optical materials into Unity and verified every generated Shader Graph has no compiler messages.
- [x] 2026-07-22: Update architecture, feature, support, diagnostics,
  migration, ADR, and changelog documentation.
- [x] 2026-07-22: Added live Shader Graph expressions for the real-corpus
  Gradient, Brightness/Contrast, Modulo, and Multiply Add routes and verified
  that those operations no longer cause optical property fallback diagnostics.
- [x] 2026-07-22: Added explicit constant/texture thickness authoring, strict
  schema rules, Blender 5.2 headless smoke coverage, and an editable Unity
  thickness-map chain.
- [x] 2026-07-22: Added explicit ThinSurface authoring, removed its closed-mesh
  requirement, and verified the URP wrapper uses the both-face target mode.
- [x] 2026-07-22: Added active-chain feature flags and diagnostics for Light
  Path ray depth, caustics assumptions, and Wavelength-driven dispersion.
- [x] 2026-07-22: Removed source/export absolute filesystem paths from the new
  public thickness texture resource while retaining portable URI provenance.
- [x] 2026-07-22: Documented a reproducible crystal reference scene and visual
  acceptance procedure without claiming unexecuted image parity.
- [x] 2026-07-22: Ran final task-focused Python/schema/Unity checks, reviewed the scoped changes, and recorded results here.
- [x] 2026-07-23: Continued visual-fidelity validation after the 8081 preview
  exposed disconnected Base Color/Emission controls, missing crack normals,
  and five omitted Principled Volume magic gems.
- [x] 2026-07-23: Added an explicit Principled Volume surface-glow approximation,
  re-export the four source libraries, import the newly allowed optical assets,
  and rebuild the live Unity preview scene with screenshot evidence.
- [x] 2026-07-23: Resolved typed Blender reroutes and implicit scalar/vector
  conversions, restored unconnected zero normals to geometry-normal semantics,
  and verified all 17 live preview shaders with zero compiler errors/warnings.
- [x] 2026-07-23: Promoted the recurring zero-default-Normal repair to an
  end-to-end MiGR invariant: source socket semantic, typed optical expression,
  composition normalization, strict schema rule, Unity resolution, legacy
  compatibility, and live 8081 regression validation.
- [x] 2026-07-23: Extended the material-active-chain compiler to cover Brick,
  Checker, Environment, Gradient, IES, Image, Magic, Noise, legacy Point
  Density, Sky, Voronoi, Wave, and White Noise with an explicit native,
  baked, runtime-required, or unsupported route for every reachable output.
- [x] 2026-07-23: Added the additive `b2u-hybrid-plan-1.1`,
  `b2u-bake-1.1`, and `cycles-optical-1.1` compatibility contracts while
  retaining strict readers for existing 1.0 documents.
- [x] 2026-07-23: Added Blender 4.5 legacy Point Density extraction and
  snapshot metadata without reintroducing the removed node into Blender 5.x.
- [x] 2026-07-23: Added per-optical-slot Texture2D parity resource generation,
  re-exported the four requested glass/gem source libraries, and validated 17
  selected materials plus their original meshes in the live 8081 preview scene.
- [x] 2026-07-23: Added Shader Graph 17.4 Texture3D property/sample emission,
  deterministic EXR-slice-atlas to `.texture3d.asset` materialization, and
  standalone HDR directional-LUT generation for Sky/IES/conditional
  Environment. Blender 5.2 top-level and nested-group Noise plus Sky smokes
  pass; live 8081 import binds a 16³ Texture3D and reports zero shader messages.

## Discoveries

- The current `_find_active_output` accepts an active EEVEE output and otherwise
  returns the first output. It does not implement the required CYCLES/ALL
  priority or diagnostics.
- Only `entry.surface` is emitted even though the output node contains Volume
  and Displacement sockets.
- `ShaderNodeVolumeAbsorption` is currently classified as unsupported.
- Glass/Refraction nodes are exported as conditional graph nodes. The Unity
  backend does not lower them to optical semantics; ordinary closure Mix is
  expanded as per-channel Lerp, which is not valid for Fresnel reflection plus
  refraction.
- A prior real diamond export contains Light Path on the required surface chain
  but only warns and still generates. The new feature analyzer must block that
  optical conversion rather than silently approximate a ray-type branch.
- Shader Graph 17.4.0 locally contains `SceneColorNode` (UV slot 0, fragment
  output 1, requires camera opaque texture), `ReflectionProbeNode` (object-space
  view/normal inputs 0/1, LOD 2, output 3), `FresnelNode`, and the required
  arithmetic nodes. This is stronger and safer than inventing MultiJson fields.
- Existing material-library exports were run with Blender 5.2.0 LTS. Two source
  files produced complete artifacts followed by a Blender exit access violation;
  artifact markers and Unity import results must be distinguished from process
  exit cleanliness.
- The generic automatic PBR bake step originally overwrote an already-selected
  `OPTICAL_CRYSTAL` backend. The workflow gate now treats an allowed versioned
  optical companion as authoritative because a fixed PBR bake cannot preserve
  refraction, Fresnel, or absorption.
- Eight real glass variants mix two Principled Transmission closures inside a
  node group. Parameter-level typed Lerp expressions preserve this supported
  pattern; three other variants mix a dielectric result with Diffuse and remain
  explicit bake fallbacks.
- The Python 3.13 repository-wide checkpoint executed 315 tests with 7 failures,
  31 errors, and 19 skips. Most failures remain in legacy ShaderLab/toon
  expectations whose compiler result contains no `shaderSource`, deleted dist
  archives, and concurrent version/channel mismatches. Concurrent edits also
  advanced the add-on to 0.6.1 and changed HSR light-map channel semantics
  without updating two old expectations. The final task-focused run excludes
  those two unrelated assertions rather than rewriting them.
- The real export surfaced a pre-existing strict-schema drift: every current
  Blender document contains `schemaVersion` and `nprFeatures`, while the
  canonical graph schema rejected both as additional properties. The graph
  schema now validates the existing overlay contract; all 36 corpus documents
  pass.
- Reimporting all seven generated graphs on Windows exposed a transient
  delete-denying file handle on a report sidecar. Bounded `File.Replace`
  retries preserve atomicity and make the real-corpus regeneration pass; a
  lock-contention EditMode regression test covers the behavior.
- The real 36-material corpus was exported by add-on 0.6.0. Current-source
  validation separately covers add-on 0.6.1 (Blender ThinSurface/thickness
  smoke) and Unity package 0.9.1 (Shader Graph generation suite), preserving
  the historical evidence instead of relabeling it.
- A final repository-wide rerun after the ThinSurface schema consistency test
  was added timed out in the command runner without a result. The new test is
  included in the passing 90-test task suite; the latest completed full-suite
  checkpoint therefore remains the 315-test result recorded below.
- The live 8081 preview showed that an optical input with a live Socket/Lerp
  expression bypassed the corresponding Shader Graph property. The property
  remained visible in the material inspector but had no edge into the final
  graph, so Base Color and Emission edits could not affect procedural materials.
- The optical `BuildSubGraph` path returned before the ordinary displacement
  fallback ran. Real cracked glass therefore preserved the source displacement
  nodes but did not connect their height-derived normal to `Normal TS`.
- All five magic gems contain a valid Principled Transmission surface plus a
  Principled Volume closure. Treating Principled Volume as the same hard error
  as raw Volume Scatter forced all five to `BAKED_PBR` and excluded them from
  the optical preview.
- The real magic-gem graph routes a color through a Blender Reroute into both
  color and scalar sockets. Shader Graph's Redirect node began as scalar and
  rejected four connections. Treating reroutes as typed aliases and expanding
  Blender's implicit luminance/splat conversions preserves the graph.
- Three glass variants mixed two zero default Normal expressions. Emitting the
  derived zero vector caused `normalize(0)` division warnings and potential NaN
  rendering; both branches semantically mean the unconnected geometry normal.
- The zero vector was introduced by optical lowering, not by an explicit source
  connection: Blender's unconnected Normal socket stored the zero sentinel, the
  first optical implementation classified every unlinked input as Constant,
  and Lerp composition then propagated that invented value. Fixing only the
  Unity graph leaves every other target exposed to the same ambiguity.
- Real v5 export showed the same semantic arriving through node-group
  flattening as a synthetic `Input.Vector(0,0,0)`. The existing
  `groupInterfaceMappings` `input_default` provenance distinguishes this case
  from a user-connected zero vector and must participate in lowering.
- Blender 5.2 exposes all requested texture nodes except
  `ShaderNodeTexPointDensity`; Blender 5.0 release notes confirm Point Density
  was removed. The compatibility path therefore requires a 4.5 exporter and
  cannot be tested by constructing the node in the installed 5.2 process.
- `ShaderNodeTexEnvironment` is mapped to `Texture.Environment` today but
  bypasses the Image Texture resource exporter, leaving the Unity graph without
  an HDR resource. IES and Sky are still classified as unsupported.
- The current generic `_texture_params` drops mode-defining properties and
  sockets such as Noise dimensions/type/normalize/offset/gain, Voronoi
  dimensions/normalize/smoothness/exponent/randomness, Wave profile/detail
  scale/phase, Brick layout controls, and Magic turbulence depth.
- Eight-bit/PNG optical IOR output silently clamps physical values above one
  unless the interchange records a reversible representation. Algorithm
  revision 5 therefore stores `(ior - 1) / 9` and declares the inverse linear
  scale/bias for the target; older parity slots retain identity decoding.
- The hybrid planner currently counts an operation as supported from a static
  set even when the Shader Graph registry has no implementation. Coverage must
  be computed from a concrete per-node representation decision.
- Installed Shader Graph 17.4 contains Texture3D and Cubemap properties plus
  `SampleTexture3DNode`, `SampleRawCubemapNode`, and `SampleCubemapNode`. These
  verified package types permit version-isolated baked spatial and directional
  resources without exposing Unity internal names in MGIR.

## Decision log

- 2026-07-22: Keep the core graph version at `mgir-2.0` only if the optical
  payload is a separately versioned additive companion (`cycles-optical-1.0`)
  accepted by the current schema. Reject unknown optical companion versions.
  This avoids redefining existing graph node semantics while still versioning
  the new public surface.
- 2026-07-22: Use installed Shader Graph's Reflection Probe node for the default
  reflection source. A cubemap fallback is deferred unless the verified node
  cannot be generated/imported.
- 2026-07-22: Use one Scene Color sample in the default path. Rough refraction
  remains an explicit approximation without an unbounded blur sampling graph.
- 2026-07-22: Preserve source socket expressions in the optical companion using
  typed expression references plus source node/socket provenance; do not reduce
  all inputs to constants.
- 2026-07-22: Expose Roughness as the source property and derive Smoothness with
  One Minus. Do not expose an independent Smoothness value that could violate
  the Blender roughness invariant.
- 2026-07-22: Use a transparent Unlit wrapper for the optical composition. Scene
  Color and Reflection Probe values are already lighting results; a Lit wrapper
  would illuminate them a second time.
- 2026-07-22: Real corpus output goes to a new task-specific output directory;
  existing output and Unity test directories are not overwritten.
- 2026-07-22: Keep atomic replacement rather than delete-then-move or direct
  overwrite. Retry only `IOException` with a bounded exponential delay so an
  exhausted retry leaves the previous asset intact and reports the failure.
- 2026-07-22: Admit overlay versions 1 through 3 and the existing strict
  `nprFeatures` shape in the canonical `mgir-2.0` schema. This is an additive
  compatibility correction, not a new overlay version.
- 2026-07-22: Keep thickness inside `cycles-optical-1.0` because the existing
  schema already reserved Texture/BakedTexture sources; make the resource,
  channel, UV set, and scale strict when those source values are selected.
- 2026-07-22: Reproduce Blender 5.0 Gradient and Brightness/Contrast formulas
  as editable Shader Graph arithmetic. Unsupported data-node routes remain
  visible property fallbacks with structured diagnostics.
- 2026-07-23: Keep live Cycles source expressions authoritative, but multiply
  Base Color, Emission Color, and Emission Strength by identity-default authoring
  properties. This preserves procedural detail and makes inspector edits real.
- 2026-07-23: Approximate Principled Volume only when it accompanies a supported
  optical surface. Preserve density, absorption color, emission color, emission
  strength, and anisotropy in typed IR; use thickness-based surface absorption
  and glow in URP. Raw Volume Scatter remains a blocking runtime requirement.
- 2026-07-23: Route optical material displacement through the existing
  `NormalFromHeight` chain into `Normal TS`. Do not claim true tessellated Cycles
  displacement or silently move the operation into Vertex Position.
- 2026-07-23: Keep the translated Cycles tint on refraction, while applying the
  identity-default `_BaseColor` control to final optical composition. This
  keeps physical source intent and makes authoring visible when Scene Color is
  unavailable in an editor preview.
- 2026-07-23: Resolve Blender Reroute as a typed alias and materialize implicit
  color-to-factor and scalar-to-vector conversions. Do not rely on Shader
  Graph's default scalar Redirect type or connection-order inference.
- 2026-07-23: The Unity constant-zero/Lerp recognition remains a compatibility
  reader for already exported documents, not the canonical representation.
  New exports mark the Blender input as `implicit_geometry_normal`, lower it to
  a constant-free varying `ImplicitGeometryNormal`, and collapse two implicit
  normal branches before serialization. An explicitly connected zero vector is
  intentional data and must not be reinterpreted.
- 2026-07-23: Limit the texture expansion to material Surface, optical inputs,
  Normal/Bump, Emission, Alpha, and Displacement. World, Light, and true Volume
  outputs remain separate features; Sky, Environment, and IES are supported
  here only as material texture functions.
- 2026-07-23: Use capability-driven routing. Checker and the already-certified
  Gradient path remain native; simple undistorted Wave may remain native.
  Blender-specific noise/hash/cell/brick algorithms default to explicit baked
  parity instead of copying GPL implementation or claiming an unverified
  equivalent.
- 2026-07-23: Keep the `mgir-2.0` envelope. Version the new optional route and
  bake payloads as 1.1 companions, read both 1.0 and 1.1, and reject unknown
  companion versions. Existing public shader property reference names are not
  renamed.
- 2026-07-23: Select bake representation from coordinate domain: UV and
  surface-dependent results use per-mesh Texture2D, pure object/generated
  spatial functions use Texture3D, and direction functions use an
  equirectangular HDR LUT. View/ray-dependent required branches fail with
  `RequiresRuntimeSupport` rather than freezing silently.
- 2026-07-23: Treat baked optical inputs as typed parity slots, including
  BaseTint, Roughness, IOR, Normal, Absorption, Emission, and Alpha. A final
  normal destination always bakes/imports as tangent-space normal data and
  never as a zero-vector placeholder.
- 2026-07-23: Encode PNG-backed optical IOR as `(ior - 1) / 9` and restore it
  in Shader Graph with deterministic Multiply/Add nodes. Keep `decodeScale` and
  `decodeBias` additive so existing 1.1 documents remain identity decoded.

## Implementation sequence

1. Add pure-Python Cycles output resolver and optical lowering module; integrate
   it into `export_material_graph` after stable graph IDs/edges exist.
2. Add schema definitions and strict validation tests for optical documents,
   finite IOR/density/thickness, entry volume/displacement, diagnostics, and
   unknown companion versions.
3. Extend Unity deserialization and the Shader Graph 17.4 URP backend with a
   dedicated optical builder that creates deterministic properties/groups,
   Scene Color refraction, IOR Fresnel, Reflection Probe reflection, One Minus
   smoothness, thickness, and Beer-Lambert transmittance.
4. Add project-setting detection and complete `.migrreport.json` data without
   changing the URP asset.
5. Add minimal pure-Python, Blender headless, and Unity EditMode coverage.
6. Run the four requested real source files through the public Blender operator,
   then import the results through the exact Unity project and inspect reports.
7. Update public docs and final compatibility evidence.
8. Add per-node Blender parameter/resource extraction and material-chain
   reachability/domain analysis for all thirteen texture node families.
9. Add deterministic baked-branch records, source dependency/cache metadata,
   and typed optical parity slots without replacing the authoritative source
   DAG.
10. Add Blender boundary materializers for 2D, 3D, and directional resources;
    use temporary meshes/data and atomic output replacement.
11. Extend the Shader Graph 17.4 adapter with verified Texture3D/directional
    sampling, exact Checker expansion, explicit conditional routes, and
    per-slot hybrid branches.
12. Add schema, planner, Blender headless, Unity EditMode, determinism,
    negative, and zero-normal regression coverage.
13. Re-export the glass/gem libraries, import through the live 8081 Unity
    instance, rebuild the existing preview scene, inspect Console and generated
    graph compiler messages, and capture comparison evidence.

## Validation

Planned commands (record outcomes only after execution):

```powershell
python -m unittest tests.test_cycles_optical_semantics tests.test_exporter_core
python -m unittest discover -s tests -p "test_*.py"
python tools/ci/run_checks.py --profile pr
& 'C:\SteamLibrary\steamapps\common\Blender\blender.exe' --version
& 'C:\SteamLibrary\steamapps\common\Blender\blender.exe' --background <blend> --python tools/export_material_library_bundle.py -- <output> <name>
./tools/ci/run_unity_editmode.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe'
```

The texture-extension checkpoint also runs focused exporter/planner/schema
tests, a Blender 5.2 all-texture construction smoke, a Blender 4.5.8 Point
Density smoke when that executable is available, and Unity EditMode tests for
each selected route and resource type. Missing Blender 5.0 or 4.5 executables
must be reported as blocked rather than passed.

Expected semantic checks include: Cycles output priority; active-subgraph
pruning; Glass/Principled/Refraction closure lowering; Fresnel pattern
recognition; Volume Absorption plus Beer-Lambert monotonicity; `smoothness = 1 -
roughness`; invalid/non-finite optical inputs rejected; Light Path and Volume
Scatter block critical graphs; stable IDs, property references, report order,
and wrapper preservation.

## Results and follow-up

The requested corpus produced 36 MiGR materials from four `.blend` files. The
glass file produced seven `OPTICAL_CRYSTAL` materials and four explicit
`BAKED_PBR` fallbacks; the first gem library produced five baked, three hybrid,
and two node-graph materials with eleven blocking diagnostics; the second gem
library produced five optical, one baked, one hybrid, and three node-graph
materials with no blocking diagnostics; after Principled Volume support, the
magic-emissive gem file produced five optical materials with no blocking
diagnostics. The current-source v5 rerun preserved those backend counts, wrote
explicit implicit-normal semantics, and all 36 documents validate against the
canonical schema.

Unity 6000.4.5f1 / URP 17.4.0 / Shader Graph 17.4.0 on the live 8081 instance
imported all 17 preview materials (seven glass, five gem, and five magic gem).
Every report has zero errors, and all 17 generated shaders have zero compiler
errors and zero warnings. The complete Shader Graph generation test class passed
29/29, including deterministic output, optical groups, normal-space conversion,
project requirements, thickness textures, live Cycles data expressions,
ThinSurface both-face generation, Principled Volume glow, typed reroutes/socket
conversions, implicit geometry normals, and atomic replacement under a
delete-denying handle.

The final 2026-07-23 focused Python run passed 77/77. All 36 current-source v5
documents passed strict local-reference schema validation; the 17 selected
optical preview assets were copied into the live Unity project and regenerated.
The earlier task-focused run passed
90/90 after excluding two unrelated stale expectations (HSR light-map channels
and add-on version 0.6.0); the unified PR check passed all available stages but
remained red because `ruff` was not installed and the unrelated full Python
baseline had the 7 failures/31 errors described above.

Blender 5.0 was not executed because it is not installed. Blender 5.2.0 LTS
produced complete glass and first-gem v5 artifacts before an exit access
violation; the second gem and magic-gem exports exited cleanly. The 8081 preview
capture was reviewed: changing Base Color affected 30,902 pixels above 5/255,
disabling Emission Strength affected 2,933, and restoring identity defaults
returned within 2/255 render variation. Visual image-parity certification, nested transparent
refraction, multi-bounce internal reflection, caustics, spectral dispersion,
volume scattering, and exact rough refraction remain outside this vertical
slice.

The texture-extension implementation added complete per-node exporter
parameters, capability-driven routing, companion schemas 1.1, optical IOR and
tangent-normal parity baking, and Unity package 0.10.0 baked/live branches. A
focused Python/schema/add-on run passed 124/124. Blender 5.2 constructed and
exported all 12 texture nodes still present in Blender 5.x. The existing
six-channel automatic bake smoke passed, and a real procedural Glass material
produced six optical parity maps with non-flat IOR and Normal data. Focused live
8081 EditMode tests for optical parity and Environment sampling passed 2/2 with
zero C# or shader compiler errors. Blender 4.5.8 Point Density and Blender 5.0
execution remain unavailable; the version-gated bridge tests are checked in but
must not be reported as executed.

The final algorithm-revision-5 corpus contains 36/36 schema-valid MGIR
documents. Sixteen selected optical materials produced IOR and tangent-normal
parity maps; `彩色玻璃Cycles` decodes to an observed IOR maximum of 1.9529 and
none of the 16 normal maps contains a zero RGB vector. Unity
6000.4.5f1/URP 17.4.0/Shader Graph 17.4.0 on instance
`test@be08cbdd5b1db5e5` passed the complete package EditMode suite 39/39,
reported zero shader compiler errors/warnings, preserved the fresh V6 wrappers,
and rebuilt `Assets/CyclesPreview/Scenes/Cycles_Material_Preview.unity` with
three original source-model groups plus standard sphere/gem and cube displays.
The 1600x900 evidence capture is
`Assets/CyclesPreview/Captures/Cycles_Material_Preview_v6_final.png`.

The repository-wide Python baseline was also executed: 340 tests ran with 4
failures, 30 errors, and 19 skips. The 34 failing legacy dynamic/toon tests
expect a ShaderLab `shaderSource` even though this project constitution disables
that backend; they are outside the Shader Graph texture work and were not
suppressed or rewritten.

Algorithm revision 6 completed the reusable procedural-resource path. Blender
5.2 generated and then cache-reused a 16³ RGBAHalf Noise Texture3D atlas, the
same path passed through a nested node group, and Sky generated a 64×32 HDR
direction LUT in the low-resolution smoke profile. The live 8081 Unity instance
materialized the atlas as `Texture3D(16,16,16)`, bound it to the generated
material, imported both graphs, and reported zero shader messages. Two focused
EditMode tests for Texture3D binding and Sky direction sampling passed 2/2.
The real glass-library algorithm-6 export then produced 11/11 schema-valid
documents and 19 successful 128³ procedural Texture3D resources. The
`彩色玻璃Cycles` White Noise volume plus its optical parity slots were imported
to the live 8081 project; Unity materialized and bound the 128³ Texture3D,
generated an editable graph with zero shader messages, and added a native-branch
validation sphere to the saved preview scene. Nine v7 glass Normal maps contain
zero `[0,0,0]` pixels.

The final 4D safety pass records constant W and frame as `snapshotInputs`, emits
`texture_resource_4d_snapshot`, and rejects a linked/varying W as
`RequiresRuntimeSupport`. Blender 5.2 passed both paths, and repeating the
failure confirmed that incomplete resource caches are not reused as successes.
