# Remove the Endfield-specific product surface

This ExecPlan is a living implementation record. It follows `PLANS.md` and must
be updated as implementation and validation progress.

## Purpose and outcome

Remove the Endfield-specific Blender translation, MiGR schema surface, Unity
backend/runtime, project assets, fixtures, tools, visual baselines, and release
artifacts. Current documentation and newly built packages must no longer claim
or contain that support. Historical plans, audits, handoffs, and changelog facts
remain available for project history.

The remaining Genshin, Honkai: Star Rail, Wuthering Waves, Standard PBR, and
target-neutral NPR workflows must continue to work. Unsupported non-empty
preset identifiers must stop generation with a structured
`unsupported_preset` diagnostic instead of silently falling back.

## Context and constraints

- The worktree was already heavily modified before this cleanup. Do not reset,
  restore, or replace shared files wholesale; retain unrelated user changes.
- Generated Endfield assets are explicitly in scope even when they contain
  local modifications. Untracked source packs in scope have no Git recovery.
- The validated Unity stack is Editor 6000.4.5f1, URP 17.4.0, and Shader Graph
  17.4.0. Blender's current public add-on version is 0.5.0 and the Unity package
  version is 0.8.1.
- Active public versions will become Blender 0.6.0, Unity package 0.9.0,
  `mgir-2.0`, `mgir-preset-2.0`, and overlay schema version 3.
- Git history, historical documentation, `CHANGELOG.md` history, Unity Library,
  Temp, and Logs remain. New release artifacts exclude historical material.
- Shared Unity scenes must be changed through Unity APIs before target assets
  are removed so serialized references can be resolved safely.

## Progress

- [x] 2026-07-22: Read repository and Unity orchestration instructions, inspect
  the dirty worktree, target inventory, active Unity instance, and compatibility
  versions.
- [x] 2026-07-22: Agree on cleanup scope, history policy, and version strategy.
- [x] Clean and save shared Unity scenes, then remove dedicated Unity assets.
- [x] Remove dedicated Blender/Core/Unity implementation and orphaned runtime
  modules.
- [x] Publish current schema/version changes and legacy compatibility behavior.
- [x] Remove dedicated tests, fixtures, tools, baselines, screenshots, source
  packs, and contaminated release artifacts.
- [x] Update current documentation, migration guidance, architecture decision,
  notices, and changelog.
- [x] Build deterministic replacement release archives.
- [x] Run Python, .NET, Unity, Blender, residual-content, GUID,
  archive, and diff validation.
- [x] Record final results and limitations here.

## Discoveries

- 2026-07-22: Two Unity 6000.4.5f1 editors are connected. The selected project
  instance is `unityproject`; its active scene is the dedicated four-character
  Endfield test scene and it is idle and ready for tools.
- 2026-07-22: `SampleScene.unity` contains target prefab GUID references and two
  `B2U_DeltaE_*` helper groups alongside unrelated game content, so restoring the
  whole scene would destroy user work.
- 2026-07-22: Genshin, HSR, and Wuwa share `NPR_FaceSDF.hlsl`; the other audited
  NPR modules are only used by the removed backend and can be deleted.
- 2026-07-22: Blender release ZIPs 0.3.0 through 0.5.0 and Unity package archives
  0.4.0 through 0.7.0 contain the removed implementation. Earlier Unity package
  archives were inspected as clean.
- 2026-07-22: The repository contains the official portable Blender 5.0.1 build
  under `.tools`, so the headless export suite could be executed. Its driver did
  not previously pass `--python-exit-code`; Blender Python failures therefore
  produced a false successful process status. The driver now propagates them.
- 2026-07-22: The full Python suite still contains 34 assertions for the old
  integrated ShaderLab compiler. The current compiler intentionally returns a
  Shader Graph request with `shaderSource: null`; those failures predate and are
  outside the feature-retirement change and were not hidden or deleted.

## Decision log

- 2026-07-22: Preserve historical plans, audits, handoffs, and changelog entries
  rather than rewriting Git-visible project history. Current docs and packages
  must not present the retired feature as supported.
- 2026-07-22: Use new major interchange schema versions and pre-1.0 component
  minor versions. Legacy non-Endfield documents remain readable, but new export
  always writes current versions.
- 2026-07-22: Reject unknown preset identifiers generically. Do not retain a
  name-specific tombstone or substitute another shader workflow.
- 2026-07-22: Preserve target-neutral NPR data and remaining game presets; only
  remove code whose ownership or use is specific to the retired feature.
- 2026-07-22: Keep `NPR_FaceSDF.hlsl` and the remaining HSR stocking semantics
  because Genshin, HSR, and Wuwa still call those paths. Delete only HLSL modules
  with no remaining callers.
- 2026-07-22: Use Unity `AssetDatabase` and `EditorSceneManager` for scene and
  asset cleanup. Deletions were sent to the Windows Recycle Bin where possible;
  no in-project backup was created.

## Implementation sequence

