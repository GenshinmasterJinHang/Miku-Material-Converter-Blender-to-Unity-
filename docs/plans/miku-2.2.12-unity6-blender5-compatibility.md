# Miku 2.2.12 Unity 6 and Blender 5 compatibility

## Purpose and outcome

Release one deterministic Miku 2.2.12 Blender ZIP and one Unity TGZ that can
be installed in the released Blender 5.0-5.2 and Unity 6000.0-6000.5 technical
streams on Windows. Correct the 2.2.11 contradiction where runtime validation
accepted older Unity 6 editors while the package manifest required Unity
6000.5 and URP 17.5.4. Compatibility means successful package/extension
installation, compilation, end-to-end material import, deterministic asset
generation, and explicit failure for unsupported or mismatched tuples.

## Context and constraints

- Canonical source roots are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`. Retired B2U roots, installed
  extensions, validation-project package caches, and `dist/` archives are not
  implementation sources.
- The worktree starts with only the unrelated untracked `vibe-kanban/`
  directory. It must remain untouched.
- The local certified Blender executable is fixed at
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe` and reports 5.2.0.
  Do not replace an installed extension while a Blender GUI has unsaved work.
- Only Unity 6000.4.5f1 is presently installed locally. Other editor results
  must not be claimed until those exact runtimes execute the tests.
- Generated Stable IDs, public Shader property references, user-owned wrapper
  assets, MaterialIR 2.0, Bundle 1.0, and Bake Result schemas stay compatible.
- Unity package dependencies cannot express a version range. The single TGZ
  declares the 6000.0 / URP 17.0 floor; each validation project pins the URP
  package appropriate for its editor technical stream.

## Progress

- [x] 2026-08-08: Confirmed canonical roots, package identity, Git status,
  fixed Blender 5.2.0, current Unity/Blender version gates, and local editor
  availability.
- [x] 2026-08-08: Implemented bounded Unity editor/package tuple validation and removed the
  unknown-minor adapter clamp.
- [x] 2026-08-08: Expanded Shader Graph adapter/template preflight and added compatibility tests.
- [x] 2026-08-08: Restored the Unity package installation floor and parameterized the Unity
  validation runner/workflow.
- [x] 2026-08-08: Bounded Blender runtime/install compatibility to 5.0 <= version < 5.3 and
  add cross-version test entry points.
- [x] 2026-08-08: Coordinated 2.2.12 metadata, profile hashes, provenance, ADR, compatibility
  documentation, and release notes.
- [x] 2026-08-08: Completed the official-document audit and deterministic release
  build, and record every result honestly. Per the release owner's updated
  acceptance boundary, no additional Unity Editors will be installed;
  Blender 5.0.1/5.1.2/5.2.0 and Unity 6000.4.5f1 are complete.
- [x] 2026-08-08: Reproduced the installed Source Mesh PBR Height binding
  failure and implemented one generated-runtime-property contract shared by
  material binding and validation.
- [x] 2026-08-08: Ran the hotfix EditMode regressions and full package suite and
  rebuilt byte-identical release candidates.
- [x] 2026-08-08: Installed the replacement TGZ and re-imported the reported
  Bundle twice without changing its user-owned wrapper or material variant.

## Discoveries

- Miku 2.2.11 already accepts any Unity 6000.x and package 17.x at runtime,
  but `package.json` declares `unity: 6000.5` and URP `17.5.4`. This produces
  the reported UPM dependency conflict before the importer can run.
- A forced local installation on Unity 6000.4.5f1 / URP and Shader Graph
  17.4.0 previously completed 195 EditMode tests with zero failures and one
  skipped fixture, so the known 6000.4 regression is primarily a package
  resolution defect.
- Existing 17.0-17.6 adapter subclasses share one reflection implementation;
  unknown 17.x minors are unsafely clamped to 17.6. The preflight currently
  serializes only an empty Sub Graph and does not exercise the APIs used by a
  real generated graph.
- The installable Blender manifest currently admits all 5.x and the runtime
  uses only a major-version gate, while many headless scripts hard-code 5.2.0.
