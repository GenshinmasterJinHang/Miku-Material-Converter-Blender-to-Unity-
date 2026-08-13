# Endfield JieGe eye-highlight source evidence

This record documents the source-material facts used to correct the private
validation scene. It contains no third-party model or texture bytes.

## Source identity

- PMX SHA-256:
  `9FB0B6C4FBB6E7E30FF7F379A0A46EA96C80F1092189688FF0987025F2D70CA6`
- PMX version: 2.0
- Source material index: 2 (zero-based)
- Source material name: `目HL`
- Source texture index: 1
- Texture-table entry 1: `textures/T_actor_aglina_iris_01_D.png`
- Sphere texture: none; Toon texture: none; edge flag: disabled
- Geometry: 48 indices, 16 triangles, 20 unique vertices

The PMX texture table does not contain `T_actor_common_face_01_hl_M.png` or
`T_actor_common_matcap_08_D.png`. The former is a separate Face shader local
highlight mask. The latter is a shared runtime MatCap for the dynamic cornea
layer.

## Texture and UV consequence

The PMX iris texture is RGB. The Unity validation copy has identical RGB but an
additional alpha channel that is nearly zero in the EyeHL UV island. Therefore
the imported overlay must preserve the explicit source texture relationship and
use opaque coverage rather than interpreting the added alpha channel.

The corrected validation state is:

- shader: `MIKU/Endfield/Overlay`
- `_BaseMap`: `T_actor_aglina_iris_01_D`
- `_AlphaSource`: `4` (Opaque)
- `_AlphaClip`: `0`
- `_Cull`: `0` (Off)
- `_OverlayUseTintOnly`: `0`
- `_LightingMode`: `0` (Legacy Unlit)
- `_BaseColorTint`: white

Face slot 0 retains `T_actor_common_face_01_hl_M -> _HighlightMap`. This is a
part-scoped mapping correction; it does not change the public `BaseMap` or
`HighlightMap` texture-role names and does not change the Miku interchange
schema.

## Validation boundary

The source evidence proves texture identity and material state, not the final
screen-space look. Depth ordering remains `ZTest LEqual`. A configurable depth
offset must not be added unless a Direct3D 12 Frame Debugger trace proves that
the correctly bound geometry is rejected as coplanar.
