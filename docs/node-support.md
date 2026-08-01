# Node support overview

The canonical Blender 5.2 EEVEE support table is the
[full node support matrix](node-support-matrix.md). This page summarizes the
cross-cutting rules used by all mapped nodes.

| Semantic family | Current path | Quality / limits |
| --- | --- | --- |
| Material Output / active surface | EEVEE-first deterministic resolver | Active `EEVEE`, then `ALL`; a required Cycles-only output fails clearly |
| Principled surface | ShaderGraph17UrpBackend | Expanded editable PBR channels; unsupported EEVEE features diagnose |
| Add/Mix closures | Per-channel semantic composition | Approximate; alpha, transparency, emission, and normals are not reduced to a color lerp |
| Roughness | One Minus | `smoothness = 1 - roughness` |
| Image Texture | Texture2D property + Sample Texture 2D | Exact for supported static flat sampling; advanced projection/animation snapshots diagnose |
| Checker / Gradient | Expanded native Shader Graph | Exact for documented Blender 5.2 modes |
| Procedural textures | Native graph, explicit approximation, Texture2D/Texture3D bake, or direction LUT | Route is selected per node and dependency; not every procedural requires baking |
| Normal/Bump/Height | Geometry normal, Normal Unpack, Normal From Height, Normal Blend | Normal maps are non-color tangent data; height/displacement remains approximate |
| Color Ramp | Native Gradient or generated LUT | Ordered RGBA keys and interpolation retained; limits diagnose |
| Texture Coordinate / Mapping | Space-aware native/expanded nodes | UV, Object, World, View, Tangent, and Screen are not interchangeable |
| Glass / Refraction | Transparent URP surface + Scene Color/probe approximation | Requires reported URP setup; no nested media/caustics parity |
| SSS / Translucent / Sheen / Toon / Holdout / Metallic | Editable URP approximations | Accepted to preserve authoring intent; each emits visual-difference diagnostics |
| Baked PBR / hybrid surface | Generated maps plus editable branch | Baked resources are the appearance-reference branch; Base Color is sRGB, data maps are Linear |
| Scene Color / Depth / derivatives | Fragment-only | A required vertex-stage use fails |
| Node groups | Flattened Miku semantics + stable visual grouping | Stable source/socket identities and deterministic generated IDs |
| Volume shaders | Deferred | No fake surface or silent absorption substitution |
| Specialized game presets | Separate compatibility backends | Preserved, but outside generic EEVEE Shader Graph coverage |

Translation quality is always one of:

- `Exact`
- `Equivalent`
- `Approximate`
- `Baked`
- `RequiresProjectSetup`
- `RequiresRuntimeSupport`
- `Unsupported`

An unsupported node outside the active output chain may warn and continue. An
unsupported node on a required chain blocks that material.

To add a node, follow
[adding node support](development/adding-node-support.md) and record source
socket types, coordinate space, shader stage, defaults, approximation,
required project setup, negative tests, and a Blender 5.2 construction fixture.
