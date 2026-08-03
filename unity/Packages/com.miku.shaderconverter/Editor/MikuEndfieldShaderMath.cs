// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    internal static class MikuEndfieldShaderMath
    {
        internal const int TextureAlpha = 0;
        internal const int Luminance = 1;
        internal const int InverseRed = 2;
        internal const int RawRed = 3;
        internal const int Opaque = 4;
        internal const int HairLutColorRgb = 0;
        internal const int HairLutScalarRed = 1;

        internal static float SelectAlpha(Color raw, int source)
        {
            switch (source)
            {
                case TextureAlpha:
                    return raw.a;
                case Luminance:
                    return Vector3.Dot(
                        new Vector3(raw.r, raw.g, raw.b),
                        new Vector3(0.299f, 0.587f, 0.114f));
                case InverseRed:
                    return 1f - raw.r;
                case RawRed:
                    return raw.r;
                default:
                    return 1f;
            }
        }

        internal static float FaceSdfThreshold(float forward, float offset) =>
            0.5f - 0.5f * Mathf.Clamp(forward, -1f, 1f) + offset;

        internal static Vector2 FaceSdfUv(Vector2 uv, float rightAmount) =>
            new Vector2(rightAmount >= 0f ? uv.x : 1f - uv.x, uv.y);

        internal static float FaceSdfPhase(
            float forward,
            float right,
            float backLightStrength)
        {
            var back = Mathf.Clamp01(-forward);
            var side = Mathf.Clamp01(1f - Mathf.Abs(right));
            return Mathf.Clamp(
                forward + back * side * Mathf.Max(backLightStrength, 0f),
                -1f,
                1f);
        }

        internal static float FaceSdfLight(
            float margin,
            float forward,
            float offset,
            float softness)
        {
            var threshold = FaceSdfThreshold(forward, offset);
            softness = Mathf.Max(softness, 0.001f);
            return SmoothStep(
                threshold - softness,
                threshold + softness,
                margin);
        }

        internal static float FaceLight(
            float sdfLight,
            float geometricLight,
            float refine,
            float useFaceSdf,
            bool sdfValid = true)
        {
            sdfLight = Mathf.Clamp01(sdfLight);
            geometricLight = Mathf.Clamp01(geometricLight);
            var sdfWithFallback = sdfValid
                ? Mathf.Max(sdfLight, geometricLight)
                : geometricLight;
            var refined = Mathf.Lerp(
                sdfWithFallback,
                geometricLight,
                Mathf.Clamp01(refine));
            return Mathf.Lerp(
                geometricLight,
                refined,
                Mathf.Clamp01(useFaceSdf));
        }

        internal static Color ToonDirectColor(
            Color shadowColor,
            Color litColor,
            float lightSignal,
            float shadowVisibility,
            float distanceDiagnostic,
            bool hasMainLight = true,
            bool layerMatches = true)
        {
            var lit = Mathf.Clamp01(lightSignal) *
                Mathf.Clamp01(shadowVisibility);
            var availability = MainLightAvailability(
                hasMainLight,
                layerMatches);
            _ = distanceDiagnostic;
            return Color.Lerp(shadowColor, litColor, lit) * availability;
        }

        internal static float MainLightAvailability(
            bool hasMainLight,
            bool layerMatches) =>
            hasMainLight && layerMatches ? 1f : 0f;

        internal static float MainLightAvailability(
            Color mainLightColor,
            bool layerMatches) =>
            layerMatches && mainLightColor.maxColorComponent > 1e-5f ? 1f : 0f;

        internal static float DirectionalSignal(
            Vector3 normal,
            Vector3 lightDirection)
        {
            if (normal.sqrMagnitude <= 1e-12f ||
                lightDirection.sqrMagnitude <= 1e-12f)
                return 0f;
            return Mathf.Clamp01(
                Vector3.Dot(normal.normalized, lightDirection.normalized) *
                0.5f + 0.5f);
        }

        internal static Color DirectDiffuseOnly(
            Color baseColor,
            Color mainLightColor,
            Vector3 normal,
            Vector3 lightDirection,
            float shadowVisibility,
            bool layerMatches = true)
        {
            var weight = DirectionalSignal(normal, lightDirection) *
                Mathf.Clamp01(shadowVisibility) *
                MainLightAvailability(mainLightColor, layerMatches);
            return new Color(
                baseColor.r * mainLightColor.r * weight,
                baseColor.g * mainLightColor.g * weight,
                baseColor.b * mainLightColor.b * weight,
                1f);
        }

        internal static Color ShOnly(
            Color sampleSh,
            Color baseColor,
            float indirectIntensity) =>
            new Color(
                sampleSh.r * baseColor.r * Mathf.Max(indirectIntensity, 0f),
                sampleSh.g * baseColor.g * Mathf.Max(indirectIntensity, 0f),
                sampleSh.b * baseColor.b * Mathf.Max(indirectIntensity, 0f),
                1f);

        internal static float BlushMask(
            float alpha,
            float gain,
            float strength) =>
            Mathf.Clamp01(alpha * Mathf.Max(gain, 0f) * Mathf.Clamp01(strength));

        internal static float HairSphereBlend(float materialRed, float use) =>
            Mathf.Clamp01((1f - Mathf.Clamp01(materialRed)) * Mathf.Clamp01(use));

        internal static float SpecularOcclusion(
            float ao,
            float shadowVisibility,
            float minimum) =>
            Mathf.Lerp(
                Mathf.Clamp01(minimum),
                1f,
                Mathf.Clamp01(ao) * Mathf.Clamp01(shadowVisibility));

        internal static Color HairSpecularLut(Color raw, int mode) =>
            mode == HairLutScalarRed
                ? new Color(raw.r, raw.r, raw.r, raw.a)
                : raw;

        internal static float SurfaceRim(
            Vector3 normal,
            Vector3 viewDirection,
            Vector3 lightDirection,
            float strength,
            float power,
            float lightAlign,
            float shadowVisibility)
        {
            if (normal.sqrMagnitude <= 1e-12f ||
                viewDirection.sqrMagnitude <= 1e-12f ||
                lightDirection.sqrMagnitude <= 1e-12f)
                return 0f;

            normal.Normalize();
            viewDirection.Normalize();
            lightDirection.Normalize();
            var edge = Mathf.Pow(
                Mathf.Clamp01(1f - Mathf.Clamp01(
                    Vector3.Dot(normal, viewDirection))),
                Mathf.Max(power, 1e-4f));
            var alignment = Mathf.Clamp01(Vector3.Dot(
                -normal,
                lightDirection));
            return edge * Mathf.Lerp(
                    1f,
                    alignment,
                    Mathf.Clamp01(lightAlign)) *
                Mathf.Max(strength, 0f) *
                Mathf.Max(Mathf.Clamp01(shadowVisibility), 0.35f);
        }

        internal static float MetalBoost(float metallic, float boost) =>
            Mathf.Lerp(
                1f,
                Mathf.Max(boost, 0f),
                Mathf.Clamp01(metallic));

        internal static Color SkinTone(
            Color color,
            float brightness,
            float whitening,
            Color target)
        {
            var brightened = new Color(
                Mathf.Clamp01(color.r * Mathf.Max(brightness, 0f)),
                Mathf.Clamp01(color.g * Mathf.Max(brightness, 0f)),
                Mathf.Clamp01(color.b * Mathf.Max(brightness, 0f)),
                color.a);
            var result = Color.Lerp(
                brightened,
                new Color(
                    Mathf.Clamp01(target.r),
                    Mathf.Clamp01(target.g),
                    Mathf.Clamp01(target.b),
                    brightened.a),
                Mathf.Clamp01(whitening));
            result.a = color.a;
            return result;
        }

        internal static Vector3 RotateEnvironmentDirection(
            Vector3 direction,
            float degrees) =>
            Quaternion.AngleAxis(degrees, Vector3.up) * direction;

        internal static Color EyeColor(
            Color irisColor,
            Color tint,
            float eyeMode)
        {
            var sclera = new Color(
                0.94f * tint.r,
                0.88f * tint.g,
                0.84f * tint.b,
                1f);
            return Color.Lerp(
                irisColor,
                sclera,
                eyeMode >= 0.5f ? 1f : 0f);
        }

        internal static Vector2 AtlasUv(
            Vector2 uv,
            float tileIndex,
            float columns,
            float rows)
        {
            columns = Mathf.Max(columns, 1f);
            rows = Mathf.Max(rows, 1f);
            tileIndex = Mathf.Clamp(tileIndex, 0f, columns * rows - 1f);
            var tileSize = new Vector2(1f / columns, 1f / rows);
            var tileOffset = new Vector2(
                tileIndex % columns,
                Mathf.Floor(tileIndex / columns));
            return Vector2.Scale(
                new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y)) +
                    tileOffset,
                tileSize);
        }

        internal static Color MetalF0(
            Color baseColor,
            float metallic,
            float reflectivity)
        {
            metallic = Mathf.Clamp01(metallic);
            reflectivity = Mathf.Clamp01(reflectivity);
            var dielectric = 0.04f * Mathf.Lerp(0.5f, 1.5f, reflectivity);
            var scale = Mathf.Lerp(0.55f, 1f, reflectivity);
            var metal = new Color(
                Mathf.Max(baseColor.r, 0.12f) * scale,
                Mathf.Max(baseColor.g, 0.12f) * scale,
                Mathf.Max(baseColor.b, 0.12f) * scale,
                1f);
            return Color.Lerp(
                new Color(dielectric, dielectric, dielectric, 1f),
                metal,
                metallic);
        }

        internal static float MetalAo(float ao, float metallic) =>
            Mathf.Max(Mathf.Clamp01(ao), Mathf.Clamp01(metallic));

        internal static Color MetalBaseResponse(
            Color baseColor,
            float metallic,
            Color directLight,
            float smoothness,
            float reflectivity)
        {
            var strength = Mathf.Lerp(
                    0.50f,
                    0.85f,
                    Mathf.Clamp01(smoothness)) *
                Mathf.Lerp(
                    0.65f,
                    1f,
                    Mathf.Clamp01(reflectivity)) *
                Mathf.Clamp01(metallic);
            return new Color(
                baseColor.r * directLight.r * strength,
                baseColor.g * directLight.g * strength,
                baseColor.b * directLight.b * strength,
                1f);
        }

        internal static Vector3 CorneaNormal(
            Vector2 uv,
            float bumpStrength)
        {
            var centered = (uv - new Vector2(0.5f, 0.5f)) * 2f;
            var radiusSquared = centered.sqrMagnitude;
            var irisMask = 1f - SmoothStep(0.72f, 1f, radiusSquared);
            var xy = centered * Mathf.Clamp01(bumpStrength) * irisMask;
            var z = Mathf.Sqrt(Mathf.Max(0f, 1f - xy.sqrMagnitude));
            var value = new Vector3(xy.x, xy.y, z);
            return value.sqrMagnitude > 1e-12f ? value.normalized : Vector3.forward;
        }

        static float SmoothStep(float from, float to, float value)
        {
            var width = Mathf.Max(to - from, 1e-6f);
            var t = Mathf.Clamp01((value - from) / width);
            return t * t * (3f - 2f * t);
        }
    }
}
