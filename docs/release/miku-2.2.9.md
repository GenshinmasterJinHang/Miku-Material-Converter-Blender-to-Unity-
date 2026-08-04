# Miku 2.2.9

Miku 2.2.9 removes patch-version installation blocks within strict downward
compatibility ranges. Blender 5.0.0 through 5.2.0 and Unity 6000.0.0f1 through
6000.4.5f1 with URP/Shader Graph 17.0.0 through 17.4.0 are admitted. The only
validated tuple remains Blender 5.2.0, Unity 6000.4.5f1, and URP/Shader Graph
17.4.0 on Windows; other admitted combinations are explicitly unvalidated.

## Release assets

- `miku_shader_converter-2.2.9.zip`
- `com.miku.shaderconverter-2.2.9.tgz`
- `SHA256SUMS.txt`

Install the ZIP through Blender's **Extensions > Install from Disk** and the
TGZ through Unity Package Manager's **Add package from tarball** command.
The Game Toon material creator remains at
`Miku > Game Toon > Materials > Create Material`.

## Compatibility behavior

- Blender 5.0/5.1 continues with `MIKU_BLENDER_VERSION_UNVALIDATED`; Blender
  5.2.1 or later is rejected.
- Unity and render-package versions below the certified tuple but inside the
  documented ranges continue with `MIKU_*_VERSION_UNVALIDATED` diagnostics.
- Unity 6000.4.5f2+, URP 17.4.1+, and Shader Graph 17.4.1+ are rejected before
  generated assets are written.
- Shader Graph selects an explicit 17.0-17.4 adapter and completes an in-memory
  capability/serialization preflight before bundle import starts.

## Schema and API impact

The Blender exporter writes `miku-bake-request-1.2`, recording the actual
Blender numeric version and build hash. The bundled worker still accepts frozen
request 1.0/1.1 on the certified 5.2.0 build. Bake Result, MaterialIR, Bundle,
Unity receipt, shader properties, and public C# request/result fields do not
change.
