# Miku 1.0 schema map

All active interchange documents use `schemaVersion: "1.0"` and forbid the
legacy root `version` field.

| Schema | Purpose |
| --- | --- |
| `miku-target-profile-1.0` | Exact Blender/Unity/backend tuple, budgets, and implementation hashes |
| `miku-material-ir-1.0` | Typed value DAG, closure graph, workflow, and selected surface model |
| `miku-conversion-plan-1.0` | Deterministic native/bake/unsupported decisions |
| `miku-conversion-manifest-1.0` | Blender/core completion and diagnostic evidence |
| `miku-blender-source-map-1.0` | Private authoring provenance |
| `miku-bundle-1.0` | Hashed manifest and sibling artifact references |
| `miku-bake-request-1.0` | MIT exporter to the bundled GPL worker request |
| `miku-bake-result-1.0` | GPL worker result and hashed resources |
| `miku-unity-import-receipt-1.0` | Unity generation/commit evidence |

Each JSON Schema `$id` is `urn:miku:schema:<kind>:1.0`. Unknown kinds and
versions fail explicitly. Unity internal classes and Blender object references
are forbidden in target-neutral documents.

Frozen MiGR schemas live under `schema/legacy/migr/` for validation of
read-only migration input. They are not active output schemas. Migration first
validates the legacy bytes and canonical hash, then normalizes to Miku 1.0.
New writers must never emit a legacy document kind.
