# Miku Shader Converter 3.0.0

This deterministic Blender 5.0-5.2 extension combines the semantic
exporter and GPL bake worker in `miku_shader_converter-3.0.0.zip`.

The visible Blender panel is intentionally Standard PBR only. **Export Current
Material** always writes `standard_pbr`; it does not rewrite old workflow
custom properties. Explicit lower-level Python workflow calls, historical game
Bundles, and the Unity importer remain available for scripts and legacy assets.

The panel follows Blender's English/Simplified Chinese language setting and
keeps the 3.0.0 controls for normal convention, displacement policy, advanced
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
`bpy.app.version == (5, 2, 0)`. Public matrix smoke tests additionally target
5.0.1 and 5.1.2. Blender 5.0-5.2 is admitted; unrecorded patches warn and run
capability preflight. Blender 5.3+ is rejected.
