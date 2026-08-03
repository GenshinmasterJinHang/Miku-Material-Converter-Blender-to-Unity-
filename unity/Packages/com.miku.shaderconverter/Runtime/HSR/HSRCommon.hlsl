// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
#ifndef MIKU_HSR_COMMON_INCLUDED
#define MIKU_HSR_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "../NPR/NPR_FaceSDF.hlsl"

float3 HSR_Desaturate(float3 color)
{
    float gray = dot(color, float3(0.3, 0.59, 0.11));
    return gray.xxx;
}

float3 HSR_SampleSH_Indirect(float3 normalWS, float flattenNormal)
{
    float3 shNormal = normalize(lerp(normalWS, float3(0.0, 1.0, 0.0), saturate(flattenNormal)));
    return SampleSH(shNormal);
}

float HSR_BodyMainShadow(float3 normalWS, float3 lightDirWS, float4 lightMap, float shadowThresholdCenter, float shadowThresholdSoftness)
{
    float NoL = dot(normalize(normalWS), normalize(lightDirWS));
    float remappedNoL = NoL * 0.5 + 0.5;
    float mainLightShadow = smoothstep(
        1.0 - lightMap.g + shadowThresholdCenter - shadowThresholdSoftness,
        1.0 - lightMap.g + shadowThresholdCenter + shadowThresholdSoftness,
        remappedNoL);
    mainLightShadow *= lightMap.r;
    return saturate(mainLightShadow);
}

float HSR_FaceSDFShadow(
    float2 uv,
    float4 faceMap,
    TEXTURE2D_PARAM(faceMapTex, sampler_faceMapTex),
    float3 lightDirWS,
    float3 headForwardWS,
    float3 headRightWS,
    float3 headUpWS,
    float faceShadowOffset,
    float faceShadowTransitionSoftness)
{
    headForwardWS = normalize(headForwardWS);
    headRightWS = normalize(headRightWS);
    headUpWS = normalize(headUpWS);

    // Blender/FBX axis conversion commonly leaves an otherwise upright
    // renderer rotated -90 degrees around X. In that layout local +Z becomes
    // world-up, while the old fixed Y-up/Z-forward basis makes horizontal
    // light rotation look vertical and prevents the face SDF threshold from
    // changing. Recover the upright basis, but preserve the original basis
    // for meshes whose axes were already baked for Unity.
    float3 worldUp = float3(0.0, 1.0, 0.0);
    if (abs(dot(headForwardWS, worldUp)) > 0.75 && abs(dot(headUpWS, worldUp)) < 0.75)
    {
        headUpWS = headForwardWS * (dot(headForwardWS, worldUp) < 0.0 ? -1.0 : 1.0);
        headForwardWS = normalize(cross(headRightWS, headUpWS));
    }

    float3 crossedHeadUpWS = cross(headForwardWS, headRightWS);
    headUpWS = dot(crossedHeadUpWS, crossedHeadUpWS) > 1e-5
        ? normalize(crossedHeadUpWS)
        : normalize(headUpWS);
    float3 projectedLightWS = lightDirWS - dot(lightDirWS, headUpWS) * headUpWS;
    float3 fixedLightDirectionWS = dot(projectedLightWS, projectedLightWS) > 1e-5
        ? normalize(projectedLightWS)
        : headForwardWS;
    float2 sdfUV = uv;
    float lightSide = dot(fixedLightDirectionWS, headRightWS);
    // Star Rail's FaceMap stores the opposite half in mirrored U.  The
    // reference implementation mirrors for light on head +Right.
    if (lightSide > 0.0)
        sdfUV.x = 1.0 - sdfUV.x;
    float sdfValue = SAMPLE_TEXTURE2D(faceMapTex, sampler_faceMapTex, sdfUV).a + faceShadowOffset;
    float sdfThreshold = 1.0 - (dot(fixedLightDirectionWS, headForwardWS) * 0.5 + 0.5);
    float sdf = smoothstep(
        sdfThreshold - faceShadowTransitionSoftness,
        sdfThreshold + faceShadowTransitionSoftness,
        sdfValue);
    return lerp(faceMap.g, sdf, step(faceMap.r, 0.5));
}

float HSR_FaceAO(float4 faceMap)
{
    return lerp(faceMap.g, 1.0, step(faceMap.r, 0.5));
}

