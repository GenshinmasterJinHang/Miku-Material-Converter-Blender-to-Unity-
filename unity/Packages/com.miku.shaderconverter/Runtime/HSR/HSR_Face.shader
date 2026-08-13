// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/HSR/Face"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("Legacy MainTex, optional", 2D) = "white" {}
        _FaceMap ("Face Map", 2D) = "white" {}
        [HideInInspector] _MikuHeadForwardWS ("Miku Head Forward WS", Vector) = (0,0,1,0)
        [HideInInspector] _MikuHeadRightWS ("Miku Head Right WS", Vector) = (1,0,0,0)
        [HideInInspector] _MikuHeadUpWS ("Miku Head Up WS", Vector) = (0,1,0,0)
        [HideInInspector] _MikuHeadAxesValid ("Miku Head Axes Valid", Float) = 0
        _BodyCoolRamp ("Body Cool Ramp", 2D) = "white" {}
        _BodyWarmRamp ("Body Warm Ramp", 2D) = "white" {}
        [MainColor] _BaseColorTint ("Base Color Tint", Color) = (1,1,1,1)
        [Toggle(_AREA_FACE)] _AreaFace ("Area Face", Float) = 1
        _FaceShadowOffset ("Face Shadow Offset", Float) = -0.01
        _FaceShadowTransitionSoftness ("Face Shadow Softness", Range(0,1)) = 0.025
        _FaceShadowStrength ("Face Shadow Strength", Range(0,1)) = 1
        _FaceSdfDebugMode ("Face Debug 0Off 1R 2G 3B 4A 5SDF 6Nose", Range(0,6)) = 0
        _IndirectLightUsage ("Indirect Light Usage", Range(0,2)) = 0.35
        _IndirectLightOcclusionUsage ("Indirect Light AO Usage", Range(0,1)) = 0.7
        _IndirectLightMixBaseColor ("Indirect Mix Base Color", Range(0,1)) = 1
        _IndirectLightFlattenNormal ("Indirect Flatten Normal", Range(0,1)) = 0.8
        _ShadowRampOffset ("Shadow Ramp Offset", Range(0,1)) = 0.75
        _FaceRampRowIndex ("Face Ramp Row Index", Range(0,7)) = 0
        _MainLightColorUsage ("Main Light Color Usage", Range(0,1)) = 0.35
        _FaceSpecularThresholdMask ("Face Specular Threshold Mask", Range(0,1)) = 0.5
        _FaceSpecularExponent ("Face Specular Exponent", Range(1,512)) = 32
        _FaceSpecularSoftness ("Face Specular Smooth Cut Width", Range(0.001,1)) = 0.1
        _FaceSpecularStrength ("Face Specular Strength", Range(0,2)) = 0.12
        [HDR] _FaceSpecularColor ("Face Specular Color", Color) = (1,0.82,0.74,1)
        _NoseLinePower ("Nose Line View Power", Range(0.1,20)) = 3
        _NoseLineStrength ("Nose Line Strength", Range(0,16)) = 8
        _NoseLineColor ("Nose Line Color", Color) = (0.18,0.07,0.06,1)
        _SkinSSSIntensity ("Skin SSS Intensity", Range(0,1)) = 0
        _SSSColor ("SSS Color", Color) = (1,0.5,0.4,1)
        _SSSArea ("SSS Area", Range(0,1)) = 0.35
        _SkinToneBrightness ("Skin Tone Brightness", Range(0,2)) = 1
        _SkinToneWhitening ("Skin Tone Whitening", Range(0,1)) = 0
        _SkinToneTarget ("Skin Tone Target", Color) = (1,0.93,0.90,1)
        _SkinMaskDebugMode ("Skin Mask Debug Mode", Range(0,1)) = 0
        _RimLightBrightness ("Rim Brightness", Range(0,4)) = 0.18
        _RimLightTintColor ("Rim Tint", Color) = (0.92,0.96,1,1)
        _RimLightWidth ("Rim Width", Range(0,10)) = 1
        _RimLightThreshold ("Rim Depth Threshold", Range(0,1)) = 0.03
        _RimLightFadeout ("Rim Fadeout", Range(0.001,1)) = 0.2
        [HideInInspector] _FresnelPower ("Legacy Fresnel Power", Range(0.1,8)) = 3
        [HideInInspector] _FresnelClamp ("Legacy Fresnel Clamp", Range(0,1)) = 1
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
            #pragma vertex HSRVert
            #pragma fragment HSRFrag
            #pragma shader_feature_local _AREA_FACE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "HSRCommon.hlsl"
            #include "../GameToon/MikuGameToonSkin.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_FaceMap); SAMPLER(sampler_FaceMap);
            TEXTURE2D(_BodyCoolRamp); SAMPLER(sampler_BodyCoolRamp);
            TEXTURE2D(_BodyWarmRamp); SAMPLER(sampler_BodyWarmRamp);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint;
                float4 _MikuHeadForwardWS; float4 _MikuHeadRightWS; float4 _MikuHeadUpWS; float _MikuHeadAxesValid;
                float _FaceShadowOffset; float _FaceShadowTransitionSoftness; float _FaceShadowStrength; float _FaceSdfDebugMode;
                float _IndirectLightUsage; float _IndirectLightOcclusionUsage; float _IndirectLightMixBaseColor; float _IndirectLightFlattenNormal; float _ShadowRampOffset; float _FaceRampRowIndex; float _MainLightColorUsage;
                float _FaceSpecularThresholdMask; float _FaceSpecularExponent; float _FaceSpecularSoftness; float _FaceSpecularStrength; float4 _FaceSpecularColor;
                float _NoseLinePower; float _NoseLineStrength; float4 _NoseLineColor;
                float _SkinSSSIntensity; float4 _SSSColor; float _SSSArea; float _SkinToneBrightness; float _SkinToneWhitening; float4 _SkinToneTarget; float _SkinMaskDebugMode;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineGamma; float4 _OutlineColorTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float4 smoothNormalData : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewDirWS : TEXCOORD2; float2 uv : TEXCOORD3; float4 shadowCoord : TEXCOORD4; };
            Varyings HSRVert(Attributes input) { Varyings output; VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz); VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS); output.positionCS = pos.positionCS; output.positionWS = pos.positionWS; output.normalWS = normalize(normal.normalWS); output.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS)); output.uv = TRANSFORM_TEX(input.uv, _BaseMap); output.shadowCoord = TransformWorldToShadowCoord(pos.positionWS); return output; }
            half4 HSRFrag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColorTint;
                float4 faceMap = SAMPLE_TEXTURE2D(_FaceMap, sampler_FaceMap, uv);
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 lightDirWS = normalize(mainLight.direction);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float skinMask = saturate(1.0 - faceMap.r);
                float3 skinBase = MikuGameToonApplySkinTone(baseSample.rgb, skinMask, _SkinToneBrightness, _SkinToneWhitening, _SkinToneTarget.rgb);
                float3 baseColor = skinBase;
                float3x3 objectToWorld = (float3x3)GetObjectToWorldMatrix();
                float3 fallbackRightWS = normalize(mul(objectToWorld, float3(1.0, 0.0, 0.0)));
                float3 fallbackUpWS = normalize(mul(objectToWorld, float3(0.0, 1.0, 0.0)));
                float3 fallbackForwardWS = normalize(mul(objectToWorld, float3(0.0, 0.0, 1.0)));
                float3 headForwardWS, headRightWS, headUpWS;
                Miku_ResolveFaceSdfHeadAxes(fallbackForwardWS, fallbackRightWS, fallbackUpWS, _MikuHeadForwardWS, _MikuHeadRightWS, _MikuHeadUpWS, _MikuHeadAxesValid, headForwardWS, headRightWS, headUpWS);
                float faceAO = HSR_FaceAO(faceMap);
                float faceShadow = HSR_FaceSDFShadow(uv, faceMap, TEXTURE2D_ARGS(_FaceMap, sampler_FaceMap), lightDirWS, headForwardWS, headRightWS, headUpWS, _FaceShadowOffset, _FaceShadowTransitionSoftness);
                float debugMode = round(_FaceSdfDebugMode);
                if (debugMode == 1) return half4(faceMap.rrr, 1.0);
                if (debugMode == 2) return half4(faceMap.ggg, 1.0);
                if (debugMode == 3) return half4(faceMap.bbb, 1.0);
                if (debugMode == 4) return half4(faceMap.aaa, 1.0);
                if (debugMode == 5) return half4(faceShadow.xxx, 1.0);
                if (_SkinMaskDebugMode > 0.5) return half4(skinMask.xxx, 1.0);
                float3 rampColor = HSR_SampleRampRow(faceShadow, _FaceRampRowIndex, lightDirWS, TEXTURE2D_ARGS(_BodyCoolRamp, sampler_BodyCoolRamp), TEXTURE2D_ARGS(_BodyWarmRamp, sampler_BodyWarmRamp), 8.0, _ShadowRampOffset);
                // Strength belongs on the resulting shadow colour. Applying it
                // to the SDF coordinate first can push the complete 0..1 mask
                // into the ramp's white tail when ShadowRampOffset is high,
                // making a valid face SDF visually disappear.
                rampColor = lerp(1.0.xxx, rampColor, saturate(_FaceShadowStrength));
                // The tutorial's nose line is pow(surface NdotV, power) *
                // FaceMap.B. Authored masks may be sparse or low-amplitude, so
                // explicit strength restores visibility without changing the
                // channel's meaning or borrowing a Body LightMap.
                float noseLineMask = HSR_FaceNoseLineMask(
                    normalWS,
                    viewDirWS,
                    faceMap.b,
                    _NoseLinePower,
                    _NoseLineStrength);
                if (debugMode == 6) return half4(noseLineMask.xxx, 1.0);
                baseColor = lerp(baseColor, _NoseLineColor.rgb, noseLineMask);
                float3 indirect = HSR_SampleSH_Indirect(normalWS, _IndirectLightFlattenNormal) * _IndirectLightUsage;
                indirect *= lerp(1.0, faceAO, _IndirectLightOcclusionUsage);
                indirect *= lerp(1.0.xxx, baseColor, _IndirectLightMixBaseColor);
                float3 mainLightColor = lerp(HSR_Desaturate(mainLight.color.rgb), mainLight.color.rgb, _MainLightColorUsage);
                float3 direct = mainLightColor * baseColor * rampColor;
                float3 specular = HSR_ComputeFaceSpecular(
                    normalWS,
                    viewDirWS,
                    lightDirWS,
                    mainLight.color.rgb,
                    _FaceSpecularThresholdMask,
                    _FaceSpecularExponent,
                    _FaceSpecularSoftness,
                    _FaceSpecularStrength,
                    _FaceSpecularColor.rgb,
                    skinMask);
                specular *= mainLight.shadowAttenuation * faceShadow;
                float3 sss = MikuGameToonSkinSSS(skinBase, skinMask, normalWS, viewDirWS, lightDirWS, mainLight.color.rgb, faceShadow, _SkinSSSIntensity, _SSSArea, _SSSColor.rgb);
                float3 finalColor = indirect + direct + specular + sss;
                return half4(finalColor, 1.0);
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
                float _FaceShadowOffset; float _FaceShadowTransitionSoftness; float _FaceShadowStrength; float _FaceSdfDebugMode;
                float _IndirectLightUsage; float _IndirectLightOcclusionUsage; float _IndirectLightMixBaseColor; float _IndirectLightFlattenNormal; float _ShadowRampOffset; float _FaceRampRowIndex; float _MainLightColorUsage;
                float _FaceSpecularThresholdMask; float _FaceSpecularExponent; float _FaceSpecularSoftness; float _FaceSpecularStrength; float4 _FaceSpecularColor;
                float _NoseLinePower; float _NoseLineStrength; float4 _NoseLineColor;
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
            ZWrite Off
            ZTest LEqual
            Blend One Zero
            ColorMask RGBA
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HSRCommon.hlsl"
            #include "Packages/com.miku.shaderconverter/Runtime/GameToon/MikuGameToonOutline.hlsl"
            TEXTURE2D(_BodyCoolRamp); SAMPLER(sampler_BodyCoolRamp);
            TEXTURE2D(_BodyWarmRamp); SAMPLER(sampler_BodyWarmRamp);
            CBUFFER_START(UnityPerMaterial)
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float _OutlineGamma; float4 _OutlineColorTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float4 smoothNormalData : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float outlineCoverage : TEXCOORD0; };
            Varyings OutlineVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                float3 outlineNormalOS = MikuGameToonOutlineNormalTangentSpaceV2(
                    input.smoothNormalData, input.normalOS, input.tangentOS);
                output.positionCS = MikuGameToonOutlinePositionCSWithLegacyMode(
                    pos.positionCS,
                    pos.positionWS,
                    outlineNormalOS,
                    _OutlineWidth,
                    _OutlineReferenceDistance,
                    _OutlineDistanceScale,
                    float4(1.0, 1.0, 1.0, 1.0),
                    1.0,
                    1.0);
                output.outlineCoverage = MikuGameToonOutlineCoverageWithLegacyMode(
                    pos.positionWS,
                    1.0,
                    _OutlineWidth,
                    _OutlineReferenceDistance,
                    _OutlineDistanceScale,
                    float4(1.0, 1.0, 1.0, 1.0),
                    1.0,
                    1.0);
                return output;
            }
            half4 OutlineFrag(Varyings input) : SV_Target
            {
                MikuGameToonOutlineClipCoverage(input.outlineCoverage);
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
