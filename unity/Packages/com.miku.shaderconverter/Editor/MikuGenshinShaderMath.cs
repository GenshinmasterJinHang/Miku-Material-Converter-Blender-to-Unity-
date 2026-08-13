// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    /// <summary>
    /// CPU mirrors of the Genshin tutorial-conformance math in
    /// <c>Runtime/Genshin/GenshinCommon.hlsl</c>. EditMode tests compare these
    /// against the tutorial formulas so the shader source is not the only
    /// place the contract is enforced.
    /// </summary>
    public static class MikuGenshinShaderMath
    {
        public static float RampRow(
            float materialId,
            float a0,
            float a1,
            float a2,
            float a3,
            float a4,
            float inNight)
        {
            var rampSampling = Mathf.Clamp01(inNight) * 0.5f;
            var ramp0 = a0 * -0.1f + 1.05f - rampSampling;
            var ramp1 = a1 * -0.1f + 1.05f - rampSampling;
            var ramp2 = a2 * -0.1f + 1.05f - rampSampling;
            var ramp3 = a3 * -0.1f + 1.05f - rampSampling;
            var ramp4 = a4 * -0.1f + 1.05f - rampSampling;
            var rampV = ramp0;
            rampV = Mathf.Lerp(rampV, ramp1, Step(0.25f, materialId));
            rampV = Mathf.Lerp(rampV, ramp2, Step(0.45f, materialId));
            rampV = Mathf.Lerp(rampV, ramp3, Step(0.65f, materialId));
            rampV = Mathf.Lerp(rampV, ramp4, Step(0.95f, materialId));
            return Mathf.Clamp01(rampV);
        }

        public static float Step(float edge, float value)
        {
            return value >= edge ? 1f : 0f;
        }

        public static float OutlineVertexMask(Color vertexColor)
        {
            return Mathf.Clamp01(vertexColor.g);
        }

        public static float TutorialAo(float lightMapGreen) =>
            SmoothStep(0.2f, 0.3f, Mathf.Clamp01(lightMapGreen));

        public static float TutorialLightingSignal(
            float ndotL,
            float lightMapGreen,
            float dark,
            float grey)
        {
            var halfLambert = SmoothStep(
                0f,
                Mathf.Max(grey, 1e-4f),
                ndotL + dark);
            return Mathf.Clamp01(
                halfLambert * TutorialAo(lightMapGreen));
        }

        // Kept for source compatibility with 2.4/early-3.0 validation tools.
        // Realtime visibility is deliberately ignored by the toon coordinate
        // and is applied through MainShadowVisibility/ApplyMainShadow instead.
        public static float TutorialLightingSignal(
            float ndotL,
            float lightMapGreen,
            float dark,
            float grey,
            float visibility) => TutorialLightingSignal(
                ndotL,
                lightMapGreen,
                dark,
                grey);

        public static float MainShadowVisibility(
            float shadowAttenuation,
            float distanceAttenuation,
            float influence)
        {
            return Mathf.Clamp01(distanceAttenuation) * Mathf.Lerp(
                1f,
                Mathf.Clamp01(shadowAttenuation),
                Mathf.Clamp01(influence));
        }

        public static Color ApplyMainShadow(
            Color toonColor,
            Color darkestRampColor,
            float shadowAttenuation,
            float influence)
        {
            var weight = (1f - Mathf.Clamp01(shadowAttenuation)) *
                         Mathf.Clamp01(influence);
            return Color.LerpUnclamped(toonColor, darkestRampColor, weight);
        }

        public static float ToonTransition(float lightingSignal, float softness)
        {
            var width = Mathf.Max(0.001f, softness);
            return SmoothStep(
                0.998f - width,
                0.998f,
                Mathf.Clamp01(lightingSignal));
        }

        public static float TutorialMetalMask(float lightMapRed) =>
            1f - Step(lightMapRed, 0.9f);

        public static Color TutorialMetal(
            Color baseColor,
            float lightMapRed,
            float metalSample,
            Color metalMapColor,
            float metalIntensity)
        {
            var matcap = Color.Lerp(
                Positive(metalMapColor),
                Positive(baseColor),
                Mathf.Clamp01(metalSample));
            return matcap * TutorialMetalMask(lightMapRed) *
                   Mathf.Max(0f, metalIntensity);
        }

        public static Color TutorialSpecular(
            Color baseColor,
            float lightMapRed,
            float lightMapBlue,
            float ndotH,
            float gloss,
            float strength,
            float brightMask,
            float visibility)
        {
            var factor = Mathf.Pow(
                    Mathf.Clamp01(ndotH),
                    Mathf.Max(gloss, 1f)) *
                Mathf.Clamp01(lightMapRed) *
                Mathf.Clamp01(lightMapBlue) *
                Mathf.Max(strength, 0f) *
                Mathf.Clamp01(brightMask) *
                Mathf.Clamp01(visibility);
            return baseColor * factor;
        }

        public static Color OutlineRegionColor(
            float lightMapAlpha,
            Color outlineColor0,
            Color outlineColor1,
            Color outlineColor2,
            Color outlineColor3,
            Color outlineColor4,
            bool regionMode)
        {
            if (!regionMode)
                return outlineColor0;
            var regionColor = outlineColor0;
            regionColor = Color.Lerp(
                regionColor,
                outlineColor1,
                Step(0.25f, lightMapAlpha));
            regionColor = Color.Lerp(
                regionColor,
                outlineColor2,
                Step(0.45f, lightMapAlpha));
            regionColor = Color.Lerp(
                regionColor,
                outlineColor3,
                Step(0.65f, lightMapAlpha));
            regionColor = Color.Lerp(
                regionColor,
                outlineColor4,
                Step(0.95f, lightMapAlpha));
            return regionColor;
        }

        public static float DiffuseAlphaEmissionMask(float baseAlpha)
        {
            return SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(baseAlpha));
        }

        public static float DiffuseAlphaClipMask(float baseAlpha)
        {
            return SmoothStep(
                0.05f,
                0.7f,
                Mathf.Clamp01(baseAlpha));
        }

        public static Vector3 DecodeNormalMap(Vector3 normalTS, float bumpScale)
        {
            var scale = Mathf.Max(0f, bumpScale);
            var xy = new Vector2(
                normalTS.x * scale,
                normalTS.y * scale);
            var z = Mathf.Sqrt(Mathf.Max(
                0f,
                1f - Mathf.Min(1f, Vector2.Dot(xy, xy))));
            return new Vector3(xy.x, xy.y, z).normalized;
        }

        public static Color MaskedSkinTone(
            Color diffuse,
            Color skinTone,
            float skinMask)
        {
            return Color.Lerp(
                diffuse,
                skinTone,
                Mathf.Clamp01(skinMask));
        }

        static float SmoothStep(float edge0, float edge1, float value)
        {
            // HLSL smoothstep(min, max, x), not Unity's Mathf.SmoothStep,
            // which applies the curve to the raw value and then lerps.
            var t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static Color Positive(Color value) => new Color(
            Mathf.Max(0f, value.r),
            Mathf.Max(0f, value.g),
            Mathf.Max(0f, value.b),
            Mathf.Max(0f, value.a));

        public static Vector2 BackFaceUv(
            Vector2 uv0,
            Vector2 uv1,
            bool isFrontFace,
            bool backUv1)
        {
            return !isFrontFace && backUv1 ? uv1 : uv0;
        }
    }
}
