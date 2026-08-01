#ifndef MIKU_BLENDER_NOISE_INCLUDED
#define MIKU_BLENDER_NOISE_INCLUDED

// Miku 1.0 semantic-region clean-room implementation, commit
// fbe6228777e7d9afefcd61a413844e790ae75db7; Miku owns Unity entry points.

// SPDX-FileCopyrightText: 2026 Miku contributors
// SPDX-License-Identifier: MIT
//
// Blender-compatible Perlin noise with fractal Brownian motion.
// CLEAN_REIMPLEMENTATION — no Blender source code was copied.
//
// References:
//   - Perlin, K. "An Image Synthesizer" (1985) — gradient noise concept
//   - Perlin, K. "Improving Noise" (2002) — improved permutation and gradients
//   - Inigo Quilez — value/gradient noise articles (public domain)
//   - The Book of Shaders — noise chapter (public domain)
//   - Blender 5.2 Manual — Noise Texture node specification only

#include "Packages/com.miku.shaderconverter/Runtime/Math/MikuBlenderMath.hlsl"

// ============================================================================
// Hash and Permutation Functions
// ============================================================================

// 1D hash
float Miku_NoiseHash1(float n)
{
    return Miku_Fract(sin(n) * 43758.5453123);
}

// 2D hash
float2 Miku_NoiseHash2(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return Miku_Fract(sin(p) * 43758.5453123);
}

// 3D hash
float3 Miku_NoiseHash3(float3 p)
{
    p = float3(dot(p, float3(127.1, 311.7, 74.7)),
               dot(p, float3(269.5, 183.3, 246.1)),
               dot(p, float3(113.5, 271.9, 124.6)));
    return Miku_Fract(sin(p) * 43758.5453123);
}

// 4D hash
float4 Miku_NoiseHash4(float4 p)
{
    p = float4(dot(p, float4(127.1, 311.7, 74.7, 34.1)),
               dot(p, float4(269.5, 183.3, 246.1, 462.3)),
               dot(p, float4(113.5, 271.9, 124.6, 83.5)),
               dot(p, float4(317.2, 172.8, 412.9, 256.7)));
    return Miku_Fract(sin(p) * 43758.5453123);
}

// ============================================================================
// Gradient Noise Core (Perlin-style)
// ============================================================================

float Miku_GradientNoise1D(float x)
{
    float i = floor(x);
    float f = Miku_Fract(x);
    float u = f * f * (3.0 - 2.0 * f);
    return lerp(Miku_NoiseHash1(i), Miku_NoiseHash1(i + 1.0), u);
}

float Miku_GradientNoise2D(float2 p)
{
    float2 i = floor(p);
    float2 f = Miku_Fract(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = Miku_NoiseHash1(i.x + i.y * 157.0);
    float b = Miku_NoiseHash1(i.x + 1.0 + i.y * 157.0);
    float c = Miku_NoiseHash1(i.x + (i.y + 1.0) * 157.0);
    float d = Miku_NoiseHash1(i.x + 1.0 + (i.y + 1.0) * 157.0);

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float Miku_GradientNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = Miku_Fract(p);
    float3 u = f * f * (3.0 - 2.0 * f);

    float n000 = Miku_NoiseHash1(i.x + i.y * 157.0 + i.z * 113.0);
    float n100 = Miku_NoiseHash1(i.x + 1.0 + i.y * 157.0 + i.z * 113.0);
    float n010 = Miku_NoiseHash1(i.x + (i.y + 1.0) * 157.0 + i.z * 113.0);
    float n110 = Miku_NoiseHash1(i.x + 1.0 + (i.y + 1.0) * 157.0 + i.z * 113.0);
    float n001 = Miku_NoiseHash1(i.x + i.y * 157.0 + (i.z + 1.0) * 113.0);
    float n101 = Miku_NoiseHash1(i.x + 1.0 + i.y * 157.0 + (i.z + 1.0) * 113.0);
    float n011 = Miku_NoiseHash1(i.x + (i.y + 1.0) * 157.0 + (i.z + 1.0) * 113.0);
    float n111 = Miku_NoiseHash1(i.x + 1.0 + (i.y + 1.0) * 157.0 + (i.z + 1.0) * 113.0);

    return lerp(
        lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
        lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y),
        u.z);
}

