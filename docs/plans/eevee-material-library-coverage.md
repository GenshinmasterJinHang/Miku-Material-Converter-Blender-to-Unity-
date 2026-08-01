# EEVEE material-library compatibility and repository cleanup

## Purpose and outcome

Miku 1.0.1 must export every bound, non-Cycles-only material in the supplied
Blender 5.2 EEVEE corpus without silently replacing unsupported semantics.
Native Shader Graph expressions remain preferred. Static spatial or complex
closure branches use an explicit Source Mesh Fidelity PBR bake. The repository
finishes with deterministic Blender and Unity packages plus reproducible corpus
and Unity evidence. Existing user changes remain intact; this task does not
create commits unless the user separately requests them.

## Context and constraints

The canonical implementation roots are `miku/`, `miku_blender/`,
`extensions/miku_shader_converter/`, and
`unity/Packages/com.miku.shaderconverter/`. Retired B2U sources, installed
extensions, `dist/` archives, and Unity validation-project package copies are
outputs and are not implementation roots. Validation uses Blender 5.2.0 from
the repository-mandated Windows installation and Unity 6000.4.5f1 with URP and
Shader Graph 17.4.0. Private corpus paths and generated corpus artifacts never
enter Git.

The hard corpus scope is 302 object-bound materials in 24 `.blend` files.
Twenty-four unbound materials named `Dots Stroke` are read-only audit scope.
Cycles-only status is decided from the active EEVEE output chain (or active ALL
fallback), with node/socket evidence.

## Progress

- [x] 2026-07-31: Preserved the pre-existing dirty worktree, verified the
  canonical Miku source boundary, and ran the read-only baseline checks.
- [x] 2026-07-31: Added active-chain EEVEE capability classification, Blender
  node identity/socket metadata, Blender 5.2 legacy Glossy handling, native
  expression coverage, and Source Mesh Fidelity diagnostics.
- [x] 2026-07-31: Added the parameterized corpus audit tool. Read-only audit
  found 24 files, 302 bound materials, 24 unbound `Dots Stroke` materials, and
  no scan failures.
- [x] 2026-07-31: Added compiler-level refinement and explicit Full PBR
  Source Mesh Fidelity planning. The final audit classifies 34 exclusions and
  268 supported materials with zero planning failures: 38 `Auto`, 228
  `AllowMeshBake`, and 2 `FullPBRBake` exports.
- [x] 2026-07-31: Added native EEVEE Camera Ray/Shadow Ray expressions and URP
  pass-aware HLSL; unsupported Light Path outputs remain Cycles-only.
- [x] 2026-07-31: Completed both independent 268-material exports. Each has
  2,866 files, 268 successes, zero hard failures, and no abandoned staging
  directories. Relative paths and every file SHA-256 are identical; both
  aggregate to tree hash
  `3b76480559cc8a1e6a126061766976ce82253d801cd0156fe28ec50b2dd949c5`.
- [x] 2026-07-31: Built and installed the canonical Blender 1.0.1 package;
  its 28 installed files exactly match the ZIP manifest and hashes. Two final
  builds produced identical ZIP and TGZ bytes.
- [x] 2026-07-31: Passed the final Python, PR, Blender 5.2 headless, and Unity
  6000.4.5f1 EditMode suites.
- [x] 2026-07-31: Imported the eight fixed coverage samples through Unity MCP,
  regenerated all receipts with the final package, verified every Shader,
  created the additive coverage scene, and captured a representative image.
- [x] 2026-07-31: Self-reviewed the final diff and verified that no private
  corpus paths or generated corpus assets entered the repository.
- [x] 2026-08-01: Fixed Standard PBR snapshot merging so legacy provenance
  strings no longer replace compiler-visible `{node, socket}` source mappings;
  Blender 5.2 smoke now exports separate BaseColor, Metalness, Roughness,
  Normal, and Emission resources.
- [x] 2026-08-01: Added native Geometry `Backfacing` lowering for closure
  weights and Generic Toon fixtures; the path now emits
  `1 - Input.IsFrontFace` without a bake or nested unsupported diagnostic.

## Discoveries

