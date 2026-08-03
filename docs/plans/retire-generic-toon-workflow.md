# Retire the Generic Toon workflow for Miku 2.0

## Purpose and outcome

Miku 2.0 removes the `generic_toon` workflow because its optimization and
validation cost is no longer justified. Standard PBR, Genshin, WuWa, and HSR
remain supported. New exports use MaterialIR 2.0; old Generic Toon inputs fail
with an explicit retirement diagnostic and are never guessed into another
workflow.

## Context and constraints

- Canonical implementation roots are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`.
- The worktree was already dirty before this change. Existing non-Generic Toon
  edits are user-owned and must remain intact.
- Blender validation uses Blender 5.2.0 at the repository-mandated fixed path.
  Unity validation uses 6000.4.5f1 with URP/Shader Graph 17.4.0.
- MaterialIR 1.0 remains a frozen historical/compatibility schema. MaterialIR
  2.0 removes the retired workflow enum while other document schemas remain
  at 1.0.
- Existing user project assets are not deleted or automatically converted.

## Progress

- [x] 2026-08-01: Confirmed the canonical Miku source boundary and inspected the
  dirty worktree.
- [x] 2026-08-01: Confirmed the Generic Toon implementation boundary and its
  shared Game Toon Screen Rim dependencies.
- [x] 2026-08-01: Chose explicit retirement errors, historical retention, a
  coordinated 2.0.0 release, and MaterialIR 2.0 for new exports.
- [x] 2026-08-01: Removed the Core, Blender, CLI, and Unity Generic Toon
  implementation and added explicit retirement diagnostics.
- [x] 2026-08-01: Moved shared Game Toon runtime/editor assets without changing
  their `.meta` GUIDs.
- [x] 2026-08-01: Updated documentation, provenance, schemas, release metadata,
  and package identity inventory.
- [x] 2026-08-01: Ran Python, Blender, Unity, and deterministic build
  validation.

## Discoveries

- `Runtime/GenericToon` contains both the Generic Toon shader family and shared
  Screen Rim assets used by Genshin, WuWa, and HSR. The shared files must move
  before the Generic directory is deleted.
- `MikuToonMaterialRecipe` is used by the game workflows, but its semantic
  preset, albedo, and three-way merge fields are Generic Toon-specific.
- MaterialIR 1.0 is embedded in Bundle 1.0 by an unconstrained document
  reference, so the importer can accept MaterialIR 2.0 while retaining Bundle
  1.0 compatibility.

## Decision log

- Retired Generic Toon inputs fail with `MIKU_WORKFLOW_RETIRED:generic_toon`;
  no automatic Standard PBR conversion is attempted.
- Historical ADRs, release notes, plans, provenance, and the frozen MaterialIR
  1.0 schema remain for traceability and are marked historical where needed.
- Game Screen Rim files move to `Runtime/GameToon`; their `.meta` GUIDs remain
  unchanged, and the runtime feature receives a `MovedFrom` compatibility hint.

## Implementation sequence

1. Add MaterialIR 2.0 and split frozen-schema validation from supported-workflow
   execution checks.
2. Remove Generic Toon from Core, Blender UI/export, CLI choices, and legacy
   migration paths; add explicit retirement diagnostics.
3. Remove Generic Toon Unity shaders/tools/backends, slim the shared recipe, and
   move Game Toon Screen Rim assets and menus.
4. Update package versions, target-profile implementation hashes, provenance,
   active documentation, historical markers, and tests.
5. Run source checks, Python tests, fixed Blender 5.2 smoke tests, Unity EditMode
   tests, package-content checks, and repeated deterministic builds.

## Validation

- `py -3.13 tools/ci/run_checks.py --profile pr`
- Fixed Blender 5.2.0 headless smoke scripts with an assertion on
  `bpy.app.version == (5, 2, 0)`.
- `tools/ci/run_unity_editmode.ps1` with Unity 6000.4.5f1.
- Two builds of the Blender extension ZIP and Unity package compared by file
  manifests and SHA-256.
- `rg` audit of active source roots with an explicit historical/retirement
  allowlist for the remaining old identifier strings.

## Results and follow-up

Implementation results:

- `py -3.13 tools/ci/run_checks.py --profile pr`: passed (227 Python tests,
  schema/package checks, deterministic package builds).
- Fixed Blender 5.2.0 headless smoke scripts: passed for fixed game textures and
  runtime inputs.
- Unity EditMode on Unity 6000.4.5f1: passed, 118 total / 116 passed / 0 failed
  / 2 skipped. The Unity Shader Graph package emitted unrelated third-party
  `GUID` compiler diagnostics during package discovery, but Miku assemblies
  compiled and the test run completed successfully.
- Repeated artifacts were byte-stable: Blender ZIP SHA-256
  `69ff97afc8d2df6ddc833348051111c763731f01bf84e9b0b1e8d64f653b47d8`; Unity
  TGZ SHA-256 `8ed5846020ebd0de12c6693d3dce9a8b0130580e4f9b546c523484e31314ed36`.
- Remaining `generic_toon`/Generic Toon strings are limited to retirement
  diagnostics, old-input recognition, `MovedFrom`, historical documentation,
  frozen schema/history, and migration guidance. Existing user project assets
  are intentionally not modified; manual reassignment remains required.
