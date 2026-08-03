# Migrating away from Generic Toon for Miku 2.0

Miku 2.0 retires `generic_toon`. Back up the Unity project and Blender sources
before upgrading. The package does not delete project assets or rewrite a
Generic Toon material into another visual model.

For every affected material, choose one of the supported routes:

- `standard_pbr` for an editable URP Shader Graph material;
- `genshin_toon`, `wuwa_toon`, or `hsr_toon` when the material follows that
  game's fixed texture and part contract.

Re-export the material, import the new bundle, and reassign the generated user
material to the relevant Renderer slots. Existing Generic Toon bundles fail
with `MIKU_WORKFLOW_RETIRED:generic_toon` before any output asset is written.
Legacy MiGR Generic Toon migration is intentionally unsupported; there is no
safe automatic visual conversion.

MaterialIR 1.0 remains readable for Standard PBR and the three game workflows.
New exports use MaterialIR 2.0, whose workflow enum does not contain
`generic_toon`.
