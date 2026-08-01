#ifndef MIKU_BLENDER_VORONOI_INCLUDED
#define MIKU_BLENDER_VORONOI_INCLUDED

// Miku 1.0 semantic-region clean-room implementation, commit
// fbe6228777e7d9afefcd61a413844e790ae75db7; Miku owns Unity entry points.

// SPDX-FileCopyrightText: 2026 Miku contributors
// SPDX-License-Identifier: MIT
//
// Blender-compatible Voronoi / Worley noise texture.
// CLEAN_REIMPLEMENTATION — no Blender source code was copied.
//
// References:
//   - Worley, S. "A Cellular Texture Basis Function" (1996)
//   - Inigo Quilez — voronoi articles (public domain)
//   - Stefan Gustavson — cellular noise implementations (public domain)
//   - Blender 5.2 Manual — Voronoi Texture node specification only

#include "Packages/com.miku.shaderconverter/Runtime/Math/MikuBlenderMath.hlsl"
#include "Packages/com.miku.shaderconverter/Runtime/Textures/MikuBlenderNoise.hlsl"

// ============================================================================
// Hash Functions for Cell Generation
// ============================================================================

float2 Miku_VoronoiHash2(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return -1.0 + 2.0 * Miku_Fract(sin(p) * 43758.5453123);
}

float3 Miku_VoronoiHash3(float3 p)
{
    p = float3(dot(p, float3(127.1, 311.7, 74.7)),
               dot(p, float3(269.5, 183.3, 246.1)),
               dot(p, float3(113.5, 271.9, 124.6)));
    return -1.0 + 2.0 * Miku_Fract(sin(p) * 43758.5453123);
}

float4 Miku_VoronoiHash4(float4 p)
{
    p = float4(dot(p, float4(127.1, 311.7, 74.7, 34.1)),
               dot(p, float4(269.5, 183.3, 246.1, 462.3)),
               dot(p, float4(113.5, 271.9, 124.6, 83.5)),
               dot(p, float4(317.2, 172.8, 412.9, 256.7)));
    return -1.0 + 2.0 * Miku_Fract(sin(p) * 43758.5453123);
}

// ============================================================================
// Distance Metrics
// ============================================================================
// 0 = Euclidean, 1 = Manhattan, 2 = Chebyshev, 3 = Minkowski

float Miku_VoronoiDistance(float2 a, float2 b, int metric, float exponent)
{
    float2 d = abs(a - b);
    switch (metric)
    {
        case 1: return d.x + d.y;                          // Manhattan
        case 2: return max(d.x, d.y);                       // Chebyshev
        case 3: return pow(pow(d.x, exponent) + pow(d.y, exponent), 1.0 / max(exponent, 1e-5)); // Minkowski
        default: return length(a - b);                      // Euclidean (default)
    }
}

float Miku_VoronoiDistance3D(float3 a, float3 b, int metric, float exponent)
{
    float3 d = abs(a - b);
    switch (metric)
    {
        case 1: return d.x + d.y + d.z;
        case 2: return max(d.x, max(d.y, d.z));
        case 3: return pow(pow(d.x, exponent) + pow(d.y, exponent) + pow(d.z, exponent), 1.0 / max(exponent, 1e-5));
        default: return length(a - b);
    }
}

float Miku_VoronoiDistance4D(float4 a, float4 b, int metric, float exponent)
{
    float4 d = abs(a - b);
    switch (metric)
    {
        case 1: return d.x + d.y + d.z + d.w;
        case 2: return max(d.x, max(d.y, max(d.z, d.w)));
        case 3: return pow(pow(d.x, exponent) + pow(d.y, exponent) + pow(d.z, exponent) + pow(d.w, exponent), 1.0 / max(exponent, 1e-5));
        default: return length(a - b);
    }
}

// ============================================================================
// 2D Voronoi
// ============================================================================

