# Roadmap

## 0.11 release candidate — current

- Blender 5.2 EEVEE-only generic material scope.
- Miku 3.0 and bundle 1.2.
- Editable Unity 6000.4.5f1 / URP 17.4 / Shader Graph 17.4 backend.
- Explicit exact/equivalent/approximate/baked diagnostics.
- Automatic material-only versus bound-GLB export.
- 18-file / 253-material review corpus.
- Human visual approval before public GitHub release.

## Next

- Resolve or isolate the Blender 5.2 background shutdown crash observed after
  successful export in six corpus files.
- Add image-based comparison metrics after a human-approved reference set
  exists.
- Expand exact/equivalent Shader Graph implementations for procedural textures
  currently relying on baked parity.
- Improve standalone Toon, Sheen, SSS, Translucent, and Metallic approximations
  without weakening diagnostics.
- Validate additional exact Blender 5.2 patch builds and operating systems.
- Add separate Shader Graph adapters only after validating new Unity/URP/Shader
  Graph version tuples.

## Later

- Volume materials: Principled Volume, Volume Absorption, Volume Scatter, and
  Volume Coefficients.
- Hair-material semantics and geometry requirements.
- Runtime implementations for animated/live 4D procedural textures.
- Reviewed schema migration tooling before the next public wire-version change.

Dates are intentionally omitted. Roadmap items are direction, not compatibility
claims.
