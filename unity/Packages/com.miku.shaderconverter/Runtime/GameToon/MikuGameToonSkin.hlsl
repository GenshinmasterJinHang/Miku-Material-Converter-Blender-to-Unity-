// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
#ifndef MIKU_GAME_TOON_SKIN_INCLUDED
#define MIKU_GAME_TOON_SKIN_INCLUDED

float MikuGameToonHighValueMask(float value)
{
    return smoothstep(0.96, 0.995, saturate(value));
}

float MikuGameToonWarmPaleFaceMask(float3 rawBaseColor)
{
    float3 color = saturate(rawBaseColor);
    float maximum = max(color.r, max(color.g, color.b));
    float minimum = min(color.r, min(color.g, color.b));
    float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
    float chroma = maximum - minimum;

    float warm = smoothstep(0.015, 0.09, color.r - color.b)
        * smoothstep(-0.035, 0.055, color.r - color.g)
        * smoothstep(0.16, 0.42, luma);
    float pale = smoothstep(0.55, 0.82, minimum)
        * (1.0 - smoothstep(0.08, 0.22, chroma));
    float validColor = smoothstep(0.08, 0.20, luma);
    return saturate(max(warm, pale) * validColor);
}

float MikuGameToonStarRailBodySkinMask(float lightMapAlpha, float3 rawBaseColor)
{
    const float skinRow = 5.0 / 255.0;
    float rowMask = 1.0 - smoothstep(0.012, 0.035, abs(saturate(lightMapAlpha) - skinRow));
    float warmMask = smoothstep(0.02, 0.10, rawBaseColor.r - rawBaseColor.b)
        * smoothstep(-0.03, 0.07, rawBaseColor.r - rawBaseColor.g)
        * smoothstep(0.16, 0.42, dot(saturate(rawBaseColor), float3(0.2126, 0.7152, 0.0722)));
    return saturate(rowMask * warmMask);
}

float3 MikuGameToonApplySkinTone(
    float3 baseColor,
    float skinMask,
    float brightness,
    float whitening,
    float3 targetColor)
{
    float mask = saturate(skinMask);
    float3 brightened = max(baseColor, 0.0.xxx) * max(brightness, 0.0);
    float3 toned = lerp(brightened, max(targetColor, 0.0.xxx), saturate(whitening));
    return lerp(baseColor, toned, mask);
}

float3 MikuGameToonSkinSSS(
    float3 skinColor,
    float skinMask,
    float3 normalWS,
    float3 viewDirWS,
    float3 lightDirWS,
    float3 lightColor,
    float litAmount,
    float intensity,
    float area,
    float3 sssColor)
{
    float3 normal = normalize(normalWS);
    float3 viewDirection = normalize(viewDirWS);
    float3 lightDirection = normalize(lightDirWS);
    float viewEdge = pow(saturate(1.0 - dot(normal, viewDirection)), 2.0);
    float backLight = saturate((dot(-normal, lightDirection) + 0.35) / 1.35);
    float transmission = saturate(skinMask) * saturate(area * (0.35 + 0.65 * viewEdge));
    transmission *= lerp(0.18, 1.0, backLight) * (1.0 - 0.65 * saturate(litAmount));
    return max(skinColor, 0.0.xxx)
        * max(sssColor, 0.0.xxx)
        * max(lightColor, 0.0.xxx)
        * transmission
        * max(intensity, 0.0);
}

#endif
