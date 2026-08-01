# Closure lowering

Miku 2.0 converts Blender 5.2 shader closures into target-neutral weighted
closure semantics before selecting a Unity URP Shader Graph surface model.

## Pipeline

1. Blender extracts the complete active Surface topology with stable node,
   socket, group-path, and source identities.
2. Core builds a `ClosureGraph` without Blender objects or Unity types.
3. `ClosureWeightFlattener` derives a symbolic `WeightedClosureSet`.
4. The simplifier merges only semantically identical terms by summing their
   weights.
5. Feature, compatibility, and budget analyzers select a `SurfaceModelPlan`.
6. Unity dispatches the plan to a Shader Graph 17.4 surface generator.

Every weighted term preserves closure domain, parameters, local and final
weights, dynamic dependencies, source paths, normal/tangent/roughness/IOR
expressions, fidelity, and diagnostics.

## Mix and Add semantics

A Mix Shader is not a color interpolation. With clamped factor `f` and parent
weight `w`, Blender input socket order is preserved:

```text
firstWeight  = w * (1 - clamp(f, 0, 1))
secondWeight = w * clamp(f, 0, 1)
```

Color-to-factor uses Blender-compatible linear luminance; vector-to-factor uses
component average. Both conversions remain explicit in the expression DAG.

Add Shader copies `w` to both branches. The default `PreserveBlender` policy
does not normalize or clamp their sum and reports `WEIGHT0003`. Optional
energy-conserving or real-time clamp policies are recorded approximations and
Strict rejects them.

Roughness, IOR, normal, transmittance, and runtime factor expressions are never
averaged to force a Standard Lit surface.

## Surface model selection

| Surface kind | Intended use |
| --- | --- |
| `OpaquePBR` | Proved single compatible opaque Principled surface |
| `CutoutPBR` | Hard coverage with depth/shadow-compatible alpha clipping |
| `TransparentLit` | Lit scattering plus transparent pass-through |
| `TransparentEmission` | Emission plus transparent pass-through |
| `RefractiveGlass` | Glass/refraction with Scene Color and probe reflection |
| `CustomMultiLobe` | Multiple independently evaluated scattering lobes |
| `UnsupportedSurface` | Required semantics have no faithful phase 1-5 backend |

Scalar transparency uses `alpha = saturate(1 - transmittance)` and one
premultiplication. Colored transmittance uses Scene Color composition and
reports the URP Opaque Texture requirement.

## Invariants

- Unity Smoothness is always `1 - Blender Roughness`.
- Fragment-only expressions never enter Vertex Position.
- Add Shader never drops or silently normalizes a lobe.
- Unsupported required closures never become black, white, zero, or a
  pass-through constant.
- Linked per-lobe normals are rejected until each can be evaluated and
  transformed independently.
- Volume, Holdout, Shader-to-RGB, spectral, and other phase-6 domains remain
  represented but Unsupported in this release.

## Target limitations

The URP 17.4 custom-lighting path evaluates main/additional lights, shadows,
cookies, probes/SH, Forward/Forward+, and target-pass fog. It does not consume
screen-space ambient occlusion. Auto reports
`MIKU_CUSTOM_LIGHTING_SSAO_UNAVAILABLE`; Strict rejects the path.

Screen-space glass uses one Scene Color sample and cannot reproduce off-screen
objects, nested media, caustics, spectral dispersion, or full rough
transmission. Auto records the approximation; Strict rejects it.

