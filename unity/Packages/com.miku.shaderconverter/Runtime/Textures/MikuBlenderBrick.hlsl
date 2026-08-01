#ifndef MIKU_BLENDER_BRICK_INCLUDED
#define MIKU_BLENDER_BRICK_INCLUDED

#include "Packages/com.miku.shaderconverter/Runtime/Math/MikuBlenderMath.hlsl"

// SPDX-FileCopyrightText: 2026 Miku contributors
// SPDX-License-Identifier: MIT
// Miku 1.0 semantic-region clean-room implementation, commit
// fbe6228777e7d9afefcd61a413844e790ae75db7.

float Miku_BrickPattern(float3 position, float scale, float brickWidth, float rowHeight,
    float mortarSize, float mortarSmooth, float offset, float bias,
    out float3 color, float4 color1, float4 color2, float4 mortar)
{
    float2 uv = position.xy * max(scale, 1e-5);
    float row = floor(uv.y / max(rowHeight, 1e-5));
    float rowOffset = (Miku_Fract(row * 0.5) > 0.25) ? offset : 0.0;
    float x = Miku_Fract(uv.x / max(brickWidth, 1e-5) + rowOffset);
    float y = Miku_Fract(uv.y / max(rowHeight, 1e-5));
    float edge = min(min(x, 1.0 - x), min(y, 1.0 - y));
    float mortarMask = 1.0 - smoothstep(mortarSize, mortarSize + max(mortarSmooth, 1e-5), edge);
    float brickMask = step(0.5, Miku_Fract(row + bias));
    float4 brick = lerp(color1, color2, brickMask);
    color = lerp(brick.rgb, mortar.rgb, mortarMask);
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

void Miku_BrickTexture2D_Full_float(float2 position, float4 color1, float4 color2, float4 mortar,
    float scale, float mortarSize, float mortarSmooth, float bias, float brickWidth, float rowHeight,
    out float4 color, out float factor)
{
    float3 rgb;
    factor = Miku_BrickPattern(float3(position, 0.0), scale, brickWidth, rowHeight,
        mortarSize, mortarSmooth, 0.5, bias, rgb, color1, color2, mortar);
    color = float4(rgb, 1.0);
}

void Miku_BrickTexture3D_Full_float(float3 position, float4 color1, float4 color2, float4 mortar,
    float scale, float mortarSize, float mortarSmooth, float bias, float brickWidth, float rowHeight,
    out float4 color, out float factor)
{
    float3 rgb;
    factor = Miku_BrickPattern(position, scale, brickWidth, rowHeight,
        mortarSize, mortarSmooth, 0.5, bias, rgb, color1, color2, mortar);
    color = float4(rgb, 1.0);
}

void Miku_BrickTexture4D_Full_float(float4 position, float4 color1, float4 color2, float4 mortar,
    float scale, float mortarSize, float mortarSmooth, float bias, float brickWidth, float rowHeight,
    out float4 color, out float factor)
{
    float3 rgb;
    factor = Miku_BrickPattern(position.xyz + position.www, scale, brickWidth, rowHeight,
        mortarSize, mortarSmooth, 0.5, bias, rgb, color1, color2, mortar);
    color = float4(rgb, 1.0);
}

#endif // MIKU_BLENDER_BRICK_INCLUDED
