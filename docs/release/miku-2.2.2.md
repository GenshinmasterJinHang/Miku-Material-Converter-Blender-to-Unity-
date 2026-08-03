# Miku Unity package 2.2.2 release validation

Miku 2.2.2 is a compatible Endfield rendering regression repair for Unity
6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0, and Windows D3D11.

## User-visible changes

- Body, Skin, Face, Eye, and Mouth retain authored shadow tones when system
  shadow visibility is zero instead of multiplying final direct color to black.
- Face SDF keeps its authored light shape and falls back to 70% geometric light
  where the SDF has no usable value.
- Iris materials use the parallax-adjusted authored Base Map; Sclera materials
  use an independent warm white and never inherit iris RGB.
- Cloth metal and hair accessories retain finite colored direct/environment
  response when the packed AO channel is zero.

## Compatibility

- MaterialIR remains 2.0; no JSON schema or public C# data structure changed.
- Texture roles, packed-map channels, Shader property names, material slots,
  `_AlphaSource` values, and `_EyeMode` values remain unchanged.
- Existing 2.2.1 packages and validation materials are not modified.

## Validation record

Automated commands, Unity results, deterministic archive hash, and blocked or
intentionally omitted checks are recorded in
`docs/plans/endfield-2.2.2-main-light-face-eye-metal-regressions.md`.
The final archive hash is recorded in `miku-2.2.2-sha256.txt` after two
identical canonical builds. Per the validation agreement, no screenshots or
`Validation/2.2.2` assets are generated; final appearance is manually accepted
by the user.