float Miku_GradientNoise4D(float4 p)
{
    float4 i = floor(p);
    float4 f = Miku_Fract(p);
    float4 u = f * f * (3.0 - 2.0 * f);

    // Sample the 16 corners of the 4D hypercube
    float n0000 = Miku_NoiseHash1(i.x + i.y * 157.0 + i.z * 113.0 + i.w * 271.0);
    float n1000 = Miku_NoiseHash1(i.x + 1.0 + i.y * 157.0 + i.z * 113.0 + i.w * 271.0);
    float n0100 = Miku_NoiseHash1(i.x + (i.y + 1.0) * 157.0 + i.z * 113.0 + i.w * 271.0);
    float n1100 = Miku_NoiseHash1(i.x + 1.0 + (i.y + 1.0) * 157.0 + i.z * 113.0 + i.w * 271.0);
    float n0010 = Miku_NoiseHash1(i.x + i.y * 157.0 + (i.z + 1.0) * 113.0 + i.w * 271.0);
    float n1010 = Miku_NoiseHash1(i.x + 1.0 + i.y * 157.0 + (i.z + 1.0) * 113.0 + i.w * 271.0);
    float n0110 = Miku_NoiseHash1(i.x + (i.y + 1.0) * 157.0 + (i.z + 1.0) * 113.0 + i.w * 271.0);
    float n1110 = Miku_NoiseHash1(i.x + 1.0 + (i.y + 1.0) * 157.0 + (i.z + 1.0) * 113.0 + i.w * 271.0);
    float n0001 = Miku_NoiseHash1(i.x + i.y * 157.0 + i.z * 113.0 + (i.w + 1.0) * 271.0);
    float n1001 = Miku_NoiseHash1(i.x + 1.0 + i.y * 157.0 + i.z * 113.0 + (i.w + 1.0) * 271.0);
    float n0101 = Miku_NoiseHash1(i.x + (i.y + 1.0) * 157.0 + i.z * 113.0 + (i.w + 1.0) * 271.0);
    float n1101 = Miku_NoiseHash1(i.x + 1.0 + (i.y + 1.0) * 157.0 + i.z * 113.0 + (i.w + 1.0) * 271.0);
    float n0011 = Miku_NoiseHash1(i.x + i.y * 157.0 + (i.z + 1.0) * 113.0 + (i.w + 1.0) * 271.0);
    float n1011 = Miku_NoiseHash1(i.x + 1.0 + i.y * 157.0 + (i.z + 1.0) * 113.0 + (i.w + 1.0) * 271.0);
    float n0111 = Miku_NoiseHash1(i.x + (i.y + 1.0) * 157.0 + (i.z + 1.0) * 113.0 + (i.w + 1.0) * 271.0);
    float n1111 = Miku_NoiseHash1(i.x + 1.0 + (i.y + 1.0) * 157.0 + (i.z + 1.0) * 113.0 + (i.w + 1.0) * 271.0);

    float nx000 = lerp(n0000, n1000, u.x);
    float nx100 = lerp(n0100, n1100, u.x);
    float nx010 = lerp(n0010, n1010, u.x);
    float nx110 = lerp(n0110, n1110, u.x);
    float nx001 = lerp(n0001, n1001, u.x);
    float nx101 = lerp(n0101, n1101, u.x);
    float nx011 = lerp(n0011, n1011, u.x);
    float nx111 = lerp(n0111, n1111, u.x);

    float nxy00 = lerp(nx000, nx100, u.y);
    float nxy10 = lerp(nx010, nx110, u.y);
    float nxy01 = lerp(nx001, nx101, u.y);
    float nxy11 = lerp(nx011, nx111, u.y);

    float nxyz0 = lerp(nxy00, nxy10, u.z);
    float nxyz1 = lerp(nxy01, nxy11, u.z);

    return lerp(nxyz0, nxyz1, u.w);
}

// ============================================================================
// Fractal Brownian Motion (fBM)
// ============================================================================
// Blender-compatible multi-octave noise.
// detail    = number of octaves (integer, clamped to [0, 15])
// roughness = persistence per octave (0 = smooth, 1 = rough)
// lacunarity = frequency multiplier between octaves (typically 2.0)
// distortion = amount of domain distortion (0 = none)

