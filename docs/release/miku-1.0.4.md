# Miku 1.0.4 release notes

Miku 1.0.4 fixes three `CustomMultiLobe` regressions without changing the
Material IR, bundle, plan, or manifest 1.0 schemas.

- Fully evaluated closure radiance now feeds Shader Graph Emission while Base
  Color remains zero, preventing a second URP lighting pass.
- Diffuse, Glossy, Metallic, Principled, and related surface lobes use their
  own Normal parameters, transformed from declared tangent or object space to
  world space at the lighting boundary.
- Blender's unconnected zero Normal and Coat Normal sentinel is normalized to
  `[0, 0, 1]`; explicitly connected expressions are never rewritten.
- The Unity importer preserves linear EXR and OpenGL tangent-normal settings,
  applies a white strength-1 emission multiplier to evaluated radiance, and
  reports `MIKU_LEGACY_CLOSURE_ZERO_NORMAL_NORMALIZED` when migrating affected
  Miku 1.0.3 bundles in memory.

The Unity package continues to accept the Miku 1.0.3 target profile beginning
`b9e8f39f`. Package ID, public Shader property references, user-owned wrapper
assets, and schema versions are unchanged.
