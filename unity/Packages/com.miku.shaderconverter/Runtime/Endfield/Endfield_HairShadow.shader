// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Endfield/HairShadow"
{
    Properties
    {
        [MainTexture] _BaseMap ("Hair Shadow Mask", 2D) = "white" {}
        [MainColor] _BaseColorTint ("Shadow Tint", Color) = (0.32,0.20,0.18,1)
        _ShadowOpacity ("Shadow Opacity", Range(0,1)) = 0.58
        _ShadowSoftness ("Mask Softness", Range(0.1,4)) = 1
        [Enum(R,0,G,1,B,2,A,3)] _MaskChannel ("Mask Channel", Float) = 0
        [Toggle] _InvertMask ("Invert Mask", Float) = 1
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 36
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "HairShadowForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref [_StencilRef]
                ReadMask [_StencilReadMask]
                WriteMask 0
                Comp Equal
                Pass Keep
            }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColorTint;
            float _ShadowOpacity;
            float _ShadowSoftness;
            float _MaskChannel;
            float _InvertMask;
            float _StencilRef;
            float _StencilReadMask;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float4 sampleValue = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 selected = step(abs(float4(0,1,2,3) - _MaskChannel), 0.1);
                float mask = dot(sampleValue, selected);
                mask = lerp(mask, 1.0 - mask, saturate(_InvertMask));
                float alpha = pow(saturate(mask), max(_ShadowSoftness, 0.1)) *
                    _ShadowOpacity * _BaseColorTint.a;
                return half4(_BaseColorTint.rgb, alpha);
            }
            ENDHLSL
        }
    }
    CustomEditor "Miku.ShaderConverter.Editor.MikuManualTextureShaderGUI"
}
