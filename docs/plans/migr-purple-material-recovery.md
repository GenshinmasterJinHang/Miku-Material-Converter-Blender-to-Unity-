# MiGR purple-material recovery and verified corpus regeneration

## Purpose and outcome

Repair the live `MetalGPLApprox` cutover so every renderer resolves a stable
standalone material, every required semantic channel is backed by a validated
constant or resource, and no completion receipt can be committed before package
identity, import/rollback, and semantic validation all pass.

This plan is a focused continuation of
`docs/plans/migr-1.0-semantic-bake-cutover.md`. It corrects acceptance claims
that were based on generated file counts and receipts rather than durable Unity
references and real semantic resources.

## Context and constraints

- Certified tuple: Blender 5.2.0 LTS build
  `fbe6228777e7d9afefcd61a413844e790ae75db7`, Unity 6000.4.5f1, URP and
  Shader Graph 17.4.0, Linear color space, Windows/D3D11.
- Implementation starts in the clean staging repository. The main workspace is
  dirty and must receive only a reviewed patch after conflict checks.
- The user authorized recoverable Full Regeneration of
  `Assets/MiGRReview/MetalGPLApprox`; unrelated roots and user-owned wrappers
  remain protected.
- MiGR 1.0 has not been publicly released. The incorrect local 1.0 bundle
  contract may be tightened in place and old local bundles rejected.
- GPL-derived Blender bake code remains a separately distributed Blender
  Extension. MIT core and the Unity package communicate with it only through a
  versioned artifact protocol.

## Progress

- [x] Reproduce the live failure and separate it from the unaffected
  `Assets/B2U_Generated` corpus.
- [x] Trace Scene -> Material -> Shader/ShaderGraph references and compare
  source-package metadata with the installed embedded package.
- [x] Audit the Blender bundle writer, Unity bundle importer, corpus menu,
  schemas, receipts, and stale review assets.
- [x] Complete four adversarial plan-review rounds; final verdict `APPROVED`.
- [x] Freeze package and generated-asset identity, including release-archive
  validation and duplicate-GUID rejection.
- [x] Tighten bundle/receipt contracts and implement secure staged reads.
- [x] Connect the GPL bake extension and emit real channel resources.
- [x] Replace the Unity false-success path with verified, crash-recoverable
  generation and stable standalone materials.
- [x] Regenerate and validate 73 materials and five review scenes.
- [ ] Apply the reviewed staging patch to the dirty main workspace.

## Discoveries

- The embedded package changed 95 of 112 source `.meta` GUIDs. Seventy-four
  fallback materials reference SemanticLit GUID
  `10e4314ce3554d94a3d70d965890cbbc`, while the installed package exposes
  `a3a0aeb25bf1ed449a6e525f4f8be53f`; Unity resolves those materials to
  `Hidden/InternalErrorShader`.
- Previous review scenes serialized Shader Graph internal Material subassets.
  Reimport changed their local file IDs, leaving renderer slots null.
- `MiGRBundleImporter` ignores sibling IR/plan/manifest/source-map documents and
  resources, recreates `.mat` assets, overwrites wrappers, skips Shader error
  checks, and writes committed receipts unconditionally.
- The 73 newly generated materials have supported shaders but no texture
  bindings. Their manifests report empty artifacts and a pending target-profile
  hash, so they cannot reproduce source appearance.
- Old fallback assets, scenes, screenshots, and HLSL coexist with a newer
  material import. Filtered corpus runs overwrite the global summary with a
  partial count while still writing a completion marker.
- The reusable channel-bake implementation is GPL-derived and cannot be copied
  into the MIT core or Unity package without an explicit compliant boundary.
- A legacy HLSL folder inside the regeneration root is still referenced by 438
  assets outside that root. Full regeneration now preserves those GUID-bearing
  compatibility dependencies and deletes only generated corpus children.
- Python and Newtonsoft.Json spell integral floating-point values differently
  by default (`1.0` versus `1`). The first live deployment stopped at material
  52, rolled back the complete corpus, and exposed this cross-language hash
  defect. Canonical Unity serialization now matches Python and both sides
  normalize negative zero.
- The first clean tarball installation exposed a missing `Templates.meta`;
  immutable package assets under that folder were ignored even though an
  embedded package worked. Package identity validation now requires a `.meta`
  for every imported directory and asset (excluding Unity's `Samples~`
  convention). Installed `package.json` identity ignores only UPM's injected
  `_fingerprint`.

