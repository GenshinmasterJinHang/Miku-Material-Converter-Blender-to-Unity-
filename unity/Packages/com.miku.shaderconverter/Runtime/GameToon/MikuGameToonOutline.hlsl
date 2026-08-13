// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
#ifndef MIKU_GAME_TOON_OUTLINE_INCLUDED
#define MIKU_GAME_TOON_OUTLINE_INCLUDED

#define MIKU_GAME_TOON_OUTLINE_EPSILON 1e-6
#define MIKU_GAME_TOON_OUTLINE_V2_MARKER 2.0
#define MIKU_GAME_TOON_OUTLINE_V2_MARKER_TOLERANCE 1e-3
#define MIKU_GAME_TOON_OUTLINE_MIN_DISTANCE_MULTIPLIER 0.25
#define MIKU_GAME_TOON_OUTLINE_MAX_DISTANCE_MULTIPLIER 4.0
#define MIKU_GAME_TOON_OUTLINE_FLOAT_EXPONENT_MASK 0x7f800000u

bool MikuGameToonOutlineFinite1(float value)
{
    return (asuint(value) & MIKU_GAME_TOON_OUTLINE_FLOAT_EXPONENT_MASK) !=
        MIKU_GAME_TOON_OUTLINE_FLOAT_EXPONENT_MASK &&
        abs(value) < 1e19;
}

bool MikuGameToonOutlineFinite2(float2 value)
{
    return all(
        (asuint(value) & MIKU_GAME_TOON_OUTLINE_FLOAT_EXPONENT_MASK) !=
        MIKU_GAME_TOON_OUTLINE_FLOAT_EXPONENT_MASK) &&
        all(abs(value) < 1e19);
}

bool MikuGameToonOutlineFinite3(float3 value)
{
    return all(
        (asuint(value) & MIKU_GAME_TOON_OUTLINE_FLOAT_EXPONENT_MASK) !=
        MIKU_GAME_TOON_OUTLINE_FLOAT_EXPONENT_MASK) &&
        all(abs(value) < 1e19);
}

bool MikuGameToonOutlineFinite4(float4 value)
{
    return all(
        (asuint(value) & MIKU_GAME_TOON_OUTLINE_FLOAT_EXPONENT_MASK) !=
        MIKU_GAME_TOON_OUTLINE_FLOAT_EXPONENT_MASK) &&
        all(abs(value) < 1e19);
}

float2 MikuGameToonOutlineSafeNormalize2(
    float2 value,
    float2 fallback)
{
    if (!MikuGameToonOutlineFinite2(value))
        return fallback;
    float componentScale = max(abs(value.x), abs(value.y));
    if (componentScale <= MIKU_GAME_TOON_OUTLINE_EPSILON)
        return fallback;
    value /= componentScale;
    float lengthSquared = dot(value, value);
    if (!MikuGameToonOutlineFinite1(lengthSquared) ||
        lengthSquared <=
        MIKU_GAME_TOON_OUTLINE_EPSILON *
        MIKU_GAME_TOON_OUTLINE_EPSILON)
        return fallback;
    return value * rsqrt(lengthSquared);
}

float3 MikuGameToonOutlineSafeNormalize(
    float3 value,
    float3 fallback)
{
    if (!MikuGameToonOutlineFinite3(value))
        return fallback;
    float componentScale = max(
        max(abs(value.x), abs(value.y)),
        abs(value.z));
    if (componentScale <= MIKU_GAME_TOON_OUTLINE_EPSILON)
        return fallback;
    value /= componentScale;
    float lengthSquared = dot(value, value);
    if (!MikuGameToonOutlineFinite1(lengthSquared) ||
        lengthSquared <=
        MIKU_GAME_TOON_OUTLINE_EPSILON *
        MIKU_GAME_TOON_OUTLINE_EPSILON)
        return fallback;
    return value * rsqrt(lengthSquared);
}

