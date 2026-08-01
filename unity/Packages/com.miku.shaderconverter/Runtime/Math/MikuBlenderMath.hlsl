#ifndef MIKU_BLENDER_MATH_INCLUDED
#define MIKU_BLENDER_MATH_INCLUDED

// Miku 1.0 semantic-region clean-room implementation, commit
// fbe6228777e7d9afefcd61a413844e790ae75db7; Miku owns Unity entry points.

// SPDX-FileCopyrightText: 2026 Miku contributors
// SPDX-License-Identifier: MIT
//
// Blender-compatible math operations for Unity Shader Graph Custom Function nodes.
// CLEAN_REIMPLEMENTATION — no Blender source code was copied.
//
// Reference: Blender 5.2.0 LTS manual (blender-v5.2-release, commit e74f0a2b)
//           Standard mathematical functions from public domain.
//
// IMPORTANT: GLSL and HLSL differ for some edge cases:
//   - GLSL mod(x,y) returns sign(y) * result, HLSL fmod(x,y) returns sign(x) * result
//   - GLSL fract(x) returns x - floor(x), HLSL frac(x) may use trunc semantics
//   - Use the Miku_ prefixed functions for Blender-compatible behavior.

// ============================================================================
// Safe Math Helpers
// ============================================================================

float Miku_SafeDivide(float a, float b)
{
    return a / max(abs(b), 1e-8);
}

float Miku_SafeDivideZero(float a, float b)
{
    return (abs(b) < 1e-8) ? 0.0 : (a / b);
}

// ============================================================================
// GLSL-compatible Modulo
// ============================================================================
// GLSL mod(x, y) returns x - y * floor(x/y), giving a result with the sign of y.
// HLSL fmod(x, y) returns x - y * trunc(x/y), giving a result with the sign of x.
// This function implements GLSL mod() semantics.

float Miku_Mod(float x, float y)
{
    // Clamp y away from zero; preserve sign of y in the divisor.
    float yy = (abs(y) < 1e-8) ? 1e-8 : y;
    return x - yy * floor(x / yy);
}

// ============================================================================
// GLSL-compatible Fraction
// ============================================================================
// GLSL fract(x) = x - floor(x), always in [0, 1) regardless of sign.
// HLSL frac(x) may use trunc(x) on some implementations.

float Miku_Fract(float x)
{
    return x - floor(x);
}

// ============================================================================
// Trigonometric Functions
// ============================================================================

void Miku_Sine_float(float x, out float result)
{
    result = sin(x);
}

void Miku_Cosine_float(float x, out float result)
{
    result = cos(x);
}

void Miku_Tangent_float(float x, out float result)
{
    result = tan(x);
}

void Miku_Arcsine_float(float x, out float result)
{
    result = asin(clamp(x, -1.0, 1.0));
}

void Miku_Arccosine_float(float x, out float result)
{
    result = acos(clamp(x, -1.0, 1.0));
}

void Miku_Arctangent_float(float x, out float result)
{
    result = atan(x);
}

void Miku_Arctan2_float(float y, float x, out float result)
{
    result = atan2(y, x);
}

// ============================================================================
// Hyperbolic Functions
// ============================================================================

void Miku_Sinh_float(float x, out float result)
{
    result = sinh(x);
}

void Miku_Cosh_float(float x, out float result)
{
    result = cosh(x);
}

void Miku_Tanh_float(float x, out float result)
{
    result = tanh(x);
}

// ============================================================================
// Exponential and Logarithmic Functions
// ============================================================================

void Miku_Exponent_float(float x, out float result)
{
    result = exp(x);
}

void Miku_Logarithm_float(float x, out float result)
{
    // GLSL log() is natural log, same as HLSL log()
    result = (x > 0.0) ? log(x) : log(1e-8);
}

void Miku_Sqrt_float(float x, out float result)
{
    result = sqrt(max(x, 0.0));
}

void Miku_InverseSqrt_float(float x, out float result)
{
    result = rsqrt(max(x, 1e-8));
}

// ============================================================================
// Integer / Rounding Functions
// ============================================================================

void Miku_Floor_float(float x, out float result)
{
    result = floor(x);
}

void Miku_Ceil_float(float x, out float result)
{
    result = ceil(x);
}

void Miku_Round_float(float x, out float result)
{
    result = round(x);
}

void Miku_Truncate_float(float x, out float result)
{
    result = trunc(x);
}

void Miku_Fraction_float(float x, out float result)
{
    // Use GLSL-compatible fract: x - floor(x)
    result = Miku_Fract(x);
}

