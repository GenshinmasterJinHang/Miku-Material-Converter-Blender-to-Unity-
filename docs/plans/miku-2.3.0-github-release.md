# Miku 2.3.0 GitHub release delivery

> **2026-08-12 correction:** references to an Endfield game LUT installer are
> historical. The inspected cloth/skin LUTs are material-local; current setup
> uses Volume-only grading unless a genuine independent screen LUT is supplied.

Status: completed on 2026-08-10. This plan publishes the 2.3.0 Endfield
tutorial-lighting, outline, and LUT work as a tagged GitHub Release with the
two reproducible package artifacts and a SHA-256 manifest.

## Baseline

The 2.3.0 implementation plan is
[`docs/plans/miku-2.3.0-endfield-tutorial-outline-lut.md`](miku-2.3.0-endfield-tutorial-outline-lut.md).
The worktree contained the complete uncommitted 2.3.0 change set on top of the
2.2.12 release commit; `vibe-kanban/` is an unrelated untracked project and was
excluded from the release commit.

## Decisions

- Release version is `2.3.0`; both components already declared 2.3.0
  (`package.json`, `blender_manifest.toml`, `pyproject.toml`).
- A single release commit `chore(release): Miku 2.3.0` (`c1f79b0`) was created
  on `codex/miku-2.3.0-release`, pushed, and merged to `main` via PR #11 with
  merge commit `5cae87bbd6a0434033afdc195348402c6414a3e9`.
- Release assets are the three files in `artifacts/miku-2.3.0-final-a/`;
  `artifacts/miku-2.3.0-final-b/` and `artifacts/miku-2.3.0-final-c/` are the
  cross-check sources.
- The Unity package asset identity manifest was regenerated because the 2.3.0
  package file set changed; the old checked-in manifest no longer matched.
- Release notes are the `## 2.3.0 - 2026-08-10` section from `CHANGELOG.md`.
- The tag and release were created with `gh release create --target
  5cae87bbd6a0434033afdc195348402c6414a3e9`, pointing `v2.3.0` at the PR #11
  merge commit.

## Implemented scope

- Opt-in Endfield tutorial lighting (`MikuEndfieldLightingController`), shared
  day/top/diffuse/specular/rim lighting, double-sided Body final normals, and
  part-specific Skin/Face/Eye/Hair/Overlay/HairShadow behavior.
- TangentSpaceV2 UV7 smooth normals and shared screen-space outline contract
  across Genshin, Wuwa, HSR, and Endfield.
- Full-screen 32-cube game LUT installer and Endfield Volume profile factory.
- Wuwa tutorial-compliance controls and Genshin normal-map support; all public
  additions are additive shader properties with no schema or API change.
- Material-creator regression test locking every workflow/part to the current
  final `MIKU/<Family>/<Part>` shader.
- Release workflows (`release-validation.yml`, `unity-editmode.yml`,
  `blender-headless.yml`) pinned to 2.3.0 artifact names.

## Progress

- [x] 2026-08-10: Regenerated `docs/provenance/miku-unity-package-asset-identity.json`.
- [x] 2026-08-10: Merged `## Unreleased` changelog entries into `## 2.3.0 -
  2026-08-10` (root and package changelogs); updated bilingual release notes.
- [x] 2026-08-10: Local validation on the release tree: PR profile 268/268
  with identity 13/13; release profile passed; Blender 5.2.0 headless 8/8 and
  installed-ZIP smoke `MIKU_INSTALLED_COMPATIBILITY_SMOKE_OK`; Unity
  6000.4.5f1 / URP 17.4.0 full EditMode 283 tests with 282 passed, 0 failed,
  1 skipped.
- [x] 2026-08-10: Pushed `codex/miku-2.3.0-release`, opened PR #11, waited for
  the required `core` check (SUCCESS), merged via `gh pr merge --merge`.
- [x] 2026-08-10: Rebuilt on merged `main` twice (plus a third confirmation
  build); all three builds byte-identical.
- [x] 2026-08-10: Created GitHub Release `Miku 2.3.0` (`v2.3.0`) with the
  three assets; verified the tag target and every uploaded asset digest.

## Validation

- `gh pr view 11` reports `state: MERGED` with merge commit `5cae87bb`.
- `gh release view v2.3.0` reports `targetCommitish 5cae87bb`, three uploaded
  assets, and digests matching `artifacts/miku-2.3.0-final-a/SHA256SUMS.txt`.
- Final SHA-256:
  - ZIP `miku_shader_converter-2.3.0.zip`:
    `db2da64cb2a03cd409e61baa7684f17e8e412854df80e43507d3c6ebb31a0c3f`
  - TGZ `com.miku.shaderconverter-2.3.0.tgz`:
    `be6d326c0ada6a97554695a1902344c0acaf3f5fcc89e24e5b2ce23a41c9471d`

## Results and follow-up

- Delivery commit on `main`: `c1f79b0` (via PR #11 merge `5cae87bb`).
- Tag: `v2.3.0` points at `5cae87bb`.
- Release: published 2026-08-10T15:26:18Z at
  <https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/releases/tag/v2.3.0>.
- Follow-up: the final-hash documentation correction
  (`docs(release): record final Miku 2.3.0 artifact hashes`, local commit
  `e0dbf29`) and this delivery record were committed locally after the merge;
  pushing `main` was blocked by GitHub's intermittent HTTPS transport reset
  (`Recv failure: Connection was reset`). The published tag, release, and
  assets are authoritative; retry `git push origin main` when the network is
  stable.
- Known limitations are recorded honestly in `docs/release/miku-2.3.0.md`:
  final-TGZ installation and hash comparison in the port-8080 project,
  transactional scene migration, and D3D11 screenshot checks remain pending
  and are not claimed as passing.
