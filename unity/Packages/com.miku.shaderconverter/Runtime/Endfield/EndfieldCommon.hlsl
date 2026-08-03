// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
#ifndef MIKU_ENDFIELD_COMMON_INCLUDED
#define MIKU_ENDFIELD_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Share URP's inline samplers. The pass exposes more packed maps than D3D11's
// sixteen active sampler registers, while the authored assets need only two
// sampling intents: trilinear repeat and linear clamp.
TEXTURE2D(_BaseMap);
TEXTURE2D(_NormalMap);
TEXTURE2D(_MaterialParamMap);
TEXTURE2D(_DiffRampMap);
TEXTURE2D(_SpecRampMap);
TEXTURE2D(_SpecularRefineF0Tex);
TEXTURE2D(_SpecularRefineColorTex);
TEXTURE2D(_ColorLutTex);
TEXTURE2D(_ShadowLutTex);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_MatCap);
TEXTURE2D(_SplitNormalMap);
TEXTURE2D(_OutlineMask);
TEXTURE2D(_SpecularMask);
TEXTURE2D(_HairLineMap);
TEXTURE2D(_HairShiftMap);
TEXTURE2D(_HairRefineMap);
TEXTURE2D(_LineMap);
TEXTURE2D(_StrokeMap);
TEXTURE2D(_SDFLightmap);
TEXTURE2D(_FaceAreaMap);
TEXTURE2D(_FaceRefineMap);
TEXTURE2D(_SDFMask);
TEXTURE2D(_EmotionMap);
TEXTURE2D(_HighlightMap);
TEXTURE2D(_EffectMask);

#define sampler_BaseMap sampler_TrilinearRepeat
#define sampler_NormalMap sampler_TrilinearRepeat
#define sampler_MaterialParamMap sampler_TrilinearRepeat
#define sampler_EmissionMap sampler_TrilinearRepeat
#define sampler_SplitNormalMap sampler_TrilinearRepeat
#define sampler_HairLineMap sampler_TrilinearRepeat
#define sampler_HairShiftMap sampler_TrilinearRepeat
#define sampler_LineMap sampler_TrilinearRepeat
#define sampler_StrokeMap sampler_TrilinearRepeat
#define sampler_DiffRampMap sampler_LinearClamp
#define sampler_SpecRampMap sampler_LinearClamp
#define sampler_SpecularRefineF0Tex sampler_LinearClamp
#define sampler_SpecularRefineColorTex sampler_LinearClamp
#define sampler_ColorLutTex sampler_LinearClamp
#define sampler_ShadowLutTex sampler_LinearClamp
#define sampler_MatCap sampler_LinearClamp
#define sampler_OutlineMask sampler_LinearClamp
#define sampler_SpecularMask sampler_LinearClamp
#define sampler_HairRefineMap sampler_LinearClamp
#define sampler_SDFLightmap sampler_LinearClamp
#define sampler_FaceAreaMap sampler_LinearClamp
#define sampler_FaceRefineMap sampler_LinearClamp
#define sampler_SDFMask sampler_LinearClamp
#define sampler_EmotionMap sampler_LinearClamp
#define sampler_HighlightMap sampler_LinearClamp
#define sampler_EffectMask sampler_LinearClamp

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseColorTint;
float4 _OutlineColorTint;
float4 _RimLightTintColor;
float4 _MouthColor;
float4 _EmissionColor;
float4 _HeadCenterOS;
float4 _CorneaHighlightColor;
float4 _BlushColor;
float4 _SpecularRefineColor;
float4 _SSSColor;
float4 _SkinToneTarget;
float4 _FaceRightOS;
float4 _FaceForwardOS;
float4 _FaceUpOS;
float _PartMode;
float _EyeMode;
float _MouthMode;
float _UseNormalMap;
float _UseMaterialParamMap;
float _UseDiffRampMap;
float _UseSpecRampMap;
float _UseColorLut;
float _UseShadowLut;
float _UseEmissionMap;
float _UseMatCap;
float _UseSplitNormalMap;
float _UseOutlineMask;
float _UseSpecularMask;
float _UseHairLineMap;
float _UseHairShiftMap;
float _UseHairRefineMap;
float _UseLineMap;
float _UseStrokeMap;
float _UseFaceSDF;
float _UseFaceAreaMap;
float _UseFaceRefineMap;
float _UseSDFMask;
float _UseEmotionMap;
float _UseHighlightMap;
float _UseEffectMask;
float _UseHeadSphereNormal;
float _UseSpecularRefine;
float _UseManualFaceBasis;
float _OverlayUseTintOnly;
float _NormalStrength;
float _ShadowSmoothness;
float _IndirectIntensity;
float _SpecularIntensity;
float _EmissionIntensity;
float _SkinSSSIntensity;
float _SkinAOStrength;
float _FaceShadowOffset;
float _FaceShadowSoftness;
float _FaceHighlightIntensity;
float _BlushStrength;
float _BlushTileIndex;
float _BlushMaskGain;
float _BackLightStrength;
float _SSSArea;
float _SelfAoShadowStrength;
float _SpecularRefineColorStrength;
float _EnvironmentRotation;
float _EnvironmentMipBias;
float _MetalDirectBoost;
float _MetalEnvironmentBoost;
float _SkinToneBrightness;
float _SkinToneWhitening;
float _HairLineIntensity;
float _HairShiftIntensity;
float _HairPrimaryWidth;
float _HairSecondaryWidth;
float _HairSpecularLutMode;
float _EmotionTileIndex;
float _EmotionColumns;
float _EmotionRows;
float _DebugView;
float _OutlineWidth;
float _OutlineReferenceDistance;
float _OutlineDistanceScale;
float _OutlineGamma;
float _OutlineMaskInvert;
float _RimLightBrightness;
float _RimLightWidth;
float _RimLightThreshold;
float _RimLightFadeout;
float _SurfaceRimStrength;
float _SurfaceRimPower;
float _SurfaceRimLightAlign;
float _AlphaSource;
float _AlphaClip;
float _IrisParallaxDepth;
float _CorneaBumpStrength;
float _CorneaSpecularIntensity;
float _Cull;
float _StencilRef;
float _StencilReadMask;
float _StencilWriteMask;
float _StencilComp;
float _StencilPass;
CBUFFER_END

struct EndfieldAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
    float3 smoothNormalOS : TEXCOORD7;
};

struct EndfieldVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 tangentWS : TEXCOORD2;
    float3 bitangentWS : TEXCOORD3;
    float2 uv : TEXCOORD4;
    float4 shadowCoord : TEXCOORD5;
    float4 color : COLOR;
};

struct EndfieldHeadBasis
{
    float3 rightWS;
    float3 forwardWS;
    float3 backWS;
    float3 upWS;
};

struct EndfieldMainLightTerms
{
    float3 color;
    float3 directionWS;
    float shadowVisibility;
    float distanceDiagnostic;
    float layerMatch;
};

float3 EndfieldSafeNormalize(float3 value, float3 fallback)
{
    return dot(value, value) > 1e-8 ? normalize(value) : fallback;
}

float2 EndfieldAtlasUv(
    float2 uv,
    float tileIndex,
    float columns,
    float rows)
{
    columns = max(columns, 1.0);
    rows = max(rows, 1.0);
    tileIndex = clamp(tileIndex, 0.0, columns * rows - 1.0);
    float2 tileSize = rcp(float2(columns, rows));
    float2 tileOffset = float2(
        fmod(tileIndex, columns),
        floor(tileIndex / columns)) * tileSize;
    return saturate(uv) * tileSize + tileOffset;
}

