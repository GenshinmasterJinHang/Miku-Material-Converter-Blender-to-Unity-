// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT
#ifndef MIKU_ENDFIELD_COMMON_INCLUDED
#define MIKU_ENDFIELD_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.miku.shaderconverter/Runtime/GameToon/MikuGameToonOutline.hlsl"

// Share URP's inline samplers to stay within conservative sampler-register
// budgets. Authored assets need only two sampling intents: trilinear repeat
// and linear clamp.
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
float4 _ClothSssColor;
float4 _SkinToneTarget;
float4 _EyeCenterColor;
float4 _EyeAlphaColor;
float4 _MatCapAlphaColor;
float4 _HairBaseF0;
float4 _HairBackF0;
float4 _FaceRightOS;
float4 _FaceForwardOS;
float4 _FaceUpOS;
float4 _MikuHeadForwardWS;
float4 _MikuHeadRightWS;
float4 _MikuHeadUpWS;
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
float _UseOutline;
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
float _LightingMode;
float _NormalStrength;
float _ShadowSmoothness;
float _ShadowCenter;
float _ShadowSigmoidSmoothness;
float _ShadowOffset;
float _ShadowStrength;
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
float _LightRimStrength;
float _LightRimPower;
float _DarkColorStrength;
float _DarkColorSaturation;
float _DarkInDarkStrength;
float _BackLightCompensation;
float _NoFStrength;
float _NoFPowStrength;
float _RefineF0U_lerp;
float _RampColorStrength;
float _DiffuseAlphaEnergy;
float _EmissionMapMode;
float _ClothSssStrength;
float _ClothSssPower;
float _SkinRoughness;
float _SkinReflectivity;
float _FaceSdfNormalStrength;
float _FaceRimMaskStrength;
float _FaceRimSideStrength;
float _RimLightArea;
float _RimLightDiffuseColorEffect;
float _FaceRoughness;
float _FaceReflectivity;
float _HairFlatten;
float _HairViewDirYOffset;
float _HairLutVPower;
float _HairBackF0ToHPower;
float _EyeRampStrength;
float _MatCapAlphaStrength;
float _MikuHeadAxesValid;
float _AlphaSource;
float _AlphaClip;
float _IrisParallaxDepth;
float _CorneaBumpStrength;
float _MatCapUvScale;
float _CorneaSpecularIntensity;
float _Cull;
float _StencilRef;
float _StencilReadMask;
float _StencilWriteMask;
float _StencilComp;
float _StencilPass;
CBUFFER_END

// Scene-wide state is intentionally outside UnityPerMaterial. A controller
// publishes these values; zero availability keeps the 2.2.x lighting path.
float _MikuEndfieldLightingAvailable;
float _MikuEndfieldDayStrength;
float4 _MikuEndfieldTopLightColor;
float4 _MikuEndfieldTopLightDirection;
float4 _MikuEndfieldTopLightParams;
float _MikuEndfieldCameraForwardBlend;
float _MikuEndfieldBackLightStrength;

struct EndfieldAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
    float4 smoothNormalData : TEXCOORD7;
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

float2 EndfieldSafeNormalize2(float2 value, float2 fallback)
{
    return dot(value, value) > 1e-8 ? normalize(value) : fallback;
}

bool EndfieldIsFinite(float value)
{
    return value == value && abs(value) < 3.402823e+38;
}

