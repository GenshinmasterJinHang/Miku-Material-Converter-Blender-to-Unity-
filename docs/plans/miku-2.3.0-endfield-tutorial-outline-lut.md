# Miku 2.3.0 Endfield tutorial lighting, outline, and LUT delivery

> **2026-08-13 supersession:** the validation-only hair-shadow menu and its
> dedicated diagnostic test recorded in this historical plan were removed from
> the distributable 3.0 package. The completed execution record is preserved.

> **2026-08-12 correction:** the selected cloth texture discussed in this plan
> is a Body/Cloth dark-color material LUT, not a screen-grading LUT. Installing
> it full-screen was an invalid validation setup. The replacement implementation
> and PMX EyeHL evidence are recorded in
> `docs/plans/miku-3.0.0-endfield-reference-fidelity.md`.

This ExecPlan is a living implementation record and follows `PLANS.md`.

## Purpose and outcome

Complete the in-scope Endfield character rendering described by the supplied
tutorial, repair discontinuous smooth-normal outlines in all four fixed game
families, and install a game-authored full-screen LUT before URP post processing.
The observable result is Miku Unity package 2.3.0 plus, after the separate
project acceptance step succeeds, an upgraded `Assets/endfield/终末地.unity`
in the Unity 6000.4.5f1 project served on port 8080. Package implementation
and isolated validation do not by themselves prove that scene migration or its
visual result. Fur shells and fire remain out of scope.

## Context and constraints

