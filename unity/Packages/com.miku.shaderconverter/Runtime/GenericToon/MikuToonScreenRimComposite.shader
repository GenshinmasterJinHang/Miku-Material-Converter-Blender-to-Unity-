// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

Shader "Hidden/Miku/GenericToon/ScreenRimComposite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ScreenRimComposite"
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_MIKU_ToonCharacterMaskTexture);

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv);
                half4 mask = SAMPLE_TEXTURE2D_X(
                    _MIKU_ToonCharacterMaskTexture,
                    sampler_LinearClamp,
                    uv);
                float radius = max(mask.a * 16.0, 1.0);
                float2 texel = _BlitTexture_TexelSize.xy * radius;
                half neighbor = min(
                    min(
                        SAMPLE_TEXTURE2D_X(
                            _MIKU_ToonCharacterMaskTexture,
                            sampler_LinearClamp,
                            uv + float2(texel.x, 0)).a,
                        SAMPLE_TEXTURE2D_X(
                            _MIKU_ToonCharacterMaskTexture,
                            sampler_LinearClamp,
                            uv - float2(texel.x, 0)).a),
                    min(
                        SAMPLE_TEXTURE2D_X(
                            _MIKU_ToonCharacterMaskTexture,
                            sampler_LinearClamp,
                            uv + float2(0, texel.y)).a,
                        SAMPLE_TEXTURE2D_X(
                            _MIKU_ToonCharacterMaskTexture,
                            sampler_LinearClamp,
                            uv - float2(0, texel.y)).a));
                half edge = saturate(mask.a * 16.0 - neighbor * 16.0);
                return half4(source.rgb + mask.rgb * edge, source.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
