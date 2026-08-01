#ifndef MIKU_BLENDER_MAGIC_INCLUDED
#define MIKU_BLENDER_MAGIC_INCLUDED

// SPDX-FileCopyrightText: 2026 Miku contributors
// SPDX-License-Identifier: MIT
// Miku 1.0 semantic-region clean-room implementation, commit
// fbe6228777e7d9afefcd61a413844e790ae75db7.

#include "Packages/com.miku.shaderconverter/Runtime/Textures/MikuBlenderNoise.hlsl"

float3 Miku_MagicColor(float3 p, float scale, float distortion)
{
    float n0 = Miku_GradientNoise3D(p * scale);
    float n1 = Miku_GradientNoise3D((p + 17.13) * (scale * 1.7 + distortion));
    float n2 = Miku_GradientNoise3D((p - 7.31) * (scale * 2.3 + distortion));
    return saturate(float3(n0, n1, n2));
}

void Miku_MagicTexture2D_Full_float(float2 position, float scale, float distortion,
    out float4 color, out float factor)
{
    float3 rgb = Miku_MagicColor(float3(position, 0.0), scale, distortion);
    color = float4(rgb, 1.0);
    factor = dot(rgb, float3(0.2126, 0.7152, 0.0722));
}

void Miku_MagicTexture3D_Full_float(float3 position, float scale, float distortion,
    out float4 color, out float factor)
{
    float3 rgb = Miku_MagicColor(position, scale, distortion);
    color = float4(rgb, 1.0);
    factor = dot(rgb, float3(0.2126, 0.7152, 0.0722));
}

void Miku_MagicTexture4D_Full_float(float4 position, float scale, float distortion,
    out float4 color, out float factor)
{
    float3 rgb = Miku_MagicColor(position.xyz + position.www, scale, distortion);
    color = float4(rgb, 1.0);
    factor = dot(rgb, float3(0.2126, 0.7152, 0.0722));
}

#endif // MIKU_BLENDER_MAGIC_INCLUDED
