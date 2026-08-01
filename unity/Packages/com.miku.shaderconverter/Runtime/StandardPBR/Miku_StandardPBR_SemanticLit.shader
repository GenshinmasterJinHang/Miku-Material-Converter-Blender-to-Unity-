Shader "MIKU/StandardPBR/SemanticLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Color", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color Tint", Color) = (1,1,1,1)
        _AOMap ("Ambient Occlusion", 2D) = "white" {}
        _AOStrength ("AO Strength", Range(0,1)) = 1
        _MetallicMap ("Metalness", 2D) = "white" {}
        _Metallic ("Metalness", Range(0,1)) = 0
        _RoughnessMap ("Roughness", 2D) = "white" {}
        _Roughness ("Roughness", Range(0,1)) = 0.5
        _GlossinessMap ("Glossiness", 2D) = "white" {}
        _Glossiness ("Glossiness", Range(0,1)) = 0.5
        _SpecularMap ("Specular", 2D) = "white" {}
        _SpecularColor ("Specular Color", Color) = (0.5,0.5,0.5,1)
        _SpecularStrength ("Specular Strength", Range(0,2)) = 1
        _ReflectionMap ("Reflection", 2D) = "white" {}
        _ReflectionCube ("Reflection Cubemap", Cube) = "_Skybox" {}
        _ReflectionStrength ("Reflection Strength", Range(0,2)) = 1
        _BumpHeightMap ("Bump Height", 2D) = "gray" {}
        _BumpStrength ("Bump Strength", Range(0,2)) = 0.1
        _BumpDistance ("Bump Distance", Float) = 1
        _NormalMap ("Normal", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1
        _FlipNormalGreen ("Flip Normal Green", Float) = 0
        _HeightMap ("Height", 2D) = "gray" {}
        _HeightStrength ("Height Strength", Range(0,0.2)) = 0.05
        _HeightBias ("Height Bias", Range(-1,1)) = 0
        _DisplacementMap ("Displacement", 2D) = "gray" {}
        _DisplacementStrength ("Displacement Strength", Range(0,0.2)) = 0.05
        _DisplacementMidlevel ("Displacement Midlevel", Range(0,1)) = 0.5
        _EmissionMap ("Emission", 2D) = "black" {}
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionStrength ("Emission Strength", Float) = 1
        _AlphaMap ("Alpha", 2D) = "white" {}
        _Alpha ("Alpha", Range(0,1)) = 1
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _ParallaxSteps ("Parallax Steps", Range(4,64)) = 16
        _ParallaxMode ("Parallax Mode", Float) = 0
        _Surface ("Surface Type", Float) = 0
        _Blend ("Blend Mode", Float) = 0
        _Cull ("Cull", Float) = 2
        _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _MIKU_BASECOLOR_MAP
            #pragma shader_feature_local _MIKU_AO_MAP
            #pragma shader_feature_local _MIKU_METALLIC_MAP
            #pragma shader_feature_local _MIKU_ROUGHNESS_MAP
            #pragma shader_feature_local _MIKU_GLOSSINESS_MAP
            #pragma shader_feature_local _MIKU_SPECULAR_MAP
            #pragma shader_feature_local _MIKU_REFLECTION_MAP
            #pragma shader_feature_local _MIKU_REFLECTION_CUBE
            #pragma shader_feature_local _MIKU_NORMAL_MAP
            #pragma shader_feature_local _MIKU_BUMP_MAP
            #pragma shader_feature_local _MIKU_HEIGHT_MAP
            #pragma shader_feature_local _MIKU_DISPLACEMENT_MAP
            #pragma shader_feature_local _MIKU_PARALLAX
            #pragma shader_feature_local _MIKU_POM
            #pragma shader_feature_local _MIKU_EMISSION_MAP
            #pragma shader_feature_local _MIKU_ALPHA_MAP
            #pragma shader_feature_local _MIKU_SPECULAR_WORKFLOW
            #pragma shader_feature_local _MIKU_METALLIC_WORKFLOW
            #pragma shader_feature_local _MIKU_ALPHA_CLIP
            #pragma shader_feature_local _MIKU_TRANSPARENT
            #pragma shader_feature_local _MIKU_PREMULTIPLY
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _ALPHABLEND_ON
            #pragma shader_feature_local _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _MIKU_DOUBLE_SIDED
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv0 : TEXCOORD0; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float4 tangentWS : TEXCOORD2; float2 uv0 : TEXCOORD3; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_AOMap); SAMPLER(sampler_AOMap);
            TEXTURE2D(_MetallicMap); SAMPLER(sampler_MetallicMap);
            TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_GlossinessMap); SAMPLER(sampler_GlossinessMap);
            TEXTURE2D(_SpecularMap); SAMPLER(sampler_SpecularMap);
            TEXTURE2D(_ReflectionMap); SAMPLER(sampler_ReflectionMap);
            TEXTURECUBE(_ReflectionCube); SAMPLER(sampler_ReflectionCube);
            TEXTURE2D(_BumpHeightMap); SAMPLER(sampler_BumpHeightMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_HeightMap); SAMPLER(sampler_HeightMap);
            TEXTURE2D(_DisplacementMap); SAMPLER(sampler_DisplacementMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _AOStrength;
                float _Metallic;
                float _Roughness;
                float _Glossiness;
                float4 _SpecularColor;
                float _SpecularStrength;
                float _ReflectionStrength;
                float _BumpStrength;
                float _BumpDistance;
                float _NormalStrength;
                float _FlipNormalGreen;
                float _HeightStrength;
                float _HeightBias;
                float _DisplacementStrength;
                float _DisplacementMidlevel;
                float4 _EmissionColor;
                float _EmissionStrength;
                float _Alpha;
                float _AlphaCutoff;
                float _ParallaxSteps;
                float _ParallaxMode;
                float _Surface;
                float _Blend;
                float _Cull;
                float _ZWrite;
            CBUFFER_END
            #include "Packages/com.miku.shaderconverter/Runtime/StandardPBR/MIKUStandardPBRCommon.hlsl"
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.tangentWS = float4(TransformObjectToWorldDir(IN.tangentOS.xyz), IN.tangentOS.w);
                OUT.uv0 = IN.uv0;
                return OUT;
            }
            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 tangentWS = normalize(IN.tangentWS.xyz);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float3x3 tangentToWorld = CreateTangentToWorld(normalWS, tangentWS, IN.tangentWS.w);
                float3 viewDirTS = mul(transpose(tangentToWorld), viewDirWS);
                SurfaceData surfaceData = MIKUSampleSurfaceData(IN.uv0, viewDirTS);
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = TransformTangentToWorld(surfaceData.normalTS, tangentToWorld);
                inputData.viewDirectionWS = viewDirWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #if defined(_MIKU_ALPHA_CLIP)
                clip(surfaceData.alpha - _AlphaCutoff);
                #endif
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                #if defined(_MIKU_PREMULTIPLY)
                color.rgb *= surfaceData.alpha;
                #endif
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _MIKU_BASECOLOR_MAP
            #pragma shader_feature_local _MIKU_ALPHA_MAP
            #pragma shader_feature_local _MIKU_ALPHA_CLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.miku.shaderconverter/Runtime/StandardPBR/MIKUStandardPBRAlphaClip.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv0 : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv0 : TEXCOORD0; };
            Varyings DepthVert(Attributes IN) { Varyings OUT; OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz); OUT.uv0 = IN.uv0; return OUT; }
            half4 DepthFrag(Varyings IN) : SV_Target { MIKUClipAlpha(IN.uv0); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _MIKU_BASECOLOR_MAP
            #pragma shader_feature_local _MIKU_ALPHA_MAP
            #pragma shader_feature_local _MIKU_ALPHA_CLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.miku.shaderconverter/Runtime/StandardPBR/MIKUStandardPBRAlphaClip.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv0 : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv0 : TEXCOORD0; };
            Varyings DepthVert(Attributes IN) { Varyings OUT; OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz); OUT.uv0 = IN.uv0; return OUT; }
            half4 DepthFrag(Varyings IN) : SV_Target { MIKUClipAlpha(IN.uv0); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma shader_feature_local _MIKU_BASECOLOR_MAP
            #pragma shader_feature_local _MIKU_ALPHA_MAP
            #pragma shader_feature_local _MIKU_ALPHA_CLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.miku.shaderconverter/Runtime/StandardPBR/MIKUStandardPBRAlphaClip.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv0 : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv0 : TEXCOORD1; };
            Varyings NormalsVert(Attributes IN) { Varyings OUT; OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz); OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS); OUT.uv0 = IN.uv0; return OUT; }
            half4 NormalsFrag(Varyings IN) : SV_Target { MIKUClipAlpha(IN.uv0); return half4(normalize(IN.normalWS) * 0.5h + 0.5h, 1); }
            ENDHLSL
        }
    }
    FallBack Off
    CustomEditor "Miku.ShaderConverter.Editor.MikuManualTextureShaderGUI"
}
