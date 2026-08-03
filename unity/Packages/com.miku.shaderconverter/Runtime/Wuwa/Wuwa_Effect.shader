// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Wuwa/Effect"
{
    Properties
    {
        [MainTexture] _BaseMap ("Primary Layer", 2D) = "white" {}
        [HideInInspector] _MainTex ("Legacy MainTex, optional", 2D) = "white" {}
        _EmissionMap ("Color Emission Layer", 2D) = "black" {}
        [MainColor] _BaseColorTint ("Primary Tint", Color) = (1,1,1,1)
        _EffectLayerBlend ("Layer Blend", Range(0,1)) = 0
        _PrimaryOpacity ("Primary Opacity", Range(0,1)) = 1
        _PrimaryEmissionStrength ("Primary Layer Brightness", Range(0,20)) = 1.4
        _AlphaPower ("Alpha Power", Range(0.1,8)) = 2
        _SecondaryEmissionStrength ("Color Layer Emission", Range(0,20)) = 1.05
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha
            ColorMask RGBA

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WuwaEffectVert
            #pragma fragment WuwaEffectFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColorTint;
                float _EffectLayerBlend;
                float _PrimaryOpacity;
                float _PrimaryEmissionStrength;
                float _AlphaPower;
                float _SecondaryEmissionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings WuwaEffectVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 WuwaEffectFrag(Varyings input) : SV_Target
            {
                float4 primary = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColorTint;
                float4 secondary = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv);
                float alphaPower = max(_AlphaPower, 0.1);
                float primaryAlpha = pow(saturate(primary.a), alphaPower) * _PrimaryOpacity;
                float secondaryAlpha = pow(saturate(secondary.a), alphaPower);
                float3 primaryPremultiplied = primary.rgb * primaryAlpha *
                    _PrimaryEmissionStrength;
                float3 secondaryPremultiplied = secondary.rgb * secondaryAlpha * _SecondaryEmissionStrength;
                float layerBlend = saturate(_EffectLayerBlend);
                float3 color = lerp(primaryPremultiplied, secondaryPremultiplied, layerBlend);
                float alpha = lerp(primaryAlpha, secondaryAlpha, layerBlend);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    CustomEditor "Miku.ShaderConverter.Editor.MikuManualTextureShaderGUI"
}
