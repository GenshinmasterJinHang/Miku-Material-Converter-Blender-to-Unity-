# Miku 2.2.8 Manual

Miku is a production-oriented Blender 5.2 to Unity 6 material converter. The
public Blender front end exports Standard PBR semantics into target-neutral
MaterialIR 2.0. Unity then creates editable Shader Graph assets. Game Toon
materials are authored explicitly in Unity and are not selected from the
Blender panel.

See the [Chinese manual](zh-CN/manual.md) for the equivalent Simplified Chinese
version and the [English README](../README.md) for the short project overview.

![Miku workflow](images/miku-workflow-en.svg)

## 1. Supported environment

The validated tuple is:

| Component | Version | Status |
| --- | --- | --- |
| Blender | 5.2.0 | Windows validated |
| Unity Editor | 6000.4.5f1 | Windows validated |
| Universal Render Pipeline | 17.4.0 | Required |
| Shader Graph | 17.4.0 | Version-specific backend |
| Miku | 2.2.8 | Experimental |

Other versions are unsupported unless separately validated. Miku should fail
with a diagnostic instead of guessing an incompatible Shader Graph format.

## 2. Install from a Release

Download these files from the [v2.2.8 GitHub Release](https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/releases/tag/v2.2.8):

- `miku_shader_converter-2.2.8.zip` — Blender 5.2 extension.
- `com.miku.shaderconverter-2.2.8.tgz` — Unity package.
- `SHA256SUMS.txt` — release integrity manifest.

In Blender, choose **Edit > Preferences > Extensions > Install from Disk** and
select the ZIP. In Unity, choose **Window > Package Manager > + > Add package
from tarball** and select the TGZ. Enable URP 17.4.0 and Shader Graph 17.4.0 in
the project before importing a bundle.

For source development, add
`unity/Packages/com.miku.shaderconverter/package.json` from disk. Do not patch
an installed extension copy or an embedded validation package; the canonical
sources are `miku/`, `miku_blender/`, `extensions/miku_shader_converter/`, and
`unity/Packages/com.miku.shaderconverter/`.

## 3. Blender: export Standard PBR

1. Open a material in the Shader Editor and select an object with the material
   in its active material slot.
2. Open the **Miku** sidebar and choose an output folder.
3. The visible material route is **Standard PBR**. Set normal-map convention
   and displacement policy only when the source requires them.
4. Expand **Advanced** only for conversion mode, fidelity policy, additive
   shader energy policy, bake texture quality, or source identity forking.
5. Click **Export Current Material**.

![Blender Standard PBR panel in English](images/blender-standard-pbr-en.png)

The exporter snapshots the active material, validates the lowered IR, and only
then creates its output directory and staging files. A reachable time
expression fails with `MIKU_TIME_INPUT_UNSUPPORTED` before an output or bake
request is written. Disconnected time nodes are allowed. Use a new static
source or a separate runtime implementation when animation is required.

The Blender UI no longer offers game workflow selection, texture-role guessing,
or the old identity migration entry point. Existing workflow properties remain
in files for compatibility, and the lower-level Python API still accepts an
explicit legacy workflow for scripts and old fixtures. The public current-
material operator always emits `standard_pbr` and does not rewrite those old
properties.

### Bake quality

The Advanced **Bake Texture Quality** values are 512, 1024, 2048, and 4096.
The default is 1024. The setting has no effect when the conversion plan does
not schedule a bake. Baking uses isolated temporary data and does not change
the source `.blend` file.

### Exported ownership

The export directory contains a deterministic `.mikubundle`, reports, and any
required baked resources. Treat the complete directory as one unit when moving
it into Unity. Do not rename files inside a bundle or copy only the JSON file.

## 4. Unity: import an editable bundle

Copy the complete `.mikubundle` directory under `Assets/`. The importer creates
the version-specific Shader Graph wrapper, Miku-owned generated Sub Graph,
materials, reports, and source mappings. Generated assets have stable IDs and
deterministic ordering.

Ownership is explicit:

- `*.generated.shadersubgraph` and generated reports are Miku-owned and may be
  regenerated.
