// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Wuwa/Face"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("Legacy MainTex, optional", 2D) = "white" {}
        _FaceSDF ("Face SDF", 2D) = "white" {}
        [Toggle] _UseFaceBasis ("Use Material Face Basis", Float) = 1
        _FaceRight ("Face Right (Object Space)", Vector) = (1,0,0,0)
        _FaceUp ("Face Up (Object Space)", Vector) = (0,0,1,0)
        _FaceForward ("Face Forward (Object Space)", Vector) = (0,-1,0,0)
        _FaceFlatness ("Face Normal Flatness", Range(0,1)) = 1
        [HideInInspector] _MikuHeadForwardWS ("Miku Head Forward WS", Vector) = (0,0,1,0)
        [HideInInspector] _MikuHeadRightWS ("Miku Head Right WS", Vector) = (1,0,0,0)
        [HideInInspector] _MikuHeadUpWS ("Miku Head Up WS", Vector) = (0,1,0,0)
        [HideInInspector] _MikuHeadAxesValid ("Miku Head Axes Valid", Float) = 0
        _FaceID ("Face ID", 2D) = "white" {}
        _FaceHET ("Face HET", 2D) = "white" {}
        _SkinRamp ("Skin Ramp", 2D) = "white" {}
        _EmissionMap ("Emission Map", 2D) = "black" {}
        [MainColor] _BaseColorTint ("Base Color Tint", Color) = (1,1,1,1)
        _LitTint ("Lit Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.76,0.72,0.76,1)
        [Toggle(_AREA_FACE)] _AreaFace ("Area Face", Float) = 1
        _FaceSdfMainChannel ("Face SDF Main Channel 0R 1G 2B 3A", Range(0,3)) = 3
        _FaceSdfSoftChannel ("Face SDF Soft Channel 0R 1G 2B 3A", Range(0,3)) = 2
        _FaceShadowOffset ("Face Shadow Offset", Range(-1,1)) = -0.08
        _FaceShadowSoftness ("Face Shadow Softness", Range(0.001,1)) = 0.067
        _FaceThresholdBias ("Face Threshold Bias", Range(-1,1)) = -0.073
        _FaceSoftChannelStrength ("Face Soft Channel Strength", Range(0,1)) = 1
        _FaceShadowStrength ("Face SDF Shadow Strength", Range(0,1)) = 0.65
        _FaceSdfDebugMode ("Face SDF Debug Mode", Range(0,5)) = 0
        _SkinRampUV ("Skin Ramp UV", Vector) = (0.24,0.9,0,0)
        _SkinRampSaturation ("Skin Ramp Saturation", Range(0,2)) = 0.77
        _SkinRampBrightness ("Skin Ramp Brightness", Range(0,2)) = 1.36
        _SkinRampCurvePower ("Skin Ramp Curve Power", Range(0.1,4)) = 2.04
        _SkinRampStrength ("Skin Ramp Strength", Range(0,1)) = 0.42
        _FaceBaseCurvePower ("Face Base Curve Power", Range(0.1,4)) = 1.2
        _FaceBaseBrightness ("Face Base Brightness", Range(0,2)) = 1
        _FaceFinalBrightness ("Face Final Brightness", Range(0,2)) = 1
        _SkinSSSIntensity ("Skin SSS Intensity", Range(0,1)) = 0
        _SSSColor ("SSS Color", Color) = (1,0.5,0.4,1)
        _SSSArea ("SSS Area", Range(0,1)) = 0.35
        _SkinToneBrightness ("Skin Tone Brightness", Range(0,2)) = 1
        _SkinToneWhitening ("Skin Tone Whitening", Range(0,1)) = 0
        _SkinToneTarget ("Skin Tone Target", Color) = (1,0.93,0.90,1)
        _SkinMaskDebugMode ("Skin Mask Debug Mode", Range(0,1)) = 0
        _FaceBlushColor ("Face Blush Color", Color) = (1,0.78,0.82,1)
        _FaceBlushStrength ("Face Blush Strength", Range(0,1)) = 0.24
        _FaceBlushCenters ("Face Blush Centers", Vector) = (0.32,0.58,0.68,0.58)
        _FaceBlushSize ("Face Blush Size", Vector) = (0.16,0.1,0,0)
        _FaceExtraLightChannel ("Face HET Channel 0R 1G 2B 3A", Range(0,3)) = 0
        _FaceExtraLightColor ("Face Extra Light Color", Color) = (1,0.72,0.68,1)
        _FaceExtraLightStrength ("Face Extra Light Strength", Range(0,2)) = 0
        _HairShadowStrength ("Hair Shadow Strength", Range(0,1)) = 0.45
        _HairShadowDepthBias ("Hair Shadow Depth Bias", Range(-1,1)) = 0.02
        _HairShadowSoftness ("Hair Shadow Softness", Range(0.001,1)) = 0.05
        _HairShadowScreenOffset ("Hair Shadow Screen Offset", Range(-0.1,0.1)) = 0.015
        [Toggle(_WUWA_HAIR_SHADOW_ON)] _UseHairShadow ("Use Hair Shadow", Float) = 1
        _IndirectLightUsage ("Indirect Light Usage", Range(0,2)) = 0
        _MainLightColorUsage ("Main Light Color Usage", Range(0,1)) = 0
        _VerticalGradientColor ("Vertical Gradient Low Color", Color) = (0.86,0.80,0.94,1)
        _VerticalGradientStrength ("Vertical Gradient Strength", Range(0,1)) = 0
        _GradientUVIndex ("Gradient UV Channel", Range(0,3)) = 3
        _GradientInvert ("Gradient Invert", Float) = 0
        _RimLightBrightness ("Rim Brightness", Range(0,4)) = 0
        _RimLightTintColor ("Rim Tint", Color) = (1,1,1,1)
        _RimLightWidth ("Rim Width", Range(0,10)) = 1
        _RimLightThreshold ("Rim Depth Threshold", Range(0,1)) = 0.03
        _RimLightFadeout ("Rim Fadeout", Range(0.001,1)) = 0.2
        [HideInInspector] _FresnelPower ("Legacy Fresnel Power", Range(0.1,8)) = 2
        [HideInInspector] _FresnelClamp ("Legacy Fresnel Clamp", Range(0,1)) = 1
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.001
        _OutlineReferenceDistance ("Outline Reference Distance", Float) = 5
        _OutlineDistanceScale ("Outline Distance Scale", Range(0,1)) = 1
        _OutlineDistanceMode ("Outline Distance Mode 0 Miku 1 Tutorial", Range(0,1)) = 1
        _OutlineVertexColorMask ("Outline Vertex Color Mask", Range(0,1)) = 1
        _OutlineColorTint ("Outline Color", Color) = (0.34,0.18,0.22,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero
            ColorMask RGBA
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WuwaFaceVert
            #pragma fragment WuwaFaceFrag
            #pragma shader_feature_local _WUWA_FACE_HET_ON
            #pragma shader_feature_local _WUWA_ID_ON
            #pragma shader_feature_local _WUWA_SKIN_RAMP_ON
            #pragma shader_feature_local _WUWA_HAIR_SHADOW_ON
            #pragma shader_feature_local _WUWA_EMISSION_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WuwaCommon.hlsl"
            #include "../GameToon/MikuGameToonSkin.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_FaceSDF); SAMPLER(sampler_FaceSDF);
            TEXTURE2D(_FaceID); SAMPLER(sampler_FaceID);
            TEXTURE2D(_FaceHET); SAMPLER(sampler_FaceHET);
            TEXTURE2D(_SkinRamp); SAMPLER(sampler_SkinRamp);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_WuwaHairShadowTexture); SAMPLER(sampler_WuwaHairShadowTexture);
            float _WuwaHairShadowAvailable;
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint; float4 _LitTint; float4 _ShadowTint;
                float _UseFaceBasis; float4 _FaceRight; float4 _FaceUp; float4 _FaceForward; float _FaceFlatness;
                float4 _MikuHeadForwardWS; float4 _MikuHeadRightWS; float4 _MikuHeadUpWS; float _MikuHeadAxesValid;
                float _FaceSdfMainChannel; float _FaceSdfSoftChannel; float _FaceShadowOffset; float _FaceShadowSoftness; float _FaceThresholdBias; float _FaceSoftChannelStrength; float _FaceShadowStrength; float _FaceSdfDebugMode;
                float4 _SkinRampUV; float _SkinRampSaturation; float _SkinRampBrightness; float _SkinRampCurvePower; float _SkinRampStrength; float _FaceBaseCurvePower; float _FaceBaseBrightness; float _FaceFinalBrightness;
                float _SkinSSSIntensity; float4 _SSSColor; float _SSSArea; float _SkinToneBrightness; float _SkinToneWhitening; float4 _SkinToneTarget; float _SkinMaskDebugMode;
                float4 _FaceBlushColor; float _FaceBlushStrength; float4 _FaceBlushCenters; float4 _FaceBlushSize; float _FaceExtraLightChannel; float4 _FaceExtraLightColor; float _FaceExtraLightStrength;
                float _HairShadowStrength; float _HairShadowDepthBias; float _HairShadowSoftness; float _HairShadowScreenOffset; float _UseHairShadow;
                float _IndirectLightUsage; float _MainLightColorUsage;
                float4 _VerticalGradientColor; float _VerticalGradientStrength; float _GradientUVIndex; float _GradientInvert;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineDistanceMode; float _OutlineVertexColorMask; float4 _OutlineColorTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv3 : TEXCOORD3; float4 smoothNormalData : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewDirWS : TEXCOORD2; float2 uv : TEXCOORD3; float4 shadowCoord : TEXCOORD4; float2 uv1 : TEXCOORD5; float2 uv3 : TEXCOORD6; };
            Varyings WuwaFaceVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = normalize(normal.normalWS);
                output.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = TransformWorldToShadowCoord(pos.positionWS);
                output.uv1 = input.uv1;
                output.uv3 = input.uv3;
                return output;
            }
            float3 WuwaFaceSafeNormalize(float3 value, float3 fallback)
            {
                float lengthSquared = dot(value, value);
                return lengthSquared > 1e-8
                    ? value * rsqrt(lengthSquared)
                    : fallback;
            }
            half4 WuwaFaceFrag(Varyings input) : SV_Target
            {
                float4 rawBaseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 baseSample = rawBaseSample * _BaseColorTint;
                float4 faceSdf = SAMPLE_TEXTURE2D(_FaceSDF, sampler_FaceSDF, input.uv);
                float4 faceID = 1.0.xxxx;
                #if defined(_WUWA_ID_ON)
                    faceID = SAMPLE_TEXTURE2D(_FaceID, sampler_FaceID, input.uv);
                #endif
                float skinMask;
                #if defined(_WUWA_ID_ON)
                    skinMask = MikuGameToonHighValueMask(faceID.r);
                #else
                    skinMask = MikuGameToonWarmPaleFaceMask(rawBaseSample.rgb);
                #endif
                float3 skinBase = MikuGameToonApplySkinTone(baseSample.rgb, skinMask, _SkinToneBrightness, _SkinToneWhitening, _SkinToneTarget.rgb);
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 lightDirWS = normalize(mainLight.direction);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float3x3 objectToWorld = (float3x3)GetObjectToWorldMatrix();
                float useFaceBasis = saturate(_UseFaceBasis);
                float3 faceForwardOS = lerp(
                    float3(0.0, 0.0, 1.0),
                    _FaceForward.xyz,
                    useFaceBasis);
                float3 faceRightOS = lerp(
                    float3(1.0, 0.0, 0.0),
                    _FaceRight.xyz,
                    useFaceBasis);
                float3 faceUpOS = lerp(
                    float3(0.0, 1.0, 0.0),
                    _FaceUp.xyz,
                    useFaceBasis);
                float3 defaultForwardWS = normalize(mul(
                    objectToWorld,
                    float3(0.0, 0.0, 1.0)));
                float3 defaultRightWS = normalize(mul(
                    objectToWorld,
                    float3(1.0, 0.0, 0.0)));
                float3 defaultUpWS = normalize(mul(
                    objectToWorld,
                    float3(0.0, 1.0, 0.0)));
                float3 fallbackForwardWS = WuwaFaceSafeNormalize(
                    mul(objectToWorld, faceForwardOS),
                    defaultForwardWS);
                float3 rawRightWS = WuwaFaceSafeNormalize(
                    mul(objectToWorld, faceRightOS),
                    defaultRightWS);
                float3 rawUpWS = WuwaFaceSafeNormalize(
                    mul(objectToWorld, faceUpOS),
                    defaultUpWS);
                float3 projectedRightWS = rawRightWS - fallbackForwardWS *
                    dot(rawRightWS, fallbackForwardWS);
                float3 fallbackRightWS = WuwaFaceSafeNormalize(
                    projectedRightWS,
                    defaultRightWS);
                float handedness = dot(
                    cross(fallbackForwardWS, fallbackRightWS),
                    rawUpWS) < 0.0 ? -1.0 : 1.0;
                fallbackRightWS *= handedness;
                float3 fallbackUpWS = normalize(
                    cross(fallbackForwardWS, fallbackRightWS));
                float3 headForwardWS, headRightWS, headUpWS;
                Miku_ResolveFaceSdfHeadAxes(fallbackForwardWS, fallbackRightWS, fallbackUpWS, _MikuHeadForwardWS, _MikuHeadRightWS, _MikuHeadUpWS, _MikuHeadAxesValid, headForwardWS, headRightWS, headUpWS);
                float3 faceShadingNormalWS = normalize(lerp(
                    normalWS,
                    headForwardWS,
                    saturate(_FaceFlatness)));
                float faceLight = Wuwa_FaceSDFLight(input.uv, faceSdf, TEXTURE2D_ARGS(_FaceSDF, sampler_FaceSDF), lightDirWS, headForwardWS, headRightWS, headUpWS, _FaceSdfMainChannel, _FaceSdfSoftChannel, _FaceShadowOffset, _FaceShadowSoftness, _FaceThresholdBias, _FaceSoftChannelStrength) * mainLight.shadowAttenuation;
                faceLight = lerp(1.0, faceLight, faceID.r);
                #if defined(_WUWA_HAIR_SHADOW_ON)
                    float hairShadow = Wuwa_HairShadowMask(TEXTURE2D_ARGS(_WuwaHairShadowTexture, sampler_WuwaHairShadowTexture), input.positionCS, lightDirWS, _HairShadowScreenOffset, _HairShadowDepthBias, _HairShadowSoftness, _HairShadowStrength, _WuwaHairShadowAvailable);
                    faceLight *= 1.0 - hairShadow * _UseHairShadow;
                #endif
                float debugMode = round(_FaceSdfDebugMode);
                if (debugMode == 1) return half4(faceSdf.rrr, 1.0);
                if (debugMode == 2) return half4(faceSdf.ggg, 1.0);
                if (debugMode == 3) return half4(faceSdf.bbb, 1.0);
                if (debugMode == 4) return half4(faceSdf.aaa, 1.0);
                if (debugMode == 5) return half4(faceLight.xxx, 1.0);
                if (_SkinMaskDebugMode > 0.5) return half4(skinMask.xxx, 1.0);
                float3 mainLightColor = lerp(1.0.xxx, mainLight.color.rgb, _MainLightColorUsage);
                float3 shadowTone = _ShadowTint.rgb;
                #if defined(_WUWA_SKIN_RAMP_ON)
                    float3 rampSample = SAMPLE_TEXTURE2D(_SkinRamp, sampler_SkinRamp, saturate(_SkinRampUV.xy)).rgb;
                    rampSample = Wuwa_ApplyPowerCurve(rampSample, _SkinRampCurvePower, 1.0);
                    rampSample = saturate(Wuwa_AdjustSaturation(rampSample, _SkinRampSaturation) * _SkinRampBrightness);
                    shadowTone = lerp(1.0.xxx, rampSample, _SkinRampStrength);
                #endif
                float3 faceBase = saturate(Wuwa_ApplyPowerCurve(skinBase, _FaceBaseCurvePower, _FaceBaseBrightness));
                float2 blushSize = max(_FaceBlushSize.xy, 1e-3.xx);
                float2 leftBlushUV = (input.uv - _FaceBlushCenters.xy) / blushSize;
                float2 rightBlushUV = (input.uv - _FaceBlushCenters.zw) / blushSize;
                float2 noseCenter = float2((_FaceBlushCenters.x + _FaceBlushCenters.z) * 0.5, _FaceBlushCenters.y - blushSize.y * 0.12);
                float2 noseBlushUV = (input.uv - noseCenter) / (blushSize * float2(0.48, 0.72));
                float cheekMask = max(saturate(1.0 - dot(leftBlushUV, leftBlushUV)), saturate(1.0 - dot(rightBlushUV, rightBlushUV)));
                float noseMask = saturate(1.0 - dot(noseBlushUV, noseBlushUV)) * 0.45;
                float blushMask = saturate(max(cheekMask * cheekMask, noseMask * noseMask) * _FaceBlushStrength);
                float3 blushTone = saturate(faceBase * _FaceBlushColor.rgb + _FaceBlushColor.rgb * 0.04);
                faceBase = lerp(faceBase, blushTone, blushMask);
                float3 faceLightingTone = lerp(shadowTone, _LitTint.rgb, faceLight);
                float3 color = faceBase * lerp(1.0.xxx, faceLightingTone, saturate(_FaceShadowStrength)) * _FaceFinalBrightness * mainLightColor;
                color += Wuwa_SampleSH_Indirect(faceShadingNormalWS, 0.0) * _IndirectLightUsage * skinBase;
                color += MikuGameToonSkinSSS(faceBase, skinMask, faceShadingNormalWS, viewDirWS, lightDirWS, mainLight.color.rgb, faceLight, _SkinSSSIntensity, _SSSArea, _SSSColor.rgb);
                color += Wuwa_FresnelStepRim(
                    faceShadingNormalWS,
                    viewDirWS,
                    _FresnelPower,
                    _RimLightBrightness,
                    _RimLightTintColor.rgb,
                    faceBase);
                float gradientValue = Wuwa_GradientValue(
                    input.uv,
                    input.uv1,
                    input.uv3,
                    _GradientUVIndex,
                    _GradientInvert);
                color = Wuwa_VerticalGradient(
                    color,
                    _VerticalGradientColor.rgb,
                    gradientValue * _VerticalGradientStrength);
                #if defined(_WUWA_FACE_HET_ON)
                    float4 het = SAMPLE_TEXTURE2D(_FaceHET, sampler_FaceHET, input.uv);
                    float extraLightMask = Wuwa_SelectChannel(het, _FaceExtraLightChannel);
                    color += extraLightMask * _FaceExtraLightColor.rgb * _FaceExtraLightStrength;
                #endif
                #if defined(_WUWA_EMISSION_ON)
                    color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb;
                #endif
                return half4(color, baseSample.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "MikuToonCharacterMask"
            Tags { "LightMode"="MikuToonCharacterMask" }
            Cull Back
            ZWrite Off
            ZTest Equal
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MikuGameScreenRimVertex
            #pragma fragment MikuGameScreenRimFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint; float4 _LitTint; float4 _ShadowTint;
                float _UseFaceBasis; float4 _FaceRight; float4 _FaceUp; float4 _FaceForward; float _FaceFlatness;
                float4 _MikuHeadForwardWS; float4 _MikuHeadRightWS; float4 _MikuHeadUpWS; float _MikuHeadAxesValid;
                float _FaceSdfMainChannel; float _FaceSdfSoftChannel; float _FaceShadowOffset; float _FaceShadowSoftness; float _FaceThresholdBias; float _FaceSoftChannelStrength; float _FaceShadowStrength; float _FaceSdfDebugMode;
                float4 _SkinRampUV; float _SkinRampSaturation; float _SkinRampBrightness; float _SkinRampCurvePower; float _SkinRampStrength; float _FaceBaseCurvePower; float _FaceBaseBrightness; float _FaceFinalBrightness;
                float _SkinSSSIntensity; float4 _SSSColor; float _SSSArea; float _SkinToneBrightness; float _SkinToneWhitening; float4 _SkinToneTarget; float _SkinMaskDebugMode;
                float4 _FaceBlushColor; float _FaceBlushStrength; float4 _FaceBlushCenters; float4 _FaceBlushSize; float _FaceExtraLightChannel; float4 _FaceExtraLightColor; float _FaceExtraLightStrength;
                float _HairShadowStrength; float _HairShadowDepthBias; float _HairShadowSoftness; float _HairShadowScreenOffset; float _UseHairShadow;
                float _IndirectLightUsage; float _MainLightColorUsage;
                float4 _VerticalGradientColor; float _VerticalGradientStrength; float _GradientUVIndex; float _GradientInvert;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineDistanceMode; float _OutlineVertexColorMask; float4 _OutlineColorTint;
            CBUFFER_END
            #include "Packages/com.miku.shaderconverter/Runtime/GameToon/MikuGameToonScreenRimPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend One Zero
            ColorMask RGBA
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WuwaCommon.hlsl"
            #include "Packages/com.miku.shaderconverter/Runtime/GameToon/MikuGameToonOutline.hlsl"
            TEXTURE2D(_FaceID); SAMPLER(sampler_FaceID);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineDistanceMode; float _OutlineVertexColorMask; float4 _OutlineColorTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float4 color : COLOR; float4 smoothNormalData : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings OutlineVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                float3 outlineNormalOS = MikuGameToonOutlineNormalTangentSpaceV2(
                    input.smoothNormalData, input.normalOS, input.tangentOS);
                float mikuDistance = MikuGameToonOutlineDistanceMultiplier(
                    pos.positionWS,
                    _OutlineReferenceDistance,
                    _OutlineDistanceScale,
                    0.0);
                float tutorialDistance = Wuwa_TutorialOutlineWidth(
                    pos.positionWS,
                    _WorldSpaceCameraPos);
                float distanceMultiplier = lerp(
                    mikuDistance,
                    tutorialDistance,
                    saturate(_OutlineDistanceMode));
                float widthMask = lerp(
                    1.0,
                    MikuGameToonOutlineVertexMask(input.color),
                    saturate(_OutlineVertexColorMask));
                output.positionCS = MikuGameToonOutlinePositionCSWithDistanceMultiplier(
                    pos.positionCS,
                    pos.positionWS,
                    outlineNormalOS,
                    _OutlineWidth,
                    _OutlineReferenceDistance,
                    _OutlineDistanceScale,
                    float4(1.0, 1.0, 1.0, 1.0),
                    widthMask,
                    distanceMultiplier);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 OutlineFrag(Varyings input) : SV_Target
            {
                float4 idMap = SAMPLE_TEXTURE2D(_FaceID, sampler_FaceID, input.uv);
                return half4(Wuwa_IDOutlineColor(idMap, _OutlineColorTint.rgb), 1.0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings DepthVert(Attributes input) { Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); return output; }
            half4 DepthFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings DepthVert(Attributes input) { Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); return output; }
            half4 DepthFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    CustomEditor "Miku.ShaderConverter.Editor.MikuManualTextureShaderGUI"
}
