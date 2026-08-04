# Miku 2.2.8

[![CI](https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/actions/workflows/ci.yml/badge.svg)](https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/code%20license-MIT-blue.svg)](LICENSE)
[![Blender](https://img.shields.io/badge/Blender-5.2.0-orange.svg)](docs/compatibility.md)
[![Unity](https://img.shields.io/badge/Unity-6000.4.5f1-black.svg)](docs/compatibility.md)

Miku converts Blender 5.2 EEVEE materials into target-neutral MaterialIR 2.0,
then imports deterministic, editable Standard PBR Shader Graph assets into
Unity 6 URP. Blender exposes one Standard PBR route; four bundled Game Toon
Shader/HLSL preset families are authored from the Unity Editor.

![Miku conversion flow](docs/images/miku-workflow-en.svg)

English is the canonical public documentation language. Read the
[Simplified Chinese README](docs/zh-CN/README.md) or the full
[English Manual](docs/manual.md) / [Chinese Manual](docs/zh-CN/manual.md).

## Highlights

- One visible Blender Standard PBR export route with declared approximations,
  isolated texture baking, deterministic bundles, and time-input preflight.
- Editable Unity Shader Graph wrappers, Miku-owned generated Sub Graphs, stable
  IDs, structured diagnostics, and historical Bundle 1.0 compatibility.
- A Unity material creator at `Miku > Game Toon > Materials > Create Material`
  with explicit texture fields, required-field validation, no asset overwrite,
  and no automatic Renderer, FBX, or Prefab binding.
- English/Simplified Chinese Miku Editor UI selected per user at
  `Miku > Settings`.

## Bundled Game Toon Shader/HLSL presets

The Unity package ships first-party, original Miku Shader/HLSL code for four
experimental preset families. These are working shader implementations, not
only field-name templates.

| Preset | Available material parts | Main authored features |
| --- | --- | --- |
| Genshin | Body, Hair, Face, Eye | Light-map/ramp Toon lighting, Face SDF, hair, eye, outline, and screen-rim support |
| Honkai: Star Rail (HSR) | Body, Hair, Face, Eye | Light-map/ramp shading, Face SDF, hair highlights, eyes, and outline support |
| Wuthering Waves (Wuwa) | Body, Hair, Face, Eye, Effect | ID/Stockings dual binding, face basis controls, authored eye maps, highlights, and emission |
| Arknights: Endfield | Body, Skin, Hair, Face, Eye, Mouth, Overlay, Effect, HairShadow | Parameter/ramp/LUT maps, Face SDF, hair refine/shift/line maps, overlays, effects, and hair shadow |

Together the creator exposes 22 valid material parts. The presets are
**Experimental** compatibility implementations and do not promise pixel-exact
parity with any game. Miku includes no extracted game Shader source, model,
texture, logo, or other game asset. See the [Manual](docs/manual.md) for every
texture rule, the example gallery, and the Unity Editor tool guide.

## Install the release

1. Download `miku_shader_converter-2.2.8.zip` and
   `com.miku.shaderconverter-2.2.8.tgz` from the
   [v2.2.8 Release](https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/releases/tag/v2.2.8).
2. In Blender 5.2, open **Edit > Preferences > Extensions**, choose
   **Install from Disk**, select the ZIP, and enable Miku.
3. In Unity 6000.4.5f1, open **Window > Package Manager > + > Add package from
   tarball**, then select the TGZ.
4. Export a material bundle from Blender and copy its complete directory under
   the Unity project's `Assets/` folder.

For a source checkout, add
`unity/Packages/com.miku.shaderconverter/package.json` from disk. Full
installation, ownership, diagnostics, and upgrade guidance are in the
[Manual](docs/manual.md).

## Five-minute workflow

1. Select an object with a material in Blender's Shader Editor.
2. Open the **Miku** sidebar, choose an output directory, and confirm
   **Standard PBR**.
3. Export the current material. A reachable `Input.Time.*` dependency stops
   before output or bake requests are written; disconnected time nodes are
   harmless.
4. Copy the complete `.mikubundle` directory to Unity `Assets/` and let the
   importer create editable graph and report assets.
5. For a Game Toon material, open the Unity creator, choose a preset and part,
   assign the displayed textures, and save a user-owned `.mat`.

![Blender Standard PBR panel](docs/images/blender-standard-pbr-en.png)

## Compatibility and licensing

The validated Windows tuple is Blender 5.2.0, Unity 6000.4.5f1, URP 17.4.0,
and Shader Graph 17.4.0. Miku is currently **Experimental**. MaterialIR 2.0,
Bundle 1.0, conversion-plan, bake-result, and public Shader property/reference
names are unchanged in 2.2.8. See the
[compatibility matrix](docs/compatibility.md).

The repository's MIT-licensed code remains under the [MIT License](LICENSE);
files carrying separate SPDX terms, including the Blender Bake Worker, retain
those terms. The four character renders shown in the manuals are separate
documentation assets:
they are provided only for non-commercial learning and documentation reference,
and commercial use is prohibited. Related characters, designs, and intellectual
property belong to their respective rights holders; Miku grants no rights to
game assets. See [Third-party notices](THIRD_PARTY_NOTICES.md) and the
[documentation image provenance](docs/provenance/documentation-images.md).

## Development

```powershell
py -3.13 -m unittest discover -s tests -p "test_*.py"
py -3.13 tools/ci/run_checks.py --profile pr
py -3.13 tools/release/build_release.py --output-dir artifacts
```

Read [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md), and
[SUPPORT.md](SUPPORT.md) before contributing or distributing a build.
