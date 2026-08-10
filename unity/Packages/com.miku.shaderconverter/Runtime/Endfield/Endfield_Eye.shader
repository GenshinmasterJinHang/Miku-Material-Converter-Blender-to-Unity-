// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Endfield/Eye"
{
    Properties
    {
        [MainTexture] _BaseMap ("Eye Base", 2D) = "white" {}
        _DiffRampMap ("Eye Diffuse Ramp", 2D) = "white" {}
        _MatCap ("Eye MatCap", 2D) = "black" {}
        [MainColor] _BaseColorTint ("Base Tint", Color) = (1,1,1,1)
        [Toggle] _UseDiffRampMap ("Use Eye Diffuse Ramp", Float) = 0
        [Toggle] _UseMatCap ("Use MatCap", Float) = 0
        _IndirectIntensity ("Indirect", Range(0,2)) = 0.5
        _SpecularIntensity ("Specular", Range(0,4)) = 0.5
        _IrisParallaxDepth ("Iris Parallax Depth", Range(0,0.03)) = 0.008
        _CorneaBumpStrength ("Cornea Bump Strength", Range(0,1)) = 0.25
        _CorneaSpecularIntensity ("Cornea Specular", Range(0,4)) = 1
        [HDR] _CorneaHighlightColor ("Cornea Highlight Color", Color) = (1,1,1,1)
        _EyeRampStrength ("Eye Ramp Strength", Range(0,1)) = 1
        _EyeCenterColor ("Iris Center Color", Color) = (1,1,1,1)
        _EyeAlphaColor ("Iris Alpha Region Color", Color) = (1,1,1,1)
        _MatCapAlphaColor ("MatCap Alpha Region Color", Color) = (1,1,1,1)
        _MatCapAlphaStrength ("MatCap Alpha Color Strength", Range(0,1)) = 0.5
        _SelfAoShadowStrength ("Minimum MatCap Shadow", Range(0,1)) = 0.5
        _DarkColorStrength ("Dark Color Strength", Range(0,1)) = 1
        _DarkColorSaturation ("Dark Color Saturation", Range(0,2)) = 1
        _DarkInDarkStrength ("Dark-in-Dark Strength", Range(0,1)) = 0.65
        _BackLightCompensation ("Back-Light Compensation", Range(0,2)) = 0.75
        _NoFStrength ("Normal-Facing Ramp Strength", Range(0,1)) = 1
        _RampColorStrength ("Ramp Color Strength", Range(0,1)) = 0.65
        _DiffuseAlphaEnergy ("Base Alpha Diffuse Energy", Range(0,1)) = 0
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001,0.5)) = 0.12
        _ShadowCenter ("Character Shadow Center", Range(0,1)) = 0.5
        _ShadowSigmoidSmoothness ("Character Shadow Sigmoid Smoothness", Range(0.001,0.5)) = 0.12
        _ShadowOffset ("Character Shadow Offset", Range(-1,1)) = 0
        _ShadowStrength ("Character Shadow Strength", Range(0,2)) = 1
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