float EndfieldSelectAlpha(float4 rawSample, float alphaSource)
{
    float luminance = dot(rawSample.rgb, float3(0.299, 0.587, 0.114));
    if (alphaSource < 0.5)
        return rawSample.a;
    if (alphaSource < 1.5)
        return luminance;
    if (alphaSource < 2.5)
        return 1.0 - rawSample.r;
    if (alphaSource < 3.5)
        return rawSample.r;
    return 1.0;
}

float EndfieldFaceSdfLight(
    float margin,
    float forwardAmount,
    float offset,
    float softness)
{
    float threshold = 0.5 - 0.5 * clamp(forwardAmount, -1.0, 1.0) + offset;
    softness = max(softness, 0.001);
    return smoothstep(threshold - softness, threshold + softness, margin);
}

float3 EndfieldFresnelSchlick(float cosTheta, float3 f0)
{
    float grazing = pow(1.0 - saturate(cosTheta), 5.0);
    return f0 + (1.0.xxx - f0) * grazing;
}

float3 EndfieldGgxSpecular(
    float3 normalWS,
    float3 viewDirWS,
    float3 lightDirWS,
    float roughness,
    float3 f0)
{
    float3 halfDir = EndfieldSafeNormalize(
        viewDirWS + lightDirWS,
        normalWS);
    float ndotl = saturate(dot(normalWS, lightDirWS));
    float ndotv = saturate(dot(normalWS, viewDirWS));
    float ndoth = saturate(dot(normalWS, halfDir));
    float vdoth = saturate(dot(viewDirWS, halfDir));
    roughness = clamp(roughness, 0.06, 1.0);
    float alpha = roughness * roughness;
    float alpha2 = alpha * alpha;
    float denominator = ndoth * ndoth * (alpha2 - 1.0) + 1.0;
    float distribution = alpha2 /
        max(PI * denominator * denominator, 1e-5);
    float k = (roughness + 1.0) * (roughness + 1.0) * 0.125;
    float geometryV = ndotv / max(ndotv * (1.0 - k) + k, 1e-5);
    float geometryL = ndotl / max(ndotl * (1.0 - k) + k, 1e-5);
    float3 fresnel = EndfieldFresnelSchlick(vdoth, saturate(f0));
    float3 response = distribution * geometryV * geometryL * fresnel /
        max(4.0 * ndotv * ndotl, 1e-4);
    return min(response * ndotl, 8.0.xxx);
}

EndfieldHeadBasis EndfieldGetHeadBasis()
{
    float3x3 objectToWorld = (float3x3)GetObjectToWorldMatrix();
    float3 rawRight = EndfieldSafeNormalize(
        mul(objectToWorld, float3(1.0, 0.0, 0.0)),
        float3(1.0, 0.0, 0.0));
    float3 forward = EndfieldSafeNormalize(
        mul(objectToWorld, float3(0.0, -1.0, 0.0)),
        float3(0.0, 0.0, 1.0));
    float3 upSeed = EndfieldSafeNormalize(
        mul(objectToWorld, float3(0.0, 0.0, 1.0)),
        float3(0.0, 1.0, 0.0));
    float3 right = EndfieldSafeNormalize(cross(upSeed, forward), rawRight);
    right *= dot(right, rawRight) < 0.0 ? -1.0 : 1.0;
    float3 up = EndfieldSafeNormalize(cross(forward, right), upSeed);

    EndfieldHeadBasis basis;
    basis.rightWS = right;
    basis.forwardWS = forward;
    basis.backWS = -forward;
    basis.upWS = up;
    return basis;
}

EndfieldHeadBasis EndfieldGetFaceBasis()
{
    EndfieldHeadBasis objectBasis = EndfieldGetHeadBasis();
    if (_UseManualFaceBasis < 0.5)
        return objectBasis;

    float3x3 objectToWorld = (float3x3)GetObjectToWorldMatrix();
    float3 rawRight = EndfieldSafeNormalize(
        mul(objectToWorld, _FaceRightOS.xyz),
        objectBasis.rightWS);
    float3 forwardSeed = EndfieldSafeNormalize(
        mul(objectToWorld, _FaceForwardOS.xyz),
        objectBasis.forwardWS);
    float3 upSeed = EndfieldSafeNormalize(
        mul(objectToWorld, _FaceUpOS.xyz),
        objectBasis.upWS);
    float3 right = EndfieldSafeNormalize(
        cross(upSeed, forwardSeed),
        rawRight);
    right *= dot(right, rawRight) < 0.0 ? -1.0 : 1.0;
    float3 up = EndfieldSafeNormalize(
        cross(forwardSeed, right),
        upSeed);
    float3 forward = EndfieldSafeNormalize(
        cross(right, up),
        forwardSeed);

    EndfieldHeadBasis basis;
    basis.rightWS = right;
    basis.forwardWS = forward;
    basis.backWS = -forward;
    basis.upWS = up;
    return basis;
}

float3 EndfieldRotateAroundWorldY(float3 directionWS, float degrees)
{
    float angle = radians(degrees);
    float sineAngle;
    float cosineAngle;
    sincos(angle, sineAngle, cosineAngle);
    return float3(
        directionWS.x * cosineAngle + directionWS.z * sineAngle,
        directionWS.y,
        -directionWS.x * sineAngle + directionWS.z * cosineAngle);
}

float EndfieldSpecularOcclusion(float ao, float visibility)
{
    return lerp(
        saturate(_SelfAoShadowStrength),
        1.0,
        saturate(ao * visibility));
}

float3 EndfieldApplySkinTone(float3 color)
{
    float3 brightened = saturate(color * max(_SkinToneBrightness, 0.0));
    return saturate(lerp(
        brightened,
        saturate(_SkinToneTarget.rgb),
        saturate(_SkinToneWhitening)));
}

float3 EndfieldSelectHairSpecularLut(float3 lutValue)
{
    return lerp(
        lutValue,
        lutValue.rrr,
        step(0.5, _HairSpecularLutMode));
}

float3 EndfieldSurfaceRim(
    float3 normalWS,
    float3 viewDirectionWS,
    float3 lightDirectionWS,
    float3 directLight,
    float visibility)
{
    float3 normal = EndfieldSafeNormalize(normalWS, float3(0.0, 1.0, 0.0));
    float3 viewDirection = EndfieldSafeNormalize(
        viewDirectionWS,
        normal);
    float3 lightDirection = EndfieldSafeNormalize(
        lightDirectionWS,
        -normal);
    float edge = pow(
        saturate(1.0 - saturate(dot(normal, viewDirection))),
        max(_SurfaceRimPower, 1e-4));
    float lightAlignment = saturate(dot(-normal, lightDirection));
    float rim = edge * lerp(
        1.0,
        lightAlignment,
        saturate(_SurfaceRimLightAlign));
    rim *= max(_SurfaceRimStrength, 0.0) *
        max(saturate(visibility), 0.35);
    return max(_RimLightTintColor.rgb, 0.0) * max(directLight, 0.0) * rim;
}

