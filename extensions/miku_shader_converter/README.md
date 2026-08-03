# Miku Shader Converter 2.2.8

This is the single Blender 5.2.0 extension distributed by Miku. It combines
the semantic exporter and the GPL bake worker in one deterministic
`miku_shader_converter-2.2.8.zip`.

The extension follows Blender's English or Simplified Chinese interface
language. Advanced settings expose Low 512, Standard 1024, High 2048, and
Ultra 4096 resolutions for generated 2D bake textures; the default remains
1024 and the setting applies only when the conversion plan schedules a bake.
New exports reject effective `Input.Time.*` dependencies before writing output
or bake requests with `MIKU_TIME_INPUT_UNSUPPORTED`; disconnected time nodes
remain harmless. Historical time-dependent Bundles are still readable by
Unity, and the two legacy time-node/identity-migration operators remain only
for script compatibility and are not shown in Advanced.

The aggregate extension is licensed under `GPL-3.0-or-later`. Files originating
from the Miku MIT codebase retain their SPDX headers and the archive includes
the MIT license and third-party notices.

Install the ZIP through **Edit > Preferences > Extensions > Install from
Disk**. Do not copy repository directories into Blender's Scripts folder.

In the Shader Editor sidebar, choose the active Material's workflow and use
**Export Current Material**. New exports contain a `.mikubundle` entry and
MaterialIR 2.0 documents. The extension preserves stable source and material
UUIDs. On first access it copies supported legacy `migr_*` custom properties
to their `miku_*` equivalents without deleting or rewriting the legacy values.

Supported workflows are Standard PBR, Genshin Toon, WuWa Toon, HSR Toon, and
Endfield Toon.
Generic Toon is retired; old saved values fail with
`MIKU_WORKFLOW_RETIRED:generic_toon`.

For Standard PBR, **Portable Hybrid (Prefer Native)** retains supported dynamic
view/camera/time expressions and bakes only proven UV0 static islands without a
SourceMesh. **Full PBR Bake (Source Mesh)** remains explicitly mesh-bound.

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
