# Migrating HSR semantics to 1.1

`hsr-toon-1.1` is emitted by Blender add-on 0.6.1 and consumed by Unity package
0.9.1. The outer Miku versions remain `miku-2.0` and `miku-preset-2.0`.

## Changed semantics

- LightMap A is `MaterialRegionId`; `metallicLightMapTarget` and
  `metallicLightMapWidth` select the metal region.
- LightMap B is `SpecularControl`: it thresholds non-metal highlights and
  continuously scales metal highlights.
- HSR rim light is only `rimLightTintColor * rimLightBrightness` masked by
  view-angle Fresnel. It does not sample camera depth or multiply main-light
  color.
- UpperBody, LowerBody, Hair, and Face request smooth outline normals in UV7.
  Unity falls back to vertex normals with Approximate quality when UV7 is absent.

## Legacy documents

An HSR companion without `schema` is treated as legacy. If its complete control
object exactly matches the Blender add-on 0.6.0 factory defaults, Unity replaces
it with the 1.1 defaults and reports `hsr_legacy_factory_defaults_migrated`.
Otherwise, all authored values are preserved over the 1.1 defaults and Unity
reports `hsr_legacy_custom_controls_preserved`.

The old `rimLightWidth`, `rimLightThreshold`, and `rimLightFadeout` values remain
readable for material serialization compatibility. They do not affect the new
Fresnel result and cause `hsr_depth_rim_controls_deprecated`.

Any non-empty semantic version other than `hsr-toon-1.1` produces
`hsr_semantic_schema_unsupported` and stops material generation. Re-export the
material with a compatible Blender add-on instead of changing the version text
by hand.

## Outline workflow

Body, Hair, and Face outlines extrude geometry along UV7 smooth normals. The
Unity command **Tools > B2U > HSR > Validate UV7 Smooth Outline Normals** is
read-only. It reports missing, incomplete, or non-finite data and never bakes or
reassigns a mesh.
