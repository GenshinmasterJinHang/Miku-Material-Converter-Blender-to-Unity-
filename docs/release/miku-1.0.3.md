# Miku 1.0.3 release notes

Miku 1.0.3 coordinates the Python core, Blender 5.2 extension, and Unity 6 URP
package without changing Miku 1.0 document kinds or schema versions.

## Portable Hybrid

The existing public `PreferNative` identifier is displayed as **Portable
Hybrid (Prefer Native)**. Supported View Direction, Fresnel, Layer Weight,
Camera Data, and Miku Time expressions remain live in Unity Shader Graph.
Unsupported but statically proven UV0 expression islands are baked on an
internal canonical 0-1 UV plane into reusable Texture2D resources.

Portable Hybrid bundles do not contain SourceMesh assets, source topology,
mesh fingerprints, or `meshBinding`. Core, Blender worker, and Unity importer
all reject a request or bundle that mixes portable and mesh-bound resources.
Destination meshes must still provide UV0; different UV layouts naturally
change texture placement.

**Full PBR Bake (Source Mesh)** retains its complete source-mesh-bound PBR
channel semantics. Runtime view/camera/light-path/time dependencies are rejected
before the worker starts, with guidance to select Portable Hybrid.

Validated target: Blender 5.2.0 LTS (`fbe6228777e7`), Unity 6000.4.5f1,
URP/Shader Graph 17.4.0, Windows D3D11.
