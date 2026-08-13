// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

Shader "Hidden/Miku/GameToon/ScreenRimComposite"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_MIKU_ToonCharacterMaskTexture);
            TEXTURE2D_X(_MIKU_ToonCharacterRimParamsTexture);
            float _MIKU_ScreenRimAlgorithm;

            float MikuLinearEyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

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
                half2 rimParams = SAMPLE_TEXTURE2D_X(
                    _MIKU_ToonCharacterRimParamsTexture,
                    sampler_LinearClamp,
                    uv).rg;
                if (mask.a <= 0.0)
                    return source;
                float radius = max(mask.a * 16.0, 1.0);
                float centerDepth = MikuLinearEyeDepth(uv);
                if (_MIKU_ScreenRimAlgorithm > 0.5)
                {
                    float distanceAlpha = saturate(1.0 - centerDepth / 80.0);
                    float2 tutorialTexel = _BlitTexture_TexelSize.xy *
                        max(radius * distanceAlpha, 1.0);
                    float upperLeft = MikuLinearEyeDepth(
                        uv + float2(-tutorialTexel.x, tutorialTexel.y));
                    float upperRight = MikuLinearEyeDepth(
                        uv + float2(tutorialTexel.x, tutorialTexel.y));
                    float lowerCenter = MikuLinearEyeDepth(
                        uv + float2(0.0, -tutorialTexel.y));
                    float sobelDepth = max(
                        max(upperLeft - centerDepth, upperRight - centerDepth),
                        lowerCenter - centerDepth);
                    float threshold = rimParams.r * max(centerDepth, 1e-4);
                    float softness = max(
                        rimParams.g * max(centerDepth, 1.0),
                        1e-5);
                    half tutorialEdge = smoothstep(
                        threshold,
                        threshold + softness,
                        max(sobelDepth, 0.0)) * distanceAlpha;
                    return half4(
                        source.rgb + mask.rgb * tutorialEdge,
                        source.a);
                }
                float2 texel = _BlitTexture_TexelSize.xy * radius;
                float depthDelta = 0.0;
                depthDelta = max(
                    depthDelta,
                    MikuLinearEyeDepth(uv + float2(texel.x, 0)) -
                    centerDepth);
                depthDelta = max(
                    depthDelta,
                    MikuLinearEyeDepth(uv - float2(texel.x, 0)) -
                    centerDepth);
                depthDelta = max(
                    depthDelta,
                    MikuLinearEyeDepth(uv + float2(0, texel.y)) -
                    centerDepth);
                depthDelta = max(
                    depthDelta,
                    MikuLinearEyeDepth(uv - float2(0, texel.y)) -
                    centerDepth);
                half edge = smoothstep(
                    rimParams.r,
                    rimParams.r + max(rimParams.g, 0.00001),
                    max(depthDelta, 0.0));
                return half4(source.rgb + mask.rgb * edge, source.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
