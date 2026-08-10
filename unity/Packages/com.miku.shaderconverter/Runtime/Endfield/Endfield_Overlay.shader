// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Endfield/Overlay"
{
    Properties
    {
        [MainTexture] _BaseMap ("Overlay Map", 2D) = "white" {}
        _NormalMap ("Raw RG Normal", 2D) = "white" {}
        _MaterialParamMap ("Material Params (R Metal G Reflect B AO A Smooth)", 2D) = "white" {}
        _DiffRampMap ("Diffuse Ramp", 2D) = "white" {}
        _SpecRampMap ("Specular Ramp", 2D) = "white" {}
        _SpecularRefineF0Tex ("Specular Refine F0 LUT", 2D) = "white" {}
        _SpecularRefineColorTex ("Specular Refine Color", 2D) = "white" {}
        _ColorLutTex ("Dark Color LUT (32x32x32)", 2D) = "white" {}
        [HideInInspector] _ShadowLutTex ("Legacy Shadow LUT", 2D) = "white" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        [MainColor] _BaseColorTint ("Overlay Tint", Color) = (1,1,1,1)
        [Enum(LegacyUnlit,0,ToonLitTransparent,1)] _LightingMode ("Lighting Mode", Float) = 0
        [Toggle] _OverlayUseTintOnly ("Use Mask With Tint Only", Float) = 0
        [Toggle] _UseNormalMap ("Use Normal", Float) = 0
        [Toggle] _UseMaterialParamMap ("Use Material Params", Float) = 0
        [Toggle] _UseDiffRampMap ("Use Diffuse Ramp", Float) = 0
        [Toggle] _UseSpecRampMap ("Use Specular Ramp", Float) = 0
        [Toggle] _UseSpecularRefine ("Use Specular Refine", Float) = 0
        [Toggle] _UseColorLut ("Use Dark Color LUT", Float) = 0
        [HideInInspector] _UseShadowLut ("Use Legacy LUT", Float) = 0
        [Toggle] _UseEmissionMap ("Use Emission", Float) = 0
        _NormalStrength ("Normal Strength", Range(0,1)) = 1
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001,0.5)) = 0.08
        _ShadowCenter ("Character Shadow Center", Range(0,1)) = 0.5
        _ShadowSigmoidSmoothness ("Character Shadow Sigmoid Smoothness", Range(0.001,0.5)) = 0.12
        _ShadowOffset ("Character Shadow Offset", Range(-1,1)) = 0
        _ShadowStrength ("Character Shadow Strength", Range(0,2)) = 1
        _IndirectIntensity ("Indirect", Range(0,2)) = 0.35
        _SpecularIntensity ("Specular", Range(0,4)) = 0.8
        _SpecularRefineColor ("Specular Refine Color Tint", Color) = (1,1,1,1)
        _SpecularRefineColorStrength ("Specular Refine Color Strength", Range(0,1)) = 1
        _SelfAoShadowStrength ("Minimum Specular AO", Range(0,1)) = 0.5
        _EnvironmentRotation ("Environment Rotation", Range(0,360)) = 0
        _EnvironmentMipBias ("Environment Mip Bias", Range(-1,1)) = 0
        _MetalDirectBoost ("Metal Direct Boost", Range(0,4)) = 1
        _MetalEnvironmentBoost ("Metal Environment Boost", Range(0,4)) = 1
        _EmissionIntensity ("Emission", Range(0,8)) = 1
        _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        [Enum(R Mask,0,Authored RGB,1,RGB x Base Alpha,2)] _EmissionMapMode ("Emission Map Mode", Float) = 1
        _DarkColorStrength ("Dark Color Strength", Range(0,1)) = 1
        _DarkColorSaturation ("Dark Color Saturation", Range(0,2)) = 1
        _DarkInDarkStrength ("Dark-in-Dark Strength", Range(0,1)) = 0.65
        _BackLightCompensation ("Back-Light Compensation", Range(0,2)) = 0.75
        _NoFStrength ("Normal-Facing Ramp Strength", Range(0,1)) = 1
        _NoFPowStrength ("Normal-Facing Ramp Power", Range(1,3)) = 1
        _RefineF0U_lerp ("Specular Refine F0 U Blend", Range(0,1)) = 0
        _RampColorStrength ("Ramp Color Strength", Range(0,1)) = 0.65
        _DiffuseAlphaEnergy ("Base Alpha Diffuse Energy", Range(0,1)) = 0
        _ClothSssStrength ("Cloth SSS Strength", Range(0,2)) = 0
        _ClothSssPower ("Cloth SSS Power", Range(0.25,8)) = 2
        _ClothSssColor ("Cloth SSS Color", Color) = (1,0.48,0.38,1)
        _RimLightTintColor ("Surface Rim Tint", Color) = (0.8,0.9,1,1)
        _SurfaceRimStrength ("Surface Rim Strength", Range(0,2)) = 0
        _SurfaceRimPower ("Surface Rim Power", Range(0.5,12)) = 5
        _SurfaceRimLightAlign ("Surface Rim Light Align", Range(0,1)) = 0.6
        _LightRimStrength ("Directional Rim Strength", Range(0,2)) = 0.12
        _LightRimPower ("Directional Rim Power", Range(0.5,12)) = 2
        [HideInInspector] _DebugView ("Debug View", Float) = 0
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