- The wrapper `*.shadergraph` is user-owned after initial creation.
- A full regeneration must be explicitly selected before replacing a modified
  wrapper graph.

MaterialIR 2.0, Bundle 1.0, conversion-plan, bake-result, and public Shader
property/reference names are stable in 2.2.8. Historical bundles, including
older runtime-time contracts, remain readable by the Unity importer even though
the current Blender front end does not create new time-dependent bundles.

## 5. Unity Game Toon material creator

Open **Miku > Game Toon > Materials > Create Material**. Select one workflow and
one filtered material part, then assign the visible texture fields explicitly.
The tool does not inspect filenames or folders and never changes importer
settings.

| Workflow | Parts |
| --- | --- |
| Genshin | Body, Hair, Face, Eye |
| Wuthering Waves | Body, Hair, Face, Eye, Effect |
| HSR | Body, Hair, Face, Eye |
| Endfield | Body, Skin, Hair, Face, Eye, Mouth, Overlay, Effect, HairShadow |

The fields are generated from the matching package shader's public 2D texture
properties in declaration order. Legacy `_MainTex` and hidden compatibility
properties are excluded. `_BaseMap` is required for every part except Endfield
Mouth, whose mouth map is optional. Wuwa Body's visible ID texture is assigned
to both the ID and stockings source expected by the shader.

![Unity Game Toon material creator in English](images/unity-game-material-wizard-en.png)

The wizard validates the shader, output path, existing asset, slot count, and
required textures before creating anything. It creates a user-owned `.mat`,
assigns the selected textures, applies the existing recommended skin/highlight
profile, synchronizes shader keywords, and saves. It does not bind a renderer,
change an FBX or prefab, overwrite an existing material, or create a recipe
implicitly.

The existing three-argument `CreateMaterialAsset` API remains available for
scripts that need an empty user-owned template. The visible menu uses the
configured texture path described above.

## 6. Editor languages

Blender follows Blender's English/Simplified Chinese interface translation.
Unity has an independent per-user setting at **Miku > Settings**. Select
`English` or `简体中文`; the preference is stored in `EditorPrefs` under
`com.miku.shaderconverter.editorLanguage`.

The preference does not change Unity's global editor language, project files,
generated assets, stable property names, diagnostics, JSON, or static menu
paths. Miku-authored windows, custom inspectors, ShaderGUI labels, dialogs,
help boxes, Undo labels, and friendly status messages follow the preference.

## 7. Diagnostics and troubleshooting

- `MIKU_TIME_INPUT_UNSUPPORTED`: remove a reachable time dependency from the
  exported Blender output chain, or keep the historical bundle on the Unity
  side for compatibility.
- `MIKU_WORKFLOW_RETIRED:generic_toon`: select Standard PBR in Blender or use a
  supported Unity Game Toon workflow; no silent visual substitution is made.
- `MIKU_REQUIRED_TEXTURE_MISSING`: assign the required field in the Unity
  creator. Endfield Mouth is the only part with an optional Base Map.
- `MIKU_ASSET_OUTPUT_PATH_INVALID`: choose a `.mat` path under `Assets/` and
  avoid `.` or `..` path segments.
- Missing shader or incompatible version: verify the exact Unity/URP/Shader
  Graph tuple before importing.

If an existing project material shows Missing Shader after upgrading, the
package does not delete the material. Back up the project, choose Standard PBR
or a Unity Game Toon material, then re-export or recreate the user-owned
material deliberately.

## 8. Development and validation

```powershell
py -3.13 -m unittest discover -s tests -p "test_*.py"
py -3.13 tools/ci/run_checks.py --profile pr
py -3.13 tools/ci/run_checks.py --profile release
py -3.13 tools/release/build_release.py --output-dir artifacts
```

The exact Blender executable for validation is
`C:\SteamLibrary\steamapps\common\Blender\blender.exe`. The exact Unity
executable is
`C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe`.
Validation must assert the editor versions before using generated evidence.

Read [CONTRIBUTING.md](../CONTRIBUTING.md), [SECURITY.md](../SECURITY.md),
[SUPPORT.md](../SUPPORT.md), the [compatibility matrix](compatibility.md), and
the [release process](release/process.md) before distributing a build.
