# Cycles crystal reference scene procedure

This document is the reproducible reference-scene definition for the
`cycles-optical-1.0` subset. It is not evidence that visual parity has already
been certified. Record captures and measured comparisons separately before
changing compatibility status from Experimental.

## Validated tool tuple

- Windows;
- Blender 5.2.0 LTS for the current source-corpus export (Blender 5.0 remains
  unexecuted for this slice);
- Unity Editor 6000.4.5f1;
- Universal RP 17.4.0;
- Shader Graph 17.4.0;
- Linear color space and URP Camera Opaque Texture enabled.

## Scene construction

Create `Assets/Miku/Tests/Scenes/CrystalReferenceScene.unity` in a disposable
verification project. Do not check it in until it has been reviewed in the
exact version tuple above.

1. Place a matte checker floor and a vertical backdrop containing saturated
   red, green, blue, white, and black patches. The backdrop must be visible
   through every transparent object so refraction direction is reviewable.
2. Add one neutral Directional Light and one baked or realtime Reflection Probe
   enclosing all specimens. Use a non-uniform environment or cubemap so probe
   reflection cannot be confused with a constant highlight.
3. Add six named, outward-wound specimens at unit scale: `ClearGlass` smooth
   sphere, `FrostedGlass` normal-mapped sphere, `GreenBottle` closed bottle,
   `Diamond` low-poly flat-shaded gem, `Sapphire` faceted gem, and `ThinGlass`
   UV-mapped slab. Keep a seventh intentionally open or non-uniformly scaled
   mesh only for diagnostic checks.
4. Arrange the specimens in two rows centered around `(0, 2, 0)`. Use a
   perspective camera at `(0, 2, -10)`, rotation `(0, 0, 0)`, vertical field of
   view 50 degrees, near plane 0.1 m, far plane 100 m, and 1920 x 1080 output.
   Avoid post-processing, depth of field, motion blur, and auto exposure.
5. Use one Directional Light at rotation `(50, -30, 0)` and intensity 1. Place
   the Reflection Probe at `(0, 2, 0)` with size `(8, 5, 6)`. Record any chosen
   HDRI/cubemap and its exposure; do not use a uniform-color environment.
6. Assign representative generated materials from the real corpus: one Glass,
   one Fresnel reflection/refraction material, one mixed dielectric, and one
   Principled Transmission gem. Add a small fixture material for Emission plus
   Glass and another for Volume Absorption with a thickness texture.
7. For the texture fixture, use a Linear/Non-Color 0-to-1 ramp on UV0, red
   channel, `_ThicknessMapScale = 1 m`; keep `_Thickness` visible as the missing
   texture fallback control.

## Acceptance checks

- Every wrapper and generated Sub Graph imports with no Shader Graph compiler
  messages; the wrapper remains user-owned on regeneration.
- Increasing roughness decreases generated smoothness (`1 - roughness`).
- Increasing roughness visibly broadens or weakens the reflection/refraction
  detail without changing the invariant above.
- Increasing IOR strengthens Fresnel at grazing angles without turning the
  closure into an ordinary color Lerp.
- The normal map on `FrostedGlass` changes the refraction direction, while
  `Diamond` and `Sapphire` retain their authored flat facets.
- Refraction samples the opaque backdrop, and disabling Camera Opaque Texture
  produces `MIKU_URP_OPAQUE_TEXTURE_REQUIRED` rather than a success claim.
- Increasing absorption density or thickness monotonically decreases RGB
  transmittance. Absorption color does not alter coverage alpha.
- The thickness ramp changes absorption/refraction distance across the UV-mapped
  sphere; deleting its image retains `_Thickness` and reports
  `MIKU_CRYSTAL_THICKNESS_TEXTURE_MISSING`.
- Reflection Probe rotation or environment changes alter reflection while the
  refraction sample remains tied to screen-space scene color.
- Open geometry, inconsistent normals, and non-uniform scale produce their
  structured mesh diagnostics; the converter does not mutate geometry.
- Transparent sorting, intersecting surfaces, objects absent from Camera Opaque
  Texture, nested media, caustics, dispersion, and multi-bounce internal
  reflection are recorded as limitations, not parity successes.

## Evidence to retain

Keep the exact source `.blend` path, exported `.miku`, generated wrapper,
generated Sub Graph, `.migrmap.json`, `.migrreport.json`, Unity Editor log,
scene GUID, render-pipeline asset GUID, camera transform, probe settings, and
paired Blender/Unity lossless captures. Name evidence by material and exact
version tuple. Do not update goldens solely because a comparison failed.