// UV7 TangentSpaceV2 is float4(normalTS.xyz, 2.0). Unmarked UV7 remains the
// legacy object-space contract. Invalid, zero, or opposite-hemisphere data
// falls back to the current geometric normal.
float3 MikuGameToonOutlineNormalTangentSpaceV2(
    float4 smoothNormalData,
    float3 normalOS,
    float4 tangentOS)
{
    float3 sourceNormalOS = MikuGameToonOutlineSafeNormalize(
        normalOS,
        float3(0.0, 0.0, 1.0));
    if (!MikuGameToonOutlineFinite4(smoothNormalData))
        return sourceNormalOS;

    bool isTangentSpaceV2 = abs(
        smoothNormalData.w - MIKU_GAME_TOON_OUTLINE_V2_MARKER) <=
        MIKU_GAME_TOON_OUTLINE_V2_MARKER_TOLERANCE;
    if (!isTangentSpaceV2)
    {
        float3 legacyNormalOS = MikuGameToonOutlineSafeNormalize(
            smoothNormalData.xyz,
            float3(0.0, 0.0, 0.0));
        if (dot(legacyNormalOS, legacyNormalOS) <=
            MIKU_GAME_TOON_OUTLINE_EPSILON *
            MIKU_GAME_TOON_OUTLINE_EPSILON ||
            dot(legacyNormalOS, sourceNormalOS) < 0.0)
            return sourceNormalOS;
        return legacyNormalOS;
    }

    if (smoothNormalData.z < 0.0 ||
        !MikuGameToonOutlineFinite4(tangentOS) ||
        abs(tangentOS.w) <= MIKU_GAME_TOON_OUTLINE_EPSILON)
        return sourceNormalOS;
    float3 tangentAxisOS = tangentOS.xyz -
        sourceNormalOS * dot(sourceNormalOS, tangentOS.xyz);
    tangentAxisOS = MikuGameToonOutlineSafeNormalize(
        tangentAxisOS,
        float3(0.0, 0.0, 0.0));
    if (dot(tangentAxisOS, tangentAxisOS) <=
        MIKU_GAME_TOON_OUTLINE_EPSILON *
        MIKU_GAME_TOON_OUTLINE_EPSILON)
        return sourceNormalOS;

    float3 bitangentAxisOS = MikuGameToonOutlineSafeNormalize(
        cross(sourceNormalOS, tangentAxisOS) *
            (tangentOS.w < 0.0 ? -1.0 : 1.0),
        float3(0.0, 0.0, 0.0));
    if (dot(bitangentAxisOS, bitangentAxisOS) <=
        MIKU_GAME_TOON_OUTLINE_EPSILON *
        MIKU_GAME_TOON_OUTLINE_EPSILON)
        return sourceNormalOS;

    float3 smoothNormalTS = MikuGameToonOutlineSafeNormalize(
        smoothNormalData.xyz,
        float3(0.0, 0.0, 0.0));
    if (dot(smoothNormalTS, smoothNormalTS) <=
        MIKU_GAME_TOON_OUTLINE_EPSILON *
        MIKU_GAME_TOON_OUTLINE_EPSILON ||
        smoothNormalTS.z < 0.0)
        return sourceNormalOS;
    float3 decodedNormalOS =
        tangentAxisOS * smoothNormalTS.x +
        bitangentAxisOS * smoothNormalTS.y +
        sourceNormalOS * smoothNormalTS.z;
    decodedNormalOS = MikuGameToonOutlineSafeNormalize(
        decodedNormalOS,
        sourceNormalOS);
    return dot(decodedNormalOS, sourceNormalOS) < 0.0
        ? sourceNormalOS
        : decodedNormalOS;
}

float MikuGameToonOutlineVertexMask(float4 vertexColor)
{
    // Miku_ToonMask_v1: R=SSS, G=outline width, B=screen rim,
    // A=face correction.
    return saturate(vertexColor.g);
}

float MikuGameToonOutlineCoverageWithDistanceMultiplier(
    float outlineEnabled,
    float outlineWidth,
    float distanceMultiplier,
    float vertexMask,
    float additionalWidthMask)
{
    if (!MikuGameToonOutlineFinite1(outlineEnabled) ||
        !MikuGameToonOutlineFinite1(outlineWidth) ||
        !MikuGameToonOutlineFinite1(distanceMultiplier) ||
        !MikuGameToonOutlineFinite1(vertexMask) ||
        !MikuGameToonOutlineFinite1(additionalWidthMask))
        return 0.0;
    float coverage = saturate(outlineEnabled) *
        max(outlineWidth, 0.0) *
        max(distanceMultiplier, 0.0) *
        saturate(vertexMask) *
        saturate(additionalWidthMask);
    return MikuGameToonOutlineFinite1(coverage) ? coverage : 0.0;
}

