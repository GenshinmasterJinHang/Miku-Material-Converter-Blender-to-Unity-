# Miku 2.2.8 GitHub final documentation and material creator

Status: implementation complete locally; GitHub PR/release pending the
maintainer's GitHub CLI authentication.

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
- Executed: candidate release build in `artifacts-final-check` (hashes must be
  regenerated after the final documentation image pass), followed by two
  independent final builds with byte-identical ZIP/TGZ outputs. Final hashes
  are recorded in `docs/release/miku-2.2.8-sha256.txt`.
- Executed: `tools/ci/run_checks.py --profile pr` and `--profile release`.
- Executed: fixed-path Blender 5.2.0 public smoke suite (8 self-contained
  scripts passed; private corpus scripts are intentionally excluded).
- Attempted: Unity 6000.4.5f1 EditMode. The package's cached Shader Graph 17.4
  source currently fails before test discovery on the editor's missing public
  `UnityEngine.GUID` API; the project also reports the existing package compile
  issue in `BuiltInCanvasSubTarget.cs` and `TargetSetupContext.cs`. This remains
  an environment/package blocker, not a claimed passing result.
- Pending: fixed Blender headless smoke, final screenshot review, independent
  double-build byte comparison, isolated install/hash comparison, and GitHub
  PR/release after `gh` authentication is available.

## Follow-up gate

Before publishing `v2.2.8`, regenerate `docs/release/miku-2.2.8-sha256.txt`,
verify the remote has no existing `v2.2.8` tag/release, push the final branch,
wait for CI/Blender/Unity validation, and re-download the three release assets
for checksum verification.