float Miku_fBMNoise1D(float x, float scale, int detail, float roughness,
                       float lacunarity, float distortion)
{
    float value = 0.0;
    float amplitude = 1.0;
    float frequency = max(scale, 1e-5);
    float maxValue = 0.0;

    detail = clamp(detail, 0, 15);

    for (int i = 0; i <= detail; i++)
    {
        float nx = x * frequency;
        if (distortion > 0.0 && i > 0)
        {
            nx += Miku_GradientNoise1D(nx) * distortion;
        }
        value += amplitude * Miku_GradientNoise1D(nx);
        maxValue += amplitude;
        amplitude *= max(roughness, 0.0);
        frequency *= max(lacunarity, 1.0);
    }

    return (maxValue > 0.0) ? value / maxValue : 0.0;
}

float Miku_fBMNoise2D(float2 p, float scale, int detail, float roughness,
                       float lacunarity, float distortion)
{
    float value = 0.0;
    float amplitude = 1.0;
    float frequency = max(scale, 1e-5);
    float maxValue = 0.0;

    detail = clamp(detail, 0, 15);

    for (int i = 0; i <= detail; i++)
    {
        float2 np = p * frequency;
        if (distortion > 0.0 && i > 0)
        {
            np.x += Miku_GradientNoise2D(np + float2(0.3, 0.7)) * distortion;
            np.y += Miku_GradientNoise2D(np + float2(11.5, 1.3)) * distortion;
        }
        value += amplitude * Miku_GradientNoise2D(np);
        maxValue += amplitude;
        amplitude *= max(roughness, 0.0);
        frequency *= max(lacunarity, 1.0);
    }

    return (maxValue > 0.0) ? value / maxValue : 0.0;
}

float Miku_fBMNoise3D(float3 p, float scale, int detail, float roughness,
                       float lacunarity, float distortion)
{
    float value = 0.0;
    float amplitude = 1.0;
    float frequency = max(scale, 1e-5);
    float maxValue = 0.0;

    detail = clamp(detail, 0, 15);

    for (int i = 0; i <= detail; i++)
    {
        float3 np = p * frequency;
        if (distortion > 0.0 && i > 0)
        {
            np.x += Miku_GradientNoise3D(np + float3(0.3, 0.7, 1.1)) * distortion;
            np.y += Miku_GradientNoise3D(np + float3(11.5, 1.3, 5.7)) * distortion;
            np.z += Miku_GradientNoise3D(np + float3(7.3, 3.1, 9.5)) * distortion;
        }
        value += amplitude * Miku_GradientNoise3D(np);
        maxValue += amplitude;
        amplitude *= max(roughness, 0.0);
        frequency *= max(lacunarity, 1.0);
    }

    return (maxValue > 0.0) ? value / maxValue : 0.0;
}

float Miku_fBMNoise4D(float4 p, float scale, int detail, float roughness,
                       float lacunarity, float distortion)
{
    float value = 0.0;
    float amplitude = 1.0;
    float frequency = max(scale, 1e-5);
    float maxValue = 0.0;

    detail = clamp(detail, 0, 15);

    for (int i = 0; i <= detail; i++)
    {
        float4 np = p * frequency;
        if (distortion > 0.0 && i > 0)
        {
            np.x += Miku_GradientNoise4D(np + float4(0.3, 0.7, 1.1, 0.5)) * distortion;
            np.y += Miku_GradientNoise4D(np + float4(11.5, 1.3, 5.7, 2.3)) * distortion;
            np.z += Miku_GradientNoise4D(np + float4(7.3, 3.1, 9.5, 4.1)) * distortion;
            np.w += Miku_GradientNoise4D(np + float4(1.7, 13.2, 3.9, 8.3)) * distortion;
        }
        value += amplitude * Miku_GradientNoise4D(np);
        maxValue += amplitude;
        amplitude *= max(roughness, 0.0);
        frequency *= max(lacunarity, 1.0);
    }

    return (maxValue > 0.0) ? value / maxValue : 0.0;
}

// ============================================================================
// Shader Graph Custom Function Entry Points
// ============================================================================

// Noise Texture 1D: Factor output
void Miku_NoiseTexture1D_Factor_float(float w, float scale, float detail,
    float roughness, float lacunarity, float distortion, out float factor)
{
    factor = Miku_fBMNoise1D(w, scale, (int)detail, roughness, lacunarity, distortion);
}

// Noise Texture 2D: Factor output
void Miku_NoiseTexture2D_Factor_float(float2 uv, float scale, float detail,
    float roughness, float lacunarity, float distortion, out float factor)
{
    factor = Miku_fBMNoise2D(uv, scale, (int)detail, roughness, lacunarity, distortion);
}

