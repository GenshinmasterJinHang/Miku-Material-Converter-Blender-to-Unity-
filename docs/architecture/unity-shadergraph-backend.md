# Unity Shader Graph backend

## Current implementation

The Unity package targets Unity 6000.4.5f1 and depends on URP 17.4.0. The
verification project resolves Shader Graph 17.4.0 transitively. Standard PBR
remains an editable `.shadergraph` wrapper plus a Miku-owned
`.generated.shadersubgraph`. Dedicated game presets keep their static
ShaderLab/HLSL paths.

Generic Toon is deliberately not a Shader Graph backend. ADR 0012 authorizes a
fixed, package-owned ShaderLab/HLSL family with eight semantic Shader names.
It creates base/derived Materials and a recipe, never a new Wrapper or Sub
Graph. Existing Generic Toon graphs remain user-owned legacy assets.

MaterialIR documents with runtime expressions select
`MikuShaderGraph17RuntimeBackend`, whose Shader Graph 17.4 reflection adapter is
isolated in the Miku Editor assembly. It creates native, editable nodes only:

- View Direction uses the World-space View Direction node.
- Camera Data expands Position(View), Split, negate/Combine/Normalize, Abs, and
  Length. It follows the camera rendering the pass and never caches
  `Camera.main`.
- Time computes `base = lerp(UnityTime, Override,
  saturate(UseTimeOverride))`, then Seconds, Frame, Sine, and Cosine using the
  four stable `_MIKU_Effect*` properties.
- Fresnel expands the complete dielectric equation. Layer Weight keeps separate
  Fresnel and Facing expressions and Blender's exact half-blend branch.

The backend rejects a fragment expression that reaches a Vertex channel with
`shader_stage_conflict`. It never emits a Custom Function for these operations.
Generated object and slot IDs are stabilized after Shader Graph MultiJson
serialization so repeated generation remains byte-stable.

Miku MaterialIR 1.0 is dispatched through `ISurfaceGraphGenerator`. The registered
surface kinds are Opaque PBR, Cutout PBR, Transparent Lit, Transparent
Emission, Refractive Glass, and Custom Multi-Lobe. The last three do not
collapse independent closure parameters into one Standard Lit record.

`MikuMultiLobeLighting.hlsl` evaluates each supported scattering term
independently using URP 17.4 lighting APIs. It includes main and additional
lights, shadows, cookies, SH/light probes, Forward and Forward+, while the URP
Unlit target applies fog after the custom radiance. Scalar pass-through uses
one premultiplication. Colored transmittance samples Scene Color and requires
Opaque Texture.

The custom lighting path currently cannot consume screen-space ambient
occlusion. Auto reports `MIKU_CUSTOM_LIGHTING_SSAO_UNAVAILABLE`; Strict rejects
the path. Linked per-lobe normals are also rejected until each can be evaluated
and transformed independently.

On initial generation or explicit Full Regeneration, the version adapter derives
the runtime Sub Graph property interface and adds matching hidden material
properties plus Property-node connections to the wrapper. This makes the four
Time references and externalized driver parameters addressable on the final
Shader/Material. A pre-existing user wrapper is never rewritten implicitly; if
it predates required runtime properties, Miku preserves it and reports
`MIKU_RUNTIME_WRAPPER_PROPERTIES_MISSING`.

## Required target design

A backend key contains exact Unity Editor, URP, Shader Graph, render pipeline, and
template-fixture versions. Selection either returns a compatible adapter or a
structured `Unsupported` diagnostic. It must not select a nearby version by
guessing.

`ShaderGraph17_4UrpBackend` is based on minimal assets created by Unity
6000.4.5f1 with Shader Graph 17.4.0. Normalized fixtures record provenance and
reviewable semantic expectations. Internal class names, object IDs, slot IDs,
targets, and serialization fields are isolated in the adapter/serializer; core
semantic lowering knows none of them.

The implemented assembly is a versioned wrapper template plus a generated subgraph.
The backend validates the imported asset and template compatibility in EditMode.
Malformed MultiJson or a fixture mismatch is an error, not a ShaderLab fallback.

Standard PBR exposes twelve public properties. Generated internal properties stay
in the wrapper with their stable references and `UnityPerMaterial` declaration,
but are non-exposed and hidden. Hidden Texture2D properties disable unused
tiling/offset, texel-size, and HDR helper declarations; adding a consumer of one
of those helpers requires a new compatibility fixture and SRP Batcher validation.

An existing Standard wrapper is automatically presentation-migrated only when
its bytes match the former Generic wrapper after deterministic Sub Graph GUID
substitution. Modified wrappers remain user-owned and require explicit Full
Regeneration.

The Cycles crystal route adds an optical subgraph with Scene Color refraction,
Schlick Fresnel, Reflection Probe reflection, explicit constant or texture
thickness, and Beer-Lambert absorption. Other Shader Graph internal formats
still require separate verified adapters rather than guessed compatibility.
