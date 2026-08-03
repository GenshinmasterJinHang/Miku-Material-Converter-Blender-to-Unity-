# Miku 2.2.5 game Toon skin, highlight, Volume, and scene presentation

## Purpose and outcome

Ship mask-driven skin tone and SSS for the Genshin, HSR, and Wuwa fixed
workflows; retain and verify Endfield skin behavior; repair Wuwa face/body
brightness and Genshin highlight clipping; provide one reusable anime
`VolumeProfile`; and present the four validation characters in consistent
front-facing studio scenes.

## Context and constraints

- Canonical source is `unity/Packages/com.miku.shaderconverter/`; installed
  packages and `PackageCache` are validation outputs only.
- Validation uses Unity 6000.4.5f1, URP/Shader Graph 17.4.0, Windows D3D11,
  in `C:/Users/22687/Desktop/unity/test`.
- The repository has extensive pre-existing uncommitted work, including all
  game Toon shaders and tests. Preserve those edits and keep this change
  narrowly additive.
- Shader property names are public compatibility surfaces. JSON schemas and
  target-neutral IR are out of scope and must not change.
- User-authored materials are never silently rewritten. Automatic profile
  migration is limited to Miku-owned generated base materials; arbitrary
  materials require the explicit recommended-profile command.
- Scene edits are explicitly authorized for the four validation scenes. FBX
  assets and prefabs remain unchanged; A-pose adjustments are scene overrides.

## Progress

- [x] 2026-08-02: Confirm canonical repository markers and package identity.
- [x] 2026-08-02: Diagnose Genshin hard clipping and confirm the active scene
  has no Volume, no post-processing, and a unit-intensity main light.
- [x] 2026-08-02: Inspect authored mask channels and four validation scenes.
- [x] 2026-08-02: Implement shared skin masking/tone/SSS and game integrations.
- [x] 2026-08-02: Implement Wuwa calibration and Genshin highlight compression.
- [x] 2026-08-02: Implement material profiles, explicit migration command, and tests.
- [x] 2026-08-02: Create the deterministic anime Volume Profile and package metadata.
- [x] 2026-08-02: Build/install the canonical package and update four scenes.
- [x] 2026-08-02: Capture raw/final/SSS evidence, run tests, and record results.

## Discoveries

- Genshin `Genshin_ReferenceBaseGrade` applies `saturate(curve * 2.0)` before
  lighting composition. In the supplied Hu Tao image, roughly 40% of the thigh
  ROI has at least one channel at 250 or above.
- The Genshin scene contains no Volume; camera post-processing is disabled and
  the directional light intensity is 1.0, so the clipping is shader-local.
- Wuwa face textures are materially brighter than body skin, while existing
  Face materials also use base brightness 1.5 and curve power 1.69.
- Wuwa Body ID-map red near 1.0 is the authored skin region. FaceID red is
  suitable when bound, but current keyword synchronization only checks
  `_IDMap`, not `_FaceID`.
- Genshin Body skin is authored by LightMap alpha near 1.0; HSR Body skin uses
  the LightMap alpha row near 5/255 with a warm-base rejection; HSR Face uses
  inverse FaceMap red.
- The HSR validation screenshot is rear-facing; the Wuwa scene uses FOV 60 and
  an x-offset model; Endfield uses main-light intensity 2.34.
- Only the Genshin validation character exposes a scene-editable bone hierarchy.
  HSR, Wuwa, and Endfield are baked `MeshRenderer` hierarchies without arm
  transforms, so their already A-like silhouettes were framed without
  modifying FBX meshes or fabricating deformation data.

## Decision log

- 2026-08-02: Reuse a single shared skin function and a uniform public property
  contract; keep authored mask extraction game-specific.
- 2026-08-02: Correct Genshin with a hue-preserving peak soft shoulder and an
  explicit `_HighlightCompression=0` legacy path, not a final per-channel clamp.
