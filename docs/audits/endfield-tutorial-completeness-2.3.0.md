# Endfield tutorial completeness audit for Miku 2.3.0

> **3.0 supersession (2026-08-13):** the validation-only hair-shadow menu and
> dedicated diagnostic test referenced by this audit were later removed from
> the distributable package. Their completed results remain historical evidence.

> **Correction (2026-08-12):** the historical full-screen LUT setup below used
> the cloth material dark-color LUT and is invalid as screen grading. Its
> screenshots remain historical evidence only. The replacement Volume-only,
> EyeHL, ShadowCaster, and outline work is tracked in
> `docs/plans/miku-3.0.0-endfield-reference-fidelity.md`.

Scoring is `0` missing, `1` implemented or structurally verified without the
required target-scene evidence, `2` implemented and verified in the port-8080
Unity scene, and `N/A` outside the main character-shader scope. A row is not
promoted to `2` until the named behavior, shader compilation, and relevant
Unity setup have been executed on the validation project. Passing isolated
math/source/ShaderUtil tests is recorded below, but does not substitute for a
D3D11 scene measurement or screenshot.

Validation target: Windows D3D11, Unity 6000.4.5f1
(`cc83ebd631f8`), URP 17.4.0, Shader Graph 17.4.0, scene
`Assets/endfield/终末地.unity`.

| Area | Tutorial contract | Score | Evidence |
| --- | --- | ---: | --- |
| Resources/binding | Nine explicit parts and deterministic texture roles; unknown parts rejected | 1 | Python PR profile passed, including fixed-workflow coverage; final-TGZ 8080 material/scene audit pending |
| Shared lighting | Day 0/1, top light, shadow sigmoid, three bands, NoF, ramp hue, backlight/pitch | 1 | Isolated `MikuEndfieldTutorialLightingTests` passed 8/8, including finite math and ShaderUtil compilation; D3D11 render validation pending |
| Body | Metallic/roughness, camera-forward specular, DFG/multiscatter, two rims, emission modes | 1 | Isolated lighting fixture passed; reference renders pending |
| Body two-sided | Cull Off and front-face flip after mapped normal | 1 | Source/compile contract passed in isolation; double-sided plane measurement pending |
| Special clothing | Base-alpha energy/SSS and opt-in lit transparent Overlay | 1 | `_LightingMode=0` legacy-unlit / `1` Toon-lit contract compiles; reference renders pending |
| Skin | LUT dark color, three bands, SSS, roughness/reflectivity, no environment specular | 1 | Isolated lighting fixture passed; reference renders pending |
| Face | SDF/backlight, expression/neck blend, Refine R/G SSS/custom normal, Refine A one-sided rim | 1 | Isolated lighting fixture passed; 360-degree render pending |
| Eye | Special cornea normal, shared toon diffuse, two bright regions, MatCap RGB/alpha | 1 | Isolated lighting fixture passed; reference render pending |
| Hair | Split/sphere normal, flattening, Kajiya--Kay, authored LUT coordinates/F0/halo, Day state | 1 | Isolated lighting fixture passed; camera/light render pending |
| Hair/Eye shadow | Offset-mesh/stencil HairShadow and Overlay EyeShadow with diagnostics | 1 | Isolated HairShadow diagnostic fixture passed 3/3; local offset-mesh/stencil audit pending |
| Outline | UV7 TangentSpaceV2, legacy defense, 13 consumers, clip-space width; Genshin/Endfield G mask, Wuwa/HSR neutral width, HSR constant historical response | 1 | Isolated outline fixture passed 14/14 and existing Game Toon fixture 28/28; static/skinned four-scene measurements pending |
| Post processing | Project game LUT before Neutral/Bloom/Vignette; target camera uses SMAA High | 1 | Installer implemented; dedicated final-TGZ installer test and target scene/LUT render validation pending |
| Fur shell | Multi-pass fur | N/A | Tutorial effect excluded from main shader denominator |
| Fire | Noise-driven fire | N/A | Tutorial effect excluded from main shader denominator |

