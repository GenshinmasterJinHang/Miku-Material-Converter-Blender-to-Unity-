// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Endfield/Skin"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _NormalMap ("Raw RG Normal", 2D) = "white" {}
        _DiffRampMap ("Diffuse Ramp", 2D) = "white" {}
        _ColorLutTex ("Skin Dark Color LUT (32x32x32)", 2D) = "white" {}
        [HideInInspector] _ShadowLutTex ("Legacy Skin LUT", 2D) = "white" {}
        _OutlineMask ("Outline Mask", 2D) = "white" {}
        [MainColor] _BaseColorTint ("Base Tint", Color) = (1,1,1,1)
        [Toggle] _UseNormalMap ("Use Normal", Float) = 0
        [Toggle] _UseDiffRampMap ("Use Diffuse Ramp", Float) = 0
        [Toggle] _UseColorLut ("Use Skin Color LUT", Float) = 0
        [HideInInspector] _UseShadowLut ("Use Legacy LUT", Float) = 0
        [Toggle] _UseOutlineMask ("Use Outline Mask", Float) = 0
        _NormalStrength ("Normal Strength", Range(0,1)) = 1
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001,0.5)) = 0.1
        _ShadowCenter ("Character Shadow Center", Range(0,1)) = 0.5
        _ShadowSigmoidSmoothness ("Character Shadow Sigmoid Smoothness", Range(0.001,0.5)) = 0.12
        _ShadowOffset ("Character Shadow Offset", Range(-1,1)) = 0
        _ShadowStrength ("Character Shadow Strength", Range(0,2)) = 1
        _IndirectIntensity ("Indirect", Range(0,2)) = 0.4
        _SpecularIntensity ("Specular", Range(0,4)) = 0.3
        _SkinSSSIntensity ("Wrapped SSS", Range(0,1)) = 0.12
        _SSSColor ("SSS Color", Color) = (1,0.5,0.4,1)
        _SSSArea ("SSS Area", Range(0,2)) = 0.3
        _SkinToneBrightness ("Skin Tone Brightness", Range(0,2)) = 1
        _SkinToneWhitening ("Skin Tone Whitening", Range(0,1)) = 0
        _SkinToneTarget ("Skin Tone Target", Color) = (1,0.93,0.90,1)
        _SkinAOStrength ("Skin AO Strength", Range(0,1)) = 0.35
        _SkinRoughness ("Skin Roughness", Range(0.06,1)) = 0.42
        _SkinReflectivity ("Skin Reflectivity", Range(0,1)) = 0.35
        _DarkColorStrength ("Dark Color Strength", Range(0,1)) = 1
        _DarkColorSaturation ("Dark Color Saturation", Range(0,2)) = 0.92
        _DarkInDarkStrength ("Dark-in-Dark Strength", Range(0,1)) = 0.65
        _BackLightCompensation ("Back-Light Compensation", Range(0,2)) = 0.55
        _NoFStrength ("Normal-Facing Ramp Strength", Range(0,1)) = 0
        _RampColorStrength ("Ramp Color Strength", Range(0,1)) = 0.45
        _DiffuseAlphaEnergy ("Base Alpha Diffuse Energy", Range(0,1)) = 0
        [HideInInspector] _DebugView ("Debug View", Float) = 0
        _OutlineWidth ("Outline Width", Range(0,0.02)) = 0.001
        _OutlineReferenceDistance ("Outline Reference Distance", Float) = 5
        _OutlineDistanceScale ("Outline Distance Scale", Range(0,1)) = 1
        _OutlineGamma ("Outline Gamma", Range(0.1,4)) = 1.5
        _OutlineColorTint ("Outline Tint", Color) = (0.22,0.12,0.13,1)
        _OutlineMaskInvert ("Outline Mask Invert", Float) = 0
        _RimLightBrightness ("Screen Rim Brightness", Range(0,4)) = 0.14
        _RimLightTintColor ("Screen Rim Tint", Color) = (1,0.82,0.76,1)
        _RimLightWidth ("Screen Rim Width", Range(0,10)) = 1
        _RimLightThreshold ("Screen Rim Threshold", Range(0,1)) = 0.03
        _RimLightFadeout ("Screen Rim Fade", Range(0.001,1)) = 0.2
        _SurfaceRimStrength ("Surface Rim Strength", Range(0,2)) = 0
        _SurfaceRimPower ("Surface Rim Power", Range(0.5,12)) = 5
        _SurfaceRimLightAlign ("Surface Rim Light Align", Range(0,1)) = 0.6
        _LightRimStrength ("Directional Rim Strength", Range(0,2)) = 0.08
        _LightRimPower ("Directional Rim Power", Range(0.5,12)) = 2.5
        [HideInInspector] _PartMode ("Part", Float) = 1
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 0
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 0
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _StencilPass ("Stencil Pass", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        UsePass "Hidden/MIKU/Endfield/PassLibrary/UniversalForward"
        UsePass "Hidden/MIKU/Endfield/PassLibrary/MikuToonCharacterMask"
        UsePass "Hidden/MIKU/Endfield/PassLibrary/Outline"
        UsePass "Hidden/MIKU/Endfield/PassLibrary/ShadowCaster"
        UsePass "Hidden/MIKU/Endfield/PassLibrary/DepthOnly"
    }
    CustomEditor "Miku.ShaderConverter.Editor.MikuManualTextureShaderGUI"
}