- Canonical implementation roots are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`. PackageCache, installed Blender
  extensions, validation assets, and `dist/` archives are never source.
- The validation tuple is Windows D3D11, Unity 6000.4.5f1, URP and Shader Graph
  17.4.0. The live MCP instance is the project served on port 8080.
- The worktree starts with only the unrelated untracked `vibe-kanban/`; preserve
  it and all unrelated user files.
- Existing shader names and property references remain compatible. MaterialIR,
  Bundle, and Blender interchange schemas do not change. New fixed-workflow
  roles and runtime controls are additive.
- Game textures, models, materials, scenes, and the selected LUT remain local
  validation assets and must not enter the open-source release archive.
- Existing source meshes, 2.2.2/2.2.4 materials, and old smooth-normal assets
  are not overwritten. Generated mesh output is a new UV7-v2 clone.

## Progress

- [x] 2026-08-09: Read repository instructions, Unity MCP skill, `PLANS.md`,
  relevant prior plans/ADRs, architecture ownership rules, and release process.
- [x] 2026-08-09: Confirm canonical markers, clean tracked worktree, package
  identity, port-8080 project tuple, renderer/scene/LUT GUIDs, and 2.2.12 baseline.
- [x] 2026-08-09: Implement TangentSpaceV2 smooth normals and shared
  four-family outline.
- [x] 2026-08-09: Implement Endfield shared day/top/three-layer lighting and part-specific
  Body, Skin, Face, Eye, Hair, Overlay, and auxiliary-shadow behavior.
- [x] 2026-08-09: Implement full-screen LUT, Endfield volume setup, nine-part workflow
  invariants, and structured diagnostics.
- [x] 2026-08-12: Repair the port-8080 Endfield Renderer so the game LUT is
  actually referenced before post processing; add fail-closed preflight and
  post-import persistence validation, exact late-failure rollback, and LUT
  importer Undo and dirty-target protection (15/15 focused EditMode tests
  passed on D3D11).
- [x] 2026-08-09: Coordinate version 2.3.0, identities, documentation, ADRs,
  changelog, and the tutorial-completeness audit.
- [ ] Run Python, Blender, Unity, shader, visual, determinism, and package
  checks. Python, Blender 5.2.0, deterministic archives, and focused isolated
  Unity fixtures are complete; final-TGZ 8080 tests and D3D11 visual checks are
  pending.
- [ ] Install the canonical TGZ into the 8080 project and transactionally migrate
  the target scene with baseline/rollback evidence.
- [ ] Complete final 8080 visual self-review and promote audit scores only from
  retained measurements/screenshots. Package-source self-review and this
  evidence split are complete.

## Discoveries

- The active 8080 scene is the clean `SampleScene`, not the target. The newest
  Endfield scene is `Assets/endfield/终末地.unity`; it binds 2.2.2 materials,
  SMAA High, HDR post processing, and a broad manually graded package volume.
- `PC_Renderer` contains SSAO and screen-rim features but no LUT feature. The
  selected cloth LUT is a 1024 by 32 sRGB flattened 32-cube and is currently
  referenced only by character materials.
- The current generated Jie Ge UV7 is numerically equal to source normals. The
  generator neither aligns accumulated triangle normals to source-normal
  hemispheres nor stores a tangent-space/skinning-safe version marker.
- Thirteen outline consumers repeated the unsafe selector shape. Their concrete
  passes wrote depth and extruded in world space. Genshin/Endfield consumed
  vertex alpha despite the public mask contract assigning outline width to
  green; Wuwa and HSR historically did not consume a vertex-color width mask.
- Endfield Body defaults to back-face culling and shared fragment input lacks a
  front-face semantic. Hidden `_Cull` and `_PartMode` values can survive shader
  changes because the recipe does not reset every part invariant.
- Version 2.2.12 has working approximations for all major parts but lacks the
  tutorial's shared day state, top light, three-layer diffuse, NoF/backlight
  shaping, camera-forward specular, separated rims, and several part-specific
  masks. Those approximations do not count as completed tutorial rows.

## Decision log

- 2026-08-12: A same-named Renderer subasset is not installation evidence.
  Validate the live Feature/local-ID map before writes and the force-reloaded
  Renderer reference, configuration, and material before reporting success.
- 2026-08-09: Release the expanded public runtime and shader behavior as Unity
  package 2.3.0. Keep interchange schema versions unchanged because the changes
  are additive fixed-workflow/runtime contracts.
- 2026-08-09: Preserve exact legacy visuals when no
  `MikuEndfieldLightingController` is active. The target scene explicitly opts
  into tutorial lighting through the controller and cloned 2.3.0 materials.
- 2026-08-09: Extend the existing Overlay shader with an opt-in lit-transparent
  mode instead of adding a tenth material part. Overlay's public
  `_LightingMode` remains `0=LegacyUnlit` by default and must be set to
  `1=ToonLitTransparent` before it enters the lit path; the controller then
  selects tutorial rather than legacy lighting inside that path. This preserves
  the nine-part public workflow and existing EyeShadow materials.
- 2026-08-09: Use URP reflection probes/skybox as the environment input and add
  the documented DFG/multiple-scattering response; do not add a duplicate game
  cubemap property.
- 2026-08-09: Install color grading with URP's built-in full-screen feature
  before post processing. Do not use the ColorLookup Volume override and do not
  package the game LUT.
- 2026-08-09: Update the authoritative target scene only after a baseline copy
  and transaction manifest exist. Old materials and meshes remain recoverable.

## Implementation sequence

1. Add shared outline selection/extrusion helpers, UV7-v2 generation and
   diagnostics, migrate all thirteen consumers, and add deterministic mesh tests.
2. Add the Endfield lighting controller and compatible shared lighting context;
   implement the common day/top/diffuse/specular/rim/emission functions before
   integrating each public part.
3. Complete Body back-face handling and conditional cloth paths, then Skin,
   Face, Eye, Hair, Overlay, HairShadow, and EyeShadow behavior and tests.
4. Add the LUT shader/installer/profile factory and fixed-workflow role/part
   validation. Make setup idempotent, Undo-aware, and rollback-safe.
5. Update version, profile hashes, package identity, ADRs, compatibility,
   diagnostics, English/Chinese documentation, changelog, and completeness audit.
6. Run focused tests, then full Python/Blender/Unity checks. Build twice and
   compare archive bytes, manifests, and SHA-256 values.
7. Pin the 8080 instance, capture the target baseline, install the TGZ, wait for
   compilation, migrate cloned assets and renderer/profile state, run tests and
   D3D11 image regressions, then save only after acceptance checks pass.

## Validation

- `python tools/ci/run_checks.py --profile pr`
- `python -m ruff check miku miku_blender extensions tests tools`
- `python tools/miku_package_identity.py --check`
- `python tools/ci/run_blender_headless.py --blender
  C:\\SteamLibrary\\steamapps\\common\\Blender\\blender.exe`
- Full Unity EditMode suite after final TGZ installation on the 8080 project;
  the pre-2.3.0 baseline was 218 total, 216 passed, zero failed, two skipped;
  record the new discovered total instead of assuming it is unchanged.
- Four-family shader compilation and D3D11 screenshots; static and dual-bone
  outline views must show both sides, gaps no larger than four pixels, and left/
  right median width difference no larger than one pixel at 1080p.
- Double-sided plane front/back luminance difference no larger than two percent
  with finite complementary normal debug output.
- LUT strength zero differs from a disabled feature by at most one 8-bit code;
  strength one changes midtones while HDR emission still produces Bloom.
- Two independent release builds must have identical normalized manifests and
  SHA-256 outputs, and installed package hashes must match canonical source.

## Results and follow-up

Executed package-source and isolated validation on 2026-08-09:

- `.venv\Scripts\python.exe tools\ci\run_checks.py --profile pr` passed all
  262 Python tests. Ruff passed, and the package-identity check passed 13/13.
- `tools\ci\run_blender_headless.py` ran through the repository-fixed
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe` with expected version
  5.2.0 and passed 8/8 public smoke scripts.
