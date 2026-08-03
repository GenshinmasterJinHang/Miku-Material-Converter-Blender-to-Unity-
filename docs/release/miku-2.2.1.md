# Miku Unity package 2.2.1 release validation

Miku 2.2.1 is a compatible Endfield rendering repair for Unity 6000.4.5f1,
URP 17.4.0, Shader Graph 17.4.0, and Windows D3D11.

## User-visible changes

- Eye-shadow raw-red coverage clips from bottom to top; brows and lashes can use
  opaque submesh coverage.
- Iris rendering uses authored color with shallow anime cornea parallax,
  view-space MatCap, and bounded non-emissive highlights.
- Face SDF and skin lighting remain readable, and the existing Emotion Atlas
  can supply an independently controlled cheek blush.
- Cloth metal and inverse hair-specular-mask accessories retain visible direct
  and environment reflection without changing packed texture channels.

## Compatibility

- MaterialIR stays at 2.0 and no JSON schema changes.
- Existing texture roles, shader property names, material slot order, and
  `_AlphaSource` values 0, 1, and 2 remain unchanged.
- New `_AlphaSource` values are 3 for raw red and 4 for opaque coverage.
- Blush defaults to zero so existing materials do not change until enabled.
- The cornea is a single-layer anime approximation, not refractive geometry.

## Validation record

The exact commands, Unity results, graphics evidence, deterministic archive
hash, and any blocked checks are recorded in
`docs/plans/endfield-2.2.1-eye-face-metal-regressions.md`. The final archive
hash is recorded in `miku-2.2.1-sha256.txt` after two identical canonical
builds.
