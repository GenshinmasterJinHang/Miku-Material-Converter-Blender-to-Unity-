# ADR 0008: Preserve Blender Volume Absorption as a coefficient plus thickness

## Status

Accepted, 2026-07-22.

## Context

Blender Volume Absorption supplies a Color and Density on the material Volume
input. URP Shader Graph has no equivalent path-traced volume closure, and using
the source color as transparent alpha would change coverage rather than light
transmission. A deterministic, editable real-time approximation needs an
explicit distance.

## Decision

MiGR stores source Color and Density expressions and derives the RGB absorption
coefficient as `(1 - Color.rgb) * Density`. The URP crystal backend computes
Beer-Lambert transmittance as `exp(-coefficient * thickness)`. Alpha is excluded
from the coefficient. Thickness is a separately versioned optical field, in
meters by default, and is supplied either by a constant or a typed Non-Color
texture/baked texture with an explicit channel, UV set, and meter scale.

The backend always keeps a constant editable `_Thickness` fallback. A valid
texture source generates `_ThicknessMap * _ThicknessMapScale` and clamps it to
the declared bounds. Missing texture resources keep the constant fallback and
emit a structured diagnostic. The backend reports closed-mesh, normal, scale,
Opaque Texture, Linear color-space, and reflection-probe requirements. It never
modifies source geometry or project settings.

The coefficient is grounded in the Blender 5.0 Cycles SVM closure
implementation: its Volume Absorption branch complements the closure color and
then applies non-negative density before adding extinction. See the official
[Blender 5.0 Cycles closure source](https://raw.githubusercontent.com/blender/blender/blender-v5.0-release/intern/cycles/kernel/svm/closure.h).

## Consequences

The result is deterministic and monotonic with thickness and density, and
absorption remains independent of opacity. It is still approximate: raster
Scene Color cannot reproduce multiple internal bounces, nested transparency,
caustics, or spectral transport. A future coefficient-model change requires a
new optical companion version and migration documentation.
