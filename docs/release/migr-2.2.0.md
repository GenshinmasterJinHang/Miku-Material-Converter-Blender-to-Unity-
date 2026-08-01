# MiGR 2.2.0 release notes

MiGR 2.2.0 adds direct translation for ordinary static PBR image workflows in
Blender 5.2. Supported PNG, JPEG, and EXR images remain sealed Texture2D
resources in editable Unity Shader Graph 17.4 output; the exporter does not
invoke the GPL Bake Worker for these paths.

## Supported authoring route

- Color/Albedo: `sRGB` Image Texture to Principled Base Color.
- Roughness and Metalness: `Non-Color` Image Texture to the corresponding
  Principled inputs. Unity still computes `Smoothness = 1 - Roughness`.
- Normal: `Non-Color` image through a Tangent Space Normal Map node to
  Principled Normal. OpenGL positive-Y is the default; DirectX negative-Y is
  an explicit material setting and is never inferred from a filename.
- Height: `Non-Color` image through an Object Space Displacement node to
  Material Output Displacement.
- Ambient Occlusion: `Non-Color` scalar multiplied with Base Color. MiGR
  recognizes the connected multiply topology and emits
  `BaseColor * lerp(1, AO, OcclusionStrength)`.
- Colored Emission: color image to Principled Emission Color, multiplied by
  the authored Emission Color/Strength. HDR, tone mapping, and Bloom remain
  Unity project settings; no artificial whitening curve is generated.
- Emission Mask: scalar image as the Mix Shader factor between Principled and
  Emission BSDF. Weights remain exactly `0 = Principled`, `1 = Emission`.
- Alpha: scalar image to Principled Alpha. Blended selects Alpha Blend,
  Dithered selects the versioned dither/cutout wrapper, and an otherwise
  Opaque material with effective linked Alpha is promoted to Alpha Blend with
  a conversion diagnostic.
- Blender material displacement method `BUMP`, `DISPLACEMENT`, or `BOTH`
  selects fragment bump, Vertex Position, or both.

The direct image subset uses Flat projection, implicit active UV/UV0,
Closest/Linear sampling, and Repeat/Extend wrapping. Displacement Normal must
be unlinked and Midlevel/Scale must be finite constants. True displacement
requires enough model subdivision; MiGR reports this as project setup rather
than mutating the mesh.

Explicit Separate Color/XYZ and Image Alpha topology can bind several scalar
semantics to one physical Linear texture. Supported bindings are Metalness,
Roughness, Ambient Occlusion, Height, Alpha, and Emission Mask. An explicit
Invert becomes an editable One Minus expression. Ambient Occlusion is applied
once to Base Color through `_OcclusionStrength`; the URP Occlusion output is
neutralized to avoid double darkening.

## Contracts

- Blender Semantic Exporter: 2.2.0.
- Unity package: 2.2.0.
- GPL Bake Worker: unchanged at 1.2.0.
- New Bundle kind: `migr-bundle-2.2`.
- New Bundle capabilities: `image/jpeg`, Height/EmissionMask, explicit scalar
  channel bindings, and DirectX tangent-normal metadata.
- MaterialIR, Conversion Plan, Conversion Manifest, and Target Profile remain
  2.0.
- New public properties: `_MIGR_HeightMap`, `_MIGR_HeightMidlevel`, and
  `_MIGR_HeightScale`, `_OcclusionMap`, `_MIGR_EmissionMask`, `_Opacity`,
  `_AlphaClipThreshold`, `_MIGR_BumpStrength`, and `_MIGR_BumpDistance`.
- Each packed image receives one deterministic `_MIGR_Packed_*` Texture2D
  property. Its Inspector label lists the R/G/B/A semantic mapping while
  semantic strength properties remain independent.
- No existing public property was renamed.

Unity 2.2.0 continues to read safe Bundle 2.0/2.1 documents. Older Unity
packages reject Bundle 2.2 as unknown instead of guessing its meaning.

## Explicit limits

UDIM/tiled, image sequence, movie, generated image, non-Flat projection,
custom linked Vector mapping, Cubic/Smart filtering, Clip wrapping,
non-tangent normals, World-space displacement, linked Displacement Normal,
dynamic Midlevel/Scale, automatic tessellation, runtime-deformed
skinning-aware displacement, and Shader Graph versions other than 17.4.0 are
outside this release. Required occurrences fail with structured diagnostics;
they are not silently baked or replaced.
