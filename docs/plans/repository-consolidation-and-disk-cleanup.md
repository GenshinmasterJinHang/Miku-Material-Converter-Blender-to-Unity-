# MiGR 2.2.0 repository consolidation and disk cleanup

## Purpose and outcome

Consolidate the repository around the canonical MiGR 2.2.0 source roots, remove
generated test projects and artifacts, archive user-owned private assets outside
the repository, and reclaim C: space without rewriting Git history.

Validated target tuple: Blender 5.2.0, Unity 6000.4.5f1, URP 17.4.0, Shader
Graph 17.4.0. The final Git state is expected to be a clean local commit tagged
`v2.2.0`; no remote push is part of this work.

## Context and constraints

- Canonical source roots are `migr/`, `migr_blender/`,
  `extensions/migr_semantic_exporter/`, `extensions/migr_gpl_bake_worker/`,
  and `unity/Packages/com.migr.shaderconverter/`.
- Generated test projects, logs, renders, caches, and validation outputs are
  disposable and must not be archived as source.
- Private or unknown-license Blender/model/material assets are user-owned and
  must be moved to `D:\\MiGR-Local-Assets\\` after hash verification.
- The active Blender GUI and Unity GUI processes must not be force-terminated.
  Locked files are reported and skipped.
- Git history is preserved. `git fsck` and garbage collection occur only after
  the final commit/tag and safety snapshot exist.

## Progress

- [x] 2026-07-30: Confirmed canonical MiGR markers and exact compatibility tuple.
- [x] 2026-07-30: Confirmed one Git worktree, dirty migration state, and active
  Blender/Unity processes.
- [x] 2026-07-30: Created `codex/repository-cleanup` branch.
- [x] 2026-07-30: Created and SHA-256 verified the source recovery snapshot on
  `D:\\MiGR-Local-Assets\\recovery\\migr-2.2.0-pre-cleanup-20260730`.
- [~] 2026-07-30: Archived private assets and Blender autosaves on D:. The
  active `贝拉.blend` remains in place until Blender is saved and closed.
- [x] 2026-07-30: Removed generated test projects/artifacts and retired B2U
  content; retained only lightweight source tests and CI runners.
- [x] 2026-07-30: Made Unity EditMode validation self-contained and ephemeral;
  the runner prints counts to the console and deletes XML, logs, the temporary
  project, and the empty scratch root in `finally`.
- [x] 2026-07-30: Ran validation, package determinism checks, and copied the
  verified 2.2.0 release archives to D:.
- [~] 2026-07-30: Cleaned external temporary directories, committed the
  consolidation, created annotated tag `v2.2.0`, and completed `git fsck` plus
  Git GC. Final deletion of files held by the active Blender/CodeGraph
  processes remains process-safe follow-up work.

## Decision log

- Preserve Git history; do not use history rewriting or a new repository.
- Delete all generated test projects/results/data, while retaining lightweight
  source-level tests and CI runners.
- Keep private assets outside Git on D: with a manifest and SHA-256 verification.
- Build release packages from canonical sources, copy verified packages to the
  D: release archive, and remove local `dist/` outputs afterward.

## Validation

Executed and passed: `py -3.13 -m unittest discover -s tests -p "test_*.py"`
(179 tests); `py -3.13 tools/migr_package_identity.py --check`; PR static
checks; Blender 5.2.0 headless smoke; Unity 6000.4.5f1 EditMode (101 tests,
100 passed, 1 skipped, 0 failed); and two consecutive deterministic package
builds. The three release hashes are recorded in
`docs/release/migr-2.2.0-sha256.txt` and the D: release archive. External
temporary directories were cleaned with locked/live entries skipped.

The standalone `py -3.13 -m ruff check .` command was not executable because
the pinned Python environment does not have the `ruff` module installed; the
repository PR runner's AST/static checks and all executable tests passed.

Disk checkpoint: C: free space increased from approximately 114.6 GiB before
cleanup to 175.34 GiB after deleting the generated projects and stale user
temporary entries. The D: archive currently uses approximately 658.4 GiB of
the volume and contains the verified source snapshot, private-asset manifests,
and 2.2.0 release archives.

## Results and follow-up

Final process-safe deletion of the active Blender file and the CodeGraph
SQLite database remains after those processes release their locks. The source
tree itself is committed and tagged. Compatibility remains `Experimental`
until the documented human render review is complete.
