// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Endfield/Eye"
{
    Properties
    {
        [MainTexture] _BaseMap ("Eye Base", 2D) = "white" {}
        _MatCap ("Eye MatCap", 2D) = "black" {}
        [MainColor] _BaseColorTint ("Base Tint", Color) = (1,1,1,1)
        [Toggle] _UseMatCap ("Use MatCap", Float) = 0
        _IndirectIntensity ("Indirect", Range(0,2)) = 0.5
        _SpecularIntensity ("Specular", Range(0,4)) = 0.5
        _IrisParallaxDepth ("Iris Parallax Depth", Range(0,0.03)) = 0.008
        _CorneaBumpStrength ("Cornea Bump Strength", Range(0,1)) = 0.25
        _CorneaSpecularIntensity ("Cornea Specular", Range(0,4)) = 1
        [HDR] _CorneaHighlightColor ("Cornea Highlight Color", Color) = (1,1,1,1)
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001,0.5)) = 0.12
        [HideInInspector] _PartMode ("Part", Float) = 4
        [HideInInspector] _EyeMode ("Eye Mode", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 0
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 0
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _StencilPass ("Stencil Pass", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        UsePass "Hidden/MIKU/Endfield/PassLibrary/UniversalForward"
        UsePass "Hidden/MIKU/Endfield/PassLibrary/DepthOnly"
    }
    CustomEditor "Miku.ShaderConverter.Editor.MikuManualTextureShaderGUI"
}
