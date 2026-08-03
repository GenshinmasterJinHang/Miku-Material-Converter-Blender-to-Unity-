# Miku 2.2.7 neutral vivid global post-processing retune

## Purpose and outcome

Retune the reusable URP global Volume Profile so scenes are brighter, clearer,
and have a restrained glow without applying a warm/cool cast or shifting hue.
The checked-in profile and its editor factory must remain semantically identical.

## Context and constraints

- Canonical implementation files are under
  `unity/Packages/com.miku.shaderconverter/`; validation projects and Unity
  `PackageCache` copies are not sources of truth.
- The repository already contains unrelated dirty work. Preserve it and limit
  this change to the global profile, its factory/tests, and release records.
- The validated target remains Unity 6000.4.5f1 with URP 17.4.0 on Windows.
- Miku 2.2.7 is still the active unreleased worktree version. This visual
  calibration does not change the bundle schema, Blender exporter contract,
  package ID, or public C# API, so it stays in the 2.2.7 release line.
- "Neutral" means zero White Balance, identity Channel Mixer/RGB curves,
  neutral Lift/Gamma/Gain, neutral Shadows/Midtones/Highlights and Split
  Toning, zero hue edits and a white color filter/Bloom tint. Global Saturation
  is deliberately limited to `+8` to satisfy the vividness requirement without
  introducing a hue cast.

## Progress

- [x] 2026-08-03: Confirmed the canonical source boundary and inspected the
  active Unity scene, global Volume, profile generator, serialized asset, and
  existing EditMode coverage.
- [x] Update the factory with a luminance master curve, Exposure `+0.35`,
  Contrast `+16`, Saturation `+8`, subtle neutral Bloom, and reduced vignette.
- [x] Regenerate/update the checked-in profile and keep it equivalent to the
  factory output.
- [x] Expand EditMode tests for neutral color controls, curve shape, Bloom, and
  deterministic regeneration.
- [x] Update changelogs/release notes and validate compilation, tests, package
  determinism, installed profile state, and representative screenshots.

## Discoveries

- `Runtime/Profiles/MikuAnimeGlobalVolumeProfile.asset` is the only reusable
  global post-processing profile in the canonical Unity package.
- The validation scene contains one global Volume and it references that exact
  package profile.
- The previous preset intentionally introduced a warm grade through White
  Balance `(6, 2)`, a non-identity Channel Mixer, channel-specific RGB curves,
  Split Toning, saturation `+12`, hue `-1`, and warm color/Bloom tints. Those
  settings conflict with the requested color-neutral result.

## Decision log

- 2026-08-03: Keep all ten existing Volume components and their order so scene
  references and the public reusable asset remain compatible.
- 2026-08-03: Make every chromatic control explicit and neutral rather than
  merely disabling components. This prevents stale values from reappearing
  when users toggle component activity or rebuild the asset.
- 2026-08-03: Use the Color Curves master channel for luminance shaping; RGB and
  hue/saturation curves remain identity/no-op. Add brightness with bounded
  post exposure and a moderate contrast/saturation lift, not a color filter,
  white-balance shift, or hue rotation.
- 2026-08-03: Retain Neutral tonemapping and high-quality Bloom. Bloom uses a
  white tint and restrained intensity so emissive highlights glow without
  washing the whole frame.

## Implementation sequence

1. Replace chromatic grading values in `MikuAnimeVolumeProfileFactory.cs` with
   explicit neutral values and retune master curve/exposure/Bloom/vignette.
2. Apply the same serialized values to
   `MikuAnimeGlobalVolumeProfile.asset`.
3. Update `MikuGameToon225Tests.cs` to assert all neutral invariants and the
   intended luminance/glow calibration.
4. Update the root/package changelogs and 2.2.7 release notes.
5. Run Unity compile and EditMode tests, rebuild the Unity package twice and
   compare bytes, install the artifact, inspect the live Volume, and capture
   visual evidence without modifying the user scene.

## Validation

- `python tools/ci/run_checks.py --profile pr` must pass.
- Unity EditMode tests must pass in Unity 6000.4.5f1 / URP 17.4.0.
- The package profile and a freshly generated test profile must expose the same
  ten components and calibrated parameter/curve values.
- Two `tools/build_unity_package.py` runs must produce byte-identical TGZs.
- The live scene Volume must report Neutral tonemapping, enabled Color Curves,
  neutral color controls, and the retuned white Bloom.
- Before/after camera captures should show higher midtone luminance and a small
  highlight halo without an intentional hue cast.

## Results and follow-up

Implementation completed 2026-08-03. The selected calibration is Contrast `+16`
and Saturation `+8`; Phi-specific lighting/exposure compensation and per-scene
artistic overrides remain outside this reusable global preset.

Executed validation:

- Unity targeted EditMode: 1 passed, 0 failed.
- Unity full EditMode: 156 passed, 0 failed, 1 ignored external 1.0.3 bundle
  test because `MIKU_103_REGRESSION_BUNDLE_ROOT` was not set.
- Unity console after clearing pre-existing package-install Inspector errors: 0
  errors.
- Python PR checks: 242 tests passed; package identity and schema checks passed.
- Unity TGZ built twice byte-identically: 383789 bytes,
  SHA-256 `87a6b63db95a345324a3f7d9b1e3d35ce4ab97d7bebe8a306d61fb5d68f42bf0`.
- Installed PackageCache profile reported ten components, White Balance `0,0`,
  Exposure `0.35`, Contrast `16`, Saturation `8`, Bloom `0.85/0.2`.
- Fixed-camera evidence was captured in the external validation project at
  `Assets/Screenshots/postprocess-baseline.png` and
  `Assets/Screenshots/postprocess-neutral-vivid.png`; the scene was not saved.
