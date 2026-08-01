// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
//
// Version-locked to Unity 6000.4.5f1 / URP 17.4.0. This file intentionally
// uses URP's public ShaderLibrary lighting entry points; Shader Graph internal
// serialization remains isolated in the editor adapter.

#ifndef MIKU_MULTI_LOBE_LIGHTING_INCLUDED
#define MIKU_MULTI_LOBE_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

half3 MikuEvaluateLobeInternal(
    float3 positionWS,
    half3 normalWS,
    half3 viewDirectionWS,
    float4 screenPosition,
    half4 baseColor,
    half roughness,
    half metallic,
    half lobeKind,
    half weight)
{
    normalWS = SafeNormalize(normalWS);
    viewDirectionWS = SafeNormalize(viewDirectionWS);
    roughness = saturate(roughness);
    metallic = saturate(metallic);
    weight = max(weight, 0.0h);

    // 0 = Diffuse, 1 = Glossy, 2 = Metallic, 3 = Principled.
    half effectiveMetallic = lobeKind > 1.5h && lobeKind < 2.5h
        ? 1.0h
        : metallic;
    half alpha = 1.0h;
    BRDFData brdfData;
    InitializeBRDFData(
        baseColor.rgb,
        effectiveMetallic,
        baseColor.rgb,
        1.0h - roughness,
        alpha,
        brdfData);
    if (lobeKind < 0.5h)
        brdfData.specular = 0.0h;
    else if (lobeKind < 1.5h)
        brdfData.diffuse = 0.0h;

#if defined(SHADERGRAPH_PREVIEW)
    return weight * (
        brdfData.diffuse +
        brdfData.specular * (1.0h - roughness));
#else
    half3 radiance = GlobalIllumination(
        brdfData,
        SampleSH(normalWS),
        1.0h,
        positionWS,
        normalWS,
        viewDirectionWS);

    half4 shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(
        shadowCoord,
        positionWS,
        shadowMask);
    radiance += LightingPhysicallyBased(
        brdfData,
        mainLight,
        normalWS,
        viewDirectionWS);

#if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    // Shader Graph Screen Position is configured in Default mode, which
    // already supplies normalized device coordinates in xy.
    inputData.normalizedScreenSpaceUV = screenPosition.xy;
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(
            lightIndex,
            positionWS,
            shadowMask);
        radiance += LightingPhysicallyBased(
            brdfData,
            light,
            normalWS,
            viewDirectionWS);
    LIGHT_LOOP_END
#endif

    return radiance * weight;
#endif
}

void MikuEvaluateLobe_half(
    half3 PositionWS,
    half3 NormalWS,
    half3 ViewDirectionWS,
    half4 ScreenPosition,
    half4 BaseColor,
    half Roughness,
    half Metallic,
    half LobeKind,
    half Weight,
    out half3 Out)
{
    Out = MikuEvaluateLobeInternal(
        PositionWS,
        NormalWS,
        ViewDirectionWS,
        ScreenPosition,
        BaseColor,
        Roughness,
        Metallic,
        LobeKind,
        Weight);
}

void MikuEvaluateLobe_float(
    float3 PositionWS,
    float3 NormalWS,
    float3 ViewDirectionWS,
    float4 ScreenPosition,
    float4 BaseColor,
    float Roughness,
    float Metallic,
    float LobeKind,
    float Weight,
    out float3 Out)
{
    Out = MikuEvaluateLobeInternal(
        PositionWS,
        NormalWS,
        ViewDirectionWS,
        ScreenPosition,
        BaseColor,
        Roughness,
        Metallic,
        LobeKind,
        Weight);
}

#endif
