// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Wuwa/Body"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("Legacy MainTex, optional", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
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
        _MatcapSaturation ("MatCap Saturation", Range(0,2)) = 0.8
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
        _RimLightBrightness ("Rim Brightness", Range(0,4)) = 0.15
        _RimLightTintColor ("Rim Tint", Color) = (1,1,1,1)
        _RimLightWidth ("Rim Width", Range(0,10)) = 1
        _RimLightThreshold ("Rim Depth Threshold", Range(0,1)) = 0.03
        _RimLightFadeout ("Rim Fadeout", Range(0.001,1)) = 0.2
        [HideInInspector] _FresnelPower ("Legacy Fresnel Power", Range(0.1,8)) = 2
        [HideInInspector] _FresnelClamp ("Legacy Fresnel Clamp", Range(0,1)) = 1
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.001
        _OutlineReferenceDistance ("Outline Reference Distance", Float) = 5
        _OutlineDistanceScale ("Outline Distance Scale", Range(0,1)) = 1
        _OutlineColorTint ("Outline Color", Color) = (0.1,0.09,0.14,1)
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
                float _SkinSSSIntensity; float4 _SSSColor; float _SSSArea; float _SkinToneBrightness; float _SkinToneWhitening; float4 _SkinToneTarget; float _SkinMaskDebugMode;
                float _UseStockings; float _StockingSheerness; float4 _StockingSkinTint; float4 _StockingEdgeTint; float _StockingFresnelPower; float _StockingReflectionStrength;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float4 _OutlineColorTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float3 smoothNormalOS : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 tangentWS : TEXCOORD2; float3 bitangentWS : TEXCOORD3; float3 viewDirWS : TEXCOORD4; float2 uv : TEXCOORD5; float4 shadowCoord : TEXCOORD6; };
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
                output.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = TransformWorldToShadowCoord(pos.positionWS);
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
                #if defined(_WUWA_NORMAL_ON)
                    float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                    normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS, input.bitangentWS, input.normalWS)));
                #endif
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 lightDirWS = normalize(mainLight.direction);
                float3 viewDirWS = normalize(input.viewDirWS);
                float toonLight = Wuwa_BodyToonLight(normalWS, lightDirWS, idMap, _ShadowStart, _ShadowEnd, _IDShadowOffsetStrength, _DarkBias) * mainLight.shadowAttenuation;
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
                float3 direct = lerp(_ShadowTint.rgb, _LitTint.rgb, toonLight) * materialBase * mainLightColor;
                float3 indirect = Wuwa_SampleSH_Indirect(normalWS, 0.0) * _IndirectLightUsage * materialBase;
                float3 color = direct + indirect;
                #if defined(_WUWA_MATCAP_ON)
                    float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
                    float2 matcapUV = normalVS.xy * 0.5 + 0.5;
                    float3 matcap = SAMPLE_TEXTURE2D(_MatCap, sampler_MatCap, matcapUV).rgb;
                    matcap = lerp(Wuwa_Desaturate(matcap), matcap, _MatcapSaturation);
                    float matcapMask = 1.0;
                    #if defined(_WUWA_ID_ON)
                        matcapMask = idMap.r;
                    #endif
                    color += matcap * _MatcapStrength * matcapMask;
                #endif
                float3 halfVectorWS = normalize(viewDirWS + lightDirWS);
                float specMask = _Metallic;
                #if defined(_WUWA_ID_ON)
                    specMask *= idMap.r;
                #endif
                float spec = pow(saturate(dot(normalWS, halfVectorWS)), 64.0) * specMask;
                color += spec.xxx * mainLightColor;
                color += stockingMask * stockingFresnel *
                    _StockingReflectionStrength * _StockingEdgeTint.rgb *
                    mainLightColor;
                color += MikuGameToonSkinSSS(materialBase, skinMask * (1.0 - stockingMask), normalWS, viewDirWS, lightDirWS, mainLight.color.rgb, toonLight, _SkinSSSIntensity, _SSSArea, _SSSColor.rgb);
                if (_SkinMaskDebugMode > 0.5) return half4(skinMask.xxx, 1.0);
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
                float _SkinSSSIntensity; float4 _SSSColor; float _SSSArea; float _SkinToneBrightness; float _SkinToneWhitening; float4 _SkinToneTarget; float _SkinMaskDebugMode;
                float _UseStockings; float _StockingSheerness; float4 _StockingSkinTint; float4 _StockingEdgeTint; float _StockingFresnelPower; float _StockingReflectionStrength;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float4 _OutlineColorTint;
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
            #include "WuwaCommon.hlsl"
            TEXTURE2D(_IDMap); SAMPLER(sampler_IDMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float4 _OutlineColorTint; float _BodyEmissionStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float3 smoothNormalOS : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings OutlineVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                float3 outlineNormalOS = Wuwa_GetOutlineNormalOS(input.smoothNormalOS, input.normalOS);
                float3 outlineNormalWS = normalize(TransformObjectToWorldNormal(outlineNormalOS));
                float outlineWidth = Wuwa_DistanceCompensatedOutlineWidth(pos.positionWS, _OutlineWidth, _OutlineReferenceDistance, _OutlineDistanceScale);
                output.positionCS = TransformWorldToHClip(pos.positionWS + outlineNormalWS * outlineWidth);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 OutlineFrag(Varyings input) : SV_Target
            {
                float4 idMap = SAMPLE_TEXTURE2D(_IDMap, sampler_IDMap, input.uv);
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