float3 EndfieldEnvironmentBrdf(
    float3 f0,
    float smoothness,
    float ndotv)
{
    BRDFData brdfData = (BRDFData)0;
    brdfData.specular = saturate(f0);
    brdfData.perceptualRoughness = saturate(1.0 - smoothness);
    brdfData.roughness = max(
        PerceptualRoughnessToRoughness(brdfData.perceptualRoughness),
        HALF_MIN_SQRT);
    brdfData.roughness2 = max(
        brdfData.roughness * brdfData.roughness,
        HALF_MIN);
    brdfData.grazingTerm = saturate(
        smoothness + max(max(f0.r, f0.g), f0.b));
    return EnvironmentBRDFSpecular(
        brdfData,
        Pow4(1.0 - saturate(ndotv)));
}

float3 EndfieldDecodeRawNormal(float4 sampleValue, float strength)
{
    float2 xy = (sampleValue.rg * 2.0 - 1.0) * strength;
    return normalize(float3(xy, sqrt(saturate(1.0 - dot(xy, xy)))));
}

float3 EndfieldDecodeSplitNormal(float2 encoded, float strength)
{
    float2 xy = (encoded * 2.0 - 1.0) * strength;
    return normalize(float3(xy, sqrt(saturate(1.0 - dot(xy, xy)))));
}

float3 EndfieldTangentToWorld(float3 normalTS, EndfieldVaryings input)
{
    return normalize(
        normalTS.x * input.tangentWS +
        normalTS.y * input.bitangentWS +
        normalTS.z * input.normalWS);
}

EndfieldVaryings EndfieldForwardVertex(EndfieldAttributes input)
{
    EndfieldVaryings output;
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = normalize(normal.normalWS);
    output.tangentWS = normalize(normal.tangentWS);
    output.bitangentWS = normalize(normal.bitangentWS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.shadowCoord = TransformWorldToShadowCoord(position.positionWS);
    output.color = input.color;
    return output;
}

float3 EndfieldLinearToSrgb(float3 linearColor)
{
    float3 value = max(linearColor, 0.0.xxx);
    float3 linearSegment = value * 12.92;
    float3 curveSegment = 1.055 * pow(max(value, 1e-8.xxx), 1.0 / 2.4) - 0.055;
    float3 useLinear = 1.0 - step(0.0031308.xxx, value);
    return lerp(curveSegment, linearSegment, useLinear);
}

float3 EndfieldSampleFlattenedColorLut(float3 linearColor)
{
    // The authored texture is a 32x32x32 LUT flattened into 32 horizontal
    // slices (1024x32). Endfield stores B as the slice axis and R/G inside it.
    const float size = 32.0;
    const float last = size - 1.0;
    const float width = size * size;
    float3 encoded = saturate(EndfieldLinearToSrgb(linearColor)).brg;
    float slice = encoded.x * last;
    float slice0 = floor(slice);
    float sliceBlend = slice - slice0;
    float texelR = encoded.y * last + 0.5;
    float texelG = encoded.z * last + 0.5;
    float2 uv0 = float2(
        (slice0 * size + texelR) / width,
        texelG / size);
    float2 uv1 = uv0 + float2(size / width, 0.0);
    float3 old0 = SAMPLE_TEXTURE2D(_ShadowLutTex, sampler_ShadowLutTex, uv0).rgb;
    float3 old1 = SAMPLE_TEXTURE2D(_ShadowLutTex, sampler_ShadowLutTex, uv1).rgb;
    float3 new0 = SAMPLE_TEXTURE2D(_ColorLutTex, sampler_ColorLutTex, uv0).rgb;
    float3 new1 = SAMPLE_TEXTURE2D(_ColorLutTex, sampler_ColorLutTex, uv1).rgb;
    float3 oldValue = lerp(old0, old1, sliceBlend);
    float3 newValue = lerp(new0, new1, sliceBlend);
    return lerp(oldValue, newValue, step(0.5, _UseColorLut));
}

float4 EndfieldSampleRamp(float signal)
{
    float u = saturate(signal * 0.5 + 0.5);
    float transition = smoothstep(
        0.45 - _ShadowSmoothness,
        0.45 + _ShadowSmoothness,
        u);
    float4 fallback = float4(
        lerp(float3(0.62, 0.52, 0.50), 1.0.xxx, transition),
        transition);
    float4 authored = SAMPLE_TEXTURE2D(
        _DiffRampMap, sampler_DiffRampMap, float2(u, 0.5));
    return lerp(fallback, authored, _UseDiffRampMap);
}

EndfieldMainLightTerms EndfieldGetMainLightTerms(Light mainLight)
{
    EndfieldMainLightTerms terms;
    terms.directionWS = EndfieldSafeNormalize(
        mainLight.direction,
        float3(0.0, 1.0, 0.0));
    terms.distanceDiagnostic = saturate(mainLight.distanceAttenuation);
    terms.layerMatch = 1.0;
#if defined(_LIGHT_LAYERS)
    terms.layerMatch = IsMatchingLightLayer(
        mainLight.layerMask,
        GetMeshRenderingLayer()) ? 1.0 : 0.0;
#endif

    // URP stores a per-object culling bit in distanceAttenuation for the main
    // directional light. The supported Endfield workflow treats that light as
    // the global character key and uses Rendering Layers for explicit
    // exclusion. Keep the raw value for diagnostics, but do not let a stale
    // zero silently erase all direct illumination while SH remains visible.
    terms.color = max(mainLight.color, 0.0.xxx);
    terms.shadowVisibility = saturate(mainLight.shadowAttenuation);
    return terms;
}

float3 EndfieldDirectLight(EndfieldMainLightTerms terms)
{
    return terms.color * terms.layerMatch;
}

float EndfieldShadowVisibility(EndfieldMainLightTerms terms)
{
    return terms.shadowVisibility * terms.layerMatch;
}

float3 EndfieldApplyLightingDebug(
    float3 current,
    EndfieldMainLightTerms mainLight,
    float3 directDiffuse,
    float3 directSpecular,
    float3 indirect)
{
    if (_DebugView < 7.5)
        return current;
    if (_DebugView < 8.5)
        return mainLight.color;
    if (_DebugView < 9.5)
        return mainLight.distanceDiagnostic.xxx;
    if (_DebugView < 10.5)
        return mainLight.shadowVisibility.xxx;
    if (_DebugView < 11.5)
        return directDiffuse;
    if (_DebugView < 12.5)
        return directSpecular;
    return indirect;
}

float3 EndfieldSampleNormalWS(EndfieldVaryings input)
{
    float3 normalTS = EndfieldDecodeRawNormal(
        SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv),
        _NormalStrength);
    return normalize(lerp(
        input.normalWS,
        EndfieldTangentToWorld(normalTS, input),
        _UseNormalMap));
}

float3 EndfieldDebug(
    float3 finalColor,
    float3 baseColor,
    float4 data,
    float3 normalWS,
    float4 ramp,
    float3 extra)
{
    if (_DebugView < 0.5)
        return finalColor;
    if (_DebugView < 1.5)
        return baseColor;
    if (_DebugView < 2.5)
        return data.rgb;
    if (_DebugView < 3.5)
        return normalWS * 0.5 + 0.5;
    if (_DebugView < 4.5)
        return ramp.rgb;
    return extra;
}

