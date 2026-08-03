# Miku 1.0.2 release notes

Miku 1.0.2 coordinates the Python core, Blender 5.2 extension, and Unity 6 URP
package without changing Miku 1.0 document kinds or schema versions.

## Fixed Toon export

`generic_toon`, `genshin_toon`, `wuwa_toon`, and `hsr_toon` now ignore Blender
closure/value topology for material generation. The source graph stays in
Source Map, while supported static Image Textures are sealed once per physical
Blender Image and assigned through explicit roles, controlled labels, or
bounded suffix aliases. No fixed workflow creates Shader Graph or bake jobs;
`standard_pbr` remains strict.

Unity creates a Miku-owned base Material, user-owned Material Variant, and
Recipe for all four workflows. Recipes preserve Generic semantic or game part,
role-to-Texture bindings, Variant GUID/parenting, and user overrides.

## Screen-space depth rim

Genshin, WuWa, and HSR Body/Hair/Face write color/width and threshold/fade mask
attachments. One URP RenderGraph feature composites an inner rim from positive
linear-depth discontinuities before transparent rendering. Eye and transparent
WuWa Effect are excluded. Legacy `_FresnelPower` and `_FresnelClamp` remain
deserializable but are hidden and unused.

Both attachments are published with `SetGlobalTextureAfterPass`; the composite
declares their read dependencies and no raster callback calls
`SetGlobalTexture`.

Validated target: Blender 5.2.0 LTS, Unity 6000.4.5f1, URP/Shader Graph 17.4.0,
Windows D3D11. Renderer Feature installation remains explicit project setup.
