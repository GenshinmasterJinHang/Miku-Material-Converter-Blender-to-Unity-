# Cycles material subset

Miku does not port the Cycles renderer to Unity. It recognizes a bounded set of
Cycles closures and optical meanings and lowers them to target-neutral
`cycles-optical-1.0` data. The Unity URP result is a deterministic, editable
real-time raster approximation.

## Supported authoring patterns

- Glass BSDF;
- Refraction BSDF;
- Principled BSDF with non-zero Transmission Weight;
- Fresnel or Layer Weight controlling reflection plus refraction;
- Transparent plus one supported dielectric, represented as coverage;
- Emission added to one supported dielectric;
- a factor mix of two supported dielectric closures;
- Volume Absorption connected to Material Output Volume;
- Principled Volume accompanying a supported dielectric surface, lowered to a
  thickness-based absorption and emissive surface-glow approximation;
- Material Output targets `CYCLES` or `ALL`, including deterministic selection
  when a file contains multiple outputs.

Surface, Volume, and Displacement entry sockets are preserved independently.
Only the active output chain participates in optical translation. An
unsupported node outside that chain may warn; one on the required chain blocks
optical generation when a safe meaning cannot be preserved.

Leave a shader Normal input unconnected to use Blender's geometry normal. Miku
exports this as `ImplicitGeometryNormal`, not as the stored zero vector. An
explicitly connected zero Vector remains explicit and is not reinterpreted.

Light Path branching (including ray depth and glossy/transmission caustics
assumptions), Wavelength-driven dispersion, raw Volume Scatter, standalone
volume-only materials, OSL, required Bevel, unrecognized closure composites, nested media, ray-traced
caustics, spectral dispersion, and multi-bounce refraction are not supported by
this subset. The feature report records these cases and the converter does not
substitute black, white, zero, or pass-through values for those semantics.

Principled Volume is not claimed as real volumetric rendering. Miku preserves
density, absorption color, emission color/strength, scatter color, and
anisotropy in `volumeApproximation`; URP uses density and explicit thickness for
surface absorption and glow. Scattering, volumetric shadows, and ray integration
remain unpreserved and are diagnosed.

## Thickness authoring

The Blender material panel contains **Cycles Crystal Thickness** controls.

- **Shape: Solid Approximation** requires closed-volume geometry. **Thin
  Surface** records `ThinSurface`, disables the closed-mesh requirement, and
  asks the URP wrapper to render both faces; it does not invent back-face depth.
- **Constant** exports `constantValue` in meters and creates `_Thickness`.
- **Texture** requires an image, R/G/B/A channel, UV0/UV1, and a meter scale.
  The image is exported as Non-Color data and Unity creates
  `_ThicknessMap * _ThicknessMapScale` with a safe clamp.
- If Texture is selected without a usable image, the constant value remains the
  explicit fallback and `MIKU_CRYSTAL_THICKNESS_TEXTURE_MISSING` is reported.

The exported thickness resource stores a portable texture placeholder/URI. It
does not serialize the Blender source path or the exporter's absolute working
path into the public Miku document.

Use a closed, outward-wound mesh for solid absorption and review applied scale.
Non-uniform scale makes physical distance ambiguous and is reported. A thin
surface should use a small reviewed constant or an authored map; no hidden
back-face depth pass is generated.

## Reports and quality

The Blender export contains `cyclesFeatureReport`; Unity writes a neighboring
`.migrreport.json`. Read diagnostic codes and `translationQuality`, not localized
message text. Quality values mean:

- `Exact`: same tested semantic formula;
- `Equivalent`: a different construction with the same intended result;
- `Approximate`: a known visual or physical difference exists;
- `Baked`: source behavior was converted to sampled data;
- `RequiresProjectSetup`: generated safely but needs settings such as Opaque
  Texture;
- `RequiresRuntimeSupport`: a runtime system outside the graph is required;
- `Unsupported`: generation is blocked for the required chain.

See the [optical IR](../architecture/optical-material-ir.md),
[URP setup](urp-crystal-backend.md), and
[node matrix](../node-support-matrix.md).

## Extending closure lowering

Add new Cycles closure support in the pure-Python semantic layer first. Define
typed inputs, closure composition rules, feature flags, translation quality,
and blocking behavior without importing `bpy` or Unity types. Add focused
positive, negative, provenance, and determinism tests, then version the public
schema if existing meanings change. A Unity backend may consume the semantic
result only after the target-neutral contract is documented and validated.
