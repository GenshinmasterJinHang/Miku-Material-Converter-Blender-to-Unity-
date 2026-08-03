# Miku 2.2.8

Release date: 2026-08-03

## Packages

- Blender extension: `miku_shader_converter-2.2.8.zip`
- Unity package: `com.miku.shaderconverter-2.2.8.tgz`
- Integrity manifest: `SHA256SUMS.txt`

## Highlights

- The Blender current-material panel now exposes only Standard PBR. It always
  emits `standard_pbr` while preserving old `.blend` properties and explicit
  lower-level legacy workflow calls for compatibility.
- Unity now exposes `Miku > Game Toon > Materials > Create Material`. The
  creator supports Genshin, Wuwa, HSR, and Endfield with 22 filtered parts and
  declaration-ordered public `Texture2D` inputs. `_BaseMap` is required except
  for Endfield Mouth; Wuwa Body's ID / Stockings Map binds both source
  properties. Failed validation leaves no partial material asset.
- The public README and Manual are bilingual, with reproducible workflow
  diagrams, screenshots, release-first installation, ownership rules, and
  exact-version validation instructions.
- Unity Miku editor UI has an independent per-user English/Simplified Chinese
  selector at `Miku/Settings`. It does not follow or modify the Unity Editor
  language and does not enter generated assets.
- Blender Advanced no longer exposes the legacy time-node and identity
  migration buttons. Their operator IDs and legacy readers remain available
  for scripts and old data.
- New Blender exports fail before any output or bake request is written when an
  effective output chain uses `Input.Time.*`, with diagnostic code
  `MIKU_TIME_INPUT_UNSUPPORTED`. Disconnected time nodes remain valid, and
  historical MiGR/time Bundles remain importable in Unity.

MaterialIR 2.0, Bundle 1.0, Conversion Plan, Bake Result, and Unity shader
property/reference names are unchanged.

## Validated versions

Blender 5.2.0, Unity 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0 on Windows.

The final deterministic build hashes are recorded in the adjacent
`miku-2.2.8-sha256.txt` file after the release build is regenerated.
