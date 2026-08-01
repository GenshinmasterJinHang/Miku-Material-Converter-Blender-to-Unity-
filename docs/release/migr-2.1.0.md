# MiGR 2.1.0 release notes

MiGR 2.1.0 prevents mesh-bound Blender bake textures from being applied to an
unrelated Unity mesh. The Semantic Exporter and Unity package are 2.1.0; the
GPL Bake Worker is 1.2.0.

## Portable Auto

`Auto`, `PreferNative`, and `ReusableBakeOnly` do not emit
`meshBindingRequired` Texture2D resources. Object Position, Point Mapping, 3D
Noise Factor, scalar math, Color Ramp, and Normal From Height remain editable
runtime expressions. The clean-room Noise implementation is Approximate and
Strict rejects it.

Wireframe, Noise Color, non-Point Mapping, and unverified mesh-dependent
expressions fail with `MIGR_PORTABLE_MESH_BAKE_REQUIRED`; no black constant,
pseudo-color, or screen-derivative substitute is emitted.

## Source Mesh Fidelity

The existing `AllowMeshBake` identifier is presented as **Source Mesh
Fidelity**. Its Bundle 2.1 contains:

- mesh-bound Texture resources;
- one deterministic evaluated static GLB (`model/gltf-binary`);
- a `MeshFingerprintSet`;
- renderer paths and material slot bindings.

Unity verifies every artifact hash, mesh/vertex/index/UV count, binding
fingerprint, and renderer slot before commit. glTFast 6.19.0 creates stable
Mesh assets, a binding-description asset, and the authoritative Prefab.
Applying the material to another selected Renderer is allowed only after its
Unity mesh fingerprint matches.

Armatures, animation, and runtime-deformed meshes remain unsupported.

## Compatibility

MaterialIR, the Value Graph, and public Shader property references remain 2.0.
Portable exports continue to use Bundle 2.0. Safe older Bundle 2.0 documents
remain readable; a Bundle 2.0 texture carrying `meshBinding` is rejected with
`MIGR_LEGACY_MESH_BOUND_BUNDLE_UNSAFE` because it has no sealed source mesh.
Known 2.0.2 and 2.0.3 target profiles remain explicitly accepted within those
safety rules.