void Miku_Voronoi2D(float2 p, float scale, float randomness, int feature,
                     int metric, float exponent, float smoothness,
                     out float distance, out float3 color, out float2 position, out float w, out float radius)
{
    float2 cell = floor(p * scale);
    float2 f = Miku_Fract(p * scale);

    float distF1 = 1e10;
    float distF2 = 1e10;
    float2 posF1 = float2(0.0, 0.0);
    float2 posF2 = float2(0.0, 0.0);
    float nSphereRadius = 0.0;

    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 neighbor = float2(x, y);
            float2 cellPos = neighbor + Miku_VoronoiHash2(cell + neighbor) * randomness;
            float2 diff = cellPos - f;
            float d = Miku_VoronoiDistance(cellPos, f, metric, exponent);

            if (d < distF1)
            {
                distF2 = distF1;
                posF2 = posF1;
                distF1 = d;
                posF1 = cell + neighbor;
            }
            else if (d < distF2)
            {
                distF2 = d;
                posF2 = cell + neighbor;
            }
        }
    }

    // N-Sphere Radius: distance to farthest corner in the search region
    nSphereRadius = max(abs(f.x - 0.5), abs(f.y - 0.5));

    // Feature selection
    switch (feature)
    {
        case 0: // F1
            distance = distF1;
            break;
        case 1: // F2
            distance = distF2;
            break;
        case 2: // Smooth F1
            {
                float h = max(smoothness, 1e-5);
                float diff = distF2 - distF1;
                float t = saturate(diff / h);
                distance = lerp(distF2, distF1, t * t * (3.0 - 2.0 * t));
            }
            break;
        case 3: // Distance to Edge
            distance = distF2 - distF1;
            break;
        case 4: // N-Sphere Radius
            distance = nSphereRadius;
            break;
        default:
            distance = distF1;
            break;
    }

    color = float3(Miku_NoiseHash1(posF1.x + posF1.y * 157.0),
                   Miku_NoiseHash1(posF1.x + 13.0 + posF1.y * 157.0),
                   Miku_NoiseHash1(posF1.x + 37.0 + posF1.y * 157.0));
    position = posF1;
    w = distF2 - distF1;
    radius = nSphereRadius;
}

// ============================================================================
// 3D Voronoi
// ============================================================================

void Miku_Voronoi3D(float3 p, float scale, float randomness, int feature,
                     int metric, float exponent, float smoothness,
                     out float distance, out float3 color, out float3 position, out float w, out float radius)
{
    float3 cell = floor(p * scale);
    float3 f = Miku_Fract(p * scale);

    float distF1 = 1e10;
    float distF2 = 1e10;
    float3 posF1 = float3(0.0, 0.0, 0.0);
    float nSphereRadius = 0.0;

    for (int z = -1; z <= 1; z++)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                float3 neighbor = float3(x, y, z);
                float3 cellPos = neighbor + Miku_VoronoiHash3(cell + neighbor) * randomness;
                float d = Miku_VoronoiDistance3D(cellPos, f, metric, exponent);

                if (d < distF1)
                {
                    distF2 = distF1;
                    distF1 = d;
                    posF1 = cell + neighbor;
                }
                else if (d < distF2)
                {
                    distF2 = d;
                }
            }
        }
    }

    nSphereRadius = max(max(abs(f.x - 0.5), abs(f.y - 0.5)), abs(f.z - 0.5));

    switch (feature)
    {
        case 0: distance = distF1; break;
        case 1: distance = distF2; break;
        case 2:
            {
                float h = max(smoothness, 1e-5);
                float diff = distF2 - distF1;
                float t = saturate(diff / h);
                distance = lerp(distF2, distF1, t * t * (3.0 - 2.0 * t));
            }
            break;
        case 3: distance = distF2 - distF1; break;
        case 4: distance = nSphereRadius; break;
        default: distance = distF1; break;
    }

    color = float3(Miku_NoiseHash1(posF1.x + posF1.y * 157.0 + posF1.z * 113.0),
                   Miku_NoiseHash1(posF1.x + 13.0 + posF1.y * 157.0 + posF1.z * 113.0),
                   Miku_NoiseHash1(posF1.x + 37.0 + posF1.y * 157.0 + posF1.z * 113.0));
    position = posF1;
    w = distF2 - distF1;
    radius = nSphereRadius;
}

// ============================================================================
// 4D Voronoi
// ============================================================================

