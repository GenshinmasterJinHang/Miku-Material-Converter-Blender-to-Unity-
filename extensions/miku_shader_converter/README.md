# Miku Shader Converter 1.0

This is the single Blender 5.2.0 extension distributed by Miku. It combines
the semantic exporter and the GPL bake worker in one deterministic
`miku_shader_converter-1.0.1.zip`.

The aggregate extension is licensed under `GPL-3.0-or-later`. Files originating
from the Miku MIT codebase retain their SPDX headers and the archive includes
the MIT license and third-party notices.

Install the ZIP through **Edit > Preferences > Extensions > Install from
Disk**. Do not copy repository directories into Blender's Scripts folder.

In the Shader Editor sidebar, choose the active Material's workflow and use
**Export Current Material**. New exports contain a `.mikubundle` entry and
Miku 1.0 documents only. The extension preserves stable source and material
UUIDs. On first access it copies supported legacy `migr_*` custom properties
to their `miku_*` equivalents without deleting or rewriting the legacy values.

The extension exports materials. It does not ask for a Model Root, scan
Renderers, build a Mesh/material-slot tree, generate a character Prefab, or
replace any Unity Renderer material reference.

Build the deterministic archive from the canonical repository root:

```powershell
python tools/build_miku_blender_extensions.py
```

Blender validation must use
`C:\SteamLibrary\steamapps\common\Blender\blender.exe` and assert
`bpy.app.version == (5, 2, 0)`.
