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

        internal static float FaceSdfLightArticle(
            float margin,
            float phase,
            float offset,
            float softness)
        {
            var x = Mathf.Clamp01(
                -Mathf.Clamp(phase, -1f, 1f) * 0.5f + 0.5f);
            var sdfMin = Mathf.Max(0f, 2f * x - 1f);
            var sdfMax = Mathf.Min(1f, 2f * x);
            var width = sdfMax - sdfMin;
            if (width <= 1e-5f)
                return x < 0.5f ? 1f : 0f;
            width = Mathf.Max(width, Mathf.Max(softness, 1e-4f));
            var t = Mathf.Clamp01((margin + offset - sdfMin) / width);
            return t * t * (3f - 2f * t);
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

        internal static float FaceRimArea(float rimNoV, float rimAreaParam)
        {
            var start = rimAreaParam * -0.6f + 0.8f;
            var end = rimAreaParam * -0.4f + 0.9f;
            var t = Mathf.Clamp01(
                ((1f - Mathf.Clamp01(rimNoV)) - start) /
                Mathf.Max(end - start, 1e-4f));
            return t * t * (3f - 2f * t);
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

        internal static Vector3 FaceSignNormal(Vector3 normal, bool isFrontFace)
        {
            if (normal.sqrMagnitude <= 1e-12f)
                return Vector3.up;
            return normal.normalized * (isFrontFace ? 1f : -1f);
        }

        internal static float BackLightSignal(
            float normalDotLight,
            Vector3 lightDirection,
            Vector3 cameraForward,
            float materialStrength,
            float globalStrength,
            float tutorialWeight)
        {
            var lightXZ = new Vector3(lightDirection.x, 0f, lightDirection.z);
            var cameraXZ = new Vector3(cameraForward.x, 0f, cameraForward.z);
            if (lightXZ.sqrMagnitude <= 1e-12f)
                lightXZ = Vector3.forward;
            if (cameraXZ.sqrMagnitude <= 1e-12f)
                cameraXZ = Vector3.forward;
            lightXZ.Normalize();
            cameraXZ.Normalize();
            var backFacing = Mathf.Clamp01(-Vector3.Dot(lightXZ, cameraXZ));
            var pitchMask = Mathf.Clamp01(0.75f - Mathf.Abs(cameraForward.y));
            pitchMask = pitchMask * pitchMask * (3f - 2f * pitchMask);
            backFacing *= pitchMask;
            var compensation = Mathf.Clamp01(
                    0.5f - 0.5f * normalDotLight * normalDotLight) *
                backFacing * Mathf.Max(materialStrength, 0f) *
                Mathf.Max(globalStrength, 0f);
            return Mathf.Clamp(
                normalDotLight + compensation * Mathf.Clamp01(tutorialWeight),
                -1f,
                1f);
        }

        internal static Color TutorialDirectLight(
            Vector3 normal,
            Color legacyDirect,
            Vector3 topDirection,
            Color topColor,
            float day,
            float normalScale,
            float normalOffset,
            float dayOneTopStrength,
            float dayZeroTopStrength,
            float tutorialWeight,
            float bandMask)
        {
            if (normal.sqrMagnitude <= 1e-12f)
                normal = Vector3.up;
            if (topDirection.sqrMagnitude <= 1e-12f)
                topDirection = Vector3.up;
            normal.Normalize();
            topDirection.Normalize();
            var topNoL = Mathf.Clamp01(
                Vector3.Dot(normal, topDirection) * normalScale + normalOffset);
            var mask = Mathf.Clamp01(bandMask);
            var legacyClamped = new Color(
                Mathf.Max(legacyDirect.r, 0f),
                Mathf.Max(legacyDirect.g, 0f),
                Mathf.Max(legacyDirect.b, 0f),
                legacyDirect.a);
            var legacyStrength = 0.299f * legacyClamped.r +
                0.587f * legacyClamped.g + 0.114f * legacyClamped.b;
            var mainTinted = Color.Lerp(
                new Color(
                    legacyStrength,
                    legacyStrength,
                    legacyStrength,
                    legacyDirect.a),
                legacyClamped,
                mask);
            var topClamped = new Color(
                Mathf.Max(topColor.r, 0f),
                Mathf.Max(topColor.g, 0f),
                Mathf.Max(topColor.b, 0f),
                topColor.a);
            var top = Color.Lerp(topClamped, Color.white, mask) * topNoL;
            var nightColor = top * Mathf.Max(dayZeroTopStrength, 0f);
            var dayColor = mainTinted + top * Mathf.Max(dayOneTopStrength, 0f);
            var tutorial = Color.Lerp(nightColor, dayColor, Mathf.Clamp01(day));
            tutorial.a = legacyDirect.a;
            return Color.Lerp(
                legacyDirect,
                tutorial,
                Mathf.Clamp01(tutorialWeight));
        }

        internal static Vector3 StylizedSpecularLightDirection(
            Vector3 mainLightDirection,
            Vector3 cameraForward,
            float day,
            float cameraForwardBlend,
            float tutorialWeight)
        {
            if (mainLightDirection.sqrMagnitude <= 1e-12f)
                mainLightDirection = Vector3.up;
            if (cameraForward.sqrMagnitude <= 1e-12f)
                cameraForward = Vector3.forward;
            mainLightDirection.Normalize();
            cameraForward.Normalize();
            day = Mathf.Clamp01(day);
            var forwardLight = new Vector3(
                cameraForward.x,
                Mathf.Lerp(0.5f, mainLightDirection.y, day),
                cameraForward.z);
            if (forwardLight.sqrMagnitude <= 1e-12f)
                forwardLight = Vector3.forward;
            forwardLight.Normalize();
            var tutorial = mainLightDirection * day + 2f * forwardLight;
            tutorial = tutorial.sqrMagnitude > 1e-12f
                ? tutorial.normalized
                : forwardLight;
            var result = Vector3.Lerp(
                mainLightDirection,
                tutorial,
                Mathf.Clamp01(cameraForwardBlend) *
                    Mathf.Clamp01(tutorialWeight));
            return result.sqrMagnitude > 1e-12f
                ? result.normalized
                : mainLightDirection;
        }

        internal static Vector3 StylizedSpecularHalfDirection(
            Vector3 normal,
            Vector3 viewDirection,
            Vector3 mainLightDirection,
            Vector3 cameraForward,
            float day,
            float cameraForwardBlend,
            float tutorialWeight)
        {
            if (normal.sqrMagnitude <= 1e-12f)
                normal = Vector3.up;
            if (viewDirection.sqrMagnitude <= 1e-12f)
                viewDirection = normal;
            if (mainLightDirection.sqrMagnitude <= 1e-12f)
                mainLightDirection = normal;
            if (cameraForward.sqrMagnitude <= 1e-12f)
                cameraForward = Vector3.forward;
            normal.Normalize();
            viewDirection.Normalize();
            mainLightDirection.Normalize();
            cameraForward.Normalize();
            day = Mathf.Clamp01(day);
            var legacy = viewDirection + mainLightDirection;
            legacy = legacy.sqrMagnitude > 1e-12f ? legacy.normalized : normal;
            var forwardLight = new Vector3(
                cameraForward.x,
                Mathf.Lerp(0.5f, mainLightDirection.y, day),
                cameraForward.z);
            if (forwardLight.sqrMagnitude <= 1e-12f)
                forwardLight = normal;
            forwardLight.Normalize();
            var tutorialLight = mainLightDirection * day + 2f * forwardLight;
            var tutorial = viewDirection * (2f + day) + tutorialLight;
            tutorial = tutorial.sqrMagnitude > 1e-12f
                ? tutorial.normalized
                : legacy;
            var result = Vector3.Lerp(
                legacy,
                tutorial,
                Mathf.Clamp01(cameraForwardBlend) *
                    Mathf.Clamp01(tutorialWeight));
            return result.sqrMagnitude > 1e-12f ? result.normalized : legacy;
        }

        internal static float TutorialSpecularD(
            float ndotH,
            float roughness2)
        {
            var a2 = roughness2 * roughness2;
            var denominator = (ndotH * a2 - ndotH) * ndotH + 1f;
            return a2 / Mathf.Max(denominator * denominator, 1e-12f);
        }

        internal static float TutorialSpecularDV(
            float ndotH,
            float ndotV,
            float roughness2)
        {
            var distribution = TutorialSpecularD(ndotH, roughness2);
            var visibility = 0.5f / Mathf.Max(
                ndotV * 2f + roughness2 + 9.99999975e-05f,
                1e-5f);
            return Mathf.Clamp(
                distribution * visibility - 6.10351562e-05f,
                0f,
                20f);
        }

        internal static float TutorialSpecularEnvelope(
            float dayZeroWeight,
            float brightWeight,
            float day,
            float minimumAoShadow)
        {
            var lowLight = Mathf.Lerp(
                Mathf.Clamp01(dayZeroWeight),
                Mathf.Clamp01(brightWeight),
                Mathf.Clamp01(day));
            var selfAo = Mathf.Lerp(
                Mathf.Clamp01(minimumAoShadow),
                1f,
                lowLight);
            return selfAo * (lowLight * 0.5f + 0.5f);
        }

        internal static float ThreeLayerLit(
            float day,
            float ao,
            float visibility,
            float rampAlpha,
            float nofRampAlpha,
            float nofStrength,
            float nofPower,
            float dayZeroNofBlend)
        {
            ao = Mathf.Clamp01(ao);
            visibility = Mathf.Clamp01(visibility);
            var nofMask = Mathf.Pow(
                Mathf.Clamp01(nofRampAlpha) * Mathf.Max(nofStrength, 0f),
                Mathf.Max(nofPower, 0.001f));
            var dayZeroMask = Mathf.Lerp(
                1f,
                nofMask,
                Mathf.Clamp01(dayZeroNofBlend));
            var dayZero = Mathf.Clamp01(ao * visibility * dayZeroMask);
            var dayOne = Mathf.Min(
                Mathf.Min(ao, visibility),
                Mathf.Clamp01(rampAlpha));
            return Mathf.Lerp(dayZero, dayOne, Mathf.Clamp01(day));
        }

        internal static Color DarkInDark(Color darkColor, float factor) =>
            new Color(
                darkColor.r * Mathf.Max(factor, 0f),
                darkColor.g * Mathf.Max(factor, 0f),
                darkColor.b * Mathf.Max(factor, 0f),
                darkColor.a);

        internal static Color RampColorEffect(Color ramp, float strength)
        {
            var maximum = Mathf.Max(ramp.r, Mathf.Max(ramp.g, ramp.b));
            var minimum = Mathf.Min(ramp.r, Mathf.Min(ramp.g, ramp.b));
            var chroma = Mathf.Clamp01(maximum - minimum);
            var weight = Mathf.Clamp01(strength);
            return new Color(
                Mathf.Lerp(1f, Mathf.Max(ramp.r, 0f) * chroma + 1f - chroma, weight),
                Mathf.Lerp(1f, Mathf.Max(ramp.g, 0f) * chroma + 1f - chroma, weight),
                Mathf.Lerp(1f, Mathf.Max(ramp.b, 0f) * chroma + 1f - chroma, weight),
                1f);
        }

        internal static float RampColorControl(Color before, Color after)
        {
            var beforeStrength =
                Mathf.Max(before.r, 0f) * 0.212672904f +
                Mathf.Max(before.g, 0f) * 0.715152204f +
                Mathf.Max(before.b, 0f) * 0.0721750036f;
            var afterStrength =
                Mathf.Max(after.r, 0f) * 0.212672904f +
                Mathf.Max(after.g, 0f) * 0.715152204f +
                Mathf.Max(after.b, 0f) * 0.0721750036f;
            return Mathf.Clamp(
                beforeStrength / Mathf.Max(afterStrength, 0.01f),
                0f,
                1.5f);
        }

        internal static Color DfgMultiscatter(
            Color legacyBrdf,
            Color f0,
            float smoothness,
            float normalDotView,
            float tutorialWeight)
        {
            if (!IsFinite(f0) || !IsFinite(smoothness) ||
                !IsFinite(normalDotView) || !IsFinite(tutorialWeight))
                return IsFinite(legacyBrdf) ? legacyBrdf : Color.black;

            var noV = Mathf.Clamp01(normalDotView);
            var roughness = Mathf.Max(1f - Mathf.Clamp01(smoothness), 0.06f);
            var roughness2 = Mathf.Max(roughness * roughness, 0.0078125f);
            var roughness4 = roughness2 * roughness2;
            var roughness6 = roughness4 * roughness2;
            var noV2 = noV * noV;
            var noV3 = noV2 * noV;
            var numerator = 3.32707f * noV + 0.0365463f +
                (-9.04755f * noV + 9.0632f) * roughness2;
            var denominator =
                (3.59685f * noV2 - 1.36772f * noV3 + 1f) +
                (9.22949f * noV3 - 16.3174f * noV2 + 9.04401f) *
                    roughness2 +
                (-20.2123f * noV3 + 19.7886f * noV2 + 5.56589f) *
                    roughness6;
            var dfgScale = numerator / Mathf.Max(Mathf.Abs(denominator), 1e-4f);

            var scaleFitPart1 = -1.28514f * noV + 0.990440011f;
            var scaleFitPart2 = 1.29678f - 0.75591f * noV;
            var biasNumerator = scaleFitPart1 + scaleFitPart2 * roughness2;
            var biasFitX = 2.92338f * noV + 59.4188f * noV3 + 1f;
            var biasFitY = 20.3225f - 27.0302f * noV + 222.592f * noV3;
            var biasFitZ = 626.130f * noV + 316.627f * noV3 + 121.563004f;
            var biasDenominator = biasFitX + biasFitY * roughness2 +
                biasFitZ * roughness6;
            var dfgBias = biasNumerator /
                Mathf.Max(Mathf.Abs(biasDenominator), 1e-4f);
            dfgScale = Mathf.Clamp(dfgScale, 0f, 8f);
            dfgBias = Mathf.Clamp(dfgBias, 0f, 8f);

            var ess = Mathf.Max(Mathf.Clamp01(dfgScale + dfgBias), 1e-4f);
            var ems = 1f - ess;
            var f0Clamped = new Color(
                Mathf.Clamp01(f0.r),
                Mathf.Clamp01(f0.g),
                Mathf.Clamp01(f0.b),
                1f);
            var singleScatter = new Color(
                dfgScale * f0Clamped.r + dfgBias,
                dfgScale * f0Clamped.g + dfgBias,
                dfgScale * f0Clamped.b + dfgBias,
                legacyBrdf.a);
            var multiple = new Color(
                Mathf.Clamp(singleScatter.r *
                    (1f + f0Clamped.r * ems / ess), 0f, 8f),
                Mathf.Clamp(singleScatter.g *
                    (1f + f0Clamped.g * ems / ess), 0f, 8f),
                Mathf.Clamp(singleScatter.b *
                    (1f + f0Clamped.b * ems / ess), 0f, 8f),
                legacyBrdf.a);
            if (!IsFinite(multiple))
                multiple = IsFinite(legacyBrdf) ? legacyBrdf : Color.black;
            return Color.Lerp(
                legacyBrdf,
                multiple,
                Mathf.Clamp01(tutorialWeight));
        }

        internal static Color Emission(
            Color raw,
            Color tint,
            int mode,
            float baseAlpha,
            float intensity)
        {
            var masked = new Color(
                raw.r * tint.r,
                raw.r * tint.g,
                raw.r * tint.b,
                1f);
            var authored = new Color(
                raw.r * tint.r,
                raw.g * tint.g,
                raw.b * tint.b,
                1f);
            var result = mode == 0
                ? masked
                : mode == 1
                    ? authored
                    : authored * Mathf.Clamp01(baseAlpha);
            result.a = 1f;
            return result * Mathf.Max(intensity, 0f);
        }

        internal static float CharacterShadow(
            float rawVisibility,
            float center,
            float smoothness,
            float offset,
            float strength,
            float tutorialWeight)
        {
            rawVisibility = Mathf.Clamp01(rawVisibility);
            var input = Mathf.Clamp(
                (rawVisibility - center) / Mathf.Max(smoothness, 1e-4f),
                -16f,
                16f);
            var sigmoid = 1f / (1f + Mathf.Exp(-input));
            var shaped = Mathf.Clamp01(
                (sigmoid + offset) * Mathf.Max(strength, 0f));
            return Mathf.Lerp(
                rawVisibility,
                shaped,
                Mathf.Clamp01(tutorialWeight));
        }

        internal static float ClothSssArea(
            float normalDotView,
            float baseAlpha,
            float alphaPower)
        {
            var exponent = 1f + Mathf.Clamp01(baseAlpha) *
                Mathf.Max(alphaPower, 0f);
            return Mathf.Pow(
                Mathf.Max(1.05f - Mathf.Clamp01(normalDotView), 1e-4f),
                exponent);
        }

        internal static float FaceSssStrength(
            float headForwardDotCameraForward,
            float refineRed,
            float refineGreen) =>
            Mathf.Lerp(
                Mathf.Clamp01(headForwardDotCameraForward + 0.5f),
                1f,
                Mathf.Clamp01(refineGreen)) * Mathf.Clamp01(refineRed);

        internal static float FaceSssViewEdge(float normalDotView) =>
            1f - (Mathf.Clamp01(normalDotView) * 0.85f + 0.15f);

        internal static Vector3 FaceSdfNormal(
            float sdfBlue,
            bool sampleRight,
            float refineGreen,
            Vector3 meshNormal)
        {
            var rawZ = Mathf.Clamp01(sdfBlue) * 2f - 1f;
            var mirroredZ = sampleRight ? rawZ : -rawZ;
            var sdfDirection = new Vector3(
                mirroredZ,
                6.10351562e-5f,
                1f - Mathf.Abs(mirroredZ));
            sdfDirection = sdfDirection.sqrMagnitude > 1e-12f
                ? sdfDirection.normalized
                : Vector3.forward;
            if (meshNormal.sqrMagnitude <= 1e-12f)
                meshNormal = Vector3.forward;
            return Vector3.Lerp(
                sdfDirection,
                meshNormal.normalized,
                Mathf.Clamp01(refineGreen)).normalized;
        }

        internal static Vector3 EyeFlattenedLightDirection(
            Vector3 lightDirection,
            Vector3 right,
            Vector3 forward)
        {
            lightDirection = lightDirection.sqrMagnitude > 1e-12f
                ? lightDirection.normalized
                : Vector3.forward;
            right = right.sqrMagnitude > 1e-12f
                ? right.normalized
                : Vector3.right;
            forward = forward.sqrMagnitude > 1e-12f
                ? forward.normalized
                : Vector3.forward;
            var xz = new Vector2(
                Vector3.Dot(lightDirection, right),
                Vector3.Dot(lightDirection, forward));
            if (xz.sqrMagnitude <= 1e-12f)
                xz = Vector2.up;
            xz.Normalize();
            var result = right * xz.x + forward * xz.y;
            return result.sqrMagnitude > 1e-12f
                ? result.normalized
                : forward;
        }

        internal static float BodyDiffuseEnergy(
            float metallic,
            float tutorialWeight) =>
            Mathf.Lerp(
                1f - Mathf.Clamp01(metallic),
                0.96f - 0.96f * Mathf.Clamp01(metallic),
                Mathf.Clamp01(tutorialWeight));

        internal static Vector2 RefineF0Uv(
            float ndotH,
            float ndotV,
            float roughness,
            float ao,
            float lerp)
        {
            var roughness2 = Mathf.Max(roughness, 0f) *
                Mathf.Max(roughness, 0f);
            var u = Mathf.Lerp(
                TutorialSpecularD(ndotH, roughness2) * roughness2,
                ndotV * ndotV,
                Mathf.Clamp01(lerp));
            var v = roughness * (1f - Mathf.Clamp01(ao));
            return new Vector2(
                Mathf.Clamp01(u),
                Mathf.Clamp01(1f - v));
        }

        internal static Vector2 HairLutUv(
            float tangentDotHalf,
            Vector2 viewProjection,
            Vector2 normalProjection,
            float vPower)
        {
            var u = Mathf.Clamp01(
                1f - tangentDotHalf * tangentDotHalf);
            var horizontal = Mathf.Clamp01(Vector2.Dot(
                viewProjection,
                normalProjection));
            horizontal = Mathf.Pow(horizontal, Mathf.Max(vPower, 1e-4f));
            var directionMask = tangentDotHalf >= 0f ? 1f : 0f;
            return new Vector2(u, horizontal * horizontal * directionMask);
        }

        internal static Color HairFinalF0(
            Color lut,
            Color baseF0,
            Color backF0,
            float materialSmoothness,
            float tangentDotHalf,
            float backPower)
        {
            var sine = Mathf.Sqrt(Mathf.Clamp01(
                1f - tangentDotHalf * tangentDotHalf));
            var halo = Mathf.Pow(sine, Mathf.Max(backPower, 1f)) *
                Mathf.Clamp01(materialSmoothness);
            return new Color(
                lut.r * Mathf.Max(baseF0.r, 0f) * 7f +
                    Mathf.Max(backF0.r, 0f) * halo,
                lut.g * Mathf.Max(baseF0.g, 0f) * 7f +
                    Mathf.Max(backF0.g, 0f) * halo,
                lut.b * Mathf.Max(baseF0.b, 0f) * 7f +
                    Mathf.Max(backF0.b, 0f) * halo,
                1f);
        }

        internal static Vector3 TutorialCorneaNormal(
            Vector2 uv,
            float bumpStrength)
        {
            var fractionalUv = new Vector2(
                uv.x - Mathf.Floor(uv.x),
                uv.y - Mathf.Floor(uv.y));
            var areaUv = fractionalUv - new Vector2(0.5f, 0.5f);
            var outsideCenter = areaUv.sqrMagnitude >= 0.25f;
            var centered = areaUv * 2f;
            var z = Mathf.Max(
                Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Min(1f, centered.sqrMagnitude))),
                1e-16f);
            var value = outsideCenter
                ? Vector3.forward
                : new Vector3(
                    -0.125f * bumpStrength * centered.x,
                    0.125f * bumpStrength * centered.y,
                    z);
            return value.sqrMagnitude > 1e-12f
                ? value.normalized
                : Vector3.forward;
        }

        internal static Color EyeBrightTrick(
            Color baseColor,
            Color centerColor,
            Color alphaColor,
            float centerArea,
            float baseAlpha)
        {
            var center = Color.Lerp(
                Color.white,
                centerColor * 2.5f,
                Mathf.Clamp01(centerArea));
            var alpha = Color.Lerp(
                Color.white,
                alphaColor * 2.5f,
                Mathf.Clamp01(baseAlpha));
            var result = new Color(
                baseColor.r * center.r * alpha.r,
                baseColor.g * center.g * alpha.g,
                baseColor.b * center.b * alpha.b,
                baseColor.a);
            return result;
        }

        internal static Color EyeMatcapBrdf(
            Color matcap,
            float specularStrength,
            Color alphaColor,
            float alphaStrength) =>
            new Color(
                matcap.r * Mathf.Max(specularStrength, 0f) +
                    Mathf.Max(alphaColor.r, 0f) * matcap.a *
                    Mathf.Max(alphaStrength, 0f),
                matcap.g * Mathf.Max(specularStrength, 0f) +
                    Mathf.Max(alphaColor.g, 0f) * matcap.a *
                    Mathf.Max(alphaStrength, 0f),
                matcap.b * Mathf.Max(specularStrength, 0f) +
                    Mathf.Max(alphaColor.b, 0f) * matcap.a *
                    Mathf.Max(alphaStrength, 0f),
                1f);

        static float SmoothStep(float from, float to, float value)
        {
            var width = Mathf.Max(to - from, 1e-6f);
            var t = Mathf.Clamp01((value - from) / width);
            return t * t * (3f - 2f * t);
        }

        static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        static bool IsFinite(Color value) =>
            IsFinite(value.r) && IsFinite(value.g) &&
            IsFinite(value.b) && IsFinite(value.a);
    }
}
