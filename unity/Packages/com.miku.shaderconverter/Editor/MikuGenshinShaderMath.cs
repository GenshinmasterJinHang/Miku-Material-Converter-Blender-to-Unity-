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
            var alphaMask = Mathf.Clamp01(vertexColor.a);
            return alphaMask > 1e-4f
                ? alphaMask
                : Mathf.Clamp01(vertexColor.g);
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
