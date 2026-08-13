# Unpublished Miku 2.4.0 candidate record

Miku 2.4.0 was never published as a GitHub Release. Its completed work was
carried forward into Miku 3.0.0; users should install v3.0.0 rather than search
for a v2.4.0 package.

> **3.0 supersession (2026-08-13):** the local Furina creator, DX12 guard
> helper, and UV1 diagnostic helper described below were removed from the later
> distributable package. DX12 acceptance remains automated and fixtures remain
> external to the package.

Miku 2.4.0 changes the default Genshin visual implementation to the
independently reconstructed tutorial contract. Body/Hair gain real-tangent
normal mapping, tutorial AO/ramp/specular/metal behavior, five outline colors,
shared alpha coverage, and separately scheduled UV1 backface plus UV7 outline
passes. Face keeps Miku's head-bone SDF basis.

Windows GPU acceptance now requires Direct3D 12. `D3D11` remains only in
historical evidence and fixtures. Ordinary package import is not blocked on
other graphics APIs, but those APIs cannot produce a new compatibility claim.

The release adds public Genshin alpha/backface material state, the Geometry
Renderer Feature, vertex-color channel mapping Mesh clone overloads, Genshin
texture audit, DX12 guard, and a local-only Furina scene creator. No reference
model, texture, scene, material, metadata, screenshot, or Shader source is
distributed.

MaterialIR, Bundle, Manifest, and Receipt schemas are unchanged. The exact
2.3.0 profile hash remains a compatibility input; importing an old Genshin
bundle reports `MIKU_GENSHIN_2_3_VISUAL_MIGRATION`.

The exact Blender ZIP and Unity TGZ hashes will be recorded only after the
canonical source builds are complete and byte-identical.