bool EndfieldIsFinite3(float3 value)
{
    return EndfieldIsFinite(value.x) &&
        EndfieldIsFinite(value.y) &&
        EndfieldIsFinite(value.z);
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

// Article face SDF light: x = saturate(-phase*0.5+0.5), then a width-scaled
// smoothstep over the SDF margin. The width shrinks as the light moves behind
// the face, producing the authored hard/soft face-shadow boundary.
float EndfieldFaceSdfLightArticle(
    float margin,
    float phase,
    float offset,
    float softness)
{
    float x = saturate(-clamp(phase, -1.0, 1.0) * 0.5 + 0.5);
    float sdfMin = max(0.0, 2.0 * x - 1.0);
    float sdfMax = min(1.0, 2.0 * x);
    float width = sdfMax - sdfMin;
    if (width <= 1e-5)
        return x < 0.5 ? 1.0 : 0.0;
    width = max(width, max(softness, 1e-4));
    float t = saturate((margin + offset - sdfMin) / width);
    return t * t * (3.0 - 2.0 * t);
}

// Article face rim area: 1-NoV through the authored start/end remap, then a
// cubic smoothstep. The caller applies the Refine-W mask and one-sided half.
float EndfieldFaceRimArea(float rimNoV, float rimAreaParam)
{
    float rimStart = rimAreaParam * -0.6 + 0.8;
    float rimEnd = rimAreaParam * -0.4 + 0.9;
    float rimWidth = max(rimEnd - rimStart, 1e-4);
    float rimT = saturate(((1.0 - saturate(rimNoV)) - rimStart) / rimWidth);
    return rimT * rimT * (3.0 - 2.0 * rimT);
}

float3 EndfieldFresnelSchlick(float cosTheta, float3 f0)
{
    float grazing = pow(1.0 - saturate(cosTheta), 5.0);
    return f0 + (1.0.xxx - f0) * grazing;
}

float3 EndfieldGgxSpecularFromHalf(
    float3 normalWS,
    float3 viewDirWS,
    float3 lightDirWS,
    float3 halfDir,
    float roughness,
    float3 f0)
{
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
    return EndfieldGgxSpecularFromHalf(
        normalWS,
        viewDirWS,
        lightDirWS,
        halfDir,
        roughness,
        f0);
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
    if (_MikuHeadAxesValid > 0.5)
    {
        float3 forward = EndfieldSafeNormalize(
            _MikuHeadForwardWS.xyz,
            objectBasis.forwardWS);
        float3 rawRight = EndfieldSafeNormalize(
            _MikuHeadRightWS.xyz,
            objectBasis.rightWS);
        float3 upSeed = EndfieldSafeNormalize(
            _MikuHeadUpWS.xyz,
            objectBasis.upWS);
        float3 right = EndfieldSafeNormalize(
            cross(upSeed, forward),
            rawRight);
        right *= dot(right, rawRight) < 0.0 ? -1.0 : 1.0;
        float3 up = EndfieldSafeNormalize(cross(forward, right), upSeed);

        EndfieldHeadBasis boundBasis;
        boundBasis.rightWS = right;
        boundBasis.forwardWS = forward;
        boundBasis.backWS = -forward;
        boundBasis.upWS = up;
        return boundBasis;
    }
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

// Article eye trick: flatten the key light onto the face plane (drop the
// face-up component) so the iris shading follows the authored 2D eye UV.
float3 EndfieldEyeFlattenedLightDirection(
    float3 lightDirectionWS,
    EndfieldHeadBasis head)
{
    float2 lightFaceXZ = EndfieldSafeNormalize2(
        float2(
            dot(lightDirectionWS, head.rightWS),
            dot(lightDirectionWS, head.forwardWS)) + float2(0.0, 1e-6),
        float2(0.0, 1.0));
    return EndfieldSafeNormalize(
        head.rightWS * lightFaceXZ.x + head.forwardWS * lightFaceXZ.y,
        head.forwardWS);
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
    float3 legacyRim = max(_RimLightTintColor.rgb, 0.0) *
        max(directLight, 0.0) * rim;
    float lightRim = edge * pow(
        lightAlignment,
        max(_LightRimPower, 1e-4)) * max(_LightRimStrength, 0.0);
    lightRim *= max(saturate(visibility), 0.2) *
        lerp(1.0, 0.35, saturate(_MikuEndfieldDayStrength));
    float3 separatedLightRim = max(_RimLightTintColor.rgb, 0.0) *
        max(directLight, 0.0) * lightRim;
    return legacyRim + separatedLightRim *
        saturate(_MikuEndfieldLightingAvailable);
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

float4 EndfieldSampleRamp(float signal);

float EndfieldTutorialWeight()
{
    return saturate(_MikuEndfieldLightingAvailable);
}

float EndfieldDayStrength()
{
    return lerp(
        1.0,
        saturate(_MikuEndfieldDayStrength),
        EndfieldTutorialWeight());
}

float3 EndfieldCameraForwardWS(float3 fallback)
{
    // UNITY_MATRIX_V row two is the camera's world-space backward axis. It is
    // the stable, orthographic-safe view direction used by the authored model.
    return EndfieldSafeNormalize(UNITY_MATRIX_V[2].xyz, fallback);
}

float EndfieldBackLightSignal(
    float ndotl,
    float3 lightDirectionWS,
    float3 cameraForwardWS)
{
    float3 lightXZ = EndfieldSafeNormalize(
        float3(lightDirectionWS.x, 0.0, lightDirectionWS.z),
        float3(0.0, 0.0, 1.0));
    float3 cameraXZ = EndfieldSafeNormalize(
        float3(cameraForwardWS.x, 0.0, cameraForwardWS.z),
        float3(0.0, 0.0, 1.0));
    float backFacing = saturate(-dot(lightXZ, cameraXZ));
    float pitchMask = saturate(0.75 - abs(cameraForwardWS.y));
    pitchMask = pitchMask * pitchMask * (3.0 - 2.0 * pitchMask);
    backFacing *= pitchMask;
    float compensation = saturate(0.5 - 0.5 * ndotl * ndotl) *
        backFacing * max(_BackLightCompensation, 0.0) *
        max(_MikuEndfieldBackLightStrength, 0.0);
    return clamp(
        ndotl + compensation * EndfieldTutorialWeight(),
        -1.0,
        1.0);
}

float3 EndfieldTutorialDirectLight(
    float3 normalWS,
    float3 legacyDirectLight,
    float bandMask)
{
    float3 topDirection = EndfieldSafeNormalize(
        _MikuEndfieldTopLightDirection.xyz,
        float3(0.0, 1.0, 0.0));
    float topNoL = saturate(
        dot(normalWS, topDirection) * _MikuEndfieldTopLightParams.x +
        _MikuEndfieldTopLightParams.y);
    float mask = saturate(bandMask);
    float3 legacy = max(legacyDirectLight, 0.0.xxx);
    // Reference shadow tint: the main light desaturates toward its own
    // luminance and the top light whitens while the character is shaded.
    float legacyStrength = dot(legacy, float3(0.299, 0.587, 0.114));
    float3 mainLightTinted = lerp(legacyStrength.xxx, legacy, mask);
    float3 topLight = lerp(
        max(_MikuEndfieldTopLightColor.rgb, 0.0),
        1.0.xxx,
        mask) * topNoL;
    float day = saturate(_MikuEndfieldDayStrength);
    float3 tutorialDirect = lerp(
        topLight * max(_MikuEndfieldTopLightParams.w, 0.0),
        mainLightTinted + topLight * max(_MikuEndfieldTopLightParams.z, 0.0),
        day);
    return lerp(
        legacyDirectLight,
        tutorialDirect,
        EndfieldTutorialWeight());
}

// Reference direct-specular D term: a2 / (NoH*(a2-1)+1)^2.
float EndfieldTutorialSpecularD(float ndotH, float roughness2)
{
    float a2 = roughness2 * roughness2;
    float denominator = (ndotH * a2 - ndotH) * ndotH + 1.0;
    // The denominator stays positive for NoH in [0,1]; the epsilon only
    // guards against denormal zero, it must not flatten the smooth peak.
    return a2 / max(denominator * denominator, 1e-12);
}

// Reference direct-specular D*V result, clamped to [0, 20].
float EndfieldTutorialSpecularDV(
    float ndotH,
    float ndotV,
    float roughness2)
{
    float distribution = EndfieldTutorialSpecularD(ndotH, roughness2);
    float visibilityTerm = 0.5 / max(
        ndotV * 2.0 + roughness2 + 9.99999975e-05,
        1e-5);
    return clamp(
        distribution * visibilityTerm - 6.10351562e-05,
        0.0,
        20.0);
}

// Reference specular light envelope:
// selfAoShadowEffect * (ao_shadow_lowLight * 0.5 + 0.5) with
// ao_shadow_lowLight = lerp(dayZeroWeight, brightWeight, day).
float EndfieldTutorialSpecularEnvelope(
    float dayZeroWeight,
    float brightWeight,
    float day)
{
    float aoShadowLowLight = lerp(
        saturate(dayZeroWeight),
        saturate(brightWeight),
        saturate(day));
    float selfAoShadowEffect = lerp(
        saturate(_SelfAoShadowStrength),
        1.0,
        aoShadowLowLight);
    return selfAoShadowEffect * (aoShadowLowLight * 0.5 + 0.5);
}

// Reference luminance-preserving ramp influence:
// strength(before) / strength(before * rampEffect), clamped to [0, 1.5].
float EndfieldRampColorControl(float3 before, float3 after)
{
    float beforeStrength = dot(
        max(before, 0.0.xxx),
        float3(0.212672904, 0.715152204, 0.0721750036));
    float afterStrength = max(
        dot(max(after, 0.0.xxx), float3(0.212672904, 0.715152204, 0.0721750036)),
        0.01);
    return clamp(beforeStrength / afterStrength, 0.0, 1.5);
}

float3 EndfieldStylizedViewDirection(
    float3 perPixelViewDirectionWS,
    float3 fallback)
{
    float3 cameraForward = EndfieldCameraForwardWS(fallback);
    return EndfieldSafeNormalize(
        lerp(
            perPixelViewDirectionWS,
            cameraForward,
            EndfieldTutorialWeight() *
                saturate(_MikuEndfieldCameraForwardBlend)),
        fallback);
}

float3 EndfieldTutorialForwardLightDirection(
    float3 mainLightDirectionWS,
    float3 fallback)
{
    float day = EndfieldDayStrength();
    float3 cameraForwardWS = EndfieldCameraForwardWS(fallback);
    float forwardLightY = lerp(
        0.5,
        mainLightDirectionWS.y,
        day);
    return EndfieldSafeNormalize(
        float3(
            cameraForwardWS.x,
            forwardLightY,
            cameraForwardWS.z),
        fallback);
}

float EndfieldStylizedSpecularBlend()
{
    return EndfieldTutorialWeight() *
        saturate(_MikuEndfieldCameraForwardBlend);
}

float3 EndfieldStylizedSpecularLightDirection(
    float3 mainLightDirectionWS,
    float3 fallback)
{
    float day = EndfieldDayStrength();
    float3 forwardLightDirectionWS = EndfieldTutorialForwardLightDirection(
        mainLightDirectionWS,
        fallback);
    float3 tutorialDirectionWS = EndfieldSafeNormalize(
        mainLightDirectionWS * day + 2.0 * forwardLightDirectionWS,
        forwardLightDirectionWS);
    return EndfieldSafeNormalize(
        lerp(
            mainLightDirectionWS,
            tutorialDirectionWS,
            EndfieldStylizedSpecularBlend()),
        mainLightDirectionWS);
}

float3 EndfieldStylizedSpecularHalfDirection(
    float3 normalWS,
    float3 viewDirectionWS,
    float3 mainLightDirectionWS)
{
    float day = EndfieldDayStrength();
    float3 legacyHalfDirectionWS = EndfieldSafeNormalize(
        viewDirectionWS + mainLightDirectionWS,
        normalWS);
    float3 forwardLightDirectionWS = EndfieldTutorialForwardLightDirection(
        mainLightDirectionWS,
        normalWS);
    float3 tutorialLightDirectionWS = mainLightDirectionWS * day +
        2.0 * forwardLightDirectionWS;
    float3 tutorialHalfDirectionWS = EndfieldSafeNormalize(
        viewDirectionWS * (2.0 + day) + tutorialLightDirectionWS,
        legacyHalfDirectionWS);
    return EndfieldSafeNormalize(
        lerp(
            legacyHalfDirectionWS,
            tutorialHalfDirectionWS,
            EndfieldStylizedSpecularBlend()),
        legacyHalfDirectionWS);
}

struct EndfieldDiffuseBands
{
    float3 color;
    float4 ramp;
    float4 nofRamp;
    float lit;
    float dayZeroWeight;
    float brightWeight;
    float nofMask;
};

struct EndfieldDiffuseOptions
{
    // -1..1 signal used to sample the diffuse ramp (article ramp_NoL).
    float rampSignal;
    float nofStrength;
    float nofPower;
    // 1 = dark-in-dark includes the NoF mask (Body/Hair/Eye);
    // 0 = dark-in-dark uses plain AO x shadow (Skin/Face reference).
    float nofInDarkInDark;
    // 1 = low-light band uses AO x shadow x NoF mask (Body/Hair/Eye);
    // 0 = low-light band uses plain AO x shadow (Skin/Face reference).
    float dayZeroNofBlend;
    // 1 = luminance-preserving ramp control (reference rampColor_control);
    // 0 = direct ramp multiply (Eye reference).
    float applyRampControl;
};

EndfieldDiffuseBands EndfieldThreeLayerDiffuse(
    float3 baseColor,
    float3 brightColor,
    float baseAlpha,
    float3 authoredDarkColor,
    float3 normalWS,
    float visibility,
    float ao,
    EndfieldDiffuseOptions options)
{
    EndfieldDiffuseBands bands;
    float3 cameraForward = EndfieldCameraForwardWS(normalWS);
    bands.ramp = EndfieldSampleRamp(options.rampSignal);
    float nof = dot(normalWS, cameraForward);
    bands.nofRamp = EndfieldSampleRamp(nof);

    float3 darkColor = lerp(
        baseColor,
        authoredDarkColor,
        saturate(_DarkColorStrength));
    float darkLuma = dot(darkColor, float3(0.299, 0.587, 0.114));
    darkColor = lerp(
        darkLuma.xxx,
        darkColor,
        max(_DarkColorSaturation, 0.0));
    float3 deepColor = darkColor * max(_DarkInDarkStrength, 0.0);

    ao = saturate(ao);
    visibility = saturate(visibility);
    bands.nofMask = pow(
        saturate(bands.nofRamp.a) * max(options.nofStrength, 0.0),
        max(options.nofPower, 0.001));
    float dayZeroMask = lerp(
        1.0,
        bands.nofMask,
        saturate(options.dayZeroNofBlend));
    float darkInDarkMask = lerp(
        1.0,
        bands.nofMask,
        saturate(options.nofInDarkInDark));
    float brightWeight = min(min(ao, visibility), bands.ramp.a);
    float darkInDarkWeight = saturate(
        ao * visibility * darkInDarkMask + bands.ramp.a);
    float dayZeroWeight = saturate(ao * visibility * dayZeroMask);
    float3 dayOneShadow = lerp(deepColor, darkColor, darkInDarkWeight);
    float3 dayOneBase = lerp(dayOneShadow, brightColor, brightWeight);
    float rampMaximum = max(
        max(bands.ramp.r, bands.ramp.g),
        bands.ramp.b);
    float rampMinimum = min(
        min(bands.ramp.r, bands.ramp.g),
        bands.ramp.b);
    float rampChroma = saturate(rampMaximum - rampMinimum);
    float3 rampColorEffect = max(bands.ramp.rgb, 0.0) * rampChroma +
        (1.0 - rampChroma);
    float3 rampedColor = dayOneBase * lerp(
        1.0.xxx,
        rampColorEffect,
        saturate(_RampColorStrength));
    float3 dayOne = saturate(options.applyRampControl) > 0.5
        ? rampedColor * EndfieldRampColorControl(dayOneBase, rampedColor)
        : rampedColor;

    float3 dayZero = lerp(darkColor, brightColor, dayZeroWeight);
    bands.color = lerp(dayZero, dayOne, EndfieldDayStrength());
    bands.color *= lerp(
        1.0,
        saturate(baseAlpha),
        saturate(_DiffuseAlphaEnergy));
    bands.lit = lerp(dayZeroWeight, brightWeight, EndfieldDayStrength());
    bands.dayZeroWeight = dayZeroWeight;
    bands.brightWeight = brightWeight;
    return bands;
}

float3 EndfieldEnvironmentBrdfMultiscatter(
    float3 f0,
    float smoothness,
    float ndotv)
{
    bool inputsFinite = EndfieldIsFinite3(f0) &&
        EndfieldIsFinite(smoothness) &&
        EndfieldIsFinite(ndotv);
    float3 safeF0 = EndfieldIsFinite3(f0) ? saturate(f0) : 0.04.xxx;
    float safeSmoothness = EndfieldIsFinite(smoothness)
        ? saturate(smoothness)
        : 0.5;
    float safeNoV = EndfieldIsFinite(ndotv) ? saturate(ndotv) : 1.0;
    float3 legacyBrdf = EndfieldEnvironmentBrdf(
        safeF0,
        safeSmoothness,
        safeNoV);
    if (!inputsFinite)
        return legacyBrdf;
    float NoV = safeNoV;
    float roughness = max(1.0 - safeSmoothness, 0.06);
    float roughness2 = max(roughness * roughness, 0.0078125);
    float roughness4 = roughness2 * roughness2;
    float roughness6 = roughness4 * roughness2;
    float NoV2 = NoV * NoV;
    float NoV3 = NoV2 * NoV;

    // Analytic split-sum DFG fit. Keeping scale and bias separate gives the
    // same A/B contract as a pre-integrated environment BRDF LUT.
    float fitA = 3.32707 * NoV + 0.0365463;
    float fitB = -9.04755 * NoV + 9.0632;
    float brdfNumerator = fitA + fitB * roughness2;
    float3 nvFactors = float3(
        3.59685 * NoV2 - 1.36772 * NoV3 + 1.0,
        9.22949 * NoV3 - 16.3174 * NoV2 + 9.04401,
        -20.2123 * NoV3 + 19.7886 * NoV2 + 5.56589);
    float brdfDenominator = dot(
        nvFactors,
        float3(1.0, roughness2, roughness6));
    float dfgScale = brdfNumerator /
        max(abs(brdfDenominator), 1e-4);

    float scaleFitPart1 = dot(
        float2(-1.28514, 1.0),
        float2(NoV, 0.990440011));
    float scaleFitPart2 = dot(
        float2(1.0, -0.75591),
        float2(1.29678, NoV));
    float biasNumerator = dot(
        float2(scaleFitPart1, scaleFitPart2),
        float2(1.0, roughness2));
    float biasFitX = dot(
        float3(2.92338, 59.4188, 1.0),
        float3(NoV, NoV3, 1.0));
    float biasFitY = dot(
        float3(1.0, -27.0302, 222.592),
        float3(20.3225, NoV, NoV3));
    float biasFitZ = dot(
        float3(626.130, 316.627, 1.0),
        float3(NoV, NoV3, 121.563004));
    float biasDenominator = dot(
        float3(biasFitX, biasFitY, biasFitZ),
        float3(1.0, roughness2, roughness6));
    float dfgBias = biasNumerator /
        max(abs(biasDenominator), 1e-4);
    float2 envBrdfAB = clamp(
        float2(dfgScale, dfgBias),
        float2(0.0, 0.0),
        float2(8.0, 8.0));

    float3 singleScatter = envBrdfAB.x * safeF0 + envBrdfAB.y;
    float directionalAlbedo = saturate(envBrdfAB.x + envBrdfAB.y);
    float Ess = max(directionalAlbedo, 1e-4);
    float Ems = 1.0 - Ess;
    float3 Favg = safeF0;
    float energyLossFactor = Ems / max(Ess, 1e-4);
    float3 Fms = Favg * energyLossFactor;
    float3 multipleScatter = singleScatter * (1.0.xxx + Fms);
    multipleScatter = clamp(multipleScatter, 0.0.xxx, 8.0.xxx);
    if (!EndfieldIsFinite3(multipleScatter))
        multipleScatter = legacyBrdf;
    return lerp(
        legacyBrdf,
        multipleScatter,
        EndfieldTutorialWeight());
}

float3 EndfieldSampleEmission(float2 uv, float baseAlpha)
{
    float4 rawEmission = SAMPLE_TEXTURE2D(
        _EmissionMap,
        sampler_EmissionMap,
        uv);
    float3 masked = rawEmission.r * _EmissionColor.rgb;
    float3 authoredRgb = rawEmission.rgb * _EmissionColor.rgb;
    float3 authoredRgbAlpha = authoredRgb * saturate(baseAlpha);
    float3 selected = _EmissionMapMode < 0.5
        ? masked
        : (_EmissionMapMode < 1.5 ? authoredRgb : authoredRgbAlpha);
    return selected *
        _UseEmissionMap * max(_EmissionIntensity, 0.0);
}

float3 EndfieldClothSubsurface(
    float3 baseColor,
    float baseAlpha,
    float3 normalWS,
    float3 viewDirectionWS)
{
    float NoV = saturate(dot(normalWS, viewDirectionWS));
    float exponent = 1.0 + saturate(baseAlpha) *
        max(_ClothSssPower, 0.0);
    float area = pow(max(1.05 - NoV, 1e-4), exponent);
    float weight = saturate(area * max(_ClothSssStrength, 0.0)) *
        EndfieldTutorialWeight();
    return lerp(baseColor, max(_ClothSssColor.rgb, 0.0), weight);
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
    float legacyVisibility = terms.shadowVisibility * terms.layerMatch;
    float sigmoidInput = clamp(
        (legacyVisibility - _ShadowCenter) /
            max(_ShadowSigmoidSmoothness, 1e-4),
        -16.0,
        16.0);
    float sigmoid = rcp(1.0 + exp(-sigmoidInput));
    float shapedVisibility = saturate(
        (sigmoid + _ShadowOffset) * max(_ShadowStrength, 0.0));
    return lerp(
        legacyVisibility,
        shapedVisibility,
        EndfieldTutorialWeight());
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

float3 EndfieldApplyFaceSign(float3 normalWS, float faceSign)
{
    return EndfieldSafeNormalize(
        normalWS * faceSign,
        float3(0.0, 1.0, 0.0));
}

float3 EndfieldSampleNormalWS(EndfieldVaryings input, float faceSign)
{
    float3 normalTS = EndfieldDecodeRawNormal(
        SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv),
        _NormalStrength);
    float3 normalWS = normalize(lerp(
        input.normalWS,
        EndfieldTangentToWorld(normalTS, input),
        _UseNormalMap));
    return EndfieldApplyFaceSign(normalWS, faceSign);
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

half4 EndfieldEvaluateBody(EndfieldVaryings input, float faceSign)
{
    float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) *
        _BaseColorTint;
    float4 material = lerp(
        float4(0.0, 1.0, 1.0, 0.45),
        SAMPLE_TEXTURE2D(_MaterialParamMap, sampler_MaterialParamMap, input.uv),
        _UseMaterialParamMap);
    float3 normalWS = EndfieldSampleNormalWS(input, faceSign);
    Light mainLight = GetMainLight(input.shadowCoord);
    EndfieldMainLightTerms keyLight = EndfieldGetMainLightTerms(mainLight);
    float3 lightDirWS = keyLight.directionWS;
    float3 perPixelViewDirWS = EndfieldSafeNormalize(
        GetWorldSpaceViewDir(input.positionWS), float3(0, 0, 1));
    float3 viewDirWS = EndfieldStylizedViewDirection(
        perPixelViewDirWS,
        normalWS);
    baseSample.rgb = EndfieldClothSubsurface(
        baseSample.rgb,
        baseSample.a,
        normalWS,
        perPixelViewDirWS);
    float ndotl = dot(normalWS, lightDirWS);
    float4 ramp = EndfieldSampleRamp(ndotl);
    float visibility = EndfieldShadowVisibility(keyLight);
    float ao = saturate(material.b);
    float lit = min(ramp.a, visibility);
    float useLut = saturate(max(_UseColorLut, _UseShadowLut));
    float3 darkColor = lerp(
        baseSample.rgb * 0.55,
        EndfieldSampleFlattenedColorLut(baseSample.rgb),
        useLut);
    float3 tutorialDarkColor = darkColor;
    darkColor *= lerp(0.8.xxx, ramp.rgb, 0.65);
    float3 diffuseColor = lerp(darkColor, baseSample.rgb, lit);
    EndfieldDiffuseOptions diffuseOptions;
    diffuseOptions.rampSignal = EndfieldBackLightSignal(
        ndotl,
        lightDirWS,
        EndfieldCameraForwardWS(normalWS));
    diffuseOptions.nofStrength = max(_NoFStrength, 0.0);
    diffuseOptions.nofPower = max(_NoFPowStrength, 0.001);
    diffuseOptions.nofInDarkInDark = 1.0;
    diffuseOptions.dayZeroNofBlend = 1.0;
    diffuseOptions.applyRampControl = 1.0;
    EndfieldDiffuseBands tutorialDiffuse = EndfieldThreeLayerDiffuse(
        baseSample.rgb,
        baseSample.rgb,
        baseSample.a,
        tutorialDarkColor,
        normalWS,
        visibility,
        ao,
        diffuseOptions);
    diffuseColor = lerp(
        diffuseColor,
        tutorialDiffuse.color,
        EndfieldTutorialWeight());
    lit = lerp(lit, tutorialDiffuse.lit, EndfieldTutorialWeight());
    ramp = lerp(ramp, tutorialDiffuse.ramp, EndfieldTutorialWeight());
    float bandMask = min(min(ao, visibility), ramp.a);
    float3 directLight = EndfieldTutorialDirectLight(
        normalWS,
        EndfieldDirectLight(keyLight),
        bandMask);
    float metallic = saturate(material.r);
    float reflectivity = saturate(material.g);
    float smoothness = saturate(material.a);
    float energyDistribution = lerp(
        1.0 - metallic,
        0.96 - 0.96 * metallic,
        EndfieldTutorialWeight());
    float3 diffuse = diffuseColor * energyDistribution *
        directLight * lerp(0.72, 1.0, lit);

    float3 specularLightDirWS = EndfieldStylizedSpecularLightDirection(
        lightDirWS,
        normalWS);
    float3 halfDir = EndfieldStylizedSpecularHalfDirection(
        normalWS,
        viewDirWS,
        lightDirWS);
    float specSignal = saturate(dot(normalWS, halfDir));
    float3 dielectricF0 = 0.04.xxx * lerp(0.5, 1.5, reflectivity);
    float3 metalF0 = max(baseSample.rgb, 0.12.xxx) *
        lerp(0.55, 1.0, reflectivity);
    float3 f0 = lerp(dielectricF0, metalF0, metallic);
    float roughness = max(1.0 - smoothness, 0.06);
    float ndotv = saturate(dot(normalWS, viewDirWS));
    float roughness2 = roughness * roughness;
    // Legacy path keeps the historical (NoV, roughness) LUT lookup; the
    // tutorial path follows the article's F0-refine UVs.
    float f0RefineU = lerp(
        EndfieldTutorialSpecularD(specSignal, roughness2) * roughness2,
        ndotv * ndotv,
        saturate(_RefineF0U_lerp));
    float f0RefineV = roughness * (1.0 - ao);
    float2 refineF0Uv = lerp(
        float2(ndotv, roughness),
        float2(f0RefineU, 1.0 - f0RefineV),
        EndfieldTutorialWeight());
    float3 refinedF0 = SAMPLE_TEXTURE2D(
        _SpecularRefineF0Tex,
        sampler_SpecularRefineF0Tex,
        refineF0Uv).rgb;
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
    float3 legacyDirectSpecular = EndfieldGgxSpecularFromHalf(
        normalWS, viewDirWS, specularLightDirWS, halfDir, roughness, f0) *
        specRefine *
        specularColor * directLight * visibility * specularOcclusion *
        _SpecularIntensity * metalDirectScale;
    float3 tutorialDirectSpecular = EndfieldTutorialSpecularDV(
        specSignal, ndotv, roughness2) *
        f0 * specRefine * specularColor *
        EndfieldTutorialSpecularEnvelope(
            tutorialDiffuse.dayZeroWeight,
            tutorialDiffuse.brightWeight,
            EndfieldDayStrength()) *
        directLight * _SpecularIntensity * metalDirectScale;
    float3 directSpecular = lerp(
        legacyDirectSpecular,
        tutorialDirectSpecular,
        EndfieldTutorialWeight());
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
    float3 environmentBrdf = EndfieldEnvironmentBrdfMultiscatter(
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
    float3 emission = EndfieldSampleEmission(input.uv, baseSample.a);
    float3 surfaceRim = EndfieldSurfaceRim(
        normalWS,
        perPixelViewDirWS,
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

half4 EndfieldEvaluateSkin(EndfieldVaryings input, float faceSign)
{
    float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) *
        _BaseColorTint;
    float3 complexion = EndfieldApplySkinTone(saturate(
        baseSample.rgb * float3(1.06, 1.015, 1.0) +
        float3(0.018, 0.006, 0.005)));
    float3 normalWS = EndfieldSampleNormalWS(input, faceSign);
    Light mainLight = GetMainLight(input.shadowCoord);
    EndfieldMainLightTerms keyLight = EndfieldGetMainLightTerms(mainLight);
    float3 lightDirWS = keyLight.directionWS;
    float3 perPixelViewDirWS = EndfieldSafeNormalize(
        GetWorldSpaceViewDir(input.positionWS), float3(0, 0, 1));
    float3 viewDirWS = EndfieldStylizedViewDirection(
        perPixelViewDirWS,
        normalWS);
    float viewEdge = pow(saturate(1.0 - dot(normalWS, viewDirWS)), 2.0);
    float sssArea = saturate(_SSSArea * viewEdge);
    complexion *= lerp(1.0.xxx, max(_SSSColor.rgb, 0.0), sssArea);
    float ndotl = dot(normalWS, lightDirWS);
    float4 ramp = EndfieldSampleRamp(ndotl);
    float authoredAo = saturate(baseSample.a);
    float ao = lerp(1.0, authoredAo, saturate(_SkinAOStrength));
    float visibility = EndfieldShadowVisibility(keyLight);
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
    float3 tutorialDarkColor = darkColor;
    darkColor *= lerp(0.88.xxx, ramp.rgb, 0.45);
    float3 diffuseColor = lerp(darkColor, complexion, lit);
    EndfieldDiffuseOptions diffuseOptions;
    diffuseOptions.rampSignal = EndfieldBackLightSignal(
        ndotl,
        lightDirWS,
        EndfieldCameraForwardWS(normalWS));
    // The skin reference omits the NoF band entirely.
    diffuseOptions.nofStrength = 0.0;
    diffuseOptions.nofPower = 1.0;
    diffuseOptions.nofInDarkInDark = 0.0;
    diffuseOptions.dayZeroNofBlend = 0.0;
    diffuseOptions.applyRampControl = 1.0;
    EndfieldDiffuseBands tutorialDiffuse = EndfieldThreeLayerDiffuse(
        complexion,
        complexion,
        baseSample.a,
        tutorialDarkColor,
        normalWS,
        visibility,
        ao,
        diffuseOptions);
    diffuseColor = lerp(
        diffuseColor,
        tutorialDiffuse.color,
        EndfieldTutorialWeight());
    lit = lerp(lit, tutorialDiffuse.lit, EndfieldTutorialWeight());
    ramp = lerp(ramp, tutorialDiffuse.ramp, EndfieldTutorialWeight());
    float bandMask = min(min(ao, visibility), ramp.a);
    float3 directLight = EndfieldTutorialDirectLight(
        normalWS,
        EndfieldDirectLight(keyLight),
        bandMask);
    float3 direct = diffuseColor * directLight *
        lerp(0.82, 1.0, lit) * lerp(0.86, 1.0, ao);
    float3 toonFill = complexion * directLight * 0.08 * ao;
    float3 indirect = SampleSH(normalWS) * complexion *
        _IndirectIntensity * lerp(0.65, 1.0, ao);
    float wrappedBack = saturate((dot(-normalWS, lightDirWS) + 0.35) / 1.35);
    float3 sss = complexion * max(_SSSColor.rgb, 0.0) * directLight *
        (0.18 + wrappedBack) * sssArea * (1.0 - lit) *
        _SkinSSSIntensity;
    float specularOcclusion = EndfieldSpecularOcclusion(ao, visibility);
    float3 halfDir = EndfieldSafeNormalize(viewDirWS + lightDirWS, normalWS);
    float specular = pow(saturate(dot(normalWS, halfDir)), 48.0) *
        visibility * specularOcclusion * _SpecularIntensity;
    float3 legacyDirectSpecular = specular * directLight * 0.04;
    float roughness = clamp(_SkinRoughness, 0.06, 1.0);
    float3 skinF0 = (0.02 + 0.06 * saturate(_SkinReflectivity)).xxx;
    float3 tutorialHalfDirWS = EndfieldStylizedSpecularHalfDirection(
        normalWS,
        viewDirWS,
        lightDirWS);
    float roughness2 = roughness * roughness;
    float3 tutorialDirectSpecular = EndfieldTutorialSpecularDV(
        saturate(dot(normalWS, tutorialHalfDirWS)),
        saturate(dot(normalWS, viewDirWS)),
        roughness2) *
        skinF0 *
        EndfieldTutorialSpecularEnvelope(
            tutorialDiffuse.dayZeroWeight,
            tutorialDiffuse.brightWeight,
            EndfieldDayStrength()) *
        directLight * _SpecularIntensity;
    float3 directSpecular = lerp(
        legacyDirectSpecular,
        tutorialDirectSpecular,
        EndfieldTutorialWeight());
    float3 surfaceRim = EndfieldSurfaceRim(
        normalWS,
        perPixelViewDirWS,
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

half4 EndfieldEvaluateFace(EndfieldVaryings input, float faceSign)
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
    float3 faceNormal = EndfieldApplyFaceSign(input.normalWS, faceSign);

    EndfieldHeadBasis head = EndfieldGetFaceBasis();
    Light mainLight = GetMainLight(input.shadowCoord);
    EndfieldMainLightTerms keyLight = EndfieldGetMainLightTerms(mainLight);
    float3 lightDirWS = EndfieldSafeNormalize(
        keyLight.directionWS,
        head.forwardWS);
    float3 projected = lightDirWS - dot(lightDirWS, head.upWS) * head.upWS;
    projected = EndfieldSafeNormalize(projected, head.forwardWS);
    float projectedRight = dot(projected, head.rightWS);
    float sdfUvFlag = step(0.0, projectedRight);
    float2 sdfUv = float2(
        lerp(1.0 - input.uv.x, input.uv.x, sdfUvFlag),
        input.uv.y);
    float4 sdf = SAMPLE_TEXTURE2D(_SDFLightmap, sampler_SDFLightmap, sdfUv);
    float margin = (sdf.r + sdf.g) * 0.5;
    float forwardAmount = dot(projected, head.forwardWS);
    float backAmount = saturate(-forwardAmount);
    float sideAmount = saturate(1.0 - abs(projectedRight));
    float sdfPhase = clamp(
        forwardAmount + backAmount * sideAmount * _BackLightStrength *
            lerp(
                1.0,
                max(_MikuEndfieldBackLightStrength, 0.0),
                EndfieldTutorialWeight()),
        -1.0,
        1.0);
    float sdfLight = EndfieldFaceSdfLightArticle(
        margin,
        sdfPhase,
        _FaceShadowOffset,
        max(_FaceShadowSoftness, 1e-4));

    float4 area = EndfieldSampleFaceArea(input.uv);
    float4 refine = EndfieldSampleFaceRefine(input.uv);
    float geometricLight = smoothstep(
        0.45 - _ShadowSmoothness,
        0.45 + _ShadowSmoothness,
        dot(faceNormal, lightDirWS) * 0.5 + 0.5);
    float sdfValid = step(1e-3, margin) * step(margin, 1.0 - 1e-3);
    float legacySdfWithFallback = lerp(
        geometricLight,
        max(sdfLight, geometricLight),
        sdfValid);
    float sdfWithFallback = lerp(
        legacySdfWithFallback,
        sdfLight,
        EndfieldTutorialWeight());
    float faceLight = lerp(
        sdfWithFallback,
        geometricLight,
        saturate(refine.g));
    faceLight = lerp(
        geometricLight,
        faceLight,
        saturate(_UseFaceSDF));
    float visibility = lerp(
        1.0,
        EndfieldShadowVisibility(keyLight),
        saturate(max(refine.b, area.g)));
    float sdfNoL = sdfLight * 2.0 - 1.0;
    float geometricNoL = dot(faceNormal, lightDirWS);
    float rampSignal = lerp(sdfNoL, geometricNoL, saturate(refine.g));
    float litSignal = min(faceLight, visibility);
    float4 ramp = EndfieldSampleRamp(litSignal * 2.0 - 1.0);
    litSignal = min(litSignal, ramp.a);
    float lit = lerp(0.42, 1.0, litSignal);

    float3 perPixelViewDirWS = EndfieldSafeNormalize(
        GetWorldSpaceViewDir(input.positionWS), head.forwardWS);
    float3 viewDirWS = EndfieldStylizedViewDirection(
        perPixelViewDirWS,
        faceNormal);
    float legacyViewEdge = pow(
        saturate(1.0 - dot(faceNormal, perPixelViewDirWS)),
        2.0);
    float legacyAuthoredSss = lerp(
        1.0,
        saturate(refine.r),
        saturate(_UseFaceRefineMap));
    float headFDotCameraF = dot(
        head.forwardWS,
        EndfieldCameraForwardWS(head.forwardWS));
    float tutorialAuthoredSss = lerp(
        saturate(headFDotCameraF + 0.5),
        1.0,
        saturate(refine.g)) * saturate(refine.r);
    float authoredSss = lerp(
        legacyAuthoredSss,
        tutorialAuthoredSss,
        EndfieldTutorialWeight());
    float tutorialViewEdge = 1.0 - (
        saturate(dot(faceNormal, perPixelViewDirWS)) * 0.85 + 0.15);
    float viewEdge = lerp(
        legacyViewEdge,
        tutorialViewEdge,
        EndfieldTutorialWeight());
    float sssArea = saturate(_SSSArea * viewEdge * authoredSss);
    complexion *= lerp(1.0.xxx, max(_SSSColor.rgb, 0.0), sssArea);

    float useLut = saturate(max(_UseColorLut, _UseShadowLut));
    float3 darkColor = lerp(
        complexion * float3(0.74, 0.59, 0.56),
        EndfieldSampleFlattenedColorLut(complexion),
        useLut);
    float3 tutorialDarkColor = darkColor;
    darkColor *= lerp(0.92.xxx, ramp.rgb, 0.35);
    float sideMask = saturate(area.r);
    float3 faceColor = lerp(darkColor, complexion, lit);
    EndfieldDiffuseOptions diffuseOptions;
    diffuseOptions.rampSignal = rampSignal;
    // The face reference omits the NoF band entirely.
    diffuseOptions.nofStrength = 0.0;
    diffuseOptions.nofPower = 1.0;
    diffuseOptions.nofInDarkInDark = 0.0;
    diffuseOptions.dayZeroNofBlend = 0.0;
    diffuseOptions.applyRampControl = 1.0;
    EndfieldDiffuseBands tutorialDiffuse = EndfieldThreeLayerDiffuse(
        complexion,
        complexion,
        baseSample.a,
        tutorialDarkColor,
        faceNormal,
        visibility,
        1.0,
        diffuseOptions);
    faceColor = lerp(
        faceColor,
        tutorialDiffuse.color,
        EndfieldTutorialWeight());
    ramp = lerp(ramp, tutorialDiffuse.ramp, EndfieldTutorialWeight());
    float bandMask = min(min(1.0, visibility), ramp.a);
    float3 directLight = EndfieldTutorialDirectLight(
        faceNormal,
        EndfieldDirectLight(keyLight),
        bandMask);
    faceColor *= lerp(0.94, 1.0, sideMask);
    float3 direct = faceColor * directLight *
        lerp(0.85, 1.0, litSignal);
    float3 toonFill = complexion * directLight *
        lerp(0.07, 0.11, litSignal);
    float3 indirect = SampleSH(faceNormal) * complexion *
        _IndirectIntensity;

    float rawSdfZ = sdf.b * 2.0 - 1.0;
    float mirroredSdfZ = lerp(-rawSdfZ, rawSdfZ, sdfUvFlag);
    float3 sdfDirection = EndfieldSafeNormalize(
        float3(mirroredSdfZ, 6.10351562e-5, 1.0 - abs(mirroredSdfZ)),
        float3(0.0, 0.0, 1.0));
    float3 sdfNormalWS = EndfieldSafeNormalize(
        head.rightWS * sdfDirection.x +
        head.upWS * sdfDirection.y +
        head.forwardWS * sdfDirection.z,
        faceNormal);
    float3 authoredFaceNormal = EndfieldSafeNormalize(
        lerp(sdfNormalWS, faceNormal, saturate(refine.g)),
        faceNormal);
    float3 specNormal = EndfieldSafeNormalize(
        lerp(
            faceNormal,
            authoredFaceNormal,
            saturate(_FaceSdfNormalStrength) * EndfieldTutorialWeight()),
        faceNormal);
    float3 halfDir = EndfieldStylizedSpecularHalfDirection(
        specNormal,
        viewDirWS,
        lightDirWS);
    float specular = pow(saturate(dot(specNormal, halfDir)), 52.0) *
        _SpecularIntensity * EndfieldShadowVisibility(keyLight);
    float highlightMask = SAMPLE_TEXTURE2D(
        _HighlightMap, sampler_HighlightMap, input.uv).r;
    float3 lightEnvelope = directLight * visibility;
    float3 highlight = highlightMask * _UseHighlightMap *
        _FaceHighlightIntensity * complexion * lightEnvelope;
    float3 sss = complexion * max(_SSSColor.rgb, 0.0) *
        directLight *
        (0.18 + saturate(-dot(faceNormal, lightDirWS))) *
        sssArea * (1.0 - litSignal) *
        _SkinSSSIntensity;
    float3 legacyDirectSpecular = specular * directLight * 0.04;
    float faceRoughness2 = max(_FaceRoughness, 0.06) *
        max(_FaceRoughness, 0.06);
    float3 faceF0 = (0.02 + 0.06 * saturate(_FaceReflectivity)).xxx;
    float3 tutorialDirectSpecular = EndfieldTutorialSpecularDV(
        saturate(dot(specNormal, halfDir)),
        saturate(dot(specNormal, perPixelViewDirWS)),
        faceRoughness2) *
        faceF0 *
        EndfieldTutorialSpecularEnvelope(
            tutorialDiffuse.dayZeroWeight,
            tutorialDiffuse.brightWeight,
            EndfieldDayStrength()) *
        directLight * _SpecularIntensity;
    float3 directSpecular = lerp(
        legacyDirectSpecular,
        tutorialDirectSpecular,
        EndfieldTutorialWeight()) + highlight;
    float3 surfaceRim = EndfieldSurfaceRim(
        specNormal,
        perPixelViewDirWS,
        lightDirWS,
        directLight,
        visibility);
    surfaceRim *= 1.0 - EndfieldTutorialWeight();
    float physicalRim = EndfieldFaceRimArea(
        saturate(dot(specNormal, perPixelViewDirWS)),
        saturate(_RimLightArea));
    float rimArea = lerp(
        physicalRim,
        saturate(refine.a),
        saturate(_FaceRimMaskStrength));
    float headFDotCameraFRim = saturate(-0.9 + headFDotCameraF * 10.0);
    headFDotCameraFRim = headFDotCameraFRim * headFDotCameraFRim *
        (3.0 - 2.0 * headFDotCameraFRim);
    float3 rimLight = rimArea * headFDotCameraFRim *
        max(_RimLightTintColor.rgb, 0.0) *
        max(_FaceRimSideStrength, 0.0);
    float3 rimLightEffectd = rimLight * min(
        1.0,
        EndfieldShadowVisibility(keyLight));
    float3 rimLightBrdf = lerp(
        0.25,
        complexion,
        saturate(_RimLightDiffuseColorEffect));
    float3 cameraRightWS = EndfieldSafeNormalize(
        mul((float3x3)GetViewToWorldMatrix(), float3(1.0, 0.0, 0.0)),
        head.rightWS);
    float3 faceRim = rimLightBrdf * rimLightEffectd *
        saturate(dot(cameraRightWS, specNormal)) *
        EndfieldTutorialWeight();
    float3 result = direct + toonFill + indirect + sss + directSpecular +
        surfaceRim + faceRim;
    float3 debugColor = EndfieldDebug(
        result, baseSample.rgb, area, specNormal, ramp, sdfLight.xxx);
    if (_DebugView > 5.5)
        debugColor = blushMask.xxx;
    debugColor = EndfieldApplyLightingDebug(
        debugColor,
        keyLight,
        direct + toonFill + sss + surfaceRim + faceRim,
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

half4 EndfieldEvaluateHair(EndfieldVaryings input, float faceSign)
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
    diffuseNormal = EndfieldApplyFaceSign(diffuseNormal, faceSign);
    authoredHighlightNormal = EndfieldApplyFaceSign(
        authoredHighlightNormal,
        faceSign);

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
    float3 lightDirWS = EndfieldSafeNormalize(
        keyLight.directionWS,
        head.forwardWS);
    float3 perPixelViewDirWS = EndfieldSafeNormalize(
        GetWorldSpaceViewDir(input.positionWS), head.forwardWS);
    perPixelViewDirWS = EndfieldSafeNormalize(
        perPixelViewDirWS + head.upWS * _HairViewDirYOffset *
            (1.0 - saturate(material.r)) * EndfieldTutorialWeight(),
        head.forwardWS);
    float3 viewDirWS = EndfieldStylizedViewDirection(
        perPixelViewDirWS,
        diffuseNormal);
    float ndotl = dot(diffuseNormal, lightDirWS);
    float4 ramp = EndfieldSampleRamp(ndotl);
    float legacyVisibility = lerp(
        1.0,
        EndfieldShadowVisibility(keyLight),
        saturate(material.r));
    float visibility = lerp(
        legacyVisibility,
        EndfieldShadowVisibility(keyLight),
        EndfieldTutorialWeight());
    float ao = saturate(material.b);
    float lit = min(ramp.a, min(visibility, ao));
    float3 darkColor = baseSample.rgb * lerp(
        float3(0.52, 0.46, 0.48),
        ramp.rgb,
        0.55);
    float3 tutorialDarkColor = baseSample.rgb *
        float3(0.52, 0.46, 0.48);
    float3 diffuseColor = lerp(darkColor, baseSample.rgb, lit);
    EndfieldDiffuseOptions diffuseOptions;
    diffuseOptions.rampSignal = EndfieldBackLightSignal(
        ndotl,
        lightDirWS,
        EndfieldCameraForwardWS(diffuseNormal));
    diffuseOptions.nofStrength = max(_NoFStrength, 0.0);
    diffuseOptions.nofPower = max(_NoFPowStrength, 0.001);
    diffuseOptions.nofInDarkInDark = 1.0;
    diffuseOptions.dayZeroNofBlend = 1.0;
    diffuseOptions.applyRampControl = 1.0;
    EndfieldDiffuseBands tutorialDiffuse = EndfieldThreeLayerDiffuse(
        baseSample.rgb,
        baseSample.rgb,
        baseSample.a,
        tutorialDarkColor,
        diffuseNormal,
        visibility,
        ao,
        diffuseOptions);
    diffuseColor = lerp(
        diffuseColor,
        tutorialDiffuse.color,
        EndfieldTutorialWeight());
    lit = lerp(lit, tutorialDiffuse.lit, EndfieldTutorialWeight());
    ramp = lerp(ramp, tutorialDiffuse.ramp, EndfieldTutorialWeight());
    float bandMask = min(min(ao, visibility), ramp.a);
    float3 directLight = EndfieldTutorialDirectLight(
        diffuseNormal,
        EndfieldDirectLight(keyLight),
        bandMask);
    float3 diffuse = diffuseColor * directLight * lerp(0.72, 1.0, lit);
    float3 indirect = SampleSH(diffuseNormal) * baseSample.rgb *
        ao * _IndirectIntensity;

    float3 cameraRightWS = EndfieldSafeNormalize(
        mul((float3x3)GetViewToWorldMatrix(), float3(1.0, 0.0, 0.0)),
        head.rightWS);
    float3 flattenedHighlightNormal = EndfieldSafeNormalize(
        highlightNormal - dot(highlightNormal, cameraRightWS) * cameraRightWS,
        highlightNormal);
    highlightNormal = EndfieldSafeNormalize(
        lerp(
            highlightNormal,
            flattenedHighlightNormal,
            saturate(_HairFlatten) * EndfieldTutorialWeight()),
        highlightNormal);
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
    float3 halfDir = EndfieldStylizedSpecularHalfDirection(
        highlightNormal,
        viewDirWS,
        lightDirWS);
    float primary = EndfieldKajiyaKayLobe(
        shiftedPrimary, halfDir, _HairPrimaryWidth);
    float secondary = EndfieldKajiyaKayLobe(
        shiftedSecondary, halfDir, _HairSecondaryWidth);
    float ToH_lut = dot(strandTangent, halfDir);
    float3 cameraForwardWS = EndfieldCameraForwardWS(head.forwardWS);
    float2 viewDirProjection = float2(
        dot(viewDirWS, cameraRightWS),
        dot(viewDirWS, cameraForwardWS));
    float2 highlightNormalProjection = float2(
        dot(authoredHighlightNormal, cameraRightWS),
        dot(authoredHighlightNormal, cameraForwardWS));
    float VoHN_horizontal = saturate(dot(
        viewDirProjection,
        highlightNormalProjection));
    VoHN_horizontal = pow(
        VoHN_horizontal,
        max(_HairLutVPower, 1e-4));
    float directionMask = step(0.0, ToH_lut);
    float2 hairLutUv = float2(
        saturate(1.0 - ToH_lut * ToH_lut),
        VoHN_horizontal * VoHN_horizontal * directionMask);
    float3 authoredHairLut = SAMPLE_TEXTURE2D(
        _SpecularRefineF0Tex,
        sampler_SpecularRefineF0Tex,
        hairLutUv).rgb;
    authoredHairLut = EndfieldSelectHairSpecularLut(authoredHairLut);
    float3 hairLut = lerp(
        1.0.xxx,
        authoredHairLut,
        saturate(_UseSpecularRefine));
    float3 tutorialHairLut = lerp(
        primary.xxx,
        authoredHairLut,
        saturate(_UseSpecularRefine));
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
    float3 legacySpecular = (primarySpecular + secondarySpecular) *
        specMask * visibility * specularOcclusion * directLight *
        _SpecularIntensity;
    float3 baseF0 = max(_HairBaseF0.rgb, 0.0) *
        lerp(0.5, 1.5, reflectivity);
    float3 lutF0 = tutorialHairLut * baseF0;
    float hairSine = sqrt(saturate(1.0 - ToH_lut * ToH_lut));
    float3 backF0 = max(_HairBackF0.rgb, 0.0) * backStrength *
        pow(hairSine, max(_HairBackF0ToHPower, 1.0));
    float3 finalF0 = lutF0 * 7.0 + backF0;
    float selfAoShadowEffect = lerp(
        saturate(_SelfAoShadowStrength),
        1.0,
        tutorialDiffuse.lit);
    float3 tutorialSpecular = finalF0 * specMask * selfAoShadowEffect *
        directLight * _SpecularIntensity;
    float3 specular = lerp(
        legacySpecular,
        tutorialSpecular,
        EndfieldTutorialWeight());
    float accessoryMask = (1.0 - specMask) * _UseSpecularMask;
    float accessoryRoughness = lerp(0.42, 0.16, backStrength);
    float3 accessoryF0 = max(
        lerp(0.16.xxx, specRefine, 0.72),
        0.12.xxx) * lerp(0.65, 1.0, reflectivity);
    float3 accessoryLightDirWS = EndfieldStylizedSpecularLightDirection(
        lightDirWS,
        authoredHighlightNormal);
    float3 accessoryHalfDirWS = EndfieldStylizedSpecularHalfDirection(
        authoredHighlightNormal,
        viewDirWS,
        lightDirWS);
    float3 accessoryDirect = EndfieldGgxSpecularFromHalf(
        authoredHighlightNormal,
        viewDirWS,
        accessoryLightDirWS,
        accessoryHalfDirWS,
        accessoryRoughness,
        accessoryF0) * directLight * visibility * _SpecularIntensity;
    float3 accessoryEnvironmentBrdf = lerp(
        accessoryF0,
        EndfieldEnvironmentBrdfMultiscatter(
            accessoryF0,
            1.0 - accessoryRoughness,
            saturate(dot(authoredHighlightNormal, viewDirWS))),
        EndfieldTutorialWeight());
    float3 accessoryReflection = GlossyEnvironmentReflection(
        reflect(-viewDirWS, authoredHighlightNormal),
        accessoryRoughness,
        specularOcclusion) * accessoryEnvironmentBrdf * _IndirectIntensity;
    float3 accessoryVisibility = accessoryF0 * directLight * 0.28;
    float3 accessorySpecular = accessoryMask * specularOcclusion *
        (accessoryDirect + accessoryReflection + accessoryVisibility);
    float3 surfaceRim = EndfieldSurfaceRim(
        diffuseNormal,
        perPixelViewDirWS,
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

half4 EndfieldEvaluateEye(EndfieldVaryings input, float faceSign)
{
    float3 normalWS = EndfieldApplyFaceSign(input.normalWS, faceSign);
    float3 perPixelViewDirWS = EndfieldSafeNormalize(
        GetWorldSpaceViewDir(input.positionWS),
        normalWS);
    float3 viewDirWS = EndfieldStylizedViewDirection(
        perPixelViewDirWS,
        normalWS);
    float3 viewDirTS = float3(
        dot(viewDirWS, input.tangentWS),
        dot(viewDirWS, input.bitangentWS),
        dot(viewDirWS, normalWS));
    float2 fractionalUv = frac(input.uv);
    float2 eyeCenterAreaUv = fractionalUv - float2(0.5, 0.5);
    float eyeCenterArea = step(0.25, dot(eyeCenterAreaUv, eyeCenterAreaUv));
    float2 centeredUv = eyeCenterAreaUv * 2.0;
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

    float2 legacyCorneaXy = centeredUv * _CorneaBumpStrength * irisMask;
    float legacyCorneaZ = sqrt(saturate(
        1.0 - dot(legacyCorneaXy, legacyCorneaXy)));
    float3 legacyCorneaNormalWS = EndfieldTangentToWorld(
        EndfieldSafeNormalize(
            float3(legacyCorneaXy, legacyCorneaZ),
            float3(0, 0, 1)),
        input);
    float uvSquared = dot(centeredUv, centeredUv);
    float zHemisphere = max(
        sqrt(max(0.0, 1.0 - min(1.0, uvSquared))),
        1e-16);
    float3 tutorialCorneaNormalTS = float3(
        0.125 * _CorneaBumpStrength * centeredUv,
        zHemisphere);
    tutorialCorneaNormalTS = lerp(
        tutorialCorneaNormalTS,
        float3(0.0, 0.0, 1.0),
        eyeCenterArea);
    tutorialCorneaNormalTS.x = -tutorialCorneaNormalTS.x;
    float3 tutorialCorneaNormalWS = EndfieldTangentToWorld(
        EndfieldSafeNormalize(tutorialCorneaNormalTS, float3(0, 0, 1)),
        input);
    float3 generatedCorneaNormalWS = EndfieldSafeNormalize(
        lerp(
            legacyCorneaNormalWS,
            tutorialCorneaNormalWS,
            EndfieldTutorialWeight()),
        normalWS);
    float3 corneaNormalWS = EndfieldSafeNormalize(
        lerp(normalWS, generatedCorneaNormalWS, irisMask * irisMode),
        normalWS);
    Light mainLight = GetMainLight(input.shadowCoord);
    EndfieldMainLightTerms keyLight = EndfieldGetMainLightTerms(mainLight);
    float3 lightDirWS = keyLight.directionWS;
    float visibility = EndfieldShadowVisibility(keyLight);
    float3 diffuseNormalWS = EndfieldSafeNormalize(
        lerp(
            normalWS,
            corneaNormalWS,
            irisMask * irisMode * EndfieldTutorialWeight()),
        normalWS);
    EndfieldHeadBasis head = EndfieldGetFaceBasis();
    float3 flattenedLightDirWS = EndfieldEyeFlattenedLightDirection(
        lightDirWS,
        head);
    float eyeNoL = dot(diffuseNormalWS, flattenedLightDirWS);
    float eyeVisibility = lerp(
        visibility,
        1.0,
        EndfieldTutorialWeight());
    float lambert = saturate(dot(diffuseNormalWS, lightDirWS) * 0.5 + 0.5);
    float3 scleraColor = float3(0.94, 0.88, 0.84) * _BaseColorTint.rgb;
    float3 eyeColor = lerp(irisSample.rgb, scleraColor, scleraMode);
    float3 eyeCenterTrick = lerp(
        1.0.xxx,
        max(_EyeCenterColor.rgb, 0.0) * 2.5,
        eyeCenterArea);
    float3 eyeAlphaTrick = lerp(
        1.0.xxx,
        max(_EyeAlphaColor.rgb, 0.0) * 2.5,
        saturate(irisSample.a));
    float3 authoredIrisColor = irisSample.rgb *
        eyeCenterTrick * eyeAlphaTrick;
    float3 tutorialBrightColor = lerp(
        authoredIrisColor,
        scleraColor,
        scleraMode);
    float legacyEyeLit = lambert * visibility;
    EndfieldDiffuseOptions diffuseOptions;
    diffuseOptions.rampSignal = eyeNoL;
    diffuseOptions.nofStrength = 1.0;
    diffuseOptions.nofPower = 1.0;
    diffuseOptions.nofInDarkInDark = 1.0;
    diffuseOptions.dayZeroNofBlend = 1.0;
    diffuseOptions.applyRampControl = 0.0;
    EndfieldDiffuseBands tutorialDiffuse = EndfieldThreeLayerDiffuse(
        eyeColor,
        tutorialBrightColor,
        irisSample.a,
        eyeColor * 0.62,
        diffuseNormalWS,
        eyeVisibility,
        1.0,
        diffuseOptions);
    float3 directLight = EndfieldTutorialDirectLight(
        diffuseNormalWS,
        EndfieldDirectLight(keyLight),
        tutorialDiffuse.ramp.a);
    float3 legacyDiffuse = eyeColor * directLight *
        lerp(0.62, 1.0, legacyEyeLit);
    float eyeTutorialWeight = EndfieldTutorialWeight() *
        saturate(_EyeRampStrength);
    float3 diffuse = lerp(
        legacyDiffuse,
        tutorialDiffuse.color * directLight,
        eyeTutorialWeight);
    float3 indirect = SampleSH(diffuseNormalWS) * eyeColor * _IndirectIntensity;

    float3 eyeSpecularLightDirWS = EndfieldStylizedSpecularLightDirection(
        lightDirWS,
        corneaNormalWS);
    float3 eyeSpecularHalfDirWS = EndfieldStylizedSpecularHalfDirection(
        corneaNormalWS,
        viewDirWS,
        lightDirWS);
    float3 directCornea = EndfieldGgxSpecularFromHalf(
        corneaNormalWS,
        viewDirWS,
        eyeSpecularLightDirWS,
        eyeSpecularHalfDirWS,
        0.12,
        0.04.xxx) * directLight * visibility;
    float3 corneaNormalVS = TransformWorldToViewDir(corneaNormalWS, true);
    float2 matcapUv = saturate(
        corneaNormalVS.xy * (0.5 * max(_MatCapUvScale, 0.0)) + 0.5);
    float4 matcap = SAMPLE_TEXTURE2D(
        _MatCap,
        sampler_MatCap,
        matcapUv);
    float3 lightEnvelope = directLight * visibility;
    float3 legacyMatcapSpecular = matcap.rgb *
        saturate(0.35 + matcap.a * 2.0) * lightEnvelope * _UseMatCap;
    float3 legacyWetHighlight = (directCornea + legacyMatcapSpecular) *
        _CorneaHighlightColor.rgb * _CorneaSpecularIntensity *
        _SpecularIntensity * irisMask * irisMode;
    float dayDarkEffect = lerp(
        tutorialDiffuse.nofRamp.a,
        tutorialDiffuse.ramp.a,
        EndfieldDayStrength());
    float specularDarkEffect = dayDarkEffect * 0.5 + 0.5;
    specularDarkEffect *= lerp(
        saturate(_SelfAoShadowStrength),
        1.0,
        dayDarkEffect);
    float3 tutorialMatcapBrdf = matcap.rgb * _SpecularIntensity +
        max(_MatCapAlphaColor.rgb, 0.0) * matcap.a *
        max(_MatCapAlphaStrength, 0.0);
    float3 tutorialWetHighlight = directLight * tutorialMatcapBrdf *
        specularDarkEffect * _UseMatCap * _CorneaHighlightColor.rgb *
        _CorneaSpecularIntensity * irisMask * irisMode;
    float3 halfDir = EndfieldStylizedSpecularHalfDirection(
        normalWS,
        viewDirWS,
        lightDirWS);
    float scleraSpecular = pow(saturate(dot(normalWS, halfDir)), 42.0) *
        visibility * _SpecularIntensity * 0.025;
    float3 legacySpecular = lerp(
        legacyWetHighlight,
        scleraSpecular * directLight,
        scleraMode);
    float3 specular = lerp(
        legacySpecular,
        tutorialWetHighlight,
        EndfieldTutorialWeight());
    float3 result = diffuse + indirect + specular;
    result = EndfieldApplyLightingDebug(
        result,
        keyLight,
        diffuse,
        specular,
        indirect);
    return half4(result, 1.0);
}

half4 EndfieldEvaluateMouth(EndfieldVaryings input, float faceSign)
{
    float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) *
        _BaseColorTint;
    float3 normalWS = EndfieldApplyFaceSign(input.normalWS, faceSign);
    Light mainLight = GetMainLight(input.shadowCoord);
    EndfieldMainLightTerms keyLight = EndfieldGetMainLightTerms(mainLight);
    float3 lightDirWS = keyLight.directionWS;
    float3 perPixelViewDirWS = EndfieldSafeNormalize(
        GetWorldSpaceViewDir(input.positionWS),
        normalWS);
    float3 viewDirWS = EndfieldStylizedViewDirection(
        perPixelViewDirWS,
        normalWS);
    float visibility = EndfieldShadowVisibility(keyLight);
    float3 directLight = EndfieldTutorialDirectLight(
        normalWS,
        EndfieldDirectLight(keyLight),
        visibility);
    float lambert = saturate(dot(normalWS, lightDirWS) * 0.5 + 0.5);
    float mouthLit = lambert * visibility;
    float3 diffuse = baseSample.rgb * directLight *
        lerp(0.55, 1.0, mouthLit);
    float3 indirect = SampleSH(normalWS) * baseSample.rgb * _IndirectIntensity;
    float3 halfDir = EndfieldStylizedSpecularHalfDirection(
        normalWS,
        viewDirWS,
        lightDirWS);
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

half4 EndfieldForwardFragment(
    EndfieldVaryings input,
    FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
{
    float faceSign = IS_FRONT_VFACE(isFrontFace, 1.0, -1.0);
    if (_PartMode < 0.5)
        return EndfieldEvaluateBody(input, faceSign);
    if (_PartMode < 1.5)
        return EndfieldEvaluateSkin(input, faceSign);
    if (_PartMode < 2.5)
        return EndfieldEvaluateHair(input, faceSign);
    if (_PartMode < 3.5)
        return EndfieldEvaluateFace(input, faceSign);
    if (_PartMode < 4.5)
        return EndfieldEvaluateEye(input, faceSign);
    return EndfieldEvaluateMouth(input, faceSign);
}

struct EndfieldOutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float outlineCoverage : TEXCOORD1;
    float4 color : COLOR;
};

EndfieldOutlineVaryings EndfieldOutlineVertex(EndfieldAttributes input)
{
    EndfieldOutlineVaryings output;
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    float3 outlineNormalOS = MikuGameToonOutlineNormalTangentSpaceV2(
        input.smoothNormalData,
        input.normalOS,
        input.tangentOS);
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
    float outlineEnabled = MikuGameToonOutlineFinite1(_UseOutline)
        ? saturate(_UseOutline)
        : 0.0;
    float additionalWidthMask = outlineMask * accessoryOutlineMask;
    output.positionCS = MikuGameToonOutlinePositionCS(
        position.positionCS,
        position.positionWS,
        outlineNormalOS,
        _OutlineWidth,
        _OutlineReferenceDistance,
        _OutlineDistanceScale,
        input.color,
        additionalWidthMask * outlineEnabled);
    output.outlineCoverage = MikuGameToonOutlineCoverageWithVertexMask(
        position.positionWS,
        outlineEnabled,
        _OutlineWidth,
        _OutlineReferenceDistance,
        _OutlineDistanceScale,
        MikuGameToonOutlineVertexMask(input.color),
        additionalWidthMask);
    output.uv = uv;
    output.color = input.color;
    return output;
}

half4 EndfieldOutlineFragment(EndfieldOutlineVaryings input) : SV_Target
{
    MikuGameToonOutlineClipCoverage(input.outlineCoverage);
    float3 baseColor = SAMPLE_TEXTURE2D(
        _BaseMap, sampler_BaseMap, input.uv).rgb;
    float3 color = pow(max(baseColor, 0.0.xxx), max(_OutlineGamma, 1e-5)) *
        _OutlineColorTint.rgb;
    return half4(color, 1.0);
}

struct EndfieldDepthVaryings { float4 positionCS : SV_POSITION; };
float3 _LightDirection;
float3 _LightPosition;

EndfieldDepthVaryings EndfieldShadowVertex(EndfieldAttributes input)
{
    EndfieldDepthVaryings output;
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    output.positionCS = TransformWorldToHClip(
        ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    output.positionCS = ApplyShadowClamping(output.positionCS);
    return output;
}

EndfieldDepthVaryings EndfieldDepthVertex(EndfieldAttributes input)
{
    EndfieldDepthVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}
half4 EndfieldDepthFragment(EndfieldDepthVaryings input) : SV_Target { return 0; }

half4 EndfieldTransparentFragment(
    EndfieldVaryings input,
    FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
{
    float faceSign = IS_FRONT_VFACE(isFrontFace, 1.0, -1.0);
    float4 rawSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    float4 baseSample = rawSample * _BaseColorTint;
    float alpha = EndfieldSelectAlpha(rawSample, _AlphaSource);
    float effectMask = lerp(
        1.0,
        SAMPLE_TEXTURE2D(_EffectMask, sampler_EffectMask, input.uv).r,
        _UseEffectMask);
    float coverage = saturate(alpha * effectMask);
    clip(coverage - _AlphaClip - 1e-5);
    float3 emission = EndfieldSampleEmission(input.uv, baseSample.a);
    float3 overlayColor = lerp(
        baseSample.rgb,
        _BaseColorTint.rgb,
        saturate(_OverlayUseTintOnly));
    float opaqueCoverage = step(3.5, _AlphaSource);
    float outputAlpha = lerp(
        saturate(coverage * _BaseColorTint.a),
        1.0,
        opaqueCoverage);
    float3 unlitColor = overlayColor + emission;
    if (_LightingMode < 0.5)
        return half4(unlitColor, outputAlpha);
    return half4(EndfieldEvaluateBody(input, faceSign).rgb, outputAlpha);
}

#endif
