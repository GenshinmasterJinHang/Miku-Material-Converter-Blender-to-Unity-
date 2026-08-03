// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Genshin/Face"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("Legacy MainTex, optional", 2D) = "white" {}
        _FaceSDFMap ("Face SDF Map", 2D) = "white" {}
        [HideInInspector] _MikuHeadForwardWS ("Miku Head Forward WS", Vector) = (0,0,1,0)
        [HideInInspector] _MikuHeadRightWS ("Miku Head Right WS", Vector) = (1,0,0,0)
        [HideInInspector] _MikuHeadUpWS ("Miku Head Up WS", Vector) = (0,1,0,0)
        [HideInInspector] _MikuHeadAxesValid ("Miku Head Axes Valid", Float) = 0
        _ShadowRampMap ("Shadow Ramp", 2D) = "white" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        [MainColor] _BaseColorTint ("Base Color Tint", Color) = (1,1,1,1)
        [Toggle(_AREA_FACE)] _AreaFace ("Area Face", Float) = 1
        _InNight ("In Night", Range(0,1)) = 0
        _FaceShadowOffset ("Face Shadow Offset", Float) = 0
        _FaceShadowSoftness ("Face Shadow Softness", Range(0.001,1)) = 0.015
        _FaceSdfFlipY ("Face SDF Flip Y", Range(0,1)) = 0
        _FaceSdfDebugMode ("Face SDF Debug 0Off 1Raw 2Mirrored 5Mask", Range(0,5)) = 0
        _MainLightColorUsage ("Main Light Color Usage", Range(0,1)) = 0.15
        _IndirectLightUsage ("Indirect Light Usage", Range(0,2)) = 0.1
        _HighlightCompression ("Highlight Compression", Range(0,1)) = 1
        _HighlightKnee ("Highlight Knee", Range(0,1)) = 0.72
        _HighlightCeiling ("Highlight Ceiling", Range(0,1)) = 0.98
        _SkinSSSIntensity ("Skin SSS Intensity", Range(0,1)) = 0
        _SSSColor ("SSS Color", Color) = (1,0.5,0.4,1)
        _SSSArea ("SSS Area", Range(0,1)) = 0.35
        _SkinToneBrightness ("Skin Tone Brightness", Range(0,2)) = 1
        _SkinToneWhitening ("Skin Tone Whitening", Range(0,1)) = 0
        _SkinToneTarget ("Skin Tone Target", Color) = (1,0.93,0.90,1)
        _SkinMaskDebugMode ("Skin Mask Debug Mode", Range(0,1)) = 0
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
            #pragma shader_feature_local _AREA_FACE
            #pragma shader_feature_local _GENSHIN_EMISSION_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GenshinCommon.hlsl"
            #include "../GameToon/MikuGameToonSkin.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_FaceSDFMap); SAMPLER(sampler_FaceSDFMap);
            TEXTURE2D(_ShadowRampMap); SAMPLER(sampler_ShadowRampMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint;
                float4 _MikuHeadForwardWS; float4 _MikuHeadRightWS; float4 _MikuHeadUpWS; float _MikuHeadAxesValid;
                float _InNight; float _FaceShadowOffset; float _FaceShadowSoftness; float _FaceSdfFlipY; float _FaceSdfDebugMode; float _MainLightColorUsage; float _IndirectLightUsage; float _EmissionIntensity;
                float _HighlightCompression; float _HighlightKnee; float _HighlightCeiling;
                float _SkinSSSIntensity; float4 _SSSColor; float _SSSArea; float _SkinToneBrightness; float _SkinToneWhitening; float4 _SkinToneTarget; float _SkinMaskDebugMode;
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
                float4 rawBaseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 baseSample = rawBaseSample * _BaseColorTint;
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float3 lightDirWS = normalize(mainLight.direction);
                float3 mainLightColor = lerp(1.0.xxx, mainLight.color.rgb, _MainLightColorUsage);
                float3x3 objectToWorld = (float3x3)GetObjectToWorldMatrix();
                float3 fallbackRightWS = normalize(mul(objectToWorld, float3(1.0, 0.0, 0.0)));
                float3 fallbackUpWS = normalize(mul(objectToWorld, float3(0.0, 1.0, 0.0)));
                float3 fallbackForwardWS = normalize(mul(objectToWorld, float3(0.0, 0.0, 1.0)));
                float3 headForwardWS, headRightWS, headUpWS;
                Miku_ResolveFaceSdfHeadAxes(fallbackForwardWS, fallbackRightWS, fallbackUpWS, _MikuHeadForwardWS, _MikuHeadRightWS, _MikuHeadUpWS, _MikuHeadAxesValid, headForwardWS, headRightWS, headUpWS);
                float inLight = Genshin_FaceSDFShadow(input.uv, TEXTURE2D_ARGS(_FaceSDFMap, sampler_FaceSDFMap), lightDirWS, headForwardWS, headRightWS, headUpWS, _FaceShadowOffset, _FaceShadowSoftness, _FaceSdfFlipY);
                float debugMode = round(_FaceSdfDebugMode);
                if (debugMode == 1) return half4(SAMPLE_TEXTURE2D(_FaceSDFMap, sampler_FaceSDFMap, input.uv).rrr, 1.0);
                if (debugMode == 2) return half4(SAMPLE_TEXTURE2D(_FaceSDFMap, sampler_FaceSDFMap, float2(1.0 - input.uv.x, input.uv.y)).rrr, 1.0);
                if (debugMode == 5) return half4(inLight.xxx, 1.0);
                float skinMask = MikuGameToonWarmPaleFaceMask(rawBaseSample.rgb);
                float3 skinBase = MikuGameToonApplySkinTone(baseSample.rgb, skinMask, _SkinToneBrightness, _SkinToneWhitening, _SkinToneTarget.rgb);
                if (_SkinMaskDebugMode > 0.5) return half4(skinMask.xxx, 1.0);
                float3 diffuse = Genshin_FaceDiffuse(skinBase, inLight, mainLightColor, _InNight, _HighlightCompression, TEXTURE2D_ARGS(_ShadowRampMap, sampler_ShadowRampMap));
                float3 indirect = Genshin_SampleSH_Indirect(normalWS) * _IndirectLightUsage * skinBase;
                float3 sss = MikuGameToonSkinSSS(skinBase, skinMask, normalWS, viewDirWS, lightDirWS, mainLight.color.rgb, inLight, _SkinSSSIntensity, _SSSArea, _SSSColor.rgb);
                float3 emission = 0.0.xxx;
                #if defined(_GENSHIN_EMISSION_ON)
                    emission += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionIntensity;
                #endif
                float3 nonEmissive = Genshin_CompressNonEmissive(indirect + diffuse + sss, _HighlightCompression, _HighlightKnee, _HighlightCeiling);
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
                float4 _MikuHeadForwardWS; float4 _MikuHeadRightWS; float4 _MikuHeadUpWS; float _MikuHeadAxesValid;
                float _InNight; float _FaceShadowOffset; float _FaceShadowSoftness; float _FaceSdfFlipY; float _FaceSdfDebugMode; float _MainLightColorUsage; float _IndirectLightUsage; float _EmissionIntensity;
                float _HighlightCompression; float _HighlightKnee; float _HighlightCeiling;
                float _SkinSSSIntensity; float4 _SSSColor; float _SSSArea; float _SkinToneBrightness; float _SkinToneWhitening; float4 _SkinToneTarget; float _SkinMaskDebugMode;
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
