# Miku 3.0.0 Endfield Reference-Fidelity Repair

> **2026-08-13 supersession:** the validation-only hair-shadow diagnostic and
> its dedicated test were removed from the distributable package, and the
> target-profile hash failure recorded below was reconciled. The earlier
> candidate's executed rendering evidence remains historical; current final
> package results are recorded in `docs/release/miku-3.0.0.md`.

## Purpose and outcome

Restore the Endfield character rendering path to the supplied game reference
without treating material dark-color LUTs as screen-grading LUTs. The finished
implementation keeps cloth and skin LUTs local to their materials, restores the
fixed and dynamic eye-highlight layers, uses a URP-correct shadow caster, routes
the outline pass through the dedicated renderer feature, and makes a standard
Volume-only post-processing setup the default.

The observable target is the supplied 643x666 front-view reference. Rendering
claims require Unity 6000.4.5f1, URP 17.4.0, Linear color, Direct3D 12, HDR camera,
post-processing, and SMAA High. Headless or null-device runs are test evidence
only and are not visual evidence.

## Context and constraints

- Canonical implementation source is
  `unity/Packages/com.miku.shaderconverter/`; validation-project package copies
  must come from a deterministic TGZ build.
- The worktree already contains substantial unrelated 3.0.0 changes. This plan
  records only additive, tightly scoped edits and does not revert user work.
- `T_actor_common_femaleskincolor03_lut_D` and
  `T_actor_common_cloth_lut_01_D` are material dark-color LUTs. Neither is a
  screen-grading LUT.
- The original PMX maps material index 2 (`目HL`) to texture index 1
  (`T_actor_aglina_iris_01_D`). The Unity RGBA copy adds an alpha channel that
  is nearly zero in the EyeHL UV island, so the overlay must use opaque coverage.
- The supplied reference is a scaled, low-resolution target. Region statistics
  and eye-highlight proportions are valid targets; full-frame SSIM, one-pixel
  outline width, and exact Bloom radius are not.
- `_MatCapUvScale` is a backward-compatible public shader property. No Miku
  interchange schema or texture-role change is permitted.
- `_ShadowCull` is intentionally deferred unless a D3D12 A/B proves that
  standard shadow bias still leaves a double boundary on two-sided cloth.

## Progress

- [x] 2026-08-12: Reconfirmed the canonical repository boundary, read governing
  instructions, inspected the dirty worktree, and traced affected code/tests.
- [x] 2026-08-12: Audited both LUTs, PMX EyeHL mapping, eye MatCap path,
  ShadowCaster path, Outline LightMode, and the current validation assets.
- [x] 2026-08-12: Implemented shader contracts and the backward-compatible eye
  property.
- [x] 2026-08-12: Implemented Volume-only default and material-LUT rejection
  diagnostics.
- [x] 2026-08-12: Built and installed a deterministic TGZ, verified its SHA-256,
  and updated the validation renderer/profile/EyeHL/iris material assets.
- [x] 2026-08-12: Added automated contract, installer, eye, and profile tests.
- [x] 2026-08-12: Corrected public documentation, provenance notes, and
  changelog.
- [x] 2026-08-12: Ran the complete isolated EditMode suite and targeted D3D12
  GUI-project tests.
- [x] 2026-08-12: Captured fixed-camera Direct3D 12 structural and MatCap A/B
  evidence.
- [ ] Quantitative reference ROI/Delta E acceptance remains pending because the
  validation asset is still in T-pose and cannot be registered to the supplied
  posed 643x666 target without measuring pose/background error as shader error.

## Discoveries

- The full-screen material currently applies the cloth dark-color LUT at
  intensity 1 before Bloom and Neutral tonemapping. Neutral gray and white retain
  only about 47-50% of their linear luminance, explaining the dark, gray result
  and weak Bloom.
- The original PMX texture table does not contain `face_01_hl_M`; its EyeHL
  material explicitly references `iris_D`. The incorrect Unity binding was a
  validation-scene mapping error later copied across material versions.
- The tutorial eye branch computes a MatCap highlight but samples near the black
  center of `matcap_08_D`. A separate sampling scale can widen the sampled region
  without changing the diffuse cornea normal or the default formula.
