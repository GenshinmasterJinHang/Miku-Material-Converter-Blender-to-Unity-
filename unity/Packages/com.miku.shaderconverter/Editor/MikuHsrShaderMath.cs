// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    /// <summary>
    /// CPU mirrors of the HSR tutorial-conformance lighting math in
    /// <c>Runtime/HSR/HSRCommon.hlsl</c>. EditMode tests keep the mask-channel
    /// interpretation and the shader implementation on the same contract.
    /// </summary>
    internal static class MikuHsrShaderMath
    {
        internal static float TutorialShadowAoHalfLambert(
            float nDotL,
            float lightMapGreen)
        {
            var halfLambert = Mathf.Clamp01(nDotL * 0.5f + 0.5f);
            return Mathf.Clamp01(4f * halfLambert * lightMapGreen);
        }

        internal static float TutorialRampU(float signal)
        {
            return Mathf.Clamp01(signal) * 0.85f + 0.15f;
        }

        internal static float TutorialSpecularMask(
            float nDotH,
            float exponent,
            float lightMapBlue,
            float softness)
        {
            var blinnPhong = Mathf.Pow(
                Mathf.Clamp01(nDotH),
                Mathf.Max(1f, exponent));
            var threshold = 1.04f - Mathf.Clamp01(lightMapBlue);
            var safeSoftness = Mathf.Max(1e-5f, softness);
            return SmoothStep(
                threshold - safeSoftness,
                threshold + safeSoftness,
                blinnPhong);
        }

        internal static float FaceSpecularWeight(
            float nDotH,
            float exponent,
            float thresholdMask,
            float softness,
            float intensity,
            float skinMask)
        {
            return TutorialSpecularMask(
                       nDotH,
                       exponent,
                       thresholdMask,
                       softness) *
                   Mathf.Max(0f, intensity) *
                   Mathf.Clamp01(skinMask);
        }

        internal static float NoseLineMask(
            float nDotV,
            float faceMapBlue,
            float power,
            float strength)
        {
            var signal = Mathf.Pow(
                             Mathf.Clamp01(nDotV),
                             Mathf.Max(0.1f, power)) *
                         Mathf.Clamp01(faceMapBlue) *
                         Mathf.Max(0f, strength);
            return SmoothStep(0f, 0.25f, signal);
        }

        static float SmoothStep(float edge0, float edge1, float value)
        {
            var t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
