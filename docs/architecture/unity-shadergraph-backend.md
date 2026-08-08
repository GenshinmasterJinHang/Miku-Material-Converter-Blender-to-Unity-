# Unity Shader Graph backend

## Current implementation

The Unity package accepts the bounded Unity 6000.0-6000.5 technical lines with
a certified reference of 6000.5.7f1 / URP 17.5.4 / Shader Graph 17.5.4. Its
manifest uses the install floors Unity 6000.0 and URP 17.0.0; each project
directly locks the exact matching 17.N URP and Shader Graph packages. Standard PBR
remains an editable `.shadergraph` wrapper plus a Miku-owned
`.generated.shadersubgraph`. Dedicated game presets keep their static
ShaderLab/HLSL paths.

The three game Toon workflows are deliberately not Shader Graph backends. They
use package-owned ShaderLab/HLSL assets and shared Game Toon Screen Rim tools.
Generic Toon is retired; old inputs fail before a backend or asset transaction
is selected.

MaterialIR documents with runtime expressions select
`MikuShaderGraph17RuntimeBackend`, whose explicit Shader Graph 17.0 through
17.5 reflection adapters are isolated in the Miku Editor assembly. Unknown
minors are rejected instead of clamped. It creates native, editable nodes only:

- View Direction uses the World-space View Direction node.
- Camera Data expands Position(View), Split, negate/Combine/Normalize, Abs, and
  Length. It follows the camera rendering the pass and never caches
  `Camera.main`.
- Time computes `base = lerp(UnityTime, Override,
  saturate(UseTimeOverride))`, then Seconds, Frame, Sine, and Cosine using the
  four stable `_MIKU_Effect*` properties.
- Fresnel expands the complete dielectric equation. Layer Weight keeps separate
  Fresnel and Facing expressions and Blender's exact half-blend branch.

Portable Hybrid (`PreferNative`) combines these runtime nodes with UV0 Sample
Texture 2D nodes for reusable static islands. Before mutating generated assets,
the importer rejects a Portable Hybrid plan or bundle containing SourceMesh,
`meshBinding`, or any non-UV0/non-uniform bake job.

The backend rejects a fragment expression that reaches a Vertex channel with
`shader_stage_conflict`. It never emits a Custom Function for these operations.
Generated object and slot IDs are stabilized after Shader Graph MultiJson
serialization so repeated generation remains byte-stable.

`Input.MaterialChannel(Height)` is sampled from UV0 with explicit LOD 0 when it
reaches Vertex Position. `_MIKU_HeightMidlevel` and `_MIKU_HeightScale` defaults
come from MaterialIR rather than importer constants, so users can tune them on
the generated material and scale zero restores the undisplaced position.

Miku MaterialIR 2.0 is dispatched through `ISurfaceGraphGenerator`. The registered
surface kinds are Opaque PBR, Cutout PBR, Transparent Lit, Transparent
Emission, Refractive Glass, and Custom Multi-Lobe. The last three do not
collapse independent closure parameters into one Standard Lit record.

`MikuMultiLobeLighting.hlsl` evaluates each supported scattering term with a
bounded diffuse/GGX model over URP 17.4 public light data. Inputs, roughness,
normals, BRDF denominators, attenuation, and final radiance are finite-checked;
invalid per-lobe normals fall back to the geometric world normal. The evaluator
includes main and additional lights, shadows, cookies, and SH/light probes in
Forward and Forward+, while the URP Unlit target applies fog after the custom
radiance. Scalar pass-through uses one premultiplication. Colored transmittance
samples Scene Color and requires Opaque Texture.

The custom lighting path currently cannot consume screen-space ambient
occlusion. Auto reports `MIKU_CUSTOM_LIGHTING_SSAO_UNAVAILABLE`; Strict rejects
the path. Linked per-lobe normals are evaluated independently in their declared
space and never feed back into geometry expressions such as Fresnel or Layer
Weight.

On initial generation or explicit Full Regeneration, the version adapter derives
the runtime Sub Graph property interface and adds matching hidden material
properties plus Property-node connections to the wrapper. This makes the four
Time references and externalized driver parameters addressable on the final
Shader/Material. A pre-existing user wrapper is never rewritten implicitly; if
it predates required runtime properties, Miku preserves it and reports
`MIKU_RUNTIME_WRAPPER_PROPERTIES_MISSING`.

For a current Miku SourceMesh bundle, the importer verifies the sealed GLB and
uses glTFast to create stable Miku-owned Mesh assets, an authoritative Prefab,
and a `MikuMeshBindingDescription`. Renderer/material-slot assignment is
accepted only after topology, UV0, submesh and fingerprint validation. Legacy
MiGR bundles retain their read-only compatibility behavior.

## Required target design

A backend key contains exact Unity Editor, URP, Shader Graph, render pipeline, and
template-fixture versions. Selection either returns a compatible adapter or a
structured `Unsupported` diagnostic. It must not select a nearby version by
guessing.

`ShaderGraph17_0Adapter` through `ShaderGraph17_5Adapter` isolate all internal
class, slot, target, and serialization access. Before any asset transaction,
the selected adapter creates and serializes a capability graph covering the
properties, nodes, ports, connections, Custom Functions, and surface outputs
used by Miku. It also verifies the fixed hashes and actual Unity import of all
five wrapper templates. Core semantic lowering knows none of these internals.

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
