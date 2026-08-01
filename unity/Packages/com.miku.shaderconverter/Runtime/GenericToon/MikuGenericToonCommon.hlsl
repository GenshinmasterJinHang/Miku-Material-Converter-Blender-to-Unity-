// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

#ifndef MIKU_GENERIC_TOON_COMMON_INCLUDED
#define MIKU_GENERIC_TOON_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

TEXTURE2D(_MIKU_BaseMap);
SAMPLER(sampler_MIKU_BaseMap);

CBUFFER_START(UnityPerMaterial)
float4 _MIKU_BaseMap_ST;
float4 _MIKU_BaseColor;
float4 _MIKU_ShadowColor;
float4 _MIKU_OutlineColor;
float4 _MIKU_RimColor;
float4 _MIKU_FaceCenterOS;
float4 _MIKU_FaceExtentOS;
float _MIKU_Cutoff;
float _MIKU_AlphaClip;
float _MIKU_ToonSteps;
float _MIKU_ShadowSoftness;
float _MIKU_SSSStrength;
float _MIKU_OutlineEnabled;
float _MIKU_OutlineWidth;
float _MIKU_OutlineDepthBias;
float _MIKU_OutlineMinPixels;
float _MIKU_OutlineMaxPixels;
float _MIKU_RimIntensity;
float _MIKU_RimWidth;
float _MIKU_FaceRembrandt;
float _MIKU_FaceBlush;
float _MIKU_MetallicAccent;
float _MIKU_SemanticMode;
CBUFFER_END

struct MikuToonAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float3 smoothNormalOS : TEXCOORD7;
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct MikuToonVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 positionOS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    float2 uv : TEXCOORD3;
    half4 color : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

inline half4 MikuSampleBase(float2 uv)
{
    return SAMPLE_TEXTURE2D(
        _MIKU_BaseMap,
        sampler_MIKU_BaseMap,
        uv) * _MIKU_BaseColor;
}

inline void MikuAlphaClip(half alpha)
{
    if (_MIKU_AlphaClip > 0.5)
        clip(alpha - _MIKU_Cutoff);
}