half4 EndfieldEvaluateBody(EndfieldVaryings input)
{
    float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) *
        _BaseColorTint;
    float4 material = lerp(
        float4(0.0, 1.0, 1.0, 0.45),
        SAMPLE_TEXTURE2D(_MaterialParamMap, sampler_MaterialParamMap, input.uv),
        _UseMaterialParamMap);
    float3 normalWS = EndfieldSampleNormalWS(input);
    Light mainLight = GetMainLight(input.shadowCoord);
    EndfieldMainLightTerms keyLight = EndfieldGetMainLightTerms(mainLight);
    float3 lightDirWS = keyLight.directionWS;
    float3 viewDirWS = EndfieldSafeNormalize(
        GetWorldSpaceViewDir(input.positionWS), float3(0, 0, 1));
    float ndotl = dot(normalWS, lightDirWS);
    float4 ramp = EndfieldSampleRamp(ndotl);
    float visibility = EndfieldShadowVisibility(keyLight);
    float3 directLight = EndfieldDirectLight(keyLight);
    float ao = saturate(material.b);
    float lit = min(ramp.a, visibility);
    float useLut = saturate(max(_UseColorLut, _UseShadowLut));
    float3 darkColor = lerp(
        baseSample.rgb * 0.55,
        EndfieldSampleFlattenedColorLut(baseSample.rgb),
        useLut);
    darkColor *= lerp(0.8.xxx, ramp.rgb, 0.65);
    float3 diffuseColor = lerp(darkColor, baseSample.rgb, lit);
    float metallic = saturate(material.r);
    float reflectivity = saturate(material.g);
    float smoothness = saturate(material.a);
    float3 diffuse = diffuseColor * (1.0 - metallic) *
        directLight * lerp(0.72, 1.0, lit);

    float3 halfDir = EndfieldSafeNormalize(viewDirWS + lightDirWS, normalWS);
    float specSignal = saturate(dot(normalWS, halfDir));
    float3 dielectricF0 = 0.04.xxx * lerp(0.5, 1.5, reflectivity);
    float3 metalF0 = max(baseSample.rgb, 0.12.xxx) *
        lerp(0.55, 1.0, reflectivity);
    float3 f0 = lerp(dielectricF0, metalF0, metallic);
    float roughness = max(1.0 - smoothness, 0.06);
    float ndotv = saturate(dot(normalWS, viewDirWS));
    float3 refinedF0 = SAMPLE_TEXTURE2D(
        _SpecularRefineF0Tex,
        sampler_SpecularRefineF0Tex,
        float2(ndotv, roughness)).rgb;
    f0 *= lerp(1.0.xxx, refinedF0, saturate(_UseSpecularRefine));
    float3 authoredSpecularColor = SAMPLE_TEXTURE2D(
        _SpecularRefineColorTex,
        sampler_SpecularRefineColorTex,
        input.uv).rgb * _SpecularRefineColor.rgb;
    float3 specularColor = lerp(
        1.0.xxx,
        authoredSpecularColor,
        saturate(_SpecularRefineColorStrength));
    specularColor = lerp(
        1.0.xxx,
        specularColor,
        saturate(_UseSpecularRefine));
    float2 refineUv = float2(
        saturate(specSignal * smoothness),
        saturate(1.0 - (1.0 - smoothness) * (1.0 - ao)));
    float3 specRefine = SAMPLE_TEXTURE2D(
        _SpecRampMap, sampler_SpecRampMap, refineUv).rgb;
    specRefine = lerp(1.0.xxx, specRefine, _UseSpecRampMap);
    float specularOcclusion = EndfieldSpecularOcclusion(ao, visibility);
    float metalDirectScale = lerp(
        1.0,
        max(_MetalDirectBoost, 0.0),
        metallic);
    float3 directSpecular = EndfieldGgxSpecular(
        normalWS, viewDirWS, lightDirWS, roughness, f0) * specRefine *
        specularColor * directLight * visibility * specularOcclusion *
        _SpecularIntensity * metalDirectScale;
    float metalBandSignal = smoothstep(0.45, 0.75, specSignal);
    float3 metalBand = f0 * specRefine * specularColor * directLight *
        visibility * specularOcclusion * metallic * metalBandSignal *
        (0.25 * max(_MetalDirectBoost, 0.0));
    metalBand = min(metalBand, 2.0.xxx);
    directSpecular += metalBand;
    float3 reflectDir = EndfieldRotateAroundWorldY(
        reflect(-viewDirWS, normalWS),
        _EnvironmentRotation);
    float perceptualRoughness = saturate(
        1.0 - smoothness + _EnvironmentMipBias);
    float3 environment = GlossyEnvironmentReflection(
        reflectDir,
        perceptualRoughness,
        specularOcclusion);
    float3 environmentBrdf = EndfieldEnvironmentBrdf(
        f0,
        smoothness,
        ndotv);
    float3 indirectSpecular = environment * environmentBrdf * specularColor *
        _IndirectIntensity * lerp(
            1.0,
            max(_MetalEnvironmentBoost, 0.0),
            metallic);
    float3 indirectDiffuse = SampleSH(normalWS) * baseSample.rgb *
        (1.0 - metallic) * ao * _IndirectIntensity;
    float3 metalBaseResponse = baseSample.rgb * metallic * directLight *
        lerp(0.50, 0.85, smoothness) *
        lerp(0.65, 1.0, reflectivity);
    float emissionMask = SAMPLE_TEXTURE2D(
        _EmissionMap, sampler_EmissionMap, input.uv).r;
    float3 emission = emissionMask * _EmissionColor.rgb *
        _UseEmissionMap * _EmissionIntensity;
    float3 surfaceRim = EndfieldSurfaceRim(
        normalWS,
        viewDirWS,
        lightDirWS,
        directLight,
        visibility);
    float3 result = diffuse + directSpecular + indirectSpecular +
        indirectDiffuse + metalBaseResponse + emission + surfaceRim;
    float3 debugColor = EndfieldDebug(
        result, baseSample.rgb, material, normalWS, ramp, darkColor);
    if (_DebugView > 6.5)
        debugColor = directSpecular + indirectSpecular + metalBaseResponse;
    else if (_DebugView > 5.5)
        debugColor = metallic.xxx;
    debugColor = EndfieldApplyLightingDebug(
        debugColor,
        keyLight,
        diffuse + metalBaseResponse + surfaceRim,
        directSpecular,
        indirectDiffuse);
    return half4(debugColor, 1.0);
}

