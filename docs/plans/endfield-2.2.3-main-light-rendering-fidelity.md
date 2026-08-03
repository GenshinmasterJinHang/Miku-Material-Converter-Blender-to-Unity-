# Endfield 2.2.3 Main Light and Rendering Fidelity Repair

## Purpose and outcome

Restore the URP Main Light as the authoritative direct key light for every
Endfield material part, then repair the remaining Face, Eye, Hair, Skin, and
Body fidelity regressions. The observable 2.2.3 result must remain visibly lit
with SH disabled, respond to directional-light rotation, keep SH as a separate
additive ambient term, and preserve non-black metal, readable Face SDF, authored
iris/sclera roles, alpha-authored blush, stable hair highlights, and bounded SSS.

## Context and constraints

- Canonical implementation is limited to `unity/Packages/com.miku.shaderconverter/`
  plus canonical public documentation. Validation assets live in the external
  Unity project and are not package source.
- Exact target: Unity 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0, Windows D3D11.
- The worktree contains substantial user-owned 2.x work. Preserve unrelated
  edits and the existing 2.2.0, 2.2.1, and 2.2.2 material/package rollback data.
- MaterialIR remains schema 2.0. Existing shader names, property references,
  texture roles, material-slot order, and `_EyeMode` values remain compatible.
- MyZmdShaders has no declared license and is behavioral evidence only. No
  implementation source or private character texture is copied into Miku.
- The selected URP directional Main Light is the Endfield key light. Rendering
  Layers control exclusion; its per-object distance attenuation is diagnostic
  and must not silently erase direct light on the supported directional path.

## Progress

- [x] 2026-08-02: Verified canonical roots, dirty worktree, package identity,
  target Unity tuple, live Main Light, Culling Mask, and Rendering Layers.
- [x] 2026-08-02: Traced the current direct-light and SH paths and confirmed the
  forward passes omit the standard URP Main Light variants.
- [x] 2026-08-02: Restored the shared Main Light contract and direct-only
  diagnostics in opaque and transparent forward passes.
- [x] 2026-08-02: Implemented Face, Eye, Hair, Skin, Body, specular-refine, and
  reflection-probe IBL repairs.
- [x] 2026-08-02: Added editor bindings, CPU mirrors, tests, versioning, and
  public documentation.
- [x] 2026-08-02: Built and deployed 2.2.3, created isolated validation
  materials/evidence, completed D3D11 shader and HDR checks, and recorded the
  deterministic package hash.

## Discoveries

- Live validation uses an enabled directional light with intensity 1, all-layer
  Culling Mask, Rendering Layer 1, and a renderer on the matching layer. The
  global `_MainLightColor` is non-zero, so scene configuration does not explain
  the SH-only appearance.
- Body, Skin, Face, Eye, and Mouth multiply direct color by
  `mainLight.distanceAttenuation`; Hair bypasses that helper. URP 17.4 sources
  define the directional main-light value as per-object `unity_LightData.z`.
- The Endfield `UniversalForward` and `TransparentForward` passes currently have
  no Main Light shadow, screen-shadow, Rendering Layer, or shadow-mask variants.
- The emotion atlas tiles zero and two contain low-amplitude authored alpha
  cheek masks. RGB is colored across most of the tile and is not safe coverage.
- Iris and sclera are separate materials with overlapping UV ranges; `_EyeMode`
  must remain the role selector.
- `T_actor_common_hairst_01_ST` is a 512x256 two-dimensional lookup texture,
  while the character hair ST is an authored mesh-space refine map.
- Scratch-project imports can log transient Shader Graph package-cache `GUID`
  errors before the package graph settles. The final test result has zero
  failures, and the installed validation project's final Console/shader checks
  are both zero-error.
- Unity's material `[Enum]` drawer rejected the expanded Face/Skin debug value
  list. Moving these selectors into the existing custom shader GUI eliminated
  the Console errors without changing `_DebugView` values.
- Unity screenshot requests are asynchronous. The first four requests captured
  the restored frame and were byte-identical; they were rejected and replaced
  only after each mode-specific image had reached disk with a distinct hash.

## Decision log

- 2026-08-02: Publish 2.2.3 and create a separate fourteen-material validation
  set. Real rollback requires reinstalling the retained 2.2.2 package.
- 2026-08-02: Direct-only Main Light validation is a hard gate before tuning
  SDF, LUT, SSS, hair, or metal coefficients.
- 2026-08-02: Keep object-matrix head space and add an optional manual object-
  space basis; do not introduce a bone-follow runtime component.
- 2026-08-02: Use URP reflection probes with rotated reflection direction and
  perceptual-roughness bias; do not add a private environment cube dependency.
- 2026-08-02: Keep legacy roles and add `SpecularRefineF0` and
  `SpecularRefineColor` as additive MaterialIR 2.0 roles.

## Implementation sequence

1. Add URP Main Light variants, shared directional-light terms, Rendering Layer
   matching, and direct/SH diagnostic outputs to the Endfield runtime.
2. Mirror the contract in editor math and prove zero raw distance attenuation
   cannot erase a valid matching directional Main Light.
3. Repair Face basis/SDF/blush/SSS, Eye cornea partitioning, Hair normal/LUT,
   and Body metal/specular/IBL behavior.
4. Add texture-role mappings, material toggles, shader property coverage, and
   focused EditMode tests.
5. Raise package/recipe identity to 2.2.3 and update changelog, compatibility,
   release, provenance, and plan records.
6. Build the canonical TGZ twice, deploy it to Unity, create the isolated 2.2.3
   validation material/evidence set, run D3D11 finite checks, and self-review.

## Validation

- `python tools/ci/run_checks.py --profile pr`
- `tools/ci/run_unity_editmode.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe"`
- Focused `MikuGameToonTests` and Unity shader compilation with zero errors.
- Direct-only rendering with `_IndirectIntensity=0`: non-zero light, directional
  response, zero-light darkness, and independent SH-only output.
- Face light sweep, Eye close-up, blush on/off, Hair LUT/sphere toggles, Skin SSS,
  low-AO metal, environment rotation, and HDR NaN/Infinity checks.
- Two consecutive `python tools/build_miku_unity_package.py` outputs with equal
  normalized manifests and SHA-256 hashes.

## Results and follow-up

The canonical PR profile passes 228 Python tests after source/schema/identity
validation. The latest full Unity EditMode snapshot passes 133 of 135 tests,
with zero failures and two declared ignored tests. The final package is
byte-stable across consecutive builds at
SHA-256 `ef13a3bc32c8f6610729ea48d898e3a7ab75da7045d73245eed55b46fbf63a08`.
The installed Unity package reports 2.2.3; all six character shaders report
zero compiler errors, the Console is clear, and an ARGBHalf 512x512 scan of the
2.2.3 material set found zero NaN/Infinity values with maximum absolute channel
value 1.105469. Four distinct Body/Face Direct Only and SH Only screenshots are
stored under the validation project's `Assets/endfield/Validation/2.2.3`.
An isolated 256x256 Body Direct gate measured direct luminance sum 597.6261;
rotating the key light 90 degrees changed the frame by 217.3084 accumulated
luminance and moved the horizontal centroid from 119.6329 to 119.9875. Zero
light and a mismatched Rendering Layer both reached the same near-black outline
baseline (maximum luminance 0.1374492), while SH Only remained non-zero with
luminance sum 211.1937.

The scene was already dirty, so validation assigned the cloned materials only
temporarily, restored all fourteen original 2.2.2 references, and did not save
the scene. Bone-driven head-space updates and additional punctual-light
participation remain intentionally outside 2.2.3.
