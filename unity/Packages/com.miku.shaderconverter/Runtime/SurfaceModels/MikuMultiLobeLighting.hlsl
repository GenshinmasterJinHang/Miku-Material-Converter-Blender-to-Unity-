// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
//
// Version-locked to Unity 6000.4.5f1 / URP 17.4.0. This file intentionally
// uses URP's public ShaderLibrary lighting entry points; Shader Graph internal
// serialization remains isolated in the editor adapter.

#ifndef MIKU_MULTI_LOBE_LIGHTING_INCLUDED
#define MIKU_MULTI_LOBE_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

static const half MIKU_MIN_ROUGHNESS = 0.045h;
static const half MIKU_LIGHTING_EPSILON = 0.0001h;

bool MikuIsFinite(half value)
{
    return !isnan((float)value) && !isinf((float)value);
}

bool MikuIsFinite3(half3 value)
{
    return !AnyIsNaN((float3)value) && !AnyIsInf((float3)value);
}

half MikuFiniteOr(half value, half fallback)
{
    return MikuIsFinite(value) ? value : fallback;
}

half3 MikuFiniteOr3(half3 value, half3 fallback)
{
    return MikuIsFinite3(value) ? value : fallback;
}

half3 MikuSafeNormalize(half3 value, half3 fallback)
{
    value = MikuFiniteOr3(value, fallback);
    half lengthSquared = dot(value, value);
    if (!MikuIsFinite(lengthSquared) || lengthSquared <= MIKU_LIGHTING_EPSILON)
        value = fallback;
    return SafeNormalize(value);
}

half3 MikuFresnelSchlick(half cosineTheta, half3 f0)
{
    half factor = Pow4(1.0h - saturate(cosineTheta));
    factor *= 1.0h - saturate(cosineTheta);
    return f0 + (1.0h - f0) * factor;
}

half3 MikuEvaluateDirectLobe(
    half3 normalWS,
    half3 viewDirectionWS,
    half4 baseColor,
    half roughness,
    half metallic,
    half lobeKind,
    Light light)
{
    half3 lightDirectionWS = MikuSafeNormalize(
        light.direction,
        normalWS);
    half ndotl = saturate(dot(normalWS, lightDirectionWS));
    half attenuation =
        MikuFiniteOr(light.distanceAttenuation, 0.0h) *
        MikuFiniteOr(light.shadowAttenuation, 0.0h);
    attenuation = max(attenuation, 0.0h);
    if (ndotl <= 0.0h || attenuation <= 0.0h)
        return 0.0h;

    half3 halfDirectionWS = MikuSafeNormalize(
        viewDirectionWS + lightDirectionWS,
        normalWS);
    half ndotv = max(saturate(dot(normalWS, viewDirectionWS)), MIKU_LIGHTING_EPSILON);
    half ndoth = saturate(dot(normalWS, halfDirectionWS));
    half vdoth = saturate(dot(viewDirectionWS, halfDirectionWS));

    half alpha = max(roughness * roughness, MIKU_LIGHTING_EPSILON);
    half alphaSquared = max(alpha * alpha, MIKU_LIGHTING_EPSILON);
    half distributionDenominator =
        ndoth * ndoth * (alphaSquared - 1.0h) + 1.0h;
    distributionDenominator = max(
        distributionDenominator * distributionDenominator,
        MIKU_LIGHTING_EPSILON);
    half distribution = alphaSquared /
        (PI * distributionDenominator);

    half geometryK = roughness + 1.0h;
    geometryK = max(
        geometryK * geometryK * 0.125h,
        MIKU_LIGHTING_EPSILON);
    half geometryView = ndotv /
        max(ndotv * (1.0h - geometryK) + geometryK, MIKU_LIGHTING_EPSILON);
    half geometryLight = ndotl /
        max(ndotl * (1.0h - geometryK) + geometryK, MIKU_LIGHTING_EPSILON);

    half3 color = max(MikuFiniteOr3(baseColor.rgb, 0.0h), 0.0h);
    half3 diffuse = 0.0h;
    half3 f0 = color;
    if (lobeKind < 0.5h)
    {
        diffuse = color * INV_PI;
        f0 = 0.0h;
    }
    else if (lobeKind < 1.5h)
    {
        f0 = color;
    }
    else if (lobeKind < 2.5h)
    {
        f0 = color;
    }
    else
    {
        diffuse = color * (1.0h - metallic) * INV_PI;
        f0 = lerp(kDielectricSpec.rgb, color, metallic);
    }

    half3 fresnel = MikuFresnelSchlick(vdoth, f0);
    half3 specular = distribution * geometryView * geometryLight * fresnel /
        max(4.0h * ndotv * ndotl, MIKU_LIGHTING_EPSILON);
    half3 lightColor = max(MikuFiniteOr3(light.color, 0.0h), 0.0h);
    return (diffuse + specular) * lightColor * (ndotl * attenuation);
}