void MikuGameToonOutlineClipCoverage(float coverage)
{
    clip(MikuGameToonOutlineFinite1(coverage) &&
        coverage > MIKU_GAME_TOON_OUTLINE_EPSILON
        ? 1.0
        : -1.0);
}

float MikuGameToonOutlineDistanceMultiplier(
    float3 positionWS,
    float referenceDistance,
    float distanceScale,
    float legacyConstantResponse)
{
    if (!MikuGameToonOutlineFinite3(positionWS) ||
        !MikuGameToonOutlineFinite1(referenceDistance) ||
        !MikuGameToonOutlineFinite1(distanceScale))
        return 0.0;
    if (referenceDistance <= MIKU_GAME_TOON_OUTLINE_EPSILON)
        return 1.0;
    float cameraDistance = distance(_WorldSpaceCameraPos, positionWS);
    if (!MikuGameToonOutlineFinite1(cameraDistance))
        return 0.0;
    float legacyDistanceResponse = clamp(
        max(
            referenceDistance / max(cameraDistance, 1e-5),
            1.0),
        1.0,
        MIKU_GAME_TOON_OUTLINE_MAX_DISTANCE_MULTIPLIER);
    // HSR's historical full-distance-compensation path was already constant
    // in screen pixels at both near and far distances. Other families grew at
    // close range and became constant beyond the reference distance.
    legacyDistanceResponse = lerp(
        legacyDistanceResponse,
        1.0,
        saturate(legacyConstantResponse));
    return clamp(
        lerp(
            1.0,
            legacyDistanceResponse,
            saturate(distanceScale)),
        MIKU_GAME_TOON_OUTLINE_MIN_DISTANCE_MULTIPLIER,
        MIKU_GAME_TOON_OUTLINE_MAX_DISTANCE_MULTIPLIER);
}

float MikuGameToonOutlineScreenHeightWidth(
    float3 positionWS,
    float outlineWidth,
    float referenceDistance,
    float distanceScale,
    float4 vertexColor,
    float additionalWidthMask,
    float legacyConstantResponse)
{
    return MikuGameToonOutlineCoverageWithDistanceMultiplier(
        1.0,
        outlineWidth,
        MikuGameToonOutlineDistanceMultiplier(
            positionWS,
            referenceDistance,
            distanceScale,
            legacyConstantResponse),
        MikuGameToonOutlineVertexMask(vertexColor),
        additionalWidthMask);
}

float MikuGameToonOutlineCoverageWithLegacyMode(
    float3 positionWS,
    float outlineEnabled,
    float outlineWidth,
    float referenceDistance,
    float distanceScale,
    float4 vertexColor,
    float additionalWidthMask,
    float legacyConstantResponse)
{
    return MikuGameToonOutlineCoverageWithDistanceMultiplier(
        outlineEnabled,
        outlineWidth,
        MikuGameToonOutlineDistanceMultiplier(
            positionWS,
            referenceDistance,
            distanceScale,
            legacyConstantResponse),
        MikuGameToonOutlineVertexMask(vertexColor),
        additionalWidthMask);
}

float MikuGameToonOutlineCoverageWithVertexMask(
    float3 positionWS,
    float outlineEnabled,
    float outlineWidth,
    float referenceDistance,
    float distanceScale,
    float vertexMask,
    float additionalWidthMask)
{
    return MikuGameToonOutlineCoverageWithDistanceMultiplier(
        outlineEnabled,
        outlineWidth,
        MikuGameToonOutlineDistanceMultiplier(
            positionWS,
            referenceDistance,
            distanceScale,
            0.0),
        vertexMask,
        additionalWidthMask);
}

float4 MikuGameToonOutlinePositionCSWithLegacyMode(
    float4 positionCS,
    float3 positionWS,
    float3 outlineNormalOS,
    float outlineWidth,
    float referenceDistance,
    float distanceScale,
    float4 vertexColor,
    float additionalWidthMask,
    float legacyConstantResponse)
{
    float3 outlineNormalWS = MikuGameToonOutlineSafeNormalize(
        TransformObjectToWorldNormal(outlineNormalOS),
        float3(0.0, 0.0, 1.0));
    // URP's GPU projection supplies aspect, mirrored projections, and the
    // render-target Y flip represented by _ProjectionParams.x.
    float2 projectedDirection = TransformWorldToHClipDir(
        outlineNormalWS,
        false).xy;
    float2 pixelDirection = MikuGameToonOutlineSafeNormalize2(
        projectedDirection * max(
            _ScreenParams.xy,
            float2(1.0, 1.0)),
        float2(0.0, 0.0));
    float2 screenDirection = pixelDirection * float2(
        _ScreenParams.y / max(_ScreenParams.x, 1.0),
        1.0);
    float screenHeightWidth = MikuGameToonOutlineScreenHeightWidth(
        positionWS,
        outlineWidth,
        referenceDistance,
        distanceScale,
        vertexColor,
        additionalWidthMask,
        legacyConstantResponse);
    positionCS.xy += screenDirection *
        (2.0 * screenHeightWidth * positionCS.w);
    return positionCS;
}

