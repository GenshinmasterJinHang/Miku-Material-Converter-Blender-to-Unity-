# Miku 2.2.8

Miku converts Blender 5.2 materials into target-neutral MaterialIR 2.0 and
imports them into Unity 6000.4.5f1, URP 17.4.0, and Shader Graph 17.4.0.

The Unity package provides an independent English / Simplified Chinese Miku
Editor UI switch at `Miku/Settings`. It is a per-user `EditorPrefs` preference
and does not follow the Unity Editor language or affect generated assets.

Supported workflows are:

- `standard_pbr`
- `genshin_toon`
- `wuwa_toon`
- `hsr_toon`
- `endfield_toon`

The public identities remain `miku`, `miku_blender`, the
`miku_shader_converter` Blender extension, and the
`com.miku.shaderconverter` Unity package. The Blender extension is version
`2.2.8`; the independently installable Unity package is `2.2.8`.

## Blender

Build `miku_shader_converter-2.2.8.zip`, select a material workflow, choose an
output directory, and export. New exports contain MaterialIR 2.0 and a
deterministic `.mikubundle`. Generic Toon is retired: saved `generic_toon`
properties and old Generic Toon inputs fail with
`MIKU_WORKFLOW_RETIRED:generic_toon`; they never silently fall back to
Standard PBR.

The extension follows Blender's interface language for English and Simplified
Chinese. Under **Advanced**, **Bake Texture Quality** selects 512, 1024, 2048,
or 4096 resolution for generated 2D bake textures. The default remains 1024;
the setting has no effect when conversion does not schedule a bake.
New Blender exports reject effective `Input.Time.*` dependencies before any
output or bake request is written (`MIKU_TIME_INPUT_UNSUPPORTED`); disconnected
time nodes remain harmless. Historical time-dependent Bundles remain importable
in Unity, while the legacy Blender time-node and identity-migration operators
are retained only for scripts and are hidden from Advanced.

## Unity

Install `com.miku.shaderconverter` and copy a complete `.mikubundle` directory
under `Assets/`. Standard PBR creates editable Shader Graph assets. The four
game workflows use their fixed ShaderLab/HLSL materials and shared Game Toon
Screen Rim and Mesh tools. The 2.2.8 package also ships the opt-in
`MikuAnimeGlobalVolumeProfile` with a ten-component URP 17.4 grading stack;
the Vertex Color Initializer and Combined Mesh Data menu entries are hidden,
while their public mesh APIs remain available to existing editor scripts.
Generated assets retain stable identities and do not overwrite user-owned
wrapper graphs.

Existing Generic Toon materials, recipes, and wrappers in a Unity project are
not deleted by package installation. Their removed shaders may show Missing
Shader. Back up the project and manually choose Standard PBR or a game workflow,
then re-export, import, and bind each material; see
[the Miku 2.0 migration guide](docs/migrations/retire-generic-toon-2.0.md).

## Compatibility and validation

The certified Windows tuple is Blender 5.2.0 at
`C:\SteamLibrary\steamapps\common\Blender\blender.exe`, Unity 6000.4.5f1,
URP 17.4.0, and Shader Graph 17.4.0. Run:

```text
py -3.13 tools/ci/run_checks.py --profile pr
py -3.13 -m unittest discover -s tests -p "test_*.py"
```

MaterialIR 1.0 remains frozen for the four non-retired workflows. Bundle,
plan, manifest, and target-profile schemas remain 1.0.
