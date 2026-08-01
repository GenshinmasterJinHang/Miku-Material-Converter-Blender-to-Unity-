# MiGR 2.0.2 release notes

MiGR 2.0.2 restores Magic Ball 1-5 export and import without dropping Bump
normals or Principled Coat. The semantic exporter and Unity package are
released together as 2.0.2. The GPL bake worker remains 1.1.1 because its
existing Blender semantic mesh-bake protocol already supports the required
Normal-channel job.

## Changes

- A runtime-independent `Vector.Bump` chain on the global surface Normal is
  emitted as one channel-scoped `MeshBake` job. Chained Bump nodes and Bump
  inside node groups are evaluated by Blender rather than approximated in
  Shader Graph.
- `NativeOnly` now reports that Bump Normal requires MeshBake. Bump graphs
  driven by Time, View, or another true runtime input remain unsupported.
- A single supported scattering closure can own the global surface Normal.
  Distinct normals on multiple active lobes remain unsupported.
- The safe Principled Coat subset maps Coat Weight to URP Coat Mask and Coat
  Roughness to `1 - roughness` and URP Coat Smoothness. Coat IOR must be 1.5,
  Coat Tint must be white, and Coat Normal must remain at its default.
- Clear Coat lowering records `MIGR_COAT_URP_APPROXIMATION`. It is allowed by
  `AllowDeclaredApproximation` and rejected by `Strict`.
- Unity 6000.4.5f1 with URP and Shader Graph 17.4.0 uses a Unity-authored
  Universal Lit Clear Coat wrapper template with `m_ClearCoat=true`, Coat Mask,
  and Coat Smoothness blocks.
- Existing user-owned wrappers that lack the required Clear Coat contract are
  not overwritten. They require Full Regeneration, which retains the existing
  GUID, backup behavior, and material bindings.
- Unity 2.0.2 accepts the known 2.0.0/2.0.1 target profile only for materials
  without Coat. A Coat bundle carrying the old profile is rejected and must be
  re-exported with 2.0.2.

## Compatibility and public surfaces

- Blender: 5.2.0.
- Unity Editor: 6000.4.5f1.
- Universal Render Pipeline: 17.4.0.
- Shader Graph: 17.4.0.
- Validated graphics API: D3D11 on Windows.
- Material IR, Bundle, and Conversion Plan remain schema 2.0.
- No SurfaceModel enum, required interchange field, or public shader property
  reference name changed.
- Blender and URP Clear Coat BRDFs are not physically identical. The generated
  graph therefore declares an Approximate translation and uses the URP Complex
  Lit forward-only path.

## Validation evidence

- Python 3.13: 142 unit tests passed.
- Repository `pr` and `release` CI profiles passed, including canonical source
  boundary, Python parse, schema, package identity, test, and build checks.
- Blender 5.2.0: Magic Ball corpus smoke test passed; Magic Ball 1-5 each
  completed their Normal bake, two exports were byte-identical, and the Magic
  Ball 10 Light Path negative case remained enforced.
- Unity 6000.4.5f1: 81 EditMode tests passed under D3D11, including Clear Coat
  template selection, graph import, shader compilation, wrapper ownership,
  profile compatibility, and deterministic output.
- End to end: actual Magic Ball 1-5 bundles exported from Blender were imported
  into an isolated Unity project with no export/import errors, no shader
  compilation errors, and no pink materials. A D3D11 comparison render used the
  exact source meshes under matched camera and light placement; the baked
  surface detail and Clear Coat highlights were simultaneously visible.
- Release archives were built twice from canonical source and had identical
  SHA-256 values. See `migr-2.0.2-sha256.txt`.

The release archives are build outputs. Do not install the Blender extension
over a running Blender process with unsaved work, and do not replace an embedded
Unity package while its project has unsaved changes.
