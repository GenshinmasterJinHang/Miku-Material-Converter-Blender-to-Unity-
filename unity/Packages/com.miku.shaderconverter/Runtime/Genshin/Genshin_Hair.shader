// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Genshin/Hair"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("Legacy MainTex, optional", 2D) = "white" {}
        _LightMap ("Hair Light Map", 2D) = "white" {}
        _HairRampMap ("Hair Ramp", 2D) = "white" {}
        _HairSpecMap ("Hair View Highlight", 2D) = "black" {}
        _MetalMap ("Metal Map", 2D) = "black" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        [MainColor] _BaseColorTint ("Base Color Tint", Color) = (1,1,1,1)
        [Toggle(_AREA_HAIR)] _AreaHair ("Area Hair", Float) = 1
        _InNight ("In Night", Range(0,1)) = 0
        _HairRange ("Hair Range", Range(0,1)) = 0.5
        _HairShadowSmooth ("Hair Shadow Smooth", Range(0.001,1)) = 0.12
        _HairDarkShadowArea ("Hair Dark Shadow Area", Range(-1,1)) = -0.45
        _HairDarkShadowSmooth ("Hair Dark Shadow Smooth", Range(-1,1)) = -0.8
        _HairSmoothShadowIntensity ("Hair Smooth Shadow Intensity", Range(0,2)) = 0.65
        _HairViewSpecularThreshold ("Hair View Specular Threshold", Range(0,1)) = 0.75
        _HairSpecAreaBaseline ("Hair Spec Area Baseline", Range(0,1)) = 0.15
        _HairAccGroveBaseline ("Hair Accessory Groove Baseline", Range(0,1)) = 0.9
        _HairViewSpecularIntensity ("Hair View Specular Intensity", Range(0,4)) = 0.75
        _HairSpecIntensity ("Hair View Highlight Intensity", Range(0,2)) = 0.45
        _MainLightColorUsage ("Main Light Color Usage", Range(0,1)) = 0.15
        _IndirectLightUsage ("Indirect Light Usage", Range(0,2)) = 0.1
        _HighlightCompression ("Highlight Compression", Range(0,1)) = 1
        _HighlightKnee ("Highlight Knee", Range(0,1)) = 0.72
        _HighlightCeiling ("Highlight Ceiling", Range(0,1)) = 0.98
        _EmissionIntensity ("Emission Intensity", Range(0,4)) = 0
        _RimLightBrightness ("Rim Brightness", Range(0,4)) = 0.08
        _RimLightTintColor ("Rim Tint", Color) = (1,0.92,0.88,1)
        _RimLightWidth ("Rim Width", Range(0,10)) = 1
        _RimLightThreshold ("Rim Depth Threshold", Range(0,1)) = 0.03
        _RimLightFadeout ("Rim Fadeout", Range(0.001,1)) = 0.2
        [HideInInspector] _FresnelPower ("Legacy Fresnel Power", Range(0.1,8)) = 2
        [HideInInspector] _FresnelClamp ("Legacy Fresnel Clamp", Range(0,1)) = 1
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.0015
        _OutlineReferenceDistance ("Outline Reference Distance", Float) = 5
        _OutlineDistanceScale ("Outline Distance Scale", Range(0,1)) = 1
        _OutlineGamma ("Outline Gamma", Range(0.1,4)) = 1.6
        _OutlineColorTint ("Outline Color Tint", Color) = (0.2,0.08,0.1,1)
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
            #pragma vertex GenshinVert
            #pragma fragment GenshinFrag
            #pragma shader_feature_local _AREA_HAIR
            #pragma shader_feature_local _GENSHIN_METALMAP_ON
            #pragma shader_feature_local _GENSHIN_EMISSION_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GenshinCommon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_LightMap); SAMPLER(sampler_LightMap);
            TEXTURE2D(_HairRampMap); SAMPLER(sampler_HairRampMap);
            TEXTURE2D(_HairSpecMap); SAMPLER(sampler_HairSpecMap);
            TEXTURE2D(_MetalMap); SAMPLER(sampler_MetalMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint;
                float _InNight; float _HairRange; float _HairShadowSmooth; float _HairDarkShadowArea; float _HairDarkShadowSmooth; float _HairSmoothShadowIntensity;
                float _HairViewSpecularThreshold; float _HairSpecAreaBaseline; float _HairAccGroveBaseline; float _HairViewSpecularIntensity; float _HairSpecIntensity;
                float _MainLightColorUsage; float _IndirectLightUsage; float _EmissionIntensity;
                float _HighlightCompression; float _HighlightKnee; float _HighlightCeiling;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineGamma; float4 _OutlineColorTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float4 vertexColor : COLOR; float3 smoothNormalOS : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewDirWS : TEXCOORD2; float2 uv : TEXCOORD3; float4 shadowCoord : TEXCOORD4; float4 vertexColor : COLOR; };
            Varyings GenshinVert(Attributes input)
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
                output.vertexColor = input.vertexColor;
                return output;
            }
            half4 GenshinFrag(Varyings input) : SV_Target
            {
                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColorTint;
                float4 lightMap = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, input.uv);
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
                float2 viewNormalUV = saturate(normalVS.xy * 0.5 + 0.5);
                float4 metalMap = SAMPLE_TEXTURE2D(_MetalMap, sampler_MetalMap, viewNormalUV);
                float hairSpecMask = SAMPLE_TEXTURE2D(_HairSpecMap, sampler_HairSpecMap, viewNormalUV).r;
                float3 lightDirWS = normalize(mainLight.direction);
                float ndotLRaw = dot(normalWS, lightDirWS);
                float3 mainLightColor = lerp(1.0.xxx, mainLight.color.rgb, _MainLightColorUsage);
                float3 diffuse = Genshin_HairDoubleShadow(baseSample.rgb, lightMap, input.vertexColor, ndotLRaw, mainLightColor, mainLight.shadowAttenuation, _InNight, _HairDarkShadowSmooth, _HairDarkShadowArea, _HairShadowSmooth, _HairSmoothShadowIntensity, _HighlightCompression, TEXTURE2D_ARGS(_HairRampMap, sampler_HairRampMap));
                float3 indirect = Genshin_SampleSH_Indirect(normalWS) * _IndirectLightUsage * baseSample.rgb;
                float3 specular = Genshin_HairSpecular(baseSample.rgb, lightMap, metalMap, normalWS, viewDirWS, lightDirWS, ndotLRaw, _HairRange, _HairViewSpecularThreshold, _HairSpecAreaBaseline, _HairAccGroveBaseline, _HairViewSpecularIntensity, _HighlightCompression);
                specular += Genshin_HairViewHighlight(baseSample.rgb, lightMap, hairSpecMask, _HairSpecIntensity, _HighlightCompression);
                float3 emission = Genshin_EmissionPulse(baseSample.a, lightMap.a, baseSample.rgb, mainLight.color.rgb, _EmissionIntensity);
                #if defined(_GENSHIN_EMISSION_ON)
                    emission += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionIntensity;
                #endif
                float3 nonEmissive = Genshin_CompressNonEmissive(indirect + diffuse + specular, _HighlightCompression, _HighlightKnee, _HighlightCeiling);
                return half4(nonEmissive + emission, 1.0);
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
                float4 _BaseMap_ST; float4 _BaseColorTint;
                float _InNight; float _HairRange; float _HairShadowSmooth; float _HairDarkShadowArea; float _HairDarkShadowSmooth; float _HairSmoothShadowIntensity;
                float _HairViewSpecularThreshold; float _HairSpecAreaBaseline; float _HairAccGroveBaseline; float _HairViewSpecularIntensity; float _HairSpecIntensity;
                float _MainLightColorUsage; float _IndirectLightUsage; float _EmissionIntensity;
                float _HighlightCompression; float _HighlightKnee; float _HighlightCeiling;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineGamma; float4 _OutlineColorTint;
            CBUFFER_END
            #include "Packages/com.miku.shaderconverter/Runtime/GameToon/MikuGameToonScreenRimPass.hlsl"
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
            #include "GenshinCommon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineGamma; float4 _OutlineColorTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 vertexColor : COLOR; float3 smoothNormalOS : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 vertexColor : COLOR; };
            Varyings OutlineVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                float3 outlineNormalOS = Genshin_GetOutlineNormalOS(input.smoothNormalOS, input.normalOS);
                float3 outlineNormalWS = normalize(TransformObjectToWorldNormal(outlineNormalOS));
                float outlineWidth = Genshin_DistanceCompensatedOutlineWidth(pos.positionWS, _OutlineWidth, _OutlineReferenceDistance, _OutlineDistanceScale) * input.vertexColor.a;
                output.positionCS = TransformWorldToHClip(pos.positionWS + outlineNormalWS * outlineWidth);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.vertexColor = input.vertexColor;
                return output;
            }
            half4 OutlineFrag(Varyings input) : SV_Target
            {
                float3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                return half4(Genshin_OutlineColor(baseColor, input.vertexColor, _OutlineGamma, _OutlineColorTint.rgb), 1.0);
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
