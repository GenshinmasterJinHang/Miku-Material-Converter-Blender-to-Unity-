# Miku schema map

Most active interchange documents use `schemaVersion: "1.0"`. Bake requests
1.1 and 1.2 are the current exceptions. Every document forbids the legacy root
`version` field.

| Schema | Purpose |
| --- | --- |
| `miku-target-profile-1.0` | Exact Blender/Unity/backend tuple, budgets, and implementation hashes |
| `miku-material-ir-1.0` | Typed value DAG, closure graph, workflow, and selected surface model |
| `miku-conversion-plan-1.0` | Deterministic native/bake/unsupported decisions |
| `miku-conversion-manifest-1.0` | Blender/core completion and diagnostic evidence |
| `miku-blender-source-map-1.0` | Private authoring provenance |
| `miku-bundle-1.0` | Hashed manifest and sibling artifact references |
| `miku-bake-request-1.0` | Frozen 1024-resolution MIT exporter request accepted for compatibility |
| `miku-bake-request-1.1` | MIT exporter request with certified 512/1024/2048/4096 bake resolution |
| `miku-bake-request-1.2` | Version-bound request for Blender 5.0.0 through 5.2.0 |
| `miku-bake-result-1.0` | GPL worker result and hashed resources |
| `miku-unity-import-receipt-1.0` | Unity generation/commit evidence |

Each JSON Schema `$id` includes its explicit document version. Unknown kinds
and versions fail explicitly. Unity internal classes and Blender object
references are forbidden in target-neutral documents.

Frozen MiGR schemas live under `schema/legacy/migr/` for validation of
read-only migration input. They are not active output schemas. Migration first
validates the legacy bytes and canonical hash, then normalizes to Miku 1.0.
New writers must never emit a legacy document kind.

### Fixed-workflow UV transforms

Miku 2.2.7 additively extends `miku-bundle-1.0` fixed-workflow
`materialBindings`. Wuwa Eye recognizes `EyeHDMF`, `EyeUpperHighlight`, and
`EyeLowerHighlight` in addition to `EyeHET` and `EyeEG`. A binding may contain:

```json
{
  "role": "EyeUpperHighlight",
  "uvTransform": {
    "coordinateSpace": "UV0",
    "operation": "Affine2D",
    "matrix": [0.68, 0.0, 0.13, 0.0, 1.27, -0.05]
  }
}
```

The six values are two row-major affine rows applied to `[u, v, 1]`. All
values must be finite. Absence means identity. Older strict readers reject the
new roles/field clearly, so bundles using them require a 2.2.7 consumer even
though the document family remains 1.0.