- The corpus contains exactly 326 node materials: 302 bound and 24 unbound.
  All unbound materials are named `Dots Stroke`.
- Structural classification alone produced false native positives. A
  compiler/planner refinement is required because static mixed closures can
  be EEVEE-valid while exceeding the editable URP surface backend.
- The first read-only structural distribution was 26 Cycles-only, 112 native,
  and 188 source-mesh. Active-chain and label policy refinement produced the
  acceptance baseline of 34 exclusions and 268 supported materials. The
  actual safe-mode distribution is 38 `Auto`, 228 `AllowMeshBake`, and 2
  `FullPBRBake`; the earlier 38/203/27 split was only a planning estimate.
- Cycles-only evidence in this corpus is limited to active Bevel, unsupported
  Light Path outputs, Toon BSDF, and standalone Sheen BSDF.
- The two malformed legacy closure graphs that previously raised
  `MIKU_CLOSURE_INPUT_MISSING` can safely use a descriptive fallback closure
  only for Full PBR planning; the GPL worker still evaluates the untouched
  original Blender tree.
- Real Unity imports exposed three integration boundaries that unit planning
  did not: emission closure parameters must participate in hybrid islands,
  valid deeply nested IR exceeds Newtonsoft.Json's default depth of 64, and
  an authoritative Source Mesh PBR projection supersedes otherwise redundant
  baked expression-island texture bindings. Each condition now has a bounded
  implementation and regression coverage.
- The final Full PBR sample also contained a baked IOR channel. URP's Metallic
  workflow has no per-pixel IOR input, so the importer retains the sealed
  resource for provenance, omits the nonexistent `_MIKU_IOR` binding, and
  writes an explicit fixed-F0 approximation diagnostic into the receipt.
- A Standard PBR regression was found at the snapshot/IR boundary: the legacy
  semantic extractor's string provenance (`socket`) replaced the closure
  endpoint mapping, so `build_material_ir` emitted defaults and Unity received
  no editable texture bindings. The merge now preserves the endpoint and
  carries provenance separately.
- Geometry `Backfacing` was already classified as runtime-dependent and Unity
  already had a native `IsFrontFaceNode` lowering, but the semantic compiler
  only implemented Geometry `Incoming`. This left closure weight binding with
  an outer `MIKU_CLOSURE_WEIGHT_EXPRESSION_UNSUPPORTED` diagnostic wrapping
  `MIKU_RUNTIME_INPUT_UNSUPPORTED:Input.Geometry:Backfacing`.

## Decision log

- 2026-07-31: Use `FullPBRBake` as the internal explicit Source Mesh Fidelity
  route for whole-material static projection. `Auto` continues to fail with
  `MIKU_SOURCE_MESH_FIDELITY_REQUIRED` and never upgrades itself.
- 2026-07-31: Treat only non-camera/non-shadow Light Path outputs as
  Cycles-only. Camera and Shadow Ray receive a pass-aware URP implementation.
- 2026-07-31: General non-Principled closure compositions are conservatively
  source-mesh projected when the realtime surface resolver cannot prove a
  safe native backend. No constants or pass-through values are substituted.
- 2026-07-31: Keep JSON schema 1.0, package IDs, conversion mode identifiers,
  and public Shader property names unchanged for Miku 1.0.1.
- 2026-08-01: Preserve Blender Backfacing semantics as a target-neutral
  `Input.IsFrontFace` plus `Math.OneMinus` expression. Reuse the existing
  Unity lowering; do not add a backend-specific Blender operation or change
  the schema/API surface.

## Implementation sequence

1. Preserve the pre-existing identity migration and dirty worktree.
2. Extend Blender snapshots and active-chain capability evidence.
3. Split non-bakeable runtime dependencies from static spatial dependencies.
4. Extend semantic and Shader Graph expression lowering.
5. Add explicit deterministic Full PBR Source Mesh Fidelity planning and
   Blender-worker execution.
6. Add corpus audit/export reporting and deterministic sample selection.
7. Update version, documentation, diagnostics, provenance, and tests.
8. Validate Blender, Python, Unity, packages, and corpus determinism.
9. Self-review the EEVEE work and verify repository path/data hygiene.

