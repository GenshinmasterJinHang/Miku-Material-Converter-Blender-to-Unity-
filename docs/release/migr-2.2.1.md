# MiGR Unity 2.2.1 release notes

MiGR Unity package 2.2.1 fixes first import of editable Shader Graphs whose
reachable Fragment expression chain uses Blender Layer Weight or Fresnel
front/back-face semantics.

## Fixed

Two generated-asset conditions required correction. First, a newly created
Sub Graph could receive a random `.meta` GUID before MiGR assigned the
deterministic GUID, so Wrapper creation needed a verified dependency barrier.
Second, MiGR connected the Sub Graph's identity Vertex Position output even
when MaterialIR had no Displacement expression. Shader Graph 17.4 generates a
single function for all Sub Graph outputs, so ShadowCaster then evaluated the
fragment-dependent function in `VertexDescriptionFunction` and emitted
`bindings.FaceSign = IN.FaceSign`; the vertex input structure correctly has no
fragment face-sign field.

The importer now:

- force-refreshes and synchronously force-imports the generated Sub Graph after
  its stable GUID is assigned;
- verifies `AssetDatabase.AssetPathToGUID` matches the expected stable GUID;
- verifies the Sub Graph main asset is loadable before Wrapper creation;
- connects the generated Sub Graph to Vertex Position only when MaterialIR has
  a runtime Displacement expression, otherwise retaining the URP Master Stack
  default Object Position;
- reports `MIGR_SUBGRAPH_GUID_SYNC_FAILED` or
  `MIGR_SUBGRAPH_IMPORT_FAILED` and uses the existing transactional rollback on
  failure; and
- force-updates the final Sub Graph and Wrapper imports so Unity cannot reuse
  the stale first-import Shader importer artifact.

`Input.IsFrontFace` remains the native, Fragment-only Shader Graph
`IsFrontFaceNode`; it is not replaced by a constant or approximation.
Existing user-owned Wrappers are not rewritten; the Vertex Position edge
change applies only to new Wrapper creation or explicit Full Regeneration.

## Contracts

- Blender Semantic Exporter: unchanged at 2.2.0.
- Unity package: 2.2.1.
- GPL Bake Worker: unchanged at 1.2.0.
- Unity Editor: 6000.4.5f1.
- URP and Shader Graph: 17.4.0.
- MaterialIR: unchanged at 2.0.
- Bundle: unchanged at 2.2, with safe Bundle 2.0/2.1 compatibility retained.
- Target profile hash: unchanged.
- Public Shader properties, stable IDs, generated Sub Graph serialization, and
  Wrapper ownership: unchanged. New or explicitly regenerated no-displacement
  Wrappers omit one redundant identity Vertex Position edge.

Existing Bundle 2.0, 2.1, and 2.2 assets produced by supported exporters can
be reimported directly with Unity package 2.2.1. No Blender re-export is
required for this fix.
