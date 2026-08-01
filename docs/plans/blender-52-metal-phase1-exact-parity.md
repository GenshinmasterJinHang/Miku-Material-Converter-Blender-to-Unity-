# Blender 5.2 Metal Phase 1 — Exact Parity Execution Plan

## Status

Executing. The implementation is complete for the first five whitelisted
materials; the remaining gate is human visual acceptance before expanding the
whitelist.

Validated target versions:

- Blender 5.2.0 LTS (`v5.2.0`, `fbe6228777e7d9afefcd61a413844e790ae75db7`)
- Unity 6000.4.5f1
- URP 17.4.0
- Shader Graph 17.4.0

The worktree is intentionally dirty. Only files related to this plan are
modified; unrelated user changes are preserved.

## Locked decisions

- First material set: `Dots Stroke`, `银5`, `金1`, `金8`, `铜1`.
- Blender export is `mgir-4.0`, schema version 4, `StrictExact`.
- Authored group boundaries and stable group socket ABI are retained in the
  interchange document. Coordinate space and shader stage are explicit.
- The only ShaderLab exception is the approved three-family legacy route:
  `mgir-1/2/3 + LegacyCompatible` → typed dispatcher → Genshin/Wuwa/HSR
  legacy backend. `mgir-4.0/StrictExact` never enters that route.
- Generated Sub Graphs and reports are MiGR-owned. Wrapper graphs remain
  user-owned after first creation.

## Implementation order

1. Add versioned models, strict policy validation, typed legacy dispatch and
   canonical validation digest.
2. Export Blender source identity, group/socket metadata and the five-material
   whitelist through the Blender 5.2 headless path.
3. Emit `SurfaceIR` with typed coordinate/stage ports and use the exact
   Shader Graph 17.4 adapter for Unity lowering.
4. Generate deterministic semantic object IDs, editable Shader Graph assets,
   material bindings, mappings and reports.
5. Run Blender/Python/.NET/Unity tests and create the Unity MCP review scene.

## Stop conditions

Do not report success if schema validation, path safety, stable IDs, algorithm
oracle, Unity import, or hard numeric gates fail. If the Glossy two-lobe
approximation is not visually accepted, stop at the first five materials.

## Execution evidence (2026-07-26)

- Blender headless export completed for all five whitelisted materials. Each
  document is `mgir-4.0`, schema `4`, `StrictExact`, with `SurfaceIR` and
  group/socket ABI data.
- Unity EditMode suite completed: 34/34 passed, including strict mgir-4
  routing, typed legacy dispatch, digest validation, deterministic graph
  generation, coordinate-space and shader-stage checks.
- Unity MCP scene validation for `MetalPhase1.unity` reports zero issues. The
  scene contains a camera, Directional Light and five material review spheres.
- Review screenshot: `Assets/MiGRReview/MetalPhase1/Captures/MetalPhase1_Review.png`.
- Human visual acceptance is still required before adding more materials.
