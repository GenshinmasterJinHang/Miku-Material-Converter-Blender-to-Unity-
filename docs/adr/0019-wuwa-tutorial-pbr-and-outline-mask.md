# ADR 0019: WuWa tutorial PBR and authored outline mask

- Status: Accepted
- Date: 2026-08-12
- Supersedes: ADR 0016 for WuWa vertex-color masking only

## Context

The first WuWa tutorial pass exposed several controls but retained a custom
toon-plus-SH lighting path, treated packed normal/roughness/metallic textures as
ordinary Unity normal maps, and contradicted the authored Phoebe model contract
by requiring a neutral outline mask.  The tutorial and inspected source assets
require URP BRDF/GI integration, four UV sets, and vertex-color G outline width.

## Decision

WuWa Body, Hair, Face, and Eye use the URP 17 BRDF and GI helpers as their
shared foundation.  WuWa packed NRM textures are linear RGBA data: RG is a
DirectX tangent normal, B is metallic, and A is perceptual roughness.  The
separate authored NPR layers remain after that foundation.

WuWa outline passes continue to decode TangentSpaceV2 smooth normals from UV7,
but their width is multiplied by vertex-color G and the tutorial distance
curve.  Passing a neutral constant into the shared outline color argument does
not disable the separate WuWa G width mask.

Vertex-color G is used only when the material explicitly enables
`_OutlineVertexColorMask`; it never modifies Base Color or PBR. Meshes without
Color data use the pre-existing neutral material fallback.

Generic Screen Rim keeps its four-tap mode as the compatibility default.  WuWa
setup explicitly selects the tutorial distance-scaled three-sample mode.

## Consequences

- Existing WuWa material lighting changes and therefore requires a 3.0 clone
  migration rather than silent in-place rewriting.
- Exporters and Unity bindings gain additive packed-NRM and outline-color
  semantic roles; the interchange schema version is unchanged.
- ADR 0016 still governs smooth-normal encoding and every non-WuWa family.
- Tests must require WuWa vertex-color G while continuing to reject unintended
  vertex-color contracts in HSR and Endfield.
- Forward shading, base color, and PBR do not consume vertex colors; the
  tutorial establishes G only as outline width data.