// Noise Texture 3D: Factor output
void Miku_NoiseTexture3D_Factor_float(float3 position, float scale, float detail,
    float roughness, float lacunarity, float distortion, out float factor)
{
    factor = Miku_fBMNoise3D(position, scale, (int)detail, roughness, lacunarity, distortion);
}

// Noise Texture 4D: Factor output
void Miku_NoiseTexture4D_Factor_float(float4 position, float scale, float detail,
    float roughness, float lacunarity, float distortion, out float factor)
{
    factor = Miku_fBMNoise4D(position, scale, (int)detail, roughness, lacunarity, distortion);
}

// Noise Texture 2D: Factor + Color outputs
void Miku_NoiseTexture2D_Full_float(float2 uv, float scale, float detail,
    float roughness, float lacunarity, float distortion, out float factor, out float4 color)
{
    float n = Miku_fBMNoise2D(uv, scale, (int)detail, roughness, lacunarity, distortion);
    factor = n;
    color = float4(n * 0.5 + 0.5, n * 0.5, 1.0 - n * 0.5, 1.0); // Pseudo-color mapping
}

// Noise Texture 3D: Factor + Color outputs
void Miku_NoiseTexture3D_Full_float(float3 position, float scale, float detail,
    float roughness, float lacunarity, float distortion, out float factor, out float4 color)
{
    float n = Miku_fBMNoise3D(position, scale, (int)detail, roughness, lacunarity, distortion);
    factor = n;
    color = float4(n * 0.5 + 0.5, n * 0.5, 1.0 - n * 0.5, 1.0);
}

// Noise Texture 1D: Factor + Color outputs.
void Miku_NoiseTexture1D_Full_float(float w, float scale, float detail,
    float roughness, float lacunarity, float distortion, out float factor, out float4 color)
{
    float n = Miku_fBMNoise1D(w, scale, (int)detail, roughness, lacunarity, distortion);
    factor = n;
    color = float4(n * 0.5 + 0.5, n * 0.5, 1.0 - n * 0.5, 1.0);
}

// Noise Texture 4D: Factor + Color outputs.
void Miku_NoiseTexture4D_Full_float(float4 position, float scale, float detail,
    float roughness, float lacunarity, float distortion, out float factor, out float4 color)
{
    float n = Miku_fBMNoise4D(position, scale, (int)detail, roughness, lacunarity, distortion);
    factor = n;
    color = float4(n * 0.5 + 0.5, n * 0.5, 1.0 - n * 0.5, 1.0);
}

// Compatibility overloads for compact generated nodes that expose only
// coordinates, roughness, lacunarity, and the scalar Factor output.
void Miku_NoiseTexture1D_Full_float(float w, float roughness, float lacunarity, out float factor)
{
    factor = Miku_fBMNoise1D(w, 5.0, 2, roughness, lacunarity, 0.0);
}

void Miku_NoiseTexture2D_Full_float(float2 uv, float roughness, float lacunarity, out float factor)
{
    factor = Miku_fBMNoise2D(uv, 5.0, 2, roughness, lacunarity, 0.0);
}

void Miku_NoiseTexture3D_Full_float(float3 position, float roughness, float lacunarity, out float factor)
{
    factor = Miku_fBMNoise3D(position, 5.0, 2, roughness, lacunarity, 0.0);
}

void Miku_NoiseTexture4D_Full_float(float4 position, float roughness, float lacunarity, out float factor)
{
    factor = Miku_fBMNoise4D(position, 5.0, 2, roughness, lacunarity, 0.0);
}

// Blender Mapping node, vector_type=POINT. Blender applies scale, XYZ Euler
// rotation, then translation to point coordinates.
void Miku_MappingPoint_float(
    float3 inputVector,
    float3 mappingLocation,
    float3 mappingRotation,
    float3 mappingScale,
    out float3 outputVector)
{
    float3 p = inputVector * mappingScale;

    float sx;
    float cx;
    sincos(mappingRotation.x, sx, cx);
    p = float3(p.x, cx * p.y - sx * p.z, sx * p.y + cx * p.z);

    float sy;
    float cy;
    sincos(mappingRotation.y, sy, cy);
    p = float3(cy * p.x + sy * p.z, p.y, -sy * p.x + cy * p.z);

    float sz;
    float cz;
    sincos(mappingRotation.z, sz, cz);
    p = float3(cz * p.x - sz * p.y, sz * p.x + cz * p.y, p.z);

    outputVector = p + mappingLocation;
}

#endif // MIKU_BLENDER_NOISE_INCLUDED
