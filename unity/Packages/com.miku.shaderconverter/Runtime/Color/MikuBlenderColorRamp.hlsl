#ifndef MIKU_BLENDER_COLOR_RAMP_INCLUDED
#define MIKU_BLENDER_COLOR_RAMP_INCLUDED

// Miku 1.0 semantic-region clean-room implementation, commit
// fbe6228777e7d9afefcd61a413844e790ae75db7; Miku owns Unity entry points.

// SPDX-FileCopyrightText: 2026 Miku contributors
// SPDX-License-Identifier: MIT
//
// Blender-compatible Color Ramp evaluator with multi-stop interpolation.
// CLEAN_REIMPLEMENTATION — no Blender source code was copied.
//
// References:
//   - Standard color interpolation mathematics (public domain)
//   - Blender 5.2 Manual — Color Ramp node specification only

// ============================================================================
// Interpolation Modes
// ============================================================================
// 0 = Constant, 1 = Linear, 2 = Ease (smoothstep), 3 = B-Spline, 4 = Cardinal

float Miku_ColorRampEase(float t)
{
    return t * t * (3.0 - 2.0 * t);
}

float Miku_ColorRampBSpline(float t)
{
    // Cubic B-spline basis for smooth interpolation
    float t2 = t * t;
    float t3 = t2 * t;
    return (3.0 * t3 - 6.0 * t2 + 4.0) / 6.0;
}

// ============================================================================
// HSV Conversion Helpers
// ============================================================================

