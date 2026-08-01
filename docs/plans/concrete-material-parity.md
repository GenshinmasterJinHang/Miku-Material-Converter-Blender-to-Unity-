# Concrete material parity and procedural fallback

## Purpose and outcome

Re-export the exact Blender object and material `混泥土5` from the concrete
library, generate an editable Unity Shader Graph plus a baked parity path, and
validate visible tangent-space relief in the live Unity 6000.4.5f1 project. The
same Blender mesh is the only visual acceptance mesh; a Unity primitive sphere
is not an acceptance target.

The work is complete. Hybrid defaults to the Blender-baked PBR channels and can
switch to a fully native Shader Graph approximation. The live validation scene
uses `混泥土5`, while wood and stone retain their previous meshes and materials.

## Context and constraints

- The worktree contained broad pre-existing user changes; this work preserves
  them and does not commit.
- Blender integration remains in `b2u_mvp_blender`; the source `.blend` was
  opened read-only in practice and was never saved.
- No Custom Function node, handwritten ShaderLab, third-party shader, UV
  re-unwrap, topology edit, or source material edit was introduced.
- Generated wrappers were regenerated once under the explicitly requested Full
  Regeneration mode. The importer is back in Linked Sub Graph mode.
- MiGR and `.migrmap.json` remain schema version 1. New fields and importer
  choices are additive.

## Progress

- [x] Confirmed the final user-selected object/material is `混泥土5`: 482
  Blender vertices, 512 Blender polygons, and one `UVMap`.
- [x] Inspected its 36-node/42-edge graph: Object coordinates, POINT Mapping,
  five high-detail 3D noise sources, ramps/mixes, two Principled branches, and
  three stacked Bump nodes.
- [x] Implemented compatible mixed-Principled semantic baking, including the
  shared tangent-space normal through an enclosing node group.
- [x] Implemented per-socket Shader Graph routing, Position(Object), Vector3
  Mapping, automatic Simple Noise/native fBM/bake selection, stacked Bump normal
  blending, and Hybrid baked/native Branch nodes.
- [x] Fixed first-import normal loss by configuring texture importers before
  material writes, reacquiring live assets after reimport, and restoring the
  live generated Shader before texture binding.
- [x] Exported `混泥土5` twice without saving the source `.blend`; normalized
  MGIR, node IDs, resource IDs, and content hashes are stable and contain no
  `ptr:` identities.
- [x] Baked BaseColor, Metalness, Roughness, Normal, Emission, and Alpha at
  2048 px, 32 samples, and 32 px margin; rendered front, side, and oblique
  Blender references.
- [x] Imported the source mesh/material into the live Unity project, ran one
  Full Regeneration, restored Linked Sub Graph, replaced only the concrete test
  object, and captured baked/native screenshots.
- [x] Updated support, compatibility, changelog, tests, and this result record.

## Discoveries and decisions

- The user changed the acceptance target from `混泥土4` to the exact in-file
  object/material `混泥土5`. All final evidence uses `混泥土5`; existing
  `混泥土4` assets were not removed.
- Simple Noise is a fast 2D approximation and is not suitable for this material.
  Object-space coordinates, Detail 12-16, and Bump usage select a native
  XY/XZ/YZ Gradient Noise fBM expansion with five editable octaves. Higher
  Blender Detail is diagnosed as capped.
- Baked parity is the reference because Blender and Shader Graph procedural
  noise are not pointwise equivalent. `_B2U_UseBakedParity` defaults to true;
  false exposes the editable native approximation.
- The Unity FBX initially stored local vertex positions at 0.01 scale and
  compensated in the asset transform. That made Position(Object) sample the
  wrong frequency even though world size looked correct. Concrete5 now imports
  with `ModelImporter.useFileScale = false`; the validation object uses scale
  0.925, giving a 1.85-unit world diameter and Blender-like local coordinates.
- Blender reports 482 vertices/512 polygons. Unity triangulates quads and splits
  vertices at attribute seams, yielding 559 vertices/960 triangles. UV0,
  normals, and tangents are present; this is expected importer behavior rather
  than a topology substitution.

## Implemented interfaces

- `B2UShaderGraphSurfaceMode`: `Auto`, `NativeProcedural`, `BakedParity`,
  `Hybrid`.
- `B2UProceduralNoiseMode`: `Auto`, `SimpleNoiseFast`, `NativeFbm`, `Bake`.
- `.migrmap.json` port mappings include optional strategy labels such as
  `Exact`, `ApproximateSimpleNoise`, `ExpandedNativeFbm`, and `BakedBranch`.
- Per-node storage now routes by source node and socket, so Texture Coordinate
  outputs can map to different native Shader Graph nodes.

## Validation results

- Blender 5.2.0 LTS headless edge cases:
  `B2U_AUTOMATIC_BAKE_EDGE_CASES_OK`. Direct and grouped mixed Principled
  closures with a shared Bump produce a real tangent normal.
- Concrete5 normalized export hash, twice:
  `2e60a75d79548e3788259be097813d4d19a1d2cb00533091ab91c4b1ba82e2c6`.
  Node IDs and resource IDs match; both exports contain zero `ptr:` strings.
- Baked Normal: 4,194,304 valid pixels; 1,566,203 pixels (37.341%) have R or G
  farther than 0.03 from 0.5. This exceeds the 1% non-neutral acceptance floor.
- Generated mapping: schema 1, 35 node mappings, two exact Object Position
  routes, two exact Mapping expansions, five `ExpandedNativeFbm` nodes, three
  expanded Bump nodes, and zero `MIGR_SG_PORT_NOT_FOUND` diagnostics.
- Generated Sub Graph contains Position, XYZ Rotate About Axis, 75 Gradient
  Noise node occurrences, three Normal From Height nodes, three Normal Blend
  nodes, Normal Unpack, and seven Hybrid Branch nodes.
- Unity Shader is supported and has zero compiler messages. Normal is
  `NormalMap`/Linear and bound to `_BumpMap`; Roughness is Linear; BaseColor is
  sRGB. Hybrid defaults to 1.
- Unity EditMode: 13/13 passed. This includes the original 11 tests plus native
  Object/Mapping/fBM/Bump and Hybrid/normal-import tests.
- Focused Python regression: 15/15 passed. The full legacy discovery run executed
  318 tests and reported 16 failures, 32 errors, and 21 skips; these failures
  primarily assert removed ShaderLab output, old package/version expectations,
  and pre-existing Endfield/name contracts. They are recorded rather than
  misreported as green.
- Wood remained `Cube|Material_1_aa945e09`; stone remained
  `Sphere|Material_1_b909e397`.

## Evidence and retained assets

- Blender export/reference staging:
  `.mcp_tmp/concrete5-export-b/Concrete5` and
  `.mcp_tmp/concrete5-export-c/Concrete5`.
- Blender references copied to
  `Assets/B2UFinalMaterialTests/Evidence/Concrete5/Blender` in the live project.
- Unity baked/native screenshots are under
  `Assets/B2UFinalMaterialTests/Evidence/Concrete5/Unity`.
- Live scene:
  `Assets/B2UFinalMaterialTests/MaterialLibraryValidation8081.unity`.

## Remaining limitations

- Native fBM is deliberately approximate and five-octave capped; it does not
  promise pixel equality with Blender Noise Detail 12-16.
- Baked parity is tied to the source object's UVMap. Use the native path on a
  different mesh unless that mesh shares the same UV layout.
- Compatibility is experimental for the exact Blender/Unity version tuple
  above; it is not generalized to every Blender 5 or Unity 6 release.
