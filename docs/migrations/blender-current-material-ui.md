# Blender current-material UI migration

This guide is current for Miku 2.0. The retired `generic_toon` entry is not
offered by the UI; saved values fail with `MIKU_WORKFLOW_RETIRED:generic_toon`.

Miku 1.0 simplifies the Blender Shader Editor panel without changing the bundle
schema or Unity importer.

- The panel exports only the active object's active material slot.
- Persistent Source ID is hidden. Existing hidden scene `miku_source_id` data
  is retained; otherwise the extension creates one blend-wide identity in a
  canonical Scene ID property. Blender `BlendData` does not support ID
  properties.
- The old scene Default Workflow and material Inherit Scene controls are hidden.
  On first display or export, Inherit Scene resolves to the old scene default
  and becomes a concrete material workflow.
- Explicit Standard PBR, Generic Toon, Genshin Toon, WuWa Toon, HSR Toon, and
  game-part values are retained.
- Conversion Mode moves under a collapsed Advanced section.
- `export_selected_materials` remains available to automation callers.

No Blender Scripts directory migration is required. Install the rebuilt
`miku_shader_converter-1.0.1.zip` through Blender 5.2 Extensions. Existing
Miku 1.0 bundle identities remain valid.
