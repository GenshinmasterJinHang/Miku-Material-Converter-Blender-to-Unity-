# Adding a Blender Node

This guide explains how to add support for a Blender 5.2 EEVEE shader node in
the Miku to Unity Shader Graph pipeline.

## Prerequisites

- Blender 5.2.0 LTS (`fbe6228777e`)
- Unity 6000.4.5f1 with URP and Shader Graph 17.4.0
- Python 3.13 and .NET 8
- Familiarity with `docs/architecture/overview.md`

## Research and licensing

Document every socket, default, range, enum, coordinate-space rule, and shader
stage. Use the Blender 5.2 manual, public mathematical references, and black-box
tests against the Blender binary. Do not copy Blender GPL source into Miku.
Record public reference and license information in the relevant provenance file.

## Choose the translation layer

1. Use an existing Shader Graph node when the semantics are exact.
2. Add a clean-room HLSL Custom Function under the package `Runtime/` tree when
   no native node is equivalent.
3. Add a versioned Sub Graph wrapper when defaults, display names, or keywords
   need to be preserved.
4. Lower BSDF closures to the target-neutral Surface and Coverage IR rather
   than exposing Unity internals in the interchange schema.

## Update canonical sources

- Add the `ShaderNode*` to Miku operation mapping in `miku_blender/__init__.py`.
- Update semantic extraction in `miku/` or the appropriate Blender extension.
- Register surface behavior in
  `unity/Packages/com.miku.shaderconverter/Editor/MikuSurfaceModelBackends.cs`.
- Keep Shader Graph 17.4 serialization details in
  `MikuShaderGraph17RuntimeBackend.cs`.
- Add a provenance entry and a diagnostic when translation is approximate,
  baked, requires setup, or unsupported.

## Tests

Add the lowest-layer test that proves the behavior and retain deterministic
fixtures only. Use Python unit tests, Blender headless smoke tests under
`tests/blender/`, and package EditMode tests under
`unity/Packages/com.miku.shaderconverter/Tests/Editor/`. Do not add a
persistent Unity project or large binary test assets.

```powershell
py -3.13 -m unittest discover -s tests -p "test_*.py"
py -3.13 tools/miku_package_identity.py --check
C:\SteamLibrary\steamapps\common\Blender\blender.exe --background --python tests/blender/<smoke>.py
```

Update the node support matrix, diagnostics, provenance, changelog, and any
feature documentation that is affected.
