# Miku 3.0.0 GitHub release ExecPlan

## Purpose and outcome

Publish the complete, reviewable Miku 3.0.0 source through a pull request and
merge commit, then create a non-prerelease `v3.0.0` GitHub Release containing
only the deterministic Blender ZIP, Unity TGZ, and `SHA256SUMS.txt`. Refresh the
public manuals with five current Unity renders while keeping every private
scene, model, texture, material, local validation artifact, `dist/`, and
`vibe-kanban/` outside the commit and uploaded release assets.

## Context and constraints

- Canonical source roots are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`.
- The release branch is `codex/miku-3.0.0-release`; the target is `main` and the
  release tag is `v3.0.0`.
- Blender validation must use the fixed Blender 5.2.0 installation. Windows GPU
  evidence must use Unity 6000.4.5f1, URP/Shader Graph 17.4.0,
  `-force-d3d12`, and must not use `-nographics`.
- The five documentation PNGs are source-controlled, non-commercial
  documentation assets excluded from Miku's MIT license. They may appear in
  GitHub's automatic source archives but must not enter the installable ZIP/TGZ.
- The external Unity validation project and all of its source assets remain
  private and outside this repository.

## Progress

- [x] 2026-08-13: verified canonical source markers and package identity
  `com.miku.shaderconverter` version 3.0.0.
- [x] 2026-08-13: created `codex/miku-3.0.0-release` without discarding the
  existing 3.0.0 worktree.
- [x] 2026-08-13: recorded the Unity editor state, scene hashes, and window
  layout; the active scene was clean and the graphics API was Direct3D 12.
- [x] 2026-08-13: captured and visually reviewed five 1920x1080 PNGs from
  temporary scene copies, restored all renderer flags, deleted temporary Unity
  assets, and returned to the clean Bronya scene.
- [x] 2026-08-13: froze bilingual documentation, provenance, release notes, compatibility
  claims, and public-document tests.
- [x] 2026-08-13: passed `git diff --check`, the public-doc suite, and the full
  PR profile (274 tests).
- [x] 2026-08-13: produced two byte-identical clean release builds.
- [x] 2026-08-13: passed eight fixed Blender 5.2.0 public scripts and the
  installed-ZIP smoke test.
- [x] 2026-08-13: final TGZ passed Unity full EditMode (335/347, 0 failed,
  12 skipped) and D3D12 acceptance (10/10, no skips or inconclusive results).
- [x] 2026-08-13: recorded final SHA-256 values and rebuilt twice after the
  documentation update; both builds matched each other and the runtime-tested
  artifacts byte-for-byte.
- [x] 2026-08-13: committed and pushed the release, merged PR #12 after its
  `core` check passed, and observed the merge-commit `main` CI pass.
- [ ] Run merged-commit `release-validation`, compare downloaded artifacts,
  publish `v3.0.0`, and verify a clean re-download.
- [x] 2026-08-13: diagnosed the first hosted `release-validation` run before
  artifact creation; Python 3.11 could not install the pinned NumPy 2.5.1.
- [x] 2026-08-13: merged the focused Python 3.13 workflow correction and reran
  hosted release validation on the corrected `main` commit.
- [x] 2026-08-13: merged the Python 3.13 correction as PR #13 and passed hosted
  release validation on `633d773`, but rejected its artifacts because all three
  downloaded files differed from the locally validated files.
- [x] 2026-08-13: normalized release-archive text bytes and produced two
  identical Windows builds of the cross-platform candidate.
- [x] 2026-08-13: the cross-platform final ZIP passed eight Blender scripts and
  installed-ZIP smoke; its final TGZ passed Unity EditMode (335/347, 0 failed)
  and D3D12 acceptance (10/10, no skips or inconclusive results).
- [x] 2026-08-13: merged the reproducibility correction as PR #14; its
  merge-commit `main` CI passed. The final hosted comparison remains pending.
- [x] 2026-08-13: the next hosted TGZ matched byte-for-byte, but the ZIP
  retained platform-specific creator metadata and the checksum manifest used
  platform-native newlines; publication remained blocked.
- [ ] Merge the ZIP-metadata and manifest-newline correction, rerun the exact
  Blender package smoke, and repeat the final hosted comparison.

## Discoveries

- The old `dist/SHA256SUMS.txt` did not identify the currently present TGZ, so
  no pre-existing `dist/` artifact can be uploaded.
- The saved Genshin scene observed at capture time placed Hu Tao and Furina
  renderer-bound centers at x=0.75 and x=3.28. Earlier x estimates were stale
  after the maintainer-approved scene save; camera clones therefore used the
  observed renderer bounds.
- `GameObject.Find` matched nested character objects before the top-level roots.
  Root selection was corrected to `Scene.GetRootGameObjects()` before the final
  Genshin captures. The rejected intermediate PNGs were overwritten before
  entering the repository.
- The first GitHub-hosted release-validation run failed during dependency
  installation because that workflow selected Python 3.11 while
  `requirements-dev.txt` pins NumPy 2.5.1, which requires Python 3.12 or newer.
  The regular `core` workflow already used Python 3.13. The manual Blender
  workflow had the same latent dependency-install mismatch.
- The corrected hosted run completed but its ZIP and TGZ did not match Windows.
  Extracted-file comparison isolated the differences to CRLF/LF bytes in three
  Blender text files and `Runtime/Profiles/MikuAnimeGlobalVolumeProfile.asset`.
  Archive metadata, ordering, and all other payload files matched. The builders
  normalized only `.meta` files, so their "deterministic" guarantee was limited
  to repeated builds from one checkout rather than Windows/Linux checkouts.
- After the text and compression correction, the hosted TGZ matched exactly
  and all 33 Blender ZIP payloads matched after extraction. Every ZIP entry
  still differed in `ZipInfo.create_system` (FAT on Windows, Unix on Linux),
  while `Path.write_text` emitted CRLF in the Windows checksum manifest. Both
  fields must be explicit at archive-write time.
- Direct URP capture used `RenderPipeline.SubmitRenderRequest` with a
  `UniversalRenderPipeline.SingleCameraRequest`; this retained the source
  camera's URP renderer, post-processing, and antialiasing settings.
- Endfield's public character name is 洁尔佩塔 while the private scene root is
  named `杰哥`; both identities are recorded without exposing the private asset.

The final private-scene hashes observed before and after capture were:

| Scene | SHA-256 |
| --- | --- |
| `Assets/鸣潮/鸣潮.unity` | `356d8284183a9901590a2f16fccaed842e48e68c5bc0f2089bff3975e2fa1c62` |
| `Assets/endfield/终末地.unity` | `339721df338abc30512b57c11cf606b0613babda42fa6cce4664be03a507f233` |
| `Assets/星穹铁道/布洛妮娅.unity` | `559d719012b004aa5a607bacf4e9a2c6dfa7bd5115784689b22e9894acbc14e3` |
| `Assets/原神/原神.unity` | `d6f082ac222a408eec6c047833f73512c6ca20453a52c2f8de4d1b4c37f2bab0` |

The recorded editor layout had the Scene view at approximately
`(0, 78.67, 763.67, 367.33)`, Game view at
`(0, 472, 763.67, 500.67)`, and the focused Hierarchy at
`(764.67, 78.67, 236.67, 894)`. Capture did not resize or rearrange these
windows.

## Decision log

- 2026-08-13: use the PR/merge workflow rather than publishing from a dirty
  local `main`; this makes the source boundary and CI evidence reviewable.
- 2026-08-13: preserve the public name 洁尔佩塔 and disclose `杰哥` only as a
  provenance scene-object name.
- 2026-08-13: use temporary scene copies plus `HideAndDontSave` camera clones.
  The Genshin capture changes only temporary `Renderer.forceRenderingOff`
  values and restores them in `finally`.
- 2026-08-13: screenshots are documentation examples, not GPU support evidence.
  Only tests run against the exact final TGZ can establish the compatibility
  row.
- 2026-08-13: upload exactly three release assets. GitHub supplies automatic
  source ZIP/TAR archives; no redundant source bundle is uploaded.
- 2026-08-13: align `release-validation` and `blender-headless` on Python 3.13,
  matching `core` and the local release command. Pinning an older NumPy only for
  the workflows was rejected because it would make hosted release gates use a
  different dependency set from the validated local gate.
- 2026-08-13: canonicalize line endings in known text payloads at archive-write
  time while leaving source files and unknown/binary suffixes byte-exact. This
  creates one cross-platform artifact identity without rewriting the checkout
  or risking binary corruption.
- 2026-08-13: store Blender ZIP entries without Deflate. After extracted bytes
  matched, Windows and Linux still produced different Deflate byte streams of
  the same size. `ZIP_STORED` removes the zlib implementation from artifact
  identity; the modest size increase is acceptable for a small extension and
  does not change installed bytes.
- 2026-08-13: pin Blender ZIP entries to creator system 3 (Unix), consistent
  with their explicit Unix mode bits, and write `SHA256SUMS.txt` as canonical
  ASCII bytes with LF endings. These are archive metadata decisions only and
  do not change installed payload bytes.

## Implementation sequence

1. Replace/add the five PNGs and update their fixed hashes in public tests.
2. Update English canonical and Chinese mirrored README/manual content,
   installation/checksum steps, image gallery, licensing, and provenance.
3. Correct 3.0.0 release notes, compatibility claims, and the unpublished 2.4.0
   record before validation.
4. Run repository gates and freeze source.
5. Build in two empty directories and compare every emitted byte.
6. Run Blender and Unity validation against those exact artifacts.
7. Record real counts and hashes, rebuild twice again, and inspect archive
   contents for excluded data.
8. Explicitly stage only Miku files, commit, push, and merge through GitHub CI.
9. Compare GitHub-hosted release-validation artifacts with the locally verified
   files before exposing the public Release.

## Validation

Required commands:

    git diff --check
    py -3.13 -m unittest tests.test_public_docs -v
    py -3.13 tools/ci/run_checks.py --profile pr
    py -3.13 tools/release/build_release.py --output-dir <empty-a>
    py -3.13 tools/release/build_release.py --output-dir <empty-b>

Blender must be invoked by the repository runners with
`C:\SteamLibrary\steamapps\common\Blender\blender.exe` and expected version
5.2.0. Unity must use the exact final TGZ in a clean project. The full EditMode
suite may skip only documented GPU/external-preview cases; the separate D3D12
acceptance must pass all ten required tests with zero failures, skips, or
inconclusive results.

GitHub validation is complete only when the PR `core` check, merged `main` CI,
and manually dispatched `release-validation` succeed and its downloaded ZIP,
TGZ, and manifest compare byte-for-byte with the local final build.

## Results and follow-up

Local implementation and runtime validation are complete for the
cross-platform artifacts. The final ZIP is `ba63d539...9ac362`; the TGZ is
`d282c9e5...b22d3a`, and the manifest is `a9d87aec...5010b9`. MaterialIR 2.0,
Bundle 1.0, public C# APIs, and JSON schemas are not changed by the release
process. PR #12 merged as `f4d5c72c4396c3585d7035ba7e7c2d0dfc827f06`;
its `main` CI passed. PR #13 aligned hosted validation on Python 3.13 and
merged as `633d77317d37ec1604d432ee0ad373674a79e848`. PR #14 made the
artifacts cross-platform reproducible and merged as
`e28bb7c53f1c1b4e92347eb51d483edfc96507b3`; its `main` CI passed. Hosted
artifact comparison, tag, and Release publication remain.
