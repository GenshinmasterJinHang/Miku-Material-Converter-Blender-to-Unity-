#ifndef MIKU_BLENDER_GABOR_INCLUDED
#define MIKU_BLENDER_GABOR_INCLUDED

// SPDX-FileCopyrightText: 2026 Miku contributors
// SPDX-License-Identifier: MIT
// Miku 1.0 semantic-region clean-room implementation, commit
// fbe6228777e7d9afefcd61a413844e790ae75db7.

float Miku_GaborKernel(float2 p, float frequency, float anisotropy, float orientation)
{
    float2 direction = float2(cos(orientation), sin(orientation));
    float2 orthogonal = float2(-direction.y, direction.x);
    float along = dot(p, direction);
    float across = dot(p, orthogonal) * max(anisotropy, 1e-4);
    float envelope = exp(-0.5 * (along * along + across * across));
    return envelope * cos(6.28318530718 * frequency * along);
}

void Miku_GaborTexture2D_Full_float(float2 position, float scale, float frequency,
    float anisotropy, float orientation, out float value, out float4 color)
{
    value = Miku_GaborKernel(position * scale, frequency, anisotropy, orientation);
    color = float4(value.xxx * 0.5 + 0.5, 1.0);
}

void Miku_GaborTexture3D_Full_float(float3 position, float scale, float frequency,
    float anisotropy, float3 orientation, out float value, out float4 color)
{
    float angle = atan2(orientation.y, orientation.x);
    value = Miku_GaborKernel(position.xy * scale, frequency, anisotropy, angle);
    color = float4(value.xxx * 0.5 + 0.5, 1.0);
}

void Miku_GaborTexture4D_Full_float(float4 position, float scale, float frequency,
    float anisotropy, float3 orientation, out float value, out float4 color)
{
    float angle = atan2(orientation.y, orientation.x);
    value = Miku_GaborKernel((position.xy + position.zw) * scale, frequency, anisotropy, angle);
    color = float4(value.xxx * 0.5 + 0.5, 1.0);
}

#endif // MIKU_BLENDER_GABOR_INCLUDED
