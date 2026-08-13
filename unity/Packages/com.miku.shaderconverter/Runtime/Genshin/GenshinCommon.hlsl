// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
#ifndef MIKU_GENSHIN_COMMON_INCLUDED
#define MIKU_GENSHIN_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
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

float3 Genshin_ReferenceBaseGrade(float3 color, float highlightCompression)
{
    // RGB Curves master point (0.6333339, 0.3691861), followed by HSV Value=2.
    float3 unboundedGrade = Genshin_ReferenceCurve(color, 0.6333339, 0.3691861) * 2.0;
    return lerp(saturate(unboundedGrade), unboundedGrade, saturate(highlightCompression));
}

float3 Genshin_ReferenceBaseGrade(float3 color)
{
    return Genshin_ReferenceBaseGrade(color, 0.0);
}

float3 Genshin_ReferenceRampGrade(float3 color)
{
    // Ramp curve master point exported by the reference material.
    return Genshin_ReferenceCurve(color, 0.4468749, 0.3437501);
}

float3 Genshin_ReferenceSkinTone(float3 color, float highlightCompression)
{
    // Blender's display transform compresses the very bright diffuse skin
    // texture while lifting its warm shadow band. Reconstruct that response in
    // shader space so Unity does not produce clipped white highlights and dull
    // grey-brown shadows.
    float3 legacyTone = saturate(float3(0.765, 0.258, 0.106) + saturate(color) * float3(0.140, 0.613, 0.765));
    return lerp(legacyTone, color, saturate(highlightCompression));
}

float3 Genshin_ReferenceSkinTone(float3 color)
{
    return Genshin_ReferenceSkinTone(color, 0.0);
}

float3 Genshin_HuePreservingSoftShoulder(float3 color, float knee, float ceiling)
{
    float safeKnee = max(0.0, knee);
    float safeCeiling = max(safeKnee + 1e-4, ceiling);
    float peak = max(color.r, max(color.g, color.b));
    if (peak <= safeKnee)
    {
        return color;
    }

    float shoulderRange = safeCeiling - safeKnee;
    float compressedPeak = safeKnee + shoulderRange * (1.0 - exp(-(peak - safeKnee) / shoulderRange));
    return color * (compressedPeak / max(peak, 1e-5));
}

float3 Genshin_CompressNonEmissive(float3 color, float compression, float knee, float ceiling)
{
    return lerp(color, Genshin_HuePreservingSoftShoulder(color, knee, ceiling), saturate(compression));
}

struct Genshin_RampRowParams
{
    float a0;
    float a1;
    float a2;
    float a3;
    float a4;
};

// Tutorial contract: five LightMap-alpha material ids (0.0/0.3/0.5/0.7/1.0)
// map to artist-adjustable ramp rows through `a * -0.1 + 1.05`, and 0.5 is
// subtracted when _InNight is enabled. The default rows are the tutorial's
// 1, 4, 3, 5, 2 mapping.
float Genshin_TutorialRampRow(
    float materialId,
    Genshin_RampRowParams rows,
    float inNight)
{
    float rampSampling = saturate(inNight) * 0.5;
    float ramp0 = rows.a0 * -0.1 + 1.05 - rampSampling;
    float ramp1 = rows.a1 * -0.1 + 1.05 - rampSampling;
    float ramp2 = rows.a2 * -0.1 + 1.05 - rampSampling;
    float ramp3 = rows.a3 * -0.1 + 1.05 - rampSampling;
    float ramp4 = rows.a4 * -0.1 + 1.05 - rampSampling;
    float lightmapA2 = step(0.25, materialId);
    float lightmapA3 = step(0.45, materialId);
    float lightmapA4 = step(0.65, materialId);
    float lightmapA5 = step(0.95, materialId);
    float rampV = ramp0;
    rampV = lerp(rampV, ramp1, lightmapA2);
    rampV = lerp(rampV, ramp2, lightmapA3);
    rampV = lerp(rampV, ramp3, lightmapA4);
    rampV = lerp(rampV, ramp4, lightmapA5);
    return saturate(rampV);
}

