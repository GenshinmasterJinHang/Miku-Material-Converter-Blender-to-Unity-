# Miku 2.2.8 GitHub final documentation and material creator

Status: released as `v2.2.8` on 2026-08-03. PR #1 delivered the implementation
and documentation, PR #2 recorded the final artifact hashes, and both merged
with the required CI check passing.

## Baseline

This plan continues the historical 2.2.8 bilingual/localization and Blender
time-input policy plan in
`docs/plans/miku-2.2.8-unity-bilingual-blender-time-policy.md`. Its existing
decisions remain in force: MaterialIR 2.0, Bundle 1.0, Conversion Plan, Bake
Result, and public shader property names are not changed.

## Decisions

- The Blender current-material UI is Standard PBR only. The exporter sets
  `standard_pbr` for that entry point but preserves explicit lower-level legacy
  workflow APIs and historical Bundle readers.
- Unity's public creator is configuration-driven: it enumerates visible public
  2D shader properties in declaration order, excludes `_MainTex` and hidden
  compatibility properties, validates required textures in memory, and only
  then writes a user-owned `.mat`.
- English is canonical public documentation; `docs/zh-CN/` mirrors the same
  sections, links, compatibility claims, and image set.
- Release artifacts are built into an independent output directory and hashed
  by `tools/release/build_release.py`; candidate hashes are never reused.
- Package identity hashes canonicalize text assets to LF, so the immutable
  manifest is stable on Windows checkouts and Linux CI.

## Implemented scope

- Blender frontend and focused regression/smoke coverage.
- Unity menu, part filtering, texture enumeration, required-field rules,
  Wuwa dual binding, localized labels, and EditMode test coverage.
- Root README, Chinese README, English/Chinese Manual, compatibility and
  release notes, Mermaid source/exports, and documentation link regression.
- Reproducible Blender smoke orchestration and release ZIP/TGZ/SHA256 builder.

## Validation record

- Executed: `py -3.13 -m unittest tests.test_miku_blender_frontend` (37 passed),
  then the full Python suite (248 passed) including public documentation
  regression checks.
- Executed: `py_compile` for the new CI/release/capture scripts.
- Executed: two independent final builds in separate output directories with
  byte-identical ZIP/TGZ outputs. Final hashes are recorded in
  `docs/release/miku-2.2.8-sha256.txt`:
  `c3fc830dc3c2c388940355462cd5da071607a73da7eb6cabd3e997dbe2b64a80` for
  the Blender ZIP and
  `0ef9b24e8f051779291b11484f2dd77c662f102426ed01fdcc7fab803730e15b` for
  the Unity TGZ.
- Executed: `tools/ci/run_checks.py --profile pr` and `--profile release`.
- Executed: GitHub Actions `core` checks for PR #1 and PR #2; both passed.
- Executed: fixed-path Blender 5.2.0 public smoke suite (8 self-contained
  scripts passed; private corpus scripts are intentionally excluded).
- Executed: extracted the downloaded ZIP/TGZ in temporary isolated locations
  and compared every packaged file and hash with the canonical source (32 ZIP
  files and 207 Unity package files matched).
- Attempted: Unity 6000.4.5f1 EditMode. The package's cached Shader Graph 17.4
  source currently fails before test discovery on the editor's missing public
  `UnityEngine.GUID` API; the project also reports the existing package compile
  issue in `BuiltInCanvasSubTarget.cs` and `TargetSetupContext.cs`. This remains
  an environment/package blocker, not a claimed passing result.
- The final documentation screenshot renderer and bilingual link regression
  were executed; no private paths or game assets are included in the checked-in
  images.

## Delivery record

- The remote was checked before publishing and had no existing `v2.2.8` tag or
  release. The unsigned annotated tag points to the latest `main` merge
  commit `d6df5f2c029f0441b7fb3be45a4a127741a54db5`.
- Release assets were uploaded as
  `miku_shader_converter-2.2.8.zip`,
  `com.miku.shaderconverter-2.2.8.tgz`, and `SHA256SUMS.txt`.
- All three assets were downloaded again from GitHub; the two package hashes
  matched the checked-in manifest and GitHub's asset digests.
