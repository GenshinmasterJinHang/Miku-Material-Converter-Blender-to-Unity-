# Miku 2.3.0

> **3.0 supersession (2026-08-13):** the validation-only hair-shadow menu and
> its dedicated diagnostic test were removed from the later distributable
> package. The 2.3 execution record below remains historical evidence.

> **Correction (2026-08-12):** the character cloth and female-skin 1024x32
> textures previously treated as candidate game LUTs are material dark-color
> LUTs. They must not be installed full-screen. Miku 3.0.0 defaults to standard
> Volume grading and rejects this misuse.

Miku 2.3.0 completes the opt-in Endfield tutorial-lighting architecture,
repairs smooth-normal outlines across all four Game Toon families, and adds a
project-owned full-screen game-LUT workflow before URP post processing.

## Highlights

- `MikuEndfieldLightingController` drives continuous Day 0/Day 1, top light,
  camera-forward specular, backlight compensation, three-band diffuse, DFG
  environment response, two rim models, and part-specific tutorial behavior.
- Endfield Body is double-sided and flips the final mapped normal using the
  fragment front-face semantic.
- Skin, Face, Eye, Hair, lit transparent Overlay, HairShadow diagnostics, and
  all nine fixed-workflow part invariants are extended without renaming public
  properties. Overlay remains unlit by default and opts into the lit path with
  `_LightingMode=1`.
- Smooth outline data uses marked tangent-space UV7 for skinned meshes, while
  unmarked object-space UV7 remains readable. Thirteen outline consumers share
  clip-space expansion and no longer write outline depth. Genshin/Endfield read
  the public green width mask; Wuwa/HSR preserve their no-mask input, and HSR
  preserves its constant historical distance response.
- The Endfield installer validates a project-owned flattened 32-cube LUT,
  installs one pre-post-process Full Screen Pass, and creates a strict
  Neutral/Bloom/Vignette profile. No game LUT is included in the TGZ.
- Wuwa Body/Hair/Face/Eye add tutorial-compliance controls: minimal
  CookTorrance direct specular and reflection-probe GI, MatCap composed onto
  the albedo at 10% saturation, a UV3 vertical gradient, Fresnel-step rim,
  official face-SDF soft channel, enabled hair-shadow sampling, eye
  main-light response, and vertex-color-gated outlines with the tutorial's
  near/far two-segment distance response. All new properties are additive
  public material surface; no schema or workflow change is introduced.

## Compatibility

This is an additive runtime/editor release. MaterialIR, Bundle, Blender/Miku
IR, and JSON schemas do not change. Existing shader names and material property
references remain valid. Existing Endfield lit materials remain on the legacy
path unless a lighting controller is present; Overlay additionally requires
its explicit `_LightingMode=1` opt-in. Regenerating smooth-normal meshes is
recommended for skinned characters but is not mandatory for old static meshes.

The release-validation tuple is Windows D3D11, Unity 6000.4.5f1
(`cc83ebd631f8`), URP 17.4.0, Shader Graph 17.4.0, and Blender 5.2.0 from the
repository-fixed installation path.

## Validation status

Executed on 2026-08-10:

- Python PR and release profiles: 268/268 tests passed; Ruff clean; package
  identity 13/13.
- Blender 5.2.0 at the repository-fixed executable: 8/8 headless smoke scripts.
  The final 2.3.0 ZIP installed smoke completed with
  `MIKU_INSTALLED_COMPATIBILITY_SMOKE_OK`; evidence is retained at
  `artifacts/miku-2.3.0-blender-release-smoke.json`.
- Unity 6000.4.5f1 / URP 17.4.0 in the port-8080 source-linked project: full
  EditMode suite, 283 tests with 282 passed, 0 failed, and 1 skipped.
- Two independent release builds were byte-identical. Final SHA-256:
  - ZIP `miku_shader_converter-2.3.0.zip`:
    `db2da64cb2a03cd409e61baa7684f17e8e412854df80e43507d3c6ebb31a0c3f`
  - TGZ `com.miku.shaderconverter-2.3.0.tgz`:
    `be6d326c0ada6a97554695a1902344c0acaf3f5fcc89e24e5b2ce23a41c9471d`

Still pending and not claimed as passing: final-TGZ installation and installed
package hash comparison in the port-8080 project; transactional migration and
save of `Assets/endfield/终末地.unity`; and all D3D11 static/skinned outline,
double-sided normal/luminance, LUT/Bloom, and reference-character screenshots.