- `.venv\Scripts\python.exe tools\ci\run_blender_release_smoke.py --blender
  C:\SteamLibrary\steamapps\common\Blender\blender.exe --expected-version
  5.2.0 --extension-zip dist\miku_shader_converter-2.3.0.zip --evidence
  artifacts\miku-2.3.0-blender-release-smoke.json` completed with
  `MIKU_INSTALLED_COMPATIBILITY_SMOKE_OK`. The ZIP SHA-256 is
  `ffaab6eb72fd184795fbc03d490a2897a13089240827ee146a94650eb4b0da13`;
  the installed-tree and normalized-IR hashes are retained in the JSON evidence.
- Two independent Unity package builds were byte-identical. The final
  `com.miku.shaderconverter-2.3.0.tgz` is 440406 bytes with SHA-256
  `515d63aee227e905b61496a107ebb5227a8cce27f708484b5ca39611d5c17903`.
- A source-linked isolated Unity 6000.4.5f1 / URP 17.4.0 project passed 53/53
  focused EditMode tests: `MikuEndfieldTutorialLightingTests` 8/8,
  `MikuEndfieldHairShadowDiagnosticTests` 3/3,
  `MikuGameToonOutlineTests` 14/14, and `MikuGameToonTests` 28/28. The retained
  XML files are under
  `%LOCALAPPDATA%\Temp\MikuEndfieldValidation-019fe493\TestResults-*.xml`.
  These fixtures include ShaderUtil compilation of the changed Endfield and
  four-family outline shaders, but they are not target-scene screenshots.

Still pending, and therefore not claimed as passed: installation and the full
EditMode suite from the final TGZ in the port-8080 project; transactional
migration/save of `Assets/endfield/终末地.unity`; final material/renderer/Volume
audit; D3D11 static and skinned outline measurements; front/back luminance and
finite-normal render checks; LUT-zero/LUT-one/Bloom image comparisons; and
retained baseline/rollback screenshots. The port-8080 attempt found a test-
assembly `Object` alias compile error; source and the deterministic TGZ were
rebuilt, but the final install/test/scene acceptance remains pending at this
documentation checkpoint. No audit row is promoted to Unity-scene-complete on
the strength of isolated or static evidence alone.
