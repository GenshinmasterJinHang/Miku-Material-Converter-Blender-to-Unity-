// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Wuwa/Body"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("Legacy MainTex, optional", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        [Enum(UnityNormalMap,0,WuwaPackedNRM,1)] _NormalMapEncoding ("Normal Map Encoding", Float) = 0
        _NormalScale ("Normal Scale", Range(0,2)) = 1
        _PackedMetallicScale ("Packed Metallic Scale", Range(0,2)) = 1
        _PackedRoughnessScale ("Packed Roughness Scale", Range(0,2)) = 1
        _OcclusionStrength ("Base Alpha Occlusion Strength", Range(0,1)) = 0
        _IDMap ("ID Map", 2D) = "white" {}
        [HideInInspector] _StockingsMap ("Stockings ID Source", 2D) = "black" {}
        _MatCap ("MatCap", 2D) = "white" {}
        _EmissionMap ("Emission Map", 2D) = "black" {}
        _BodyEmissionStrength ("Body Emission Strength", Range(0,4)) = 1
        [MainColor] _BaseColorTint ("Base Color Tint", Color) = (1,1,1,1)
        _LitTint ("Lit Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.72,0.72,0.72,1)
        [Toggle(_AREA_UPPER_BODY)] _AreaUpperBody ("Area Upper Body", Float) = 0
        [Toggle(_AREA_LOWER_BODY)] _AreaLowerBody ("Area Lower Body", Float) = 0
        [Toggle(_AREA_CLOTH)] _AreaCloth ("Area Cloth", Float) = 0
        _ShadowStart ("Shadow Start", Range(0,1)) = 0.35
        _ShadowEnd ("Shadow End", Range(0,1)) = 0.55
        _IDShadowOffsetStrength ("ID Shadow Offset Strength", Range(-1,1)) = 0.15
        _DarkBias ("Dark Bias", Range(-1,1)) = 0
        _IndirectLightUsage ("Indirect Light Usage", Range(0,2)) = 0
        _MainLightColorUsage ("Main Light Color Usage", Range(0,1)) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        _MatcapStrength ("MatCap Strength", Range(0,2)) = 0.15
        _MatcapSaturation ("MatCap Saturation", Range(0,2)) = 0.1
        _Roughness ("Roughness", Range(0.02,1)) = 0.6
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularStrength ("Specular Strength", Range(0,4)) = 0.25
        _ReflectionStrength ("Reflection Strength", Range(0,4)) = 0.1
        _VerticalGradientColor ("Vertical Gradient Low Color", Color) = (0.86,0.80,0.94,1)
        _VerticalGradientStrength ("Vertical Gradient Strength", Range(0,1)) = 0.35
        _GradientUVIndex ("Gradient UV Channel", Range(0,3)) = 3
        _GradientInvert ("Gradient Invert", Float) = 0
        _SkinSSSIntensity ("Skin SSS Intensity", Range(0,1)) = 0
        _SSSColor ("SSS Color", Color) = (1,0.5,0.4,1)
        _SSSArea ("SSS Area", Range(0,1)) = 0.34
        _SkinToneBrightness ("Skin Tone Brightness", Range(0,2)) = 1
        _SkinToneWhitening ("Skin Tone Whitening", Range(0,1)) = 0
        _SkinToneTarget ("Skin Tone Target", Color) = (1,0.93,0.90,1)
        _SkinMaskDebugMode ("Skin Mask Debug Mode", Range(0,1)) = 0
        [Toggle(_WUWA_STOCKINGS_ON)] _UseStockings ("Use ID Stockings", Float) = 0
        _StockingSheerness ("Stocking Sheerness", Range(0,1)) = 0.58
        _StockingSkinTint ("Stocking Transmitted Skin Tint", Color) = (1,0.78,0.74,1)
        _StockingEdgeTint ("Stocking Grazing Tint", Color) = (0.58,0.56,0.70,1)
        _StockingFresnelPower ("Stocking Fresnel Power", Range(0.1,8)) = 2.5
        _StockingReflectionStrength ("Stocking Reflection Strength", Range(0,2)) = 0.22
        _RimLightBrightness ("Rim Brightness (Fresnel + Screen)", Range(0,4)) = 0.15
        _RimLightTintColor ("Rim Tint (Fresnel + Screen)", Color) = (1,1,1,1)
        _RimLightWidth ("Screen Rim Radius (Pixels)", Range(0,10)) = 1
        _RimLightThreshold ("Screen Rim Depth Threshold", Range(0,1)) = 0.03
        _RimLightFadeout ("Screen Rim Softness", Range(0.001,1)) = 0.2
        _FresnelPower ("Fresnel Rim Power", Range(0.1,8)) = 2
        [HideInInspector] _FresnelClamp ("Legacy Fresnel Clamp", Range(0,1)) = 1
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.001
        _OutlineReferenceDistance ("Outline Reference Distance", Float) = 5
        _OutlineDistanceScale ("Outline Distance Scale", Range(0,1)) = 1
        _OutlineDistanceMode ("Outline Distance Mode 0 Miku 1 Tutorial", Range(0,1)) = 1
        _OutlineVertexColorMask ("Outline Vertex Color Mask", Range(0,1)) = 1
        _OutlineColorTint ("Outline Color", Color) = (0.1,0.09,0.14,1)
        _OutlineColorMap ("Outline Color Map", 2D) = "white" {}
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
            #pragma vertex WuwaBodyVert
            #pragma fragment WuwaBodyFrag
            #pragma shader_feature_local _WUWA_NORMAL_ON
            #pragma shader_feature_local _WUWA_ID_ON
            #pragma shader_feature_local _WUWA_STOCKINGS_ON
            #pragma shader_feature_local _WUWA_EMISSION_ON
            #pragma shader_feature_local _WUWA_MATCAP_ON
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #if UNITY_VERSION >= 60010000
                #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #else
                #pragma multi_compile _ _FORWARD_PLUS
            #endif
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WuwaCommon.hlsl"
            #include "../GameToon/MikuGameToonSkin.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_IDMap); SAMPLER(sampler_IDMap);
            TEXTURE2D(_MatCap); SAMPLER(sampler_MatCap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint; float4 _LitTint; float4 _ShadowTint;
                float _ShadowStart; float _ShadowEnd; float _IDShadowOffsetStrength; float _DarkBias; float _IndirectLightUsage; float _MainLightColorUsage;
                float _Metallic; float _MatcapStrength; float _MatcapSaturation; float _BodyEmissionStrength;
                float _NormalMapEncoding; float _NormalScale; float _PackedMetallicScale; float _PackedRoughnessScale; float _OcclusionStrength;
                float _Roughness; float4 _SpecularColor; float _SpecularStrength; float _ReflectionStrength;
                float4 _VerticalGradientColor; float _VerticalGradientStrength; float _GradientUVIndex; float _GradientInvert;
                float _SkinSSSIntensity; float4 _SSSColor; float _SSSArea; float _SkinToneBrightness; float _SkinToneWhitening; float4 _SkinToneTarget; float _SkinMaskDebugMode;
                float _UseStockings; float _StockingSheerness; float4 _StockingSkinTint; float4 _StockingEdgeTint; float _StockingFresnelPower; float _StockingReflectionStrength;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineDistanceMode; float _OutlineVertexColorMask; float4 _OutlineColorTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; float2 uv3 : TEXCOORD3; float4 smoothNormalData : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 tangentWS : TEXCOORD2; float3 bitangentWS : TEXCOORD3; float2 uv : TEXCOORD4; float4 shadowCoord : TEXCOORD5; float4 uv12 : TEXCOORD6; float2 uv3 : TEXCOORD7; DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 8); };
            Varyings WuwaBodyVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = normalize(normal.normalWS);
                output.tangentWS = normalize(normal.tangentWS);
                output.bitangentWS = normalize(normal.bitangentWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(pos);
                output.uv12 = float4(input.uv1, input.uv2);
                output.uv3 = input.uv3;
                OUTPUT_LIGHTMAP_UV(input.uv1, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }
            half4 WuwaBodyFrag(Varyings input) : SV_Target
            {
                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColorTint;
                float4 idMap = float4(0.0, 0.5, 0.0, 1.0);
                #if defined(_WUWA_ID_ON)
                    idMap = SAMPLE_TEXTURE2D(_IDMap, sampler_IDMap, input.uv);
                #endif
                float skinMask = 0.0;
                #if defined(_WUWA_ID_ON)
                    skinMask = MikuGameToonHighValueMask(idMap.r);
                #endif
                float3 skinBase = MikuGameToonApplySkinTone(baseSample.rgb, skinMask, _SkinToneBrightness, _SkinToneWhitening, _SkinToneTarget.rgb);
                float3 normalWS = normalize(input.normalWS);
                float metallic = saturate(_Metallic);
                float roughness = saturate(_Roughness);
                #if defined(_WUWA_NORMAL_ON)
                    float3 normalTS;
                    Wuwa_DecodeNormalRoughnessMetallic(
                        SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv),
                        _NormalMapEncoding,
                        _NormalScale,
                        _Metallic,
                        _Roughness,
                        _PackedMetallicScale,
                        _PackedRoughnessScale,
                        normalTS,
                        metallic,
                        roughness);
                    normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS, input.bitangentWS, input.normalWS)));
                #endif
                metallic = saturate(metallic * (1.0 - skinMask));
                float occlusion = lerp(1.0, saturate(baseSample.a), saturate(_OcclusionStrength));
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 lightDirWS = normalize(mainLight.direction);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float toonLight = Wuwa_BodyToonLight(normalWS, lightDirWS, idMap, _ShadowStart, _ShadowEnd, _IDShadowOffsetStrength, _DarkBias);
                float3 mainLightColor = lerp(1.0.xxx, mainLight.color.rgb, _MainLightColorUsage);
                float stockingMask = 0.0;
                float stockingFresnel = 0.0;
                float3 materialBase = skinBase;
                #if defined(_WUWA_STOCKINGS_ON) && defined(_WUWA_ID_ON)
                    float idLuminance = dot(
                        saturate(idMap.rgb),
                        float3(0.2126, 0.7152, 0.0722));
                    stockingMask = (idLuminance > 0.5 ? 1.0 : 0.0) *
                        saturate(_UseStockings);
                    stockingFresnel = pow(
                        saturate(1.0 - dot(normalWS, viewDirWS)),
                        max(_StockingFresnelPower, 0.1));
                    float faceOnSheerness = saturate(_StockingSheerness) *
                        (1.0 - stockingFresnel * 0.55);
                    float3 transmitted = lerp(
                        skinBase,
                        _StockingSkinTint.rgb,
                        faceOnSheerness);
                    float3 stockingTone = lerp(
                        transmitted,
                        _StockingEdgeTint.rgb,
                        stockingFresnel * 0.45);
                    materialBase = lerp(
                        skinBase,
                        stockingTone,
                        stockingMask);
                #endif
                #if defined(_WUWA_MATCAP_ON)
                    float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
                    float2 matcapUV = normalVS.xy * 0.5 + 0.5;
                    float3 matcap = SAMPLE_TEXTURE2D(_MatCap, sampler_MatCap, matcapUV).rgb;
                    float matcapMask = step(0.8, metallic);
                    materialBase = Wuwa_MatcapAlbedo(
                        materialBase,
                        matcap,
                        _MatcapSaturation,
                        matcapMask,
                        _MatcapStrength);
                #endif
                BRDFData brdfData = Wuwa_InitializeBRDFData(
                    materialBase,
                    metallic,
                    roughness,
                    baseSample.a);
                float3 direct = Wuwa_DirectPBR(
                    brdfData,
                    normalWS,
                    lightDirWS,
                    viewDirWS,
                    mainLightColor,
                    mainLight.distanceAttenuation,
                    mainLight.shadowAttenuation,
                    toonLight,
                    _ShadowTint.rgb,
                    _LitTint.rgb,
                    _SpecularColor.rgb,
                    _SpecularStrength);
                float3 bakedGI = SAMPLE_GI(
                    input.lightmapUV,
                    input.vertexSH,
                    normalWS);
                float3 indirect = Wuwa_IndirectPBR(
                    brdfData,
                    bakedGI,
                    normalWS,
                    viewDirWS,
                    input.positionWS,
                    GetNormalizedScreenSpaceUV(input.positionCS),
                    occlusion,
                    _IndirectLightUsage,
                    _ReflectionStrength);
                float3 color = direct + indirect;
                color += stockingMask * stockingFresnel *
                    _StockingReflectionStrength * _StockingEdgeTint.rgb *
                    mainLightColor;
                color += MikuGameToonSkinSSS(materialBase, skinMask * (1.0 - stockingMask), normalWS, viewDirWS, lightDirWS, mainLight.color.rgb, toonLight, _SkinSSSIntensity, _SSSArea, _SSSColor.rgb);
                if (_SkinMaskDebugMode > 0.5) return half4(skinMask.xxx, 1.0);
                color += Wuwa_FresnelStepRim(
                    normalWS,
                    viewDirWS,
                    _FresnelPower,
                    _RimLightBrightness,
                    _RimLightTintColor.rgb,
                    materialBase);
                float gradientValue = Wuwa_GradientValue(
                    input.uv,
                    input.uv12.xy,
                    input.uv12.zw,
                    input.uv3,
                    _GradientUVIndex,
                    _GradientInvert);
                color = Wuwa_ApplyVerticalGradient(
                    color,
                    _VerticalGradientColor.rgb,
                    gradientValue,
                    _VerticalGradientStrength);
                #if defined(_WUWA_EMISSION_ON)
                    color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _BodyEmissionStrength;
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
                float _ShadowStart; float _ShadowEnd; float _IDShadowOffsetStrength; float _DarkBias; float _IndirectLightUsage; float _MainLightColorUsage;
                float _Metallic; float _MatcapStrength; float _MatcapSaturation; float _BodyEmissionStrength;
                float _Roughness; float4 _SpecularColor; float _SpecularStrength; float _ReflectionStrength;
                float4 _VerticalGradientColor; float _VerticalGradientStrength; float _GradientUVIndex; float _GradientInvert;
                float _SkinSSSIntensity; float4 _SSSColor; float _SSSArea; float _SkinToneBrightness; float _SkinToneWhitening; float4 _SkinToneTarget; float _SkinMaskDebugMode;
                float _UseStockings; float _StockingSheerness; float4 _StockingSkinTint; float4 _StockingEdgeTint; float _StockingFresnelPower; float _StockingReflectionStrength;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineDistanceMode; float _OutlineVertexColorMask; float4 _OutlineColorTint;
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
            #include "WuwaCommon.hlsl"
            #include "Packages/com.miku.shaderconverter/Runtime/GameToon/MikuGameToonOutline.hlsl"
            TEXTURE2D(_IDMap); SAMPLER(sampler_IDMap);
            TEXTURE2D(_OutlineColorMap); SAMPLER(sampler_OutlineColorMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineDistanceMode; float _OutlineVertexColorMask; float4 _OutlineColorTint; float _BodyEmissionStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float4 color : COLOR; float4 smoothNormalData : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float outlineCoverage : TEXCOORD1; };
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
                output.outlineCoverage =
                    MikuGameToonOutlineCoverageWithDistanceMultiplier(
                        1.0,
                        _OutlineWidth,
                        distanceMultiplier,
                        1.0,
                        widthMask);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 OutlineFrag(Varyings input) : SV_Target
            {
                MikuGameToonOutlineClipCoverage(input.outlineCoverage);
                float4 idMap = SAMPLE_TEXTURE2D(_IDMap, sampler_IDMap, input.uv);
                float3 outlineMap = SAMPLE_TEXTURE2D(
                    _OutlineColorMap,
                    sampler_OutlineColorMap,
                    input.uv).rgb;
                return half4(
                    Wuwa_IDOutlineColor(idMap, _OutlineColorTint.rgb) *
                    outlineMap,
                    1.0);
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