float HSR_GetBodyRampRow(float lightMapAlpha, float rowCount)
{
    float rowIndex = floor(saturate(lightMapAlpha) * rowCount);
    return clamp(rowIndex, 0.0, max(rowCount - 1.0, 0.0));
}

float2 HSR_GetRampUV(float mainLightShadow, float rowIndex, float rowCount, float shadowRampOffset)
{
    float rampUVx = mainLightShadow * (1.0 - shadowRampOffset) + shadowRampOffset;
    // Row center in the HSR tutorial form: (2 * row + 1) / (rowCount * 2).
    float rampUVy = (2.0 * rowIndex + 1.0) * (1.0 / max(rowCount * 2.0, 1e-5));
    return float2(saturate(rampUVx), saturate(rampUVy));
}

float3 HSR_SampleRampRow(
    float mainLightShadow,
    float rowIndex,
    float3 lightDirWS,
    TEXTURE2D_PARAM(coolRamp, sampler_coolRamp),
    TEXTURE2D_PARAM(warmRamp, sampler_warmRamp),
    float rowCount,
    float shadowRampOffset)
{
    rowIndex = clamp(rowIndex, 0.0, max(rowCount - 1.0, 0.0));
    float2 rampUV = HSR_GetRampUV(mainLightShadow, rowIndex, rowCount, shadowRampOffset);
    float3 coolRampColor = SAMPLE_TEXTURE2D(coolRamp, sampler_coolRamp, rampUV).rgb;
    float3 warmRampColor = SAMPLE_TEXTURE2D(warmRamp, sampler_warmRamp, rampUV).rgb;
    float isDay = saturate(lightDirWS.y * 0.5 + 0.5);
    return lerp(coolRampColor, warmRampColor, isDay);
}

float3 HSR_SampleRamp(
    float mainLightShadow,
    float lightMapAlpha,
    float3 lightDirWS,
    TEXTURE2D_PARAM(coolRamp, sampler_coolRamp),
    TEXTURE2D_PARAM(warmRamp, sampler_warmRamp),
    float rowCount,
    float shadowRampOffset)
{
    float rowIndex = HSR_GetBodyRampRow(lightMapAlpha, rowCount);
    return HSR_SampleRampRow(
        mainLightShadow,
        rowIndex,
        lightDirWS,
        TEXTURE2D_ARGS(coolRamp, sampler_coolRamp),
        TEXTURE2D_ARGS(warmRamp, sampler_warmRamp),
        rowCount,
        shadowRampOffset);
}

float3 HSR_SampleHairRamp(
    float mainLightShadow,
    float3 lightDirWS,
    TEXTURE2D_PARAM(coolRamp, sampler_coolRamp),
    TEXTURE2D_PARAM(warmRamp, sampler_warmRamp),
    float shadowRampOffset)
{
    return HSR_SampleRampRow(
        mainLightShadow,
        0.0,
        lightDirWS,
        TEXTURE2D_ARGS(coolRamp, sampler_coolRamp),
        TEXTURE2D_ARGS(warmRamp, sampler_warmRamp),
        1.0,
        shadowRampOffset);
}

float HSR_ExtractMetallicFromLightMap(float lightMapAlpha, float metallicTarget, float metallicWidth)
{
    return 1.0 - saturate(abs(lightMapAlpha - metallicTarget) / max(metallicWidth, 1e-5));
}

float3 HSR_ComputeSpecular(
    float3 baseColor,
    float4 lightMap,
    float3 normalWS,
    float3 viewDirWS,
    float3 lightDirWS,
    float3 mainLightColor,
    float specularExponent,
    float specularKsNonMetal,
    float specularKsMetal,
    float specularBrightness,
    float metallic)
{
    float3 halfVectorWS = normalize(viewDirWS + lightDirWS);
    float NoH = saturate(dot(normalize(normalWS), halfVectorWS));
    float blinnPhong = pow(NoH, max(1.0, specularExponent));

    // LightMap.B has two meanings selected by the material region in A:
    // non-metals use it as a hard toon threshold, while metals retain its
    // continuous authored intensity and tint the highlight by base color.
    float nonMetalSpecular = step(1.04 - blinnPhong, saturate(lightMap.b)) * specularKsNonMetal;
    float3 metalSpecular = saturate(baseColor) * blinnPhong * saturate(lightMap.b) * specularKsMetal;

    float3 specularColor = lerp(nonMetalSpecular.xxx, metalSpecular, saturate(metallic));
    specularColor *= mainLightColor;
    specularColor *= specularBrightness;
    return specularColor;
}

