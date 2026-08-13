// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
Shader "MIKU/Endfield/Face"
{
    Properties
    {
        [MainTexture] _BaseMap ("Face Base", 2D) = "white" {}
        _DiffRampMap ("Face Ramp", 2D) = "white" {}
        _ColorLutTex ("Skin Dark Color LUT (32x32x32)", 2D) = "white" {}
        [HideInInspector] _ShadowLutTex ("Legacy Skin LUT", 2D) = "white" {}
        _SDFLightmap ("Face SDF", 2D) = "white" {}
        _FaceAreaMap ("Face Area Mask (cm_M)", 2D) = "white" {}
        _FaceRefineMap ("Face Refine (ST)", 2D) = "white" {}
        [HideInInspector] _SDFMask ("Legacy Face Area Mask", 2D) = "white" {}
        _EmotionMap ("Emotion Atlas", 2D) = "black" {}
        _HighlightMap ("Highlight Mask", 2D) = "black" {}
        _OutlineMask ("Outline Mask", 2D) = "white" {}
        [MainColor] _BaseColorTint ("Base Tint", Color) = (1,1,1,1)
        [Toggle] _UseDiffRampMap ("Use Face Ramp", Float) = 0
        [Toggle] _UseColorLut ("Use Skin Color LUT", Float) = 0
        [HideInInspector] _UseShadowLut ("Use Legacy LUT", Float) = 0
        [Toggle] _UseFaceSDF ("Use Face SDF", Float) = 1
        [Toggle] _UseFaceAreaMap ("Use Face Area Mask", Float) = 0
        [Toggle] _UseFaceRefineMap ("Use Face Refine", Float) = 0
        [HideInInspector] _UseSDFMask ("Use Legacy Face Area", Float) = 0
        [Toggle] _UseEmotionMap ("Use Emotion", Float) = 0
        [Toggle] _UseHighlightMap ("Use Highlight", Float) = 0
        [Toggle] _UseOutline ("Use Outline", Float) = 1
        [Toggle] _UseOutlineMask ("Use Outline Mask", Float) = 0
        [HideInInspector] _MikuEndfieldMaterialStateVersion ("Miku Endfield Material State Version", Float) = 0
        _FaceShadowOffset ("Face Shadow Offset", Range(-1,1)) = 0
        _FaceShadowSoftness ("Face Shadow Softness", Range(0.001,0.5)) = 0.035
        [Toggle] _UseManualFaceBasis ("Use Manual Face Basis", Float) = 0
        _FaceRightOS ("Face Right (Object Space)", Vector) = (1,0,0,0)
        _FaceForwardOS ("Face Forward (Object Space)", Vector) = (0,-1,0,0)
        _FaceUpOS ("Face Up (Object Space)", Vector) = (0,0,1,0)
        [HideInInspector] _MikuHeadForwardWS ("Miku Head Forward WS", Vector) = (0,0,1,0)
        [HideInInspector] _MikuHeadRightWS ("Miku Head Right WS", Vector) = (1,0,0,0)
        [HideInInspector] _MikuHeadUpWS ("Miku Head Up WS", Vector) = (0,1,0,0)
        [HideInInspector] _MikuHeadAxesValid ("Miku Head Axes Valid", Float) = 0
        _BackLightStrength ("Face Back Light Compensation", Range(0,1)) = 0.25
        _HeadCenterOS ("Head Center (Object Space)", Vector) = (0,0,0,1)
        _EmotionTileIndex ("Emotion Tile", Float) = 0
        _EmotionColumns ("Emotion Columns", Float) = 2
        _EmotionRows ("Emotion Rows", Float) = 2
        _BlushTileIndex ("Blush Tile", Float) = 0
        _BlushStrength ("Blush Strength", Range(0,1)) = 0
        _BlushMaskGain ("Blush Mask Gain", Range(0,8)) = 3
        _BlushColor ("Blush Color", Color) = (1,0.82,0.88,1)
        _FaceHighlightIntensity ("Face Highlight", Range(0,1)) = 0.12
        _SkinSSSIntensity ("Face SSS", Range(0,1)) = 0.1
        _SSSColor ("SSS Color", Color) = (1,0.5,0.4,1)
        _SSSArea ("SSS Area", Range(0,2)) = 0.35
        _SkinToneBrightness ("Skin Tone Brightness", Range(0,2)) = 1
        _SkinToneWhitening ("Skin Tone Whitening", Range(0,1)) = 0
        _SkinToneTarget ("Skin Tone Target", Color) = (1,0.93,0.90,1)
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001,0.5)) = 0.08
        _ShadowCenter ("Character Shadow Center", Range(0,1)) = 0.5
        _ShadowSigmoidSmoothness ("Character Shadow Sigmoid Smoothness", Range(0.001,0.5)) = 0.12
        _ShadowOffset ("Character Shadow Offset", Range(-1,1)) = 0
        _ShadowStrength ("Character Shadow Strength", Range(0,2)) = 1
        _IndirectIntensity ("Indirect", Range(0,2)) = 0.45
        _SpecularIntensity ("Specular", Range(0,4)) = 0.15
        _DarkColorStrength ("Dark Color Strength", Range(0,1)) = 1
        _DarkColorSaturation ("Dark Color Saturation", Range(0,2)) = 0.92
        _DarkInDarkStrength ("Dark-in-Dark Strength", Range(0,1)) = 0.65
        _BackLightCompensation ("Back-Light Compensation", Range(0,2)) = 0.6
        _NoFStrength ("Normal-Facing Ramp Strength", Range(0,1)) = 0
        _RampColorStrength ("Ramp Color Strength", Range(0,1)) = 0.35
        _DiffuseAlphaEnergy ("Base Alpha Diffuse Energy", Range(0,1)) = 0
        _FaceSdfNormalStrength ("Face SDF-B Normal Strength", Range(0,1)) = 1
        _FaceRimMaskStrength ("Face Refine-W Rim Mask Strength", Range(0,1)) = 0.35
        _FaceRimSideStrength ("Face One-Sided Rim Strength", Range(0,2)) = 0.1
        _RimLightArea ("Face Rim Area", Range(0,1)) = 1
        _RimLightDiffuseColorEffect ("Face Rim Diffuse Color Effect", Range(0,1)) = 0.1
        [HideInInspector] _FaceRoughness ("Face Roughness", Range(0.06,1)) = 0.42
        [HideInInspector] _FaceReflectivity ("Face Reflectivity", Range(0,1)) = 0.35
        [HideInInspector] _DebugView ("Debug View", Float) = 0
        _OutlineWidth ("Outline Width", Range(0,0.02)) = 0.0008
        _OutlineReferenceDistance ("Outline Reference Distance", Float) = 5
        _OutlineDistanceScale ("Outline Distance Scale", Range(0,1)) = 1
        _OutlineGamma ("Outline Gamma", Range(0.1,4)) = 1.6
        _OutlineColorTint ("Outline Tint", Color) = (0.22,0.12,0.13,1)
        _OutlineMaskInvert ("Outline Mask Invert", Float) = 0
        _RimLightBrightness ("Screen Rim Brightness", Range(0,4)) = 0.12
        _RimLightTintColor ("Screen Rim Tint", Color) = (1,0.85,0.78,1)
        _RimLightWidth ("Screen Rim Width", Range(0,10)) = 1
        _RimLightThreshold ("Screen Rim Threshold", Range(0,1)) = 0.03
        _RimLightFadeout ("Screen Rim Fade", Range(0.001,1)) = 0.2
        _SurfaceRimStrength ("Surface Rim Strength", Range(0,2)) = 0
        _SurfaceRimPower ("Surface Rim Power", Range(0.5,12)) = 5
        _SurfaceRimLightAlign ("Surface Rim Light Align", Range(0,1)) = 0.6
        _LightRimStrength ("Directional Rim Strength", Range(0,2)) = 0.06
        _LightRimPower ("Directional Rim Power", Range(0.5,12)) = 2.5
        [HideInInspector] _PartMode ("Part", Float) = 3
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 36
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _StencilPass ("Stencil Pass", Float) = 2
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