- Blender 5.0/5.1 `imbuf.ImBuf` has no output-format controls or
  `write_to_buffer`; merely writing a TARGA buffer to a `.png` path preserves
  TARGA bytes. Their adapters therefore load a temporary, independent image
  datablock, force lazy pixels into memory, and save PNG without mutating the
  user image. Blender 5.2 retains the memory-buffer encoder.
- A Unity TGZ referenced from `Packages/manifest.json` resolves relative to
  the Unity project root, not the manifest directory. The matrix runner now
  copies the archive to that resolved location before Editor startup.
- The final TGZ regression lane on Unity 6000.4.5f1 / URP and Shader Graph
  17.4.0 completed 215 tests: 213 passed, zero failed, and two skipped.
- The reported `木头11.001` Bundle retains an original `Height` texture resource,
  but its final Source Mesh PBR channels contain no Height value and no active
  Displacement. The runtime graph therefore correctly omits
  `_MIKU_HeightMap`; the importer failed because a static semantic allow-list,
  rather than the generated graph contract, decided material bindings.
- Installed-project evidence corrected the preceding source-only inference:
  the user-owned wrapper and current generated Sub Graph for this existing
  Bundle both expose `_MIKU_HeightMap`, and the generated base material binds
  the imported Height texture. The replacement package therefore exercised
  the reachable-property hard-binding path for this real asset, not the new
  superseded-resource branch. The superseded branch remains covered by the
  dedicated EditMode fixture. The Bundle contains material and texture
  resources only, so no model or Prefab asset is expected from this import.

## Decision log

- 2026-08-08: Ship one 2.2.12 TGZ with minimum Unity 6000.0 and URP 17.0.0;
  do not create per-editor package variants.
- 2026-08-08: Support stable Unity technical streams 6000.0-6000.5 only and
  require their URP/Shader Graph minors to match 17.0-17.5 respectively.
  URP and Shader Graph exact package versions must match.
- 2026-08-08: Stable future patches inside an admitted technical stream run
  only after capability preflight and emit unvalidated warnings. Unity 6000.6,
  package 17.6, and prerelease channels fail before asset writes.
- 2026-08-08: Support Blender 5.0-5.2, admit future patches within those
  minors with an unvalidated warning, and reject Blender 5.3+.
- 2026-08-08: Formal compatibility claims are Windows-only. Build release
  artifacts and notes but do not publish a GitHub Release.
- 2026-08-08: The release owner explicitly replaced the six new local Unity
  installs with an official-document adaptation audit. Keep those exact lanes
  Experimental until an external runtime matrix records them; retain the local
  6000.4.5f1 final-TGZ regression as the executed Unity evidence.
- 2026-08-08: Keep 2.2.12 because it is an unpublished release candidate. For
  editable Source Mesh PBR graphs, derive required material texture bindings
  from `RuntimePropertyReferences(generatedSubGraph)`. This preserves hard
  failure for reachable properties and emits
  `MIKU_SOURCE_MESH_PBR_RESOURCE_SUPERSEDED` for provenance-only resources.
- 2026-08-08: The release owner subsequently authorized public publication as
  `v2.2.12`. Deliver through a reviewed release-branch PR, fast-forward `dev`
  to the merged `main` commit, and publish the verified ZIP, TGZ, and checksum
  manifest as a stable GitHub Release.

## Implementation sequence

1. Introduce bounded Unity compatibility profiles mapping editor minor to
   URP/Shader Graph minor and adapter. Reject mismatched or prerelease tuples
   before staging a Bundle.
2. Replace the adapter clamp with explicit 17.0-17.5 selection. Expand the
   adapter capability probe to build and serialize representative properties,
   nodes, slots, custom functions, and connections used by generated graphs.
3. Lower the package manifest floor, preserve user-owned wrapper behavior, and
   parameterize Unity tests so each matrix project pins its own editor and URP
   version while installing the final TGZ.
4. Bound Blender manifest/runtime/bake-request validation to 5.0-5.2 and split
   common cross-version smoke coverage from 5.2-certified corpus coverage.