MikuToonVaryings MikuToonVertex(MikuToonAttributes input)
{
    MikuToonVaryings output = (MikuToonVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(
        input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.positionOS = input.positionOS.xyz;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _MIKU_BaseMap);
    output.color = input.color;
    return output;
}

inline half MikuToonBand(half value)
{
    half steps = max(1.0h, (half)_MIKU_ToonSteps);
    half quantized = floor(saturate(value) * steps) / max(1.0h, steps - 1.0h);
    half width = max(fwidth(value), (half)_MIKU_ShadowSoftness * 0.05h);
    return smoothstep(quantized - width, quantized + width, value);
}

inline half3 MikuFaceNormal(
    float3 positionOS,
    half3 geometricNormalWS)
{
    float3 extent = max(abs(_MIKU_FaceExtentOS.xyz), 0.0001);
    float3 local = (positionOS - _MIKU_FaceCenterOS.xyz) / extent;
    float3 virtualNormalOS = normalize(float3(local.x, local.y * 0.35, 1.0));
    half3 virtualNormalWS = TransformObjectToWorldNormal(virtualNormalOS);
    half faceWeight = step(0.5h, (half)_MIKU_SemanticMode);
    faceWeight *= step((half)_MIKU_SemanticMode, 1.5h);
    return normalize(lerp(geometricNormalWS, virtualNormalWS, faceWeight));
}

half4 MikuToonFragment(MikuToonVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half4 baseSample = MikuSampleBase(input.uv);
    MikuAlphaClip(baseSample.a);
    half3 normalWS = MikuFaceNormal(
        input.positionOS,
        normalize(input.normalWS));
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    half ndl = saturate(dot(normalWS, mainLight.direction));
    half attenuation =
        mainLight.distanceAttenuation * mainLight.shadowAttenuation;
    half band = MikuToonBand(ndl * attenuation);
    half3 color = baseSample.rgb *
        lerp(_MIKU_ShadowColor.rgb, mainLight.color, band);
    color += baseSample.rgb * SampleSH(normalWS) * 0.35h;

    uint additionalCount = GetAdditionalLightsCount();
    for (uint lightIndex = 0u; lightIndex < additionalCount; ++lightIndex)
    {
        Light light = GetAdditionalLight(lightIndex, input.positionWS);
        half fill = saturate(dot(normalWS, light.direction));
        color += baseSample.rgb * light.color * fill *
            light.distanceAttenuation * 0.25h;
    }

    half sssMask = input.color.r;
    color += baseSample.rgb * _MIKU_SSSStrength * sssMask *
        pow(saturate(1.0h - ndl), 2.0h) * 0.25h;
    if (_MIKU_SemanticMode > 0.5 && _MIKU_SemanticMode < 1.5)
    {
        float3 face = (input.positionOS - _MIKU_FaceCenterOS.xyz) /
            max(abs(_MIKU_FaceExtentOS.xyz), 0.0001);
        half faceTriangle = saturate(
            1.0h - abs(face.x * 2.0h + face.y + 0.25h));
        color += baseSample.rgb * faceTriangle *
            _MIKU_FaceRembrandt * (1.0h - band) * 0.2h;
        half blush = saturate(1.0h - abs(face.y + 0.25h) * 5.0h) *
            saturate(abs(face.x) * 2.5h);
        color = lerp(color, color * half3(1.15h, 0.75h, 0.8h),
            blush * _MIKU_FaceBlush);
    }
    color += _MIKU_MetallicAccent *
        pow(saturate(dot(reflect(-mainLight.direction, normalWS),
            GetWorldSpaceNormalizeViewDir(input.positionWS))), 24.0h);
    return half4(color, baseSample.a);
}

half4 MikuDepthFragment(MikuToonVaryings input) : SV_Target
{
    MikuAlphaClip(MikuSampleBase(input.uv).a);
    return 0;
}

half4 MikuDepthNormalsFragment(MikuToonVaryings input) : SV_Target
{
    MikuAlphaClip(MikuSampleBase(input.uv).a);
    return half4(normalize(input.normalWS) * 0.5h + 0.5h, 0);
}

struct MikuMotionVaryings
{
    float4 positionCS : SV_POSITION;
    float4 currentPositionCSNoJitter : TEXCOORD0;
    float4 previousPositionCSNoJitter : TEXCOORD1;
    float2 uv : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

MikuMotionVaryings MikuMotionVertex(MikuToonAttributes input)
{
    MikuMotionVaryings output = (MikuMotionVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.currentPositionCSNoJitter = mul(
        _NonJitteredViewProjMatrix,
        mul(UNITY_MATRIX_M, input.positionOS));
    output.previousPositionCSNoJitter = mul(
        _PrevViewProjMatrix,
        mul(UNITY_PREV_MATRIX_M, input.positionOS));
    output.uv = TRANSFORM_TEX(input.uv, _MIKU_BaseMap);
    return output;
}

half4 MikuMotionFragment(MikuMotionVaryings input) : SV_Target
{
    MikuAlphaClip(MikuSampleBase(input.uv).a);
    return half4(CalcNdcMotionVectorFromCsPositions(
        input.currentPositionCSNoJitter,
        input.previousPositionCSNoJitter), 0, 0);
}

MikuToonVaryings MikuOutlineVertex(MikuToonAttributes input)
{
    MikuToonVaryings output = MikuToonVertex(input);
    float3 normalOS = dot(
        input.smoothNormalOS,
        input.smoothNormalOS) > 0.000001
        ? normalize(input.smoothNormalOS)
        : normalize(input.normalOS);
    float3 normalVS = TransformWorldToViewDir(
        TransformObjectToWorldNormal(normalOS));
    float2 direction = normalize(normalVS.xy + float2(0.000001, 0.0));
    float vertexMask = saturate(input.color.g);
    float pixels = clamp(
        _MIKU_OutlineWidth * vertexMask,
        _MIKU_OutlineMinPixels,
        _MIKU_OutlineMaxPixels);
    output.positionCS.xy += direction * pixels *
        (2.0 / _ScreenParams.xy) * output.positionCS.w;
    output.positionCS.z += _MIKU_OutlineDepthBias *
        output.positionCS.w * 0.0001;
    return output;
}

half4 MikuOutlineFragment(MikuToonVaryings input) : SV_Target
{
    clip(_MIKU_OutlineEnabled - 0.5);
    MikuAlphaClip(MikuSampleBase(input.uv).a);
    return _MIKU_OutlineColor;
}

half4 MikuCharacterMaskFragment(MikuToonVaryings input) : SV_Target
{
    MikuAlphaClip(MikuSampleBase(input.uv).a);
    half enabled = saturate(input.color.b);
    return half4(
        _MIKU_RimColor.rgb * _MIKU_RimIntensity * enabled,
        saturate(_MIKU_RimWidth / 16.0) * enabled);
}

#endif
