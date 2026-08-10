# Outline UV7 TangentSpaceV2 migration

Miku 2.3.0 changes newly generated smooth-outline data from an unmarked
object-space `float3` to `float4(normalTS.xyz, 2.0)` in UV7/TEXCOORD7.

No immediate mesh migration is required. Miku's four Game Toon shader families
continue to read unmarked legacy object-space UV7. Invalid or reversed legacy
data falls back to the current normal, so an old mesh may render a less smooth
outline but must not extrude the wrong side.

Regenerate when the model is skinned, the old UV7 equals the source normal, or
one side of the outline is missing:

1. Select the source Mesh asset.
2. Open **Miku > Game Toon > Mesh > Smooth Normal Generator**.
3. Choose a new output name such as `Character_SmoothOutline_v2`.
4. Select **Replace on Clone** if the source already has UV7.
5. Generate, then explicitly assign the new Mesh to the target MeshFilter or
   SkinnedMeshRenderer.

Miku never edits the source/imported mesh. Failure is transactional: invalid
tangents report `MIKU_TOON_TANGENTS_REQUIRED`, invalid numeric/topology data
reports `MIKU_TOON_MESH_DATA_INVALID:<semantic>`, and no partial UV7 is kept.

`_OutlineWidth` is interpreted as a fraction of screen height; `0.001` is about
1.08 pixels at 1080p. `_OutlineDistanceScale` remains compatible as a blend
from constant screen width to the bounded historical distance response.

For an animated character, verify both the rest pose and a bent two-bone pose.
If only posed frames fail, confirm that the renderer uses the regenerated
TangentSpaceV2 mesh and that the source tangents are valid; do not convert UV7
back to object space.
