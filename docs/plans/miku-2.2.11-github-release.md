# Miku 2.2.11 GitHub release delivery

Status: completed on 2026-08-05. This plan publishes the 2.2.11
major-version-only compatibility work as a tagged GitHub Release with the two
reproducible package artifacts and a SHA-256 manifest.

## Baseline

The 2.2.11 implementation plan is
[`docs/plans/miku-2.2.11-major-version-only-compatibility.md`](miku-2.2.11-major-version-only-compatibility.md).
Its decisions are in force: version validation relaxes to a major-version
policy (Blender 5.x, Unity 6000.x, URP/Shader Graph 17.x); the certified
reference moves to 6000.5.4f1 / 17.5.4; both ends unify at 2.2.11. MaterialIR
2.0, Bundle 1.0, and public shader property names are not changed; the
2.2.9/2.2.10 target-profile canonical hash is retained so existing bundles keep
importing.

## Decisions

- The release branch is `codex/miku-2.2.11-release` based on `main` HEAD
  (`60a1e6f`) because local and remote `dev` are stale (4 commits behind
  `main`, missing the 2.2.9/2.2.10 changes this release builds on).
- Development committed per task (11 commits), then the branch merged into
  `main` via PR #9 with a merge commit; the tag `v2.2.11` points at that merge
  commit.
- Release title is `Miku 2.2.11`; release notes are the `## 2.2.11 - 2026-08-05`
  section from `CHANGELOG.md`.
- Release assets are the three prebuilt files in `artifacts/miku-2.2.11-final-a/`;
  `artifacts/miku-2.2.11-final-b/` is retained as the cross-check source.
- No force-push, no rewrite of existing history, no alteration of the existing
  `v2.2.8`/`v2.2.9`/`v2.2.10` tags or releases.

## Implemented scope

- Major-version validation gates in `miku_blender/versioning.py`,
  `miku/bake_protocol.py`, `MikuRuntimeCompatibility`
  (`MikuBundleImporter.cs`), and the Shader Graph adapter selector
  (`MikuShaderGraph17RuntimeBackend.cs`, with `17_5`/`17_6` adapters and
  unknown-minor clamping).
- Certified reference moved to Blender 5.2.0 / Unity 6000.5.4f1 / URP+SG
  17.5.4; `TargetProfile` and the package manifest raised to match
  (`unity: 6000.5`, URP `17.5.4`).
- Both ends unified at 2.2.11; target-profile canonical hash rebased
  (old 2.2.9/2.2.10 hash retained as `Package2210ProfileHash`);
  `docs/provenance/miku-unity-package-asset-identity.json` rebaselined in the
  same commit as the package edits.
- ADR 0014, changelogs, bilingual release notes, compatibility matrix, and
  current-state documentation sweep.

## Progress

- [x] 2026-08-05: Confirmed canonical roots, `v2.2.10` already released, `dev`
  stale behind `main`, and the strict closed-range validation code that 2.2.11
  replaces.
- [x] 2026-08-05: Created `codex/miku-2.2.11-release` from `main` (`60a1e6f`)
  and committed 11 scoped commits covering Blender/Unity/bake validation
  relaxation, manifest and version unification, hash migration, changelogs,
  matrix, ADR 0014, and docs.
- [x] 2026-08-05: Verified locally — `tools/ci/run_checks.py --profile pr`
  (260 tests, exit 0), `ruff check` clean, Blender headless smoke 8/8 on 5.2.0,
  `miku_package_identity.py --check` green, deterministic double-build
  byte-identical.
- [x] 2026-08-05: Pushed the branch and opened PR #9 (merge commit
  `1584561f83e66c7eed8f3e37c78a028a25369c5a`); the required `core` check
  passed (run 30978315149) and the PR merged via `gh pr merge --merge`.
- [x] 2026-08-05: Created GitHub Release `Miku 2.2.11` with the three artifacts
  from `artifacts/miku-2.2.11-final-a/`; notes are the `## 2.2.11 - 2026-08-05`
  section copied verbatim from `CHANGELOG.md`.
- [x] 2026-08-05: Verified the three GitHub asset digests match
  `artifacts/miku-2.2.11-final-a/SHA256SUMS.txt`.

## Discoveries

