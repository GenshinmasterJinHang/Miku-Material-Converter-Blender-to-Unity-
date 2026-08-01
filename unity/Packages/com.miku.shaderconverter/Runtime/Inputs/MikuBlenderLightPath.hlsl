#ifndef MIKU_BLENDER_LIGHT_PATH_INCLUDED
#define MIKU_BLENDER_LIGHT_PATH_INCLUDED

// SPDX-FileCopyrightText: 2026 Miku contributors
// SPDX-License-Identifier: MIT
//
// Raster-pass equivalent for the EEVEE Light Path outputs that have a
// deterministic URP meaning. Other Blender Light Path outputs are rejected
// before Shader Graph generation.

void Miku_LightPath_float(
    out float IsCameraRay,
    out float IsShadowRay)
{
#if defined(SHADERPASS_SHADOWCASTER)
    IsCameraRay = 0.0;
    IsShadowRay = 1.0;
#else
    IsCameraRay = 1.0;
    IsShadowRay = 0.0;
#endif
}

#endif
