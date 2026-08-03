# Endfield 2.2.4 Hair, Rim, Metal, and Skin Fidelity

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan follows `PLANS.md` at the repository root.

## Purpose / Big Picture

Endfield character materials currently lose the neutral hair highlight encoded in a red-only lookup texture, have little readable silhouette lighting, understate metallic direct and environment response, and render skin darker than the intended Endfield reference. After this work, package version 2.2.4 will expose compatible opt-in controls for those four areas, ship calibrated 2.2.4 materials in the validation project, and preserve all existing 2.2.2/2.2.3 assets and the original `Assets/Scenes/1.unity` scene.

The visual target is stylistic rather than character-identical: restrained strand highlights, a thin continuous rim, readable metal reflections, and warm pale skin. Success is demonstrated through automated shader/math tests, deterministic package evidence, Unity compilation and EditMode tests, a new isolated validation scene, screenshots, and numeric image checks.

## Progress

- [x] (2026-08-02) Read repository instructions, `PLANS.md`, package metadata, relevant shader/math/tests, and inspect the dirty worktree without modifying unrelated user work.
- [x] (2026-08-02) Confirm canonical Miku roots and package identity; confirm Unity 6000.4.5f1 with URP/Shader Graph 17.4.0 in `C:\Users\22687\Desktop\unity\test`.
- [x] (2026-08-02) Inspect the existing 2.2.3 material group, source scene, screen-space rim renderer feature, and relevant texture channel distributions.
- [x] (2026-08-02) Implement compatible HLSL controls and matching CPU reference math.
- [ ] Add and execute focused EditMode coverage for LUT mode, surface rim, metal boosts, skin grading, property exposure, and shader compilation. Tests are implemented; Unity execution remains.
- [x] (2026-08-02) Update package version, recipe version, changelogs, compatibility/provenance documentation, release notes, and package identity tests to 2.2.4.
- [x] (2026-08-02) Run the Python PR profile and deterministic package build twice; 228 tests passed, both 190-entry manifests matched, and both archives had SHA-256 `27500e95d1650d5cf4d947458962fd475ae29e8c0126aca3cff58f394162c43f`.
- [ ] Install the canonical 2.2.4 archive into the Unity validation project and verify the PackageCache copy against the archive/canonical package.
- [ ] Create `杰哥_2.2.4` materials and `Assets/endfield/Validation/2.2.4/Endfield_2.2.4.unity`, add and bake the Reflection Probe, and preserve previous assets byte-for-byte where required.
- [ ] Run Unity EditMode tests, shader/Console checks, capture the requested views, and calculate acceptance metrics.
- [ ] Review the final diff and record exact results and limitations.

## Surprises & Discoveries

- Observation: `T_actor_common_hairst_01_ST` stores useful highlight weights almost exclusively in the red channel; green is near zero and blue is zero. Sampling it as RGB explains the colored/dim highlight without requiring a texture replacement.
  Evidence: inspection of the validation texture showed a useful red range while the other two color channels contain essentially no signal.

- Observation: the screen-space rim renderer feature is already installed in `PC_Renderer`, so 2.2.4 should augment it with a bounded material-space rim rather than replace the renderer feature.
  Evidence: Unity renderer data inspection in the validation project.

- Observation: the source scene currently binds the 2.2.2 material GUIDs, while a complete 2.2.3 material group already exists. The 2.2.4 validation scene must therefore be a new copy and must not be saved over the source scene.
  Evidence: scene YAML and material GUID inspection during preflight.

## Decision Log

- Decision: Keep all new shader properties at compatibility-neutral defaults and opt into the stronger look only in the new 2.2.4 material group.
  Rationale: existing materials must continue importing and rendering without missing-property failures or forced visual changes.
  Date/Author: 2026-08-02 / Codex

- Decision: Treat the red-only hair LUT as either authored RGB or scalar red through an explicit enum property; do not infer the mode from texture contents.
  Rationale: deterministic author intent is safer than asset-dependent heuristics and preserves old materials by default.
  Date/Author: 2026-08-02 / Codex

- Decision: Retain existing legacy complexion shaping, then apply the new neutral skin-tone grading helper before face emotion/blush overlays.
  Rationale: neutral parameters preserve the established shader result while tuned parameters brighten skin without washing out authored facial overlays.
  Date/Author: 2026-08-02 / Codex

- Decision: Use the project-owned CPU math mirror for exact behavioral unit tests and use Unity shader compilation plus rendered validation for GPU integration evidence.
  Rationale: ShaderLab/HLSL helper internals are not directly invocable from EditMode tests, but the mirror provides deterministic edge-case tests and rendering validates integration.
  Date/Author: 2026-08-02 / Codex

## Outcomes & Retrospective

Not complete. This section will be updated after implementation and validation.

## Context and Orientation

The canonical Unity package is `unity/Packages/com.miku.shaderconverter/`. Endfield shared lighting resides in `Runtime/Endfield/EndfieldCommon.hlsl`; the six character shaders are beside it. Editor-side reference math is in `Editor/MikuEndfieldShaderMath.cs`, and its focused EditMode coverage is in `Tests/Editor/MikuGameToonTests.cs`.

