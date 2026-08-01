# MiGR 2.0.2 Magic Ball Bump and Clear Coat Regression

## Purpose and outcome

Restore successful Blender 5.2 export and Unity 6 URP Shader Graph import for
Magic Ball materials 1 through 5 without silently dropping their Bump normals
or Principled Coat. Bump normals are preserved through the existing semantic
mesh-bake protocol. The supported Principled Coat subset is lowered to URP
17.4 Clear Coat with an explicit approximation diagnostic.

## Context and constraints

- Canonical source roots are `migr/`, `migr_blender/`,
  `extensions/migr_semantic_exporter/`,
  `extensions/migr_gpl_bake_worker/`, and
  `unity/Packages/com.migr.shaderconverter/`.
- The repository already has a large dirty worktree. This work must not revert,
  format, delete, or otherwise absorb unrelated changes.
- Material IR, Conversion Plan, and Bundle remain schema 2.0. No new required
  interchange field or SurfaceModel kind is introduced.
- Generated subgraphs and MiGR sidecars are MiGR-owned. Existing wrapper graphs
  are user-owned unless Full Regeneration is selected.
- The fixed Blender runtime is Blender 5.2.0. Unity validation targets Editor
  6000.4.5f1 with URP and Shader Graph 17.4.0.
- A Blender GUI process currently has an unsaved Magic Ball file. Installed
  extensions must not be overwritten until that process is saved and closed.
- A Unity GUI process currently has an unsaved scene. Validation should use an
  isolated project or wait for the user to save and close that editor.

## Progress

- [x] 2026-07-29: Confirmed canonical repository markers and package identity.
- [x] 2026-07-29: Reproduced the Blender 5.2 export failure for Magic Ball 1.
- [x] 2026-07-29: Confirmed installed semantic exporter source hashes match the
  canonical 2.0.0 source.
- [x] 2026-07-29: Inspected Magic Ball 1-5 Bump and Coat topology.
- [x] 2026-07-29: Implemented Bump snapshot metadata and bakeable dependency
  classification.
- [x] 2026-07-29: Treated a single scattering term's Normal as the global
  surface Normal while retaining distinct per-lobe Normal rejection.
- [x] 2026-07-29: Added the supported Principled Coat subset and diagnostics.
- [x] 2026-07-29: Added the Unity 17.4 Clear Coat template/backend path and
  wrapper ownership checks.
- [x] 2026-07-29: Added regression tests and public documentation.
- [x] 2026-07-29: Built deterministic semantic-exporter 2.0.2, GPL worker
  1.1.1, and Unity-package 2.0.2 archives twice with matching SHA-256 values.
- [x] 2026-07-29: Ran Python, Blender 5.2, Unity EditMode, and isolated
  end-to-end export/import validation.

## Discoveries

- `ShaderNodeBump` is snapshotted as `Vector.Bump`, but the semantic expression
  compiler has no `Vector.Bump` lowering. It falls through to
  `MIGR_RUNTIME_INPUT_UNSUPPORTED:Vector.Bump:Normal`.
- Closure parameter binding wraps that failure as
  `MIGR_CLOSURE_PARAMETER_EXPRESSION_UNSUPPORTED`; repeated traversal produces
  duplicate diagnostics.
- `Vector.Bump` is classified as mesh-dependent, but mesh-dependence currently
  follows the runtime-expression path instead of the static channel-bake path.
- Magic Ball 3 contains two chained Bump nodes. Baking the final Normal channel
  in Blender preserves this topology without inventing a Shader Graph
  approximation.
- The current surface backend rejects every non-default closure Normal as
  per-lobe, including the single Principled term used by Magic Ball 1-5.
- Magic Ball 1-5 use Coat Weight 0.25, Coat Roughness 0.03, Coat IOR 1.5,
  white Coat Tint, and a default Coat Normal.
- Unity URP 17.4 exposes Universal Lit Clear Coat through Coat Mask and Coat
  Smoothness Master Stack blocks and serializes the target with
  `m_ClearCoat=true`.
- The runtime Sub Graph output node must be the source of truth when the wrapper
  is rebound. A Clear Coat wrapper has the correct Master Stack blocks, but an
  older serialized SubGraphNode reference does not expose the new output slots
  until they are synchronized from the loaded Sub Graph.

## Decision log

- 2026-07-29: Preserve Bump through one channel-scoped Normal MeshBake rather
  than a native Normal From Height approximation. This covers groups, linked
  Strength, and chained Bump nodes using Blender's own evaluation.
- 2026-07-29: Keep truly runtime-dependent Bump chains unsupported. Static
  baking must not freeze Time, View, or other runtime semantics.
- 2026-07-29: Allow a linked Normal only when one supported surface-scattering
  term owns it. Multiple distinct per-lobe normals remain unsupported.
- 2026-07-29: Support Coat Weight and Coat Roughness only when Coat IOR is 1.5,
  Coat Tint is white, and Coat Normal is default. Broader coat semantics are
  outside this release.
- 2026-07-29: Map Coat Weight to Coat Mask and Coat Roughness to
  `1 - roughness`. Declare `MIGR_COAT_URP_APPROXIMATION`; Strict rejects it.
