// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

#ifndef MIKU_GAME_TOON_SCREEN_RIM_PASS_INCLUDED
#define MIKU_GAME_TOON_SCREEN_RIM_PASS_INCLUDED

#include "Packages/com.miku.shaderconverter/Runtime/GameToon/MikuToonScreenRimMask.hlsl"

struct MikuGameScreenRimAttributes
{
    float4 positionOS : POSITION;
};

struct MikuGameScreenRimVaryings
{
    float4 positionCS : SV_POSITION;
};

MikuGameScreenRimVaryings MikuGameScreenRimVertex(
    MikuGameScreenRimAttributes input)
{
    MikuGameScreenRimVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}

MikuToonScreenRimMaskOutput MikuGameScreenRimFragment(
    MikuGameScreenRimVaryings input)
{
    return MikuBuildScreenRimMask(
        _RimLightTintColor.rgb,
        _RimLightBrightness,
        _RimLightWidth,
        _RimLightThreshold,
        _RimLightFadeout,
        1.0);
}

#endif
