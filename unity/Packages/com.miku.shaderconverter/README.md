# Miku Shader Converter 1.0.1

Package ID: `com.miku.shaderconverter`
Root namespace: `Miku.ShaderConverter`

The validated target is Unity 6000.4.5f1 with URP and Shader Graph 17.4.0 on
Windows. Copy a complete Blender-exported directory containing a
`.mikubundle` entry under `Assets/`; the importer validates the sealed
artifacts and writes Miku 1.0 outputs transactionally.

## Generic Toon

`generic_toon` uses a package-owned ShaderLab/HLSL backend. It never creates a
new Wrapper Shader Graph or Sub Graph. Missing semantic information resolves
to `Miku/GenericToon/GenericOpaque`.

The public semantic family is:

- `Miku/GenericToon/Face`
- `Miku/GenericToon/BodySkin`
- `Miku/GenericToon/Hair`
- `Miku/GenericToon/Eye`
- `Miku/GenericToon/Mouth`
- `Miku/GenericToon/Cloth`
- `Miku/GenericToon/MetalAccessory`
- `Miku/GenericToon/GenericOpaque`

All eight shaders share one property/CBUFFER layout and include
`UniversalForwardOnly`, `ShadowCaster`, `DepthOnly`, `DepthNormalsOnly`,
`MotionVectors`, `MikuToonOutline`, and `MikuToonCharacterMask` passes.
Geometry outlines are drawn only by the original Material's embedded outline
pass. UV7/TEXCOORD7 supplies smooth normals and falls back to the Object Space
normal when absent or zero.

Use **Miku > Generic Toon > Material Builder** for material-driven
authoring. Each input row is a Material asset. The builder always creates a
Miku-owned base Material, a user-owned derived Material, and a
`MikuToonMaterialRecipe`. Rebuild performs a three-way merge: only values that
still equal the previous synchronized values follow the source.

Screen Rim is opt-in. Use **Miku > Generic Toon > Rendering > Screen Rim
Installer**, select one `UniversalRendererData`, preview the change, then
apply. Material and Shader GUIs provide a shortcut that only opens and prefills
the installer. It deduplicates the feature, rolls back a failed edit, and does
not modify every URP asset.

Open the dedicated tools under **Miku > Generic Toon > Mesh**. Smooth normals
and vertex colors are separate explicit Mesh tools, with a combined entry when
both are needed. They clone the selected source Mesh to a chosen asset path and
never edit the source Mesh, its importer, or any Renderer reference. Vertex colors use
`Miku_ToonMask_v1`: R SSS, G outline width, B screen rim, A face correction;
the neutral value is `(255,255,255,0)`.

## Ownership and migration

For Generic Toon, generated base Materials and recipes are Miku-owned; derived
Materials are user-owned. Existing Wrapper/Sub Graph assets are never deleted
or overwritten by the static backend.

Legacy `.migrbundle` files are read-only compatibility input and normalize to
Miku 1.0 in memory before generation. Use the selected-asset Dry Run and Apply
commands under **Miku > Migration** to migrate persistent Materials
and AnimationClip curves. The migration never scans scene hierarchies and
cannot migrate runtime `MaterialPropertyBlock` values.

Miku does not provide a Model Root picker, Renderer scan, Mesh checkbox list,
material-slot tree, automatic Renderer replacement, or character Prefab
generation.
