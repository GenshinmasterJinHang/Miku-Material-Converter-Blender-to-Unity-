# Hybrid texture companion migration 1.1

Miku remains `miku-2.0`. This change versions three additive companion
contracts:

- `b2u-hybrid-plan-1.1`
- `b2u-bake-1.1`
- `cycles-optical-1.1`

Unity package 0.10.0 reads both 1.0 and 1.1 companions. Unknown companion
versions are rejected with a structured diagnostic; they are never
reinterpreted as 1.0.

## Export changes

`capabilityReport.textureNodes` records one decision for every active texture
node: strategy, translation quality, representation, coordinate domain, and
reason. Coverage counts a node only when a verified native implementation or a
concrete bake route exists.

`automaticBake.branches` records the material/mesh binding, representation,
color space, and Normal Map requirement for each baked channel.
`automaticTextureResources` records reusable node-level resources:

- Generated/Object/Position functions use an RGBAHalf EXR slice atlas plus
  `dimensions` and `atlas` metadata. Unity deterministically materializes that
  source as a `Texture3D` asset and samples it with `Sample Texture 3D`.
- Sky/IES/conditional Environment functions use a linear HDR 2:1
  Equirectangular `DirectionLut`.
- `node.params.bakedOutputs` maps each used source output to its concrete
  resource, channel, coordinate input, domain, and `Baked` quality.

The cache key includes the normalized graph, Blender build, frame, and bake
settings. Algorithm revision 6 removes unstable Blender RNA object addresses
from the key, so identical exports reuse resources across headless processes.

`opticalMaterial.paritySlots` adds Base Tint, Roughness, IOR, Normal,
Absorption, Emission, and Alpha resource bindings. Only present slots are
generated. `_B2U_UseBakedParity` defaults to true; false keeps the supported
live editable route.

Scalar slots may carry additive `decodeScale` and `decodeBias` metadata. IOR
uses a linear PNG-safe encoding, `encoded = (ior - 1) / 9`, and Unity restores
the value with `ior = encoded * 9 + 1`. Older 1.1 documents without these
members retain the identity decode (`1`, `0`).

## Behavioral changes

- Noise and Voronoi use baked parity by default. Their old Shader Graph
  approximations remain opt-in implementation details and are not reported as
  equivalent.
- Brick, Magic, and White Noise no longer use unrelated constants or random
  functions as a success path.
- Environment exports its image resource and uses editable material-direction
  sampling for Equirectangular and Mirror Ball projections.
- Unconnected zero Normal sockets remain `ImplicitGeometryNormal`; optical
  parity normals are imported and unpacked as tangent-space normal maps.
- Point Density is accepted only through Blender 4.5.8. Blender 5.x does not
  recreate the removed node.
- Texture3D atlases and direction LUTs are dependencies of the MIKU importer;
  changing either resource triggers regeneration and material rebinding.

No Shader property reference was renamed. Wrapper Shader Graph ownership is
unchanged: Miku regenerates `*.generated.shadersubgraph`; an existing user
wrapper is not overwritten unless Full Regeneration was explicitly requested.
