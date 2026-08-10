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

// Tutorial 3.1.1: minimal CookTorrance direct specular from the IcePaper Wuwa
// tutorial. roughness2MinusOne and normalizationTerm follow URP BRDFData so
// the same formulas can be validated by CPU math mirrors in EditMode tests.
float Wuwa_DirectBRDFSpecular(
    float3 normalWS,
    float3 lightDirWS,
    float3 viewDirWS,
    float roughness)
{
    float3 safeNormalWS = normalize(normalWS);
    float3 safeLightDirWS = normalize(lightDirWS);
    float3 safeViewDirWS = normalize(viewDirWS);
    float3 halfDir = normalize(safeLightDirWS + safeViewDirWS);

    float NoH = saturate(dot(safeNormalWS, halfDir));
    float LoH = saturate(dot(safeLightDirWS, halfDir));
    float NoL = saturate(dot(safeNormalWS, safeLightDirWS));
    float NoV = saturate(dot(safeNormalWS, safeViewDirWS));
    float safeRoughness = clamp(roughness, 1e-3, 1.0);
    float roughness2 = safeRoughness * safeRoughness;
    float roughness2MinusOne = roughness2 - 1.0;
    float normalizationTerm = max(4.0 * LoH * NoH, 1e-3);

    float d = NoH * NoH * roughness2MinusOne + 1.00001;
    float LoH2 = LoH * LoH;
    float specularTerm = roughness2 /
        ((d * d) * max(0.1, LoH2) * normalizationTerm);
    // Guard against invalid inputs while keeping the tutorial's response.
    return (specularTerm == specularTerm) &&
        abs(specularTerm) < 1e19 &&
        NoL > 0.0 && NoV > 0.0
        ? saturate(specularTerm)
        : 0.0;
}

// Tutorial 3.1.2: reflection-probe indirect specular without the Fresnel term
// (the tutorial removes fresnelTerm because a separate rim effect follows).
float3 Wuwa_IndirectSpecular(
    float3 normalWS,
    float3 viewDirWS,
    float roughness)
{
    float3 safeNormalWS = normalize(normalWS);
    float3 safeViewDirWS = normalize(viewDirWS);
    float3 reflectVector = reflect(-safeViewDirWS, safeNormalWS);
    float perceptualRoughness = clamp(roughness, 0.0, 1.0);
    float3 reflected = GlossyEnvironmentReflection(
        reflectVector,
        perceptualRoughness,
        1.0);
    float reflectedEnergy = dot(reflected, reflected);
    return (reflectedEnergy == reflectedEnergy) &&
        abs(reflectedEnergy) < 1e19
        ? reflected
        : 0.0.xxx;
}

// Tutorial 3.2: MatCap is desaturated before it is added onto the albedo so
// the metal highlight reads as energy on the base color rather than an
// independent unlit overlay.
float3 Wuwa_MatcapAlbedo(
    float3 albedo,
    float3 matcap,
    float saturation,
    float mask,
    float strength)
{
    float3 tone = lerp(Wuwa_Desaturate(matcap), matcap, saturate(saturation));
    return albedo + tone * saturate(mask) * max(strength, 0.0);
}

// Tutorial 3.4: vertical gradient applied as a multiply toward a low color.
float3 Wuwa_VerticalGradient(
    float3 color,
    float3 lowColor,
    float gradingValue)
{
    float amount = saturate(gradingValue);
    return lerp(color * max(lowColor, 0.0.xxx), color, amount);
}

// Tutorial 3.5.1: Fresnel edge light with a hard step.
float3 Wuwa_FresnelStepRim(
    float3 normalWS,
    float3 viewDirWS,
    float fresnelPower,
    float brightness,
    float3 tint,
    float3 baseColor)
{
    float NoV = saturate(dot(normalize(normalWS), normalize(viewDirWS)));
    float fresnel = pow(
        saturate(1.0 - NoV),
        max(fresnelPower, 0.1));
    fresnel = step(0.5, fresnel);
    return fresnel * max(brightness, 0.0) * max(tint, 0.0.xxx) *
        max(baseColor, 0.0.xxx);
}

// Selects the V coordinate of one of the model's four UV sets for the
// vertical-gradient channel (tutorial 3.4). Feibi's EXTRAUVS2 (Unity uv3)
// spans 0..1 from bottom to top on body parts and stays constant on the face.
float Wuwa_GradientValue(
    float2 uv0,
    float2 uv1,
    float2 uv3,
    float channel,
    float invert)
{
    int index = (int)round(channel);
    float2 selected = uv0;
    if (index == 1)
        selected = uv1;
    else if (index == 3)
        selected = uv3;
    float value = selected.y;
    if (!(value == value) || abs(value) >= 1e19)
        value = uv0.y;
    value = saturate(value);
    return step(0.5, invert) > 0.5 ? 1.0 - value : value;
}

// Tutorial 3.10: two-segment empirical outline width by camera distance.
// Near 0..1 m grows from 0.13 toward 0.3, far 1..10 m grows toward 1.5.
float Wuwa_TutorialOutlineWidth(
    float3 positionWS,
    float3 cameraPos)
{
    float3 nearfar = float3(0.005, 1, 10);
    float3 weight = float3(0.13, 0.3, 1.5);
    float dis = distance(cameraPos, positionWS);
    if (!(dis == dis) || abs(dis) >= 1e19 || dis < 0.0)
        return 1.0;
    float disNear = saturate(
        (dis - nearfar.x) / max(nearfar.y - nearfar.x, 1e-5));
    float disFar = saturate(
        (dis - nearfar.y) / max(nearfar.z - nearfar.y, 1e-5));
    return lerp(weight.x, weight.y, disNear) +
        (weight.z - weight.y) * disFar;
}


float3 Wuwa_IDOutlineColor(float4 idMap, float3 outlineColor)
{
    float regionScale = lerp(0.82, 1.0, saturate(idMap.r));
    float3 coolShift = lerp(1.0.xxx, float3(0.82, 0.88, 1.0), saturate(idMap.g) * 0.18);
    return max(outlineColor * regionScale * coolShift, 0.0.xxx);
}

#endif
