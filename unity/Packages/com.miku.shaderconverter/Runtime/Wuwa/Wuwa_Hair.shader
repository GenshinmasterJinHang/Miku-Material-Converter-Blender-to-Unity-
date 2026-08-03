// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Wuwa/Hair"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _MainTex ("Legacy MainTex, optional", 2D) = "white" {}
        _HairHM ("Hair HM", 2D) = "white" {}
        _IDMap ("ID Map", 2D) = "white" {}
        _EmissionMap ("Emission Map", 2D) = "black" {}
        [MainColor] _BaseColorTint ("Base Color Tint", Color) = (1,1,1,1)
        _LitTint ("Lit Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.68,0.56,0.38,1)
        [Toggle(_AREA_HAIR)] _AreaHair ("Area Hair", Float) = 1
        _ShadowStart ("Shadow Start", Range(0,1)) = 0.42
        _ShadowEnd ("Shadow End", Range(0,1)) = 0.52
        _HairBaseSaturation ("Hair Base Saturation", Range(0,2)) = 1
        _HairBaseBrightness ("Hair Base Brightness", Range(0,2)) = 1
        _HairSpecStart ("Hair Spec Start", Range(0,1)) = 0.35
        _HairSpecEnd ("Hair Spec End", Range(0,1)) = 0.65
        _HairSpecColor ("Hair Spec Color", Color) = (1,1,1,1)
        _HairSpecStrength ("Hair Spec Strength", Range(0,1)) = 0.06
        _HairSpecOffsetStrength ("Hair Spec Offset Strength", Range(-1,1)) = 0.25
        _HairLitMaskStrength ("Hair HM Broad Light Strength", Range(0,1)) = 0.5
        _IndirectLightUsage ("Indirect Light Usage", Range(0,2)) = 0
        _MainLightColorUsage ("Main Light Color Usage", Range(0,1)) = 0
        _RimLightBrightness ("Rim Brightness", Range(0,4)) = 0.03
        _RimLightTintColor ("Rim Tint", Color) = (1,1,1,1)
        _RimLightWidth ("Rim Width", Range(0,10)) = 1
        _RimLightThreshold ("Rim Depth Threshold", Range(0,1)) = 0.03
        _RimLightFadeout ("Rim Fadeout", Range(0.001,1)) = 0.2
        [HideInInspector] _FresnelPower ("Legacy Fresnel Power", Range(0.1,8)) = 2
        [HideInInspector] _FresnelClamp ("Legacy Fresnel Clamp", Range(0,1)) = 1
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.001
        _OutlineReferenceDistance ("Outline Reference Distance", Float) = 5
        _OutlineDistanceScale ("Outline Distance Scale", Range(0,1)) = 1
        _OutlineColorTint ("Outline Color", Color) = (0.24,0.16,0.12,1)
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
            #pragma vertex WuwaHairVert
            #pragma fragment WuwaHairFrag
            #pragma shader_feature_local _WUWA_ID_ON
            #pragma shader_feature_local _WUWA_HAIR_HM_ON
            #pragma shader_feature_local _WUWA_EMISSION_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WuwaCommon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_HairHM); SAMPLER(sampler_HairHM);
            TEXTURE2D(_IDMap); SAMPLER(sampler_IDMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColorTint; float4 _LitTint; float4 _ShadowTint;
                float _ShadowStart; float _ShadowEnd; float _HairBaseSaturation; float _HairBaseBrightness; float _HairSpecStart; float _HairSpecEnd; float4 _HairSpecColor; float _HairSpecStrength; float _HairSpecOffsetStrength; float _HairLitMaskStrength; float _IndirectLightUsage; float _MainLightColorUsage;
                float _RimLightBrightness; float4 _RimLightTintColor; float _RimLightWidth; float _RimLightThreshold; float _RimLightFadeout; float _FresnelPower; float _FresnelClamp;
                float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float4 _OutlineColorTint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float3 smoothNormalOS : TEXCOORD7; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 tangentWS : TEXCOORD2; float3 viewDirWS : TEXCOORD3; float2 uv : TEXCOORD4; float4 shadowCoord : TEXCOORD5; };
            Varyings WuwaHairVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = normalize(normal.normalWS);
                output.tangentWS = normalize(normal.tangentWS);
                output.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = TransformWorldToShadowCoord(pos.positionWS);
                return output;
            }
            half4 WuwaHairFrag(Varyings input) : SV_Target
            {
                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColorTint;
                float4 hm = float4(0.0, 0.5, 0.0, 1.0);
                #if defined(_WUWA_HAIR_HM_ON)
                    hm = SAMPLE_TEXTURE2D(_HairHM, sampler_HairHM, input.uv);
                #endif
                float4 idMap = float4(0.0, 0.5, 0.0, 1.0);
                #if defined(_WUWA_ID_ON)
                    idMap = SAMPLE_TEXTURE2D(_IDMap, sampler_IDMap, input.uv);
                #endif
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 lightDirWS = normalize(mainLight.direction);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float toonLight = Wuwa_BodyToonLight(normalWS, lightDirWS, idMap, _ShadowStart, _ShadowEnd, 0.05, 0.0) * mainLight.shadowAttenuation;
                float3 mainLightColor = lerp(1.0.xxx, mainLight.color.rgb, _MainLightColorUsage);
                float3 baseTone = max(Wuwa_AdjustSaturation(baseSample.rgb, _HairBaseSaturation) * _HairBaseBrightness, 0.0.xxx);
                float broadLight = lerp(1.0, lerp(0.72, 1.08, saturate(hm.g)), _HairLitMaskStrength);
                baseTone *= broadLight;
                float3 color = lerp(_ShadowTint.rgb, _LitTint.rgb, toonLight) * baseTone * mainLightColor;
                color += Wuwa_SampleSH_Indirect(normalWS, 0.0) * _IndirectLightUsage * baseTone;

                float viewFacing = saturate(dot(viewDirWS, normalWS));
                float viewOffset = (hm.g - 0.5) * _HairSpecOffsetStrength;
                float viewGate = smoothstep(_HairSpecStart, max(_HairSpecEnd, _HairSpecStart + 1e-4), viewFacing + viewOffset);
                float narrowSpec = saturate(hm.r * viewGate * _HairSpecStrength);
                float broadSpec = saturate(hm.g * hm.g * viewGate * _HairSpecStrength * 0.5);
                float spec = saturate(narrowSpec * 0.9 + broadSpec);
                color = lerp(color, max(color, _HairSpecColor.rgb), spec);
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
                float _ShadowStart; float _ShadowEnd; float _HairBaseSaturation; float _HairBaseBrightness; float _HairSpecStart; float _HairSpecEnd; float4 _HairSpecColor; float _HairSpecStrength; float _HairSpecOffsetStrength; float _HairLitMaskStrength; float _IndirectLightUsage; float _MainLightColorUsage;
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
                float4 _BaseMap_ST; float _OutlineWidth; float _OutlineReferenceDistance; float _OutlineDistanceScale; float4 _OutlineColorTint;
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
            Name "WuwaHairShadow"
            Tags { "LightMode"="WuwaHairShadow" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WuwaHairShadowVert
            #pragma fragment WuwaHairShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings WuwaHairShadowVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 WuwaHairShadowFrag(Varyings input) : SV_Target
            {
                float linearDepth01 = LinearEyeDepth(input.positionCS.z, _ZBufferParams) / max(_ProjectionParams.z, 1e-5);
                return half4(linearDepth01, 0, 0, 1);
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