1. Use the active Unity editor to load `SampleScene`, remove scene roots whose
   prefab sources resolve under the target asset roots plus `B2U_DeltaE_*`
   helpers, save, and validate the scene. Delete dedicated assets through
   `AssetDatabase` and refresh.
2. Delete dedicated Python/C#/HLSL implementations and remove their branches
   from shared exporter, importer, compiler, model, texture, shader GUI, editable
   asset, dynamic-effect, release-builder, and UI code.
3. Add current schemas and update export/import version handling. Move old
   schemas under historical documentation, retain legacy readers for unaffected
   documents, and add generic unknown-preset validation.
4. Delete named source packs, outputs, screenshots, fixtures, test scenes,
   scripts, visual baselines, and contaminated archives. Generalize retained
   Delta-E tooling.
5. Update active tests and documentation, add migration and architecture records,
   then generate deterministic 0.6.0 and 0.9.0 release artifacts.
6. Run all validation, review the final diff/status, and update this record with
   exact outcomes.

## Validation

- `python tools/ci/run_checks.py --profile pr`
- `python tools/ci/run_checks.py --profile release`
- `tools/ci/run_unity_editmode.ps1 -UnityPath '<Unity 6000.4.5f1 Unity.exe>'`
- Run compatible Blender 5.x headless tests when an executable is installed.
- Search active source/assets for retired identifiers while excluding approved
  historical documentation, this plan, migration/ADR/changelog records, `.git`,
  and regenerable Unity caches.
- Resolve all deleted Unity `.meta` GUIDs and assert no surviving asset or scene
  references them.
- Inspect every remaining ZIP/TGZ entry and text payload; build replacement
  archives twice and require matching hashes.
- Run `git diff --check` and review `git status --short` without modifying
  unrelated files.

## Results and follow-up

Implementation is complete for the requested retirement scope.

Executed and passed:

- Unity 6000.4.5f1 loaded and saved `SampleScene`; the final scene has seven
  unrelated roots, zero missing scripts, zero broken prefab references, and no
  console errors. Package EditMode tests passed 19/19 through
  `tools/ci/run_unity_editmode.ps1`.
- Both .NET 8 harnesses restored, built in Release with zero warnings/errors,
  and ran successfully. The compiler harness generated a Shader Graph request;
  the HSR smooth-normal harness passed.
- Blender 5.0.1 bundle export and automatic-bake smoke scripts passed. The
  edge-case script failed one pre-existing grouped-closure classification check;
  the corrected CI driver now returns nonzero for this failure.
- Current schema/fixture, unknown-preset, legacy non-preset import, package
  absence, archive absence, and Generic Toon contract tests passed. The full
  Python run executed 297 tests: 244 passed, 19 skipped, 4 failed, and 30 errored;
  all 34 failures are stale integrated-ShaderLab expectations against the
  current Shader Graph request backend.
- Active-source identifier search returned no matches outside approved history,
  migration/ADR/changelog/plan records, and caches. The path audit found only
  ignored `__pycache__` artifacts. All 220 deleted Unity `.meta` GUIDs had zero
  remaining references under `unityproject/Assets`.
- Twenty-seven retained ZIP/TGZ files were inspected. The only raw binary match
  was a false positive in Blender's bundled NumPy tests; current and retained
  legacy Unity archives contained no removed feature path or text.
- Two final builds produced identical file lists and hashes. The final Blender
  ZIP has 32 files, 134341 bytes, SHA-256
  `56109c02f2119a08f6dd5a9197fb5ff36062a8da63c5a849988483346c99fd60`.
  The final Unity TGZ has 175 files, 202898 bytes, SHA-256
  `2358e31abcafa22f0ee421906bdab763dd2b6f3b3939a487ce8e918ee1429db1`.
- `run_checks.py --profile pr` and `--profile release` both passed version,
  text, AST, schemas/fixtures, documentation links, package/notices, .NET, and
  reproducible-release checks. Text hygiene initially identified cache/history
  false positives; the current check excludes approved historical roots and
  `__pycache__`.

Blocked or failed checks:

- Ruff was not installed in any Python 3.11+ environment. Installing
  `requirements-dev.txt` was blocked by the configured package index/proxy, so
  both aggregate profiles report the missing module.
- The aggregate Python check reports the 34 stale ShaderLab expectations noted
  above. Migrating those broad compiler tests to Shader Graph asset assertions
  is a separate backend-transition task.
- The release profile additionally remains blocked by intentionally unconfigured
  maintainer/CODEOWNER, private security contact, and incomplete third-party
  rights review in `.github/project-maintainers.yml`.
- The Blender edge-case grouped-closure classification failure is unrelated to
  the retired preset but is now visible because the driver no longer swallows
  Blender Python exit status.

No compatibility baseline changed beyond the documented schema/preset retirement:
Unity remains 6000.4.5f1 with URP and Shader Graph 17.4.0; Blender 5.0.1 is the
exact tested version. No automatic visual migration is provided for retired
preset documents.
