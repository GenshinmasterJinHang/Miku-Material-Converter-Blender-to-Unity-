# Blender-Compatible Shader Node Library

Miku provides a Blender 5.2 EEVEE-compatible shader node library for Unity 6
URP Shader Graph 17.4. The library translates Blender shader nodes into
editable Shader Graph assets using a combination of Unity native nodes,
Custom Function HLSL, and Sub Graph wrappers.

## Architecture

```
Blender 5.2 EEVEE Node Graph
        |
        v
Active Output Graph Slicer (eevee_semantics.py)
        |
        v
Blender Node Extractor (exporter_core.py)
        |
        v
Miku Semantic IR (miku-3.0)
        |
        v
Value Node Compiler (B2UMgirCompiler.cs)
        |
        v
Blender-Compatible Algorithm Library (HLSL *.hlsl)
        |
        v
Closure Lowering + Render State Translation
        |
        v
Unity 6 URP Shader Graph Backend (B2UShaderGraph17UrpBackend.cs)
        |
        v
Editable .shadergraph + .generated.shadersubgraph
```

## Node Implementation Strategy

Nodes are implemented using one of four strategies, in priority order:

| Priority | Strategy | When Used |
|---|---|---|
| A | Unity native Shader Graph node | Semantics match exactly (Add, Multiply, Dot, etc.) |
| B | Custom Function HLSL | Algorithm differs or no Unity equivalent exists |
| C | Sub Graph wrapper | Public Blender-compatible node with specific display names, defaults, and modes |
| D | Closure Lowering | Principled BSDF, Mix Shader, Transparent BSDF → Surface + Coverage IR |

## Math Operations (40+ supported)

All Blender 5.2 ShaderNodeMath operations are supported:

| Category | Operations | Implementation |
|---|---|---|
| Basic Arithmetic | Add, Subtract, Multiply, Divide, Power, Multiply-Add | Native nodes |
| Modulo | Modulo (GLSL semantics) | Native (Equivalent — see GLSL vs HLSL note) |
| Comparison | Minimum, Maximum, Less Than, Less Equal, Greater Than, Greater Equal, Compare | Native nodes |
| Smooth Min/Max | Smooth Minimum, Smooth Maximum | Custom Function HLSL |
| Rounding | Absolute, Floor, Ceil, Round, Truncate, Fraction, Sign | Native nodes |
| Trigonometric | Sine, Cosine, Tangent, Arcsine, Arccosine, Arctangent, Arctan2 | Native nodes |
| Hyperbolic | Sinh, Cosh, Tanh | Custom Function HLSL |
| Exponential | Exponent, Logarithm, Square Root, Inverse Square Root | Native nodes |
| Clamping | Clamp, Saturate, One Minus, Step, Smoothstep | Native nodes |
| Interpolation | Lerp, Remap | Native nodes |
| Special | Ping-Pong, Wrap, Snap | Custom Function HLSL |

## Vector Math Operations (26 supported)

All Blender 5.2 ShaderNodeVectorMath operations are supported:

| Category | Operations | Implementation |
|---|---|---|
| Basic | Add, Subtract, Multiply, Divide, Scale | Native nodes |
| Products | Dot Product, Cross Product | Native nodes |
| Properties | Normalize, Length, Distance | Native nodes |
| Manipulation | Reflect, Refract, Project | Native nodes |
| Component-wise | Absolute, Minimum, Maximum, Floor, Ceil, Fraction, Modulo | Native nodes |
| Component-wise Custom | Wrap, Snap | Custom Function HLSL |
| Component-wise Trig | Sine, Cosine, Tangent | Native nodes |

## Procedural Textures

| Node | Default Route | Implementation |
|---|---|---|
| Noise Texture | Baked parity + editable fBM approximation | MikuBlenderNoise.hlsl |
| Voronoi Texture | Baked parity + editable HLSL | MikuBlenderVoronoi.hlsl |
| Wave Texture | Native graph + HLSL | MikuBlenderWave.hlsl |
| Checker Texture | Native graph | Exact via floor/parity formula |
| Gradient Texture | Native graph | All 7 modes supported |
| Brick Texture | Channel bake / approximation | Legacy B2UHelpers.hlsl |
| Magic Texture | Channel bake / approximation | Legacy B2UHelpers.hlsl |
| White Noise Texture | Channel bake / approximation | Legacy B2UHelpers.hlsl |

## Color Operations

| Node | Implementation |
|---|---|
| Color Ramp | MikuBlenderColorRamp.hlsl — multi-stop with all interpolation modes |
| Mix Color | Native LerpNode (Approximate for non-RGB blend modes) |
| Brightness/Contrast | Expanded native graph |
| Hue/Saturation/Value | Custom Function HLSL |
| Invert Color | Expanded native graph |
| Gamma | Native node |
| RGB to BW | Custom Function HLSL |

## Coordinate and Mapping

| Node | Implementation |
|---|---|
| Mapping | TilingAndOffsetNode (Approximate for non-uniform scale) |
| Vector Rotate | Native RotateNode |
| Vector Transform | Custom Function HLSL |
| Normal Map | Native NormalUnpackNode |
| Bump | Native NormalFromHeightNode (Approximate) |

## GLSL / HLSL Semantic Differences

Several operations require careful handling:

1. **Modulo**: GLSL `mod(x,y)` = `x - y * floor(x/y)`. HLSL `fmod(x,y)` = `x - y * trunc(x/y)`. Results differ for negative `x`. Miku uses the GLSL-compatible formula.

2. **Fraction**: GLSL `fract(x)` = `x - floor(x)`, always returning [0, 1). Miku uses explicit `x - floor(x)`.

3. **Smooth Minimum**: Blender uses a quadratic smooth minimum formula with a distance parameter `k`. Miku implements this in Custom Function HLSL.

## License and Provenance

All HLSL implementations in this library are **clean-room** — derived from public
math references (Perlin 1985, Worley 1996, Inigo Quilez articles) and validated
against Blender 5.2.0 LTS as a black-box reference. No Blender source code was
copied. See `docs/provenance/` for per-function provenance records.

The project is MIT licensed. GPL-derived ports from Blender source require
explicit maintainer approval. See `docs/audits/blender-source-port-license-audit.md`.
