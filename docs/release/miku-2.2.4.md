# Miku Unity package 2.2.4 release validation

Miku 2.2.4 targets Blender 5.2.0, Unity 6000.4.5f1
(`cc83ebd631f8`), URP 17.4.0, Shader Graph 17.4.0, Windows, and D3D11.

## Scope

This release repairs four Endfield material-fidelity gaps while retaining the
2.2.3 Main Light contract:

- Hair can treat a red-only specular LUT as a neutral scalar and blends a
  camera-cylinder strand direction with projected mesh tangents using the
  renderer object's head Up direction as its stable fallback.
- Body, Skin, Face, and Hair can add a bounded view-edge rim aligned to the
  directional Main Light. The existing Game Toon screen-space rim remains in
  place.
- Body metal keeps GGX and URP reflection-probe IBL while exposing independent
  direct and environment boosts plus a bounded highlight band.
- Skin and Face share brightness/whitening grading toward a warm-pale target;
  face emotion and blush remain authored overlays applied after grading.

All new properties have compatibility defaults. MaterialIR remains 2.0;
texture roles, shader names, material slot order, and existing public property
semantics are unchanged.

## Validation contract

The canonical archive is `dist/com.miku.shaderconverter-2.2.4.tgz`. Its final
SHA-256 is recorded in `miku-2.2.4-sha256.txt` after two byte-identical builds.
The Unity validation project installs that archive without patching its
PackageCache copy.

Private Endfield character meshes and textures are validation-only inputs and
are not distributed. MyZmdShaders was used only as observable behavioral
evidence because its inspected revision declares no license; no implementation
source or serialized asset was copied.

The isolated validation assets are `Assets/endfield/Materials/杰哥_2.2.4`
and `Assets/endfield/Validation/2.2.4/Endfield_2.2.4.unity`. Existing 2.2.2 and
2.2.3 material groups and `Assets/Scenes/1.unity` remain unchanged.

Exact commands, results, screenshots, numeric render checks, archive hash, and
known limitations are recorded in
`docs/plans/endfield-2.2.4-hair-rim-metal-skin-fidelity.md`.
