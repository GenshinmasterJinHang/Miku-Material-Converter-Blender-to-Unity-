# Migrating to Miku 2.0

Miku 2.0 is emitted by Blender add-on 0.6.1 and consumed by Unity package 0.9.1.
New documents use:

- `version: "miku-2.0"`
- `schema: "miku-preset-2.0"` when a preset companion is present
- `schemaVersion: 3` for the target-neutral NPR overlay
- `hsrToonPreset.schema: "hsr-toon-1.1"` for newly exported HSR materials

The canonical `miku-2.0` schema directly validates the optional numeric
`schemaVersion` (1 through 3) and `nprFeatures` overlay members. This aligns the
strict graph schema with fields already emitted by the Blender add-on; it does
not rename or reinterpret either public field.

Legacy `miku-1.0` and overlay 1/2 documents remain readable when they use a
currently supported workflow. Unknown and retired non-empty preset identifiers
return `unsupported_preset`; no shader, material, wrapper, or sidecar is
generated.

HSR documents without a semantic version use the migration rules documented in
[`hsr-toon-1.1.md`](hsr-toon-1.1.md). Other Miku 2.0 workflows are unchanged.

The Endfield preset has no automatic migration because substituting a generic
or different game shader would silently change material semantics. Re-export or
author the material with Standard PBR, Generic Toon, Genshin, Wuwa, or HSR as
appropriate.
