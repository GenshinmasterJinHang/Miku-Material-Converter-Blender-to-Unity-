# ADR 0017: Opt-in Endfield tutorial lighting and project-owned LUT setup

- Status: Accepted
- Date: 2026-08-09

## Decision

Endfield's tutorial-complete lighting is activated by one execute-always scene
controller that publishes namespaced shader globals. When no controller is
available, Endfield materials retain the 2.2.12 lighting path. New controls are
additive material properties, and existing shader/property identities are not
renamed.

The nine-part workflow remains unchanged. Lit transparent clothing is an
opt-in mode of the existing Overlay part. Its public `_LightingMode` contract
is `0=LegacyUnlit` and `1=ToonLitTransparent`; the default remains zero.
The scene controller gates the tutorial contribution inside the lit path, but
does not silently change `_LightingMode`. EyeShadow and historical unlit
overlays therefore keep their serialized behavior.

Full-screen Endfield grading uses URP's built-in full-screen renderer feature
before post processing. The installer references a user-selected project LUT,
validates a 1024 by 32 flattened 32-cube import, and never uses the ColorLookup
Volume override. Game textures and project materials/scenes are validation
inputs and are excluded from Miku release archives.

## Consequences

- MaterialIR, Bundle, bake, and Blender interchange schemas do not change.
- One controller owns the Endfield global state; duplicates are diagnosed and
  do not race to write globals.
- Renderer-feature, profile, material, and scene setup is idempotent,
  Undo-aware, and rollback-safe. The target project keeps baseline scene,
  material, and mesh assets.
- The package may ship shaders, runtime/editor setup code, tests, and profile
  factories, but never the selected game LUT itself.
