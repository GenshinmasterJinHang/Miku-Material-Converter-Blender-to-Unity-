// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/HSR/Body"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("Legacy MainTex, optional", 2D) = "white" {}
        _LightMap ("Light Map", 2D) = "white" {}
        _BodyCoolRamp ("Body Cool Ramp", 2D) = "white" {}
        _BodyWarmRamp ("Body Warm Ramp", 2D) = "white" {}
        _StockingsMap ("Stockings Map", 2D) = "white" {}
        [MainColor] _BaseColorTint ("Base Color Tint", Color) = (1,1,1,1)
        [Toggle(_AREA_UPPER_BODY)] _AreaUpperBody ("Area Upper Body", Float) = 0
        [Toggle(_AREA_LOWER_BODY)] _AreaLowerBody ("Area Lower Body", Float) = 0
        [Toggle(_HSR_STOCKINGS_ON)] _UseStockings ("Use Stockings", Float) = 0
        _IndirectLightUsage ("Indirect Light Usage", Range(0,2)) = 0.35
        _IndirectLightOcclusionUsage ("Indirect Light AO Usage", Range(0,1)) = 0.7
        _IndirectLightMixBaseColor ("Indirect Mix Base Color", Range(0,1)) = 1
        _IndirectLightFlattenNormal ("Indirect Flatten Normal", Range(0,1)) = 0.8
        _ShadowThresholdCenter ("Shadow Threshold Center", Range(-1,1)) = 0
        _ShadowThresholdSoftness ("Shadow Threshold Softness", Range(0.001,1)) = 0.035
        _ShadowRampOffset ("Shadow Ramp Offset", Range(0,1)) = 0.75
        _BodyRampRowCount ("Body Ramp Row Count", Float) = 8
        _MainLightColorUsage ("Main Light Color Usage", Range(0,1)) = 0.35
        _SpecularExponent ("Specular Exponent", Range(1,512)) = 48
        _SpecularKsNonMetal ("Specular Ks NonMetal", Range(0,1)) = 0.035
        _SpecularKsMetal ("Specular Ks Metal", Range(0,4)) = 1.1
        _SpecularBrightness ("Specular Brightness", Range(0,4)) = 1
        _MetallicLightMapTarget ("Metallic LightMap A Target", Range(0,1)) = 0.52
        _MetallicLightMapWidth ("Metallic LightMap A Width", Range(0.001,1)) = 0.08
        _StockingsTransitionPower ("Stockings Transition Power", Range(0.1,8)) = 1
        _StockingsTransitionHardness ("Stockings Transition Hardness", Range(0,1)) = 0
        _StockingsTextureUsage ("Stockings Texture Usage", Range(0,1)) = 0.2
        _StockingsDetailStrength ("Stockings Detail Strength", Range(0,1)) = 0.2
        _StockingsDetailMin ("Stockings Detail Min", Range(0,1)) = 0.85
        _StockingsDarkColor ("Stockings Dark Color", Color) = (0,0,0,1)
        _StockingsTransitionColor ("Stockings Transition Color", Color) = (0.360381,0.242986,0.358131,1)
        _StockingsLightColor ("Stockings Light Color", Color) = (1.8,1.48299,0.856821,1)
        _StockingsTransitionThreshold ("Stockings Transition Threshold", Range(0,1)) = 0.58
        _StockingsDebugMode ("Stockings Debug Mode", Range(0,4)) = 0
        _RimLightBrightness ("Rim Brightness", Range(0,4)) = 0.18
        _RimLightTintColor ("Rim Tint", Color) = (0.92,0.96,1,1)
        _RimLightWidth ("Rim Width", Range(0,10)) = 1
        _RimLightThreshold ("Rim Depth Threshold", Range(0,1)) = 0.03
        _RimLightFadeout ("Rim Fadeout", Range(0.001,1)) = 0.2
        _FresnelPower ("Fresnel Power", Range(0.1,8)) = 3
        _FresnelClamp ("Fresnel Clamp", Range(0,1)) = 1
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.001
        _OutlineReferenceDistance ("Outline Reference Distance", Float) = 5
        _OutlineDistanceScale ("Outline Distance Scale", Range(0,1)) = 1
        _OutlineGamma ("Outline Gamma", Range(0.1,4)) = 1.35
        _OutlineColorTint ("Outline Color Tint", Color) = (1,1,1,1)
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
            #pragma vertex HSRVert
            #pragma fragment HSRFrag
            #pragma shader_feature_local _AREA_UPPER_BODY
            #pragma shader_feature_local _AREA_LOWER_BODY
            #pragma shader_feature_local _HSR_STOCKINGS_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "HSRCommon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_LightMap); SAMPLER(sampler_LightMap);
            TEXTURE2D(_BodyCoolRamp); SAMPLER(sampler_BodyCoolRamp);
            TEXTURE2D(_BodyWarmRamp); SAMPLER(sampler_BodyWarmRamp);
            TEXTURE2D(_StockingsMap); SAMPLER(sampler_StockingsMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _StockingsMap_ST; float4 _BaseColorTint;
                float _IndirectLightUsage; float _IndirectLightOcclusionUsage; float _IndirectLightMixBaseColor; float _IndirectLightFlattenNormal;
                float _ShadowThresholdCenter; float _ShadowThresholdSoftness; float _ShadowRampOffset; float _BodyRampRowCount; float _MainLightColorUsage;
                float _SpecularExponent; float _SpecularKsNonMetal; float _SpecularKsMetal; float _SpecularBrightness; float _MetallicLightMapTarget; float _MetallicLightMapWidth;
                float _StockingsTransitionPower; float _StockingsTransitionHardness; float _StockingsTextureUsage; float _StockingsDetailStrength; float _StockingsDetailMin; float4 _StockingsDarkColor; float4 _StockingsTransitionColor; float4 _StockingsLightColor; float _StockingsTransitionThreshold; float _StockingsDebugMode;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineGamma; float4 _OutlineColorTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float3 smoothNormalOS : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewDirWS : TEXCOORD2; float2 uvBase : TEXCOORD3; float2 uvStocking : TEXCOORD4; float4 shadowCoord : TEXCOORD5; };
            Varyings HSRVert(Attributes input) { Varyings output; VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz); VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS); output.positionCS = pos.positionCS; output.positionWS = pos.positionWS; output.normalWS = normalize(normal.normalWS); output.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS)); output.uvBase = TRANSFORM_TEX(input.uv, _BaseMap); output.uvStocking = TRANSFORM_TEX(input.uv, _StockingsMap); output.shadowCoord = TransformWorldToShadowCoord(pos.positionWS); return output; }
            half4 HSRFrag(Varyings input) : SV_Target
            {
                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uvBase) * _BaseColorTint;
                float4 lightMap = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, input.uvBase);
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 lightDirWS = normalize(mainLight.direction);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float3 baseColor = baseSample.rgb;
                #if defined(_HSR_STOCKINGS_ON)
                    float4 stockingsMap = SAMPLE_TEXTURE2D(_StockingsMap, sampler_StockingsMap, input.uvStocking);
                    float stockingsFac = 1.0;
                    float3 stockingsEffect = HSR_ComputeStockingsEffect(normalWS, viewDirWS, stockingsMap, _StockingsTransitionPower, _StockingsTransitionHardness, _StockingsTextureUsage, _StockingsDetailStrength, _StockingsDetailMin, _StockingsDarkColor.rgb, _StockingsTransitionColor.rgb, _StockingsLightColor.rgb, _StockingsTransitionThreshold, stockingsFac);
                    float stockingsDebugMode = round(_StockingsDebugMode);
                    if (stockingsDebugMode == 1) return half4(stockingsMap.rrr, 1.0);
                    if (stockingsDebugMode == 2) return half4(stockingsMap.ggg, 1.0);
                    if (stockingsDebugMode == 3) return half4(stockingsMap.bbb, 1.0);
                    if (stockingsDebugMode == 4) return half4(stockingsFac.xxx, 1.0);
                    baseColor *= stockingsEffect;
                #endif
                float mainLightShadow = HSR_BodyMainShadow(normalWS, lightDirWS, lightMap, _ShadowThresholdCenter, _ShadowThresholdSoftness) * mainLight.shadowAttenuation;
                float3 rampColor = HSR_SampleRamp(mainLightShadow, lightMap.a, lightDirWS, TEXTURE2D_ARGS(_BodyCoolRamp, sampler_BodyCoolRamp), TEXTURE2D_ARGS(_BodyWarmRamp, sampler_BodyWarmRamp), _BodyRampRowCount, _ShadowRampOffset);
                float3 indirect = HSR_SampleSH_Indirect(normalWS, _IndirectLightFlattenNormal) * _IndirectLightUsage;
                indirect *= lerp(1.0, lightMap.r, _IndirectLightOcclusionUsage);
                indirect *= lerp(1.0.xxx, baseColor, _IndirectLightMixBaseColor);
                float metallic = 0.0;
                #if defined(_AREA_UPPER_BODY) || defined(_AREA_LOWER_BODY)
                    metallic = HSR_ExtractMetallicFromLightMap(lightMap.a, _MetallicLightMapTarget, _MetallicLightMapWidth);
                #endif
                float3 specular = HSR_ComputeSpecular(baseColor, lightMap, normalWS, viewDirWS, lightDirWS, mainLight.color.rgb, _SpecularExponent, _SpecularKsNonMetal, _SpecularKsMetal, _SpecularBrightness, metallic);
                float3 mainLightColor = lerp(HSR_Desaturate(mainLight.color.rgb), mainLight.color.rgb, _MainLightColorUsage);
                float3 direct = mainLightColor * baseColor * rampColor;
                float3 rim = HSR_FresnelRimLight(
                    normalWS,
                    viewDirWS,
                    _RimLightTintColor.rgb,
                    _RimLightBrightness,
                    _FresnelPower,
                    _FresnelClamp);
                float3 finalColor = indirect + direct + specular + rim;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual
            Blend One Zero
            ColorMask RGBA
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HSRCommon.hlsl"
            TEXTURE2D(_BodyCoolRamp); SAMPLER(sampler_BodyCoolRamp);
            TEXTURE2D(_BodyWarmRamp); SAMPLER(sampler_BodyWarmRamp);
            CBUFFER_START(UnityPerMaterial)
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineGamma; float4 _OutlineColorTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float3 smoothNormalOS : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings OutlineVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                float3 outlineNormalOS = HSR_GetOutlineNormalOS(input.smoothNormalOS, input.normalOS);
                float3 outlineNormalWS = normalize(TransformObjectToWorldNormal(outlineNormalOS));
                output.positionCS = HSR_ExtrudeOutlinePositionCS(pos.positionWS, outlineNormalWS, _OutlineWidth, _OutlineReferenceDistance, _OutlineDistanceScale);
                return output;
            }
            half4 OutlineFrag(Varyings input) : SV_Target
            {
                float3 outlineColor = HSR_BodyOutlineColor(TEXTURE2D_ARGS(_BodyCoolRamp, sampler_BodyCoolRamp), TEXTURE2D_ARGS(_BodyWarmRamp, sampler_BodyWarmRamp), _OutlineGamma) * _OutlineColorTint.rgb;
                return half4(outlineColor, 1.0);
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
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings NormalsVert(Attributes input) { Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); output.normalWS = TransformObjectToWorldNormal(input.normalOS); return output; }
            half4 NormalsFrag(Varyings input) : SV_Target { return half4(normalize(input.normalWS) * 0.5h + 0.5h, 1); }
            ENDHLSL
        }
    }
    CustomEditor "Miku.ShaderConverter.Editor.MikuManualTextureShaderGUI"
}
