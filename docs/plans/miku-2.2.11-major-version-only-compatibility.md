# Miku 2.2.11 major-version-only compatibility

Status: completed on 2026-08-05. Delivery record:
[`docs/plans/miku-2.2.11-github-release.md`](miku-2.2.11-github-release.md).

## Purpose and outcome

Replace Miku's strict closed-interval version validation (Blender 5.0.0-5.2.0,
Unity 6000.0.0f1-6000.4.5f1, URP/Shader Graph 17.0.0-17.4.0, with hard failure
above the certified upper bound) with a major-version-only policy: any Blender
5.x, any Unity 6000.x (Unity 6), and any URP/Shader Graph 17.x is accepted.
Wrong-major or unparseable/missing versions still fail before any asset write;
in-major versions that are not exactly certified emit
`MIKU_*_VERSION_UNVALIDATED` warnings. The certified reference moves to Blender
5.2.0, Unity 6000.5.4f1, and URP/Shader Graph 17.5.4. Both the Blender
extension and the Unity package unify at **2.2.11** and ship as a coordinated
GitHub Release.

## Context and constraints

- AGENTS.md: canonical sources only; deterministic output; no random IDs; the
  generated-asset StableId namespace (`miku-shadergraph-17.4:`) must not change;
  structured `MIKU_*_UNSUPPORTED` (hard) vs `MIKU_*_UNVALIDATED` (soft)
  diagnostics; compatibility changes need an ADR, matrix, changelog, and
  release notes; tests must be executed, never merely claimed.
- `v2.2.10` was already tagged and released on GitHub (2026-08-04, Unity-only
  fix), so the coordinated release is **2.2.11**.
- `dev` is stale (4 commits behind `main`); the release bases on `main`.
- Blender install is fixed at
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe` (5.2.0).
- Unity EditMode is a known environment blocker; not claimed as passing.

## Implementation sequence

1. `miku_blender/versioning.py` — replace the closed `MIN..MAX` check with a
   major gate (`BLENDER_MAJOR_VERSION = 5`); `5.2.0` stays certified; update
   `supported=5.x` diagnostic strings. Tests updated
   (`tests/test_miku_blender_versions.py`).
2. `miku/bake_protocol.py` — `normalize_bake_blender_version` requires major 5
   instead of the 5.0.0-5.2.0 range; remove the unused `MIN/MAX_RUNTIME_VERSION`.
   `extensions/miku_shader_converter/blender_manifest.toml` —
   `blender_version_max = "5.99.0"` (exclusive install gate admits all 5.x).
   Frozen bake-request 1.0/1.1 certified binding unchanged.
3. `unity/.../Editor/MikuBundleImporter.cs` — `CertifiedUnity` -> `6000.5.4f1`,
   `CertifiedPackage` -> `17.5.4`; `ValidateUnityVersion`/`ValidatePackageVersion`
   hard-fail only on major mismatch (`UnityMajorVersion = 6000`,
   `PackageMajorVersion = 17`); `FindPackageVersion` message -> `supported=17.x`;
   remove the closed-range `Minimum/Maximum*` constants.
4. `unity/.../Editor/MikuShaderGraph17RuntimeBackend.cs` — add
   `ShaderGraph17_5Adapter`/`ShaderGraph17_6Adapter`, extend `CreateAdapter`
   with minors 5/6 and clamp unknown minors to `17_6`; `configuredAdapterMinor`
   -> 6. StableId prefix unchanged.
5. `unity/.../Tests/Editor/MikuBundleImporterTests.cs` — rewrite the four range
   tests to major-gate semantics; add 17.5/17.6/clamp adapter cases.
   NOT EXECUTED locally (EditMode blocked).
6. `unity/.../package.json` — `unity: "6000.5"`, URP dep `17.5.4`.
7. `miku/planner.py` — `TargetProfile` defaults -> `6000.5.4f1` / `17.5.4` /
   `17.5.4`.
8. Version unification to 2.2.11 across `miku/contracts.py`, `miku_blender/__init__.py`,
   both manifests, `pyproject.toml`, release/installed-export tools, C# version
   constants, and Python/C# tests.
9. Mandatory hash migration: recompute `MikuShaderGraph17RuntimeBackend.cs`
   SHA-256 into `implementationHashes["runtimeStructuredBackend"]`; recompute
   the target-profile `canonicalHash` into `ExpectedProfileHash`; retain the old
   2.2.9/2.2.10 hash as `Package2210ProfileHash` in `SupportedProfileHashes`;
   rebaseline `docs/provenance/miku-unity-package-asset-identity.json` in the
   same commit as the package edits.
10. `CHANGELOG.md` and the Unity subpackage CHANGELOG (including a backfilled
    `[2.2.10]` entry).
11. `docs/compatibility.md` matrix, ADR 0014, bilingual release notes, and the
    current-state documentation sweep (historical rows and provenance left
    intact).

## Validation (executed)

- `python tools/ci/run_checks.py --profile pr` — 260 tests, exit 0.
- `python -m ruff check miku miku_blender extensions tests tools` — clean.
- `python tools/miku_package_identity.py --check` — green (after rebaseline).
- `python tools/ci/run_blender_headless.py --blender
  C:\SteamLibrary\steamapps\common\Blender\blender.exe` — 8/8 smoke scripts
  passed on Blender 5.2.0.
- `python tools/release/build_release.py --output-dir
  artifacts/miku-2.2.11-final-{a,b}` — SHA256SUMS byte-identical.
- GitHub `core` CI check on PR #9 — SUCCESS.
- Unity EditMode — **blocked**, not executed (cached Shader Graph 17.4 lacks
  `UnityEngine.GUID`; package now requires URP >= 17.5.4 on the 6000.4.5f1
  runner).

## Results

- Branch `codex/miku-2.2.11-release` merged to `main` via PR #9 (merge commit
  `1584561f83e66c7eed8f3e37c78a028a25369c5a`).
- Tag `v2.2.11` -> `1584561f`; release `Miku 2.2.11` published 2026-08-05 at
  <https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/releases/tag/v2.2.11>.
- Asset digests verified against `artifacts/miku-2.2.11-final-a/SHA256SUMS.txt`:
  `miku_shader_converter-2.2.11.zip`
  `b3a65519a14d67833ddb066b33cc637584e4e84fb6c0f74b8effa619b416c50e`;
  `com.miku.shaderconverter-2.2.11.tgz`
  `b7f99094e90dc5c947d954b687b717e8f8b58b8ead85e68d1bf45c397c02c288`.

## Known limitations and follow-up

- Unity EditMode tests and the C# assertions are implemented but not executed in
  this environment; the 6000.5.4f1/17.5.4 certified tuple remains to be
  validated on an actual Unity 6.5.4 install.
- Local `main` was not fast-forwarded to the merge commit during this session
  because GitHub's git HTTPS transport was intermittently unreachable (the `gh`
  API worked); `git fetch origin main && git merge --ff-only origin/main` is
  required when the network is stable.
