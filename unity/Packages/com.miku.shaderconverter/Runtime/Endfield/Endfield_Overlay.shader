// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Endfield/Overlay"
{
    Properties
    {
        [MainTexture] _BaseMap ("Overlay Map", 2D) = "white" {}
        [MainColor] _BaseColorTint ("Overlay Tint", Color) = (1,1,1,1)
        [Toggle] _OverlayUseTintOnly ("Use Mask With Tint Only", Float) = 0
        [Enum(Texture Alpha,0,Luminance,1,Inverse Red,2,Raw Red,3,Opaque,4)] _AlphaSource ("Alpha Source", Float) = 0
        _AlphaClip ("Alpha Clip", Range(0,1)) = 0.02
        [HideInInspector] _PartMode ("Part", Float) = 6
        [HideInInspector] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        UsePass "Hidden/MIKU/Endfield/PassLibrary/TransparentForward"
    }
    CustomEditor "Miku.ShaderConverter.Editor.MikuManualTextureShaderGUI"
}
