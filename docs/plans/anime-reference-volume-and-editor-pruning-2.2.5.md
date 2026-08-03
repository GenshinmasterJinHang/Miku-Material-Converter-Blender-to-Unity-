# Miku 2.2.5 reference-grade Volume and Mesh menu pruning

## Purpose and outcome

Remove the unused Vertex Color Initializer and Combined Mesh Data menu entries
while preserving their public mesh APIs, and expand the shared anime Volume
Profile from three overrides to a deterministic ten-component URP 17.4 grading
stack inspired by the supplied Endfield game screenshot.

## Context and constraints

- Canonical source is `unity/Packages/com.miku.shaderconverter/`.
- Validation target is Unity 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0.
- The worktree contains unrelated uncommitted Miku 2.2.5 changes; this plan
  only owns the menu registrations, Volume factory/profile, related tests, and
  documentation of the new visual behavior.
- The shared profile keeps its existing GUID and is referenced by four saved
  validation scenes. No scene, camera, light, model, schema, or shader property
  changes are authorized.
- The supplied screenshot is a visual reference only and must not be copied
  into the repository.

## Progress

- [x] 2026-08-03: Confirm canonical source markers, package identity, dirty
  worktree, Unity validation project, and URP 17.4 Volume API surface.
- [x] 2026-08-03: Confirm the two requested Mesh entries are only menu-facing
  entry points; retain the underlying public vertex-color APIs.
- [x] 2026-08-03: Remove the two menu registrations and extend the Volume
  factory/profile through Unity's public Volume API; retain the profile GUID.
- [x] 2026-08-03: Add EditMode coverage for component values, curves, menu
  registrations,
  and semantic rebuild stability.
- [x] 2026-08-03: Update package/release documentation and changelog entries.
- [x] 2026-08-03: Build/install the package and validate Unity compilation,
  tests, deterministic package output, and four scene captures.

## Discoveries

- `MikuToonMeshDataTool.cs` contains the two requested menu attributes, while
  runtime shaders still consume authored vertex colors and public helper APIs
  are not referenced elsewhere by the current package.
- URP 17.4 exposes WhiteBalance, ChannelMixer, LiftGammaGain,
  ShadowsMidtonesHighlights, SplitToning, ColorCurves, ColorAdjustments, Bloom,
  Vignette, and Tonemapping. The first seven grading components are assembled
  into the internal color-grading LUT.
- The supplied reference image is brighter and more saturated than the current
  dark studio captures; the shared stack therefore uses controlled exposure,
  lifted midtones, cool shadows, warm highlights, and restrained bloom rather
  than camera-dependent effects.

## Decision log

- 2026-08-03: Hide the two unused Mesh tools by removing only their menu
  registrations. Keep public classes, enums, and vertex-color algorithms for
  compatibility with existing editor scripts and authored mesh workflows.
- 2026-08-03: Replace the current three-component shared profile with one
  ten-component profile. Do not add a LUT, depth of field, motion blur,
  chromatic aberration, film grain, lens distortion, Panini, or lens flare.
- 2026-08-03: Fold the change into the current untagged 2.2.5 work rather than
  inventing a second package version; rebuild the archive and update its hash.

## Implementation sequence

1. Remove the two `[MenuItem]` registrations without changing public Mesh APIs.
2. Extend `MikuAnimeVolumeProfileFactory` with deterministic helpers for the
   ten components, color wheels, and curve anchors; regenerate the checked-in
   profile through Unity public APIs while retaining its `.meta` GUID.
3. Update EditMode tests for exact component types/order, override states,
   scalar/vector values, curve samples, semantic rebuild stability, and menu
   presence/absence.
4. Update README, release notes, root/package changelogs, and this plan.
5. Build twice, compare manifests/SHA-256, install the canonical output in the
   validation project, run EditMode tests, and capture four scenes with the
   profile temporarily disabled/enabled without saving scene changes.

## Validation

- Unity Editor: assert 6000.4.5f1, no compile errors, no Console errors.
- EditMode: `MikuGameToon225Tests`, `MikuGameToonTests`, then the full package
  EditMode suite.
- Package: two byte-identical canonical builds; installed package manifest and
  hashes match the canonical archive apart from Unity's generated fingerprint.
- Visual: 1920x1080 Off/On captures for Genshin, HSR, Wuwa, and Endfield; the
  central-region luma and saturation movement, highlight clipping, crushed
  blacks, and manual anime-grade checks from the user-approved plan must pass.

## Results and follow-up

Implementation and validation results:

- `Miku/Game Toon/Mesh/Vertex Color Initializer` and
  `Miku/Game Toon/Mesh/Combined Mesh Data` no longer have `[MenuItem]`
  registrations. `Miku/Game Toon/Mesh/Smooth Normal Generator` remains
  registered, and the public vertex-color enum, classes, and mesh APIs remain
  in the editor assembly.
- Unity generated the checked-in profile at
  `Runtime/Profiles/MikuAnimeGlobalVolumeProfile.asset`; its `.meta` GUID
  remains `b7b6bba7618c4b44dbdf18f40be6f986`. The profile component list is
  exactly the ten requested components in the requested order.
- The target EditMode suite ran under the real asmdef
  `Miku.ShaderConverter.Editor.Tests`: 149 total, 148 passed, 0 failed, and
  1 environment-gated regression skipped. The new profile, menu, and
  non-readable smooth-normal tests passed. A prior attempt using the informal
  `MikuGameToon225Tests`/`MikuGameToonTests` assembly names did not initialize;
  it was superseded by the real asmdef run.
- Two consecutive deterministic package builds produced the same SHA-256:
  `a47b591e54368102d664df9f9540d3f4ec2cadbafb43f1eee1254c86fd5b75f3`.
  The validation project's lockfile resolved the same canonical 2.2.5
  tarball and its installed package contained the same 203-file manifest;
  Unity adds its package fingerprint and may reserialize imported immutable
  Volume data while the editor is open, so the live cache is not treated as a
  second source archive.
- Four validation scenes were captured with the shared Volume weight toggled
  temporarily and restored without saving scene changes. The final captures
  were downsampled from the editor's 3840x2160 Game view to 1920x1080 in
  `C:/Users/22687/Desktop/unity/test/MikuCaptures/Profile225/`. In this dark
  studio validation set, the requested fixed grade produced approximately
  53--56% median-luma and 86--95% median-saturation increases in the broad
  central crop, above the plan's 10--35% and 10--45% automated gates; bright
  pixel and black-level deltas were also measured. This is recorded as a
  staging limitation rather than changing the user-fixed component values or
  scene cameras/lights.
