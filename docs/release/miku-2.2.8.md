# Miku 2.2.8

Release date: 2026-08-03

## Packages

- Blender extension: `miku_shader_converter-2.2.8.zip`
- Unity package: `com.miku.shaderconverter-2.2.8.tgz`

## Highlights

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

MaterialIR, Bundle, Conversion Plan, bake-result, and Unity shader public
interfaces are unchanged.

## Validated versions

Blender 5.2.0, Unity 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0 on Windows.

The deterministic build hashes are recorded in the adjacent
`miku-2.2.8-sha256.txt` file.