- The Endfield ShadowCaster reused the camera-depth vertex and therefore omitted
  URP light-space bias, clamping, and punctual-light variants.
- The Outline pass used `SRPDefaultUnlit`, while the geometry renderer feature
  requests `MikuToonOutline`; the route was not closed.
- No Unity Editor instance was available when implementation began, so MCP scene
  mutation and Frame Debugger checks must wait for the validation stage.

## Decision log

- 2026-08-12: Keep material LUT sampling unchanged and remove it only from the
  screen path. Exposure compensation is not accepted as a substitute for fixing
  the semantic mismatch.
- 2026-08-12: Default post-processing is Volume-only. The explicit full-screen
  LUT API remains for genuine screen LUTs, but material LUTs produce a stable
  diagnostic before any asset mutation.
- 2026-08-12: `_MatCapUvScale = 1` must reproduce the previous sampling exactly.
  The property scales only view-space MatCap UV displacement about the texture
  center; the cornea normal used by diffuse lighting is unchanged.
- 2026-08-12: Restore the PMX fixed EyeHL layer with `iris_D`, opaque coverage,
  untinted texture color, Cull Off, and Legacy Unlit. Keep `ZTest LEqual`; use
  depth offset only if the Frame Debugger proves coplanar rejection.
- 2026-08-12: Implement the URP ShadowCaster contract before considering a
  per-material shadow cull property.
- 2026-08-12: Do not add a GGX or Bloom-created white dot to imitate the eye
  reference. MatCap remains the dynamic layer.

## Implementation sequence

1. Add `_MatCapUvScale` to the Endfield Eye shader and shared constant buffer;
   apply it only to MatCap UV displacement with a default of 1.
2. Add `EndfieldShadowVertex`, URP bias/clamping, direction/punctual light
   selection, and the punctual shadow-caster variant. Keep DepthOnly unchanged.
3. Route Outline through `MikuToonOutline` while retaining the pass name.
4. Expand the Endfield Volume profile to Color Adjustments, Color Curves,
   Neutral Tonemapping, Bloom, and Vignette using the agreed starting values.
5. Add a public Volume-only installer entry, make it the default UI mode, and
   reject textures whose path, material references, or recipe role identify
   them as material LUTs.
6. Add Endfield Eye MatCap diagnostics that apply to iris mode but not sclera.
7. Add automated tests for all contracts and the PMX-derived EyeHL material
   state.
8. Update documentation and changelog without changing public texture roles or
   interchange schemas.
9. Build a deterministic TGZ, verify hashes, install it into the validation
   project, then update its renderer/profile/EyeHL material from that package.
10. Run EditMode and D3D12 fixed-camera validation; record exact commands and
    results below.

## Validation

Automated source and EditMode validation will cover:

- `_MatCapUvScale` existence, default compatibility, and sampling isolation.
- standard ShadowCaster bias/clamping and punctual-light compilation contract.
- `MikuToonOutline` agreement between pass and renderer feature.
- five-component Endfield Volume profile and starting color-adjustment values.
- Volume-only installation without a renderer or LUT.
- deterministic rejection of cloth/skin/material `_ColorLutTex` resources as
  screen LUTs before writes.
- Eye MatCap missing-resource diagnostics for iris only.
- PMX-derived EyeHL golden state while Face keeps `face_01_hl_M` as its local
  highlight mask.

Planned visual command requirements:

- Unity Editor 6000.4.5f1 with `-force-d3d12`; never `-nographics`.
- Fixed front full-body and eye-close-up cameras, plus yaw/pitch +/-15 degrees.
- Output downsampled to 643x666 and registered by eye centers.
- Region targets: skin .734, hair .073, black .037, white .491, red .199 linear
  median luminance; absolute Delta EV <= .15, median Delta E 2000 <= 3, P95 <= 6,
  and saturation ratio .9-1.1.
- Shadow A/B: outline width/tint, full-screen LUT, standard caster, directional
  light, and one punctual light.

Commands and outcomes are appended only after execution.

Executed 2026-08-12:

