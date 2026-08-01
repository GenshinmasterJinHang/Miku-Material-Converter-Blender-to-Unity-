#ifndef MIKU_NPR_FACE_SDF_INCLUDED
#define MIKU_NPR_FACE_SDF_INCLUDED

// Chooses the animated head-bone basis supplied by MikuFaceSdfHeadBinder when
// available, otherwise keeps the shader's model/object-matrix fallback.
void Miku_ResolveFaceSdfHeadAxes(
    float3 fallbackForwardWS,
    float3 fallbackRightWS,
    float3 fallbackUpWS,
    float4 boundForwardWS,
    float4 boundRightWS,
    float4 boundUpWS,
    float boundAxesValid,
    out float3 headForwardWS,
    out float3 headRightWS,
    out float3 headUpWS)
{
    float useBoundAxes = step(0.5, boundAxesValid) *
        step(1e-5, dot(boundForwardWS.xyz, boundForwardWS.xyz)) *
        step(1e-5, dot(boundRightWS.xyz, boundRightWS.xyz)) *
        step(1e-5, dot(boundUpWS.xyz, boundUpWS.xyz));

    headForwardWS = normalize(lerp(fallbackForwardWS, boundForwardWS.xyz, useBoundAxes));
    headRightWS = normalize(lerp(fallbackRightWS, boundRightWS.xyz, useBoundAxes));
    headUpWS = normalize(lerp(fallbackUpWS, boundUpWS.xyz, useBoundAxes));

    // Remove scale/shear while preserving the authored handedness of Right.
    float3 orthogonalRight = normalize(cross(headUpWS, headForwardWS));
    orthogonalRight *= dot(orthogonalRight, headRightWS) < 0.0 ? -1.0 : 1.0;
    headRightWS = orthogonalRight;
    headUpWS = normalize(cross(headForwardWS, headRightWS));
}

#endif
