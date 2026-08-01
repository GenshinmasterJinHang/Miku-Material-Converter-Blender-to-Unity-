# Cycles optical material translation

Miku treats Cycles dielectric materials as optical semantics, not as ordinary
color interpolation. The Blender adapter resolves a Material Output in this
order: active `CYCLES`, active `ALL`, inactive `CYCLES`, inactive `ALL`, with a
stable name tie-break and diagnostics. Only nodes reachable from Surface,
Volume, and Displacement are considered.

The additive `opticalMaterial` companion uses schema
`cycles-optical-1.0`. It records a typed dielectric surface, source socket
expressions, IOR, roughness, transmission, coverage, emission, thickness,
Volume Absorption or an additive `volumeApproximation`, mesh requirements,
target requirements, limitations, and a
`cycles-feature-report-1.0` result. Expressions retain value type, coordinate
space, shader stage, uniformity, and stable source node/socket identity.

Supported closure patterns in this vertical slice are:

- Glass BSDF and Refraction BSDF;
- Principled BSDF with non-zero Transmission Weight;
- Transparent plus a supported dielectric, lowered to coverage rather than
  Base Color;
- Fresnel/Layer Weight reflection plus refraction, recognized as one
  dielectric;
- two supported dielectric closures mixed by a factor, lowered to typed
  parameter expressions;
- Emission added to a supported dielectric;
- Volume Absorption on the Material Output Volume input;
- Principled Volume accompanying a supported optical surface, preserved as
  typed density/color/emission data and lowered to surface absorption/glow.

Light Path branching, raw Volume Scatter, OSL, Bevel on the required chain, and
unrecognized closure composites cannot be silently replaced. They block the
optical backend or require an explicitly reported bake/runtime alternative.

The Shader Graph 17.4 URP crystal path generates an editable Miku-owned Sub
Graph and a user-owned transparent Unlit wrapper. It uses Scene Color for one
screen-space refraction sample, Schlick Fresnel derived from IOR, Reflection
Probe reflection, `smoothness = 1 - roughness`, explicit thickness, and
Beer-Lambert transmittance. Live source expressions are retained when their
Miku routes can be emitted; properties supply stable editable fallbacks.
Blender reroutes are resolved as typed aliases, while color-to-factor and
scalar-to-vector socket conversions are expanded explicitly so Shader Graph
does not infer an incompatible scalar redirect.

Blender's unconnected built-in shader Normal input stores a zero-vector
sentinel that means the current geometry normal. The exporter marks that input
as `implicit_geometry_normal`; optical lowering writes the target-neutral
`ImplicitGeometryNormal` expression instead of a constant. Expression
composition collapses two such normal branches before they can become a zero
Lerp. Unity resolves the semantic to a varying Normal Vector in the requested
space. It also recognizes legacy `cycles-optical-1.0` constant-zero normal
expressions so old files do not generate `normalize(0)`. Node-group flattening
is covered separately: a synthetic zero `Input.Vector` listed as an
`input_default` group-interface mapping becomes the same implicit normal only
when it feeds a Normal expression. A user-connected zero Vector remains
explicit.

Shader Graph 17.4 emission includes exact Blender 5.0 formulas for Gradient
Texture (all seven modes), Brightness/Contrast, Math Modulo, and Multiply Add,
plus registered Separate/Combine aliases. Unsupported live routes such as Magic
Texture, RGB Curves, White Noise, or unsupported Geometry outputs retain their
typed property fallback and emit `MIKU_CYCLES_OPTICAL_EXPRESSION_FALLBACK`.

Formula provenance is the official Blender 5.0 source for
[Gradient Texture](https://raw.githubusercontent.com/blender/blender/blender-v5.0-release/source/blender/nodes/shader/nodes/node_shader_tex_gradient.cc),
[Brightness/Contrast](https://raw.githubusercontent.com/blender/blender/blender-v5.0-release/source/blender/gpu/shaders/material/gpu_shader_material_bright_contrast.glsl),
and [Cycles volume closure evaluation](https://raw.githubusercontent.com/blender/blender/blender-v5.0-release/intern/cycles/kernel/svm/closure.h).

Thickness is explicit and measured in meters. `Constant` uses the editable
`_Thickness` property. `Texture`/`BakedTexture` references a typed Non-Color
resource, channel, and UV set and generates `_ThicknessMap` multiplied by
`_ThicknessMapScale`, clamped to the declared safe bounds. A missing texture
never becomes an implicit value: the graph retains `_Thickness` and reports the
missing resource.

Shape is also explicit. `SolidApproximation` keeps front-face rendering and the
closed-mesh requirement. Artist-selected `ThinSurface` sets `thinWalled`, drops
the closed-volume requirement, and asks the verified URP wrapper to render both
faces. It is still a raster thin-surface approximation and does not add a
back-face depth pass.

Volume absorption is color transmission through distance and never controls
coverage alpha. The approximation is:

```text
absorptionCoefficient = (1 - absorptionColor.rgb) * density
transmittance = exp(-absorptionCoefficient * thickness)
```

Principled Volume uses the same surface absorption path plus
`1 - exp(-density * thickness)` as emission coverage. This is an intentional
surface approximation of Blender's combined scattering, absorption, and
emission volume model, not a claim of volumetric parity.

URP Camera Opaque Texture and Linear color space are required. The generator
reports project state but does not mutate project settings. Reflection probes
are recommended. Solid absorption assumes a closed manifold mesh, consistent
outward normals, and reviewed object scale/thickness. Split normals and flat
shading are inspected and preserved rather than rewritten.

This is a real-time raster approximation. It does not provide nested transparent
refraction, multi-bounce internal reflection, path-traced caustics, spectral
dispersion, or physically exact rough refraction.
