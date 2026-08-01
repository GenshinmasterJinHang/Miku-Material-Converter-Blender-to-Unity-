#ifndef MIKU_BLENDER_WHITE_NOISE_INCLUDED
#define MIKU_BLENDER_WHITE_NOISE_INCLUDED

// SPDX-FileCopyrightText: 2026 Miku contributors
// SPDX-License-Identifier: MIT
// Miku 1.0 semantic-region clean-room implementation, commit
// fbe6228777e7d9afefcd61a413844e790ae75db7.

#include "Packages/com.miku.shaderconverter/Runtime/Math/MikuBlenderMath.hlsl"

float Miku_WhiteNoiseHash(float x)
{
    return Miku_Fract(sin(x * 12.9898 + 78.233) * 43758.5453123);
}

float Miku_WhiteNoiseHash2(float2 p)
{
    return Miku_Fract(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
}

float Miku_WhiteNoiseHash3(float3 p)
{
    return Miku_Fract(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453123);
}

float Miku_WhiteNoiseHash4(float4 p)
{
    return Miku_Fract(sin(dot(p, float4(127.1, 311.7, 74.7, 34.1))) * 43758.5453123);
}

void Miku_WhiteNoiseTexture1D_Full_float(float w, out float value, out float4 color)
{
    value = Miku_WhiteNoiseHash(w);
    color = float4(value, value, value, 1.0);
}

void Miku_WhiteNoiseTexture2D_Full_float(float2 position, out float value, out float4 color)
{
    value = Miku_WhiteNoiseHash2(position);
    color = float4(value, value, value, 1.0);
}

void Miku_WhiteNoiseTexture3D_Full_float(float3 position, out float value, out float4 color)
{
    value = Miku_WhiteNoiseHash3(position);
    color = float4(value, value, value, 1.0);
}

void Miku_WhiteNoiseTexture4D_Full_float(float4 position, out float value, out float4 color)
{
    value = Miku_WhiteNoiseHash4(position);
    color = float4(value, value, value, 1.0);
}

#endif // MIKU_BLENDER_WHITE_NOISE_INCLUDED