- `dev` is stale relative to `main` (both local and `origin/dev` sit at the
  2.2.8-era `97f644e`); the 2.2.9/2.2.10 releases were merged to `main` without
  merging `dev` forward. The release branch therefore bases on `main`, which
  contains the code this release modifies.
- GitHub's git HTTPS transport (github.com:443) was intermittently
  unreachable during this session while the `gh` API (api.github.com)
  continued to work. The merge was created via `gh pr merge`, and the tag +
  release were created with `gh release create v2.2.11 --target <merge-sha>`,
  which points `v2.2.11` at the merge commit without requiring a local fetch.
- The auto-merge race that hit PR #6 in 2.2.9 did not recur: the release PR had
  no auto-merge request, and the merge was performed manually after the `core`
  check reached SUCCESS.
- The tag `v2.2.11` was created by `gh release create` rather than a local
  `git tag -a` + `git push origin v2.2.11`; it is an annotated tag pointing at
  the PR #9 merge commit (`1584561f`), confirmed via the `git/ref/tags/v2.2.11`
  API.

## Decision log

- 2026-08-05: Base the release branch on `main` instead of the stale `dev`
  because `dev` lacks the 2.2.9/2.2.10 code this release modifies.
- 2026-08-05: Use per-task commits rather than a single release commit; the
  release is reviewable as small coherent changes per AGENTS.md section 13.
- 2026-08-05: Create the tag and release via `gh release create --target` when
  the local git fetch could not reach GitHub, keeping the published tag and
  release authoritative and identical to what a local tag would produce.
- 2026-08-05: Report Unity EditMode as blocked rather than claimed passing
  (cached Shader Graph 17.4 lacks `UnityEngine.GUID`, and the package now
  requires URP >= 17.5.4 on the 6000.4.5f1 runner).

## Implementation sequence

1. `git checkout -b codex/miku-2.2.11-release` from `main`.
2. Commit per task (11 commits) implementing the version-validation relaxation,
   manifest fields, version unification, hash migration, changelogs, matrix,
   ADR 0014, and docs.
3. `python tools/ci/run_checks.py --profile pr` and
   `tools/release/build_release.py --output-dir artifacts/miku-2.2.11-final-{a,b}`;
   diff the two `SHA256SUMS.txt` (byte-identical).
4. `git push -u origin codex/miku-2.2.11-release`.
5. `gh pr create --base main --head codex/miku-2.2.11-release` (PR #9).
6. Wait for the required `core` check (SUCCESS), then `gh pr merge --merge`.
7. `gh release create v2.2.11 --target <merge-sha> --title "Miku 2.2.11"
   --notes-file <CHANGELOG 2.2.11 section> artifacts/miku-2.2.11-final-a/*`.
8. Verify tag target, release, and asset digests with `gh release view v2.2.11`.

## Validation

- `gh pr view 9` reports `state: MERGED` with merge commit `1584561f`.
- `gh release view v2.2.11` lists the three assets; the tag ref
  `git/ref/tags/v2.2.11` resolves to `1584561f`.
- Each uploaded asset digest matches
  `artifacts/miku-2.2.11-final-a/SHA256SUMS.txt`.

## Results and follow-up

- Delivery commit on `main`: `1584561f83e66c7eed8f3e37c78a028a25369c5a` (PR #9
  merge of `codex/miku-2.2.11-release`).
- Tag: `v2.2.11` points at `1584561f`.
- Release: `Miku 2.2.11` published 2026-08-05T05:41:07Z at
  <https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/releases/tag/v2.2.11>.
- Assets (digest matches `artifacts/miku-2.2.11-final-a/SHA256SUMS.txt`):
  - `miku_shader_converter-2.2.11.zip` (196,881 bytes,
    `b3a65519a14d67833ddb066b33cc637584e4e84fb6c0f74b8effa619b416c50e`)
  - `com.miku.shaderconverter-2.2.11.tgz` (397,470 bytes,
    `b7f99094e90dc5c947d954b687b717e8f8b58b8ead85e68d1bf45c397c02c288`)
  - `SHA256SUMS.txt` (203 bytes)
- Follow-up: local `main` could not be fast-forwarded in this session because
  GitHub's git HTTPS transport was intermittently unreachable while the `gh`
  API worked; run `git fetch origin main && git merge --ff-only origin/main`
  when the network is stable so the local checkout includes the merge. The
  published tag, release, and assets are authoritative and already verified.
