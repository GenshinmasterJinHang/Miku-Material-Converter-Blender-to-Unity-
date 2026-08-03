// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Endfield/Effect"
{
    Properties
    {
        [MainTexture] _BaseMap ("Effect Base", 2D) = "white" {}
        _EffectMask ("Effect Mask", 2D) = "white" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        [MainColor] _BaseColorTint ("Effect Tint", Color) = (1,1,1,1)
        [Toggle] _UseEffectMask ("Use Effect Mask", Float) = 0
        [Toggle] _UseEmissionMap ("Use Emission", Float) = 0
        _EmissionIntensity ("Emission", Range(0,16)) = 1
        [Enum(Texture Alpha,0,Luminance,1,Inverse Red,2)] _AlphaSource ("Alpha Source", Float) = 0
        _AlphaClip ("Alpha Clip", Range(0,1)) = 0.01
        [HideInInspector] _PartMode ("Part", Float) = 7
        [HideInInspector] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        UsePass "Hidden/MIKU/Endfield/PassLibrary/TransparentForward"
    }
    CustomEditor "Miku.ShaderConverter.Editor.MikuManualTextureShaderGUI"
}
