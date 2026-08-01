// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
#ifndef MIKU_GENSHIN_COMMON_INCLUDED
#define MIKU_GENSHIN_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "../NPR/NPR_FaceSDF.hlsl"

float3 Genshin_SampleSH_Indirect(float3 normalWS)
{
    return SampleSH(normalize(normalWS));
}

// The source Blender preset grades both the base texture and the ramp before
// they are multiplied together.  The exported MIKU curve has one master-curve
// control point, so a compact piecewise reconstruction is substantially closer
// than adding URP ambient light on top of the ungraded texture.
float3 Genshin_ReferenceCurve(float3 color, float midX, float midY)
{
    color = saturate(color);
    float3 lower = color * (midY / max(midX, 1e-5));
    float3 upper = midY + (color - midX) * ((1.0 - midY) / max(1.0 - midX, 1e-5));
    return saturate(lerp(lower, upper, step(midX.xxx, color)));
}

float3 Genshin_ReferenceBaseGrade(float3 color)
{
    // RGB Curves master point (0.6333339, 0.3691861), followed by HSV Value=2.
    return saturate(Genshin_ReferenceCurve(color, 0.6333339, 0.3691861) * 2.0);
}

float3 Genshin_ReferenceRampGrade(float3 color)
{
    // Ramp curve master point exported by the reference material.
    return Genshin_ReferenceCurve(color, 0.4468749, 0.3437501);
}

float3 Genshin_ReferenceSkinTone(float3 color)
{
    // Blender's display transform compresses the very bright diffuse skin
    // texture while lifting its warm shadow band. Reconstruct that response in
    // shader space so Unity does not produce clipped white highlights and dull
    // grey-brown shadows.
    return saturate(float3(0.765, 0.258, 0.106) + saturate(color) * float3(0.140, 0.613, 0.765));
}

float Genshin_ReferenceRampRow(float materialId, float inNight)
{
    // The Blender group selects one of five discrete 20px ramp rows from the
    // LightMap alpha/material id.  Nearest ranges are more stable than exact
    // float comparisons after PNG import.
    float row = materialId < 0.15 ? 0.95 :
                materialId < 0.40 ? 0.65 :
                materialId < 0.60 ? 0.75 :
                materialId < 0.85 ? 0.55 : 0.85;
    return saturate(row - 0.5 * saturate(inNight));
}

float Genshin_ReferenceLightingSignal(float ndotLRaw, float lightMapGreen, float shadowAttenuation)
{
    // Faithful reconstruction of the exported nodes:
    // SUN scale 2 -> MapRange(-1..1) -> MultiplyAdd(0.5, 0.5) -> Power(2),
    // then the binary LightMap.G detail mask.
    float halfLambert = saturate(ndotLRaw + 0.5);
    halfLambert = pow(saturate(halfLambert * 0.5 + 0.5), 2.0);
    float detailMask = lerp(0.5, 1.0, step(0.42, lightMapGreen));
    return saturate(halfLambert * detailMask * shadowAttenuation);
}

float Genshin_DayNightOffset(float inNight)
{
    return -0.5 * saturate(inNight);
}

float Genshin_AdjustedHalfSampler(float lambertRampAO, float4 vertexColor)
{
    float halfSampler = saturate(lambertRampAO * 0.5 + 0.5);
    float rampOffset = step(0.5, vertexColor.g) == 1.0 ? vertexColor.g : vertexColor.g - 1.0;
    return saturate(halfSampler + rampOffset);
}

float3 Genshin_BodyDiffuse(
    float3 baseColor,
    float4 lightMap,
    float4 vertexColor,
    float3 normalWS,
    float3 lightDirWS,
    float3 mainLightColor,
    float shadowAttenuation,
    float bodyShadowSmooth,
    float inNight,
    TEXTURE2D_PARAM(shadowRampMap, sampler_shadowRampMap))
{
    float ndotLRaw = dot(normalize(normalWS), normalize(lightDirWS));
    float lightingSignal = Genshin_ReferenceLightingSignal(ndotLRaw, lightMap.g, shadowAttenuation);
    float rampU = lerp(0.01, 0.998, lightingSignal);
    float rampV = Genshin_ReferenceRampRow(lightMap.a, inNight);
    float3 rampShadow = Genshin_ReferenceRampGrade(
        SAMPLE_TEXTURE2D(shadowRampMap, sampler_shadowRampMap, float2(rampU, rampV)).rgb);
    float transition = max(0.001, bodyShadowSmooth * 0.02);
    float inLight = smoothstep(0.998 - transition, 0.998, lightingSignal);
    return Genshin_ReferenceBaseGrade(baseColor) * lerp(rampShadow, mainLightColor, inLight);
}