void Miku_Sign_float(float x, out float result)
{
    result = sign(x);
}

// ============================================================================
// Comparison / Bounds Functions
// ============================================================================

void Miku_PingPong_float(float x, float scale, out float result)
{
    float s = max(abs(scale), 1e-8);
    float t = (x - s) / (2.0 * s);
    result = abs(Miku_Fract(t) * 2.0 * s - s);
}

void Miku_Wrap_float(float x, float wrapMin, float wrapMax, out float result)
{
    float range = max(abs(wrapMax - wrapMin), 1e-8);
    result = wrapMin + Miku_Mod(x - wrapMin, range);
}

void Miku_Snap_float(float x, float increment, out float result)
{
    float inc = max(abs(increment), 1e-8);
    result = floor(x / inc + 0.5) * inc;
}

// ============================================================================
// Smooth Minimum / Maximum
// ============================================================================
// Blender 5.2 smooth minimum/maximum formula:
//   smooth_min(a, b, k) where k is the smoothing distance.
//   For k > 0, the function smoothly blends between a and b.
//   For k = 0, it's equivalent to min(a, b).
// Reference: Inigo Quilez — Smooth Minimum (public domain)
//   https://iquilezles.org/articles/smin/

void Miku_SmoothMin_float(float a, float b, float k, out float result)
{
    float h = max(k - abs(a - b), 0.0) / max(k, 1e-8);
    result = min(a, b) - h * h * k * 0.25;
}

void Miku_SmoothMax_float(float a, float b, float k, out float result)
{
    float h = max(k - abs(a - b), 0.0) / max(k, 1e-8);
    result = max(a, b) + h * h * k * 0.25;
}

// ============================================================================
// Vector variants (for Vector Math node compatibility)
// ============================================================================

float2 Miku_Mod2(float2 x, float y)
{
    float yy = (abs(y) < 1e-8) ? 1e-8 : y;
    return x - yy * floor(x / yy);
}

float3 Miku_Mod3(float3 x, float y)
{
    float yy = (abs(y) < 1e-8) ? 1e-8 : y;
    return x - yy * floor(x / yy);
}

float4 Miku_Mod4(float4 x, float y)
{
    float yy = (abs(y) < 1e-8) ? 1e-8 : y;
    return x - yy * floor(x / yy);
}

void Miku_Sine_float2(float2 x, out float2 result)
{
    result = sin(x);
}

void Miku_Cosine_float2(float2 x, out float2 result)
{
    result = cos(x);
}

void Miku_Tangent_float2(float2 x, out float2 result)
{
    result = tan(x);
}

void Miku_Floor_float2(float2 x, out float2 result)
{
    result = floor(x);
}

void Miku_Ceil_float2(float2 x, out float2 result)
{
    result = ceil(x);
}

void Miku_Fraction_float2(float2 x, out float2 result)
{
    result = x - floor(x);
}

void Miku_Modulo_float2(float2 x, float y, out float2 result)
{
    result = Miku_Mod2(x, y);
}

void Miku_Wrap_float2(float2 x, float minVal, float maxVal, out float2 result)
{
    float range = max(abs(maxVal - minVal), 1e-8);
    result = minVal + Miku_Mod2(x - minVal, range);
}

void Miku_Snap_float2(float2 x, float increment, out float2 result)
{
    float inc = max(abs(increment), 1e-8);
    result = floor(x / inc + 0.5) * inc;
}

// ============================================================================
// Vector3 overloads
// ============================================================================

void Miku_Sine_float3(float3 x, out float3 result) { result = sin(x); }
void Miku_Cosine_float3(float3 x, out float3 result) { result = cos(x); }
void Miku_Tangent_float3(float3 x, out float3 result) { result = tan(x); }
void Miku_Floor_float3(float3 x, out float3 result) { result = floor(x); }
void Miku_Ceil_float3(float3 x, out float3 result) { result = ceil(x); }
void Miku_Fraction_float3(float3 x, out float3 result) { result = x - floor(x); }

void Miku_Modulo_float3(float3 x, float y, out float3 result)
{
    result = Miku_Mod3(x, y);
}

void Miku_Wrap_float3(float3 x, float minVal, float maxVal, out float3 result)
{
    float range = max(abs(maxVal - minVal), 1e-8);
    result = minVal + Miku_Mod3(x - minVal, range);
}

void Miku_Snap_float3(float3 x, float increment, out float3 result)
{
    float inc = max(abs(increment), 1e-8);
    result = floor(x / inc + 0.5) * inc;
}

#endif // MIKU_BLENDER_MATH_INCLUDED
