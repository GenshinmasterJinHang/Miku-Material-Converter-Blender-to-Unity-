# MaterialIR 1.0 to 2.0 migration

Miku 2.0 introduces closure-aware documents. The affected document kinds are:

- `miku-target-profile-2.0`
- `miku-material-ir-2.0`
- `miku-conversion-plan-2.0`
- `miku-conversion-manifest-2.0`
- `miku-bundle-2.0`

## Safe automatic migration

A v1 `standard_pbr` material can migrate only when it is an ordinary opaque
Standard Lit surface. Miku creates one unit-weight Principled closure, preserves
the existing typed channels and expressions, and records migration provenance.
The result must validate as MaterialIR 2.0 before it is written.

## Re-export required

Re-export from Blender 5.2 with the Miku 2.0 Semantic Exporter when a v1
material is transparent, dielectric, dithered, alpha-clipped, or otherwise
depends on closure composition. V1 did not contain enough topology to infer
whether a value came from Mix Shader, Add Shader, Transparent BSDF, or another
closure. Miku refuses to invent that information.

Unknown v1 surface companions, workflows other than the supported opaque
Standard PBR migration, malformed graphs, and unknown schema versions fail with
a structured diagnostic.

## Policy changes

`Auto` remains the compatibility default and permits only explicitly recorded
approximations. `Strict` fails any material whose selected backend reports
`Approximate`. Add Shader preserves Blender's unnormalized branch weights by
default. Energy-conserving normalization or real-time clamping is opt-in,
recorded in the plan, and rejected by Strict.

Public shader property reference names, persistent source/material identities,
and user-owned wrapper ownership are unchanged.

