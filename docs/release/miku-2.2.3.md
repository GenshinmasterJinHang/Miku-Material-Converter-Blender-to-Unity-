# Miku Unity package 2.2.3 release validation

Miku 2.2.3 targets Unity 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0, and
Windows D3D11. It restores the Endfield directional Main Light before applying
Face, Eye, Hair, Skin SSS, metal, and reflection fidelity repairs.

## Compatibility and public contracts

- MaterialIR remains schema 2.0.
- Existing shader names, texture roles, property references, material slots,
  and `_EyeMode=0/1` remain compatible.
- New roles: `SpecularRefineF0` and `SpecularRefineColor`.
- New Face controls: `_UseManualFaceBasis`, `_FaceRightOS`,
  `_FaceForwardOS`, `_FaceUpOS`, `_BackLightStrength`, `_BlushMaskGain`,
  `_SSSColor`, and `_SSSArea`.
- New Skin controls: `_SSSColor` and `_SSSArea`.
- New Body controls: `_SpecularRefineF0Tex`, `_SpecularRefineColorTex`,
  `_UseSpecularRefine`, `_SelfAoShadowStrength`, `_EnvironmentRotation`, and
  `_EnvironmentMipBias`.
- New Hair controls: `_SpecularRefineF0Tex`, `_UseSpecularRefine`, and
  `_SelfAoShadowStrength`.

## Validation contract

The release gate requires a non-zero Direct Only image with SH disabled, light
direction response, black non-emissive output when key and indirect intensity
are both zero, independent SH Only output, correct Rendering Layer exclusion,
finite HDR output, zero shader/Console errors, and two byte-identical canonical
package builds. Validation evidence is recorded in the active ExecPlan and the
final archive hash is recorded in `miku-2.2.3-sha256.txt`.

The implementation plan is
`docs/plans/endfield-2.2.3-main-light-rendering-fidelity.md`. Private character
textures and screenshots are validation inputs only and are not distributed.

## Recorded result

- PR profile: 228 tests passed.
- Unity EditMode: 135 total, 133 passed, 0 failed, 2 ignored.
- Installed package: 2.2.3.
- Endfield shader compiler errors: 0.
- Unity Console errors after final clear and validation: 0.
- 512x512 ARGBHalf finite scan: 0 NaN/Infinity; maximum absolute channel value
  1.105469.
- Isolated Body gate: Direct sum 597.6261; 90-degree light rotation difference
  217.3084; zero-light and layer-mismatch maximum luminance both 0.1374492
  (outline baseline); SH Only sum 211.1937.
- Deterministic TGZ SHA-256:
  `ef13a3bc32c8f6610729ea48d898e3a7ab75da7045d73245eed55b46fbf63a08`.
- Validation assets: fourteen cloned materials plus four distinct Body/Face
  Direct Only and SH Only screenshots. The pre-existing dirty scene was not
  saved and its original 2.2.2 material references were restored.