- 2026-07-29: Reuse weighted closures and approximation records instead of
  changing the 2.0 interchange schema.
- 2026-07-29: Coordinate semantic exporter and Unity package version 2.0.2;
  keep the already capable GPL worker at 1.1.1.
- 2026-07-29: Authored the Clear Coat wrapper by deserializing and modifying the
  existing standard wrapper through Unity 6000.4.5f1/Shader Graph 17.4.0 APIs.
  The canonical template SHA-256 is
  `b64ec59e48a3f964a389ff707f6a3592014163e71a349d2deca9cd96a12d4afc`;
  no Shader Graph internal field was invented by hand.
- 2026-07-29: Synchronize SubGraphNode output slots from the imported Sub Graph
  using deterministic slot identities before connecting the Clear Coat blocks.
  This preserves stable graph output while making the Unity-authored wrapper
  consume Coat Mask and Coat Smoothness.

## Implementation sequence

1. Extend Blender snapshot metadata and semantic dependency analysis for Bump.
2. Route bakeable Bump-backed Normal channels into one existing MeshBake job;
   retain NativeOnly and true-runtime failure behavior.
3. Correct single-term Normal ownership and add Coat compatibility,
   approximation, and Strict-policy handling.
4. Author a Unity 17.4 Clear Coat wrapper fixture from a real Unity asset, then
   add deterministic template selection, Coat subgraph outputs, and wrapper
   compatibility enforcement.
5. Add focused Python, Blender, and Unity tests before updating version
   identities, implementation hashes, diagnostics, compatibility docs, and
   changelog.
6. Build each release archive twice, compare manifests and SHA-256 hashes, then
   validate installation only after running editors are safe to update.

## Validation

- Python:
  `python -m unittest discover -s tests -p "test_migr_*.py"`
- Blender:
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe --background
  --factory-startup --python tests/blender/migr_magic_ball_corpus_smoke.py`
- Unity:
  run the package EditMode suite in Unity 6000.4.5f1 with URP/Shader Graph
  17.4.0 and verify no compile/import errors.
- Packaging:
  build the exporter and Unity package twice from canonical source, compare
  archive manifests and SHA-256 values, and verify installed package identities.
- End to end:
  export Magic Ball 1-5, complete Normal bake jobs, import into the D3D11
  validation project, and compare Bump detail and coat highlights under the same
  mesh, camera, and lighting.

Expected results are no error diagnostics for Magic Ball 1-5 in
AllowDeclaredApproximation mode, one completed Normal bake per material that
uses Bump, an explicit Coat approximation warning, importable non-pink shaders,
and deterministic output.

## Results and follow-up

Implementation and automated validation are complete.

- Python 3.13 unit suite: 142 passed.
- Repository `pr` and `release` CI profiles passed, including canonical-boundary
  validation, parsing 68 Python files, validating 14 schemas, package identity,
  tests, and package builds.
- Blender 5.2.0 Magic Ball corpus: passed. Magic Ball 1-5 completed their
  Normal MeshBake jobs twice with identical manifests, resources, and bytes;
  the Magic Ball 10 Light Path negative case remained enforced.
- Unity 6000.4.5f1 EditMode under D3D11: 81 passed, 0 failed, 0 skipped.
- Actual Magic Ball 1-5 bundles imported in an isolated Unity project without
  export/import errors, shader compilation errors, or pink materials. Generated
  wrappers contain `m_ClearCoat=true`, Coat Mask, and Coat Smoothness.
- The final D3D11 comparison used the exact Magic Ball 1-5 source meshes
  exported read-only from the fixed Blender file, with matched camera and light
  placement. Visual inspection confirmed baked Normal detail and Clear Coat
  highlights were simultaneously visible.
- The semantic exporter, GPL worker, and Unity package archives were each built
  twice with byte-identical SHA-256 values recorded under `docs/release/`.

The Blender and URP renders are not pixel-identical: Clear Coat is a declared
BRDF approximation, and pre-existing procedural emission appearance differences
remain visible on some balls. This release's acceptance scope is satisfied by
the preserved Bump detail, Clear Coat highlights, successful graph compilation,
and absence of export/import errors.

After the user confirmed both editors were saved and closed, the deterministic
archives were installed into the fixed Blender portable repository and the
actual Unity validation project. The installed Blender exporter 2.0.2 and GPL
worker 1.1.1 matched all 32 archive files byte-for-byte. The installed exporter
then exported Magic Ball 1-5 with completed Normal resources and no errors. The
Unity package cache matched all 140 archive files after excluding UPM's expected
`_fingerprint` field, and the actual project passed all 81 package EditMode
tests under D3D11.

Non-default Coat IOR, Coat Tint, Coat Normal, and truly runtime-dependent Bump
graphs intentionally remain unsupported for 2.0.2.

Follow-up (2026-07-29): MiGR 2.0.3 corrects typed Mix input selection,
Principled emission strength, closure-composite Normal reachability, and baked
closure-weight references discovered during the complete Magic Ball 1-10
visual regression. See
`docs/plans/magic-ball-runtime-expression-regressions-2.0.3.md`.