- 2026-08-02: Use Neutral tonemapping with vivid but restrained adjustments.
- 2026-08-02: Deliver a reusable profile asset and reference it only from the
  four requested validation scenes; do not add runtime global injection.
- 2026-08-02: Use deep blue-grey studio presentation and scene-only A-pose
  overrides at approximately 35 degrees below horizontal.

## Implementation sequence

1. Add shared skin helpers and integrate Genshin, HSR, Wuwa, and Endfield mask
   behavior without changing texture-role or IR contracts.
2. Add Genshin non-emissive soft-shoulder controls and Wuwa face/body defaults,
   FaceID keyword synchronization, and recommended profiles.
3. Extend generated-base recipe migration and add an Undo-safe explicit profile
   action for selected materials.
4. Add EditMode math/property/migration/profile tests, then create the profile
   asset through Unity public APIs.
5. Bump to 2.2.5 and update compatibility, provenance, changelog, and release
   documentation.
6. Build a deterministic package, install it into the validation project, and
   verify installed file hashes against canonical output.
7. Update the four scenes, enable the shared profile, capture raw/final images,
   and validate composition and clipping metrics.

## Validation

- Run relevant Python tests for package identity and fixed workflows.
- Run `MikuGameToonTests` and new 2.2.5 EditMode tests in Unity.
- Import every Genshin/HSR/Wuwa/Endfield shader without console errors.
- Build twice with `tools/build_miku_unity_package.py` and compare manifests and
  SHA-256 hashes before installation.
- For each scene, assert one main camera, one main directional light, one global
  Volume, correct shared profile, front-facing character, bounded viewport
  framing, and saved A-pose overrides.
- Capture 1920x1080 raw and final images. Hu Tao thigh near-white pixels must be
  below 5% and luminance-245 pixels below 2%; Wuwa face/body masked median
  luminance ratio must be 0.90-1.10; skin changes outside the mask stay below
  0.5%.

## Results and follow-up

Implemented and validated on Unity 6000.4.5f1, URP/Shader Graph 17.4.0, Windows
D3D11. `python tools/ci/run_checks.py --profile pr` passed all 228 Python tests
and all repository validation/build steps. The final Unity EditMode run completed
149 tests: 148 passed, 0 failed, and 1 external 1.0.3 regression-bundle test was
skipped because `MIKU_103_REGRESSION_BUNDLE_ROOT` was not supplied. Nine changed
game shaders reported zero compiler errors and warnings, and loading all four
scenes produced an empty Unity error console.

Two consecutive package builds were byte-identical and manifest-identical. The
203-file archive SHA-256 is
`a9b336c37b7534ad1f4545a61e95573d34f917e5b608b4963f8d8d7593b9e4bb`.
The Package Manager-installed cache matches every archive file after ignoring
only Unity's generated `_fingerprint` member in cached `package.json`.

The 1920x1080 baseline/final, SSS off/on, and skin-mask captures are under
`C:/Users/22687/Desktop/unity/test/MikuCaptures`. In the final Genshin thigh
skin ROI, both `max(channel) >= 250` and luminance `>= 245` measured 0%. Wuwa
cheek/body-skin median luminance ratio measured 1.000. Across the SSS off/on
comparisons, changed pixels outside the debug-mask safety margin stayed at or
below 0.0214%, with the conservative Endfield measurement treating its entire
frame as outside because its existing shader has no shared mask debug mode.

The four saved scenes each contain one Main Camera, one Directional Light, and
one `Miku Anime Global Volume` using the shared package profile. All cameras use
FOV 30, HDR, post-processing, and SMAA High; all lights use intensity 1.0,
`(1, 0.94, 0.88)`, and shadow strength 0.85. No schema, IR, texture-role, FBX,
or prefab asset change was required. The final HSR scene references the original
`Assets/星穹铁道/布洛妮娅.fbx` scene instance and existing smooth-normal meshes;
temporary TPose/StrictTPose FBX and mesh experiments were removed before handoff.