float3 Genshin_HairDoubleShadow(
    float3 baseColor,
    float4 lightMap,
    float4 vertexColor,
    float ndotLRaw,
    float3 mainLightColor,
    float shadowAttenuation,
    float inNight,
    float hairDarkShadowSmooth,
    float hairDarkShadowArea,
    float hairShadowSmooth,
    float hairSmoothShadowIntensity,
    TEXTURE2D_PARAM(hairRampMap, sampler_hairRampMap))
{
    float lightingSignal = Genshin_ReferenceLightingSignal(ndotLRaw, lightMap.g, shadowAttenuation);
    float rampU = lerp(0.01, 0.998, lightingSignal);
    float rampV = Genshin_ReferenceRampRow(lightMap.a, inNight);
    float3 rampShadow = Genshin_ReferenceRampGrade(
        SAMPLE_TEXTURE2D(hairRampMap, sampler_hairRampMap, float2(rampU, rampV)).rgb);
    // Hair in the source material has a second, deliberately placed dark
    // band.  Keeping this separate from the main terminator avoids the soft
    // cylindrical/PBR look while still taking its colour from the exported
    // hair ramp instead of inventing a black shadow.
    float deepShadowStart = min(hairDarkShadowSmooth, hairDarkShadowArea);
    float deepShadowEnd = max(hairDarkShadowSmooth, hairDarkShadowArea);
    float deepShadowBand = 1.0 - smoothstep(deepShadowStart, deepShadowEnd, ndotLRaw);
    float3 deepRampShadow = Genshin_ReferenceRampGrade(
        SAMPLE_TEXTURE2D(hairRampMap, sampler_hairRampMap, float2(0.01, rampV)).rgb);
    rampShadow = lerp(
        rampShadow,
        deepRampShadow,
        saturate(deepShadowBand * hairSmoothShadowIntensity));
    float transition = max(0.001, hairShadowSmooth * 0.02);
    float inLight = smoothstep(0.998 - transition, 0.998, lightingSignal);
    return Genshin_ReferenceBaseGrade(baseColor) * lerp(rampShadow, mainLightColor, inLight);
}

float Genshin_RoughnessToSpecularExponent(float roughness)
{
    return max(1.0, sqrt(2.0 / max(roughness + 2.0, 1e-5)) * 128.0);
}

float3 Genshin_ComputeSpecular(
    float3 baseColor,
    float4 lightMap,
    float4 metalMap,
    float3 normalWS,
    float3 viewDirWS,
    float3 lightDirWS,
    float strokeRange,
    float patternRange,
    float metalIntensity)
{
    float3 halfDir = normalize(viewDirWS + lightDirWS);
    float NoH = saturate(dot(normalWS, halfDir));

    // The authored LightMap is authoritative: R is specular intensity and the
    // metal-region mask, while B shifts the graphic specular threshold.  The
    // separate view-space metal map only supplies a subtle sphere-map tint.
    float lobeExponent = lerp(6.0, 14.0, saturate(strokeRange));
    float graphicLobe = pow(NoH, lobeExponent);
    float specularIntensity = saturate(lightMap.r);
    float metalMask = smoothstep(0.89, 0.93, specularIntensity);
    float specularThreshold = saturate(1.015 - lightMap.b);
    float thresholdFeather = lerp(0.035, 0.10, saturate(patternRange));
    float graphicBlob = smoothstep(
        specularThreshold - thresholdFeather,
        specularThreshold + thresholdFeather,
        graphicLobe);
    float highlightMask = metalMask * specularIntensity * graphicBlob;

    float3 gradedBase = Genshin_ReferenceBaseGrade(baseColor);
    float sphereTint = lerp(0.85, 1.0, dot(saturate(metalMap.rgb), float3(0.299, 0.587, 0.114)));
    float3 highlightTint = lerp(gradedBase, 1.0.xxx, 0.45) * sphereTint;
    float3 rawHighlight = highlightTint * highlightMask * (0.50 * max(0.0, metalIntensity));

    // Reserve headroom for the authored diffuse/ramp color.  This keeps metal
    // readable without letting the additive lobe erase texture detail.
    float3 highlightHeadroom = max(0.0.xxx, 0.98.xxx - gradedBase);
    return min(rawHighlight, highlightHeadroom * 0.90);
}

