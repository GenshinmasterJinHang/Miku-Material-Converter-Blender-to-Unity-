# Miku 2.1.0 release notes

Miku 2.1.0 adds the fixed `endfield_toon` workflow for the validated Blender
5.2.0 to Unity 6000.4.5f1 / URP 17.4.0 tuple. It includes nine material parts,
strict semantic texture roles, user-owned material templates, texture import
auditing, UV7 smooth-outline data generation for non-readable imported meshes,
and the shared Game Toon screen-space rim contract.

Hair shadow is implemented as texture-driven transparent overlay geometry
clipped by the face stencil. It does not use a shadow camera or the WuWa hair
depth feature. Existing Genshin, WuWa, and HSR Shader/property identities are
unchanged. MaterialIR remains schema 2.0; `endfield_toon` is an additive enum
value, while MaterialIR 1.0 remains frozen.

The Endfield implementation is clean-room first-party code. Validation assets
are not part of the release archive.