void Miku_Voronoi4D(float4 p, float scale, float randomness, int feature,
                     int metric, float exponent, float smoothness,
                     out float distance, out float3 color, out float4 position, out float w, out float radius)
{
    float4 cell = floor(p * scale);
    float4 f = Miku_Fract(p * scale);

    float distF1 = 1e10;
    float distF2 = 1e10;
    float4 posF1 = float4(0.0, 0.0, 0.0, 0.0);
    float nSphereRadius = 0.0;

    // 3x3x3x3 = 81 cell search
    for (int w2 = -1; w2 <= 1; w2++)
    {
        for (int z = -1; z <= 1; z++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    float4 neighbor = float4(x, y, z, w2);
                    float4 cellPos = neighbor + Miku_VoronoiHash4(cell + neighbor) * randomness;
                    float d = Miku_VoronoiDistance4D(cellPos, f, metric, exponent);

                    if (d < distF1)
                    {
                        distF2 = distF1;
                        distF1 = d;
                        posF1 = cell + neighbor;
                    }
                    else if (d < distF2)
                    {
                        distF2 = d;
                    }
                }
            }
        }
    }

    nSphereRadius = max(max(max(abs(f.x - 0.5), abs(f.y - 0.5)), abs(f.z - 0.5)), abs(f.w - 0.5));

    switch (feature)
    {
        case 0: distance = distF1; break;
        case 1: distance = distF2; break;
        case 2:
            {
                float h = max(smoothness, 1e-5);
                float diff = distF2 - distF1;
                float t = saturate(diff / h);
                distance = lerp(distF2, distF1, t * t * (3.0 - 2.0 * t));
            }
            break;
        case 3: distance = distF2 - distF1; break;
        case 4: distance = nSphereRadius; break;
        default: distance = distF1; break;
    }

    color = float3(Miku_NoiseHash1(posF1.x + posF1.y * 157.0 + posF1.z * 113.0 + posF1.w * 271.0),
                   Miku_NoiseHash1(posF1.x + 13.0 + posF1.y * 157.0 + posF1.z * 113.0 + posF1.w * 271.0),
                   Miku_NoiseHash1(posF1.x + 37.0 + posF1.y * 157.0 + posF1.z * 113.0 + posF1.w * 271.0));
    position = posF1;
    w = distF2 - distF1;
    radius = nSphereRadius;
}

void Miku_Voronoi1D_float(float p, float scale, float randomness, int feature,
    int metric, float exponent, float smoothness,
    out float distance, out float3 color, out float position, out float w, out float radius)
{
    float2 position2;
    Miku_Voronoi2D(float2(p, 0.0), scale, randomness, feature, metric, exponent, smoothness,
        distance, color, position2, w, radius);
    position = position2.x;
}

void Miku_Voronoi2D_float(float2 p, float scale, float randomness, int feature,
    int metric, float exponent, float smoothness,
    out float distance, out float4 color, out float2 position)
{
    float3 rgb;
    float w;
    float radius;
    Miku_Voronoi2D(p, scale, randomness, feature, metric, exponent, smoothness,
        distance, rgb, position, w, radius);
    color = float4(rgb, 1.0);
}

void Miku_Voronoi3D_float(float3 p, float scale, float detail, float roughness,
    float lacunarity, float randomness, float featureHint,
    out float distance, out float4 color, out float3 position)
{
    float3 rgb;
    float w;
    float radius;
    Miku_Voronoi3D(p, scale, randomness, (int)featureHint, 0, max(lacunarity, 1.0), detail,
        distance, rgb, position, w, radius);
    color = float4(rgb, 1.0);
}

// Shader Graph compatibility entry point.  The node emits seven scalar/vector
// inputs followed by Distance/Color/Position outputs; keep this wrapper
// separate from the legacy Blender-named overloads above.
void Miku_VoronoiCompat3D_float(float3 p, float scale, float detail, float roughness,
    float lacunarity, float randomness, float featureHint,
    out float distance, out float4 color, out float3 position)
{
    float3 rgb;
    float w;
    float radius;
    Miku_Voronoi3D(p, scale, randomness, (int)featureHint, 0, max(lacunarity, 1.0), detail,
        distance, rgb, position, w, radius);
    color = float4(rgb, 1.0);
}

void Miku_VoronoiCompat3D_float(float3 p, float scale, float detail, float roughness,
    float lacunarity, float randomness,
    out float distance, out float4 color, out float3 position)
{
    Miku_VoronoiCompat3D_float(p, scale, detail, roughness, lacunarity, randomness, 0.0,
        distance, color, position);
}

// Distance-only ABI emitted when the source node does not use Color/Position.
void Miku_VoronoiCompat3D_float(float3 p, float scale, float detail, float roughness,
    float lacunarity, float randomness, out float distance)
{
    float4 color;
    float3 position;
    Miku_VoronoiCompat3D_float(p, scale, detail, roughness, lacunarity, randomness, 0.0,
        distance, color, position);
}

void Miku_Voronoi4D_float(float4 p, float scale, float randomness, int feature,
    int metric, float exponent, float smoothness,
    out float distance, out float4 color, out float4 position)
{
    float3 rgb;
    float w;
    float radius;
    Miku_Voronoi4D(p, scale, randomness, feature, metric, exponent, smoothness,
        distance, rgb, position, w, radius);
    color = float4(rgb, 1.0);
}

#endif // MIKU_BLENDER_VORONOI_INCLUDED