## Decision log

1. Package `.meta` files are release identity. A checked-in path/GUID manifest
   is validated against source, the packed archive, and an isolated installation.
2. Generated GUIDs use persistent source ID, persistent material ID, and logical
   asset role only. Backend/profile versions never change public asset identity.
3. Channel values are `Constant`, `TextureResource`, or `SemanticExpression`.
   Mesh-dependent bakes carry a mesh-binding fingerprint and are not reusable
   across unrelated meshes.
4. The GPL bake executor writes `migr-bake-result-1.0` plus hashed resources.
   The MIT assembler consumes only that public artifact protocol.
5. Unity deployment is crash-recoverable: validate in an isolated project,
   stage outside `Assets`, back up exact bytes and metadata, update only final
   paths, then refresh and verify.
6. Completion has three independent gates: asset/reference integrity,
   bundle/import/rollback integrity, and semantic/channel/visual validation.
   A partial corpus run cannot write a global completion marker.

## Implementation sequence

1. Add asset-identity tooling/tests and validate package GUIDs before restoring
   or deploying any source metadata.
2. Define strict document/resource references, canonical hashing, safe staged
   reads, persistent IDs, mesh bindings, and bake request/result contracts.
3. Execute the GPL bake implementation behind the artifact boundary and populate
   conversion manifests/bundles with real constants and resources.
4. Rebuild the Unity importer around validated staging, stable assets,
   version-specific texture/Shader Graph generation, diagnostics, and recovery.
5. Aggregate exactly 8/12/19/14/20 stable material IDs, rebuild scenes with
   standalone `.mat` references, and reject stale review assets.
6. Run Python, Blender, package, Unity EditMode, domain-reload/restart, channel,
   and calibrated visual gates before changing compatibility status. The first
   four gates are complete; calibrated visual comparison remains pending.

## Validation

- Python:
  `python -m unittest tests.test_migr_semantic_core tests.test_migr_package_identity tests.test_migr_bundle_security`.
- Package identity: build the archive, install it twice in an isolated Unity
  project, and compare every manifest path/GUID pair.
- Blender: verify 5/5 source hashes, 8/12/19/14/20 materials, non-empty required
  bake artifacts, deterministic output, and no source mutation.
- Unity: run EditMode tests, import all 73 bundles, wait for Shader Graph
  compilation, reload the domain, restart the editor, and resolve every renderer
  slot through `AssetDatabase` as a main `.mat` asset.
- Channel gates: BaseColor/Emission linear MAE <= 0.01 and p99 <= 0.03;
  Metalness/Roughness/Alpha MAE <= 1/255 and p99 <= 2/255; normal mean angular
  error <= 2 degrees and p95 <= 5 degrees; alpha silhouette IoU >= 0.995.

## Results and follow-up

Implemented and exercised in the clean staging repository and the exact live
Unity tuple:

- Blender exported all five locked sources as exactly 8/12/19/14/20 = 73
  bundles. Every bundle has real hashed resources, a current target-profile
  hash, and a stable material ID. Six explicitly authorized
  `AppearanceSnapshot` materials are recorded as `Approximate`.
- Focused Python security/protocol/package tests pass. The broad historical
  suite still contains unrelated failures for the removed
  `com.b2u.shaderconverter` path and missing 0.11 release artifacts.
- Unity EditMode passes 50/50. A deliberate real deployment failure restored
  the complete pre-run asset tree; the corrected second run committed all 73
  materials, 73 Graphs, 73 SubGraphs, 73 receipts, and five scenes.
- The final deterministic `.tgz` installs in a clean Unity project with all 114
  package GUIDs and content identities intact. A second editor open has zero C#
  compile errors, Shader errors, missing-meta warnings, or MiGR Sub Graph
  dependency errors.
- Before and after an editor restart, including an explicit
  `-force-d3d11`/Linear run, all 73 shaders are supported, have no compiler
  errors or `Hidden/InternalErrorShader`, and have at least one bound texture.
  All 73 renderer slots resolve to main `.mat` assets with zero nulls and zero
  Shader Graph subasset references.
- The committed summary retains
  `semanticVisualCertification=pending-three-view-exr-thresholds`. No six
  known-good fixture set or frozen EXR baselines exist in this workspace, so
  numerical MAE/p99/angular/IoU gates were not executed and compatibility
  remains Experimental.
