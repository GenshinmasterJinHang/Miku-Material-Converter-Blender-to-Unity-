# Four-game outline TangentSpaceV2 cutover

## Purpose and outcome

Make the maintained Genshin, HSR, WuWa, and Endfield outline passes consume one
safe outline-normal contract. The Smooth Normal Generator writes marked
`float4(normalTS.xyz, 2.0)` values to UV7, all thirteen material consumers
decode V2 or compatible legacy object-space data through one shared HLSL
helper, and outline passes do not write depth. Genshin and Endfield use
vertex-color G for screen-height-relative width. Wuwa and HSR preserve their
historical no-vertex-mask width behavior; HSR also keeps its constant historical
distance response.

## Context and constraints

- Canonical implementation is limited to
  `unity/Packages/com.miku.shaderconverter/`; validation-project packages and
  `dist/` are not sources of truth.
- The working tree initially contains only the unrelated untracked
  `vibe-kanban/` directory. It must remain untouched.
- `package.json`, `CHANGELOG.md`, and top-level product documentation are outside
  this task.
- The public mesh-tool API keeps its current fail-fast style. No diagnostic
  report object or schema is introduced.
- Missing, malformed, non-finite, or degenerate tangents fail with
  `MIKU_TOON_TANGENTS_REQUIRED`. Invalid finite mesh inputs fail before UV7 is
  written with `MIKU_TOON_MESH_DATA_INVALID:<semantic>`.
- Endfield lighting and material CBUFFER work is concurrent. Only the outline
  function region may be patched after re-reading the current file.
- Supported Unity technical lines remain 6000.0 through 6000.5 paired with URP
  and Shader Graph 17.0 through 17.5. No compatibility claim changes.

## Progress

- [x] 2026-08-09: Read repository instructions, `PLANS.md`, relevant ADR and
  provenance documents; verified canonical package identity and clean tracked
  diff.
- [x] 2026-08-09: Traced the generator, nine concrete Outline passes, four
  Endfield UsePass consumers, and current EditMode coverage.
- [x] Implement UV7 TangentSpaceV2 generation and fail-before-write validation.
- [x] Add the shared clip-space outline HLSL and migrate all thirteen consumers.
- [x] Add focused EditMode coverage and run the available static validation.
- [x] Self-review the final diff and record results.

## Discoveries

- UV7 currently stores averaged object-space normals. Every family labels and
  transforms UV7 as object space, which cannot preserve a split vertex's local
  tangent-frame relationship.
- All nine concrete Outline passes use `Cull Front`, `ZTest LEqual`, and
  `ZWrite On`. Endfield shares one concrete pass among Body, Skin, Hair, and
  Face, producing thirteen material consumers in total.
- Genshin and Endfield multiply width by vertex-color A even though the public
  `Miku_ToonMask_v1` editor contract labels G as Outline and A as Face
  Correction. HSR and WuWa do not consume vertex color in the outline pass.
- Existing non-readable-mesh coverage omits tangents and therefore must be
  updated for the new explicit tangent requirement.

## Decision log

- 2026-08-09: UV7 stores `float4(normalTS.xyz, 2.0)`. The marker distinguishes
  V2 from the supported unmarked object-space legacy contract. The per-vertex
  normal is frame Z, the orthogonalized tangent is X, and
  `cross(normal, tangent) * tangent.w` is Y.
- 2026-08-09: Triangle area normals are aligned to the source-normal hemisphere
  before accumulation. A degenerate or cancelled sum falls back to the valid
  source normal; it is not diagnosed as corrupt input.
- 2026-08-09: Shader selection decodes marked V2 through the current tangent
  frame, reads unmarked UV7 as legacy object-space data, and falls back to the
  geometric normal for zero, non-finite, or opposite-hemisphere values.
- 2026-08-09: Family-specific outline colors remain local. Normal selection,
  bounded legacy distance response, the optional G-channel mask, aspect
  correction, and clip-space screen-height extrusion move to one
  `Runtime/GameToon` include. Genshin/Endfield pass authored vertex colors;
  Wuwa/HSR pass a neutral constant, and HSR selects the constant historical
  response branch.

## Implementation sequence

1. Validate source positions, normals, tangents, and triangle indices before
   computing or writing destination UV7.
2. Align area-normal contributions, compute smooth object-space normals, encode
   them in the orthonormal tangent frame with marker `2.0`, and verify
   finite/unit/hemisphere postconditions.
3. Add the shared marker/legacy selector and aspect-correct clip-space helper,
   update nine concrete passes and the shared Endfield outline vertex, then
   switch every Outline pass to `ZWrite Off`.
4. Add independent EditMode tests for encoding, mirrored winding, diagnostics,
   no partial writes, thirteen-consumer source contracts, and shader import.

## Validation

- Run repository static checks that inspect the thirteen consumer paths and
  ensure no Outline pass retains `ZWrite On` or A-channel width masking.
- Run the focused Unity EditMode fixture when an authorized Unity project with
  the canonical package is available.
- Do not use the external desktop Unity validation project in this task.

## Results and follow-up

Implemented marked Vector4 UV7 generation, strict pre-write mesh/tangent
validation, the marker/legacy selector, bounded screen-height clip-space
extrusion, and all thirteen consumer migrations. Static checks passed for:

- nine concrete Outline blocks plus four Endfield UsePass consumers;
- `Cull Front`, `ZTest LEqual`, `ZWrite Off`, float4 TEXCOORD7, the
  Genshin/Endfield G-channel mask, Wuwa/HSR neutral width input, HSR's constant
  historical response, and clip-position call contracts;
- V2 marker, legacy fallback, explicit 0.25..4 distance bounds, aspect/Y-flip
  correction, and absence of world-position extrusion;
- one terminal UV7 write after all input validation; and
- `git diff --check` (only unrelated line-ending warnings were emitted).

The new EditMode fixture covers marker/dimension, signed tangent frames, cross-
submesh seams, mirrored tangent islands, reversed winding, near-opposite
two-sided isolation, repeat stability, stable diagnostics, no partial UV7
write, 16:9/9:16 aspect math, thirteen source contracts, and shader import.
The coordinated isolated Unity 6000.4.5f1 run passed all 14 tests in
`MikuGameToonOutlineTests` and all eight tests in
`MikuEndfieldTutorialLightingTests`; together they exercised generator failure
paths, source contracts, all thirteen consumer shaders, and the final shared
projected-direction include. The existing `MikuGameToonTests` fixture also
passed 28/28 after its non-readable mesh gained the required tangent stream.
The external desktop validation project was not loaded or modified by this
subtask. Existing unmarked UV7 object-space assets remain supported; generated
V2 meshes require finite non-degenerate tangents.
No interchange schema changed. The public mesh helper adds
`DefaultPositionTolerance` and changes the optional smooth-normal tolerance
default from `1e-4` to `1e-6`; shader property names remain unchanged.