float3 Miku_ColorRampRGBToHSV(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 Miku_ColorRampHSVToRGB(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

float3 Miku_ColorRampHSLToRGB(float3 hsl)
{
    float3 rgb = saturate(abs(hsl.x * 6.0 - float3(3.0, 2.0, 4.0)) - 1.0);
    return hsl.z + hsl.y * (rgb - 0.5) * (1.0 - abs(2.0 * hsl.z - 1.0));
}

float3 Miku_ColorRampRGBToHSL(float3 c)
{
    float minC = min(min(c.r, c.g), c.b);
    float maxC = max(max(c.r, c.g), c.b);
    float l = (minC + maxC) * 0.5;
    float3 hsl = float3(0.0, 0.0, l);
    if (maxC - minC > 1e-5)
    {
        float d = maxC - minC;
        hsl.y = (l > 0.5) ? d / (2.0 - maxC - minC) : d / (maxC + minC);
        if (abs(maxC - c.r) < 1e-5)
            hsl.x = (c.g - c.b) / d + (c.g < c.b ? 6.0 : 0.0);
        else if (abs(maxC - c.g) < 1e-5)
            hsl.x = (c.b - c.r) / d + 2.0;
        else
            hsl.x = (c.r - c.g) / d + 4.0;
        hsl.x /= 6.0;
    }
    return hsl;
}

// ============================================================================
// Color Interpolation in Different Color Modes
// ============================================================================
// colorMode: 0 = RGB, 1 = HSV, 2 = HSL

float4 Miku_InterpolateColor(float4 a, float4 b, float t, int colorMode)
{
    switch (colorMode)
    {
        case 1: // HSV
            {
                float3 hsvA = Miku_ColorRampRGBToHSV(a.rgb);
                float3 hsvB = Miku_ColorRampRGBToHSV(b.rgb);
                // Handle hue wrapping: take shortest path
                float hueDiff = hsvB.x - hsvA.x;
                if (abs(hueDiff) > 0.5)
                    hsvB.x -= sign(hueDiff);
                float3 hsv = lerp(hsvA, hsvB, t);
                hsv.x = frac(hsv.x);
                return float4(Miku_ColorRampHSVToRGB(hsv), lerp(a.a, b.a, t));
            }
        case 2: // HSL
            {
                float3 hslA = Miku_ColorRampRGBToHSL(a.rgb);
                float3 hslB = Miku_ColorRampRGBToHSL(b.rgb);
                float hueDiff = hslB.x - hslA.x;
                if (abs(hueDiff) > 0.5)
                    hslB.x -= sign(hueDiff);
                float3 hsl = lerp(hslA, hslB, t);
                hsl.x = frac(hsl.x);
                return float4(Miku_ColorRampHSLToRGB(hsl), lerp(a.a, b.a, t));
            }
        default: // RGB
            return lerp(a, b, t);
    }
}

// ============================================================================
// Hue Interpolation Direction
// ============================================================================
// hueInterpolation: 0 = Near (shortest path), 1 = CW (clockwise), 2 = CCW (counter-clockwise)

float Miku_AdjustHueForDirection(float hueA, float hueB, int hueInterpolation)
{
    float diff = hueB - hueA;
    switch (hueInterpolation)
    {
        case 1: // CW — always go forward
            if (diff < 0.0) hueB -= 1.0;
            break;
        case 2: // CCW — always go backward
            if (diff > 0.0) hueB -= 1.0;
            break;
        default: // Near — shortest path
            if (abs(diff) > 0.5)
                hueB -= sign(diff);
            break;
    }
    return hueB;
}

// ============================================================================
// Main Color Ramp Evaluator
// ============================================================================
// Evaluates a color ramp with arbitrary stops at the given factor position.
//
// Parameters:
//   fac             — input factor (typically 0–1, but works outside)
//   stops           — array of stop positions (sorted ascending, values 0–1)
//   colors          — array of RGBA colors (one per stop)
//   stopCount       — number of stops (1–16)
//   interpolation   — 0=Constant, 1=Linear, 2=Ease, 3=B-Spline, 4=Cardinal
//   colorMode       — 0=RGB, 1=HSV, 2=HSL
//   hueInterpolation — 0=Near, 1=CW, 2=CCW

float4 Miku_EvaluateColorRamp(float fac, float stops[16], float4 colors[16],
                               int stopCount, int interpolation, int colorMode,
                               int hueInterpolation)
{
    if (stopCount <= 0)
        return float4(0.0, 0.0, 0.0, 1.0);

    if (stopCount == 1)
        return colors[0];

    // Constant interpolation: find the stop whose position is just before fac
    if (interpolation == 0)
    {
        int idx = 0;
        for (int i = 0; i < stopCount - 1; i++)
        {
            if (fac >= stops[i])
                idx = i;
        }
        // If fac is beyond last stop, use last stop
        if (fac >= stops[stopCount - 1])
            idx = stopCount - 1;
        // If fac is before first stop, use first stop
        if (fac < stops[0])
            idx = 0;
        return colors[idx];
    }

    // Find the segment
    int segStart = 0;
    for (int j = 0; j < stopCount - 1; j++)
    {
        if (fac >= stops[j] && fac < stops[j + 1])
            segStart = j;
    }

    // Boundary cases
    if (fac <= stops[0])
        return colors[0];
    if (fac >= stops[stopCount - 1])
        return colors[stopCount - 1];

    // Compute interpolation parameter
    float t = (fac - stops[segStart]) / max(stops[segStart + 1] - stops[segStart], 1e-8);

    // Apply interpolation curve
    switch (interpolation)
    {
        case 2: // Ease
            t = Miku_ColorRampEase(t);
            break;
        case 3: // B-Spline
            t = Miku_ColorRampBSpline(t * 2.0 - 1.0) * 0.5 + 0.5;
            break;
        case 4: // Cardinal
            // Simplified cardinal: smooth cubic
            t = t * t * (3.0 - 2.0 * t);
            break;
        default: // Linear
            break;
    }

    return Miku_InterpolateColor(colors[segStart], colors[segStart + 1], t, colorMode);
}

// ============================================================================
// Shader Graph Entry Point (fixed-size stop array for Shader Graph compatibility)
// ============================================================================
// Shader Graph Custom Function nodes need fixed-size arrays as individual inputs.
// This entry point handles up to 8 stops (common case). For more stops, use
// the generic Miku_EvaluateColorRamp function directly.
//
// Stops are passed as individual scalar (position) and float4 (color) parameters.

void Miku_ColorRamp8_float(float fac,
    float pos0, float4 color0,
    float pos1, float4 color1,
    float pos2, float4 color2,
    float pos3, float4 color3,
    float pos4, float4 color4,
    float pos5, float4 color5,
    float pos6, float4 color6,
    float pos7, float4 color7,
    int stopCount, int interpolation, int colorMode, int hueInterpolation,
    out float4 color, out float alpha)
{
    float stops[16];
    float4 colors[16];

    stops[0] = pos0; colors[0] = color0;
    stops[1] = pos1; colors[1] = color1;
    stops[2] = pos2; colors[2] = color2;
    stops[3] = pos3; colors[3] = color3;
    stops[4] = pos4; colors[4] = color4;
    stops[5] = pos5; colors[5] = color5;
    stops[6] = pos6; colors[6] = color6;
    stops[7] = pos7; colors[7] = color7;

    // Fill unused stops with sentinel values
    for (int i = stopCount; i < 8; i++)
    {
        stops[i] = 2.0; // Beyond normal range
        colors[i] = float4(0.0, 0.0, 0.0, 1.0);
    }

    float4 result = Miku_EvaluateColorRamp(fac, stops, colors, stopCount,
                                            interpolation, colorMode, hueInterpolation);
    color = result;
    alpha = result.a;
}

#endif // MIKU_BLENDER_COLOR_RAMP_INCLUDED