## Evidence recorded at this checkpoint

- 2026-08-10 (LUT wiring): the Endfield LUT renderer feature is now actually
  used by the scene. `PC_Renderer.asset` was cloned to
  `Assets/Miku/Endfield/Rendering/PC_Renderer_Endfield_2_3_0.asset`, the
  `Miku Endfield Full Screen LUT` feature (BeforeRenderingPostProcessing),
  `MikuEndfieldFullScreenColorLut.mat`, and
  `MikuEndfieldPostVolumeProfile.asset` were installed under
  `Assets/Miku/Endfield`, the clone was appended to `PC_RPAsset` at index 1
  (default remains 0), the 终末地 camera now uses renderer index 1, and the
  scene volume references the Endfield profile. Scene backup:
  `MikuBackups/2.3.0-endfield-lut-install/20260810-210551`. Console reported
  zero errors after the install; scene SHA is now
  `FC6A69A973495E024423193AF8B07455A386D1FDC330839A632E670B71EF7D1C`.
  A Game-view capture of the wired scene is retained at
  `Captures/EndfieldLutCheck/screenshot-20260810-211228.png` (editor
  reopened `Assets/星穹铁道/布洛妮娅.unity` afterwards; visual confirmation
  of the LUT grade in that PNG is still recommended).
- 2026-08-10 (second pass): fixed the hidden Endfield PassLibrary compile
  error (`EndfieldSafeNormalize2` float2 overload) and the full-screen LUT
  shader `TEXTURE2D_X` error (URP Core.hlsl include before Blit.hlsl); both
  hidden shaders joined the ShaderHasError compile test. Isolated Unity
  EditMode passed 283/283 (0 failed, 2 skipped) on the rebuilt TGZ, and the
  live port-8080 editor console reported zero errors after reimport. The
  targeted Endfield-only scene migration then applied fourteen cloned
  `Endfield_00..13` materials (from `杰哥_2.2.2` into `杰哥_2.3.0`) to
  `Assets/endfield/终末地.unity`, installed one
  `MikuEndfieldLightingController`, and saved the scene with a byte backup
  under `MikuBackups/2.3.0-endfield-material-apply/20260810-203716`.
- 2026-08-10: tutorial-fidelity audit fixes implemented against the article:
  article D/V direct specular and Day-blended envelope, article F0-refine UVs,
  width-scaled face SDF with article ramp signal, face-plane projected eye
  light without scene shadow, luminance-preserving ramp control, NoF power
  with Skin/Face NoF disabled, shaded-band light desaturation, article face
  rim, face SSS `0.85/0.15` remap, and `0.96 - 0.96 * metallic` Body energy.
  Legacy paths and documented adaptations are preserved.
- Source-linked isolated Unity 6000.4.5f1 / URP 17.4.0: 53/53 focused
  EditMode tests passed (8 lighting, 3 HairShadow diagnostics, 14 shared outline,
  28 existing Game Toon). XML is retained under
  `%LOCALAPPDATA%\Temp\MikuEndfieldValidation-019fe493`.
- Python PR profile: 262/262; Ruff clean; package identity 13/13.
- Blender 5.2.0: 8/8 headless smoke scripts plus final-ZIP installed smoke;
  `artifacts/miku-2.3.0-blender-release-smoke.json` records archive, installed
  tree, and normalized IR hashes.
- Unity TGZ: two byte-identical 440406-byte builds, SHA-256
  `515d63aee227e905b61496a107ebb5227a8cce27f708484b5ca39611d5c17903`.

The release is incomplete while any in-scope row remains `0` or `1`. Final
port-8080 test results, screenshot paths, measurements, target-scene rollback
assets, and final-TGZ installed-package hashes remain pending. An isolated
implementation test is not target-scene visual evidence.
