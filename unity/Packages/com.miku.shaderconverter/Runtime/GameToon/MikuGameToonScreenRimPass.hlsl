// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

#ifndef MIKU_GAME_TOON_SCREEN_RIM_PASS_INCLUDED
#define MIKU_GAME_TOON_SCREEN_RIM_PASS_INCLUDED

#include "Packages/com.miku.shaderconverter/Runtime/GameToon/MikuToonScreenRimMask.hlsl"

struct MikuGameScreenRimAttributes
{
    float4 positionOS : POSITION;
#if defined(MIKU_GAME_TOON_ALPHA_COVERAGE)
    float2 uv : TEXCOORD0;
#endif
};

struct MikuGameScreenRimVaryings
{
    float4 positionCS : SV_POSITION;
#if defined(MIKU_GAME_TOON_ALPHA_COVERAGE)
    float2 uv : TEXCOORD0;
#endif
};

MikuGameScreenRimVaryings MikuGameScreenRimVertex(
    MikuGameScreenRimAttributes input)
{
    MikuGameScreenRimVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
#if defined(MIKU_GAME_TOON_ALPHA_COVERAGE)
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
#endif
    return output;
}

MikuToonScreenRimMaskOutput MikuGameScreenRimFragment(
    MikuGameScreenRimVaryings input)
{
#if defined(MIKU_GAME_TOON_ALPHA_COVERAGE)
    float baseAlpha =
        SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a *
        _BaseColorTint.a;
    Genshin_ApplyBaseAlphaCoverage(baseAlpha, _DiffuseA, _Cutoff);
#endif
    return MikuBuildScreenRimMask(
        _RimLightTintColor.rgb,
        _RimLightBrightness,
        _RimLightWidth,
        _RimLightThreshold,
        _RimLightFadeout,
        1.0);
}

#endif
