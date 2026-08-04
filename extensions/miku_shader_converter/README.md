# Miku Shader Converter 2.2.8

This deterministic Blender 5.2.0 extension combines the semantic exporter and
GPL bake worker in `miku_shader_converter-2.2.8.zip`.

The visible Blender panel is intentionally Standard PBR only. **Export Current
Material** always writes `standard_pbr`; it does not rewrite old workflow
custom properties. Explicit lower-level Python workflow calls, historical game
Bundles, and the Unity importer remain available for scripts and legacy assets.

The panel follows Blender's English/Simplified Chinese language setting and
keeps the 2.2.8 controls for normal convention, displacement policy, advanced
conversion mode, bake quality (512/1024/2048/4096), and source identity. An
effective `Input.Time.*` dependency fails before any output or bake request is
written with `MIKU_TIME_INPUT_UNSUPPORTED`; disconnected time nodes remain
valid.

Install through **Edit > Preferences > Extensions > Install from Disk**. Do
not copy a checked-out repository into Blender's Scripts directory. Build from
the canonical repository with:

```powershell
python tools/build_miku_blender_extensions.py
```

Validation uses only
`C:\SteamLibrary\steamapps\common\Blender\blender.exe` and asserts
`bpy.app.version == (5, 2, 0)`.
