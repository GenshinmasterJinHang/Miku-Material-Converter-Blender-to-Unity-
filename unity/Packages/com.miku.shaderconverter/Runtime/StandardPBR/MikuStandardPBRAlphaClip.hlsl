#ifndef MIKU_STANDARD_PBR_ALPHA_CLIP_INCLUDED
#define MIKU_STANDARD_PBR_ALPHA_CLIP_INCLUDED

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float _AOStrength;
    float _Metallic;
    float _Roughness;
    float _Glossiness;
    float4 _SpecularColor;
    float _SpecularStrength;
    float _ReflectionStrength;
    float _BumpStrength;
    float _BumpDistance;
    float _NormalStrength;
    float _FlipNormalGreen;
    float _HeightStrength;
    float _HeightBias;
    float _DisplacementStrength;
    float _DisplacementMidlevel;
    float4 _EmissionColor;
    float _EmissionStrength;
    float _Alpha;
    float _AlphaCutoff;
    float _ParallaxSteps;
    float _ParallaxMode;
    float _Surface;
    float _Blend;
    float _Cull;
    float _ZWrite;
CBUFFER_END

half MIKUSampleAlpha(float2 uv)
{
    half alpha = _BaseColor.a * _Alpha;
    #if defined(_MIKU_BASECOLOR_MAP)
    alpha *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
    #endif
    #if defined(_MIKU_ALPHA_MAP)
    alpha *= SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, uv).r;
    #endif
    return alpha;
}

void MIKUClipAlpha(float2 uv)
{
    #if defined(_MIKU_ALPHA_CLIP)
    clip(MIKUSampleAlpha(uv) - _AlphaCutoff);
    #endif
}

#endif