The external validation project is `C:\Users\22687\Desktop\unity\test`. It consumes the deterministic archive under this repository's `dist/` directory. Existing material groups and `Assets/Scenes/1.unity` are user validation evidence and must not be overwritten. The new material group will live under `Assets/endfield/Materials/杰哥_2.2.4`; the new scene will live under `Assets/endfield/Validation/2.2.4`.

## Plan of Work

First, extend the shared Endfield constant buffer and implement compact helpers for hair LUT selection, surface rim evaluation, skin-tone grading, and bounded metal boosts. Integrate them only into the relevant body, skin, face, and hair evaluation paths. Expose matching properties in the four shaders with neutral defaults. Preserve existing texture roles and the MaterialIR 2.0 schema.

Second, extend `MikuEndfieldShaderMath` with CPU equivalents and add EditMode tests that cover neutral and calibrated behavior, numeric bounds, property presence, and compilation of all six Endfield shaders.

Third, update all package/public version surfaces to 2.2.4 and document exact compatibility, behavior, provenance, and release details. Execute the repository's Python PR profile and Unity EditMode tests before packaging.

Fourth, build the archive twice and compare normalized file manifests and SHA-256 output. Install only the canonical archive into the external Unity project. Verify that the installed PackageCache manifest and hashes match the built package rather than patching PackageCache directly.

Finally, duplicate the 2.2.3 material group into a 2.2.4 group through Unity asset APIs, apply the approved calibration values, copy the source scene to the new validation path, bind only the new materials, add a 128-resolution baked box-projected Reflection Probe at intensity 1.3, and bake it. Capture full-body and close-up evidence plus an environment-rotation comparison. Read back an ARGBHalf render target and calculate the specified hair, rim, metal, skin, finite-value, peak, and clipping checks.

## Concrete Steps

Run repository commands from `C:\Users\22687\Desktop\项目4` and Unity validation actions against `C:\Users\22687\Desktop\unity\test`.

1. Patch the shared HLSL, shader properties, CPU math mirror, and EditMode tests.
2. Run the focused Python/package tests and Unity EditMode test assembly while the source package is linked or after the deterministic archive is installed.
3. Change all 2.2.3 release surfaces that describe the current package to 2.2.4; add release notes rather than erasing historical 2.2.3 notes.
4. Run the full Python PR profile specified by repository tooling.
5. Build `dist/com.miku.shaderconverter-2.2.4.tgz` twice, storing and comparing each manifest and SHA-256 before retaining the final deterministic artifact.
6. Install the archive with Unity package tooling, wait for compilation, run EditMode and shader checks, and verify PackageCache hashes.
7. Use Unity asset/scene APIs to create and calibrate the new material group and scene, bake the probe, render evidence, and compute metrics.
8. Verify hashes for `Assets/Scenes/1.unity` and existing 2.2.2/2.2.3 material assets did not change.

## Validation and Acceptance

Automated acceptance requires the package identity tests, full Python PR profile, focused and complete Unity EditMode suite, six Endfield shader compile checks, and a clean Unity Console after import and rendering. The package archive must be reproducible across two builds, and installed content must match the canonical archive/source manifest.

Visual acceptance uses the approved same-camera scene and the following measurements: neutral RGB hair highlight from the scalar-red LUT with a peak at least 1.35 times adjacent diffuse hair; a continuous approximately 1–2 pixel rim without obvious interior seam halos; visible direct metal highlight and a measurable metal ROI change after 90-degree environment rotation; Skin/Face median sRGB luminance from 0.72 through 0.88 with a warm-pale ordering; and an ARGBHalf scan containing no NaN/Infinity, absolute channel peak no greater than 4, and fewer than 1 percent clipped RGB pixels.

## Idempotence and Recovery

All source edits are ordinary patches and can be rerun safely after inspecting the worktree. Package builds write a versioned archive and are repeated deliberately. Unity assets use new 2.2.4 paths, so retries must overwrite only those newly owned paths after verifying them explicitly; never delete or rewrite the source scene or old material groups. Before any scene mutation, record the source scene hash. If validation fails midway, reopen the new scene and rerun only its deterministic setup/bake routine.

## Artifacts and Notes

Expected repository artifact: `dist/com.miku.shaderconverter-2.2.4.tgz` plus updated provenance/release documentation.

Expected validation artifacts: the `杰哥_2.2.4` material group, `Assets/endfield/Validation/2.2.4/Endfield_2.2.4.unity`, baked probe data, screenshots, and a machine-readable metric report.

Repository validation evidence to date:

    python tools/ci/run_checks.py --profile pr
    Ran 228 tests ... OK

    first SHA-256  27500e95d1650d5cf4d947458962fd475ae29e8c0126aca3cff58f394162c43f
    second SHA-256 27500e95d1650d5cf4d947458962fd475ae29e8c0126aca3cff58f394162c43f
    package entries 190; manifest differences 0

## Interfaces and Dependencies

No production dependency is added. MaterialIR remains schema version 2.0. The public shader properties added in 2.2.4 are `_SurfaceRimStrength`, `_SurfaceRimPower`, `_SurfaceRimLightAlign`, `_MetalDirectBoost`, `_MetalEnvironmentBoost`, `_SkinToneBrightness`, `_SkinToneWhitening`, `_SkinToneTarget`, and `_HairSpecularLutMode`. Existing texture roles, shader names, material slot ordering, and prior property meanings remain unchanged.
