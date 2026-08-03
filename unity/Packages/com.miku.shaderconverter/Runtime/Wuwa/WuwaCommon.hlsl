// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
#ifndef MIKU_WUWA_COMMON_INCLUDED
#define MIKU_WUWA_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "../NPR/NPR_FaceSDF.hlsl"

float3 Wuwa_Desaturate(float3 color)
{
    float gray = dot(color, float3(0.3, 0.59, 0.11));
    return gray.xxx;
}

float3 Wuwa_AdjustSaturation(float3 color, float saturation)
{
    return lerp(Wuwa_Desaturate(color), color, saturation);
}

float3 Wuwa_ApplyPowerCurve(float3 color, float power, float brightness)
{
    return max(pow(saturate(color), max(power, 1e-3)) * brightness, 0.0.xxx);
}

float3 Wuwa_SampleSH_Indirect(float3 normalWS, float flattenNormal)
{
    float3 shNormal = normalize(lerp(normalWS, float3(0.0, 1.0, 0.0), saturate(flattenNormal)));
    return SampleSH(shNormal);
}

float Wuwa_BodyToonLight(
    float3 normalWS,
    float3 lightDirWS,
    float4 idMap,
    float shadowStart,
    float shadowEnd,
    float idShadowOffsetStrength,
    float darkBias)
{
    float halfLambert = dot(normalize(normalWS), normalize(lightDirWS)) * 0.5 + 0.5;
    float idOffset = (idMap.g - 0.5) * idShadowOffsetStrength + darkBias;
    return smoothstep(shadowStart + idOffset, shadowEnd + idOffset, halfLambert);
}

float Wuwa_SelectChannel(float4 value, float channel)
{
    int index = (int)round(channel);
    if (index == 1)
        return value.g;
    if (index == 2)
        return value.b;
    if (index == 3)
        return value.a;
    return value.r;
}

float Wuwa_FaceSDFLight(
    float2 uv,
    float4 faceSdfSample,
    TEXTURE2D_PARAM(faceSdfTex, sampler_faceSdfTex),
    float3 lightDirWS,
    float3 headForwardWS,
    float3 headRightWS,
    float3 headUpWS,
    float mainChannel,
    float softChannel,
    float faceShadowOffset,
    float faceShadowSoftness,
    float faceThresholdBias,
    float softChannelStrength)
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

    float3 crossedHeadRightWS = cross(headUpWS, headForwardWS);
    headRightWS = dot(crossedHeadRightWS, crossedHeadRightWS) > 1e-5
        ? normalize(crossedHeadRightWS)
        : normalize(headRightWS);
    float3 crossedHeadUpWS = cross(headForwardWS, headRightWS);
    headUpWS = dot(crossedHeadUpWS, crossedHeadUpWS) > 1e-5
        ? normalize(crossedHeadUpWS)
        : headUpWS;
    float3 projectedLightWS = lightDirWS - dot(lightDirWS, headUpWS) * headUpWS;
    float3 fixedLightDirectionWS = dot(projectedLightWS, projectedLightWS) > 1e-5
        ? normalize(projectedLightWS)
        : headForwardWS;
    float2 sdfUV = uv;
    // The source Blender graph samples -U on the positive-right light side.
    if (dot(fixedLightDirectionWS, headRightWS) > 0.0)
        sdfUV.x = 1.0 - sdfUV.x;

    float4 mirroredSdfSample = SAMPLE_TEXTURE2D(faceSdfTex, sampler_faceSdfTex, sdfUV);
    float sdfValue = Wuwa_SelectChannel(mirroredSdfSample, mainChannel) + faceShadowOffset;
    float softValue = Wuwa_SelectChannel(mirroredSdfSample, softChannel) + faceShadowOffset;
    float sdfThreshold = 1.0 - (dot(fixedLightDirectionWS, headForwardWS) * 0.5 + 0.5) + faceThresholdBias;
    float softness = max(faceShadowSoftness, 1e-5);
    float sourceMap = saturate((sdfValue - (sdfThreshold - softness)) / (softness * 2.0));
    float mainMask = saturate(lerp(-1.3, 1.0, sourceMap));
    float softMask = smoothstep(sdfThreshold - softness * 2.0, sdfThreshold + softness, softValue);
    return saturate(lerp(mainMask, mainMask * softMask, saturate(softChannelStrength)));
}

float Wuwa_HairShadowMask(
    TEXTURE2D_PARAM(hairShadowTex, sampler_hairShadowTex),
    float4 positionCS,
    float3 lightDirWS,
    float screenOffset,
    float depthBias,
    float softness,
    float strength,
    float available)
{
    float2 screenUV = positionCS.xy / max(_ScaledScreenParams.xy, 1e-5.xx);
    #if UNITY_UV_STARTS_AT_TOP
        screenUV.y = 1.0 - screenUV.y;
    #endif
    float2 lightOffset = normalize(lightDirWS.xy + 1e-5.xx) * screenOffset;
    float sampledDepth01 = SAMPLE_TEXTURE2D(hairShadowTex, sampler_hairShadowTex, saturate(screenUV + lightOffset)).r;
    float faceDepth01 = LinearEyeDepth(positionCS.z, _ZBufferParams) / max(_ProjectionParams.z, 1e-5);
    float shadow = saturate((faceDepth01 - sampledDepth01 - depthBias) / max(softness, 1e-5));
    return saturate(shadow * strength * step(0.5, available));
}

float3 Wuwa_GetOutlineNormalOS(float3 smoothNormalOS, float3 fallbackNormalOS)
{
    float3 selectedNormalOS = dot(smoothNormalOS, smoothNormalOS) > 1e-5
        ? smoothNormalOS
        : fallbackNormalOS;
    return normalize(selectedNormalOS);
}

float Wuwa_DistanceCompensatedOutlineWidth(
    float3 positionWS,
    float outlineWidth,
    float referenceDistance,
    float distanceScale)
{
    float safeReferenceDistance = max(referenceDistance, 1e-5);
    float cameraDistance = distance(_WorldSpaceCameraPos, positionWS);
    float farScale = max(cameraDistance / safeReferenceDistance, 1.0);
    return outlineWidth * lerp(1.0, farScale, saturate(distanceScale));
}

float3 Wuwa_IDOutlineColor(float4 idMap, float3 outlineColor)
{
    float regionScale = lerp(0.82, 1.0, saturate(idMap.r));
    float3 coolShift = lerp(1.0.xxx, float3(0.82, 0.88, 1.0), saturate(idMap.g) * 0.18);
    return max(outlineColor * regionScale * coolShift, 0.0.xxx);
}

#endif
