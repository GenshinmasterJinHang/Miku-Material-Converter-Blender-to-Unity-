# MiGR 1.0 format unification and Gold10 repair

## Purpose and outcome

Make the `migr-*-1.0` semantic family the only active conversion contract,
package Blender integration as supported extensions, automatically import
`.migrbundle` roots in Unity, preserve five workflow backends behind one
pipeline, and repair Gold10 by re-exporting it from Blender.

Success is observable when copying a complete Gold10 bundle directory containing
`Gold10.migrbundle` into the validated Unity project automatically produces an
editable wrapper graph, MiGR-owned generated subgraph and base material,
user-owned material variant, receipt, and visible diagnostics without an
`unsupported_version` failure.

## Context and constraints

- Source of truth: this staging repository. The old `项目4` repository is not
  modified or removed.
- The worktree contains existing user-owned MiGR changes. Preserve and integrate
  them; do not reset, stash, or overwrite them.
- Exact target: Blender 5.2.0 LTS, Unity 6000.4.5f1, URP 17.4.0, Shader Graph
  17.4.0 on Windows.
- Ordinary output is editable Shader Graph. Only the three approved game
  compatibility backends retain static Shader/HLSL assets.
- Schema, property names, Blender operators, generated mappings, and public C#
  APIs are compatibility surfaces.

## Progress

- [x] 2026-07-27: Maintainer locked the unified-format implementation plan.
- [x] 2026-07-27: Maintainer confirmed Genshin/Wuwa/HSR backend code is original
  MIT work and selected `MiGR Project Authors` as copyright holder.
- [x] 2026-07-27: The read-only review returned no verdict and was explicitly
  waived by the maintainer; it is not recorded as approved.
- [x] 2026-07-27: External review timed out; maintainer explicitly waived the
  verdict gate and authorized direct implementation.
- [x] 2026-07-27: Audited and integrated the existing dirty worktree.
- [x] 2026-07-27: Implemented the unified schemas and Blender extensions.
- [x] 2026-07-27: Implemented automatic Unity import, durable scheduling,
  transactional ownership, legacy rejection, and one five-workflow registry.
- [x] 2026-07-27: Retired legacy release inputs and updated canonical
  documentation, licensing, and provenance records.
- [x] 2026-07-27: Ran Python, Blender, Unity, security, determinism, license,
  live Shader Graph compile, and live URP render gates.
- [x] 2026-07-27: Backed up and deployed the package/extensions, re-exported
  Gold10, and validated the generated assets.
- [x] 2026-07-27: Self-reviewed the active release diff and recorded results.

## Discoveries

- The current Blender exporter writes `.migrbundle.json`, which Unity imports as
  a generic JSON/TextAsset rather than invoking a MiGR importer.
- The live MiGR package has an explicit static bundle import path but no
  `.migrbundle` ScriptedImporter.
- Existing legacy B2U/MGIR code and tests remain in the staging tree and package;
  retirement must be handled as an audited release-boundary change.
- A valid semantic Gold10 bundle already exists in recovery output, showing that
  the semantic artifacts are healthy; the live Unity folder contains the old
  failed `.mgir` layout.
- A prior external review timed out without a verdict. It must not be described
  as approved.
- The file-based retry also reached the mandatory 10-minute ceiling after
  producing a large read-only exploration trace, but no verdict. The lingering
  reviewer was terminated and no business-code implementation began.
- Shader Graph 17.4 derives a Sub Graph HLSL function identifier from its file
  name. `金10.generated.shadersubgraph` imported but produced invalid HLSL.
  Generated Sub Graphs now use a deterministic ASCII file name derived from
  their stable GUID; user-facing graphs and materials keep the Blender name.
- `Shader.isSupported` and `ShaderUtil.ShaderHasError` missed errors returned by
  `ShaderUtil.GetShaderMessages`. Import validation now treats every
  `Error`-severity compiler message as fatal.

## Decision log

1. Correct unpublished MiGR 1.0 in place instead of creating 2.0.
2. Use `documentKind` plus string `schemaVersion` as the only version authority.
3. Use `.migrbundle` plus sibling files, with no `.json` suffix.
4. Re-export legacy assets; do not implement an in-Unity migration path.
5. Resolve workflow explicitly in Blender and serialize a concrete
   `workflow.kind`.
6. Share one importer, transaction coordinator, and backend registry across all
   five workflows.
7. Keep game rendering implementations in the MIT package after the maintainer's
   authorship and license confirmation; add per-file SPDX and provenance.
8. Use a MiGR-owned base plus a user-owned Unity Material Variant, and preserve
   user wrapper graphs after initial creation.
9. 2026-07-27: The maintainer waived the unavailable external-review verdict.
   Implementation proceeds with local diff review and proof tests as mandatory
   replacement gates.
10. Keep editable Shader Graph property references canonical (`_BaseMap`,
    `_BumpMap`, `_RoughnessMap`, and peers); per-material rewriting broke the
    wrapper/Sub Graph generated-HLSL contract.
