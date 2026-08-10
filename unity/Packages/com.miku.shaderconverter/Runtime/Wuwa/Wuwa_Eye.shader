// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Wuwa/Eye"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("Legacy MainTex, optional", 2D) = "white" {}
        _EyeHET ("Eye Emission Mask", 2D) = "black" {}
        _EyeHDMF ("Eye HDMF", 2D) = "white" {}
        _EyeUpperHighlight ("Upper Highlight", 2D) = "black" {}
        _EyeLowerHighlight ("Lower Highlight", 2D) = "black" {}
        _EyeEG ("Fresnel Secondary Highlight", 2D) = "black" {}
        [HideInInspector] _EmissionMap ("Legacy Eye Emission Map", 2D) = "black" {}
        [MainColor] _BaseColorTint ("Base Color Tint", Color) = (1,1,1,1)
        [Toggle(_AREA_EYE)] _AreaEye ("Area Eye", Float) = 1
        _EyeBaseCurvePower ("Eye Base Curve Power", Range(0.1,4)) = 1.44
        _EyeBaseBrightness ("Eye Base Brightness", Range(0,4)) = 1.2
        _EyeTopShadowStrength ("Eye Top Shadow Strength", Range(0,1)) = 0.32
        [HDR] _EyeHETScleraColor ("HET Sclera Emission Color", Color) = (1,1,1,1)
        _EyeHETScleraStrength ("HET Sclera Emission Strength", Range(0,20)) = 1
        [HDR] _EyeHETPupilColor ("HET Pupil Emission Color", Color) = (1,1,1,1)
        _EyeHETPupilStrength ("HET Pupil Emission Strength", Range(0,20)) = 1
        [HDR] _EyeHDMFHighlightColor ("HDMF Highlight Color", Color) = (0.82,0.92,1,1)
        _EyeHDMFHighlightStrength ("HDMF Highlight Strength", Range(0,20)) = 1
        _EyeHighlightStrength ("Upper Highlight Strength", Range(0,20)) = 1
        _EyeSecondHighlightStrength ("Lower Highlight Strength", Range(0,20)) = 1
        [HDR] _EyeHighlightColor ("Upper Highlight Color", Color) = (0.82,0.92,1,1)
        [HDR] _EyeSecondHighlightColor ("Lower Highlight Color", Color) = (0.65,0.82,1,1)
        [HideInInspector] _EyePackedHighlightColor ("Legacy Packed Highlight Color", Color) = (1,0.72,0.9,1)
        _EyeUpperHighlightOffset ("Upper Highlight Fine Offset", Vector) = (0,0,0,0)
        _EyeLowerHighlightOffset ("Lower Highlight Fine Offset", Vector) = (0,0,0,0)
        _EyeUpperHighlightScale ("Upper Highlight Fine Scale", Vector) = (1,1,0,0)
        _EyeLowerHighlightScale ("Lower Highlight Fine Scale", Vector) = (1,1,0,0)
        [HideInInspector] _EyeUpperHighlightUVRow0 ("Upper Highlight UV Row 0", Vector) = (1,0,0,0)
        [HideInInspector] _EyeUpperHighlightUVRow1 ("Upper Highlight UV Row 1", Vector) = (0,1,0,0)
        [HideInInspector] _EyeLowerHighlightUVRow0 ("Lower Highlight UV Row 0", Vector) = (1,0,0,0)
        [HideInInspector] _EyeLowerHighlightUVRow1 ("Lower Highlight UV Row 1", Vector) = (0,1,0,0)
        [HideInInspector] _EyeEGUVRow0 ("EG UV Row 0", Vector) = (1,0,0,0)
        [HideInInspector] _EyeEGUVRow1 ("EG UV Row 1", Vector) = (0,1,0,0)
        [HideInInspector] _EyeHighlightThreshold ("Legacy Highlight Threshold", Range(0,1)) = 0.04000002
        [HideInInspector] _EyeHighlightSoftness ("Legacy Highlight Softness", Range(0.001,0.5)) = 0.001
        [HDR] _EyeEGColor ("EG Highlight Color", Color) = (0.7,0.86,1,1)
        _EyeEGStrength ("EG Highlight Strength", Range(0,20)) = 1
        _EyeEGFresnelPower ("EG Fresnel Power", Range(0.1,12)) = 3
        _EyeEGLightFollow ("EG Main Light Follow", Range(-0.25,0.25)) = 0.08
        _EyeEGCenter ("EG Center", Vector) = (0.5,0.5,0,0)
        _EyeEGScale ("EG Scale", Vector) = (1,1,0,0)
        _EyeEGOffset ("EG Fine Offset", Vector) = (0,0,0,0)
        _EyeParallaxStrength ("Eye Parallax Strength", Range(-0.1,0.1)) = 0
        _EyeShadowStart ("Eye Shadow Start", Range(0,1)) = 0.25
        _EyeShadowEnd ("Eye Shadow End", Range(0,1)) = 0.55
        _EyeLitTint ("Eye Lit Tint", Color) = (1,1,1,1)
        _EyeShadowTint ("Eye Shadow Tint", Color) = (0.82,0.82,0.82,1)
        _EyeBaseEmissionStrength ("Eye Base Emission Strength", Range(0,8)) = 0
        _EmissionStrength ("Highlight Emission Strength", Range(0,20)) = 1
        [HideInInspector] _EyeDebugView ("Eye Debug View", Float) = 0
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
            #pragma vertex WuwaEyeVert
            #pragma fragment WuwaEyeFrag
            #pragma shader_feature_local _WUWA_EYE_HET_ON
            #pragma shader_feature_local _WUWA_EYE_HDMF_ON
            #pragma shader_feature_local _WUWA_EYE_UPPER_HIGHLIGHT_ON
            #pragma shader_feature_local _WUWA_EYE_LOWER_HIGHLIGHT_ON
            #pragma shader_feature_local _WUWA_EYE_EG_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WuwaCommon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EyeHET); SAMPLER(sampler_EyeHET);
            TEXTURE2D(_EyeHDMF); SAMPLER(sampler_EyeHDMF);
            TEXTURE2D(_EyeUpperHighlight); SAMPLER(sampler_EyeUpperHighlight);
            TEXTURE2D(_EyeLowerHighlight); SAMPLER(sampler_EyeLowerHighlight);
            TEXTURE2D(_EyeEG); SAMPLER(sampler_EyeEG);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint;
                float _EyeBaseCurvePower; float _EyeBaseBrightness; float _EyeTopShadowStrength;
                float4 _EyeHETScleraColor; float _EyeHETScleraStrength;
                float4 _EyeHETPupilColor; float _EyeHETPupilStrength;
                float4 _EyeHDMFHighlightColor; float _EyeHDMFHighlightStrength;
                float _EyeHighlightStrength; float _EyeSecondHighlightStrength;
                float4 _EyeHighlightColor; float4 _EyeSecondHighlightColor; float4 _EyePackedHighlightColor;
                float4 _EyeUpperHighlightOffset; float4 _EyeLowerHighlightOffset;
                float4 _EyeUpperHighlightScale; float4 _EyeLowerHighlightScale;
                float4 _EyeUpperHighlightUVRow0; float4 _EyeUpperHighlightUVRow1;
                float4 _EyeLowerHighlightUVRow0; float4 _EyeLowerHighlightUVRow1;
                float4 _EyeEGUVRow0; float4 _EyeEGUVRow1;
                float _EyeHighlightThreshold; float _EyeHighlightSoftness;
                float4 _EyeEGColor; float _EyeEGStrength; float _EyeEGFresnelPower; float _EyeEGLightFollow;
                float4 _EyeEGCenter; float4 _EyeEGScale; float4 _EyeEGOffset;
                float _EyeParallaxStrength; float _EyeShadowStart; float _EyeShadowEnd; float4 _EyeLitTint; float4 _EyeShadowTint;
                float _EyeBaseEmissionStrength; float _EmissionStrength;
                float _EyeDebugView;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
            };
            Varyings WuwaEyeVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs basis = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = pos.positionCS;
                output.viewDirWS = GetWorldSpaceViewDir(pos.positionWS);
                output.uv = input.uv;
                output.normalWS = basis.normalWS;
                output.tangentWS = basis.tangentWS;
                output.bitangentWS = basis.bitangentWS;
                return output;
            }
            float2 WuwaEyeAffineUV(float2 uv, float4 row0, float4 row1)
            {
                float3 source = float3(uv, 1.0);
                return float2(dot(row0.xyz, source), dot(row1.xyz, source));
            }
            float2 WuwaEyeFineUV(float2 uv, float2 scale, float2 offset)
            {
                float2 safeScale = max(abs(scale), 1e-4.xx);
                return (uv - 0.5.xx - offset) / safeScale + 0.5.xx;
            }
            float WuwaEyeAuthoredHighlightRamp(float value)
            {
                const float gate = 0.0400000215;
                const float rampStart = 0.0803109035;
                const float rampEnd = 0.9041451216;
                return step(gate, value) * saturate(
                    (value - rampStart) / (rampEnd - rampStart));
            }
            float3 WuwaEyeSafeNormalize(float3 value, float3 fallback)
            {
                float lengthSquared = dot(value, value);
                return lengthSquared > 1e-8
                    ? value * rsqrt(lengthSquared)
                    : fallback;
            }
            half4 WuwaEyeFrag(Varyings input) : SV_Target
            {
                float3 normalWS = WuwaEyeSafeNormalize(input.normalWS, float3(0, 0, 1));
                float3 viewDirWS = WuwaEyeSafeNormalize(input.viewDirWS, normalWS);
                float3 tangentWS = WuwaEyeSafeNormalize(input.tangentWS, float3(0, 0, 0));
                float3 bitangentWS = WuwaEyeSafeNormalize(input.bitangentWS, float3(0, 0, 0));
                float tangentValid = step(1e-6, dot(tangentWS, tangentWS)) *
                    step(1e-6, dot(bitangentWS, bitangentWS));
                float2 parallax = float2(
                    dot(viewDirWS, tangentWS),
                    dot(viewDirWS, bitangentWS)) * _EyeParallaxStrength * tangentValid;
                float2 uv = saturate(input.uv + parallax);
                float2 baseUV = TRANSFORM_TEX(uv, _BaseMap);
                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV) * _BaseColorTint;
                float3 baseColor = saturate(Wuwa_ApplyPowerCurve(baseSample.rgb, _EyeBaseCurvePower, _EyeBaseBrightness));
                float topMask = smoothstep(0.42, 1.0, uv.y);
                float topShadow = saturate(topMask * _EyeTopShadowStrength);
                float3 color = baseColor * lerp(1.0, 0.62, topShadow);
                Light mainLight = GetMainLight();
                float3 lightDirWS = WuwaEyeSafeNormalize(mainLight.direction, normalWS);
                float ndl = saturate(dot(normalWS, lightDirWS));
                color *= lerp(
                    max(_EyeShadowTint.rgb, 0.0.xxx),
                    max(_EyeLitTint.rgb, 0.0.xxx),
                    smoothstep(_EyeShadowStart, _EyeShadowEnd, ndl));

                float hetMask = 0.0;
                #if defined(_WUWA_EYE_HET_ON)
                    hetMask = saturate(SAMPLE_TEXTURE2D(_EyeHET, sampler_EyeHET, uv).r);
                #endif

                float4 hdmf = float4(0.0, 1.0, 0.0, 1.0);
                #if defined(_WUWA_EYE_HDMF_ON)
                    hdmf = SAMPLE_TEXTURE2D(_EyeHDMF, sampler_EyeHDMF, uv);
                #endif
                float pupilMask = saturate(1.0 - hdmf.a);
                float3 scleraEmission = _EyeHETScleraColor.rgb * max(_EyeHETScleraStrength, 0.0);
                float3 pupilEmission = _EyeHETPupilColor.rgb * max(_EyeHETPupilStrength, 0.0);
                color += baseColor * hetMask * lerp(scleraEmission, pupilEmission, pupilMask);

                #if defined(_WUWA_EYE_HDMF_ON)
                    float hdmfHighlight = saturate(hdmf.r / 0.7538858056);
                    color += hdmfHighlight * _EyeHDMFHighlightColor.rgb *
                        max(_EyeHDMFHighlightStrength, 0.0) * _EmissionStrength;
                #endif

                float upperRaw = 0.0;
                float lowerRaw = 0.0;
                #if defined(_WUWA_EYE_UPPER_HIGHLIGHT_ON)
                    float2 upperMappedUV = WuwaEyeAffineUV(
                        uv,
                        _EyeUpperHighlightUVRow0,
                        _EyeUpperHighlightUVRow1);
                    float2 upperUV = WuwaEyeFineUV(
                        upperMappedUV,
                        _EyeUpperHighlightScale.xy,
                        _EyeUpperHighlightOffset.xy);
                    upperRaw = SAMPLE_TEXTURE2D(
                        _EyeUpperHighlight,
                        sampler_EyeUpperHighlight,
                        upperUV).r;
                #endif
                #if defined(_WUWA_EYE_LOWER_HIGHLIGHT_ON)
                    float2 lowerMappedUV = WuwaEyeAffineUV(
                        uv,
                        _EyeLowerHighlightUVRow0,
                        _EyeLowerHighlightUVRow1);
                    float2 lowerUV = WuwaEyeFineUV(
                        lowerMappedUV,
                        _EyeLowerHighlightScale.xy,
                        _EyeLowerHighlightOffset.xy);
                    lowerRaw = SAMPLE_TEXTURE2D(
                        _EyeLowerHighlight,
                        sampler_EyeLowerHighlight,
                        lowerUV).r;
                #endif
                float authoredRaw = 0.5 * (upperRaw + lowerRaw);
                float authoredMask = WuwaEyeAuthoredHighlightRamp(authoredRaw);
                float authoredWeight = max(upperRaw + lowerRaw, 1e-5);
                float3 authoredTint = (
                    upperRaw * _EyeHighlightColor.rgb * max(_EyeHighlightStrength, 0.0) +
                    lowerRaw * _EyeSecondHighlightColor.rgb * max(_EyeSecondHighlightStrength, 0.0)) /
                    authoredWeight;
                color += authoredMask * authoredTint * _EmissionStrength;

                float egMask = 0.0;
                #if defined(_WUWA_EYE_EG_ON)
                    float2 lightOffset = float2(
                        dot(lightDirWS, tangentWS),
                        dot(lightDirWS, bitangentWS)) *
                        _EyeEGLightFollow * tangentValid;
                    float2 egMappedUV = WuwaEyeAffineUV(
                        uv,
                        _EyeEGUVRow0,
                        _EyeEGUVRow1);
                    float2 safeEGScale = max(abs(_EyeEGScale.xy), 1e-4.xx);
                    float2 egUV = (
                        egMappedUV - _EyeEGCenter.xy - _EyeEGOffset.xy - lightOffset) /
                        safeEGScale + 0.5.xx;
                    egMask = SAMPLE_TEXTURE2D(_EyeEG, sampler_EyeEG, egUV).r;
                    float fresnel = pow(
                        saturate(1.0 - dot(normalWS, viewDirWS)),
                        max(_EyeEGFresnelPower, 0.1));
                    color += egMask * fresnel * _EyeEGColor.rgb *
                        max(_EyeEGStrength, 0.0) * _EmissionStrength;
                #endif

                color += baseColor * max(_EyeBaseEmissionStrength, 0.0);
                if (_EyeDebugView > 0.5 && _EyeDebugView < 1.5)
                    return half4(baseSample.aaa, 1.0);
                if (_EyeDebugView > 1.5 && _EyeDebugView < 2.5)
                    return half4(hetMask.xxx, 1.0);
                if (_EyeDebugView > 2.5 && _EyeDebugView < 3.5)
                    return half4(hdmf.rrr, 1.0);
                if (_EyeDebugView > 3.5 && _EyeDebugView < 4.5)
                    return half4(hdmf.ggg, 1.0);
                if (_EyeDebugView > 4.5 && _EyeDebugView < 5.5)
                    return half4(hdmf.bbb, 1.0);
                if (_EyeDebugView > 5.5 && _EyeDebugView < 6.5)
                    return half4(hdmf.aaa, 1.0);
                if (_EyeDebugView > 6.5 && _EyeDebugView < 7.5)
                    return half4(pupilMask.xxx, 1.0);
                if (_EyeDebugView > 7.5)
                    return half4(egMask.xxx, 1.0);
                return half4(color, baseSample.a);
            }
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