- `python tools/build_miku_unity_package.py` twice: byte-identical TGZ output.
  Final post-fix archive SHA-256 is
  `AC0E67D66831B9A85C095A127496CE7DBF27527C981F7F5B88C272B8FC1AB910`.
- `tools/ci/run_unity_editmode.ps1` with Unity 6000.4.5f1, URP 17.4.0,
  Shader Graph 17.4.0, and the final TGZ: 315 total, 312 passed, 0 failed,
  3 skipped. This run used `-nographics` and is not GPU evidence.
- Installed the same TGZ into the private validation project and verified an
  identical SHA-256. Unity GUI reported Direct3D12, Linear color, HDR camera,
  post processing, and SMAA High.
- Targeted GUI-project EditMode selection: 5 total, 5 passed, 0 failed,
  0 skipped. It covered shader compilation, ShadowCaster contract, Volume
  composition, material-LUT rejection, and PMX EyeHL state.
- Captured MatCap-only `_MatCapUvScale` values 2, 4, 6, and 8. Value 2 was too
  broad/white; 4 was the smallest value producing an upper-iris band; 6 and 8
  collapsed to smaller point-like signals. The JieGe validation material keeps
  4, while the public shader default remains 1.
- Captured final D3D12 full-body and eye-close-up images under
  `Captures/EndfieldReferenceFidelity/` in the validation project. The eye
  layers are visible and no compiler/console errors were present. The fixed
  EyeHL remains brighter and warmer than the low-resolution target, so the
  images are structural evidence, not a claim of Delta E acceptance.

## Results and follow-up

Implementation and structural validation are complete. The validation project
now uses the five-component Endfield profile, has no Miku full-screen LUT
feature, keeps Face `face_01_hl_M`, binds EyeHL to `iris_D` with opaque
coverage, and stores JieGe `_MatCapUvScale = 4`.

Known limitations: the scene model is still in T-pose, so the supplied posed
reference cannot support credible ROI/Delta E acceptance yet. A pose-matched
camera/model setup, yaw/pitch eye sequence, closed-eye animation, directional
versus punctual shadow-caster image sequence, and Frame Debugger trace remain
genuine follow-up validation. `_ShadowCull` and Overlay depth offset were not
added because no D3D12 trace demonstrated either conditional defect after the
core fixes.

## 2026-08-12 outline hard-disable follow-up

### Purpose and outcome

Remove the black inverted-shell fragments beside JieGe's ear and mouth while
preserving the existing Endfield lighting families. Material slot 12 keeps the
Hair shader but stops participating in the Outline pass, and Face keeps its
outline draw with a lighter texture-derived skin tint. Zero width or zero local
coverage becomes a true no-fragment contract for every shared GameToon outline
consumer.

### Progress

- [x] Confirmed the original PMX material 12 has edge rendering disabled and
  that the corresponding Unity material currently enables Endfield Outline.
- [x] Confirmed the PMX edge flag is absent from the FBX, material recipe, and
  bundle binding, so this repair cannot infer it during the existing import.
- [x] Audited all bound Face textures and rejected them as outline masks: no
  channel suppresses only the two mouth-corner triangles while retaining the
  authored face silhouette.
- [x] Locked the user-selected Face fallback to `_OutlineColorTint = white`,
  `_OutlineGamma = 1.2`, `_OutlineWidth = 0.0012`, and no outline-mask texture.
- [x] Implement the shared finite coverage/discard contract and Endfield
  material-state API.
- [x] Add source, material-state, shader-compile, and D3D12 pixel regressions.
- [x] Build and install the canonical TGZ, update only the two private target
  materials, and capture Direct3D 12 acceptance evidence.

### Discoveries and decisions

- Endfield's ShaderLab pass name is `Outline`; its LightMode tag is
  `MikuToonOutline`. Unity 6000.4 reflection and a live in-memory material test
  confirm `SetShaderPassEnabled` keys these independently, so the persistent
  material state must disable `Outline` rather than the LightMode string.
- `_UseOutlineMask` is synchronized from texture presence by the custom
  inspector and therefore cannot represent a persistent material-level enable.