float3 Genshin_HairSpecular(
    float3 baseColor,
    float4 lightMap,
    float4 metalMap,
    float3 normalWS,
    float3 viewDirWS,
    float3 lightDirWS,
    float ndotLRaw,
    float hairRange,
    float hairViewSpecularThreshold,
    float hairSpecAreaBaseline,
    float hairAccGroveBaseline,
    float hairViewSpecularIntensity)
{
    // Hair accessories share the same ILM/metal-map contract as body metal.
    // Reuse the energy-limited response so bright ornaments do not clip.
    return Genshin_ComputeSpecular(
        baseColor,
        lightMap,
        metalMap,
        normalWS,
        viewDirWS,
        lightDirWS,
        0.35,
        0.70,
        hairViewSpecularIntensity);
}

float3 Genshin_HairViewHighlight(float3 baseColor, float4 lightMap, float hairSpecMask, float intensity)
{
    // The reference graph samples hair_s with the camera-space normal rather
    // than the mesh UV. This gives a broad graphic highlight without turning
    // the hair into a glossy PBR surface.
    float mask = smoothstep(0.18, 0.82, saturate(hairSpecMask));
    float lightMask = lerp(0.45, 1.0, saturate(lightMap.b));
    return Genshin_ReferenceBaseGrade(baseColor) * mask * lightMask * intensity;
}

float3 Genshin_EmissionPulse(float baseAlpha, float lightMapAlpha, float3 baseColor, float3 mainLightColor, float emissionIntensity)
{
    // The reference graph has no time-driven pulse.  Explicit Emission textures
    // are still applied by the material shader when the keyword is enabled.
    return 0.0.xxx;
}

float Genshin_FaceSDFShadow(
    float2 uv,
    TEXTURE2D_PARAM(faceSDFMap, sampler_faceSDFMap),
    float3 lightDirWS,
    float3 headForwardWS,
    float3 headRightWS,
    float3 headUpWS,
    float faceShadowOffset,
    float faceShadowSoftness,
    float faceSdfFlipY)
{
    // Keep the same surface-to-light convention as the body shader.  The SDF
    // threshold below is inverted instead; negating the full vector reverses
    // the left/right shadow motion while appearing correct only for frontal
    // lighting.
    lightDirWS = normalize(lightDirWS);
    headForwardWS = normalize(headForwardWS);
    headRightWS = normalize(headRightWS);
    headUpWS = normalize(headUpWS);

    // FBX axis conversion commonly leaves the renderer rotated -90 degrees on
    // X.  In that case local +Z is world-up and local +Y is the forward axis.
    // Recover an upright face basis while keeping the documented basis for
    // already-baked meshes.
    float3 worldUp = float3(0.0, 1.0, 0.0);
    if (abs(dot(headForwardWS, worldUp)) > 0.75 && abs(dot(headUpWS, worldUp)) < 0.75)
    {
        headUpWS = headForwardWS * (dot(headForwardWS, worldUp) < 0.0 ? -1.0 : 1.0);
        headForwardWS = normalize(cross(headRightWS, headUpWS));
    }

    float3 projectedLightWS = lightDirWS - dot(lightDirWS, headUpWS) * headUpWS;
    float3 fixedLightDirectionWS = dot(projectedLightWS, projectedLightWS) > 1e-5 ? normalize(projectedLightWS) : headForwardWS;
    float FDotL = dot(headForwardWS, fixedLightDirectionWS);
    float RDotL = dot(headRightWS, fixedLightDirectionWS);
    // Both the original Genshin reconstruction and the HSR reference select
    // the mirrored half when the surface-to-light vector points along the
    // character's +Right axis.  Keeping this test in the same convention as
    // body NdotL prevents the facial shadow from travelling against the body.
    float2 faceUV = RDotL > 0.0 ? float2(1.0 - uv.x, uv.y) : uv;
    faceUV.y = lerp(faceUV.y, 1.0 - faceUV.y, saturate(faceSdfFlipY));
    float shadowMargin = SAMPLE_TEXTURE2D(faceSDFMap, sampler_faceSDFMap, faceUV).r;
    float threshold = 1.0 - (0.5 * FDotL + 0.5) + faceShadowOffset;
    return smoothstep(threshold - faceShadowSoftness, threshold + faceShadowSoftness, shadowMargin);
}