half4 EndfieldEvaluateSkin(EndfieldVaryings input)
{
    float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) *
        _BaseColorTint;
    float3 complexion = EndfieldApplySkinTone(saturate(
        baseSample.rgb * float3(1.06, 1.015, 1.0) +
        float3(0.018, 0.006, 0.005)));
    float3 normalWS = EndfieldSampleNormalWS(input);
    Light mainLight = GetMainLight(input.shadowCoord);
    EndfieldMainLightTerms keyLight = EndfieldGetMainLightTerms(mainLight);
    float3 lightDirWS = keyLight.directionWS;
    float3 viewDirWS = EndfieldSafeNormalize(
        GetWorldSpaceViewDir(input.positionWS), float3(0, 0, 1));
    float viewEdge = pow(saturate(1.0 - dot(normalWS, viewDirWS)), 2.0);
    float sssArea = saturate(_SSSArea * viewEdge);
    complexion *= lerp(1.0.xxx, max(_SSSColor.rgb, 0.0), sssArea);
    float ndotl = dot(normalWS, lightDirWS);
    float4 ramp = EndfieldSampleRamp(ndotl);
    float authoredAo = saturate(baseSample.a);
    float ao = lerp(1.0, authoredAo, saturate(_SkinAOStrength));
    float visibility = EndfieldShadowVisibility(keyLight);
    float3 directLight = EndfieldDirectLight(keyLight);
    float geometryLight = smoothstep(
        0.35 - _ShadowSmoothness,
        0.35 + _ShadowSmoothness,
        ndotl * 0.5 + 0.5);
    float lit = min(ramp.a, min(visibility, geometryLight));
    float useLut = saturate(max(_UseColorLut, _UseShadowLut));
    float3 darkColor = lerp(
        complexion * float3(0.68, 0.54, 0.52),
        EndfieldSampleFlattenedColorLut(complexion),
        useLut);
    darkColor *= lerp(0.88.xxx, ramp.rgb, 0.45);
    float3 diffuseColor = lerp(darkColor, complexion, lit);
    float3 direct = diffuseColor * directLight *
        lerp(0.82, 1.0, lit) * lerp(0.86, 1.0, ao);
    float3 toonFill = complexion * directLight * 0.08 * ao;
    float3 indirect = SampleSH(normalWS) * complexion *
        _IndirectIntensity * lerp(0.65, 1.0, ao);
    float wrappedBack = saturate((dot(-normalWS, lightDirWS) + 0.35) / 1.35);
    float3 sss = complexion * max(_SSSColor.rgb, 0.0) * directLight *
        (0.18 + wrappedBack) * sssArea * (1.0 - lit) *
        _SkinSSSIntensity;
    float3 halfDir = EndfieldSafeNormalize(viewDirWS + lightDirWS, normalWS);
    float specularOcclusion = EndfieldSpecularOcclusion(ao, visibility);
    float specular = pow(saturate(dot(normalWS, halfDir)), 48.0) *
        visibility * specularOcclusion * _SpecularIntensity;
    float3 directSpecular = specular * directLight * 0.04;
    float3 surfaceRim = EndfieldSurfaceRim(
        normalWS,
        viewDirWS,
        lightDirWS,
        directLight,
        visibility);
    float3 result = direct + toonFill + indirect + sss + directSpecular +
        surfaceRim;
    float3 debugColor = EndfieldDebug(
        result, baseSample.rgb, float4(authoredAo, ao, ao, ao),
        normalWS, ramp, darkColor);
    debugColor = EndfieldApplyLightingDebug(
        debugColor,
        keyLight,
        direct + toonFill + sss + surfaceRim,
        directSpecular,
        indirect);
    return half4(debugColor, 1.0);
}

float4 EndfieldSampleFaceArea(float2 uv)
{
    float4 legacy = SAMPLE_TEXTURE2D(_SDFMask, sampler_SDFMask, uv);
    float4 current = SAMPLE_TEXTURE2D(_FaceAreaMap, sampler_FaceAreaMap, uv);
    return lerp(legacy, current, step(0.5, _UseFaceAreaMap));
}

float4 EndfieldSampleFaceRefine(float2 uv)
{
    float4 legacy = SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, uv);
    float4 current = SAMPLE_TEXTURE2D(_FaceRefineMap, sampler_FaceRefineMap, uv);
    return lerp(legacy, current, step(0.5, _UseFaceRefineMap));
}

half4 EndfieldEvaluateFace(EndfieldVaryings input)
{
    float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) *
        _BaseColorTint;
    baseSample.rgb = EndfieldApplySkinTone(saturate(
        baseSample.rgb * float3(1.06, 1.015, 1.0) +
        float3(0.018, 0.006, 0.005)));
    float columns = max(_EmotionColumns, 1.0);
    float rows = max(_EmotionRows, 1.0);
    float4 emotion = SAMPLE_TEXTURE2D(
        _EmotionMap,
        sampler_EmotionMap,
        EndfieldAtlasUv(input.uv, _EmotionTileIndex, columns, rows));
    baseSample.rgb = lerp(
        baseSample.rgb,
        emotion.rgb,
        saturate(emotion.a * _UseEmotionMap));
    float4 blush = SAMPLE_TEXTURE2D(
        _EmotionMap,
        sampler_EmotionMap,
        EndfieldAtlasUv(input.uv, _BlushTileIndex, columns, rows));
    float blushMask = saturate(blush.a * max(_BlushMaskGain, 0.0) *
        _BlushStrength);
    baseSample.rgb = lerp(
        baseSample.rgb,
        saturate(blush.rgb * _BlushColor.rgb),
        blushMask);
    float3 complexion = baseSample.rgb;

    EndfieldHeadBasis head = EndfieldGetFaceBasis();
    Light mainLight = GetMainLight(input.shadowCoord);
    EndfieldMainLightTerms keyLight = EndfieldGetMainLightTerms(mainLight);
    float3 lightDirWS = EndfieldSafeNormalize(
        keyLight.directionWS,
        head.forwardWS);
    float3 projected = lightDirWS - dot(lightDirWS, head.upWS) * head.upWS;
    projected = EndfieldSafeNormalize(projected, head.forwardWS);
    float projectedRight = dot(projected, head.rightWS);
    float2 sdfUv = float2(
        lerp(1.0 - input.uv.x, input.uv.x, step(0.0, projectedRight)),
        input.uv.y);
    float4 sdf = SAMPLE_TEXTURE2D(_SDFLightmap, sampler_SDFLightmap, sdfUv);
    float margin = (sdf.r + sdf.g) * 0.5;
    float forwardAmount = dot(projected, head.forwardWS);
    float backAmount = saturate(-forwardAmount);
    float sideAmount = saturate(1.0 - abs(projectedRight));
    float sdfPhase = clamp(
        forwardAmount + backAmount * sideAmount * _BackLightStrength,
        -1.0,
        1.0);
    float dynamicSoftness = max(
        _FaceShadowSoftness * lerp(1.25, 0.75, abs(sdfPhase)),
        1e-4);
    float sdfLight = EndfieldFaceSdfLight(
        margin,
        sdfPhase,
        _FaceShadowOffset,
        dynamicSoftness);

    float4 area = EndfieldSampleFaceArea(input.uv);
    float4 refine = EndfieldSampleFaceRefine(input.uv);
    float geometricLight = smoothstep(
        0.45 - _ShadowSmoothness,
        0.45 + _ShadowSmoothness,
        dot(input.normalWS, lightDirWS) * 0.5 + 0.5);
    float sdfValid = step(1e-3, margin) * step(margin, 1.0 - 1e-3);
    float sdfWithFallback = lerp(
        geometricLight,
        max(sdfLight, geometricLight),
        sdfValid);
    float faceLight = lerp(
        sdfWithFallback,
        geometricLight,
        saturate(refine.g));
    faceLight = lerp(
        geometricLight,
        faceLight,
        saturate(_UseFaceSDF));
    float3 directLight = EndfieldDirectLight(keyLight);
    float visibility = lerp(
        1.0,
        EndfieldShadowVisibility(keyLight),
        saturate(max(refine.b, area.g)));
    float litSignal = min(faceLight, visibility);
    float4 ramp = EndfieldSampleRamp(litSignal * 2.0 - 1.0);
    litSignal = min(litSignal, ramp.a);
    float lit = lerp(0.42, 1.0, litSignal);

    float3 viewDirWS = EndfieldSafeNormalize(
        GetWorldSpaceViewDir(input.positionWS), head.forwardWS);
    float viewEdge = pow(saturate(1.0 - dot(input.normalWS, viewDirWS)), 2.0);
    float authoredSss = lerp(
        1.0,
        saturate(refine.r),
        saturate(_UseFaceRefineMap));
    float sssArea = saturate(_SSSArea * viewEdge * authoredSss);
    complexion *= lerp(1.0.xxx, max(_SSSColor.rgb, 0.0), sssArea);

    float useLut = saturate(max(_UseColorLut, _UseShadowLut));
    float3 darkColor = lerp(
        complexion * float3(0.74, 0.59, 0.56),
        EndfieldSampleFlattenedColorLut(complexion),
        useLut);
    darkColor *= lerp(0.92.xxx, ramp.rgb, 0.35);
    float sideMask = saturate(area.r);
    float3 faceColor = lerp(darkColor, complexion, lit);
    faceColor *= lerp(0.94, 1.0, sideMask);
    float3 direct = faceColor * directLight *
        lerp(0.85, 1.0, litSignal);
    float3 toonFill = complexion * directLight *
        lerp(0.07, 0.11, litSignal);
    float3 indirect = SampleSH(input.normalWS) * complexion *
        _IndirectIntensity;

    // The face SDF is a binary illumination mask, not a normal map.
    float3 specNormal = normalize(input.normalWS);
    float3 halfDir = EndfieldSafeNormalize(viewDirWS + lightDirWS, specNormal);
    float specular = pow(saturate(dot(specNormal, halfDir)), 52.0) *
        _SpecularIntensity * EndfieldShadowVisibility(keyLight);
    float highlightMask = SAMPLE_TEXTURE2D(
        _HighlightMap, sampler_HighlightMap, input.uv).r;
    float3 lightEnvelope = directLight * visibility;
    float3 highlight = highlightMask * _UseHighlightMap *
        _FaceHighlightIntensity * complexion * lightEnvelope;
    float3 sss = complexion * max(_SSSColor.rgb, 0.0) *
        directLight *
        (0.18 + saturate(-dot(input.normalWS, lightDirWS))) *
        sssArea * (1.0 - litSignal) *
        _SkinSSSIntensity;
    float3 directSpecular = specular * directLight * 0.04 + highlight;
    float3 surfaceRim = EndfieldSurfaceRim(
        specNormal,
        viewDirWS,
        lightDirWS,
        directLight,
        visibility);
    float3 result = direct + toonFill + indirect + sss + directSpecular +
        surfaceRim;
    float3 debugColor = EndfieldDebug(
        result, baseSample.rgb, area, specNormal, ramp, sdfLight.xxx);
    if (_DebugView > 5.5)
        debugColor = blushMask.xxx;
    debugColor = EndfieldApplyLightingDebug(
        debugColor,
        keyLight,
        direct + toonFill + sss + surfaceRim,
        directSpecular,
        indirect);
    return half4(debugColor, 1.0);
}