float Genshin_ReferenceRampRow(float materialId, float inNight)
{
    Genshin_RampRowParams defaultRows;
    defaultRows.a0 = 1.0;
    defaultRows.a1 = 4.0;
    defaultRows.a2 = 3.0;
    defaultRows.a3 = 5.0;
    defaultRows.a4 = 2.0;
    return Genshin_TutorialRampRow(materialId, defaultRows, inNight);
}

float Genshin_TutorialAO(float lightMapGreen)
{
    return smoothstep(0.2, 0.3, saturate(lightMapGreen));
}

float Genshin_TutorialLightingSignal(
    float ndotLRaw,
    float lightMapGreen,
    float darkOffset,
    float greyWidth)
{
    float halfLambert = smoothstep(
        0.0,
        max(greyWidth, 1e-4),
        ndotLRaw + darkOffset);
    return saturate(
        halfLambert *
        Genshin_TutorialAO(lightMapGreen));
}

float Genshin_ReferenceLightingSignal(
    float ndotLRaw,
    float lightMapGreen)
{
    return Genshin_TutorialLightingSignal(
        ndotLRaw,
        lightMapGreen,
        0.5,
        1.14);
}

float Genshin_MainShadowVisibility(
    float shadowAttenuation,
    float distanceAttenuation,
    float mainShadowInfluence)
{
    return saturate(distanceAttenuation) * lerp(
        1.0,
        saturate(shadowAttenuation),
        saturate(mainShadowInfluence));
}

float3 Genshin_ApplyMainShadow(
    float3 toonColor,
    float3 darkestRampColor,
    float shadowAttenuation,
    float mainShadowInfluence)
{
    float shadowWeight =
        (1.0 - saturate(shadowAttenuation)) *
        saturate(mainShadowInfluence);
    return lerp(toonColor, darkestRampColor, shadowWeight);
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
    float distanceAttenuation,
    float mainShadowInfluence,
    float darkOffset,
    float greyWidth,
    float bodyShadowSmooth,
    Genshin_RampRowParams rampRows,
    float inNight,
    float highlightCompression,
    TEXTURE2D_PARAM(shadowRampMap, sampler_shadowRampMap))
{
    float ndotLRaw = dot(normalize(normalWS), normalize(lightDirWS));
    float lightingSignal = Genshin_TutorialLightingSignal(
        ndotLRaw,
        lightMap.g,
        darkOffset,
        greyWidth);
    float rampU = lerp(0.01, 0.998, lightingSignal);
    float rampV = Genshin_TutorialRampRow(lightMap.a, rampRows, inNight);
    float3 rampShadow = Genshin_ReferenceRampGrade(
        SAMPLE_TEXTURE2D(shadowRampMap, sampler_shadowRampMap, float2(rampU, rampV)).rgb);
    float transition = max(0.001, bodyShadowSmooth);
    float inLight = smoothstep(0.998 - transition, 0.998, lightingSignal);
    float3 gradedBase = Genshin_ReferenceBaseGrade(baseColor, highlightCompression);
    float3 toonDiffuse = gradedBase * lerp(rampShadow, mainLightColor, inLight);
    float3 darkestDiffuse = gradedBase * Genshin_ReferenceRampGrade(
        SAMPLE_TEXTURE2D(shadowRampMap, sampler_shadowRampMap, float2(0.01, rampV)).rgb);
    return Genshin_ApplyMainShadow(
        toonDiffuse,
        darkestDiffuse,
        shadowAttenuation,
        mainShadowInfluence) * saturate(distanceAttenuation);
}