## Validation

Commands and results are appended only after execution:

- `py -3.13 -m unittest discover -s tests -p "test_*.py"`: passed 201 tests.
- `py -3.13 tools/ci/run_checks.py --profile pr`: passed after regenerating
  the deterministic Unity package identity manifest.
- Fixed Blender 5.2 audit with expected counts and `--require-complete`:
  planning passed with zero hard failures.
- Seven directly relevant Blender 5.2 headless smokes passed: current material
  frontend, closure surface, expression-island bake, runtime inputs, static
  PBR textures, installed extension identity, and glass surface.
- `tools/ci/run_unity_editmode.ps1` with Unity 6000.4.5f1: passed 113 of 114
  tests with zero failures and one intentional skip.
- First complete corpus export: 302 bound, 24 unbound, 34 excluded, 268
  supported, 268 exported, zero hard failures. The second export has the same
  counts and byte-identical 2,866-file output.
- Deterministic package hashes: Blender ZIP
  `89ad940b4151e3bd8140ceb043d6893c6a532f6ee08d06fa3638b5822ef64df2`;
  Unity TGZ
  `4b5e0270dc900566cfdbaef386c59dc4bbedda78ab96d2d3e513788ff4c9c586`.
- Unity MCP instance `test@be08cbdd5b1db5e5`: eight committed imports,
  eight receipts with diagnostics, eight wrappers, eight Sub Graphs, sixteen
  materials, 21 imported textures, four glTF Source Mesh instances, zero
  Shader errors, and zero final Console errors or warnings. The saved scene is
  `Assets/miku/MaterialLibraryCoverage/MaterialLibraryCoverage.unity`; Windows
  and Unity preserve the project's pre-existing lowercase `Assets/miku`
  spelling for the requested `Assets/Miku` directory.
- The private source snapshot retained all 51 files and the same per-file
  hashes. Its latest source write predates validation; no source asset was
  modified.

### Standard PBR texture-binding revalidation addendum — 2026-08-01

- Python 3.13 suite passed 207 tests; the Standard PBR wrapper check passed.
- Fixed Blender 5.2 static PBR smoke passed, including separate BaseColor,
  Metalness, Roughness, Normal, and Emission resources with source mappings
  and positive confidence.
- Unity 6000.4.5f1 EditMode passed 115 tests: 114 passed and one intentional
  skip, including the five non-null Standard PBR Inspector texture slots.
- The installed 1.0.1 ZIP was byte-deterministic and matched the installed
  `user_default` extension tree. An installed-module export produced all five
  semantic texture resources and binding keys.
- A synthetic Blender 5.2 bake smoke passed all five channel bakes and proved
  material assignment, node count, active object, and selection restoration.
- The corpus-specific expression-island bake smoke remains blocked because
  its private input `材质库\石头\彩色镀层\彩色镀层.blend` is absent from this workspace.

### Geometry Backfacing runtime-lowering addendum — 2026-08-01

- `py -3.13 -m unittest discover -s tests -p "test_*.py"`: passed 209 tests.
- `C:\SteamLibrary\steamapps\common\Blender\blender.exe --background
  --factory-startup --python tests/blender/miku_runtime_inputs_smoke.py`:
  passed on Blender 5.2.0, including a Generic Toon Mix Shader Backfacing
  closure-weight fixture.
- `py -3.13 tools/ci/run_checks.py --profile pr`: passed, including package
  identity and deterministic package builds.
- `tools/ci/run_unity_editmode.ps1` with Unity 6000.4.5f1: passed 115 tests,
  114 passed, one intentional skip, zero failures.
- Ruff was not executed because no `ruff` command or Python module is
  installed in the available environments; Python 3.13 tests and the PR
  profile completed successfully.

## Results and follow-up

Implementation and requested validation are complete. Known release boundaries
remain: active Cycles-only chains are excluded with evidence; unbound
`Dots Stroke` materials are audit-only; Source Mesh Fidelity is tied to the
source object topology and UVs; per-pixel IOR is explicitly approximated by the
URP Metallic fixed F0; and Unity validation is a deterministic feature-covering
sample rather than a claim of per-pixel validation for all materials.
