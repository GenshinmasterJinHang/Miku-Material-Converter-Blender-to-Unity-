# Miku 2.2.7

Miku 2.2.7 targets Blender 5.2.0 and Unity Editor 6000.4.5f1 with URP and
Shader Graph 17.4.0 on Windows.

## Wuwa Eye

- `EyeHET` is a direct linear emission mask. White emits, black does not, and
  gray remains proportional. Sclera and pupil have independent HDR colors and
  strengths, separated by inverse HDMF alpha.
- HDMF red supplies the primary highlight. Green, blue, alpha, BaseMap alpha,
  and the derived pupil mask are available in debug views; HDMF blue does not
  affect final shading.
- `EyeUpperHighlight` and `EyeLowerHighlight` restore the two authored images.
  Their static Blender UV0 Point Mapping is transported as an Affine2D bundle
  binding and persisted in the Unity material recipe.
- Optional `EyeEG` uses a Fresnel term and the Main Light projected into the
  mesh tangent basis. Missing textures disable the keyword; invalid tangents
  produce zero motion rather than NaN output.

## Global Volume Profile

The reusable URP profile keeps White Balance, channel mixing, hue, and color
filters neutral. It uses a master luminance curve, Exposure `+0.35`, Contrast
`+16`, Saturation `+8`, and white Bloom at threshold `0.85` with intensity
`0.20` for a brighter image and restrained glow while retaining authored
character colors and material effects.

The 2.2.6 single-HET double-highlight path is removed. Existing Eye materials
therefore adopt the corrected HET meaning immediately. A 2.2.6 recipe displays
`MIKU_WUWA_EYE_2_2_6_REIMPORT_REQUIRED` until re-export/re-import binds the new
roles and transforms.

## Interchange and compatibility

`miku-bundle-1.0` remains the document family. The role enum gains
`EyeHDMF`, `EyeUpperHighlight`, and `EyeLowerHighlight`; fixed material
bindings gain optional UV0 Affine2D metadata. These are additive for 2.2.7 but
older strict consumers reject them, so exporter and importer must be upgraded
together. Package IDs and existing shader property references are unchanged.

Blender Shader-to-RGB energy mixing has no exact URP equivalent. Texture
selection, channel direction, mask ramps, and UV transforms are preserved;
the final lighting composition remains an explicitly documented
Equivalent/Approximate translation.

## Validation

Public tests use generated textures and node graphs. The privately supplied
Phi blend is a read-only local validation input and is not distributed. Final
test counts, deterministic archive hashes, and any visual-validation limits
are recorded in `docs/plans/wuwa-eye-rendering-2.2.7.md`.
