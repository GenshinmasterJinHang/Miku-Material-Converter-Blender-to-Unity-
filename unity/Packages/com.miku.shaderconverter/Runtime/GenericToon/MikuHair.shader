// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

Shader "Miku/GenericToon/Hair"
{
    Properties
    {
        [MainTexture] _MIKU_BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _MIKU_BaseColor("Base Color", Color) = (1,1,1,1)
        _MIKU_ShadowColor("Shadow Color", Color) = (0.55,0.55,0.65,1)
        _MIKU_Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle] _MIKU_AlphaClip("Alpha Clip", Float) = 0
        _MIKU_ToonSteps("Toon Steps", Range(1,6)) = 2
        _MIKU_ShadowSoftness("Shadow Softness", Range(0,1)) = 0.16
        _MIKU_SSSStrength("Pseudo SSS", Range(0,1)) = 0
        [Toggle] _MIKU_OutlineEnabled("Outline Enabled", Float) = 1
        _MIKU_OutlineColor("Outline Color", Color) = (0.04,0.03,0.06,1)
        _MIKU_OutlineWidth("Outline Width (px)", Range(0,16)) = 1.25
        _MIKU_OutlineDepthBias("Outline Depth Bias", Range(-8,8)) = 0
        _MIKU_OutlineMinPixels("Outline Min Pixels", Range(0,16)) = 0
        _MIKU_OutlineMaxPixels("Outline Max Pixels", Range(0,32)) = 8
        _MIKU_RimColor("Screen Rim Color", Color) = (0.65,0.8,1,1)
        _MIKU_RimIntensity("Screen Rim Intensity", Range(0,4)) = 0.25
        _MIKU_RimWidth("Screen Rim Width", Range(0,16)) = 2
        _MIKU_FaceCenterOS("Face Center OS", Vector) = (0,0,0,0)
        _MIKU_FaceExtentOS("Face Extent OS", Vector) = (1,1,1,0)
        _MIKU_FaceRembrandt("Face Rembrandt", Range(0,1)) = 0
        _MIKU_FaceBlush("Face Blush", Range(0,1)) = 0
        _MIKU_MetallicAccent("Metallic Accent", Range(0,2)) = 0
        [HideInInspector] _MIKU_SemanticMode("Semantic Mode", Float) = 3
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "MikuGenericToonCommon.hlsl"
        ENDHLSL

        Pass
        {
            Name "UniversalForwardOnly"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MikuToonVertex
            #pragma fragment MikuToonFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Back ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MikuToonVertex
            #pragma fragment MikuDepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Back ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MikuToonVertex
            #pragma fragment MikuDepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MikuToonVertex
            #pragma fragment MikuDepthNormalsFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode"="MotionVectors" }
            Cull Back ZWrite On ColorMask RG
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MikuMotionVertex
            #pragma fragment MikuMotionFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "MikuToonOutline"
            Tags { "LightMode"="MikuToonOutline" }
            Cull Front ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MikuOutlineVertex
            #pragma fragment MikuOutlineFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "MikuToonCharacterMask"
            Tags { "LightMode"="MikuToonCharacterMask" }
            Cull Back ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex MikuToonVertex
            #pragma fragment MikuCharacterMaskFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    CustomEditor "Miku.ShaderConverter.Editor.MikuGenericToonShaderGUI"
    Fallback Off
}
