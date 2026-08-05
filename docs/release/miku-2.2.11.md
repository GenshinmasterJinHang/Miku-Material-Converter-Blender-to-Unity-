# Miku 2.2.11

Miku 2.2.11 replaces strict closed version ranges with a major-version
validation policy. Any Blender 5.x, any Unity 6000.x (Unity 6), and any
URP/Shader Graph 17.x is admitted. The certified (warning-free) reference is
Blender 5.2.0, Unity 6000.5.4f1, and URP/Shader Graph 17.5.4; other admitted
combinations continue with explicit `MIKU_*_VERSION_UNVALIDATED` diagnostics.
Wrong-major versions fail before any asset write.

## Release assets

- `miku_shader_converter-2.2.11.zip`
- `com.miku.shaderconverter-2.2.11.tgz`
- `SHA256SUMS.txt`

Install the ZIP through Blender's **Extensions > Install from Disk** and the
TGZ through Unity Package Manager's **Add package from tarball** command.
The Game Toon material creator remains at
`Miku > Game Toon > Materials > Create Material`.

## Compatibility behavior

- Blender 5.x is admitted; Blender versions other than certified 5.2.0 emit
  `MIKU_BLENDER_VERSION_UNVALIDATED`. Blender 4.x/6.x is rejected.
- Unity 6000.x (Unity 6) and URP/Shader Graph 17.x are admitted; versions
  other than the certified 6000.5.4f1 / 17.5.4 tuple emit
  `MIKU_*_VERSION_UNVALIDATED`. Wrong-major versions are rejected before
  generated assets are written.
- Shader Graph selects an explicit 17.x adapter (17.0-17.6) and clamps unknown
  higher minors to the highest-known adapter while the generated-asset
  identity namespace stays stable.
- The Unity package manifest floor rises to `unity: 6000.5` and URP 17.5.4;
  UPM will not install it below those versions.

## Schema and API impact

The Blender exporter still writes `miku-bake-request-1.2`; the bake-request
Blender range check and the extension manifest install gate relax to
major-only for coherence. Bake Result, MaterialIR, Bundle, Unity receipt,
shader properties, and public C# request/result fields do not change. The
target-profile canonical hash moves to the 2.2.11 tuple; the 2.2.9/2.2.10
canonical hash remains accepted so existing bundles keep importing.
