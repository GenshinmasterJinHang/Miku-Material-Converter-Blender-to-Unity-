// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Endfield/Hair"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _MaterialParamMap ("Material Params", 2D) = "white" {}
        _DiffRampMap ("Diffuse Ramp", 2D) = "white" {}
        _SpecRampMap ("Specular Ramp", 2D) = "white" {}
        _SplitNormalMap ("Split Normal RG/BA", 2D) = "white" {}
        _SpecularMask ("Specular Mask", 2D) = "white" {}
        _SpecularRefineF0Tex ("Specular Refine F0 LUT", 2D) = "white" {}
        _HairLineMap ("Hair Line Detail", 2D) = "black" {}
        _HairShiftMap ("Hair Tangent Shift", 2D) = "gray" {}
        _HairRefineMap ("Hair Refine (ST)", 2D) = "white" {}
        [HideInInspector] _LineMap ("Legacy Hair Line", 2D) = "black" {}
        [HideInInspector] _StrokeMap ("Legacy Hair Shift", 2D) = "gray" {}
        _OutlineMask ("Outline Mask", 2D) = "white" {}
        [MainColor] _BaseColorTint ("Base Tint", Color) = (1,1,1,1)
        [Toggle] _UseMaterialParamMap ("Use Material Params", Float) = 0
        [Toggle] _UseDiffRampMap ("Use Diffuse Ramp", Float) = 0
        [Toggle] _UseSpecRampMap ("Use Specular Ramp", Float) = 0
        [Toggle] _UseSplitNormalMap ("Use Split Normal", Float) = 0
        [Toggle] _UseSpecularMask ("Use Specular Mask", Float) = 0
        [Toggle] _UseSpecularRefine ("Use Specular Refine", Float) = 0
        [Enum(Color RGB,0,Scalar R,1)] _HairSpecularLutMode ("Hair Specular LUT Mode", Float) = 0
        [Toggle] _UseHairLineMap ("Use Hair Line", Float) = 0
        [Toggle] _UseHairShiftMap ("Use Hair Shift", Float) = 0
        [Toggle] _UseHairRefineMap ("Use Hair Refine", Float) = 0
        [HideInInspector] _UseLineMap ("Use Legacy Hair Line", Float) = 0
        [HideInInspector] _UseStrokeMap ("Use Legacy Hair Shift", Float) = 0
        [Toggle] _UseOutlineMask ("Use Outline Mask", Float) = 0
        [Toggle] _UseHeadSphereNormal ("Use Head Sphere Normal", Float) = 1
        _HeadCenterOS ("Head Center (Object Space)", Vector) = (0,0,0,1)
        _NormalStrength ("Split Normal Strength", Range(0,1)) = 1
        _HairLineIntensity ("Hair Line Intensity", Range(0,0.5)) = 0.08
        _HairShiftIntensity ("Hair Shift Intensity", Range(0,2)) = 0.45
        _HairPrimaryWidth ("Primary Highlight Width", Range(1,64)) = 8
        _HairSecondaryWidth ("Secondary Highlight Width", Range(1,128)) = 40
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001,0.5)) = 0.07
        _IndirectIntensity ("Indirect", Range(0,2)) = 0.25
        _SpecularIntensity ("Specular", Range(0,4)) = 1.2
        _SelfAoShadowStrength ("Minimum Specular AO", Range(0,1)) = 0.5
        [HideInInspector] _DebugView ("Debug View", Float) = 0
        _OutlineWidth ("Outline Width", Range(0,0.02)) = 0.0016
        _OutlineReferenceDistance ("Outline Reference Distance", Float) = 5
        _OutlineDistanceScale ("Outline Distance Scale", Range(0,1)) = 1
        _OutlineGamma ("Outline Gamma", Range(0.1,4)) = 1.7
        _OutlineColorTint ("Outline Tint", Color) = (0.10,0.06,0.09,1)
        _OutlineMaskInvert ("Outline Mask Invert", Float) = 0
        _RimLightBrightness ("Screen Rim Brightness", Range(0,4)) = 0.22
        _RimLightTintColor ("Screen Rim Tint", Color) = (0.72,0.86,1,1)
        _RimLightWidth ("Screen Rim Width", Range(0,10)) = 1
        _RimLightThreshold ("Screen Rim Threshold", Range(0,1)) = 0.03
        _RimLightFadeout ("Screen Rim Fade", Range(0.001,1)) = 0.2
        _SurfaceRimStrength ("Surface Rim Strength", Range(0,2)) = 0
        _SurfaceRimPower ("Surface Rim Power", Range(0.5,12)) = 5
        _SurfaceRimLightAlign ("Surface Rim Light Align", Range(0,1)) = 0.6
        [HideInInspector] _PartMode ("Part", Float) = 2
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
