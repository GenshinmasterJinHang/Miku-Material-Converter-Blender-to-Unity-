# Migrating to Miku 2.0

Miku 2.0.0 is emitted by the 2.0.0 Blender extension and consumed by the
2.0.0 Unity package. New documents use:

- `version: "miku-2.0"`
- `schema: "miku-preset-2.0"` when a preset companion is present
- `schemaVersion: 3` for the target-neutral NPR overlay
- `hsrToonPreset.schema: "hsr-toon-1.1"` for newly exported HSR materials

The canonical `miku-2.0` schema directly validates the optional numeric
`schemaVersion` (1 through 3) and `nprFeatures` overlay members. This aligns the
strict graph schema with fields already emitted by the Blender add-on; it does
not rename or reinterpret either public field.

Legacy MaterialIR 1.0 documents remain readable only for Standard PBR, Genshin,
WuWa, and HSR. Generic Toon inputs return
`MIKU_WORKFLOW_RETIRED:generic_toon`; no shader, material, wrapper, or sidecar
is generated.

HSR documents without a semantic version use the migration rules documented in
[`hsr-toon-1.1.md`](hsr-toon-1.1.md). Other Miku 2.0 workflows are unchanged.

There is no automatic visual migration between workflows. Re-export or author
the material with Standard PBR, Genshin, WuWa, or HSR as appropriate.
