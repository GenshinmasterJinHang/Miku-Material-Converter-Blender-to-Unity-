// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Endfield/Mouth"
{
    Properties
    {
        [MainTexture] _BaseMap ("Optional Mouth Map", 2D) = "white" {}
        [MainColor] _BaseColorTint ("Mouth Color", Color) = (0.24,0.035,0.045,1)
        _IndirectIntensity ("Indirect", Range(0,2)) = 0.45
        _SpecularIntensity ("Specular", Range(0,4)) = 0.1
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001,0.5)) = 0.12
        [HideInInspector] _PartMode ("Part", Float) = 5
        [HideInInspector] _MouthMode ("Mouth Mode", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 0
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 0
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _StencilPass ("Stencil Pass", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+2" }
        UsePass "Hidden/MIKU/Endfield/PassLibrary/UniversalForward"
        UsePass "Hidden/MIKU/Endfield/PassLibrary/DepthOnly"
    }
    CustomEditor "Miku.ShaderConverter.Editor.MikuManualTextureShaderGUI"
}
