# Miku 2.2.9 version-range compatibility

## Purpose and outcome

Allow Miku to install and run on Blender 5.0.0 through 5.2.0 and Unity
6000.0.0f1 through 6000.4.5f1 with URP and Shader Graph 17.0.0 through
17.4.0. The certified tuple remains Blender 5.2.0, Unity 6000.4.5f1, and
URP/Shader Graph 17.4.0. Other in-range versions continue with explicit
unvalidated-version diagnostics; versions outside the closed ranges fail
before generated assets are written.

## Context and constraints

- Canonical sources are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`.
- The Blender extension manifest uses an exclusive maximum, so `5.2.1` is the
  first unsupported version and 5.2.0 is the effective maximum.
- Blender validation remains fixed to the repository-mandated 5.2.0 Steam
  executable. Lower allowed Blender and Unity versions are not locally
  installed and must not be described as validated.
- Shader Graph internal access stays isolated behind version adapters and must
  pass a capability preflight before any import transaction starts.
- Bake request 1.0 and 1.1 remain frozen to the certified Blender build.

## Progress

- [x] 2026-08-03: Confirmed canonical roots, package identity, clean worktree,
  existing exact-version gates, and local validation runtimes.
- [x] 2026-08-03: Implemented Blender range checks and bake request 1.2.
- [x] 2026-08-03: Implemented Unity, URP, and Shader Graph range checks and
  explicit 17.0-17.4 adapters.
- [x] 2026-08-03: Coordinated the 2.2.9 release metadata and documentation.
- [x] 2026-08-03: Ran Python, Blender, Unity, manifest, and deterministic
  package checks.

## Discoveries

- Blender installation was blocked by `blender_version_min = "5.2.0"`; runtime
  registration and Time-node creation also required exactly `(5, 2, 0)`.
- Unity installation required 6000.4 and URP 17.4.0, while bundle import also
  required the exact editor, URP, and Shader Graph patch versions.
- The bake request schemas encode the certified Blender version and commit as
  constants, so broadening execution requires a new request kind rather than
  changing frozen 1.0/1.1 semantics.
- Historical repository evidence records Blender 5.0.1 export and automatic
  bake success, but it is not evidence for the new 2.2.9 implementation.
- Unity 6000.4.5f1 exposed a pre-existing compile error where editor code used
  the nonexistent `Shader.HasProperty` API before material construction. The
  preflight now enumerates shader properties with `ShaderUtil`, preserving the
  required no-write validation behavior.
- The first compiled EditMode run exposed three old `Has.Count` assertions
  that NUnit could not apply to an enumerable. Using LINQ `Count()` made the
  assertions explicit without changing production behavior.

## Decision log

- 2026-08-03: Use strict closed ranges. Reject Blender 5.2.1+, Unity
  6000.4.5f2+, and URP/Shader Graph 17.4.1+.
- 2026-08-03: Treat only the current exact tuple as certified. In-range lower
  versions are allowed with structured warnings.
- 2026-08-03: Add bake request 1.2 carrying the actual Blender numeric version
  and build hash; retain frozen request 1.0/1.1 validation.
- 2026-08-03: Select an explicit Shader Graph adapter per 17.x minor and run
  capability checks before staging or writing assets.
- 2026-08-03: Preserve the existing Shader Graph stable-ID namespace across
  17.0-17.4. Adapter choice changes internal compatibility probing, not public
  identities or generated property references.
- 2026-08-03: Share request/runtime build binding in the license-neutral bake
  protocol so both the GPL worker and ordinary Python tests exercise the same
  mismatch diagnostics.

## Implementation sequence

1. Add pure Blender version classification and integrate it into registration,
   export, UI diagnostics, Time-node creation, and bake execution.
2. Add bake request 1.2 schema/validation and bind requests to the executing
   Blender build.
3. Replace Unity exact checks with deterministic range parsers, diagnostics,
   and Shader Graph 17.0-17.4 adapter selection/preflight.
4. Update coordinated 2.2.9 metadata, compatibility docs, release notes, and
   deterministic build expectations.

## Validation

- Run focused and full Python tests, including JSON Schema tests.
- Build the Blender ZIP twice and compare hashes; validate and run smoke tests
  with the fixed Blender 5.2.0 executable.
- Run Unity EditMode tests on 6000.4.5f1 with URP/Shader Graph 17.4.0.
- Build the Unity TGZ twice and compare hashes.
- Record lower in-range versions as unvalidated because those runtimes are not
  installed in the approved validation environment.

## Results and follow-up

Implementation is complete. Validation evidence:

- `.venv/Scripts/python.exe -m pytest -q`: 258 passed.
- `.venv/Scripts/python.exe -m ruff check miku miku_blender extensions tests
  tools`: passed.
- `tools/ci/run_blender_headless.py --blender
  C:\SteamLibrary\steamapps\common\Blender\blender.exe`: all 8 scripts passed
  under Blender 5.2.0. Blender reported only existing Blender 6.0 deprecation
  warnings for `Material.use_nodes` in smoke fixtures.
- `tools/ci/run_unity_editmode.ps1 -UnityPath
  C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe`: 186 total,
  184 passed, 0 failed, 2 skipped.
- Two independent release builds produced identical manifests and bytes:
  Blender ZIP SHA-256
  `5d4d0d9846cc743e870b621f11e5cd1bce6c65e2e4f8e81a3e8246615ae6a48b`;
  Unity TGZ SHA-256
  `4e3088228e5de37a358ec31cbb71f6a911d3d21e674110356413a473275ed401`.
- Blender extension validation successfully parsed the 2.2.9 ZIP manifest.

Blender 5.0/5.1 and lower Unity 6 / package 17.x versions remain Allowed /
Unvalidated. Their range and capability-probe paths have automated coverage,
but the runtimes were not installed or executed in the fixed validation
environment. No schema or generated-asset migration is needed beyond pairing
the new bake request 1.2 producer with the 2.2.9 worker.
