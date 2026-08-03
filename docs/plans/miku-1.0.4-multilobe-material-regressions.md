# Miku 1.0.4 Multi-Lobe Material Regression Fixes

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be updated as implementation proceeds.

This plan follows `PLANS.md` at the repository root.

## Purpose / Big Picture

Miku 1.0.3 can export the three supplied multi-closure materials without losing their closure expressions or baked resources, but the Unity runtime backend composes them incorrectly. After this work, an existing 1.0.3 bundle can be imported by Miku 1.0.4 without re-export: evaluated closure radiance feeds Shader Graph Emission instead of Base Color, each lighting lobe uses its own normal expression, Blender's unconnected zero normal sentinel is treated as the tangent-space neutral normal, and the importer reports the legacy in-memory migration. The original directory `C:\Users\22687\Desktop\output` remains untouched; any regenerated evidence is written to `output-fixed-1.0.4`.

The observable acceptance result is that 彩色镀层5 no longer becomes black or receives a second lighting pass, 彩色镀层8 retains its gray linear EXR while its blue emission and view-dependent blend remain dynamic, and 凹凸石3 lights from its baked OpenGL tangent normal in both diffuse and glossy lobes.

## Progress

- [x] (2026-08-01) Read the repository constitution, `PLANS.md`, current architecture/version documents, relevant source and existing tests.
- [x] (2026-08-01) Preflighted the canonical Miku source boundary and confirmed package ID `com.miku.shaderconverter`.
- [x] (2026-08-01) Inspected the dirty worktree and treated all pre-existing changes as user-owned.
- [x] (2026-08-01) Diagnosed all three supplied bundles under `C:\Users\22687\Desktop\output` and recorded their shared backend causes.
- [x] (2026-08-01) Implemented and tested unconnected closure Normal/Coat Normal sentinel normalization in Core.
- [x] (2026-08-01) Separated geometry, compatibility, and per-lobe normals in the Unity Shader Graph backend.
- [x] (2026-08-01) Routed evaluated closure radiance to Emission with zero Base Color.
- [x] (2026-08-01) Added importer defaults and diagnosed legacy 1.0.3 zero-normal migration.
- [x] (2026-08-01) Coordinated the 1.0.4 version, component hashes, and target-profile compatibility.
- [x] (2026-08-01) Ran focused Core, Blender, Unity EditMode, old-bundle import, and determinism validation. Exact-material D3D11 visual captures remain unexecuted for the reason recorded below.
- [x] (2026-08-01) Reviewed the final diff and recorded outcomes and remaining limitations.

## Surprises & Discoveries

- The gray EXR in 彩色镀层8 is not a failed color bake. It is the correct static grayscale result of the source Color Ramp/HSV island. The blue color, strength 11.2, and view-dependent weighting remain runtime closure expressions.
- The 凹凸石3 normal PNG is valid tangent-space OpenGL (+Y) data and is referenced by both exported lobes. The defect occurs because the Unity backend supplies one top-level compatibility normal to every lighting evaluation.
- `MikuEvaluateLobe` already returns fully evaluated URP radiance, including direct and indirect lighting. Connecting that result to Base Color necessarily asks URP to light it again.
- 彩色镀层5 contains an unconnected Blender closure Normal represented as `[0,0,0]`. The compatibility normal blend can therefore collapse to zero even though that socket semantically means the unmodified surface normal.
- Exact historical source `.blend` files for the three supplied bundles are not all present in the canonical workspace. A backup source library exists outside the repository and matches the bundle semantics except for unstable source node identities; old-bundle import is therefore the authoritative compatibility test.

## Decision Log

- Decision: Normalize only unconnected `Normal` and `Coat Normal` constant zero vectors while constructing closure IR. Explicit links, including an explicit zero-producing expression, are preserved.
  Rationale: Blender's unconnected socket default is a sentinel for the surface normal; silently rewriting an authored expression would change user semantics.
  Date/Author: 2026-08-01 / Codex

- Decision: Treat `Input.Normal` in expression evaluation as the geometric normal in its declared coordinate space. Do not substitute the aggregate compatibility normal.
  Rationale: Fresnel, Layer Weight, and Bump expressions must not recursively consume a normal derived from the same closure aggregate.
  Date/Author: 2026-08-01 / Codex

- Decision: Resolve each surface lobe's `Normal` parameter independently and transform tangent/object normals to world space at the lighting boundary.
  Rationale: Normal ownership belongs to the closure leaf. The top-level normal remains only a compatibility/output channel.
  Date/Author: 2026-08-01 / Codex

- Decision: Emit evaluated closure radiance through Shader Graph Emission and emit zero Base Color. Preserve wrapper-owned Clear Coat outputs.
  Rationale: This prevents a second URP lighting pass while retaining the existing wrapper contract.
  Date/Author: 2026-08-01 / Codex

