# ADR 0020: Continuous WuWa Face SDF and layered eye parallax

- Status: Accepted
- Date: 2026-08-13
- Extends: ADR 0019

## Context

The WuWa Face shader selected one horizontal SDF orientation from the sign of
the light direction relative to the animated head-right axis. Crossing that
axis changed the selected half-face texture in one frame. The primary A channel
also used a hard threshold, so increasing SDF softness could not make the whole
response continuous.

The Eye shader already had a tangent-derived view offset, but it applied one
offset UV to the iris layers, eye-socket shadow, authored upper/lower
highlights, and EG highlight together. This made surface highlights slide with
the pupil and did not match the supplied tangent-space parallax convention.

## Decision

Face SDF evaluates the unmirrored and horizontally mirrored samples
independently. For each side, both the primary A channel and soft-detail B
channel use the same bounded `smoothstep` threshold band. A remains the primary
gate; B refines it through `main * lerp(1, soft, strength)` and cannot light a
region rejected by A.

The two completed side masks are blended with a weight derived from the light's
signed head-right component. The additive material property
`_FaceSdfMirrorBlendWidth` controls the signed transition width, with shader and
recommended-profile default `0.10` and public range `0` to `0.5`. A zero width
uses hard compatibility selection and selects the unmirrored side exactly on
the centre line.

Miku blends final masks, not UV coordinates or raw SDF values. UV interpolation
would collapse toward the texture centre at the transition, while raw-value
interpolation would change the authored threshold topology before evaluation.
The implementation samples `float2(1 - u, v)` explicitly and does not depend on
negative UVs, Repeat wrapping, or `sign(0)` behavior.

Eye keeps two UV domains. `surfaceUV` is the original mesh UV. `irisUV` applies
the normalized tangent/bitangent view offset, flips its Y component, and
subtracts it from `surfaceUV`. Base, HET, and HDMF use `irisUV`; the eye-socket
shadow, authored upper/lower highlights, and EG surface highlight use
`surfaceUV`. Invalid tangents produce exactly zero offset.

```hlsl
float2 viewTS = float2(dot(viewWS, tangentWS), dot(viewWS, bitangentWS));
float2 offset = viewTS * _EyeParallaxStrength * tangentValid;
offset.y = -offset.y;
float2 irisUV = saturate(surfaceUV - offset);
```

The existing `_EyeParallaxStrength` property keeps its name, range, and shader
default of `0`. Applying the recommended profile writes `0.02` only when HDMF
is bound, so HET-only sclera materials stay flat. Existing materials are not
rewritten automatically.

## Consequences

- Rotating the main light across the face centre produces a continuous SDF
  transition instead of replacing one half-face mask in one frame.
- The new Face property is an additive ShaderLab material property; no existing
  property reference, public C# API, package identity, or interchange schema is
  renamed or versioned.
- Existing Eye materials with a non-zero parallax value receive corrected
  direction and layer ownership. Setting the value to zero remains an exact
  flat compatibility path.
- CPU/EditMode tests must cover threshold and mirror continuity, and D3D12
  acceptance must include small light-yaw steps around the mirror boundary.
  Eye tests must distinguish iris motion from stationary surface highlights.
