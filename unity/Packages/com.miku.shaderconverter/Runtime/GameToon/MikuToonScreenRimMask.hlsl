// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

#ifndef MIKU_TOON_SCREEN_RIM_MASK_INCLUDED
#define MIKU_TOON_SCREEN_RIM_MASK_INCLUDED

struct MikuToonScreenRimMaskOutput
{
    half4 colorWidth : SV_Target0;
    half4 thresholdFade : SV_Target1;
};

MikuToonScreenRimMaskOutput MikuBuildScreenRimMask(
    half3 color,
    half brightness,
    half widthPixels,
    half threshold,
    half fade,
    half enabled)
{
    MikuToonScreenRimMaskOutput output;
    half active = saturate(enabled);
    output.colorWidth = half4(
        color * brightness * active,
        saturate(widthPixels / 16.0) * active);
    output.thresholdFade = half4(
        max(threshold, 0.0),
        max(fade, 0.00001),
        0.0,
        0.0);
    return output;
}

#endif