- Decision: Preserve schema version `1.0`, public property references, package ID, and wrapper ownership. Add only version/profile compatibility and a structured migration diagnostic.
  Rationale: The correction changes backend interpretation, not the public interchange shape.
  Date/Author: 2026-08-01 / Codex

## Context and Orientation

Core closure extraction lives in `miku/closure_ir.py`; `_parameter_inputs` distinguishes unconnected constants from linked value expressions. Unity graph generation lives in `unity/Packages/com.miku.shaderconverter/Editor/MikuShaderGraph17RuntimeBackend.cs`; `BuildClosureComposite`, `EvaluateLobe`, and `BuildExpression` control closure composition, lobe lighting, and `Input.Normal`. `unity/Packages/com.miku.shaderconverter/Editor/MikuBundleImporter.cs` owns bundle/profile validation, in-memory compatibility migration, texture import settings, and wrapper material defaults. Target-profile component identities are declared in `miku/planner.py` and consumed by the importer.

Relevant tests are in `tests/test_miku_closure_weights.py`, Blender headless fixtures under `tests/blender`, and Unity EditMode tests under `unity/Packages/com.miku.shaderconverter/Tests/Editor`. The supplied 1.0.3 bundles are external immutable fixtures at `C:\Users\22687\Desktop\output`.

The worktree was already dirty before this plan. Implementation must make narrow edits, inspect overlapping diffs before patching, and never revert unrelated user work.

## Plan of Work

First, add a narrow Core canonicalization helper used only by the unconnected-parameter branch. Add regression tests proving unconnected zero Normal and Coat Normal become `[0,0,1]`, linked values remain expressions, closure weights do not change, and repeated export is deterministic.

Second, refactor the Unity runtime backend so graph expressions always see the geometric normal, while top-level surface normal remains a separate compatibility output. Resolve a lobe Normal from its own parameter record and transform its declared Tangent or Object space to World before connecting `MikuEvaluateLobe.NormalWS`. Use the geometric normal for a missing or neutral normal. Route final radiance to Emission and a literal zero to Base Color.

Third, update the bundle importer. Evaluated-radiance materials receive white emission color and strength 1; non-authoritative top-level Emission constants/textures are ignored. Before graph generation, old 1.0.3 closure constant zero normals are normalized in memory and produce `MIKU_LEGACY_CLOSURE_ZERO_NORMAL_NORMALIZED`. Keep existing linear EXR and OpenGL normal importer behavior unchanged and cover it with regression tests.

Fourth, coordinate all active package/tool versions to 1.0.4. Recalculate the runtime backend component digest and resulting current target-profile hash. Preserve `b9e8f39f…` as an explicitly supported 1.0.3 profile so existing bundles import without regeneration.

Finally, run focused Python tests, fixed Blender 5.2.0 headless tests, Unity EditMode tests, graphics validation when a D3D11 device is available, PR checks, and two deterministic package builds. If Blender GUI is running, do not overwrite its installed extension; use source/headless validation or report the installation-dependent test as blocked. Never write into the original `output` directory.

## Concrete Steps

Run commands from `C:\Users\22687\Desktop\项目4`.

1. Add Core regression tests, implement canonicalization, then run the focused Python test module.
2. Add Unity graph/importer regression tests and implement the backend/importer changes.
3. Update versions and component hashes; print the canonical target profile and copy its hash into the importer.
4. Run repository PR checks and the Unity EditMode runner.
5. Confirm `C:\SteamLibrary\steamapps\common\Blender\blender.exe` reports exactly `(5, 2, 0)` and run the relevant headless exporter/smoke coverage.
6. Build the Blender and Unity packages twice and compare manifests/SHA-256 hashes.
7. If safe and source evidence permits, export regenerated bundles to `C:\Users\22687\Desktop\output-fixed-1.0.4`; always import-test the original 1.0.3 bundles with the new Unity package.

## Validation and Acceptance

Core acceptance requires tests that distinguish unconnected constants from explicit connections and demonstrate stable identities/weights. Unity structural acceptance requires parsed graph evidence for Radiance-to-Emission, zero Base Color, and distinct per-lobe `NormalWS` sources. Importer acceptance requires the legacy profile and diagnostic, linear default EXR settings, unflipped OpenGL normal settings, and Source Mesh prefab/material-slot binding.

Visual acceptance requires a real D3D11 Unity rendering device: 彩色镀层5 has no unlit white self-emission and no double lighting, 彩色镀层8 shows dynamic blue emission/view response on its Source Mesh UVs while retaining its gray intermediate EXR, and 凹凸石3 differs clearly and directionally from a flat-normal baseline. A batch run using Unity's Null graphics device cannot satisfy this visual criterion and must be reported separately.

