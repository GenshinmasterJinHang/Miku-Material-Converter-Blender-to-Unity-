# Open-source foundation ExecPlan

## Purpose and outcome

Turn the current B2U/MiGR repository into an honestly documented, testable, and
release-gated open-source project without rewriting existing compatibility
surfaces or disturbing the large user worktree. The current ShaderLab generator
remains available as a legacy compatibility path. This plan does not implement a
Shader Graph MultiJson serializer.

## Context and constraints

- `AGENTS.md` is the engineering constitution and takes precedence over this
  plan.
- B2U package identifiers, namespaces, Blender operators/settings, generated
  property references, and existing schema paths are public compatibility
  surfaces.
- The pre-task audit observed 383 modified, 2 deleted, and 16,292 untracked
  entries. At implementation start Git reported 383 modified, 2 deleted, and
  16,300 untracked entries including the newly added `AGENTS.md`; pre-existing
  changes are user-owned and must not be normalized or removed.
- No Git remote or tags are configured. Maintainer handle and private security
  contact are intentionally unknown.
- Original project code will be MIT licensed by explicit maintainer decision.
  Third-party assets and Unity-licensed packages remain under their own terms.
- Blender is not available on `PATH`. Unity 6000.4.5f1 is installed locally;
  project dependencies resolve URP and Shader Graph 17.4.0.

## Progress

- [x] 2026-07-22: Read the supplied constitution and saved it as root
  `AGENTS.md` in UTF-8 without weakening its requirements.
- [x] 2026-07-22: Reconfirmed branch, Git status, remote/tag state, and nested
  instruction files before foundation edits.
- [ ] Create audit, governance, architecture, compatibility, and contributor docs.
- [ ] Add repository hygiene, CI runner, deterministic release builder, and
  GitHub workflow entry points.
- [ ] Reconcile the four known stale assertions and add narrowly scoped
  correctness/safety tests and implementation.
- [ ] Connect Unity verification to a real EditMode/NUnit assembly.
- [ ] Run layered validation and record final results.

## Discoveries

- The repository is on `task-1.4-mgir-fixtures`, with no remote and no tags.
- Source versions are independent and currently differ: Blender add-on 0.5.0,
  Unity package 0.8.1, latest committed Unity tarball 0.7.0, MiGR wire schemas
  `mgir-1.0` plus a separate `schemaVersion: 2` overlay.
- Existing editable output is ShaderLab `.shader`, written by
  `B2UEditableShaderAssetUtility`; no production `.shadergraph` or
  `.shadersubgraph` backend exists.
- A baseline Python run before implementation executed 318 tests: 293 passed,
  21 skipped, and 4 failed due to stale expectations described in the task.
- `git diff --check` is not meaningful repository-wide while user-owned Unity
  YAML has pre-existing whitespace; formatting verification must be scoped to
  foundation-owned source and documentation.

## Decision log

- 2026-07-22 — Keep B2U as the compatibility/product label in existing public
  surfaces; use MiGR for the target-neutral semantic IR and project constitution.
- 2026-07-22 — Use independent SemVer for Blender, Unity package, and schemas;
  document valid combinations in a compatibility matrix.
- 2026-07-22 — Release archives use explicit allowlists, not a repository-wide
  source archive, because the worktree contains restricted and unreviewed assets.
- 2026-07-22 — Release validation fails closed until real maintainership and a
  private security contact are configured. No identities will be fabricated.
- 2026-07-22 — The existing ShaderLab path is retained but frozen as legacy. A
  version-specific Shader Graph backend requires real 17.4.0 fixtures and is a
  separate development task.

Rejected alternatives:

- Renaming B2U packages/namespaces/operators: rejected as a breaking public API
  change unrelated to the foundation work.
- Treating every repository file as MIT: rejected because third-party rights are
  not transferred by adding a root license.
- Inventing Shader Graph MultiJson fields or claiming support based on package
  presence: rejected because no verified serializer/fixture pipeline exists.
- Automatically updating all golden files: rejected; each stale assertion is
  reconciled against current implementation and contracts.

## Implementation sequence

1. Establish constitution, ExecPlan rules, and auditable worktree baseline.
2. Add canonical English public docs, Chinese workflow preservation, governance,
   architecture, schema mapping, compatibility, diagnostics, and ADRs.
3. Add minimal format/lint configuration, a single CI runner, deterministic
   package builder, release allowlists, and pinned Actions workflows.
4. Add or update tests before narrow correctness and file-safety implementation.
5. Add Unity EditMode assembly and headless runner entry points without claiming
   execution on unavailable environments.
6. Run PR and release profiles, unit/harness/editor checks where possible,
   reproducibility checks, diff review, and final status count.

## Validation

Planned commands (results are recorded below only after execution):

```text
python -m unittest discover -s tests -p "test_*.py"
python tools/ci/run_checks.py --profile pr
python tools/ci/run_checks.py --profile release
dotnet restore/build/run for each tests/*Harness project
Unity.exe -batchmode ... EditMode tests
git diff --check -- <foundation-owned paths>
```

Blender headless validation remains `implemented but not executed` until a
supported Blender binary is available. Blender 5.0 and Unity/URP/Shader Graph
versions other than the exact configured set remain `Unknown`.

## Command log and results

- 2026-07-22 — `git status --porcelain=v1 -uall`: implementation-start snapshot
  383 modified, 2 deleted, 16,300 untracked, 16,685 total (includes new
  `AGENTS.md`; the worktree was already highly dirty).
- 2026-07-22 — `git branch --show-current`, `git remote`, `git tag`: branch
  `task-1.4-mgir-fixtures`; zero remotes; zero tags.
- Pre-implementation — `python -m unittest discover -s tests -p "test_*.py"`:
  318 run, 4 failed, 21 skipped, about 86 seconds. Failures: material export
  target `objects`, Endfield FaceSDF contract, hard-coded package 0.7.0, and
  Unlit Shader output expectation.

## Results and follow-up

Implementation is in progress. Release blockers that cannot be resolved by this
task are: a named maintainer/CODEOWNER, a private security reporting channel,
rights review or removal authorization for restricted assets, Blender 5.0
validation, and a real version-specific Shader Graph 17.4.0 backend built from
Unity-created fixtures.
