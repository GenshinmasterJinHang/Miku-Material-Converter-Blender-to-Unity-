// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Endfield/Body"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _NormalMap ("Raw RG Normal", 2D) = "white" {}
        _MaterialParamMap ("Material Params (R Metal G Reflect B AO A Smooth)", 2D) = "white" {}
        _DiffRampMap ("Diffuse Ramp", 2D) = "white" {}
        _SpecRampMap ("Specular Ramp", 2D) = "white" {}
        _SpecularRefineF0Tex ("Specular Refine F0 LUT", 2D) = "white" {}
        _SpecularRefineColorTex ("Specular Refine Color", 2D) = "white" {}
        _ColorLutTex ("Dark Color LUT (32x32x32)", 2D) = "white" {}
        [HideInInspector] _ShadowLutTex ("Legacy Shadow LUT", 2D) = "white" {}
        _EmissionMap ("Emission", 2D) = "black" {}
        _OutlineMask ("Outline Mask", 2D) = "white" {}
        [MainColor] _BaseColorTint ("Base Tint", Color) = (1,1,1,1)
        [Toggle] _UseNormalMap ("Use Normal", Float) = 0
        [Toggle] _UseMaterialParamMap ("Use Material Params", Float) = 0
        [Toggle] _UseDiffRampMap ("Use Diffuse Ramp", Float) = 0
        [Toggle] _UseSpecRampMap ("Use Specular Ramp", Float) = 0
        [Toggle] _UseSpecularRefine ("Use Specular Refine", Float) = 0
        [Toggle] _UseColorLut ("Use Dark Color LUT", Float) = 0
        [HideInInspector] _UseShadowLut ("Use Legacy LUT", Float) = 0
        [Toggle] _UseEmissionMap ("Use Emission", Float) = 0
        [Toggle] _UseOutline ("Use Outline", Float) = 1
        [Toggle] _UseOutlineMask ("Use Outline Mask", Float) = 0
        [HideInInspector] _MikuEndfieldMaterialStateVersion ("Miku Endfield Material State Version", Float) = 0
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
        [Enum(R Mask,0,Authored RGB,1,RGB x Base Alpha,2)] _EmissionMapMode ("Emission Map Mode", Float) = 0
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
        [HideInInspector] _DebugView ("Debug View", Float) = 0
        _OutlineWidth ("Outline Width", Range(0,0.02)) = 0.0014
        _OutlineReferenceDistance ("Outline Reference Distance", Float) = 5
        _OutlineDistanceScale ("Outline Distance Scale", Range(0,1)) = 1
        _OutlineGamma ("Outline Gamma", Range(0.1,4)) = 1.5
        _OutlineColorTint ("Outline Tint", Color) = (0.16,0.09,0.12,1)
        _OutlineMaskInvert ("Outline Mask Invert", Float) = 0
        _RimLightBrightness ("Screen Rim Brightness", Range(0,4)) = 0.18
        _RimLightTintColor ("Screen Rim Tint", Color) = (0.8,0.9,1,1)
        _RimLightWidth ("Screen Rim Width", Range(0,10)) = 1
        _RimLightThreshold ("Screen Rim Threshold", Range(0,1)) = 0.03
        _RimLightFadeout ("Screen Rim Fade", Range(0.001,1)) = 0.2
        _SurfaceRimStrength ("Surface Rim Strength", Range(0,2)) = 0
        _SurfaceRimPower ("Surface Rim Power", Range(0.5,12)) = 5
        _SurfaceRimLightAlign ("Surface Rim Light Align", Range(0,1)) = 0.6
        _LightRimStrength ("Directional Rim Strength", Range(0,2)) = 0.12
        _LightRimPower ("Directional Rim Power", Range(0.5,12)) = 2
        [HideInInspector] _PartMode ("Part", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
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