float3 Genshin_FaceDiffuse(
    float3 baseColor,
    float inLight,
    float3 mainLightColor,
    float inNight,
    TEXTURE2D_PARAM(shadowRampMap, sampler_shadowRampMap))
{
    float rampU = lerp(0.01, 0.998, saturate(inLight));
    float rampV = Genshin_ReferenceRampRow(1.0, inNight);
    float3 faceShadowColor = Genshin_ReferenceRampGrade(
        SAMPLE_TEXTURE2D(shadowRampMap, sampler_shadowRampMap, float2(rampU, rampV)).rgb);
    float3 faceColor = Genshin_ReferenceBaseGrade(baseColor) * lerp(faceShadowColor, mainLightColor, saturate(inLight));
    return Genshin_ReferenceSkinTone(faceColor);
}

float3 Genshin_DepthRimLight(
    float4 positionCS,
    float3 normalWS,
    float3 viewDirWS,
    float3 mainLightColor,
    float3 rimLightTintColor,
    float rimLightBrightness,
    float rimLightWidth,
    float rimLightThreshold,
    float rimLightFadeout,
    float fresnelPower,
    float fresnelClamp)
{
    float linearEyeDepth = LinearEyeDepth(positionCS.z, _ZBufferParams);
    float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalize(normalWS));
    float2 uvOffset = float2(sign(normalVS.x), 0.0) * rimLightWidth / (1.0 + linearEyeDepth) / 100.0;
    int2 loadTexPos = int2(positionCS.xy + uvOffset * _ScaledScreenParams.xy);
    int2 maxTexPos = max(int2(_ScaledScreenParams.xy) - 1, int2(0, 0));
    loadTexPos = min(max(loadTexPos, int2(0, 0)), maxTexPos);
    float offsetSceneDepth = LoadSceneDepth(loadTexPos);
    float offsetLinearEyeDepth = LinearEyeDepth(offsetSceneDepth, _ZBufferParams);
    float rimLight = saturate((offsetLinearEyeDepth - (linearEyeDepth + rimLightThreshold)) / max(rimLightFadeout, 1e-5));
    float NoV = dot(normalize(normalWS), normalize(viewDirWS));
    float fresnel = pow(1.0 - saturate(NoV), fresnelPower);
    fresnel = fresnel * saturate(fresnelClamp) + (1.0 - saturate(fresnelClamp));
    return rimLight * fresnel * rimLightTintColor * mainLightColor * rimLightBrightness;
}

float3 Genshin_GetOutlineNormalOS(float3 smoothNormalOS, float3 fallbackNormalOS)
{
    float3 selectedNormalOS = dot(smoothNormalOS, smoothNormalOS) > 1e-5 ? smoothNormalOS : fallbackNormalOS;
    return normalize(selectedNormalOS);
}

float Genshin_DistanceCompensatedOutlineWidth(float3 positionWS, float outlineWidth, float referenceDistance, float distanceScale)
{
    float cameraDistance = distance(_WorldSpaceCameraPos, positionWS);
    float farScale = max(cameraDistance / max(referenceDistance, 1e-5), 1.0);
    return outlineWidth * lerp(1.0, farScale, saturate(distanceScale));
}

float3 Genshin_OutlineColor(float3 baseColor, float4 vertexColor, float outlineGamma, float3 outlineTint)
{
    float3 vertexTint = lerp(1.0.xxx, saturate(vertexColor.rgb), step(0.001, dot(vertexColor.rgb, vertexColor.rgb)));
    return pow(max(baseColor * vertexTint, 0.0.xxx), max(outlineGamma, 1e-5)) * outlineTint;
}

#endif
