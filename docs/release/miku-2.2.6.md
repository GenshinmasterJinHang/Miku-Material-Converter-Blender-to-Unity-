# Miku 2.2.6

Miku 2.2.6 targets Unity Editor 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0,
and Windows D3D11.

## Wuwa material fidelity

- Eye samples one linear RGB `_EyeHET` mask twice for independently movable
  upper and lower highlights. Existing highlight color/strength references are
  retained, `_EmissionStrength` controls highlight HDR energy, and
  `_EyeBaseEmissionStrength` controls the eye base separately. The calibrated
  profile uses `0.32`/`0.22` scales, a `0.7` threshold, highlight emission `1`,
  and no base-eye emission.
- Face exposes `_FaceRight`, `_FaceUp`, and `_FaceForward` in object space,
  transforms them through the renderer object matrix, repairs the orthonormal
  basis, and preserves `_MikuHead*WS` runtime overrides.
- Body uses one authored ID sample for ordinary ID shading and opaque
  view-dependent sheer stockings. `IDMap` and `StockingsMap` must reference the
  same texture. `_BodyEmissionStrength` scales the emission sample without
  affecting the texture-presence keyword; its default is `1`, and Body MatCap
  strength defaults to `0.15`.
- The recommended profile brightens Wuwa Hair and adds a primary Effect-layer
  brightness control. It is applied automatically only to Miku-owned generated
  base materials when their recipe advances to 2.2.6.

## Compatibility

MaterialIR remains 2.0. Bundle, plan, manifest, and target-profile schemas
remain unchanged. `EyeHET`, `IDMap`, and the existing cross-workflow
`StockingsMap` role are reused; no `EyeBottomHighlight` role is introduced.
Legacy Wuwa Eye `_EyeEG` and `_EmissionMap` properties remain serialized but no
longer contribute to Eye shading. User-owned material variants are not
automatically overwritten.

The canonical Unity archive is `com.miku.shaderconverter-2.2.6.tgz`. Its final
SHA-256 is recorded after two byte-identical builds.
