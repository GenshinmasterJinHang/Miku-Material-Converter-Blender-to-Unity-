#ifndef MIKU_BLENDER_VECTOR_MATH_INCLUDED
#define MIKU_BLENDER_VECTOR_MATH_INCLUDED

// Miku 1.0 semantic-region clean-room implementation, commit
// fbe6228777e7d9afefcd61a413844e790ae75db7; Miku owns Unity entry points.

// SPDX-FileCopyrightText: 2026 Miku contributors
// SPDX-License-Identifier: MIT
//
// Blender-compatible vector math operations for Unity Shader Graph Custom Function nodes.
// CLEAN_REIMPLEMENTATION — no Blender source code was copied.
//
// Reference: Blender 5.2.0 LTS manual (blender-v5.2-release, commit e74f0a2b)
//           Standard mathematical functions from public domain.

// ============================================================================
// Component-wise Wrap
// ============================================================================
// Blender Wrap: maps value into [min, max] range by wrapping.

void Miku_Wrap_float2(float2 value, float minVal, float maxVal, out float2 result)
{
    float range = max(abs(maxVal - minVal), 1e-8);
    result = minVal + (value - minVal) - range * floor((value - minVal) / range);
}

void Miku_Wrap_float3(float3 value, float minVal, float maxVal, out float3 result)
{
    float range = max(abs(maxVal - minVal), 1e-8);
    result = minVal + (value - minVal) - range * floor((value - minVal) / range);
}

void Miku_Wrap_float4(float4 value, float minVal, float maxVal, out float4 result)
{
    float range = max(abs(maxVal - minVal), 1e-8);
    result = minVal + (value - minVal) - range * floor((value - minVal) / range);
}

// ============================================================================
// Component-wise Snap
// ============================================================================
// Blender Snap: rounds to nearest multiple of increment.

void Miku_Snap_float2(float2 value, float increment, out float2 result)
{
    float inc = max(abs(increment), 1e-8);
    result = floor(value / inc + 0.5) * inc;
}

void Miku_Snap_float3(float3 value, float increment, out float3 result)
{
    float inc = max(abs(increment), 1e-8);
    result = floor(value / inc + 0.5) * inc;
}

void Miku_Snap_float4(float4 value, float increment, out float4 result)
{
    float inc = max(abs(increment), 1e-8);
    result = floor(value / inc + 0.5) * inc;
}

// ============================================================================
// Reflect (already handled by native ReflectNode, provided for completeness)
// ============================================================================

void Miku_Reflect_float3(float3 incident, float3 normal, out float3 result)
{
    result = incident - 2.0 * dot(normal, incident) * normal;
}

// ============================================================================
// Refract (Snell's law with total internal reflection handling)
// ============================================================================

void Miku_Refract_float3(float3 incident, float3 normal, float ior, out float3 result)
{
    float eta = max(ior, 1e-5);
    float ndi = dot(normal, incident);
    float k = 1.0 - eta * eta * (1.0 - ndi * ndi);
    // Total internal reflection: return zero vector (Blender behavior)
    result = (k < 0.0) ? float3(0.0, 0.0, 0.0) : (eta * incident - (eta * ndi + sqrt(k)) * normal);
}

// ============================================================================
// Project (A onto B)
// ============================================================================

void Miku_Project_float3(float3 a, float3 b, out float3 result)
{
    float bDot = dot(b, b);
    result = (bDot > 1e-8) ? b * dot(a, b) / bDot : float3(0.0, 0.0, 0.0);
}

#endif // MIKU_BLENDER_VECTOR_MATH_INCLUDED
