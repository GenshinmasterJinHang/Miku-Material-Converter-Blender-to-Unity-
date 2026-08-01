# ADR 0010: Closure-aware MaterialIR 2.0

- Status: Accepted
- Date: 2026-07-29

## Context

MaterialIR 1.0 projected Blender shader closures into one Standard PBR surface
contract. That representation cannot preserve general `Mix Shader` and
`Add Shader` topology, symbolic runtime weights, independent roughness or IOR,
or transparent/emissive composites without silently changing material meaning.

## Decision

MiGR 2.0 makes closure topology a first-class, target-neutral part of
MaterialIR. The Blender boundary emits a `ClosureGraph`; core computes a
symbolic `WeightedClosureSet`, performs only proved simplifications, analyzes
surface features and budgets, and selects a `SurfaceModelPlan`.

`Mix Shader` distributes a clamped factor as `parent * (1 - fac)` and
`parent * fac` in Blender socket order. `Add Shader` copies the parent weight
to both inputs and does not normalize unless the user selects an explicit
energy policy. Unity selects one of six versioned surface generators:
`OpaquePBR`, `CutoutPBR`, `TransparentLit`, `TransparentEmission`,
`RefractiveGlass`, or `CustomMultiLobe`.

MaterialIR, conversion plan, manifest, bundle, and target profile move to
schema 2.0. Stable document kinds and unknown-version rejection remain
mandatory. `Auto` permits only declared approximations; `Strict` rejects every
declared approximation. Legacy v1 opaque Standard PBR may migrate
deterministically. Legacy transparent or dielectric v1 documents are not
reinterpreted as closure graphs.

## Consequences

- Arbitrary surface closures are no longer flattened into one averaged PBR
  material.
- The Unity 17.4 adapter may use package-owned custom lighting HLSL while core
  remains free of Unity internal types.
- Generated v2 assets require the MiGR 2.0 exporter and Unity package.
- Screen-space glass remains approximate and project-dependent.
- Custom multi-lobe lighting currently omits SSAO and reports that limitation;
  Strict rejects it.
- Linked per-lobe normals are rejected until the backend can evaluate each
  normal independently.
- Volume, Holdout, Shader-to-RGB, spectral, and other phase-6 domains remain
  represented but unsupported in the phase 1-5 runtime backend.