float3 Genshin_HairDoubleShadow(
    float3 baseColor,
    float4 lightMap,
    float4 vertexColor,
    float ndotLRaw,
    float3 mainLightColor,
    float shadowAttenuation,
    float distanceAttenuation,
    float mainShadowInfluence,
    float darkOffset,
    float greyWidth,
    float inNight,
    float hairDarkShadowSmooth,
    float hairDarkShadowArea,
    float hairShadowSmooth,
    float hairSmoothShadowIntensity,
    Genshin_RampRowParams rampRows,
    float highlightCompression,
    TEXTURE2D_PARAM(hairRampMap, sampler_hairRampMap))
{
    float lightingSignal = Genshin_TutorialLightingSignal(
        ndotLRaw,
        lightMap.g,
        darkOffset,
        greyWidth);
    float rampU = lerp(0.01, 0.998, lightingSignal);
    float rampV = Genshin_TutorialRampRow(lightMap.a, rampRows, inNight);
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
    float transition = max(0.001, hairShadowSmooth);
    float inLight = smoothstep(0.998 - transition, 0.998, lightingSignal);
    float3 gradedBase = Genshin_ReferenceBaseGrade(baseColor, highlightCompression);
    float3 toonDiffuse = gradedBase * lerp(rampShadow, mainLightColor, inLight);
    float3 darkestDiffuse = gradedBase * deepRampShadow;
    return Genshin_ApplyMainShadow(
        toonDiffuse,
        darkestDiffuse,
        shadowAttenuation,
        mainShadowInfluence) * saturate(distanceAttenuation);
}

float Genshin_RoughnessToSpecularExponent(float roughness)
{
    return max(1.0, sqrt(2.0 / max(roughness + 2.0, 1e-5)) * 128.0);
}

float Genshin_TutorialMetalMask(float lightMapRed)
{
    return 1.0 - step(lightMapRed, 0.9);
}

float3 Genshin_TutorialSpecular(
    float3 baseColor,
    float4 lightMap,
    float3 normalWS,
    float3 viewDirWS,
    float3 lightDirWS,
    float3 mainLightColor,
    float glossPower,
    float glossStrength,
    float brightMask,
    float lightVisibility)
{
    float3 halfDir = normalize(viewDirWS + lightDirWS);
    float ndotH = saturate(dot(normalize(normalWS), halfDir));
    return pow(ndotH, max(glossPower, 1.0)) *
        saturate(lightMap.r) *
        saturate(lightMap.b) *
        max(glossStrength, 0.0) *
        max(baseColor, 0.0.xxx) *
        saturate(brightMask) *
        saturate(lightVisibility) *
        max(mainLightColor, 0.0.xxx);
}

float3 Genshin_TutorialMetal(
    float3 baseColor,
    float4 lightMap,
    float metalSample,
    float3 metalMapColor,
    float metalIntensity)
{
    float mask = Genshin_TutorialMetalMask(lightMap.r);
    float3 matCap = lerp(
        max(metalMapColor, 0.0.xxx),
        max(baseColor, 0.0.xxx),
        saturate(metalSample));
    return matCap * mask * max(metalIntensity, 0.0);
}

float3 Genshin_TutorialFresnel(
    float3 baseColor,
    float3 normalWS,
    float3 viewDirWS,
    float fresnelPower,
    float fresnelStrength)
{
    float fresnel = pow(
        1.0 - saturate(dot(normalize(normalWS), normalize(viewDirWS))),
        max(fresnelPower, 0.1));
    return max(baseColor, 0.0.xxx) * fresnel * max(fresnelStrength, 0.0);
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
    float metalIntensity,
    float highlightCompression)
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

    float3 gradedBase = Genshin_ReferenceBaseGrade(baseColor, highlightCompression);
    float sphereTint = lerp(0.85, 1.0, dot(saturate(metalMap.rgb), float3(0.299, 0.587, 0.114)));
    float3 highlightTint = lerp(gradedBase, 1.0.xxx, 0.45) * sphereTint;
    float3 rawHighlight = highlightTint * highlightMask * (0.50 * max(0.0, metalIntensity));

    // Reserve headroom for the authored diffuse/ramp color.  This keeps metal
    // readable without letting the additive lobe erase texture detail.
    float3 highlightHeadroom = max(0.0.xxx, 0.98.xxx - gradedBase);
    float3 legacyHighlight = min(rawHighlight, highlightHeadroom * 0.90);
    return lerp(legacyHighlight, rawHighlight, saturate(highlightCompression));
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
    float hairViewSpecularIntensity,
    float highlightCompression)
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
        hairViewSpecularIntensity,
        highlightCompression);
}

