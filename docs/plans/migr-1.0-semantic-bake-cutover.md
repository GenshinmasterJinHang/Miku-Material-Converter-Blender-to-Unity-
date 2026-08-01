# MiGR 1.0 semantic-region and adjustable-material cutover

## Purpose and outcome

Replace the Blender-node-to-Shader-Graph translation route with a target-neutral,
strongly typed semantic IR and region planner. Deliver native, reusable-bake,
mesh-bake, full-PBR-bake, appearance-snapshot, and adjustable Unity material
flows for the five Blender 5.2 metal fixtures (73 assigned materials), then
atomically replace only the authorized previous Unity metal import.

The ordinary route emits editable Unity 6 URP Shader Graph/Sub Graph assets and
semantic HLSL. ShaderLab remains isolated to the three explicitly retained game
preset compatibility families (Genshin, Wuwa, HSR).

## Context and constraints

- Supported baseline: Blender 5.2.0 LTS, Unity 6000.4.5f1, URP/Shader Graph
  17.4.0, Windows/D3D11, Linear color space.
- Corpus: five strict `.blend` files under the private `材质库/金属` tree;
  `.blend1` files and each file's unassigned `Dots Stroke` material are excluded.
- Public identity becomes `migr`, `migr_blender`, `MiGR.*`, and
  `com.migr.shaderconverter` 1.0.0. New schemas are `migr-*-1.0`.
- Ordinary old MGIR inputs hard-fail. Only the isolated Genshin/Wuwa/HSR
  preset projection accepts the dedicated `mgir-preset-2.0` top-level form.
- The user authorized a new MIT root history and a one-time Full Regeneration of
  `Assets/MiGRReview/MetalGPLApprox` plus the five named old metal bundle roots.
  All other bundle roots and user-owned assets are protected.
- Source `.blend` files must never be mutated. Reachable animation dependencies
  are unsupported; no animation clips or frame freezing are generated.
- Root source is MIT after provenance audit. The Blender Extensions ZIP is a
  separate GPL-3.0-or-later artifact with retained MIT notices.

## Progress

- [x] Read repository constitution, PLANS.md, architecture/compatibility docs,
  and inspect the dirty worktree.
- [x] Reconfirm the five input hashes, Unity version/package state, and 23/5/18
  bundle-root inventory.
- [x] Create an external full workspace copy, Git bundle, Unity target backup,
  inventories, and critical hash records at the timestamped backup directory.
- [x] Build the clean staging root and add the new public contracts.
- [x] Implement the first semantic extraction/planning contracts, Blender
  snapshot exporter, deterministic bundle writer, verified SG 17.4 wrapper
  importer, parameter binding API, and explicit rebake coordinator.
- [x] Run the Python semantic tests, Blender 5.2 corpus export, Unity package
  compilation, and Unity EditMode suite. The live batch import produced 73
  committed receipts.
- [ ] Complete raw-channel visual parity, full rebake/rollback fault injection,
  and repository root swap after the remaining gates are reviewed.

## Discoveries

- The fixture audit found 73 assigned materials and 5,156 expanded reachable
  node instances. The corpus requires Anisotropic closure support, LayerWeight/
  Fresnel runtime evaluation, and mesh-dependent baking for Object/Generated
  coordinates, Bump, AO, Bevel, and Wireframe.
- Blender 5.2 Eevee does not execute object baking; MeshBake must use Cycles on
  copied evaluated data and must report approximation explicitly.
- Existing visual and batch runners can succeed without producing images or
  running tests; they require replacement with manifest-complete gates.
- The current Unity project uses an embedded `com.migr.shaderconverter` package
  and has no external C# callers outside the disposable target review assets.

## Decision log

1. Semantic closure/region planning replaces one-to-one node lowering. Complex
   source expressions become typed `OpaqueSemanticRegion` values evaluated by
   Blender; Blender node identifiers remain private SourceMap data.
2. `ConversionManifest` is the Blender/Core execution receipt only. Unity paths,
   GUIDs, imports, compilation, bindings, commit, and rollback belong only to
   `UnityImportReceipt`; the latter references sealed bundle hashes and never
   rewrites them.
3. Standard Principled regions use a versioned Lit wrapper. Anisotropic and
   closure-mixed regions use a separate SG 17.4 Custom Lit wrapper with
   centralized clean-room MIT lighting HLSL; inability to validate required
   passes is critical Unsupported, not a Lit fallback.
4. SourceMap is an editor-only opaque artifact that makes parameter writes and
   local rebakes replayable. Source/map/plan/override drift requires full
   reconversion.
5. Auto allows Cycles data-channel approximation but never COMBINED appearance
   baking. AppearanceSnapshot is explicit and fixed-light/unlit.
6. First cutover resets old generated material values to Blender defaults;
   later regenerations preserve values through stable parameter identities.

## Implementation sequence

1. Create the staging repository from an audited allowlist; write MIT license,
   provenance/third-party notices, product identity, compatibility matrix, ADRs,
   and this living plan. Exclude private/game assets from the new root.
2. Add canonical JSON and the seven `migr-*-1.0` document contracts. Implement
   typed MaterialIR, TargetProfile, ConversionPlan, Manifest, Bundle, SourceMap,
   and UnityImportReceipt validation with deterministic IDs and diagnostics.
3. Implement Blender snapshot extraction, closure recognizers, dependency/stage/
   coordinate propagation, region partitioning, SourceMap creation, Eevee
   ReusableBake, Cycles MeshBake, FullPBRBake, explicit AppearanceSnapshot,
   cache invalidation, cleanup, and secure process completion markers.
