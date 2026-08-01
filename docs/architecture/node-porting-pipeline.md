# Node Porting Pipeline

How Miku translates a Blender 5.2 EEVEE shader node into a Unity 6 URP
Shader Graph equivalent.

## Pipeline Stages

```
1. Node Declaration Analysis
   |
   v
2. GPU/EEVEE Algorithm Identification
   |
   v
3. License Compatibility Check
   |
   v
4. Implementation Strategy Selection
   |
   v
5. HLSL / Registry Implementation
   |
   v
6. Blender Reference Test Generation
   |
   v
7. Unity Shader Graph Validation
   |
   v
8. Documentation and Provenance Update
```

## Stage Details

### 1. Node Declaration Analysis

Read the Blender 5.2 Manual entry for the node to extract:
- Input sockets (name, type, default, min/max)
- Output sockets (name, type)
- Parameters and their valid ranges
- Any conditional visibility rules

### 2. GPU/EEVEE Algorithm Identification

For nodes with non-trivial algorithms:
- Identify the mathematical formula from the manual description
- For complex nodes, consult publicly documented algorithms
- Do NOT read Blender source code (CLEAN_REIMPLEMENTATION mode)

### 3. License Compatibility Check

- Verify the algorithm can be implemented from public math references
- Record all reference sources
- Flag any algorithms whose only known documentation is GPL source
- See `docs/audits/blender-source-port-license-audit.md`

### 4. Implementation Strategy Selection

Choose one of four strategies:

| Strategy | Criteria |
|---|---|
| **A: Native Node** | Unity Shader Graph 17.4 has an exact equivalent |
| **B: Custom Function** | Algorithm differs or no equivalent exists |
| **C: Sub Graph** | Needs specific display name, defaults, mode keywords |
| **D: Closure Lowering** | BSDF/Shader nodes → Surface + Coverage IR |

### 5. HLSL / Registry Implementation

For **Custom Function** nodes:
1. Write clean-room HLSL in the appropriate `Runtime/` subdirectory
2. Add SPDX header with MIT license and reference citations
3. Add CustomFunctionNode entry in `B2UShaderGraphNodeRegistry.cs`
4. Add backend handler in `B2UShaderGraph17UrpBackend.cs` if needed

For **Native Node** additions:
1. Add registry entry with correct Shader Graph type name
2. Map input/output socket names to Shader Graph slot names
3. Set appropriate translation quality (Exact / Equivalent / Approximate)

### 6. Blender Reference Test

1. Create a test `.blend` file with the node and known inputs
2. Render reference output using Blender 5.2.0 LTS headless
3. Save as EXR or float array with color space documentation
4. Add test to `tests/`

### 7. Unity Shader Graph Validation

1. Generate Shader Graph with the node
2. Verify compilation succeeds in Unity 6000.4.5f1
3. Compare output against Blender reference (Delta-E or functional test)
4. Add to EditMode/PlayMode tests

### 8. Documentation

1. Add entry to `docs/provenance/blender-node-ports.yml`
2. Update `docs/node-support-matrix.md`
3. Update `docs/diagnostics.md` if new codes are added
4. Update `CHANGELOG.md`

## Directory Layout for New HLSL

```
unity/Packages/com.miku.shaderconverter/Runtime/
  Math/
    MikuBlenderMath.hlsl          — Math operations (Custom Function)
  Vector/
    MikuBlenderVectorMath.hlsl    — Vector math operations
  Textures/
    MikuBlenderNoise.hlsl         — Noise Texture (fBM Perlin)
    MikuBlenderVoronoi.hlsl       — Voronoi Texture (Worley)
    MikuBlenderWave.hlsl          — Wave Texture
  Color/
    MikuBlenderColorRamp.hlsl     — Color Ramp evaluator
```

## Registry File Location

```
unity/Packages/com.miku.shaderconverter/Editor/ShaderGraph/
  B2UShaderGraphNodeRegistry.cs   — Central node registry
  B2UShaderGraph17UrpBackend.cs   — Generation backend
  B2UShaderGraph17ReflectionBridge.cs — Reflection-based node creation
```