float3 Genshin_HairViewHighlight(float3 baseColor, float4 lightMap, float hairSpecMask, float intensity, float highlightCompression)
{
    // The reference graph samples hair_s with the camera-space normal rather
    // than the mesh UV. This gives a broad graphic highlight without turning
    // the hair into a glossy PBR surface.
    float mask = smoothstep(0.18, 0.82, saturate(hairSpecMask));
    float lightMask = lerp(0.45, 1.0, saturate(lightMap.b));
    return Genshin_ReferenceBaseGrade(baseColor, highlightCompression) * mask * lightMask * intensity;
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
    Genshin_RampRowParams rampRows,
    float inNight,
    float highlightCompression,
    float shadowAttenuation,
    float distanceAttenuation,
    float mainShadowInfluence,
    TEXTURE2D_PARAM(shadowRampMap, sampler_shadowRampMap))
{
    float rampU = lerp(0.01, 0.998, saturate(inLight));
    float rampV = Genshin_TutorialRampRow(1.0, rampRows, inNight);
    float3 faceShadowColor = Genshin_ReferenceRampGrade(
        SAMPLE_TEXTURE2D(shadowRampMap, sampler_shadowRampMap, float2(rampU, rampV)).rgb);
    float3 gradedBase = Genshin_ReferenceBaseGrade(baseColor, highlightCompression);
    float3 faceColor = gradedBase * lerp(faceShadowColor, mainLightColor, saturate(inLight));
    float3 darkestFaceColor = gradedBase * Genshin_ReferenceRampGrade(
        SAMPLE_TEXTURE2D(shadowRampMap, sampler_shadowRampMap, float2(0.01, rampV)).rgb);
    faceColor = Genshin_ApplyMainShadow(
        faceColor,
        darkestFaceColor,
        shadowAttenuation,
        mainShadowInfluence) * saturate(distanceAttenuation);
    return Genshin_ReferenceSkinTone(faceColor, highlightCompression);
}

float3 Genshin_OutlineColor(float3 baseColor, float4 vertexColor, float outlineGamma, float3 outlineTint)
{
    float3 vertexTint = lerp(1.0.xxx, saturate(vertexColor.rgb), step(0.001, dot(vertexColor.rgb, vertexColor.rgb)));
    return pow(max(baseColor * vertexTint, 0.0.xxx), max(outlineGamma, 1e-5)) * outlineTint;
}

float Genshin_OutlineVertexMask(float4 vertexColor)
{
    // Miku_ToonMask_v1 reserves G for outline width. Local import tools may
    // explicitly swizzle a source asset's authored channel into G on a clone.
    return saturate(vertexColor.g);
}

float3 Genshin_OutlineRegionColor(
    float4 lightMap,
    float3 baseColor,
    float4 vertexColor,
    float3 outlineColor0,
    float3 outlineColor1,
    float3 outlineColor2,
    float3 outlineColor3,
    float3 outlineColor4,
    float outlineGamma,
    float3 outlineTint,
    float regionMode)
{
    if (regionMode <= 0.5)
    {
        return Genshin_OutlineColor(
            baseColor,
            vertexColor,
            outlineGamma,
            outlineTint);
    }

    // Tutorial contract: lightmap.a carries the five material ids and the
    // outline pass reassembles them into five authored outline colors.
    float lightmapA2 = step(0.25, lightMap.a);
    float lightmapA3 = step(0.45, lightMap.a);
    float lightmapA4 = step(0.65, lightMap.a);
    float lightmapA5 = step(0.95, lightMap.a);
    float3 regionColor = outlineColor0;
    regionColor = lerp(regionColor, outlineColor1, lightmapA2);
    regionColor = lerp(regionColor, outlineColor2, lightmapA3);
    regionColor = lerp(regionColor, outlineColor3, lightmapA4);
    regionColor = lerp(regionColor, outlineColor4, lightmapA5);
    return max(regionColor, 0.0.xxx);
}

