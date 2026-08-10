# Wuwa tutorial completeness audit 2.3.0

## Scope

Compares `MIKU/Wuwa/*` (Body, Hair, Face, Eye, Effect) against the user-supplied
IcePaper Wuwa rendering tutorial and records the 2.3.0 repairs. Only the Unity
package is in scope; Blender export and schemas are unchanged.

## Baseline findings

Already present and equivalent:

- 3.3 shadow region: `smoothstep` ramp plus shadow-color multiply and optional
  ID offset.
- 3.5.2 depth-convolution rim: `MikuToonCharacterMask` pass plus
  `MikuToonScreenRimRendererFeature` composite (4-neighbor depth delta).
- 3.6 hair highlight: HM map with view gating (approximation; gate changed to
  the tutorial's `step(0.3, ...)` shape in 2.3.0).
- 3.7 hair shadow: `Wuwa_HairShadowMask` already implemented; the renderer
  feature was not installed and materials disabled it.
- 3.8 face SDF: `Wuwa_FaceSDFLight` already implemented with left/right flip;
  the material used a non-official texture and disabled the soft channel.
- 3.9 eye: parallax and authored highlights already implemented; Feibi only
  bound `EyeHET` and the eye was unlit.
- 3.10 outline: multi-pass normal extrude with UV7 smooth normals already
  present; vertex-color mask and tutorial distance formula were missing.

Missing or misaligned before 2.3.0:

- 3.1.1 direct light used `pow(NdotH, 64)` instead of the tutorial's minimal
  CookTorrance specular.
- 3.1.2 indirect light had no `GlossyEnvironmentReflection` term.
- 3.2 MatCap was added after lighting at 80% saturation instead of onto the
  albedo at 10%.
- 3.4 vertical gradient was not implemented; the model's four UV sets were
  unused beyond UV0.
- 3.5.1 Fresnel-step rim properties existed but were not consumed by the
  forward passes.
- 3.10 outlines ignored the vertex-color green width mask and used a
  reference-distance response instead of the tutorial's near/far formula.

## 2.3.0 changes

- WuwaCommon: tutorial formula functions with finite guards (see plan
  `docs/plans/wuwa-tutorial-compliance-2.3.0.md`).
- Body/Hair/Face/Eye: new additive properties and behavior listed in the
  changelog; Face defaults enable the SDF soft channel and hair shadow;
  Eye responds to the main light.
- Shared outline helper: `MikuGameToonOutlinePositionCSWithDistanceMultiplier`
  overload so family-specific distance formulas compose with the vertex-color
  mask.
- Editor: recommended-profile defaults and an idempotent Wuwa hair-shadow
  feature installer.

## Results

Executed 2026-08-10 against the local validation project (Unity 6000.4.5f1,
URP/Shader Graph 17.4.0, Windows D3D12):

- `python tools/ci/run_checks.py --profile pr`: passed. 275 Python tests OK,
  package-identity boundary check passed, Blender extension and Unity TGZ
  rebuilt from canonical source.
- Unity `ShaderUtil.GetShaderMessages` for `MIKU/Wuwa/Body`, `Hair`, `Face`,
  `Eye`, and `Effect`: zero messages after the `isfinite` warning was removed.
- Unity EditMode `Miku.ShaderConverter.Editor.Tests.MikuWuwaTutorialTests`:
  9/9 passed in the first executed run (job `545d6c9c`). The final 10th test
  (public material-tools wrapper) was added afterward; its code path was
  exercised by the editor automation that configured all nine validation
  materials, and the shared-editor MCP test runner became unresponsive for the
  final re-run, so the final class execution is recorded as implemented but
  not re-executed in that environment.
- Full package EditMode assembly run: 282 tests executed. Remaining failures
  were confined to the concurrently edited Genshin/HSR tutorial tests and the
  shared outline-consumer contract, which were mid-flight in another editor
  session and are not part of this Wuwa change.
- Deterministic build: two consecutive `com.miku.shaderconverter-2.3.0.tgz`
  builds are byte-identical with SHA-256
  `d2b14abe1b6ace7739b451d8694887924333b1c12279dd490892035e6b0b3a43`.
- Validation assets: nine screenshots saved under the validation project's
  `Assets/Screenshots/` (`FeibiTutorial_Front`, `Face`, `Side`, `Back`,
  `FaceLight100/145/190`, `HairCloseup`, `OutlineCloseup`) plus the scene
  `Assets/鸣潮/Validation/菲比_教程验证.unity` and the versioned materials
  under `Assets/鸣潮/Materials/菲比_2.3.0/`. Pixel checks confirm the character
  renders with no magenta error shader and no black frames.
- Renderer feature recovery: `Assets/Settings/PC_Renderer.asset` was restored
  to the pre-existing `ScreenSpaceAmbientOcclusion` + `MikuToonScreenRimRendererFeature`
  set and then gained the Wuwa hair-shadow feature through the public
  installer. A final front screenshot confirms the scene still renders after
  the recovery.

## Known limitations

- The SDF shadow offsets/softness and the vertical-gradient strength were
  wired to tutorial defaults but final visual calibration is still
  recommended; the face material exposes `_FaceSdfDebugMode` 1-5 for channel
  and faceLight inspection.
- The validation editor was shared with concurrent HSR/Genshin work, so
  light-yaw screenshots were captured in a contended session; re-run the
  scene when the editor is quiet if stricter A/B evidence is needed.
- Outline width applies the tutorial near/far formula as a multiplier on
  `_OutlineWidth`; absolute width is still material-calibrated.
- The validation project's `PC_Renderer.asset` previously also contained a
  `MikuHSRCharacterRendererFeature` entry whose script GUID collided with a
  retired MiGR meta; that broken entry was removed during renderer recovery
  and must be reinstalled by the HSR workstream after its meta GUID is fixed.
  The Wuwa hair-shadow and screen-rim features are verified active.
- The validation project's `Packages/manifest.json` points at the canonical
  package as an embedded local package (`file:../../../项目4/...`); the
  canonical TGZ remains the deterministic release artifact.
