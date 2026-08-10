// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

Shader "Hidden/MIKU/Endfield/FullScreenColorLut"
{
    Properties
    {
        [NoScaleOffset] _LutTex ("Flattened 32x32x32 LUT", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "EndfieldColorLut"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            // URP full-screen passes include the universal Core.hlsl before
            // Blit.hlsl so the TEXTURE2D_X(_BlitTexture) macros are defined.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_LutTex);
            SAMPLER(sampler_LutTex);

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END

            float3 MikuLinearToSrgb(float3 linearColor)
            {
                float3 value = max(linearColor, 0.0.xxx);
                float3 linearSegment = value * 12.92;
                float3 curveSegment =
                    1.055 * pow(max(value, 1e-8.xxx), 1.0 / 2.4) - 0.055;
                float3 useLinear =
                    1.0 - step(0.0031308.xxx, value);
                return lerp(curveSegment, linearSegment, useLinear);
            }

            float3 MikuSampleFlattenedLut(float3 encoded)
            {
                const float size = 32.0;
                const float last = size - 1.0;
                const float width = size * size;
                float3 coordinate = saturate(encoded).brg;
                float slice = coordinate.x * last;
                float slice0 = floor(slice);
                float sliceBlend = slice - slice0;
                float texelR = coordinate.y * last + 0.5;
                float texelG = coordinate.z * last + 0.5;
                float2 uv0 = float2(
                    (slice0 * size + texelR) / width,
                    texelG / size);
                float2 uv1 = uv0 + float2(size / width, 0.0);
                float3 value0 =
                    SAMPLE_TEXTURE2D(_LutTex, sampler_LutTex, uv0).rgb;
                float3 value1 =
                    SAMPLE_TEXTURE2D(_LutTex, sampler_LutTex, uv1).rgb;
                return lerp(value0, value1, sliceBlend);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 source = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    input.texcoord);
                float intensity = saturate(_Intensity);
                if (intensity <= 0.0)
                    return source;

                float peak = max(
                    1.0,
                    max(source.r, max(source.g, source.b)));
                float3 normalized = max(source.rgb, 0.0.xxx) / peak;
                float3 encoded = MikuLinearToSrgb(normalized);
                float3 graded = MikuSampleFlattenedLut(encoded) * peak;
                return float4(lerp(source.rgb, graded, intensity), source.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
