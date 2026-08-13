# ADR 0016: TangentSpaceV2 outline normals

> WuWa's vertex-color-mask clause is superseded by ADR 0019. The UV7
> TangentSpaceV2 encoding decision remains in force.

- Status: Accepted
- Date: 2026-08-09

## Decision

Fixed game-Toon mesh clones store smooth outline normals in UV7 as
`float4(normalTS.xyz, 2.0)`. The `w` value is the contract marker. Generation
requires finite source positions/normals and finite non-degenerate tangents,
aligns every accumulated face normal with its source-normal hemisphere, keeps
opposite two-sided vertices separate, and writes deterministic unit vectors.

Shader consumers reconstruct marked values through the current, potentially
skinned normal/tangent frame. Unmarked three-component UV7 remains a supported
legacy object-space input, but zero, non-finite, or opposite-hemisphere data
falls back to the current geometric normal.

All four fixed game families use one finite-safe screen/clip-space extrusion
helper. Outline width is a screen-height proportion, accounts for aspect ratio,
and retains the existing distance control as a bounded compatibility blend.
The helper accepts a width mask and a historical-response selector instead of
forcing one family contract onto every shader: Genshin and Endfield pass
`Miku_ToonMask_v1` green, Wuwa and HSR pass a neutral constant, and HSR selects
the constant historical distance response.

## Consequences

- Source meshes and importers are never mutated. V2 data is written only to a
  new explicitly selected mesh clone.
- Missing or invalid tangents fail before asset creation with
  `MIKU_TOON_TANGENTS_REQUIRED`; unsafe source mesh data fails with
  `MIKU_TOON_MESH_DATA_INVALID`.
- Existing material property names and legacy UV7 assets remain readable.
- Wuwa/HSR do not start reading vertex-color G as a side effect of sharing the
  helper, and HSR does not acquire the other families' near-distance growth.
- V2 is the required path for deformation-safe `SkinnedMeshRenderer` outlines.
