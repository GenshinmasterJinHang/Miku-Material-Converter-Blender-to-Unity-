// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    internal static class MikuGameToonCpuMath
    {
        internal static float HighValueMask(float value) =>
            SmoothStep(0.96f, 0.995f, Mathf.Clamp01(value));

        internal static float WarmPaleFaceMask(Color rawBaseColor)
        {
            var color = Clamp01(rawBaseColor);
            var maximum = Mathf.Max(color.r, color.g, color.b);
            var minimum = Mathf.Min(color.r, color.g, color.b);
            var luma = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
            var chroma = maximum - minimum;
            var warm = SmoothStep(0.015f, 0.09f, color.r - color.b) *
                SmoothStep(-0.035f, 0.055f, color.r - color.g) *
                SmoothStep(0.16f, 0.42f, luma);
            var pale = SmoothStep(0.55f, 0.82f, minimum) *
                (1f - SmoothStep(0.08f, 0.22f, chroma));
            return Mathf.Clamp01(Mathf.Max(warm, pale) *
                SmoothStep(0.08f, 0.20f, luma));
        }

        internal static float StarRailBodySkinMask(
            float lightMapAlpha,
            Color rawBaseColor)
        {
            const float skinRow = 5f / 255f;
            var color = Clamp01(rawBaseColor);
            var luma = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
            var row = 1f - SmoothStep(
                0.012f,
                0.035f,
                Mathf.Abs(Mathf.Clamp01(lightMapAlpha) - skinRow));
            var warm = SmoothStep(0.02f, 0.10f, color.r - color.b) *
                SmoothStep(-0.03f, 0.07f, color.r - color.g) *
                SmoothStep(0.16f, 0.42f, luma);
            return Mathf.Clamp01(row * warm);
        }

        internal static Color ApplySkinTone(
            Color baseColor,
            float mask,
            float brightness,
            float whitening,
            Color target)
        {
            var brightened = baseColor * Mathf.Max(0f, brightness);
            var toned = Color.LerpUnclamped(
                brightened,
                target,
                Mathf.Clamp01(whitening));
            return Color.LerpUnclamped(baseColor, toned, Mathf.Clamp01(mask));
        }

        internal static Color SkinSss(
            Color skinColor,
            float skinMask,
            Vector3 normal,
            Vector3 viewDirection,
            Vector3 lightDirection,
            Color lightColor,
            float litAmount,
            float intensity,
            float area,
            Color sssColor)
        {
            normal.Normalize();
            viewDirection.Normalize();
            lightDirection.Normalize();
            var viewEdge = Mathf.Pow(Mathf.Clamp01(1f - Vector3.Dot(normal, viewDirection)), 2f);
            var backLight = Mathf.Clamp01((Vector3.Dot(-normal, lightDirection) + 0.35f) / 1.35f);
            var transmission = Mathf.Clamp01(skinMask) *
                Mathf.Clamp01(area * (0.35f + 0.65f * viewEdge));
            transmission *= Mathf.Lerp(0.18f, 1f, backLight) *
                (1f - 0.65f * Mathf.Clamp01(litAmount));
            return Multiply(Multiply(Positive(skinColor), Positive(sssColor)),
                       Positive(lightColor)) *
                transmission * Mathf.Max(0f, intensity);
        }

        internal static Color HuePreservingSoftShoulder(
            Color color,
            float knee,
            float ceiling)
        {
            var safeKnee = Mathf.Max(0f, knee);
            var safeCeiling = Mathf.Max(safeKnee + 0.0001f, ceiling);
            var peak = Mathf.Max(color.r, color.g, color.b);
            if (peak <= safeKnee)
                return color;
            var shoulderRange = safeCeiling - safeKnee;
            var compressedPeak = safeKnee + shoulderRange *
                (1f - Mathf.Exp(-(peak - safeKnee) / shoulderRange));
            return color * (compressedPeak / Mathf.Max(peak, 0.00001f));
        }

        internal static Color CompressNonEmissive(
            Color color,
            float compression,
            float knee,
            float ceiling) =>
            Color.LerpUnclamped(
                color,
                HuePreservingSoftShoulder(color, knee, ceiling),
                Mathf.Clamp01(compression));

        internal static float WuwaStockingMask(Color idColor)
        {
            var color = Clamp01(idColor);
            var luminance = color.r * 0.2126f +
                color.g * 0.7152f +
                color.b * 0.0722f;
            return luminance > 0.5f ? 1f : 0f;
        }

        internal static Vector2 WuwaEyeHighlightUv(
            Vector2 uv,
            Vector2 scale,
            Vector2 offset)
        {
            var safeScale = new Vector2(
                Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                Mathf.Max(Mathf.Abs(scale.y), 0.0001f));
            return new Vector2(
                (uv.x - 0.5f - offset.x) / safeScale.x + 0.5f,
                (uv.y - 0.5f - offset.y) / safeScale.y + 0.5f);
        }

        internal static Vector2 WuwaEyeAffineUv(
            Vector2 uv,
            Vector3 row0,
            Vector3 row1)
        {
            var source = new Vector3(uv.x, uv.y, 1f);
            return new Vector2(
                Vector3.Dot(row0, source),
                Vector3.Dot(row1, source));
        }

        internal static Vector2 WuwaEyeParallaxUv(
            Vector2 surfaceUv,
            Vector3 viewDirection,
            Vector3 tangent,
            Vector3 bitangent,
            float strength,
            float tangentValid)
        {
            var offset = Vector2.zero;
            if (tangentValid > 0f &&
                viewDirection.sqrMagnitude > 0.00000001f &&
                tangent.sqrMagnitude > 0.00000001f &&
                bitangent.sqrMagnitude > 0.00000001f)
            {
                var view = viewDirection.normalized;
                offset = new Vector2(
                    Vector3.Dot(view, tangent.normalized),
                    Vector3.Dot(view, bitangent.normalized)) *
                    strength * Mathf.Clamp01(tangentValid);
                offset.y = -offset.y;
            }
            return new Vector2(
                Mathf.Clamp01(surfaceUv.x - offset.x),
                Mathf.Clamp01(surfaceUv.y - offset.y));
        }

        internal static float WuwaEyeAuthoredHighlightMask(float value)
        {
            const float gate = 0.0400000215f;
            const float rampStart = 0.0803109035f;
            const float rampEnd = 0.9041451216f;
            if (value < gate)
                return 0f;
            return Mathf.Clamp01(
                (value - rampStart) / (rampEnd - rampStart));
        }

        internal static float WuwaEyePupilMask(float hdmfAlpha) =>
            Mathf.Clamp01(1f - hdmfAlpha);

        internal static float WuwaEyeEmissionWeight(float het, float strength) =>
            Mathf.Clamp01(het) * Mathf.Max(strength, 0f);

        internal static Color WuwaEyeHetEmission(
            Color baseColor,
            float het,
            Color hdmf,
            Color scleraColor,
            float scleraStrength,
            Color pupilColor,
            float pupilStrength)
        {
            var region = Color.Lerp(
                scleraColor * Mathf.Max(scleraStrength, 0f),
                pupilColor * Mathf.Max(pupilStrength, 0f),
                WuwaEyePupilMask(hdmf.a));
            var result = baseColor * region * Mathf.Clamp01(het);
            result.a = 0f;
            return result;
        }

        internal static Vector2 WuwaEyeEgLightOffset(
            Vector3 lightDirection,
            Vector3 tangent,
            Vector3 bitangent,
            float follow)
        {
            if (tangent.sqrMagnitude <= 0.000001f ||
                bitangent.sqrMagnitude <= 0.000001f ||
                lightDirection.sqrMagnitude <= 0.000001f)
                return Vector2.zero;
            var light = lightDirection.normalized;
            return new Vector2(
                Vector3.Dot(light, tangent.normalized),
                Vector3.Dot(light, bitangent.normalized)) * follow;
        }

        internal static float WuwaEyeHighlightMask(
            float redChannel,
            Vector2 transformedUv,
            float threshold,
            float softness)
        {
            if (transformedUv.x < 0f || transformedUv.x > 1f ||
                transformedUv.y < 0f || transformedUv.y > 1f)
                return 0f;
            var safeSoftness = Mathf.Max(softness, 0.0001f);
            return SmoothStep(
                threshold - safeSoftness,
                threshold + safeSoftness,
                redChannel);
        }

        internal static float WuwaDirectBrdfSpecular(
            Vector3 normal,
            Vector3 lightDirection,
            Vector3 viewDirection,
            float roughness)
        {
            var safeNormal = SafeNormalize(normal, Vector3.up);
            var safeLight = SafeNormalize(lightDirection, Vector3.forward);
            var safeView = SafeNormalize(viewDirection, Vector3.forward);
            var halfDir = SafeNormalize(safeLight + safeView, Vector3.forward);
            var noH = Mathf.Clamp01(Vector3.Dot(safeNormal, halfDir));
            var loH = Mathf.Clamp01(Vector3.Dot(safeLight, halfDir));
            var noL = Mathf.Clamp01(Vector3.Dot(safeNormal, safeLight));
            var noV = Mathf.Clamp01(Vector3.Dot(safeNormal, safeView));
            var perceptualRoughness = Mathf.Clamp01(roughness);
            var linearRoughness = Mathf.Max(
                perceptualRoughness * perceptualRoughness,
                0.0078125f);
            var roughness2 = Mathf.Max(
                linearRoughness * linearRoughness,
                0.00006103515625f);
            var roughness2MinusOne = roughness2 - 1f;
            var normalizationTerm = linearRoughness * 4f + 2f;
            var d = noH * noH * roughness2MinusOne + 1.00001f;
            var loH2 = loH * loH;
            var specularTerm = roughness2 /
                ((d * d) * Mathf.Max(0.1f, loH2) * normalizationTerm);
            if (!float.IsFinite(specularTerm) || noL <= 0f || noV <= 0f)
                return 0f;
            return specularTerm;
        }

        internal static void WuwaDecodePackedNormalRoughnessMetallic(
            Color sample,
            float normalScale,
            float metallicScale,
            float roughnessScale,
            out Vector3 normalTangentSpace,
            out float metallic,
            out float roughness)
        {
            var normalXY = new Vector2(
                sample.r * 2f - 1f,
                -(sample.g * 2f - 1f)) * Mathf.Max(normalScale, 0f);
            var normalZ = Mathf.Sqrt(Mathf.Clamp01(
                1f - Vector2.Dot(normalXY, normalXY)));
            normalTangentSpace = SafeNormalize(
                new Vector3(normalXY.x, normalXY.y, normalZ),
                Vector3.forward);
            metallic = Mathf.Clamp01(sample.b * Mathf.Max(metallicScale, 0f));
            roughness = Mathf.Clamp01(sample.a * Mathf.Max(roughnessScale, 0f));
        }

        internal static float WuwaMetallicMatcapMask(float metallic) =>
            metallic >= 0.8f ? 1f : 0f;

        internal static Color WuwaFaceShadowTone(
            Color shadowTint,
            Color rampSample,
            float skinRampStrength) => Color.LerpUnclamped(
                shadowTint,
                rampSample,
                Mathf.Clamp01(skinRampStrength));

        internal static Color WuwaMatcapAlbedo(
            Color albedo,
            Color matcap,
            float saturation,
            float mask,
            float strength)
        {
            var gray = new Color(
                matcap.r * 0.3f + matcap.g * 0.59f + matcap.b * 0.11f,
                matcap.r * 0.3f + matcap.g * 0.59f + matcap.b * 0.11f,
                matcap.r * 0.3f + matcap.g * 0.59f + matcap.b * 0.11f,
                matcap.a);
            var tone = Color.Lerp(gray, matcap, Mathf.Clamp01(saturation));
            return albedo + tone * Mathf.Clamp01(mask) * Mathf.Max(0f, strength);
        }

        internal static float WuwaTutorialOutlineWidth(float distance)
        {
            if (!float.IsFinite(distance) || distance < 0f)
                return 1f;
            var nearFar = new Vector3(0.005f, 1f, 10f);
            var weight = new Vector3(0.13f, 0.3f, 1.5f);
            var disNear = Mathf.Clamp01(
                (distance - nearFar.x) / Mathf.Max(nearFar.y - nearFar.x, 0.00001f));
            var disFar = Mathf.Clamp01(
                (distance - nearFar.y) / Mathf.Max(nearFar.z - nearFar.y, 0.00001f));
            return Mathf.Lerp(weight.x, weight.y, disNear) +
                (weight.z - weight.y) * disFar;
        }

        internal static Color WuwaVerticalGradient(
            Color color,
            Color lowColor,
            float gradingValue)
        {
            var amount = Mathf.Clamp01(gradingValue);
            return Color.LerpUnclamped(
                color * new Color(
                    Mathf.Max(0f, lowColor.r),
                    Mathf.Max(0f, lowColor.g),
                    Mathf.Max(0f, lowColor.b),
                    1f),
                color,
                amount);
        }

        internal static Color WuwaApplyVerticalGradient(
            Color color,
            Color lowColor,
            float gradingValue,
            float strength) => Color.LerpUnclamped(
                color,
                WuwaVerticalGradient(color, lowColor, gradingValue),
                Mathf.Clamp01(strength));

        internal static float WuwaHairTutorialSpecular(
            float hmRed,
            float normalDotView,
            float strength) => Mathf.Max(hmRed, 0f) *
            (normalDotView >= 0.3f ? 1f : 0f) * Mathf.Max(strength, 0f);

        internal static float WuwaFaceSdfMask(
            float mainChannel,
            float softChannel,
            float threshold,
            float softness,
            float softStrength)
        {
            var safeSoftness = Mathf.Max(softness, 0.00001f);
            var main = SmoothStep(
                threshold - safeSoftness,
                threshold + safeSoftness,
                mainChannel);
            var soft = SmoothStep(
                threshold - safeSoftness,
                threshold + safeSoftness,
                softChannel);
            return Mathf.Clamp01(main * Mathf.Lerp(
                1f,
                soft,
                Mathf.Clamp01(softStrength)));
        }

        internal static Vector2 WuwaFaceSdfMirroredUv(Vector2 uv) =>
            new Vector2(1f - uv.x, uv.y);

        internal static float WuwaFaceSdfMirrorWeight(
            float sideDot,
            float mirrorBlendWidth)
        {
            var safeWidth = Mathf.Max(mirrorBlendWidth, 0f);
            return safeWidth <= 0.00001f
                ? (sideDot > 0f ? 1f : 0f)
                : SmoothStep(-safeWidth, safeWidth, sideDot);
        }

        internal static float WuwaFaceSdfBlendMasks(
            float unmirroredMask,
            float mirroredMask,
            float sideDot,
            float mirrorBlendWidth) => Mathf.Clamp01(Mathf.Lerp(
                Mathf.Clamp01(unmirroredMask),
                Mathf.Clamp01(mirroredMask),
                WuwaFaceSdfMirrorWeight(sideDot, mirrorBlendWidth)));

        internal static Vector2 WuwaHairShadowUv(
            Vector2 screenUv,
            Vector2 screenOffset) => new Vector2(
            Mathf.Clamp01(screenUv.x + screenOffset.x),
            Mathf.Clamp01(screenUv.y + screenOffset.y));

        internal static float WuwaTutorialScreenRim(
            float centerDepth,
            float upperLeftDepth,
            float upperRightDepth,
            float lowerCenterDepth,
            float threshold,
            float softness)
        {
            var distanceAlpha = Mathf.Clamp01(1f - centerDepth / 80f);
            var delta = Mathf.Max(
                Mathf.Max(
                    upperLeftDepth - centerDepth,
                    upperRightDepth - centerDepth),
                lowerCenterDepth - centerDepth);
            var scaledThreshold = threshold * Mathf.Max(centerDepth, 0.0001f);
            var scaledSoftness = Mathf.Max(
                softness * Mathf.Max(centerDepth, 1f),
                0.00001f);
            return SmoothStep(
                scaledThreshold,
                scaledThreshold + scaledSoftness,
                Mathf.Max(delta, 0f)) * distanceAlpha;
        }

        internal static Color WuwaFresnelStepRim(
            Vector3 normal,
            Vector3 viewDirection,
            float fresnelPower,
            float brightness,
            Color tint,
            Color baseColor)
        {
            var noV = Mathf.Clamp01(Vector3.Dot(
                SafeNormalize(normal, Vector3.up),
                SafeNormalize(viewDirection, Vector3.forward)));
            var fresnel = Mathf.Pow(
                Mathf.Clamp01(1f - noV),
                Mathf.Max(fresnelPower, 0.1f));
            fresnel = fresnel >= 0.5f ? 1f : 0f;
            var positiveTint = new Color(
                Mathf.Max(0f, tint.r),
                Mathf.Max(0f, tint.g),
                Mathf.Max(0f, tint.b),
                1f);
            var positiveBase = new Color(
                Mathf.Max(0f, baseColor.r),
                Mathf.Max(0f, baseColor.g),
                Mathf.Max(0f, baseColor.b),
                1f);
            return positiveTint * positiveBase *
                fresnel * Mathf.Max(0f, brightness);
        }

        internal static float WuwaGradientValue(
            Vector2 uv0,
            Vector2 uv1,
            Vector2 uv2,
            Vector2 uv3,
            float channel,
            float invert)
        {
            var index = Mathf.RoundToInt(channel);
            var selected = uv0;
            if (index == 1)
                selected = uv1;
            else if (index == 2)
                selected = uv2;
            else if (index == 3)
                selected = uv3;
            var value = selected.y;
            if (!float.IsFinite(value))
                value = uv0.y;
            value = Mathf.Clamp01(value);
            return invert > 0.5f ? 1f - value : value;
        }

        internal static float WuwaGradientValue(
            Vector2 uv0,
            Vector2 uv1,
            Vector2 uv3,
            float channel,
            float invert) => WuwaGradientValue(
            uv0,
            uv1,
            uv0,
            uv3,
            channel,
            invert);

        internal static void WuwaFaceBasis(
            Matrix4x4 objectToWorld,
            Vector3 rightObjectSpace,
            Vector3 upObjectSpace,
            Vector3 forwardObjectSpace,
            out Vector3 rightWorldSpace,
            out Vector3 upWorldSpace,
            out Vector3 forwardWorldSpace)
        {
            var defaultRight = SafeNormalize(
                objectToWorld.MultiplyVector(Vector3.right),
                Vector3.right);
            var defaultUp = SafeNormalize(
                objectToWorld.MultiplyVector(Vector3.up),
                Vector3.up);
            var defaultForward = SafeNormalize(
                objectToWorld.MultiplyVector(Vector3.forward),
                Vector3.forward);
            forwardWorldSpace = SafeNormalize(
                objectToWorld.MultiplyVector(forwardObjectSpace),
                defaultForward);
            var rawRight = SafeNormalize(
                objectToWorld.MultiplyVector(rightObjectSpace),
                defaultRight);
            var rawUp = SafeNormalize(
                objectToWorld.MultiplyVector(upObjectSpace),
                defaultUp);
            var projectedRight = rawRight -
                forwardWorldSpace * Vector3.Dot(rawRight, forwardWorldSpace);
            rightWorldSpace = SafeNormalize(projectedRight, defaultRight);
            if (Vector3.Dot(
                Vector3.Cross(forwardWorldSpace, rightWorldSpace),
                rawUp) < 0f)
            {
                rightWorldSpace = -rightWorldSpace;
            }
            upWorldSpace = SafeNormalize(
                Vector3.Cross(forwardWorldSpace, rightWorldSpace),
                defaultUp);
        }

        static float SmoothStep(float from, float to, float value)
        {
            var t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        static Color Clamp01(Color color) => new Color(
            Mathf.Clamp01(color.r),
            Mathf.Clamp01(color.g),
            Mathf.Clamp01(color.b),
            Mathf.Clamp01(color.a));

        static Color Positive(Color color) => new Color(
            Mathf.Max(0f, color.r),
            Mathf.Max(0f, color.g),
            Mathf.Max(0f, color.b),
            Mathf.Max(0f, color.a));

        static Vector3 SafeNormalize(Vector3 value, Vector3 fallback) =>
            value.sqrMagnitude > 0.00000001f
                ? value.normalized
                : fallback;

        static Color Multiply(Color left, Color right) => new Color(
            left.r * right.r,
            left.g * right.g,
            left.b * right.b,
            left.a * right.a);
    }
}
