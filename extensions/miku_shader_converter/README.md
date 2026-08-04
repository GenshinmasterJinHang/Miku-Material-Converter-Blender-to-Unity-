# Miku Shader Converter 2.2.9

This deterministic Blender 5.0.0 through 5.2.0 extension combines the semantic
exporter and GPL bake worker in `miku_shader_converter-2.2.9.zip`.

The visible Blender panel is intentionally Standard PBR only. **Export Current
Material** always writes `standard_pbr`; it does not rewrite old workflow
custom properties. Explicit lower-level Python workflow calls, historical game
Bundles, and the Unity importer remain available for scripts and legacy assets.

The panel follows Blender's English/Simplified Chinese language setting and
keeps the 2.2.9 controls for normal convention, displacement policy, advanced
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
`bpy.app.version == (5, 2, 0)`. Blender 5.0 and 5.1 are allowed with an
unvalidated-version warning but are not claimed as validated.