## Idempotence and Recovery

All source edits are incremental and reviewable. Package and bundle outputs use separate destinations, so reruns do not damage the supplied fixtures. Deterministic build commands may be repeated; their hashes must match. If a Unity or Blender process is interrupted, remove only the explicitly created temporary/build directory after resolving its absolute path. Do not reset or clean the dirty repository.

## Artifacts and Notes

Authoritative external fixtures:

- `C:\Users\22687\Desktop\output\彩色镀层5__70dcd51d8b5b`
- `C:\Users\22687\Desktop\output\彩色镀层8__576e51791e32`
- `C:\Users\22687\Desktop\output\凹凸石3__b4c02f01f6e4`

Historical supported target-profile hash:

- Miku 1.0.3: `b9e8f39f…` (the complete value remains in the importer and supplied manifests)

## Interfaces and Dependencies

No new production dependency is planned. Material IR, bundle plan/manifest, and schema versions remain `1.0`. The Unity package remains `com.miku.shaderconverter`; generated Shader Graph property references and user-owned wrappers remain unchanged. The only new externally visible behavior is Miku 1.0.4 identity, the new current target-profile hash, and the legacy migration diagnostic.

## Outcomes & Retrospective

Miku 1.0.4 now preserves the intended closure ownership boundary. Core changes only unconnected zero Normal/Coat Normal constants. The Unity backend evaluates each surface term with its own declared-space normal, keeps expression `Input.Normal` geometric, and supplies fully evaluated radiance through Emission with zero Base Color. The importer supplies a white strength-1 emission multiplier and migrates supported 1.0.3 closure zero normals only in memory with the new diagnostic.

The final runtime backend SHA-256 is `e3b5b979593d5e8eab75d547159f04bca4fe5cd4253b148aef504dfea447945e`. The Miku 1.0.4 target-profile hash is `c8450c824a8e8b75d1c979cba68cfbe9573747116dce60d553e67b3eda7e06e4`; the complete Miku 1.0.3 hash `b9e8f39f08ed1d76da8e6af18ae58e14ea84cc05a009a0b7d4479978d629841b` remains explicitly supported.

Validation evidence:

- Focused Core closure tests: 22 passed.
- Python suite excluding two NumPy-dependent modules: 206 passed; the two excluded modules were run with the bundled Python/NumPy/Pillow runtime and contributed 19 additional passing tests.
- Blender `C:\SteamLibrary\steamapps\common\Blender\blender.exe`: asserted 5.2.0 and passed the expanded closure surface smoke, including three Principled lobes, dynamic blue emission at strength 11.2, and one shared Bump normal expression used by Diffuse and Glossy.
- Unity full EditMode discovery compiled and executed 129 tests. The task-specific external 1.0.3 bundle test passed. Four unrelated Generic Toon camera graphics cases failed in the batch environment; one temporary test-parameter mistake was corrected afterward.
- Unity filtered `MikuBundleImporterTests` after correction: 113 total, 112 passed, 0 failed, 1 graphics-only test skipped under the Null device. The external test directly imported 彩色镀层5, 彩色镀层8, and 凹凸石3, verified the migration diagnostic where applicable, linear Default EXR, unflipped OpenGL Normal Map, Source Mesh Prefabs, and bound material slots.
- Focused Radiance routing cases for TransparentEmission, TransparentLit, and CustomMultiLobe: 3 passed, including parsed zero Base Color and Radiance-to-Emission edges.
- Package identity check passed. Two consecutive builds were byte-stable: Blender ZIP `0cf377655507ea3e19edf2b773aa3d7f857c25be096e2fca3d2d3cdf5ca7967e`; Unity TGZ `898de30959437e97057e075918fdd13f2b3ea5f1f4072b1d21d80a957bdd3970`.
- `git diff --check` passed, with only line-ending conversion warnings on pre-existing mixed-EOL files.

Exact-material D3D11 image acceptance and regenerated `output-fixed-1.0.4` were not performed. A Blender GUI process was open with an unsaved file (`* 贝拉 ... Blender 5.2.0 LTS`), so the repository constitution prohibited replacing the installed extension. In addition, the exact locked source blends for all three supplied bundles are not present in the canonical workspace. The immutable original directory `C:\Users\22687\Desktop\output` was not modified. These are validation limitations, not remaining implementation work.

Post-release correction (2026-08-01): subsequent real D3D11 ARGBFloat testing
showed that Colorful Coating 5 produced non-finite radiance and Bumpy Stone 3
produced finite zero radiance. The 1.0.4 structural fixes and compatibility
claims remain accurate, but its visual acceptance was incomplete. Miku 1.0.5
supersedes the lighting evaluator and adds finite-pixel graphics acceptance.