float4 EndfieldSampleHairRefine(float2 uv)
{
    float4 legacy = SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, uv);
    float4 current = SAMPLE_TEXTURE2D(_HairRefineMap, sampler_HairRefineMap, uv);
    return lerp(legacy, current, step(0.5, _UseHairRefineMap));
}

float EndfieldSampleHairShift(float2 uv)
{
    float legacy = SAMPLE_TEXTURE2D(_StrokeMap, sampler_StrokeMap, uv).r;
    float current = SAMPLE_TEXTURE2D(_HairShiftMap, sampler_HairShiftMap, uv).r;
    return lerp(legacy, current, step(0.5, _UseHairShiftMap));
}

float EndfieldSampleHairLine(float2 uv)
{
    float legacy = SAMPLE_TEXTURE2D(_LineMap, sampler_LineMap, uv).r;
    float current = SAMPLE_TEXTURE2D(_HairLineMap, sampler_HairLineMap, uv).r;
    return lerp(legacy, current, step(0.5, _UseHairLineMap));
}

float EndfieldKajiyaKayLobe(
    float3 strandTangentWS,
    float3 halfDirectionWS,
    float exponent)
{
    // Kajiya-Kay evaluates the sine between the strand tangent and H. This
    // creates a highlight band perpendicular to the authored strand flow.
    float tangentDotHalf = dot(strandTangentWS, halfDirectionWS);
    float sineTangentHalf = sqrt(saturate(
        1.0 - tangentDotHalf * tangentDotHalf));
    return pow(sineTangentHalf, max(exponent, 1.0));
}

