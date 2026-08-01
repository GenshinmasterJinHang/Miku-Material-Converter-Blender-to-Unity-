#ifndef MIKU_BLENDER_WAVE_INCLUDED
#define MIKU_BLENDER_WAVE_INCLUDED

// Miku 1.0 semantic-region clean-room implementation, commit
// fbe6228777e7d9afefcd61a413844e790ae75db7; Miku owns Unity entry points.

// SPDX-FileCopyrightText: 2026 Miku contributors
// SPDX-License-Identifier: MIT
//
// Blender-compatible Wave Texture node.
// CLEAN_REIMPLEMENTATION — no Blender source code was copied.
//
// References:
//   - Standard trigonometric wave synthesis (public domain)
//   - Blender 5.2 Manual — Wave Texture node specification only

#include "Packages/com.miku.shaderconverter/Runtime/Math/MikuBlenderMath.hlsl"
#include "Packages/com.miku.shaderconverter/Runtime/Textures/MikuBlenderNoise.hlsl"

// ============================================================================
// Wave Profiles
// ============================================================================
// 0 = Sine, 1 = Saw (sawtooth), 2 = Triangle

float Miku_WaveProfile(float t, int profile)
{
    switch (profile)
    {
        case 1: // Sawtooth
            return Miku_Fract(t);
        case 2: // Triangle
            return 1.0 - abs(Miku_Fract(t) * 2.0 - 1.0);
        default: // Sine
            return sin(t * 6.28318530718) * 0.5 + 0.5;
    }
}

// ============================================================================
// Wave Coordinate Computation
// ============================================================================

float Miku_WaveCoordinate(float3 p, int waveType, float3 direction, float distortion,
                           float detailScale, float detailRoughness, int detail)
{
    float coord;

    if (waveType == 0) // Bands
    {
        // Project position onto direction
        coord = dot(p, normalize(direction + 1e-5));
    }
    else // Rings
    {
        // Distance from origin in the direction plane or spherical
        coord = length(p);
    }

    // Apply distortion using noise
    if (distortion > 0.0)
    {
        coord += Miku_GradientNoise3D(p * 0.5) * distortion;
    }

    // Apply detail (additional high-frequency detail)
    if (detail > 0)
    {
        float detailNoise = Miku_fBMNoise3D(p, detailScale, detail, detailRoughness, 2.0, 0.0);
        coord += detailNoise * 0.25;
    }

    return coord;
}

// ============================================================================
// Shader Graph Entry Points
// ============================================================================

// Wave Texture 2D (UV input, bands along X direction)
void Miku_WaveTexture2D_Bands_float(float2 uv, float scale, float distortion,
    float detailScale, float detailRoughness, float detail, float phase,
    out float factor, out float3 color)
{
    float3 p = float3(uv * scale, 0.0);
    float3 dir = float3(1.0, 0.0, 0.0);
    float coord = Miku_WaveCoordinate(p, 0, dir, distortion, detailScale, detailRoughness, (int)detail);
    coord += phase;
    float wave = Miku_WaveProfile(coord, 0); // Sine profile
    factor = wave;
    color = float3(wave, wave, 1.0 - wave);
}

// Wave Texture 3D with full parameter control
void Miku_WaveTexture3D_float(float3 position, float scale, float distortion,
    float detailScale, float detailRoughness, float detail, float phase,
    int waveType, int waveProfile, float3 direction,
    out float factor, out float3 color)
{
    float3 p = position * scale;
    float3 dir = normalize(direction + float3(1e-5, 0.0, 0.0));
    float coord = Miku_WaveCoordinate(p, waveType, dir, distortion, detailScale, detailRoughness, (int)detail);
    coord += phase;
    float wave = Miku_WaveProfile(coord, waveProfile);
    factor = wave * 2.0 - 1.0; // Remap to [-1, 1] for Blender compatibility
    color = float3(wave, wave * 0.5 + 0.25, 0.5 + 0.5 * sin(wave * 6.2831853));
}

void Miku_WaveTexture2D_Full_float(float2 position, float scale, float distortion,
    float detailScale, float detailRoughness, float detail, float phase,
    out float4 color, out float factor)
{
    float3 rgb;
    Miku_WaveTexture2D_Bands_float(position, scale, distortion, detailScale,
        detailRoughness, detail, phase, factor, rgb);
    color = float4(rgb, 1.0);
}

void Miku_WaveTexture3D_Full_float(float3 position, float scale, float distortion,
    float detailScale, float detailRoughness, float detail, float phase,
    out float4 color, out float factor)
{
    float3 rgb;
    Miku_WaveTexture3D_float(position, scale, distortion, detailScale, detailRoughness,
        detail, phase, 0, 0, float3(1.0, 0.0, 0.0), factor, rgb);
    color = float4(rgb, 1.0);
}

void Miku_WaveTexture4D_Full_float(float4 position, float scale, float distortion,
    float detailScale, float detailRoughness, float detail, float phase,
    out float4 color, out float factor)
{
    float3 rgb;
    Miku_WaveTexture3D_float(position.xyz + position.www, scale, distortion, detailScale,
        detailRoughness, detail, phase, 0, 0, float3(1.0, 0.0, 0.0), factor, rgb);
    color = float4(rgb, 1.0);
}

#endif // MIKU_BLENDER_WAVE_INCLUDED
