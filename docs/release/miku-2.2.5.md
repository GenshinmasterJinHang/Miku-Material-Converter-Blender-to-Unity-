# Miku 2.2.5

Miku 2.2.5 targets Unity Editor 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0,
and Windows D3D11.

## Rendering changes

- Genshin, HSR, and Wuwa Body/Face materials gain authored-mask skin SSS and
  restrained warm-pale skin grading. Face-only Base Map fallback is allowed for
  Genshin and Wuwa; Body materials never infer skin from clothing color.
- Wuwa FaceID now enables `_WUWA_ID_ON`. The recommended Wuwa Face calibration
  removes the former 1.5 base-brightness lift and balances Face against Body.
- Genshin keeps its reference curve and HSV-value intent but carries
  non-clipped light energy to one final hue-preserving shoulder. Explicit
  emission is added afterward. `_HighlightCompression=0` is the legacy visual
  fallback.
- Endfield keeps its existing SSS controls; an enabled Face Refine map now uses
  its red channel as the actual SSS mask.

## Authoring and migration

Only Miku-owned `generatedBaseMaterial` assets migrate automatically and only
once when their recipe version advances to 2.2.5. User material variants are
not changed. Existing ordinary materials can be updated explicitly with the
Undoable **Apply Recommended Skin & Highlight Profile** menu.

`Runtime/Profiles/MikuAnimeGlobalVolumeProfile.asset` contains the shared ten-
component grade in this order: Neutral Tonemapping, White Balance, Channel
Mixer, Lift/Gamma/Gain, Shadows/Midtones/Highlights, Split Toning, Color
Curves, Color Adjustments, Bloom, and Vignette. Miku does not inject the
profile at runtime.

The Mesh menu keeps **Smooth Normal Generator** and removes the
**Vertex Color Initializer** and **Combined Mesh Data** entries. Their public
editor types and lower-level mesh APIs remain available for existing scripts.

## Compatibility

MaterialIR, JSON Schema, texture roles, shader names, and existing material
property references are unchanged. New SSS and Genshin highlight properties
are additive shader interfaces. Blender export behavior is unchanged.

The canonical archive is `dist/com.miku.shaderconverter-2.2.5.tgz`. Its final
SHA-256 is recorded in `miku-2.2.5-sha256.txt` after two byte-identical builds.