// Explicit-distance-multiplier variant used by family-specific distance
// formulas (for example the Wuwa tutorial's near/far two-segment response).
// The caller supplies a precomputed multiplier; the shared vertex-color mask
// and additional width mask are still applied here.
float4 MikuGameToonOutlinePositionCSWithDistanceMultiplier(
    float4 positionCS,
    float3 positionWS,
    float3 outlineNormalOS,
    float outlineWidth,
    float referenceDistance,
    float distanceScale,
    float4 vertexColor,
    float additionalWidthMask,
    float distanceMultiplier)
{
    float3 outlineNormalWS = MikuGameToonOutlineSafeNormalize(
        TransformObjectToWorldNormal(outlineNormalOS),
        float3(0.0, 0.0, 1.0));
    float2 projectedDirection = TransformWorldToHClipDir(
        outlineNormalWS,
        false).xy;
    float2 pixelDirection = MikuGameToonOutlineSafeNormalize2(
        projectedDirection * max(
            _ScreenParams.xy,
            float2(1.0, 1.0)),
        float2(0.0, 0.0));
    float2 screenDirection = pixelDirection * float2(
        _ScreenParams.y / max(_ScreenParams.x, 1.0),
        1.0);
    float width = MikuGameToonOutlineCoverageWithDistanceMultiplier(
        1.0,
        outlineWidth,
        distanceMultiplier,
        MikuGameToonOutlineVertexMask(vertexColor),
        additionalWidthMask);
    positionCS.xy += screenDirection *
        (2.0 * width * positionCS.w);
    return positionCS;
}

// Explicit vertex-mask variant used by families whose authored mesh colors
// use a channel other than the Miku green width mask. The Genshin tutorial
// contract stores the outline width in vertex-color A; the shared green path
// remains the default for the other families.
float4 MikuGameToonOutlinePositionCSWithVertexMask(
    float4 positionCS,
    float3 positionWS,
    float3 outlineNormalOS,
    float outlineWidth,
    float referenceDistance,
    float distanceScale,
    float vertexMask,
    float additionalWidthMask)
{
    float3 outlineNormalWS = MikuGameToonOutlineSafeNormalize(
        TransformObjectToWorldNormal(outlineNormalOS),
        float3(0.0, 0.0, 1.0));
    float2 projectedDirection = TransformWorldToHClipDir(
        outlineNormalWS,
        false).xy;
    float2 pixelDirection = MikuGameToonOutlineSafeNormalize2(
        projectedDirection * max(
            _ScreenParams.xy,
            float2(1.0, 1.0)),
        float2(0.0, 0.0));
    float2 screenDirection = pixelDirection * float2(
        _ScreenParams.y / max(_ScreenParams.x, 1.0),
        1.0);
    float width = MikuGameToonOutlineCoverageWithVertexMask(
        positionWS,
        1.0,
        outlineWidth,
        referenceDistance,
        distanceScale,
        vertexMask,
        additionalWidthMask);
    positionCS.xy += screenDirection *
        (2.0 * width * positionCS.w);
    return positionCS;
}

float4 MikuGameToonOutlinePositionCS(
    float4 positionCS,
    float3 positionWS,
    float3 outlineNormalOS,
    float outlineWidth,
    float referenceDistance,
    float distanceScale,
    float4 vertexColor,
    float additionalWidthMask)
{
    return MikuGameToonOutlinePositionCSWithVertexMask(
        positionCS,
        positionWS,
        outlineNormalOS,
        outlineWidth,
        referenceDistance,
        distanceScale,
        MikuGameToonOutlineVertexMask(vertexColor),
        additionalWidthMask);
}

#endif