half3 MikuEvaluateIndirectLobe(
    half3 normalWS,
    half4 baseColor,
    half metallic,
    half lobeKind)
{
    if (lobeKind >= 0.5h && lobeKind < 2.5h)
        return 0.0h;
    half3 color = max(MikuFiniteOr3(baseColor.rgb, 0.0h), 0.0h);
    half diffuseFactor = lobeKind >= 2.5h ? 1.0h - metallic : 1.0h;
    half3 irradiance = max(MikuFiniteOr3(SampleSH(normalWS), 0.0h), 0.0h);
    return color * diffuseFactor * irradiance;
}

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
    normalWS = MikuSafeNormalize(normalWS, half3(0.0h, 0.0h, 1.0h));
    viewDirectionWS = MikuSafeNormalize(viewDirectionWS, normalWS);
    roughness = clamp(
        MikuFiniteOr(roughness, 0.5h),
        MIKU_MIN_ROUGHNESS,
        1.0h);
    metallic = saturate(MikuFiniteOr(metallic, 0.0h));
    lobeKind = clamp(MikuFiniteOr(lobeKind, 0.0h), 0.0h, 3.0h);
    weight = max(MikuFiniteOr(weight, 0.0h), 0.0h);
    baseColor = half4(
        max(MikuFiniteOr3(baseColor.rgb, 0.0h), 0.0h),
        saturate(MikuFiniteOr(baseColor.a, 1.0h)));

#if defined(SHADERGRAPH_PREVIEW)
    half previewNdotL = saturate(dot(
        normalWS,
        MikuSafeNormalize(half3(0.4h, 0.6h, 0.7h), normalWS)));
    half3 previewDiffuse = lobeKind < 0.5h || lobeKind >= 2.5h
        ? baseColor.rgb * (lobeKind >= 2.5h ? 1.0h - metallic : 1.0h)
        : 0.0h;
    half3 previewSpecular = lobeKind >= 0.5h
        ? lerp(kDielectricSpec.rgb, baseColor.rgb, metallic) *
            (1.0h - roughness * 0.75h)
        : 0.0h;
    half3 previewRadiance =
        (previewDiffuse * (0.2h + 0.8h * previewNdotL)) + previewSpecular;
    return MikuIsFinite3(previewRadiance)
        ? previewRadiance * weight
        : 0.0h;
#else
    half3 radiance = MikuEvaluateIndirectLobe(
        normalWS,
        baseColor,
        metallic,
        lobeKind);

    half4 shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight();
    // UniversalUnlitSubTarget does not request the per-object LightData that
    // backs unity_LightData.z. URP still supplies the global main directional
    // light color/direction, so using that uninitialized attenuation would
    // incorrectly black out every non-clear-coat CustomMultiLobe material.
    // A missing main light has zero color and therefore remains non-emissive.
    mainLight.distanceAttenuation = 1.0h;
    mainLight.shadowAttenuation = MainLightRealtimeShadow(shadowCoord);
    radiance += MikuEvaluateDirectLobe(
        normalWS,
        viewDirectionWS,
        baseColor,
        roughness,
        metallic,
        lobeKind,
        mainLight);

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
        radiance += MikuEvaluateDirectLobe(
            normalWS,
            viewDirectionWS,
            baseColor,
            roughness,
            metallic,
            lobeKind,
            light);
    LIGHT_LOOP_END
#endif

    radiance *= weight;
    return MikuIsFinite3(radiance) ? radiance : 0.0h;
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