float3 HSR_SampleStockingGradient(float x, float3 darkColor, float3 transitionColor, float3 lightColor, float transitionThreshold)
{
    x = saturate(x);
    if (x < transitionThreshold)
    {
        float t = saturate(x / max(transitionThreshold, 1e-5));
        return lerp(darkColor, transitionColor, t);
    }

    float t = saturate((x - transitionThreshold) / max(1.0 - transitionThreshold, 1e-5));
    return lerp(transitionColor, lightColor, t);
}

float3 HSR_ComputeStockingsEffect(
    float3 normalWS,
    float3 viewDirWS,
    float4 stockingsMap,
    float transitionPower,
    float transitionHardness,
    float textureUsage,
    float detailStrength,
    float detailMin,
    float3 darkColor,
    float3 transitionColor,
    float3 lightColor,
    float transitionThreshold,
    out float fac)
{
    float NoV = saturate(dot(normalize(normalWS), normalize(viewDirWS)));
    fac = pow(NoV, max(transitionPower, 1e-5));
    fac = saturate((fac - transitionHardness * 0.5) / max(1.0 - transitionHardness, 1e-5));
    float detail = lerp(detailMin, 1.0, stockingsMap.b);
    fac *= lerp(1.0, detail, saturate(textureUsage * detailStrength));
    // Tutorial convention: higher G means thinner/more translucent stockings.
    fac = lerp(fac, 1.0, stockingsMap.g);
    float3 stockingsColor = HSR_SampleStockingGradient(fac, darkColor, transitionColor, lightColor, transitionThreshold);
    return lerp(1.0.xxx, stockingsColor, stockingsMap.r);
}

float3 HSR_GetOutlineNormalOS(float3 smoothNormalOS, float3 fallbackNormalOS)
{
    float3 selectedNormalOS = dot(smoothNormalOS, smoothNormalOS) > 1e-5
        ? smoothNormalOS
        : fallbackNormalOS;
    return normalize(selectedNormalOS);
}

float4 HSR_ExtrudeOutlinePositionCS(
    float3 positionWS,
    float3 outlineNormalWS,
    float outlineWidth,
    float referenceDistance,
    float distanceScale)
{
    float cameraDistance = max(distance(_WorldSpaceCameraPos, positionWS), 1e-5);
    float distanceCompensation = cameraDistance / max(referenceDistance, 1e-5);
    float worldWidth = max(outlineWidth, 0.0)
        * lerp(1.0, distanceCompensation, saturate(distanceScale));
    float3 extrudedPositionWS = positionWS + normalize(outlineNormalWS) * worldWidth;
    return TransformWorldToHClip(extrudedPositionWS);
}

float3 HSR_BodyOutlineColor(
    TEXTURE2D_PARAM(coolRamp, sampler_coolRamp),
    TEXTURE2D_PARAM(warmRamp, sampler_warmRamp),
    float outlineGamma)
{
    float2 outlineUV = float2(0.0, 0.0625);
    float3 coolRampColor = SAMPLE_TEXTURE2D(coolRamp, sampler_coolRamp, outlineUV).rgb;
    float3 warmRampColor = SAMPLE_TEXTURE2D(warmRamp, sampler_warmRamp, outlineUV).rgb;
    float3 ramp = lerp(coolRampColor, warmRampColor, 0.5);
    return pow(max(ramp, 0.0.xxx), max(outlineGamma, 1e-5));
}

float3 HSR_HairOutlineColor(
    TEXTURE2D_PARAM(coolRamp, sampler_coolRamp),
    TEXTURE2D_PARAM(warmRamp, sampler_warmRamp),
    float outlineGamma)
{
    float2 outlineUV = float2(0.0, 0.0625);
    float3 coolRampColor = SAMPLE_TEXTURE2D(coolRamp, sampler_coolRamp, outlineUV).rgb;
    float3 warmRampColor = SAMPLE_TEXTURE2D(warmRamp, sampler_warmRamp, outlineUV).rgb;
    float3 ramp = lerp(coolRampColor, warmRampColor, 0.5);
    return pow(max(ramp, 0.0.xxx), max(outlineGamma, 1e-5));
}

#endif