- Width and masks currently affect only vertex displacement. At zero coverage,
  `Cull Front`, `ZTest LEqual`, and an opaque fragment return still render the
  original-position back shell on interior or concave geometry.
- Face outline color is `pow(BaseMap.rgb, _OutlineGamma) *
  _OutlineColorTint.rgb`. The selected white tint and gamma 1.2 intentionally
  produce a lighter BaseMap-derived skin line; they do not remove the two
  underlying triangles, and a faint skin-colored seam remains an accepted
  limitation.
- No interchange schema, texture role, or importer heuristic changes are in
  scope. Private PMX/FBX/material bytes and screenshots remain outside the
  public package.

### Implementation and validation sequence

1. Centralize finite effective-width calculation and fragment clipping in the
   shared outline HLSL, then wire the nine concrete passes plus the four Endfield
   shaders that share one pass library.
2. Add `_UseOutline` to Endfield Body/Skin/Face/Hair and a public material-state
   API that keeps the property and `Outline` pass state synchronized without a
   shader keyword.
3. Extend EditMode contracts and add a real Direct3D 12 render-to-texture
   positive/zero-coverage regression. Make the D3D12 runner reject missing,
   failed, or skipped required tests by parsing NUnit XML.
4. Build the canonical TGZ twice, verify byte identity and SHA-256, install it
   into the private validation project, and update only material 12 pass state
   plus material 0 tint/gamma.
5. Validate slot 12 draw absence and fixed-camera ear/mouth appearance on
   Direct3D 12. Append commands and exact outcomes; do not count headless runs
   as GPU evidence.

### Validation record

- `python -m unittest tests.test_miku_package_identity
  tests.test_genshin_tutorial_conformance tests.test_public_docs` ran 27 tests:
  26 passed and one pre-existing unrelated profile-hash assertion failed for
  `gameToonScreenRim`. The outline package identity and public-doc tests passed.
- `tools/ci/run_unity_editmode.ps1` ran Unity 6000.4.5f1 with URP and Shader
  Graph 17.4.0 against the final TGZ: 321 total, 315 passed, one failed, five
  skipped. The only failure was the pre-existing Endfield LUT rollback test,
  where Windows file locking added a second `IOException` during rollback and
  changed the expected exception into an `AggregateException`. The 13-consumer
  source contract, shader compilation, Endfield outline state, persistence,
  migration, and unsupported-material regressions all passed. A separate
  focused run of the new non-GPU contracts reported six passed, zero failed,
  and zero skipped. This `-nographics` run is compile and logic evidence only.
- Two independent canonical package builds produced byte-identical archives
  with SHA-256
  `bbfda4095903e286ab417795fcbc27fb6fefe6290deb2e9808b66e907a93b8d9`.
  The archive was installed in the private validation project and resolved as
  `com.miku.shaderconverter` 3.0.0.
- `tools/ci/run_unity_dx12_gpu.ps1` ran without `-nographics` under Unity
  6000.4.5f1, Direct3D 12, URP 17.4.0, and Shader Graph 17.4.0. NUnit XML
  reported four required tests discovered, four passed, zero failed, zero
  skipped, and zero inconclusive. Three of those are this follow-up's Outline
  gates; the fourth is the package's co-located Wuwa Forward+ GPU gate.
  Positive coverage rendered pixels; zero width, vertex G, texture mask,
  disabled state, NaN, and Infinity rendered none.
- The live 14-slot renderer retained `MIKU/Endfield/Hair` on slot 12 while
  reporting `_UseOutline = 0` and `GetShaderPassEnabled("Outline") = false`;
  its serialized private material contains `disabledShaderPasses: [Outline]`.
  Slot 0 retained the Face Outline pass with white tint, gamma 1.2, width
  0.0012, and no mask. Frame Debugger captured the remaining character Outline
  renderer-list events on Direct3D 12 while the disabled slot stayed outside
  the pass contract.
- Fixed front, ear three-quarter, and mouth close-up captures were inspected in
  the private project. The ear-side black shell is absent; the mouth-corner
  artifact is reduced to the accepted light skin-colored seam; cheek and chin
  silhouette coverage remains. The private captures are not committed.
