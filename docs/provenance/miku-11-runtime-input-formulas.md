# Miku 1.1 runtime-input formula provenance

Miku's Fresnel and Layer Weight implementations are original, independently
written code that reproduces documented Blender node behavior through native
Shader Graph math nodes. No Blender GPL source code was copied into the MIT
exporter, core, or Unity package.

The behavior was locked against Blender commit `fbe6228777e7`:

- [Blender Fresnel material shader](https://github.com/blender/blender/blob/fbe6228777e7d9afefcd61a413844e790ae75db7/source/blender/gpu/shaders/material/gpu_shader_material_fresnel.glsl)
- [Blender Layer Weight material shader](https://github.com/blender/blender/blob/fbe6228777e7d9afefcd61a413844e790ae75db7/source/blender/gpu/shaders/material/gpu_shader_material_layer_weight.glsl)

For dielectric Fresnel, Miku evaluates the absolute normalized view/normal
cosine, applies the front-face IOR direction, computes the unpolarized
reflectance, and returns one for total internal reflection. Layer Weight derives
its medium ratio from `max(1 - Blend, 1e-5)`. Its separate Facing output uses
`1 - pow(abs(dot(N,V)), exponent)` when Blend is not exactly `0.5`; the exponent
is `2 * Blend` below `0.5` and `0.5 / (1 - Blend)` above it after clamping Blend
to `[0, 0.99999]`. At exactly `0.5`, the unpowered cosine is retained.

`miku/runtime_math.py` is a test oracle for these equations. Production Unity
generation expands them into stock Shader Graph 17.4 nodes. Unit and EditMode
tests require numerical agreement within `1e-4` and verify that no Shader Graph
Fresnel shortcut or Custom Function node is emitted.