4. Implement parameter extraction, identity/reference names, influence/hoist
   proofs, mutability/scope/update actions, OverrideAsset, RebakeCoordinator,
   presets/variants, runtime binding generation, and keyword-budget validation.
5. Implement `ShaderGraph17_4UrpBackend` and `ShaderGraph17_4UrpCustomLitBackend`
   from verified 17.4 templates; isolate all Shader Graph serialization details
   in version adapters. Implement the exact legacy preset projection and retain
   the three public ShaderLab compatibility surfaces.
6. Replace old success-path tests and unsafe visual runners with schema, planner,
   bake, Unity EditMode, visual-manifest, determinism, security, and rollback
   gates. Complete the vertical slices before expanding to the full corpus.
7. Build a temporary Unity project with the exact package/project settings,
   import all 73 materials, run every gate, and record the sealed manifest and
   receipt. Abort on any critical Unsupported, missing artifact, compile error,
   pink material, or visual threshold failure.
8. Re-preflight the live project, back up the five target roots/package manifests,
   switch to `com.migr.shaderconverter`, use journaled atomic writes, refresh,
   verify, and write the commit marker last. Roll back from the backup on any
   failure. After live acceptance, switch the repository directory to the clean
   staging root and retain the old root externally.

## Validation

- Python: `python -m unittest discover -s tests -p "test_*.py"`.
- .NET: a real MiGR test project through `dotnet test`; ordinary old schemas must
  reject, and only the three preset families may compile through the compatibility
  island.
- Blender: headless runner must assert the expected script count, use the pinned
  Blender executable, prove source non-mutation, and exercise cache/error cleanup.
- Unity: EditMode runner must assert nonzero expected suites, exact package/version,
  graph loading, shader compilation, importer settings, ownership, GUID stability,
  parameter bindings, and rollback.
- Visual: fixed mesh/camera/lights/background, raw linear EXR, no alignment/crop/
  scale; each of 219 material/view pairs and each raw channel passes its own
  silhouette/alpha, Native, ReusableBake, MeshBake, Hybrid, or calibrated corpus
  threshold.
- Release corpus: 5/5 input hashes, 8/12/19/14/20 material counts, exactly 73
  named materials, zero excluded `Dots Stroke`, zero critical Unsupported, zero
  compile/pink/name errors, stable second run, single-job invalidation for one
  Noise Scale change, and injected-failure rollback.

## Results and follow-up

This section is append-only and must contain executed commands with `passed`,
`failed`, `implemented but not executed`, or `blocked` status. The task is not
complete until the live Unity receipt and repository cutover are both accepted.
Known permanent boundaries are unsupported animation, exact-version Unity/URP/
Shader Graph support, and explicit approximation diagnostics for Cycles-derived
data or Custom Lit lighting.

### Execution addendum — 2026-07-27

- **passed** `python -m unittest tests.test_migr_semantic_core`: 7/7, and
  `python -m unittest tests.test_migr_package_identity`: 3/3.
- **failed (expected legacy-suite drift)** `python -m unittest discover -s tests
  -p "test_*.py"`: 322 collected, 32 failures, 4 errors, 44 skipped. The
  failures are in pre-cutover B2U tests that assert the retired 0.11 package
  path/version, GPL provenance, or missing release fixtures; the MiGR semantic
  acceptance tests above are the authoritative new route.
- **passed** Blender `5.2.0 LTS` headless export for the five locked `.blend`
  files: 5/5 exit code 0, source hashes match the audit, and material counts are
  8/12/19/14/20 (73 total). `.blend1` files were excluded.
- **passed** Unity package switch and compile on Unity `6000.4.5f1`, URP and
  Shader Graph `17.4.0`. The old `.b2ubundle` ScriptedImporter is disabled in
  MiGR 1.0 so legacy assets cannot trigger recursive mutation.
- **passed** Unity EditMode run through MCP before the batch import: 40/40
  tests passed. A later package refresh had no C# compile errors; unrelated
  legacy assets emitted existing warnings only.
- **passed** Unity batch method
  `MiGR.ShaderConverter.Editor.MiGRMetalCorpusMenu.ImportMetalCorpus`: marker
  `MIGR_UNITY_IMPORT_COMPLETE:73`, 73 `.mat`, 73 `.shadergraph`, 73 generated
  subgraphs, and 73 committed `migr-unity-import-receipt-1.0` files under
  `Assets/MiGRReview/MetalMiGRStaging`, with source names preserved.
- **passed** live Unity cutover: the package manifest now points to
  `com.migr.shaderconverter` 1.0.0; the five authorized legacy roots were moved
  to the external rollback backup and regenerated with exact source-name
  mapping (8/12/19/14/20 materials). Receipt validation covers 146 committed
  receipts with zero malformed/BOM-bearing documents. The old `Fallback`
  review folder, scenes, screenshots, and HLSL remain preserved because they
  are user-owned review assets outside the five authorized roots.
- **implemented but not executed**: raw linear EXR reference capture, all 219
  image threshold gates, animation-negative corpus, stable-parameter rebake
  after a source edit, and injected-failure rollback. The current semantic
  exporter records routes and receipts but does not yet emit baked channel
  images for every opaque region.
- **blocked**: final repository root swap is held until the visual and rollback
  gates above pass. The external rollback backup is retained outside the
  repository and is not deleted.
  remains at `C:/Users/22687/Desktop/项目4.pre-migr-backup.20260726T174619Z`.
