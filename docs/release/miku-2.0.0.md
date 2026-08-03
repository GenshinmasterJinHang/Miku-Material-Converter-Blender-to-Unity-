# Miku 2.0.0

Miku 2.0.0 retires Generic Toon and coordinates the Python package, Blender
extensions, and Unity package at version 2.0.0.

## Highlights

- New exports use `miku-material-ir-2.0`; MaterialIR 1.0 is frozen.
- Standard PBR, Genshin Toon, WuWa Toon, and HSR Toon remain supported.
- Shared Screen Rim and Mesh tooling is now under Game Toon with stable asset
  GUIDs and a `MovedFrom` compatibility marker.
- Generic Toon shaders, backend, builder, recipe semantic APIs, menus, and
  texture mappings are removed.

## Migration

Back up the Unity project before upgrading. Package installation does not
delete old materials, recipes, or wrapper graphs in `Assets/`; old materials may
show Missing Shader. For each Generic Toon material, manually select Standard
PBR or the appropriate game workflow, re-export from Blender, import the new
bundle, and rebind the material to its Renderer. No automatic visual conversion
is attempted.

Old Generic Toon MaterialIR or MiGR input fails before output writes with
`MIKU_WORKFLOW_RETIRED:generic_toon`.
