# Source Mesh Hybrid Baking, Height, and Unity Binding

## Purpose and outcome

This plan makes Miku's Source Mesh and Full PBR paths preserve the actual
material semantics instead of producing plausible but disconnected textures.
The observable outcomes are: view-dependent coating remains dynamic while only
static unsupported islands are baked; closure-owned Bump inputs are discovered;
`CustomMultiLobe` does not export unused compatibility IOR/BaseColor resources;
current Miku bundles create an authoritative glTFast-backed Source Mesh prefab
and binding asset in Unity; and PBR export can carry a raw, adjustable Height
map without silently inventing a displacement approximation.

## Context and constraints

- Canonical source roots are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`. All required root markers and the
  package identity `com.miku.shaderconverter` were verified before editing.
- The worktree contains extensive pre-existing uncommitted changes. They are
  user-owned and must not be reverted, reformatted, or folded into this work.
- Blender validation must use the fixed Blender 5.2.0 executable documented by
  `AGENTS.md`. Unity validation targets Unity 6000.4.5f1 with URP and Shader
  Graph 17.4.0.
- Material IR, bake request, and bundle remain schema `1.0`. Their existing open
  optional fields can carry Height expressions, policy, and bake metadata.
- Generated Source Mesh, prefab, and binding assets are Miku-owned. Wrapper
  Shader Graphs and material variants remain user-owned.
- Legacy MiGR compatibility bundles remain read-only and must not gain current
  Miku automatic Source Mesh generation.
- glTF `TEXCOORD_0` remains `(u, 1-v)` in the GLB writer; Unity must not flip V
  again.

## Progress

- [x] 2026-08-01: Read repository instructions and `PLANS.md`; verified the
  canonical repository boundary, package identity, CodeGraph index, dirty
  worktree, and exact target versions.
- [x] 2026-08-01: Inspected the four supplied bundles and isolated the failures
  in planning, bake-task semantics, compatibility-channel binding, and Unity
  Source Mesh ownership.
- [x] 2026-08-01: Implement recursive closure-parameter planning, authoritative-channel
  filtering, precise diagnostics, and runtime/static island separation.
- [x] 2026-08-01: Add the material displacement policy, raw Height source planning, worker
  baking, cache revision, and deterministic replacement in the IR.
- [x] 2026-08-01: Restore current-Miku glTFast Source Mesh import, prefab/binding generation,
  fingerprint validation, and safe legacy-resource compatibility in Unity.
- [x] 2026-08-01: Add and execute focused Python/Core, Blender 5.2.0, and Unity EditMode
  tests. Exact re-export of three supplied materials remains unavailable because
  their source `.blend` files are not present in the workspace.
- [x] 2026-08-01: Update public documentation, compatibility notes, diagnostics, support
  matrix, and changelog; perform a final diff review.

## Discoveries

- 2026-08-01: `彩色镀层8` has a static unsupported Voronoi Color leaf behind a
  Color Ramp and HSV node, while Layer Weight/ViewDirection remains runtime
  dependent. The grayscale EXR is an intermediate static island, not the final
  colored coating.
- 2026-08-01: `凹凸石3` stores the active Bump normal as `requiresBake` inside
  `weightedClosures` parameters. The planner only scans top-level channels and
  expression roots, so it emits no bake job for that active closure input.
- 2026-08-01: `蓝玉` uses `CustomMultiLobe`; its authoritative BaseColor and IOR
  are closure parameters. Full compatibility projections at the top level are
  not consumed by that backend, yet the existing planner exports them and the
  Unity importer tries to bind `_MIKU_IOR`, which the generated graph does not
  declare.
- 2026-08-01: `玉石`'s GLB UV0 and baked texture occupancy agree, including
  padding. The current Unity importer explicitly ignores Source Mesh, so the
  texture is commonly applied to a non-authoritative mesh and appears shifted.
- 2026-08-01: the bake worker expands a `FullPBR` job to its hard-coded channel
  list instead of honoring the job's `semantics`, which explains the unplanned
  IOR output.
- 2026-08-01: the current Unity backend already reserves
  `_MIKU_HeightMap`, `_MIKU_HeightMidlevel`, and `_MIKU_HeightScale`, and already
  has object-space `Vector.Displacement` lowering. The missing work is raw
  Height planning/baking, vertex-safe sampling, defaults, and end-to-end
  binding.
- 2026-08-01: a historical implementation before the Miku package migration
  contains the required glTFast import, validation, stable mesh/prefab creation,
  and mesh-binding construction. It can be ported without reintroducing the
  retired package identity.

## Decision log

- 2026-08-01: Preserve runtime inputs in native expressions and bake only
  static unsupported islands. A UV texture containing ViewDirection or time is
  rejected rather than snapshot at an arbitrary camera/time.
- 2026-08-01: Use a single raw Height map only when all active displacement
  consumers share one stable source endpoint. Different sources are not ranked
  or combined heuristically; their normal paths remain independently baked and
  a structured diagnostic explains why no shared Height was emitted.
- 2026-08-01: Add `FOLLOW_BLENDER`, `ALWAYS_VERTEX`, and `MAP_ONLY` as an
  additive Blender material API with `FOLLOW_BLENDER` as the compatibility
  default.
- 2026-08-01: Bake Height before Scale/Midlevel as Linear R half-float EXR.
  Direct Blender Displacement keeps its Scale/Midlevel. Bump promotion uses
  Midlevel 0.5 and finite constant `Strength * Distance`, negated for Invert;
  dynamic or non-finite controls cannot be automatically promoted.
- 2026-08-01: Keep schema versions at `1.0`; add no mandatory fields and rename
  no shader properties.
- 2026-08-01: Restore automatic Source Mesh assets for current Miku bundles
  only. Retired MiGR bundles keep the existing compatibility-only behavior.

## Implementation sequence

1. Extend semantic/planning traversal to enumerate active weighted-closure
   parameter expressions and their consumer paths, classify static versus
   runtime dependencies, schedule exact endpoint island bakes, and rewrite every
   scheduled active parameter so no `requiresBake` marker survives.
2. Mark `CustomMultiLobe` top-level compatibility projections as
   non-authoritative for planning and resource generation. Improve fidelity
   diagnostics to report the deepest unsupported source plus its consumer path.
3. Add the Blender material displacement policy and snapshot it in normalized
   material data. Resolve direct Displacement and Bump height endpoints,
   deduplicate stable sources, validate promotion controls, and emit optional
   Height plus target-neutral `Input.MaterialChannel(Height)` feeding
   `Vector.Displacement`.
4. Make the bake worker honor each task's explicit semantics, support stable
   node/socket Height endpoints, bake Linear R half-float EXR, include policy
   and endpoint in cache identity, and increment the algorithm revision.
5. Update Unity graph lowering so vertex Height uses UV0 with explicit LOD 0 and
   property defaults come from IR. Skip only provably non-authoritative legacy
   compatibility resources; preserve hard failure for reachable missing
   properties.
6. Port the verified historical glTFast flow into the current Miku importer:
   verify sealed GLB/hash, import into stable Mesh assets, validate topology,
   UV0, submeshes, slots and fingerprints, create a Miku-owned prefab and
   `MikuMeshBindingDescription`, and bind material variants atomically.
7. Add focused unit/integration fixtures and documentation, then validate
   determinism and the supplied materials in the exact supported runtimes.

## Validation

Commands are recorded here only after they run. Planned validation includes:

- Core/Python focused tests for closure traversal, precise diagnostics,
  authoritative-channel filtering, FullPBR semantics, displacement policies,
  height deduplication/conflict, non-finite/dynamic promotion, and runtime input
  rejection.
- Blender 5.2.0 headless tests through
  `C:\\SteamLibrary\\steamapps\\common\\Blender\\blender.exe`, with an explicit
  `bpy.app.version == (5, 2, 0)` assertion, plus Height EXR metadata/range,
  padding, and deterministic re-export checks.
- Unity 6000.4.5f1 EditMode tests for import, stable GUIDs, glTFast Source Mesh
  prefab/binding, corruption/UV/slot atomic failures, asymmetric four-color UV
  orientation, Height controls, Shader Graph compilation, and wrapper ownership.
- Manual or scripted re-export/import of `彩色镀层8`, `凹凸石3`, `蓝玉`, and
  `玉石` using their supplied output directories as evidence, without modifying
  those directories unless an export command explicitly targets a new output.

### Executed commands

- `codegraph explore "ConversionPlanner closure parameters requiresBake Height SourceMesh import MikuBundleImporter"` — passed; used to identify current call paths.
- repository marker and package-identity preflight — passed; package name is
  `com.miku.shaderconverter`.
- `Get-Content -Raw PLANS.md` — passed; this plan follows its required living
  record structure.
- `.venv\\Scripts\\python.exe -m unittest tests.test_miku_runtime_inputs tests.test_miku_semantic_core tests.test_miku_mesh_bound_safety tests.test_miku_hybrid_height_plan` — 45 passed.
- `.venv\\Scripts\\python.exe -m unittest tests.test_miku_eevee_capabilities tests.test_miku_package_identity` — 23 passed.
- `.venv\\Scripts\\python.exe tools\\ci\\run_checks.py --profile pr` —
  boundary, syntax, schema and identity checks passed; 198 tests passed and two
  Delta-E modules could not import because the project virtual environment has
  neither NumPy nor Pillow. Network installation was attempted but the
  configured local proxy refused the connection.
- Bundled Codex Python `-m unittest tests.test_delta_e_combined
  tests.test_delta_e_tool` — 19/19 passed with its NumPy/Pillow runtime, closing
  the two dependency-gated modules from the PR run.
- `C:\\SteamLibrary\\steamapps\\common\\Blender\\blender.exe` version assertion — passed with Blender 5.2.0 LTS.
- Blender headless `miku_static_pbr_textures_smoke.py`,
  `miku_height_bake_smoke.py`, and `miku_expression_island_bake_smoke.py` —
  passed, including a real half-float EXR Height bake, cache reuse, byte
  stability, policy behavior, deepest-leaf diagnostics, and Source Mesh island
  baking.
- Blender headless `miku_colorful_coating_corpus_smoke.py` — blocked: the exact
  locked corpus file is absent. The available backup fixture has SHA-256
  `bb09...`, not the required `b02d...`; the test correctly refused to treat it
  as equivalent evidence.
- Unity 6000.4.5f1 official EditMode run — 120 passed, 5 failed, 1 skipped. One
  new SourceMesh fixture issue was corrected and passed on rerun; the remaining
  four failures are pre-existing Toon graphics tests executed with
  `-nographics`/NullGfxDevice.
- Targeted Unity `MikuBundleImporterTests` — 110/110 passed, including current
  Miku glTFast SourceMesh creation, stable GUIDs, Height LOD/default lowering,
  compatibility-resource filtering, Shader Graph import and determinism.
- Two consecutive `tools/build_miku_unity_package.py` runs produced identical
  SHA-256 `BEA2C494DFACA2053241A93D804D151732A7A2799F56375F0DA40DF50A518224`.
- Two consecutive `tools/build_miku_blender_extensions.py` runs produced
  identical SHA-256
  `E2DDF678A2BBF2B5024232ECF1AF379896C360FBA67D30D89A7B0AE2ADF8E5C8`.
- Targeted `ruff check` and `git diff --check` over every task-owned source,
  test, and documentation file — passed.

## Results and follow-up

Implementation is complete. MaterialIR, bake request, and bundle remain `1.0`;
the only public addition is the Blender material enum
`miku_displacement_policy`, defaulting to `FOLLOW_BLENDER`. Existing Unity
property names are unchanged. The bake algorithm revision is 13 so earlier
caches cannot be reused as Height-capable results.

Known limitations are intentional: vertex displacement does not subdivide a
mesh; distinct active Height endpoints are not combined; runtime-dependent
Height cannot be UV-baked; and legacy MiGR bundles do not gain automatic
SourceMesh Prefabs. The exact `凹凸石3`, `蓝玉`, and `玉石` source materials were
not present as `.blend` files, so their supplied old export directories could
be diagnosed but not re-exported in place. The same repaired code paths are
covered by deterministic Blender and Unity fixtures.