half4 EndfieldEvaluateHair(EndfieldVaryings input)
{
    float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) *
        _BaseColorTint;
    float4 material = lerp(
        float4(1.0, 0.35, 1.0, 0.45),
        SAMPLE_TEXTURE2D(_MaterialParamMap, sampler_MaterialParamMap, input.uv),
        _UseMaterialParamMap);
    float4 split = SAMPLE_TEXTURE2D(
        _SplitNormalMap, sampler_SplitNormalMap, input.uv);
    float3 diffuseNormal = EndfieldTangentToWorld(
        EndfieldDecodeSplitNormal(split.rg, _NormalStrength), input);
    float3 authoredHighlightNormal = EndfieldTangentToWorld(
        EndfieldDecodeSplitNormal(split.ba, _NormalStrength), input);
    diffuseNormal = normalize(lerp(
        input.normalWS, diffuseNormal, _UseSplitNormalMap));
    authoredHighlightNormal = normalize(lerp(
        input.normalWS, authoredHighlightNormal, _UseSplitNormalMap));

    EndfieldHeadBasis head = EndfieldGetHeadBasis();
    float3 headCenterWS = TransformObjectToWorld(_HeadCenterOS.xyz);
    float3 sphereNormal = EndfieldSafeNormalize(
        input.positionWS - headCenterWS, authoredHighlightNormal);
    float sphereBlend = saturate((1.0 - material.r) * _UseHeadSphereNormal);
    float3 highlightNormal = EndfieldSafeNormalize(
        lerp(authoredHighlightNormal, sphereNormal, sphereBlend),
        authoredHighlightNormal);
    float4 refine = EndfieldSampleHairRefine(input.uv);
    float lineMask = EndfieldSampleHairLine(input.uv);
    float lineUse = saturate(max(_UseHairLineMap, _UseLineMap));
    baseSample.rgb += baseSample.rgb * lineMask * refine.r *
        _HairLineIntensity * lineUse;

    Light mainLight = GetMainLight(input.shadowCoord);
    EndfieldMainLightTerms keyLight = EndfieldGetMainLightTerms(mainLight);
    float3 directLight = EndfieldDirectLight(keyLight);
    float3 lightDirWS = EndfieldSafeNormalize(
        keyLight.directionWS,
        head.forwardWS);
    float3 viewDirWS = EndfieldSafeNormalize(
        GetWorldSpaceViewDir(input.positionWS), head.forwardWS);
    float ndotl = dot(diffuseNormal, lightDirWS);
    float4 ramp = EndfieldSampleRamp(ndotl);
    float visibility = lerp(
        1.0,
        EndfieldShadowVisibility(keyLight),
        saturate(material.r));
    float ao = saturate(material.b);
    float lit = min(ramp.a, min(visibility, ao));
    float3 darkColor = baseSample.rgb * lerp(
        float3(0.52, 0.46, 0.48),
        ramp.rgb,
        0.55);
    float3 diffuseColor = lerp(darkColor, baseSample.rgb, lit);
    float3 diffuse = diffuseColor * directLight * lerp(0.72, 1.0, lit);
    float3 indirect = SampleSH(diffuseNormal) * baseSample.rgb *
        ao * _IndirectIntensity;

    float3 cameraRightWS = EndfieldSafeNormalize(
        mul((float3x3)GetViewToWorldMatrix(), float3(1.0, 0.0, 0.0)),
        head.rightWS);
    float3 cylinderNormal = highlightNormal -
        dot(highlightNormal, cameraRightWS) * cameraRightWS;
    cylinderNormal = EndfieldSafeNormalize(cylinderNormal, highlightNormal);
    float3 meshStrand = input.tangentWS -
        dot(input.tangentWS, cylinderNormal) * cylinderNormal;
    meshStrand = EndfieldSafeNormalize(meshStrand, input.bitangentWS);
    float3 cylinderStrand = EndfieldSafeNormalize(
        cross(head.upWS, cylinderNormal),
        meshStrand);
    float3 strandTangent = EndfieldSafeNormalize(
        lerp(cylinderStrand, meshStrand, saturate(material.r)),
        cylinderStrand);
    float shiftValue = (EndfieldSampleHairShift(input.uv) - 0.5) *
        _HairShiftIntensity * saturate(max(_UseHairShiftMap, _UseStrokeMap));
    float3 shiftedPrimary = EndfieldSafeNormalize(
        strandTangent + highlightNormal * shiftValue, strandTangent);
    float3 shiftedSecondary = EndfieldSafeNormalize(
        strandTangent - highlightNormal * (shiftValue + 0.12), strandTangent);
    float3 halfDir = EndfieldSafeNormalize(viewDirWS + lightDirWS, highlightNormal);
    float primary = EndfieldKajiyaKayLobe(
        shiftedPrimary, halfDir, _HairPrimaryWidth);
    float secondary = EndfieldKajiyaKayLobe(
        shiftedSecondary, halfDir, _HairSecondaryWidth);
    float tangentDotHalf = dot(strandTangent, halfDir);
    float viewNormal = saturate(dot(viewDirWS, highlightNormal));
    float2 hairLutUv = float2(
        primary,
        viewNormal * viewNormal * step(0.0, tangentDotHalf));
    float3 hairLut = SAMPLE_TEXTURE2D(
        _SpecularRefineF0Tex,
        sampler_SpecularRefineF0Tex,
        hairLutUv).rgb;
    hairLut = EndfieldSelectHairSpecularLut(hairLut);
    hairLut = lerp(1.0.xxx, hairLut, saturate(_UseSpecularRefine));
    float specMask = lerp(
        1.0,
        SAMPLE_TEXTURE2D(_SpecularMask, sampler_SpecularMask, input.uv).r,
        _UseSpecularMask);
    float3 specRefine = SAMPLE_TEXTURE2D(
        _SpecRampMap, sampler_SpecRampMap, float2(primary, 0.5)).rgb;
    specRefine = lerp(1.0.xxx, specRefine, _UseSpecRampMap);
    float reflectivity = saturate(material.g);
    float backStrength = saturate(material.a);
    float3 primaryColor = lerp(baseSample.rgb, specRefine, 0.45) * hairLut;
    float3 primarySpecular = primaryColor * primary * reflectivity * 0.38;
    float3 secondarySpecular = lerp(
        baseSample.rgb, 1.0.xxx, 0.22) * secondary * backStrength * 0.12;
    float specularOcclusion = EndfieldSpecularOcclusion(ao, visibility);
    float3 specular = (primarySpecular + secondarySpecular) *
        specMask * visibility * specularOcclusion * directLight *
        _SpecularIntensity;
    float accessoryMask = (1.0 - specMask) * _UseSpecularMask;
    float accessoryRoughness = lerp(0.42, 0.16, backStrength);
    float3 accessoryF0 = max(
        lerp(0.16.xxx, specRefine, 0.72),
        0.12.xxx) * lerp(0.65, 1.0, reflectivity);
    float3 accessoryDirect = EndfieldGgxSpecular(
        authoredHighlightNormal,
        viewDirWS,
        lightDirWS,
        accessoryRoughness,
        accessoryF0) * directLight * visibility * _SpecularIntensity;
    float3 accessoryReflection = GlossyEnvironmentReflection(
        reflect(-viewDirWS, authoredHighlightNormal),
        accessoryRoughness,
        specularOcclusion) * accessoryF0 * _IndirectIntensity;
    float3 accessoryVisibility = accessoryF0 * directLight * 0.28;
    float3 accessorySpecular = accessoryMask * specularOcclusion *
        (accessoryDirect + accessoryReflection + accessoryVisibility);
    float3 surfaceRim = EndfieldSurfaceRim(
        diffuseNormal,
        viewDirWS,
        lightDirWS,
        directLight,
        visibility);
    float3 result = diffuse + indirect + specular + accessorySpecular +
        surfaceRim;
    float3 debugColor = EndfieldDebug(
        result, baseSample.rgb, material, highlightNormal, ramp,
        float3(primary, secondary, specMask));
    if (_DebugView > 6.5)
        debugColor = accessorySpecular;
    else if (_DebugView > 5.5)
        debugColor = accessoryMask.xxx;
    debugColor = EndfieldApplyLightingDebug(
        debugColor,
        keyLight,
        diffuse + surfaceRim,
        specular + accessoryDirect,
        indirect);
    return half4(debugColor, 1.0);
}

half4 EndfieldEvaluateEye(EndfieldVaryings input)
{
    float3 normalWS = normalize(input.normalWS);
    float3 viewDirWS = EndfieldSafeNormalize(
        GetWorldSpaceViewDir(input.positionWS),
        normalWS);
    float3 viewDirTS = float3(
        dot(viewDirWS, input.tangentWS),
        dot(viewDirWS, input.bitangentWS),
        dot(viewDirWS, normalWS));
    float2 centeredUv = (saturate(input.uv) - 0.5) * 2.0;
    float radiusSquared = dot(centeredUv, centeredUv);
    float irisMask = 1.0 - smoothstep(0.72, 1.0, radiusSquared);
    float parallaxMask = 1.0 - smoothstep(0.55, 1.0, radiusSquared);
    float scleraMode = step(0.5, _EyeMode);
    float irisMode = 1.0 - scleraMode;
    float safeViewZ = max(abs(viewDirTS.z), 0.2);
    float2 parallaxOffset = -viewDirTS.xy / safeViewZ *
        _IrisParallaxDepth * parallaxMask * irisMode;
    float2 irisUv = saturate(input.uv + parallaxOffset);
    float4 irisSample = SAMPLE_TEXTURE2D(
        _BaseMap, sampler_BaseMap, irisUv) * _BaseColorTint;

    float2 corneaXy = centeredUv * _CorneaBumpStrength * irisMask;
    float corneaZ = sqrt(saturate(1.0 - dot(corneaXy, corneaXy)));
    float3 generatedCorneaNormalWS = EndfieldTangentToWorld(
        EndfieldSafeNormalize(float3(corneaXy, corneaZ), float3(0, 0, 1)),
        input);
    float3 corneaNormalWS = EndfieldSafeNormalize(
        lerp(normalWS, generatedCorneaNormalWS, irisMask * irisMode),
        normalWS);
    Light mainLight = GetMainLight(input.shadowCoord);
    EndfieldMainLightTerms keyLight = EndfieldGetMainLightTerms(mainLight);
    float3 lightDirWS = keyLight.directionWS;
    float visibility = EndfieldShadowVisibility(keyLight);
    float3 directLight = EndfieldDirectLight(keyLight);
    float lambert = saturate(dot(normalWS, lightDirWS) * 0.5 + 0.5);
    float3 scleraColor = float3(0.94, 0.88, 0.84) * _BaseColorTint.rgb;
    float3 eyeColor = lerp(irisSample.rgb, scleraColor, scleraMode);
    float eyeLit = lambert * visibility;
    float3 diffuse = eyeColor * directLight * lerp(0.62, 1.0, eyeLit);
    float3 indirect = SampleSH(normalWS) * eyeColor * _IndirectIntensity;

    float3 directCornea = EndfieldGgxSpecular(
        corneaNormalWS,
        viewDirWS,
        lightDirWS,
        0.12,
        0.04.xxx) * directLight * visibility;
    float3 corneaNormalVS = TransformWorldToViewDir(corneaNormalWS, true);
    float4 matcap = SAMPLE_TEXTURE2D(
        _MatCap,
        sampler_MatCap,
        saturate(corneaNormalVS.xy * 0.5 + 0.5));
    float3 lightEnvelope = directLight * visibility;
    float3 matcapSpecular = matcap.rgb *
        saturate(0.35 + matcap.a * 2.0) * lightEnvelope * _UseMatCap;
    float3 wetHighlight = (directCornea + matcapSpecular) *
        _CorneaHighlightColor.rgb * _CorneaSpecularIntensity *
        _SpecularIntensity * irisMask * irisMode;
    float3 halfDir = EndfieldSafeNormalize(
        viewDirWS + lightDirWS,
        normalWS);
    float scleraSpecular = pow(saturate(dot(normalWS, halfDir)), 42.0) *
        visibility * _SpecularIntensity * 0.025;
    float3 specular = lerp(
        wetHighlight,
        scleraSpecular * directLight,
        scleraMode);
    float3 result = diffuse + indirect + specular;
    result = EndfieldApplyLightingDebug(
        result,
        keyLight,
        diffuse,
        specular,
        indirect);
    return half4(result, 1.0);
}

