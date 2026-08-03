# Miku 2.2.8

[![CI](https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/actions/workflows/ci.yml/badge.svg)](https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Blender](https://img.shields.io/badge/Blender-5.2.0-orange.svg)](docs/compatibility.md)
[![Unity](https://img.shields.io/badge/Unity-6000.4.5f1-black.svg)](docs/compatibility.md)

Miku converts Blender 5.2 EEVEE materials into target-neutral MaterialIR 2.0,
then imports deterministic, editable Standard PBR Shader Graph assets into
Unity 6 URP. Game-specific Toon materials remain available as Unity-side
authoring tools and are no longer selected in the Blender export panel.

![Miku conversion flow](docs/images/miku-workflow-en.svg)

English is the canonical public documentation language. See the
[简体中文 README](docs/zh-CN/README.md) and the full
[English Manual](docs/manual.md) / [中文手册](docs/zh-CN/manual.md).

## What Miku does

- Blender exporter: one visible Standard PBR path with editable semantic
  conversion, declared approximations, and isolated texture baking when it is
  required.
- Unity importer: deterministic `.mikubundle` import, editable Shader Graph
  wrappers, generated Sub Graph ownership, stable IDs, and structured reports.
- Unity Game Toon creator: explicit texture inputs for Genshin, Wuthering
  Waves, HSR, and Endfield materials under `Miku > Game Toon > Materials >
  Create Material`.
- Independent Unity UI language preference at `Miku > Settings` for English or
  Simplified Chinese. The preference is per-user and never enters generated
  assets.

## Install the release

1. Download the two assets from the [v2.2.8 Release](https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/releases/tag/v2.2.8):
   `miku_shader_converter-2.2.8.zip` and
   `com.miku.shaderconverter-2.2.8.tgz`.
2. In Blender 5.2, open **Edit > Preferences > Extensions**, choose **Install
   from Disk**, select the Blender ZIP, and enable Miku.
3. In Unity 6000.4.5f1, open **Window > Package Manager > + > Add package from
   tarball**, then select the Unity TGZ.
4. Export a material bundle from Blender and copy the complete bundle directory
   under the Unity project's `Assets/` folder.

For a source checkout, add `unity/Packages/com.miku.shaderconverter/package.json`
from disk. The complete installation, ownership, diagnostics, and upgrade
guidance are in the [Manual](docs/manual.md).

## Five-minute workflow

1. Select an object with a material in Blender's Shader Editor.
2. Open the **Miku** sidebar, choose an output directory, and confirm the
   **Standard PBR** path.
3. Export the current material. A reachable `Input.Time.*` dependency stops
   before any output or bake request is written; disconnected time nodes are
   harmless.
4. Copy the resulting `.mikubundle` directory to Unity `Assets/` and let the
   importer create the editable graph and report files.
5. For a game material, use the Unity Game Toon material creator, choose a
   workflow and part, assign the visible texture slots explicitly, and save a
   user-owned `.mat`.

![Blender Standard PBR panel](docs/images/blender-standard-pbr-en.png)

## Compatibility

The validated Windows tuple is Blender 5.2.0, Unity 6000.4.5f1, URP 17.4.0,
and Shader Graph 17.4.0. The project is currently marked **Experimental**;
unsupported version combinations must fail clearly. See the
[compatibility matrix](docs/compatibility.md) for format and workflow details.

MaterialIR 2.0, Bundle 1.0, conversion-plan, bake-result, and public Shader
property/reference names are unchanged in 2.2.8. Historical bundles remain
importable, but the current Blender UI does not create new game-workflow or
time-dependent bundles.

## Development

```powershell
py -3.13 -m unittest discover -s tests -p "test_*.py"
py -3.13 tools/ci/run_checks.py --profile pr
py -3.13 tools/release/build_release.py --output-dir artifacts
```

The exact Blender and Unity validation commands, screenshot sources, schema
policy, security guidance, and contribution rules are documented in the
[Manual](docs/manual.md), [CONTRIBUTING.md](CONTRIBUTING.md),
[SECURITY.md](SECURITY.md), and [SUPPORT.md](SUPPORT.md).