5. Upgrade coordinated component versions to 2.2.12, update the certified
   target to Unity 6000.5.7f1 / URP and Shader Graph 17.5.4, migrate target
   profile hashes, and refresh package provenance.
6. Supersede ADR 0014, update compatibility/install/diagnostic/release docs,
   run the available matrix, build twice, compare artifacts, and self-review.
7. Hotfix Source Mesh PBR binding so `BindMaterial` and `ValidateMaterial`
   consume the same generated runtime-property references; cover superseded
   Height, reachable Height, wrapper safety, and the reported real Bundle.

## Validation

- Python/Core: `python tools/ci/run_checks.py --profile pr` and
  `python -m ruff check miku miku_blender extensions tests tools`.
- Blender certified Windows lane:
  `python tools/ci/run_blender_headless.py --blender
  C:\SteamLibrary\steamapps\common\Blender\blender.exe`.
- Blender compatibility lanes: final ZIP installation and common smoke tests
  on 5.0.1, 5.1.2, and 5.2.0 when those exact Windows binaries are available.
- Unity lane: final TGZ installation, package resolution, compilation, and
  full EditMode suite on the existing local 6000.4.5f1 / 17.4.0 regression.
  Audit official Unity package-manifest and Shader Graph 17.0-17.5 documents
  for the other technical lines without installing those Editors locally.
- Release: build twice into separate artifact directories, compare manifests
  and SHA-256 values, then run `tools/miku_package_identity.py --check`.

### Height hotfix executed commands

- `tools/ci/run_unity_editmode.ps1` against the rebuilt TGZ on Unity
  6000.4.5f1 / URP and Shader Graph 17.4.0 — passed: 218 total, 216 passed,
  zero failed, two skipped.
- `python tools/ci/run_checks.py --profile pr` — passed: canonical boundary,
  schemas, package identity, deterministic component builds, and 262 Python
  tests.
- `python -m ruff check miku miku_blender extensions tests tools`,
  `python tools/miku_package_identity.py --check`, and `git diff --check` —
  passed.
- Two `tools/release/build_release.py` runs produced identical manifests and
  hashes. Blender ZIP remained
  `3344a2e7fc93e08412b6929511bdd26d814309cc5f2ce834864f63db1cb518c4`;
  the corrected Unity TGZ is
  `9d03d5b0cac5da6dbfa81e7f3b34f12a26de1fc226607353bb8fd173f2fe971d`.
- Backed up the existing project's manifest, lock file, and previous TGZ,
  installed the new archive as
  `Packages/com.miku.shaderconverter-2.2.12-height-hotfix.tgz`, resolved and
  compiled the project, then forced two imports of `木头11.001.mikubundle`.
  Both imports completed with zero Console errors or warnings. The receipt is
  committed with `assetReferences=true` and `textureBindings=true`; the
  generated assets, user-owned wrapper, and user material variant retained
  identical SHA-256 values across the two imports.

## Results and follow-up

Miku 2.2.12 now has bounded Unity 6000.0-6000.5 / Shader Graph 17.0-17.5
adapters, a minimum-install package manifest, exact Editor/URP/Shader Graph
tuple enforcement, full capability/template preflight, and bounded Blender
5.0-5.2 adapters. Python/Core completed 262 tests; Ruff and the release profile
passed. The final ZIP installed and passed on Blender 5.0.1, 5.1.2, and 5.2.0;
their normalized IR hashes are identical. The existing Unity 6000.4.5f1 /
17.4.0 lane ran the final TGZ and completed 215 EditMode tests (213 passed,
zero failed, two skipped). Two independent release builds produced identical
ZIP, TGZ, and manifest hashes.

The six new exact Unity Editor lanes were intentionally not installed per the
release owner's updated acceptance boundary. Their mapping and compatibility
logic are based on Unity's official package-manifest and Shader Graph 17.0-17.5
documentation, so those rows remain Experimental until an external runtime
matrix executes them. The release owner subsequently authorized manual public
delivery under tag `v2.2.12`; this does not upgrade any Experimental row to a
runtime-validated compatibility claim.
