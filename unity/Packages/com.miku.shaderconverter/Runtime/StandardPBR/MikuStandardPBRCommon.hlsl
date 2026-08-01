#ifndef MIKU_STANDARD_PBR_COMMON_INCLUDED
#define MIKU_STANDARD_PBR_COMMON_INCLUDED

float3 MIKUBlendNormalRNM(float3 baseNormalTS, float3 detailNormalTS)
{
    baseNormalTS = normalize(baseNormalTS * float3(2.0, 2.0, 2.0) + float3(-1.0, -1.0, 0.0));
    detailNormalTS = normalize(detailNormalTS * float3(-2.0, -2.0, 2.0) + float3(1.0, 1.0, -1.0));
    return normalize(baseNormalTS * dot(baseNormalTS, detailNormalTS) - detailNormalTS * baseNormalTS.z);
}

float2 ApplyHeightParallax(float2 uv, float3 viewDirTS)
{
    #if defined(_MIKU_PARALLAX) || defined(_MIKU_POM)
    float height = 0.0;
    #if defined(_MIKU_DISPLACEMENT_MAP)
    height = SAMPLE_TEXTURE2D(_DisplacementMap, sampler_DisplacementMap, uv).r - _DisplacementMidlevel;
    height *= _DisplacementStrength;
    #elif defined(_MIKU_HEIGHT_MAP)
    height = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, uv).r + _HeightBias;
    height *= _HeightStrength;
    #endif
    float3 v = normalize(viewDirTS);
    uv += (height * v.xy) / max(v.z, 0.15);
    #endif
    return uv;
}

float MIKUSampleScalar(TEXTURE2D_PARAM(mapTex, mapSampler), float2 uv, float fallback)
{
    return SAMPLE_TEXTURE2D(mapTex, mapSampler, uv).r * fallback;
}

float3 MIKUBumpNormalTS(float2 uv)
{
    float height = SAMPLE_TEXTURE2D(_BumpHeightMap, sampler_BumpHeightMap, uv).r;
    float heightDx = ddx(height);
    float heightDy = ddy(height);
    return normalize(float3(-heightDx * _BumpStrength * _BumpDistance, -heightDy * _BumpStrength * _BumpDistance, 1.0));
}

SurfaceData MIKUSampleSurfaceData(float2 uv, float3 viewDirTS)
{
    uv = ApplyHeightParallax(uv, viewDirTS);

    SurfaceData surfaceData = (SurfaceData)0;
    float4 baseSample = _BaseColor;
    #if defined(_MIKU_BASECOLOR_MAP)
    baseSample *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    #endif
    surfaceData.albedo = baseSample.rgb;
    surfaceData.alpha = saturate(baseSample.a * _Alpha);
    #if defined(_MIKU_ALPHA_MAP)
    surfaceData.alpha *= SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, uv).r;
    #endif

    float ao = 1.0;
    #if defined(_MIKU_AO_MAP)
    float sampleAO = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, uv).r;
    ao = lerp(1.0, sampleAO, _AOStrength);
    #endif
    surfaceData.occlusion = saturate(ao);

    float metallic = _Metallic;
    #if defined(_MIKU_METALLIC_MAP)
    metallic = SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, uv).r * _Metallic;
    #endif
    surfaceData.metallic = saturate(metallic);

    float roughness = _Roughness;
    #if defined(_MIKU_ROUGHNESS_MAP)
    roughness = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, uv).r * _Roughness;
    #endif
    float smoothness = saturate(1.0 - roughness);
    #if defined(_MIKU_GLOSSINESS_MAP) && !defined(_MIKU_ROUGHNESS_MAP)
    smoothness = saturate(SAMPLE_TEXTURE2D(_GlossinessMap, sampler_GlossinessMap, uv).r * _Glossiness);
    #endif
    surfaceData.smoothness = smoothness;

    float3 normalTS = float3(0.0, 0.0, 1.0);
    #if defined(_MIKU_NORMAL_MAP)
    normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalStrength);
    if (_FlipNormalGreen > 0.5)
        normalTS.y = -normalTS.y;
    #endif
    #if defined(_MIKU_BUMP_MAP)
    normalTS = MIKUBlendNormalRNM(normalTS, MIKUBumpNormalTS(uv));
    #endif
    surfaceData.normalTS = normalize(normalTS);

    surfaceData.specular = _SpecularColor.rgb * _SpecularStrength;
    #if defined(_MIKU_SPECULAR_MAP)
    surfaceData.specular *= SAMPLE_TEXTURE2D(_SpecularMap, sampler_SpecularMap, uv).rgb;
    #endif

    surfaceData.emission = _EmissionColor.rgb * _EmissionStrength;
    #if defined(_MIKU_EMISSION_MAP)
    surfaceData.emission *= SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
    #endif

    #if defined(_MIKU_REFLECTION_MAP)
    float3 reflectionControl = SAMPLE_TEXTURE2D(_ReflectionMap, sampler_ReflectionMap, uv).rgb * _ReflectionStrength;
    surfaceData.occlusion *= saturate(max(max(reflectionControl.r, reflectionControl.g), reflectionControl.b));
    #endif

    return surfaceData;
}

#endif
