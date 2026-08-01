// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Wuwa/Eye"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("Legacy MainTex, optional", 2D) = "white" {}
        _EyeHET ("Eye HET", 2D) = "white" {}
        _EyeEG ("Eye EG", 2D) = "black" {}
        _EmissionMap ("Emission Map", 2D) = "black" {}
        [MainColor] _BaseColorTint ("Base Color Tint", Color) = (1,1,1,1)
        [Toggle(_AREA_EYE)] _AreaEye ("Area Eye", Float) = 1
        _EyeBaseCurvePower ("Eye Base Curve Power", Range(0.1,4)) = 1.44
        _EyeBaseBrightness ("Eye Base Brightness", Range(0,4)) = 1.8
        _EyeTopShadowStrength ("Eye Top Shadow Strength", Range(0,1)) = 0.32
        _EyeHighlightStrength ("Eye Highlight Strength", Range(0,1)) = 0.1
        _EyeSecondHighlightStrength ("Eye Second Highlight Strength", Range(0,1)) = 0.08
        _EyeHighlightColor ("Eye Highlight Color", Color) = (0.82,0.92,1,1)
        _EyeSecondHighlightColor ("Eye Second Highlight Color", Color) = (0.65,0.82,1,1)
        _EyePackedHighlightColor ("Eye Packed Highlight Color", Color) = (1,0.72,0.9,1)
        _EyeParallaxStrength ("Eye Parallax Strength", Range(-0.1,0.1)) = 0
        _EmissionStrength ("Packed Highlight Strength", Range(0,4)) = 1.1
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
            #pragma shader_feature_local _WUWA_EYE_EG_ON
            #pragma shader_feature_local _WUWA_EMISSION_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WuwaCommon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EyeHET); SAMPLER(sampler_EyeHET);
            TEXTURE2D(_EyeEG); SAMPLER(sampler_EyeEG);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint;
                float _EyeBaseCurvePower; float _EyeBaseBrightness; float _EyeTopShadowStrength; float _EyeHighlightStrength; float _EyeSecondHighlightStrength; float4 _EyeHighlightColor; float4 _EyeSecondHighlightColor; float4 _EyePackedHighlightColor; float _EyeParallaxStrength; float _EmissionStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 viewDirWS : TEXCOORD0; float2 uv : TEXCOORD1; };
            Varyings WuwaEyeVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 WuwaEyeFrag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 parallax = normalize(input.viewDirWS.xy + 1e-5.xx) * _EyeParallaxStrength;
                uv = saturate(uv + parallax);
                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColorTint;
                float3 baseColor = saturate(Wuwa_ApplyPowerCurve(baseSample.rgb, _EyeBaseCurvePower, _EyeBaseBrightness));
                float topMask = smoothstep(0.42, 1.0, uv.y);
                float topShadow = saturate(topMask * _EyeTopShadowStrength);
                float3 color = baseColor * lerp(1.0, 0.62, topShadow);
                #if defined(_WUWA_EYE_HET_ON)
                    float4 het = SAMPLE_TEXTURE2D(_EyeHET, sampler_EyeHET, uv);
                    float hetMask = saturate(het.r * het.a);
                    color += hetMask * _EyeHighlightColor.rgb * _EyeHighlightStrength;
                #endif
                #if defined(_WUWA_EYE_EG_ON)
                    float4 eg = SAMPLE_TEXTURE2D(_EyeEG, sampler_EyeEG, uv);
                    float egMask = saturate(eg.r * eg.a);
                    color += egMask * _EyeSecondHighlightColor.rgb * _EyeSecondHighlightStrength;
                #endif
                #if defined(_WUWA_EMISSION_ON)
                    float4 packedHighlight = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv);
                    float packedMask = saturate(packedHighlight.r * packedHighlight.a);
                    color += packedMask * _EyePackedHighlightColor.rgb * _EmissionStrength;
                #endif
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