half4 EndfieldEvaluateMouth(EndfieldVaryings input)
{
    float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) *
        _BaseColorTint;
    float3 normalWS = normalize(input.normalWS);
    Light mainLight = GetMainLight(input.shadowCoord);
    EndfieldMainLightTerms keyLight = EndfieldGetMainLightTerms(mainLight);
    float3 lightDirWS = keyLight.directionWS;
    float3 viewDirWS = EndfieldSafeNormalize(
        GetWorldSpaceViewDir(input.positionWS),
        normalWS);
    float visibility = EndfieldShadowVisibility(keyLight);
    float3 directLight = EndfieldDirectLight(keyLight);
    float lambert = saturate(dot(normalWS, lightDirWS) * 0.5 + 0.5);
    float mouthLit = lambert * visibility;
    float3 diffuse = baseSample.rgb * directLight *
        lerp(0.55, 1.0, mouthLit);
    float3 indirect = SampleSH(normalWS) * baseSample.rgb * _IndirectIntensity;
    float3 halfDir = EndfieldSafeNormalize(viewDirWS + lightDirWS, normalWS);
    float specular = pow(saturate(dot(normalWS, halfDir)), 32.0) *
        visibility * _SpecularIntensity * 0.02;
    float3 directSpecular = specular * directLight;
    float3 result = diffuse + indirect + directSpecular;
    result = EndfieldApplyLightingDebug(
        result,
        keyLight,
        diffuse,
        directSpecular,
        indirect);
    return half4(result, 1.0);
}

half4 EndfieldForwardFragment(EndfieldVaryings input) : SV_Target
{
    if (_PartMode < 0.5)
        return EndfieldEvaluateBody(input);
    if (_PartMode < 1.5)
        return EndfieldEvaluateSkin(input);
    if (_PartMode < 2.5)
        return EndfieldEvaluateHair(input);
    if (_PartMode < 3.5)
        return EndfieldEvaluateFace(input);
    if (_PartMode < 4.5)
        return EndfieldEvaluateEye(input);
    return EndfieldEvaluateMouth(input);
}

struct EndfieldOutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

EndfieldOutlineVaryings EndfieldOutlineVertex(EndfieldAttributes input)
{
    EndfieldOutlineVaryings output;
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    float3 selectedNormalOS = dot(input.smoothNormalOS, input.smoothNormalOS) > 1e-5
        ? input.smoothNormalOS
        : input.normalOS;
    float3 normalWS = normalize(TransformObjectToWorldNormal(selectedNormalOS));
    float distanceScale = max(
        distance(_WorldSpaceCameraPos, position.positionWS) /
        max(_OutlineReferenceDistance, 1e-5),
        1.0);
    float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);
    float outlineMask = SAMPLE_TEXTURE2D_LOD(
        _OutlineMask, sampler_OutlineMask, uv, 0).r;
    outlineMask = lerp(outlineMask, 1.0 - outlineMask, _OutlineMaskInvert);
    outlineMask = lerp(1.0, outlineMask, _UseOutlineMask);
    float accessoryOutlineMask = SAMPLE_TEXTURE2D_LOD(
        _SpecularMask, sampler_SpecularMask, uv, 0).r;
    accessoryOutlineMask = lerp(
        1.0,
        lerp(0.45, 1.0, accessoryOutlineMask),
        _UseSpecularMask);
    float width = _OutlineWidth *
        lerp(1.0, distanceScale, saturate(_OutlineDistanceScale)) *
        outlineMask * accessoryOutlineMask * input.color.a;
    output.positionCS = TransformWorldToHClip(position.positionWS + normalWS * width);
    output.uv = uv;
    output.color = input.color;
    return output;
}

half4 EndfieldOutlineFragment(EndfieldOutlineVaryings input) : SV_Target
{
    float3 baseColor = SAMPLE_TEXTURE2D(
        _BaseMap, sampler_BaseMap, input.uv).rgb;
    float3 color = pow(max(baseColor, 0.0.xxx), max(_OutlineGamma, 1e-5)) *
        _OutlineColorTint.rgb;
    return half4(color, 1.0);
}

struct EndfieldDepthVaryings { float4 positionCS : SV_POSITION; };
EndfieldDepthVaryings EndfieldDepthVertex(EndfieldAttributes input)
{
    EndfieldDepthVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}
half4 EndfieldDepthFragment(EndfieldDepthVaryings input) : SV_Target { return 0; }

half4 EndfieldTransparentFragment(EndfieldVaryings input) : SV_Target
{
    float4 rawSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    float4 baseSample = rawSample * _BaseColorTint;
    float alpha = EndfieldSelectAlpha(rawSample, _AlphaSource);
    float effectMask = lerp(
        1.0,
        SAMPLE_TEXTURE2D(_EffectMask, sampler_EffectMask, input.uv).r,
        _UseEffectMask);
    float coverage = saturate(alpha * effectMask);
    clip(coverage - _AlphaClip - 1e-5);
    float3 emission = SAMPLE_TEXTURE2D(
        _EmissionMap, sampler_EmissionMap, input.uv).rgb *
        _UseEmissionMap * _EmissionIntensity;
    float3 overlayColor = lerp(
        baseSample.rgb,
        _BaseColorTint.rgb,
        saturate(_OverlayUseTintOnly));
    float opaqueCoverage = step(3.5, _AlphaSource);
    float outputAlpha = lerp(
        saturate(coverage * _BaseColorTint.a),
        1.0,
        opaqueCoverage);
    return half4(overlayColor + emission, outputAlpha);
}

#endif