float3 Genshin_DiffuseAlphaEmission(
    float baseAlpha,
    float3 baseColor,
    float4 glow,
    float flicker)
{
    // Tutorial contract: diffuse.a is an emission mask after a 0..1
    // smoothstep, and the glow flickers with sin(_Time.w * _flicker).
    float mask = smoothstep(0.0, 1.0, saturate(baseAlpha));
    float pulse = sin(_Time.w * max(flicker, 0.0)) * 0.5 + 0.5;
    return baseColor * (pulse * max(glow.rgb, 0.0.xxx)) * mask;
}

float Genshin_BaseAlphaCoverage(float baseAlpha)
{
    return smoothstep(0.05, 0.7, saturate(baseAlpha));
}

void Genshin_ApplyBaseAlphaCoverage(
    float baseAlpha,
    float alphaMode,
    float cutoff)
{
    if (alphaMode > 0.5 && alphaMode <= 1.5)
        clip(Genshin_BaseAlphaCoverage(baseAlpha) - cutoff);
}

float3 Genshin_DecodeNormalMap(float4 normalSample, float bumpScale)
{
    // Tutorial contract: UnpackNormal, scale xy by the bump strength, then
    // rebuild z so the tangent-space normal stays unit length.
    float3 normalTS = UnpackNormal(normalSample).rgb;
    normalTS.xy *= max(bumpScale, 0.0);
    normalTS.z = sqrt(1.0 - saturate(dot(normalTS.xy, normalTS.xy)));
    return normalize(normalTS);
}

float3x3 Genshin_DerivativeTangentFrame(
    float3 positionWS,
    float3 geometricNormalWS,
    float2 uv)
{
    float3 dpdx = ddx(positionWS);
    float3 dpdy = ddy(positionWS);
    float2 duvdx = ddx(uv);
    float2 duvdy = ddy(uv);
    float determinant = duvdx.x * duvdy.y - duvdx.y * duvdy.x;
    float3 normalWS = normalize(geometricNormalWS);
    if (abs(determinant) < 1e-8)
    {
        float3 axis = abs(normalWS.y) < 0.999
            ? float3(0.0, 1.0, 0.0)
            : float3(1.0, 0.0, 0.0);
        float3 fallbackTangent = normalize(cross(axis, normalWS));
        return float3x3(
            fallbackTangent,
            normalize(cross(normalWS, fallbackTangent)),
            normalWS);
    }
    float inverseDeterminant = rcp(determinant);
    float3 tangentWS =
        (dpdx * duvdy.y - dpdy * duvdx.y) * inverseDeterminant;
    tangentWS = tangentWS - normalWS * dot(normalWS, tangentWS);
    if (dot(tangentWS, tangentWS) < 1e-8)
    {
        float3 axis = abs(normalWS.y) < 0.999
            ? float3(0.0, 1.0, 0.0)
            : float3(1.0, 0.0, 0.0);
        tangentWS = cross(axis, normalWS);
    }
    tangentWS = normalize(tangentWS);
    float handedness = determinant < 0.0 ? -1.0 : 1.0;
    float3 bitangentWS = normalize(cross(normalWS, tangentWS)) * handedness;
    return float3x3(tangentWS, bitangentWS, normalWS);
}

void Genshin_DiffuseAlphaClip(float baseAlpha, float cutoff)
{
    // Tutorial contract: diffuse.a cutout removes noise with a 0.05..0.7
    // smoothstep before clip(diffuseA - _Cutoff).
    clip(Genshin_BaseAlphaCoverage(baseAlpha) - cutoff);
}

#endif
