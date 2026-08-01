# Compatibility matrix

| Blender | Unity Editor | URP | Shader Graph | Miku | OS | Status |
| --- | --- | --- | --- | --- | --- | --- |
| 5.2.0 | 6000.4.5f1 (`cc83ebd631f8`) | 17.4.0 | 17.4.0 | 1.0.1 | Windows | Experimental |
| Any other tuple | Any | Any | Any | 1.0.1 | Any | Unsupported |

`Experimental` means automated schema, unit, package, import, shader-compile,
recovery, and determinism evidence exists. Promotion to `Supported` requires
the final reviewed character-render screenshot set on this exact tuple.

## Workflow matrix

| `workflow.kind` | Unity backend | New outputs |
| --- | --- | --- |
| `standard_pbr` | Shader Graph 17.4 adapter | Editable wrapper, Miku-owned generated Sub Graph/base Material, optional user Material Variant |
| `generic_toon` | Fixed Miku ShaderLab/HLSL family | Miku-owned base Material/recipe and user-owned derived Material; no new graphs |
| `genshin_toon` | Package static ShaderLab/HLSL | Base Material and user Material Variant |
| `wuwa_toon` | Package static ShaderLab/HLSL | Base Material and user Material Variant |
| `hsr_toon` | Package static ShaderLab/HLSL | Base Material and user Material Variant |

Generic Toon supports Opaque and Alpha Clip. Transparent sorting, desktop XR
validation, and custom SRPs are outside the Miku 1.0 compatibility claim.

## Format policy

New writes use only:

- `miku-target-profile-1.0`
- `miku-material-ir-1.0`
- `miku-conversion-plan-1.0`
- `miku-conversion-manifest-1.0`
- `miku-bundle-1.0`
- `.mikubundle`

Schema IDs use `urn:miku:schema:<kind>:1.0`. A root `version` field and unknown
schema versions are rejected.

Legacy MiGR 1.x/2.x bundles are read-only input. Their original bytes and
canonical hash are validated before deterministic normalization to Miku 1.0.
New output never writes a MiGR document.

`persistentSourceId`, `persistentMaterialId`, and their stable asset-ID seed
roles remain the continuity boundary. Existing user Wrapper Graphs, user
Materials, and `.meta` GUIDs are preserved. A legacy source-mesh resource is
validated as bundle data but is not used to generate a Prefab or rewrite
Renderer references; users opt into the separate Mesh tools instead.

The public Miku Material properties use `_MIKU_*`. Runtime
`MaterialPropertyBlock` data is not a persistent asset and must be updated by
its calling code.
