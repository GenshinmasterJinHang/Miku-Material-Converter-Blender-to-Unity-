// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Genshin/Body"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("Legacy MainTex, optional", 2D) = "white" {}
        _LightMap ("Light Map", 2D) = "white" {}
        _ShadowRampMap ("Shadow Ramp", 2D) = "white" {}
        _MetalMap ("Metal Map", 2D) = "black" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        [MainColor] _BaseColorTint ("Base Color Tint", Color) = (1,1,1,1)
        [Toggle(_AREA_UPPER_BODY)] _AreaUpperBody ("Area Upper Body", Float) = 0
        [Toggle(_AREA_LOWER_BODY)] _AreaLowerBody ("Area Lower Body", Float) = 0
        [Toggle(_AREA_CLOTH)] _AreaCloth ("Area Cloth", Float) = 0
        [Toggle(_AREA_SKIN)] _AreaSkin ("Area Skin", Float) = 0
        _InNight ("In Night", Range(0,1)) = 0
        _BodyShadowSmooth ("Body Shadow Smooth", Range(0.001,1)) = 0.12
        _MainShadowInfluence ("Realtime Main Shadow Influence", Range(0,1)) = 0.25
        _MainLightColorUsage ("Main Light Color Usage", Range(0,1)) = 1
        _IndirectLightUsage ("Indirect Light Usage", Range(0,2)) = 0.1
        _StrokeRange ("Stroke Range", Range(0,1)) = 0.35
        _PatternRange ("Pattern Range", Range(0,1)) = 0.7
        _MetalIntensity ("Metal Intensity", Range(0,4)) = 1
        _MetalMapColor ("Metal Map Color", Color) = (1,1,1,1)
        _Bright ("Tutorial Bright Threshold", Range(0,1)) = 0.99
        _Grey ("Tutorial Grey Width", Range(0.001,2)) = 1.14
        _Dark ("Tutorial Dark Offset", Range(-1,1)) = 0.5
        _Gloss ("Tutorial Gloss Power", Range(1,128)) = 16
        _GlossStrength ("Tutorial Gloss Strength", Range(0,4)) = 1
        _HighlightCompression ("Highlight Compression", Range(0,1)) = 1
        _HighlightKnee ("Highlight Knee", Range(0,1)) = 0.72
        _HighlightCeiling ("Highlight Ceiling", Range(0,1)) = 0.98
        _SkinSSSIntensity ("Skin SSS Intensity", Range(0,1)) = 0
        _SSSColor ("SSS Color", Color) = (1,0.5,0.4,1)
        _SSSArea ("SSS Area", Range(0,1)) = 0.30
        _SkinToneBrightness ("Skin Tone Brightness", Range(0,2)) = 1
        _SkinToneWhitening ("Skin Tone Whitening", Range(0,1)) = 0
        _SkinToneTarget ("Skin Tone Target", Color) = (1,0.93,0.90,1)
        _SkinMaskDebugMode ("Skin Mask Debug Mode", Range(0,1)) = 0
        _EmissionIntensity ("Emission Intensity", Range(0,4)) = 0
        [Toggle(_GENSHIN_DOUBLE_SIDED)] _DoubleSided ("Double Sided (Tutorial Back Pass)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Int) = 2
        _BackUV1 ("Back Face Use UV1", Range(0,1)) = 0
        [HideInInspector] _UseUv1Backface ("Use UV1 Backface", Float) = 0
        [HideInInspector] _MikuGenshinMaterialStateVersion ("Miku Genshin Material State Version", Float) = 0
        [Enum(None,0,Cutout,1,DiffuseAlphaEmission,2)] _DiffuseA ("Base Alpha Mode", Float) = 0
        _Cutoff ("Cutout Threshold", Range(0,1)) = 1
        [HDR] _Glow ("Glow (Emission)", Color) = (1,1,1,1)
        _Flicker ("Flicker Speed", Float) = 0.8
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        _LightmapA0 ("1.0 Ramp Row", Range(1,5)) = 1
        _LightmapA1 ("0.7 Ramp Row", Range(1,5)) = 4
        _LightmapA2 ("0.5 Ramp Row", Range(1,5)) = 3
        _LightmapA3 ("0.3 Ramp Row", Range(1,5)) = 5
        _LightmapA4 ("0.0 Ramp Row", Range(1,5)) = 2
        _RimLightBrightness ("Rim Brightness", Range(0,4)) = 0.08
        _RimLightTintColor ("Rim Tint", Color) = (1,0.92,0.88,1)
        _RimLightWidth ("Rim Width", Range(0,10)) = 1
        _RimLightThreshold ("Rim Depth Threshold", Range(0,1)) = 0.03
        _RimLightFadeout ("Rim Fadeout", Range(0.001,1)) = 0.2
        [HideInInspector] _FresnelPower ("Legacy Fresnel Power", Range(0.1,8)) = 2
        [HideInInspector] _FresnelClamp ("Legacy Fresnel Clamp", Range(0,1)) = 1
        _FresnelStrength ("Tutorial Fresnel Strength", Range(0,4)) = 0
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.0015
        _OutlineReferenceDistance ("Outline Reference Distance", Float) = 5
        _OutlineDistanceScale ("Outline Distance Scale", Range(0,1)) = 1
        _OutlineGamma ("Outline Gamma", Range(0.1,4)) = 1.6
        _OutlineColorTint ("Outline Color Tint", Color) = (0.2,0.08,0.1,1)
        _OutlineColorMode ("Outline Color Mode (0 Tint, 1 LightMap Regions)", Range(0,1)) = 1
        _OutlineColor0 ("Outline Color 1", Color) = (0.2,0.08,0.1,1)
        _OutlineColor1 ("Outline Color 2", Color) = (0.2,0.08,0.1,1)
        _OutlineColor2 ("Outline Color 3", Color) = (0.2,0.08,0.1,1)
        _OutlineColor3 ("Outline Color 4", Color) = (0.2,0.08,0.1,1)
        _OutlineColor4 ("Outline Color 5", Color) = (0.2,0.08,0.1,1)
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
            #pragma shader_feature_local _AREA_UPPER_BODY
            #pragma shader_feature_local _AREA_LOWER_BODY
            #pragma shader_feature_local _AREA_CLOTH
            #pragma shader_feature_local _AREA_SKIN
            #pragma shader_feature_local _GENSHIN_METALMAP_ON
            #pragma shader_feature_local _GENSHIN_NORMALMAP_ON
            #pragma shader_feature_local _GENSHIN_EMISSION_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #if UNITY_VERSION >= 60010000
                #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #else
                #pragma multi_compile _ _FORWARD_PLUS
            #endif
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GenshinCommon.hlsl"
            #include "../GameToon/MikuGameToonSkin.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_LightMap); SAMPLER(sampler_LightMap);
            TEXTURE2D(_ShadowRampMap); SAMPLER(sampler_ShadowRampMap);
            TEXTURE2D(_MetalMap); SAMPLER(sampler_MetalMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint;
                float _InNight; float _BodyShadowSmooth; float _MainShadowInfluence; float _MainLightColorUsage; float _IndirectLightUsage;
                float _StrokeRange; float _PatternRange; float _MetalIntensity; float _EmissionIntensity; float4 _MetalMapColor;
                float _Bright; float _Grey; float _Dark; float _Gloss; float _GlossStrength;
                float _HighlightCompression; float _HighlightKnee; float _HighlightCeiling;
                float _SkinSSSIntensity; float4 _SSSColor; float _SSSArea; float _SkinToneBrightness; float _SkinToneWhitening; float4 _SkinToneTarget; float _SkinMaskDebugMode;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp; float _FresnelStrength;
                float _BackUV1; float _UseUv1Backface; float _DiffuseA; float _Cutoff; float4 _Glow; float _Flicker;
                float _BumpScale;
                float _LightmapA0; float _LightmapA1; float _LightmapA2; float _LightmapA3; float _LightmapA4;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineGamma; float4 _OutlineColorTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; float4 vertexColor : COLOR; float4 smoothNormalData : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewDirWS : TEXCOORD2; float2 uv : TEXCOORD3; float4 shadowCoord : TEXCOORD4; float2 uv1 : TEXCOORD5; float3 tangentWS : TEXCOORD6; float3 bitangentWS : TEXCOORD7; float4 vertexColor : COLOR; };
            Varyings GenshinVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = normalize(normal.normalWS);
                output.tangentWS = normalize(normal.tangentWS);
                output.bitangentWS = normalize(normal.bitangentWS);
                output.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uv1 = TRANSFORM_TEX(input.uv1, _BaseMap);
                output.shadowCoord = GetShadowCoord(pos);
                output.vertexColor = input.vertexColor;
                return output;
            }
            half4 GenshinFrag(Varyings input) : SV_Target
            {
                float2 sampleUV = input.uv;
                float4 rawBaseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sampleUV);
                float4 baseSample = rawBaseSample * _BaseColorTint;
                Genshin_ApplyBaseAlphaCoverage(baseSample.a, _DiffuseA, _Cutoff);
                float4 lightMap = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, sampleUV);
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 normalWS = normalize(input.normalWS);
                #if defined(_GENSHIN_NORMALMAP_ON)
                    float3 normalTS = Genshin_DecodeNormalMap(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, sampleUV), _BumpScale);
                    normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS, input.bitangentWS, input.normalWS)));
                #endif
                float3 viewDirWS = normalize(input.viewDirWS);
                float3 lightDirWS = normalize(mainLight.direction);
                float3 mainLightColor = lerp(1.0.xxx, mainLight.color.rgb, _MainLightColorUsage);
                float directVisibility = Genshin_MainShadowVisibility(mainLight.shadowAttenuation, mainLight.distanceAttenuation, _MainShadowInfluence);
                float lightingSignal = Genshin_TutorialLightingSignal(dot(normalWS, lightDirWS), lightMap.g, _Dark, _Grey);
                float brightMask = step(_Bright, lightingSignal);
                float skinMask = MikuGameToonHighValueMask(lightMap.a);
                float3 skinBase = MikuGameToonApplySkinTone(baseSample.rgb, skinMask, _SkinToneBrightness, _SkinToneWhitening, _SkinToneTarget.rgb);
                Genshin_RampRowParams rampRows;
                rampRows.a0 = _LightmapA0;
                rampRows.a1 = _LightmapA1;
                rampRows.a2 = _LightmapA2;
                rampRows.a3 = _LightmapA3;
                rampRows.a4 = _LightmapA4;
                float3 diffuse = Genshin_BodyDiffuse(skinBase, lightMap, input.vertexColor, normalWS, lightDirWS, mainLightColor, mainLight.shadowAttenuation, mainLight.distanceAttenuation, _MainShadowInfluence, _Dark, _Grey, _BodyShadowSmooth, rampRows, _InNight, _HighlightCompression, TEXTURE2D_ARGS(_ShadowRampMap, sampler_ShadowRampMap));
                float metalMask = Genshin_TutorialMetalMask(lightMap.r);
                #if defined(_AREA_SKIN)
                    diffuse = lerp(diffuse, Genshin_ReferenceSkinTone(diffuse, _HighlightCompression), skinMask);
                #endif
                diffuse *= 1.0 - metalMask;
                float3 indirect = Genshin_SampleSH_Indirect(normalWS) * _IndirectLightUsage * skinBase * (1.0 - metalMask);
                float3 normalVS = normalize(TransformWorldToViewDir(normalWS, true));
                float metalSample = SAMPLE_TEXTURE2D(_MetalMap, sampler_MetalMap, normalVS.xy * 0.5 + 0.5).r;
                float3 specular = Genshin_TutorialSpecular(skinBase, lightMap, normalWS, viewDirWS, lightDirWS, mainLightColor, _Gloss, _GlossStrength, brightMask, directVisibility);
                float3 metal = Genshin_TutorialMetal(skinBase, lightMap, metalSample, _MetalMapColor.rgb, _MetalIntensity);
                specular += Genshin_TutorialFresnel(skinBase, normalWS, viewDirWS, _FresnelPower, _FresnelStrength);
                float litAmount = lightingSignal;
                float3 sss = MikuGameToonSkinSSS(skinBase, skinMask, normalWS, viewDirWS, lightDirWS, mainLight.color.rgb, litAmount, _SkinSSSIntensity, _SSSArea, _SSSColor.rgb) * directVisibility * (1.0 - metalMask);
                if (_SkinMaskDebugMode > 0.5) return half4(skinMask.xxx, 1.0);
                float3 emission = Genshin_EmissionPulse(baseSample.a, lightMap.a, baseSample.rgb, mainLight.color.rgb, _EmissionIntensity);
                #if defined(_GENSHIN_EMISSION_ON)
                    emission += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, sampleUV).rgb * _EmissionIntensity;
                #endif
                if (_DiffuseA > 1.5)
                    emission += Genshin_DiffuseAlphaEmission(baseSample.a, baseSample.rgb, _Glow, _Flicker) * _EmissionIntensity;
                float3 nonEmissive = Genshin_CompressNonEmissive(indirect + diffuse + metal + specular + sss, _HighlightCompression, _HighlightKnee, _HighlightCeiling);
                return half4(nonEmissive + emission, 1.0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "MikuGenshinBackface"
            Tags { "LightMode"="MikuGenshinBackface" }
            Cull Front
            ZWrite On
            ZTest LEqual
            Blend One Zero
            ColorMask RGBA
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex BackfaceVert
            #pragma fragment BackfaceFrag
            #pragma shader_feature_local _AREA_SKIN
            #pragma shader_feature_local _GENSHIN_METALMAP_ON
            #pragma shader_feature_local _GENSHIN_NORMALMAP_ON
            #pragma shader_feature_local _GENSHIN_EMISSION_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #if UNITY_VERSION >= 60010000
                #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #else
                #pragma multi_compile _ _FORWARD_PLUS
            #endif
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GenshinCommon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_LightMap); SAMPLER(sampler_LightMap);
            TEXTURE2D(_ShadowRampMap); SAMPLER(sampler_ShadowRampMap);
            TEXTURE2D(_MetalMap); SAMPLER(sampler_MetalMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint;
                float _InNight; float _BodyShadowSmooth; float _MainShadowInfluence; float _MainLightColorUsage; float _IndirectLightUsage;
                float _StrokeRange; float _PatternRange; float _MetalIntensity; float _EmissionIntensity; float4 _MetalMapColor;
                float _Bright; float _Grey; float _Dark; float _Gloss; float _GlossStrength;
                float _HighlightCompression; float _HighlightKnee; float _HighlightCeiling;
                float _SkinSSSIntensity; float4 _SSSColor; float _SSSArea; float _SkinToneBrightness; float _SkinToneWhitening; float4 _SkinToneTarget; float _SkinMaskDebugMode;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp; float _FresnelStrength;
                float _BackUV1; float _UseUv1Backface; float _DiffuseA; float _Cutoff; float4 _Glow; float _Flicker;
                float _BumpScale;
                float _LightmapA0; float _LightmapA1; float _LightmapA2; float _LightmapA3; float _LightmapA4;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineGamma; float4 _OutlineColorTint;
            CBUFFER_END
            struct BackfaceAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv1 : TEXCOORD1; float4 vertexColor : COLOR; };
            struct BackfaceVaryings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewDirWS : TEXCOORD2; float2 uv : TEXCOORD3; float4 shadowCoord : TEXCOORD4; float4 vertexColor : COLOR; };
            BackfaceVaryings BackfaceVert(BackfaceAttributes input)
            {
                BackfaceVaryings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = -normalize(TransformObjectToWorldNormal(input.normalOS));
                output.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS));
                output.uv = TRANSFORM_TEX(input.uv1, _BaseMap);
                output.shadowCoord = GetShadowCoord(pos);
                output.vertexColor = input.vertexColor;
                return output;
            }
            half4 BackfaceFrag(BackfaceVaryings input) : SV_Target
            {
                clip(_UseUv1Backface - 0.5);
                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColorTint;
                Genshin_ApplyBaseAlphaCoverage(baseSample.a, _DiffuseA, _Cutoff);
                float4 lightMap = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, input.uv);
                float3 normalWS = normalize(input.normalWS);
                #if defined(_GENSHIN_NORMALMAP_ON)
                    float3 normalTS = Genshin_DecodeNormalMap(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _BumpScale);
                    normalWS = normalize(TransformTangentToWorld(normalTS, Genshin_DerivativeTangentFrame(input.positionWS, normalWS, input.uv)));
                #endif
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 viewDirWS = normalize(input.viewDirWS);
                float3 lightDirWS = normalize(mainLight.direction);
                float3 mainLightColor = lerp(1.0.xxx, mainLight.color.rgb, _MainLightColorUsage);
                float directVisibility = Genshin_MainShadowVisibility(mainLight.shadowAttenuation, mainLight.distanceAttenuation, _MainShadowInfluence);
                float lightingSignal = Genshin_TutorialLightingSignal(dot(normalWS, lightDirWS), lightMap.g, _Dark, _Grey);
                Genshin_RampRowParams rows;
                rows.a0 = _LightmapA0; rows.a1 = _LightmapA1; rows.a2 = _LightmapA2; rows.a3 = _LightmapA3; rows.a4 = _LightmapA4;
                float3 diffuse = Genshin_BodyDiffuse(baseSample.rgb, lightMap, input.vertexColor, normalWS, lightDirWS, mainLightColor, mainLight.shadowAttenuation, mainLight.distanceAttenuation, _MainShadowInfluence, _Dark, _Grey, _BodyShadowSmooth, rows, _InNight, _HighlightCompression, TEXTURE2D_ARGS(_ShadowRampMap, sampler_ShadowRampMap));
                float metalMask = Genshin_TutorialMetalMask(lightMap.r);
                diffuse *= 1.0 - metalMask;
                float3 normalVS = normalize(TransformWorldToViewDir(normalWS, true));
                float metalSample = SAMPLE_TEXTURE2D(_MetalMap, sampler_MetalMap, normalVS.xy * 0.5 + 0.5).r;
                float3 direct = Genshin_TutorialSpecular(baseSample.rgb, lightMap, normalWS, viewDirWS, lightDirWS, mainLightColor, _Gloss, _GlossStrength, step(_Bright, lightingSignal), directVisibility);
                direct += Genshin_TutorialMetal(baseSample.rgb, lightMap, metalSample, _MetalMapColor.rgb, _MetalIntensity);
                direct += Genshin_TutorialFresnel(baseSample.rgb, normalWS, viewDirWS, _FresnelPower, _FresnelStrength);
                float3 emission = 0.0.xxx;
                #if defined(_GENSHIN_EMISSION_ON)
                    emission += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionIntensity;
                #endif
                if (_DiffuseA > 1.5)
                    emission += Genshin_DiffuseAlphaEmission(baseSample.a, baseSample.rgb, _Glow, _Flicker) * _EmissionIntensity;
                float3 indirect = Genshin_SampleSH_Indirect(normalWS) * _IndirectLightUsage * baseSample.rgb * (1.0 - metalMask);
                return half4(Genshin_CompressNonEmissive(indirect + diffuse + direct, _HighlightCompression, _HighlightKnee, _HighlightCeiling) + emission, 1.0);
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
            #include "GenshinCommon.hlsl"
            #define MIKU_GAME_TOON_ALPHA_COVERAGE 1
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint;
                float _InNight; float _BodyShadowSmooth; float _MainShadowInfluence; float _MainLightColorUsage; float _IndirectLightUsage;
                float _StrokeRange; float _PatternRange; float _MetalIntensity; float _EmissionIntensity; float4 _MetalMapColor;
                float _Bright; float _Grey; float _Dark; float _Gloss; float _GlossStrength;
                float _HighlightCompression; float _HighlightKnee; float _HighlightCeiling;
                float _SkinSSSIntensity; float4 _SSSColor; float _SSSArea; float _SkinToneBrightness; float _SkinToneWhitening; float4 _SkinToneTarget; float _SkinMaskDebugMode;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp; float _FresnelStrength;
                float _BackUV1; float _UseUv1Backface; float _DiffuseA; float _Cutoff; float4 _Glow; float _Flicker;
                float _BumpScale;
                float _LightmapA0; float _LightmapA1; float _LightmapA2; float _LightmapA3; float _LightmapA4;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineGamma; float4 _OutlineColorTint;
            CBUFFER_END
            #include "Packages/com.miku.shaderconverter/Runtime/GameToon/MikuGameToonScreenRimPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MikuToonOutline"
            Tags { "LightMode"="MikuToonOutline" }
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
            #include "GenshinCommon.hlsl"
            #include "Packages/com.miku.shaderconverter/Runtime/GameToon/MikuGameToonOutline.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_LightMap); SAMPLER(sampler_LightMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint; float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineGamma; float4 _OutlineColorTint;
                float _OutlineColorMode; float4 _OutlineColor0; float4 _OutlineColor1; float4 _OutlineColor2; float4 _OutlineColor3; float4 _OutlineColor4;
                float _DiffuseA; float _Cutoff;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float4 vertexColor : COLOR; float4 smoothNormalData : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float outlineCoverage : TEXCOORD1; float4 vertexColor : COLOR; };
            Varyings OutlineVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                float3 outlineNormalOS = MikuGameToonOutlineNormalTangentSpaceV2(
                    input.smoothNormalData, input.normalOS, input.tangentOS);
                output.positionCS = MikuGameToonOutlinePositionCSWithVertexMask(
                    pos.positionCS,
                    pos.positionWS,
                    outlineNormalOS,
                    _OutlineWidth,
                    _OutlineReferenceDistance,
                    _OutlineDistanceScale,
                    Genshin_OutlineVertexMask(input.vertexColor),
                    1.0);
                output.outlineCoverage = MikuGameToonOutlineCoverageWithVertexMask(
                    pos.positionWS,
                    1.0,
                    _OutlineWidth,
                    _OutlineReferenceDistance,
                    _OutlineDistanceScale,
                    Genshin_OutlineVertexMask(input.vertexColor),
                    1.0);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.vertexColor = input.vertexColor;
                return output;
            }
            half4 OutlineFrag(Varyings input) : SV_Target
            {
                MikuGameToonOutlineClipCoverage(input.outlineCoverage);
                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColorTint;
                Genshin_ApplyBaseAlphaCoverage(baseSample.a, _DiffuseA, _Cutoff);
                float4 lightMap = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, input.uv);
                float3 outlineColor = Genshin_OutlineRegionColor(
                    lightMap,
                    baseSample.rgb,
                    input.vertexColor,
                    _OutlineColor0.rgb,
                    _OutlineColor1.rgb,
                    _OutlineColor2.rgb,
                    _OutlineColor3.rgb,
                    _OutlineColor4.rgb,
                    _OutlineGamma,
                    _OutlineColorTint.rgb,
                    _OutlineColorMode);
                return half4(outlineColor, 1.0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Back
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment CoverageFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "GenshinCommon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float3 _LightDirection; float3 _LightPosition;
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint; float _DiffuseA; float _Cutoff;
            CBUFFER_END
            struct CoverageAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct CoverageVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CoverageVaryings ShadowVert(CoverageAttributes input)
            {
                CoverageVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 CoverageFrag(CoverageVaryings input) : SV_Target
            {
                float alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColorTint.a;
                Genshin_ApplyBaseAlphaCoverage(alpha, _DiffuseA, _Cutoff);
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Back
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment CoverageFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GenshinCommon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint; float _DiffuseA; float _Cutoff;
            CBUFFER_END
            struct CoverageAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct CoverageVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CoverageVaryings DepthVert(CoverageAttributes input) { CoverageVaryings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); output.uv = TRANSFORM_TEX(input.uv, _BaseMap); return output; }
            half4 CoverageFrag(CoverageVaryings input) : SV_Target { float alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColorTint.a; Genshin_ApplyBaseAlphaCoverage(alpha, _DiffuseA, _Cutoff); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GenshinCommon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint; float _DiffuseA; float _Cutoff;
            CBUFFER_END
            struct NormalsAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct NormalsVaryings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };
            NormalsVaryings NormalsVert(NormalsAttributes input) { NormalsVaryings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); output.normalWS = TransformObjectToWorldNormal(input.normalOS); output.uv = TRANSFORM_TEX(input.uv, _BaseMap); return output; }
            half4 NormalsFrag(NormalsVaryings input) : SV_Target { float alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColorTint.a; Genshin_ApplyBaseAlphaCoverage(alpha, _DiffuseA, _Cutoff); return half4(normalize(input.normalWS) * 0.5h + 0.5h, 1); }
            ENDHLSL
        }
    }
    CustomEditor "Miku.ShaderConverter.Editor.MikuManualTextureShaderGUI"
}