11. Use `migr_<stable-guid-prefix>.generated.shadersubgraph` for generated Sub
    Graph file names so generated HLSL identifiers are ASCII-safe.

## Implementation sequence

1. Review the locked plan read-only and log every critique and response.
2. Inventory the current diff and active schemas, exporters, Unity importer,
   backend dependencies, tests, build tools, and deployment state.
3. Normalize the nine MiGR contracts and add workflow validation; change bundle
   naming and deterministic atomic export.
4. Build the MIT exporter extension and GPL worker extension with an artifact
   protocol boundary and clean-profile installation tests.
5. Add the `.migrbundle` ScriptedImporter, diagnostic asset, durable queue,
   transaction coordinator, receipt idempotency, and legacy `.mgir` rejector.
6. Implement one workflow registry and adapt Standard PBR, Generic Toon,
   Genshin, Wuwa, and HSR without preserving old B2U public APIs.
7. Enforce generated/user asset ownership and deterministic identities.
8. Remove legacy release inputs, add SPDX/provenance/non-affiliation records,
   and update canonical English plus Chinese documentation.
9. Execute focused tests, then full applicable suites; repair regressions without
   updating goldens merely to hide failures.
10. Build release artifacts, create recoverable live backups, deploy, re-export
    Gold10, import, compile, and validate the real generated outputs.

## Validation

- Python focused gate:
  `python -m unittest tests.test_migr_semantic_core tests.test_migr_bundle_security tests.test_migr_package_identity`
- Python full gate:
  `python -m unittest discover -s tests -p "test_migr*.py"`
- Correctness lint:
  `python -m ruff check migr migr_blender tests tools`
- Blender: install both ZIPs in a clean 5.2 profile; test semantic-only export,
  bake export, missing worker, repeat determinism, and source non-mutation.
- Unity: compile package, run all MiGR EditMode tests, exercise automatic import,
  dependency changes, restart recovery, rollback, five workflow routes, legacy
  rejection, and ownership preservation.
- License: scan all shipped Shader/HLSL sources for copyright and SPDX markers
  and verify the UPM allowlist excludes extracted game assets.
- Live acceptance: automatic Gold10 import produces all owned/user assets and
  reports no compile, pink-material, unsupported-version, or TextAsset-only
  failure.

## Results and follow-up

- **waived / no verdict:** the external read-only review exceeded 10 minutes
  and returned no verdict. The maintainer explicitly waived this gate.
- **passed:** `py -3.13 -m unittest discover -s tests -p "test_*.py"`:
  58/58 tests.
- **passed:** Unity EditMode job `0ab7147f348f429c9ed1b55c0dce0d65`:
  16/16 tests.
- **passed:** Blender 5.2.0 LTS installed both ZIPs through
  `bpy.ops.extensions.package_install_files`; the normal user profile reports
  both extensions enabled and registered.
- **passed:** Blender exported Gold10 from the locked `金1.blend` source as
  `standard_pbr`, with six baked resources. Bundle hash:
  `3e7c2874001694d786b24486f419791e693abf9626dadcc690ce5da572cf6626`.
- **passed:** Unity automatically imported `金10.migrbundle`, created the
  diagnostic bundle asset, wrapper, ASCII-safe generated Sub Graph, base
  Material, user Material Variant, textures, identity report, and committed
  receipt. The durable queue has no diagnostics.
- **passed:** live Shader Graph compiler messages: 0. A real URP scene render
  produced the expected metallic gold sphere instead of a pink error material.
  Evidence:
  `C:\Users\22687\Desktop\unity\test\Library\MiGR\Gold10SceneRenderFixed.png`.
- **passed:** repeating the same non-full import preserved all generated/user
  bytes; combined SHA-256:
  `9075e44f3a723404ec00a169f41fcb95251ea1d5e6cb0554d4eec7613612cab8`.
- **passed:** deterministic release hashes: semantic exporter
  `5d2f3e66fb89ae8e5325f990a8729c96a03b78559609c83c76adaeabcb414f30`,
  GPL worker
  `bb06b16b01a0576894bfebe8eef055123fecf405970e65d6362b14317f6f957c`,
  UPM `903632bf82d6dd0a04d355e2a255484a098f9b784502a1304e87ebf2a80c5249`.
- **passed:** final source and deployed Unity package trees match exactly:
  123 files on each side.
- **backup:** deployment rollback root:
  `C:\Users\22687\Desktop\unity\test.migr-backup.20260727-222237`.
- **not executed:** Ruff correctness lint because the installed Python 3.13
  environment has no `ruff` module; no dependency was installed implicitly.
- **remaining evidence gate:** Gold10 Standard PBR has live visual evidence.
  Generic Toon and the three game workflows have schema, registry, and license
  evidence but not four additional live visual fixtures, so the compatibility
  matrix remains `Experimental`.
