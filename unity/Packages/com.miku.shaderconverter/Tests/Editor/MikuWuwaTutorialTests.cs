// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using Miku.ShaderConverter.Runtime.GameToon;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Miku.ShaderConverter.Editor.Tests
{
    /// <summary>
    /// Tutorial-compliance coverage for the Wuwa 3.0 renderer foundation.
    /// </summary>
    public sealed class MikuWuwaTutorialTests
    {
        const string TestFolder = "Assets/MikuWuwaTutorialTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void WuwaDirectBrdfSpecularMatchesTutorialFormula()
        {
            var normal = Vector3.up;
            var light = new Vector3(0f, 1f, 0f);
            var view = new Vector3(0f, 1f, 0f);
            var rough = MikuGameToonCpuMath.WuwaDirectBrdfSpecular(
                normal,
                light,
                view,
                0.6f);
            Assert.That(float.IsFinite(rough), Is.True);
            var perceptualRoughness = 0.6f;
            var linearRoughness = perceptualRoughness * perceptualRoughness;
            var roughness2 = linearRoughness * linearRoughness;
            var d = roughness2 + 0.00001f;
            var expected = roughness2 /
                (d * d * (linearRoughness * 4f + 2f));
            Assert.That(rough, Is.EqualTo(expected).Within(0.0001f));

            var roughSmooth = MikuGameToonCpuMath.WuwaDirectBrdfSpecular(
                normal,
                light,
                view,
                0.1f);
            var roughRough = MikuGameToonCpuMath.WuwaDirectBrdfSpecular(
                normal,
                light,
                view,
                0.9f);
            Assert.That(roughSmooth, Is.GreaterThan(roughRough));

            var backlit = MikuGameToonCpuMath.WuwaDirectBrdfSpecular(
                normal,
                Vector3.down,
                view,
                0.5f);
            Assert.That(backlit, Is.Zero);
        }

        [Test]
        public void WuwaMatcapAlbedoKeepsTenPercentSaturationByDefault()
        {
            var saturated = new Color(1f, 0f, 0f, 1f);
            var result = MikuGameToonCpuMath.WuwaMatcapAlbedo(
                Color.gray,
                saturated,
                0.1f,
                1f,
                0.15f);
            var gray = 1f * 0.3f + 0f * 0.59f + 0f * 0.11f;
            var toneR = Mathf.Lerp(gray, 1f, 0.1f);
            var toneG = Mathf.Lerp(gray, 0f, 0.1f);
            Assert.That(result.r, Is.EqualTo(0.5f + toneR * 0.15f)
                .Within(0.0001f));
            Assert.That(result.g, Is.EqualTo(0.5f + toneG * 0.15f)
                .Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaMatcapAlbedo(
                    Color.gray,
                    saturated,
                    0.1f,
                    0f,
                    1f),
                Is.EqualTo(Color.gray));
            Assert.That(
                MikuGameToonCpuMath.WuwaMetallicMatcapMask(0.79f),
                Is.Zero);
            Assert.That(
                MikuGameToonCpuMath.WuwaMetallicMatcapMask(0.8f),
                Is.EqualTo(1f));
        }

        [Test]
        public void WuwaPackedNrmPreservesAllAuthoredChannels()
        {
            MikuGameToonCpuMath.WuwaDecodePackedNormalRoughnessMetallic(
                new Color(0.75f, 0.25f, 0.9f, 0.35f),
                1f,
                1f,
                1f,
                out var normal,
                out var metallic,
                out var roughness);
            Assert.That(normal.x, Is.GreaterThan(0f));
            Assert.That(normal.y, Is.GreaterThan(0f));
            Assert.That(normal.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(metallic, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(roughness, Is.EqualTo(0.35f).Within(0.0001f));
        }

        [Test]
        public void WuwaFaceSkinRampFallsBackToAuthoredShadowTint()
        {
            var shadow = new Color(0.4f, 0.3f, 0.2f, 1f);
            var ramp = new Color(0.8f, 0.7f, 0.6f, 1f);
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceShadowTone(shadow, ramp, 0f),
                Is.EqualTo(shadow));
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceShadowTone(shadow, ramp, 1f),
                Is.EqualTo(ramp));
        }

        [Test]
        public void WuwaTutorialOutlineWidthMatchesEmpiricalFormula()
        {
            Assert.That(
                MikuGameToonCpuMath.WuwaTutorialOutlineWidth(0f),
                Is.EqualTo(0.13f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaTutorialOutlineWidth(1f),
                Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaTutorialOutlineWidth(5f),
                Is.EqualTo(0.3f + 1.2f * 4f / 9f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaTutorialOutlineWidth(20f),
                Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void WuwaVerticalGradientAndFresnelRimMatchTutorial()
        {
            var color = new Color(0.8f, 0.7f, 0.6f, 1f);
            var low = new Color(0.86f, 0.80f, 0.94f, 1f);
            Assert.That(
                MikuGameToonCpuMath.WuwaVerticalGradient(color, low, 1f),
                Is.EqualTo(color));
            Assert.That(
                MikuGameToonCpuMath.WuwaVerticalGradient(color, low, 0f),
                Is.EqualTo(new Color(
                    color.r * low.r,
                    color.g * low.g,
                    color.b * low.b,
                    color.a)));
            Assert.That(
                MikuGameToonCpuMath.WuwaApplyVerticalGradient(
                    color,
                    low,
                    0f,
                    0f),
                Is.EqualTo(color));

            var noRim = MikuGameToonCpuMath.WuwaFresnelStepRim(
                Vector3.up,
                Vector3.up,
                2f,
                0.5f,
                Color.white,
                color);
            Assert.That(noRim.r, Is.Zero);
            var fullRim = MikuGameToonCpuMath.WuwaFresnelStepRim(
                Vector3.up,
                Vector3.down,
                1f,
                0.5f,
                Color.white,
                color);
            Assert.That(fullRim.r, Is.EqualTo(color.r * 0.5f).Within(0.0001f));

            var halfAngle = new Vector3(
                Mathf.Sqrt(0.75f),
                0.5f,
                0f);
            var narrowRim = MikuGameToonCpuMath.WuwaFresnelStepRim(
                Vector3.up,
                halfAngle,
                2f,
                0.5f,
                Color.white,
                color);
            var wideRim = MikuGameToonCpuMath.WuwaFresnelStepRim(
                Vector3.up,
                halfAngle,
                1f,
                0.5f,
                Color.white,
                color);
            Assert.That(narrowRim.r, Is.Zero);
            Assert.That(
                wideRim.r,
                Is.EqualTo(color.r * 0.5f).Within(0.0001f));
        }

        [Test]
        public void WuwaGradientValueSelectsChannelAndInvert()
        {
            Assert.That(
                MikuGameToonCpuMath.WuwaGradientValue(
                    new Vector2(0f, 0.2f),
                    new Vector2(0f, 0.4f),
                    new Vector2(0f, 0.6f),
                    new Vector2(0f, 0.9f),
                    3f,
                    0f),
                Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaGradientValue(
                    new Vector2(0f, 0.2f),
                    new Vector2(0f, 0.4f),
                    new Vector2(0f, 0.6f),
                    new Vector2(0f, 0.9f),
                    3f,
                    1f),
                Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaGradientValue(
                    new Vector2(0f, 0.2f),
                    new Vector2(0f, 0.4f),
                    new Vector2(0f, 0.6f),
                    new Vector2(0f, 0.9f),
                    1f,
                    0f),
                Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaGradientValue(
                    new Vector2(0f, 0.2f),
                    new Vector2(0f, 0.4f),
                    new Vector2(0f, 0.6f),
                    new Vector2(0f, 0.9f),
                    2f,
                    0f),
                Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void WuwaFaceHairShadowHairSpecAndDepthRimMatchTutorial()
        {
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfMask(
                    0.6f,
                    0.55f,
                    0.5f,
                    0.1f,
                    1f),
                Is.GreaterThan(0.5f));
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfMask(
                    0.4f,
                    1f,
                    0.5f,
                    0.1f,
                    1f),
                Is.Zero);
            Assert.That(
                MikuGameToonCpuMath.WuwaHairTutorialSpecular(
                    0.8f,
                    0.3f,
                    0.5f),
                Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaHairShadowUv(
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.02f, -0.03f)),
                Is.EqualTo(new Vector2(0.52f, 0.47f)));
            var near = MikuGameToonCpuMath.WuwaTutorialScreenRim(
                10f, 10f, 12f, 10f, 0.03f, 0.02f);
            var far = MikuGameToonCpuMath.WuwaTutorialScreenRim(
                80f, 80f, 100f, 80f, 0.03f, 0.02f);
            Assert.That(near, Is.GreaterThan(0f));
            Assert.That(far, Is.Zero);
        }

        [Test]
        public void WuwaFaceSdfThresholdAndMirrorTransitionsAreContinuous()
        {
            var previous = 0f;
            var intermediateCount = 0;
            var maximumDelta = 0f;
            for (var index = 0; index <= 100; index++)
            {
                var mainValue = index / 100f;
                var value = MikuGameToonCpuMath.WuwaFaceSdfMask(
                    mainValue,
                    1f,
                    0.5f,
                    0.1f,
                    0f);
                Assert.That(float.IsFinite(value), Is.True);
                Assert.That(value, Is.InRange(0f, 1f));
                Assert.That(value + 0.000001f, Is.GreaterThanOrEqualTo(previous));
                maximumDelta = Mathf.Max(maximumDelta, value - previous);
                if (value > 0.001f && value < 0.999f)
                    intermediateCount++;
                previous = value;
            }

            Assert.That(intermediateCount, Is.GreaterThan(5));
            Assert.That(maximumDelta, Is.LessThan(0.1f));

            previous = 0f;
            intermediateCount = 0;
            maximumDelta = 0f;
            for (var index = 0; index <= 100; index++)
            {
                var softValue = index / 100f;
                var value = MikuGameToonCpuMath.WuwaFaceSdfMask(
                    1f,
                    softValue,
                    0.5f,
                    0.1f,
                    1f);
                Assert.That(value + 0.000001f, Is.GreaterThanOrEqualTo(previous));
                maximumDelta = Mathf.Max(maximumDelta, value - previous);
                if (value > 0.001f && value < 0.999f)
                    intermediateCount++;
                previous = value;
            }
            Assert.That(intermediateCount, Is.GreaterThan(5));
            Assert.That(maximumDelta, Is.LessThan(0.1f));

            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfMask(
                    0.5f, 1f, 0.5f, 0.1f, 0f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfMask(
                    0.39f, 1f, 0.5f, 0.1f, 1f),
                Is.Zero,
                "The B refinement channel cannot light a region rejected by A.");
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfMask(
                    0.6f, 0f, 0.5f, 0.1f, 0f),
                Is.EqualTo(1f).Within(0.0001f),
                "Strength zero must ignore B.");
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfMask(
                    0.6f, 0.5f, 0.5f, 0.1f, 1f),
                Is.EqualTo(0.5f).Within(0.0001f),
                "Strength one must apply B as a refinement.");

            foreach (var softness in new[] { 0f, -1f })
            {
                var value = MikuGameToonCpuMath.WuwaFaceSdfMask(
                    0.5f, 0.5f, 0.5f, softness, 1f);
                Assert.That(float.IsFinite(value), Is.True, softness.ToString());
                Assert.That(value, Is.InRange(0f, 1f));
            }

            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfMirroredUv(
                    new Vector2(0.2f, 0.7f)),
                Is.EqualTo(new Vector2(0.8f, 0.7f)));
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfMirrorWeight(-0.1f, 0.1f),
                Is.Zero);
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfMirrorWeight(0f, 0.1f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfMirrorWeight(0.1f, 0.1f),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfMirrorWeight(0f, 0f),
                Is.Zero,
                "Zero width selects the unmirrored side on the centre line.");
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfMirrorWeight(0.001f, 0f),
                Is.EqualTo(1f));
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfBlendMasks(
                    0.2f, 0.8f, -0.2f, 0.1f),
                Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaFaceSdfBlendMasks(
                    0.2f, 0.8f, 0.2f, 0.1f),
                Is.EqualTo(0.8f).Within(0.0001f),
                "Positive sideDot must select the explicit 1-u sample.");

            previous = 0.2f;
            maximumDelta = 0f;
            for (var index = -10; index <= 10; index++)
            {
                var sideDot = index / 100f;
                var value = MikuGameToonCpuMath.WuwaFaceSdfBlendMasks(
                    0.2f,
                    0.8f,
                    sideDot,
                    0.1f);
                Assert.That(value + 0.000001f, Is.GreaterThanOrEqualTo(previous));
                maximumDelta = Mathf.Max(maximumDelta, value - previous);
                previous = value;
            }
            Assert.That(maximumDelta, Is.LessThan(0.06f));
        }

        [Test]
        public void WuwaEyeParallaxUsesTangentSpaceAndHasExactFlatFallback()
        {
            var surfaceUv = new Vector2(0.5f, 0.5f);
            var tangentView = MikuGameToonCpuMath.WuwaEyeParallaxUv(
                surfaceUv,
                Vector3.right * 4f,
                Vector3.right,
                Vector3.up,
                0.02f,
                1f);
            Assert.That(tangentView.x, Is.EqualTo(0.48f).Within(0.0001f));
            Assert.That(tangentView.y, Is.EqualTo(0.5f).Within(0.0001f));

            var bitangentView = MikuGameToonCpuMath.WuwaEyeParallaxUv(
                surfaceUv,
                Vector3.up,
                Vector3.right,
                Vector3.up,
                0.02f,
                1f);
            Assert.That(bitangentView.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(bitangentView.y, Is.EqualTo(0.52f).Within(0.0001f));

            var rotatedBasis = MikuGameToonCpuMath.WuwaEyeParallaxUv(
                surfaceUv,
                Vector3.forward,
                Vector3.forward,
                Vector3.up,
                0.02f,
                1f);
            Assert.That(rotatedBasis, Is.EqualTo(tangentView));
            Assert.That(
                MikuGameToonCpuMath.WuwaEyeParallaxUv(
                    surfaceUv,
                    Vector3.right,
                    Vector3.right,
                    Vector3.up,
                    0.02f,
                    0f),
                Is.EqualTo(surfaceUv));
            Assert.That(
                MikuGameToonCpuMath.WuwaEyeParallaxUv(
                    surfaceUv,
                    Vector3.right,
                    Vector3.zero,
                    Vector3.up,
                    0.02f,
                    1f),
                Is.EqualTo(surfaceUv));
        }

        [Test]
        public void WuwaShadersExposeTutorialPropertiesWithDefaults()
        {
            var body = new Material(Shader.Find("MIKU/Wuwa/Body"));
            var hair = new Material(Shader.Find("MIKU/Wuwa/Hair"));
            var face = new Material(Shader.Find("MIKU/Wuwa/Face"));
            var eye = new Material(Shader.Find("MIKU/Wuwa/Eye"));
            try
            {
                foreach (var property in new[]
                {
                    "_Roughness", "_SpecularColor", "_SpecularStrength",
                    "_ReflectionStrength", "_VerticalGradientColor",
                    "_VerticalGradientStrength", "_GradientUVIndex",
                    "_GradientInvert", "_OutlineDistanceMode",
                    "_OutlineVertexColorMask",
                    "_NormalMapEncoding", "_PackedMetallicScale",
                    "_PackedRoughnessScale", "_OcclusionStrength",
                    "_OutlineColorMap",
                })
                    Assert.That(body.HasProperty(property), Is.True, property);
                Assert.That(body.GetFloat("_MatcapSaturation"), Is.EqualTo(0.1f));
                Assert.That(body.GetFloat("_Roughness"), Is.EqualTo(0.6f));
                Assert.That(body.GetFloat("_GradientUVIndex"), Is.EqualTo(3f));
                Assert.That(body.GetFloat("_OutlineDistanceMode"), Is.EqualTo(1f));
                Assert.That(body.GetFloat("_OutlineVertexColorMask"), Is.EqualTo(1f));

                foreach (var material in new[] { hair, face, eye })
                    foreach (var property in new[]
                    {
                        "_Roughness", "_SpecularColor", "_SpecularStrength",
                        "_ReflectionStrength",
                    })
                        Assert.That(material.HasProperty(property), Is.True,
                            material.shader.name + ":" + property);
                Assert.That(hair.HasProperty("_AlphaClip"), Is.True);
                Assert.That(hair.HasProperty("_Cutoff"), Is.True);
                Assert.That(hair.GetFloat("_OutlineDistanceMode"), Is.EqualTo(1f));

                Assert.That(face.GetFloat("_FaceSoftChannelStrength"), Is.EqualTo(1f));
                Assert.That(face.HasProperty("_FaceSdfMirrorBlendWidth"), Is.True);
                Assert.That(
                    face.GetFloat("_FaceSdfMirrorBlendWidth"),
                    Is.EqualTo(0.10f).Within(0.0001f));
                Assert.That(face.GetFloat("_UseHairShadow"), Is.EqualTo(1f));
                Assert.That(face.GetFloat("_VerticalGradientStrength"), Is.Zero);
                Assert.That(face.HasProperty("_FaceShadowOffsetX"), Is.True);
                Assert.That(face.HasProperty("_FaceShadowOffsetY"), Is.True);

                Assert.That(eye.GetFloat("_EyeShadowStart"), Is.EqualTo(0.25f));
                Assert.That(eye.GetFloat("_EyeShadowEnd"), Is.EqualTo(0.55f));
                Assert.That(eye.GetFloat("_EyeParallaxStrength"), Is.Zero);
                Assert.That(eye.GetColor("_EyeShadowTint"), Is.EqualTo(
                    new Color(0.82f, 0.82f, 0.82f, 1f)));
            }
            finally
            {
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(hair);
                Object.DestroyImmediate(face);
                Object.DestroyImmediate(eye);
            }
        }

        [Test]
        public void WuwaRecommendedProfileWritesTutorialDefaults()
        {
            var body = new Material(Shader.Find("MIKU/Wuwa/Body"));
            var face = new Material(Shader.Find("MIKU/Wuwa/Face"));
            var eye = new Material(Shader.Find("MIKU/Wuwa/Eye"));
            var hair = new Material(Shader.Find("MIKU/Wuwa/Hair"));
            var hdmf = new Texture2D(1, 1);
            try
            {
                body.SetFloat("_MatcapSaturation", 0.8f);
                body.SetFloat("_VerticalGradientStrength", 0f);
                face.SetFloat("_FaceSoftChannelStrength", 0f);
                face.SetFloat("_UseHairShadow", 0f);
                face.SetFloat("_MainShadowInfluence", 1f);
                face.SetFloat("_FaceSdfMirrorBlendWidth", 0f);
                eye.SetColor("_EyeLitTint", Color.gray);
                eye.SetFloat("_EyeParallaxStrength", 0.08f);
                hair.SetFloat("_VerticalGradientStrength", 0f);
                Assert.That(MikuGameToonMaterialProfiles.ApplyRecommended(
                    body,
                    false), Is.True);
                Assert.That(MikuGameToonMaterialProfiles.ApplyRecommended(
                    face,
                    false), Is.True);
                Assert.That(MikuGameToonMaterialProfiles.ApplyRecommended(
                    eye,
                    false), Is.True);
                Assert.That(MikuGameToonMaterialProfiles.ApplyRecommended(
                    hair,
                    false), Is.True);

                Assert.That(body.GetFloat("_MatcapSaturation"), Is.EqualTo(0.1f));
                Assert.That(body.GetFloat("_VerticalGradientStrength"), Is.EqualTo(1f));
                Assert.That(face.GetFloat("_FaceSoftChannelStrength"), Is.EqualTo(1f));
                Assert.That(face.GetFloat("_UseHairShadow"), Is.EqualTo(1f));
                Assert.That(face.GetFloat("_MainShadowInfluence"), Is.Zero);
                Assert.That(
                    face.GetFloat("_FaceSdfMirrorBlendWidth"),
                    Is.EqualTo(0.10f).Within(0.0001f));
                Assert.That(eye.GetColor("_EyeLitTint"), Is.EqualTo(Color.white));
                Assert.That(
                    eye.GetFloat("_EyeParallaxStrength"),
                    Is.Zero,
                    "HET-only/sclera materials must not receive iris parallax.");

                eye.SetTexture("_EyeHDMF", hdmf);
                Assert.That(MikuGameToonMaterialProfiles.ApplyRecommended(
                    eye,
                    false), Is.True);
                Assert.That(
                    eye.GetFloat("_EyeParallaxStrength"),
                    Is.EqualTo(0.02f).Within(0.0001f));
                Assert.That(hair.GetFloat("_VerticalGradientStrength"), Is.EqualTo(1f));
                Assert.That(hair.GetFloat("_HairLitMaskStrength"), Is.Zero);
                Assert.That(hair.GetFloat("_HairSpecOffsetStrength"), Is.Zero);

                Assert.That(MikuGameToonMaterialProfiles.ApplyRecommended(
                    body,
                    false), Is.False);
                Assert.That(MikuGameToonMaterialProfiles.ApplyRecommended(
                    face,
                    false), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(face);
                Object.DestroyImmediate(eye);
                Object.DestroyImmediate(hair);
                Object.DestroyImmediate(hdmf);
            }
        }

        [Test]
        public void WuwaHairShadowInstallerIsIdempotent()
        {
            AssetDatabase.CreateFolder("Assets", "MikuWuwaTutorialTests");
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = "Renderer";
            AssetDatabase.CreateAsset(
                renderer,
                TestFolder + "/Renderer.asset");
            try
            {
                var first = MikuWuwaRendererFeatureInstaller.Install(renderer);
                Assert.That(first.created, Is.True);
                Assert.That(first.screenRimFeature, Is.Not.Null);
                Assert.That(first.screenRimCreated, Is.True);
                Assert.That(first.geometryFeature, Is.Not.Null);
                Assert.That(first.geometryCreated, Is.True);
                Assert.That(
                    first.screenRimFeature.settings.algorithm,
                    Is.EqualTo(MikuToonScreenRimRendererFeature.RimAlgorithm
                        .WuwaTutorial));
                Assert.That(
                    MikuWuwaRendererFeatureInstaller.CountFeatures(renderer),
                    Is.EqualTo(1));
                Assert.That(
                    MikuToonRendererFeatureInstaller.CountGeometryFeatures(
                        renderer),
                    Is.EqualTo(1));
                Assert.That(
                    MikuToonRendererFeatureInstaller.CountFeatures(renderer),
                    Is.EqualTo(1));

                var second = MikuWuwaRendererFeatureInstaller.Install(renderer);
                Assert.That(second.created, Is.False);
                Assert.That(second.screenRimCreated, Is.False);
                Assert.That(second.geometryCreated, Is.False);
                Assert.That(second.feature, Is.SameAs(first.feature));
                Assert.That(
                    second.screenRimFeature,
                    Is.SameAs(first.screenRimFeature));
                Assert.That(
                    second.geometryFeature,
                    Is.SameAs(first.geometryFeature));
                Assert.That(
                    MikuWuwaRendererFeatureInstaller.CountFeatures(renderer),
                    Is.EqualTo(1));
                Assert.That(
                    MikuToonRendererFeatureInstaller.CountFeatures(renderer),
                    Is.EqualTo(1));
                Assert.That(
                    MikuToonRendererFeatureInstaller.CountGeometryFeatures(
                        renderer),
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(renderer, true);
            }
        }

        [Test]
        public void WuwaHairShadowInstallerRollsBackBeforeCommitFailure()
        {
            AssetDatabase.CreateFolder("Assets", "MikuWuwaTutorialTests");
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, TestFolder + "/Rollback.asset");
            try
            {
                Assert.Throws<System.InvalidOperationException>(() =>
                    MikuWuwaRendererFeatureInstaller.Install(
                        renderer,
                        () => throw new System.InvalidOperationException(
                            "expected")));
                Assert.That(
                    MikuWuwaRendererFeatureInstaller.CountFeatures(renderer),
                    Is.Zero);
                Assert.That(
                    MikuToonRendererFeatureInstaller.CountFeatures(renderer),
                    Is.Zero);
                Assert.That(
                    MikuToonRendererFeatureInstaller.CountGeometryFeatures(
                        renderer),
                    Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(renderer, true);
            }
        }

        [Test]
        public void WuwaOutlinePassUsesVertexColorMaskAndTutorialDistance()
        {
            foreach (var shaderName in new[]
            {
                "MIKU/Wuwa/Body",
                "MIKU/Wuwa/Hair",
                "MIKU/Wuwa/Face",
            })
            {
                var source = File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "../Packages/com.miku.shaderconverter/Runtime/Wuwa/" +
                    "Wuwa_" +
                    shaderName.Substring("MIKU/Wuwa/".Length) + ".shader"));
                StringAssert.Contains(
                    "MikuGameToonOutlinePositionCSWithDistanceMultiplier",
                    source);
                StringAssert.Contains("_OutlineVertexColorMask", source);
                StringAssert.Contains("float4 color : COLOR", source);
                StringAssert.Contains("Wuwa_TutorialOutlineWidth", source);
                StringAssert.Contains(
                    "Tags { \"LightMode\"=\"MikuToonOutline\" }",
                    source);
            }
        }

        [Test]
        public void WuwaRimPropertiesDistinguishFresnelAndScreenControls()
        {
            foreach (var part in new[] { "Body", "Hair", "Face" })
            {
                var source = File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "../Packages/com.miku.shaderconverter/Runtime/Wuwa/" +
                    "Wuwa_" + part + ".shader"));
                StringAssert.Contains(
                    "_RimLightBrightness (\"Rim Brightness (Fresnel + Screen)\"",
                    source,
                    part);
                StringAssert.Contains(
                    "_RimLightTintColor (\"Rim Tint (Fresnel + Screen)\"",
                    source,
                    part);
                StringAssert.Contains(
                    "_RimLightWidth (\"Screen Rim Radius (Pixels)\"",
                    source,
                    part);
                StringAssert.Contains(
                    "_RimLightThreshold (\"Screen Rim Depth Threshold\"",
                    source,
                    part);
                StringAssert.Contains(
                    "_RimLightFadeout (\"Screen Rim Softness\"",
                    source,
                    part);
                StringAssert.Contains(
                    "_FresnelPower (\"Fresnel Rim Power\"",
                    source,
                    part);
                StringAssert.DoesNotContain(
                    "[HideInInspector] _FresnelPower",
                    source,
                    part);
                StringAssert.Contains(
                    "[HideInInspector] _FresnelClamp",
                    source,
                    part);
            }
        }

        [Test]
        public void WuwaForwardPassesDeclareTheUrpClusterMainLightVariant()
        {
            foreach (var part in new[] { "Body", "Hair", "Face", "Eye" })
            {
                var source = File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "../Packages/com.miku.shaderconverter/Runtime/Wuwa/" +
                    "Wuwa_" + part + ".shader"));
                var passStart = source.IndexOf(
                    "Name \"UniversalForward\"",
                    StringComparison.Ordinal);
                Assert.That(passStart, Is.GreaterThanOrEqualTo(0), part);
                var programStart = source.IndexOf(
                    "HLSLPROGRAM",
                    passStart,
                    StringComparison.Ordinal);
                Assert.That(programStart, Is.GreaterThan(passStart), part);
                var programEnd = source.IndexOf(
                    "ENDHLSL",
                    programStart,
                    StringComparison.Ordinal);
                Assert.That(programEnd, Is.GreaterThan(programStart), part);
                var forwardProgram = source.Substring(
                    programStart,
                    programEnd - programStart);
                StringAssert.Contains(
                    "#pragma multi_compile _ _CLUSTER_LIGHT_LOOP",
                    forwardProgram,
                    part + " must not read the Forward per-object " +
                    "unity_LightData attenuation while URP renders Forward+.");
                StringAssert.Contains(
                    "#if UNITY_VERSION >= 60010000",
                    forwardProgram,
                    part + " must select the keyword used by its Unity line.");
                StringAssert.Contains(
                    "#pragma multi_compile _ _FORWARD_PLUS",
                    forwardProgram,
                    part + " must retain Unity 6000.0 / URP 17.0 Forward+.");
                StringAssert.Contains(
                    "#pragma multi_compile _ _MAIN_LIGHT_SHADOWS " +
                    "_MAIN_LIGHT_SHADOWS_CASCADE " +
                    "_MAIN_LIGHT_SHADOWS_SCREEN",
                    forwardProgram,
                    part);
                StringAssert.Contains(
                    "#pragma multi_compile_fragment _ _SHADOWS_SOFT " +
                    "_SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM " +
                    "_SHADOWS_SOFT_HIGH",
                    forwardProgram,
                    part);
                StringAssert.Contains(
                    "output.shadowCoord = GetShadowCoord(pos);",
                    forwardProgram,
                    part);
                StringAssert.DoesNotContain(
                    "TransformWorldToShadowCoord(pos.positionWS)",
                    forwardProgram,
                    part);
            }
        }

        [Test]
        public void WuwaFaceDiagnosticsReportDisabledSdfWithoutChangingMaterial()
        {
            var shader = Shader.Find("MIKU/Wuwa/Face");
            Assert.That(shader, Is.Not.Null);
            var face = new Material(shader);
            try
            {
                var originalStrength = face.GetFloat("_FaceShadowStrength");
                var originalSerializedMaterial = EditorJsonUtility.ToJson(face);
                CollectionAssert.Contains(
                    MikuWuwaFaceMaterialDiagnostics.Validate(face),
                    MikuWuwaFaceMaterialDiagnostics.SdfRequired);
                Assert.That(
                    face.GetFloat("_FaceShadowStrength"),
                    Is.EqualTo(originalStrength),
                    "Validation must be read-only.");
                Assert.That(
                    EditorJsonUtility.ToJson(face),
                    Is.EqualTo(originalSerializedMaterial),
                    "Validation must preserve every serialized material value.");

                AssetDatabase.CreateFolder("Assets", "MikuWuwaTutorialTests");
                var authoredSdf = new Texture2D(1, 1)
                {
                    name = "AuthoredFaceSdf",
                };
                authoredSdf.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                authoredSdf.Apply(false, false);
                AssetDatabase.CreateAsset(
                    authoredSdf,
                    TestFolder + "/AuthoredFaceSdf.asset");
                face.SetTexture("_FaceSDF", authoredSdf);
                face.SetFloat("_FaceShadowStrength", 0f);
                face.SetVector("_FaceForward", Vector4.zero);
                face.SetFloat("_FaceSdfMainChannel", 3f);
                face.SetFloat("_FaceSdfSoftChannel", 3f);
                face.SetColor("_LitTint", Color.white);
                face.SetColor("_ShadowTint", Color.white);
                face.SetFloat("_FaceSdfDebugMode", 5f);
                face.SetFloat("_FaceShadowSoftness", 0.88f);
                face.SetFloat("_SkinSSSIntensity", 0.42f);

                var disabledSerializedMaterial = EditorJsonUtility.ToJson(face);
                var diagnostics = MikuWuwaFaceMaterialDiagnostics.Validate(face);
                Assert.That(
                    EditorJsonUtility.ToJson(face),
                    Is.EqualTo(disabledSerializedMaterial),
                    "Diagnostics must not normalize authored SDF settings.");
                CollectionAssert.Contains(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.SdfStrengthZero);
                CollectionAssert.Contains(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.BasisInvalid);
                CollectionAssert.Contains(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.ChannelsIdentical);
                CollectionAssert.Contains(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.TintContrastZero);
                CollectionAssert.Contains(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.DebugViewActive);
                CollectionAssert.Contains(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.TransitionTooWide);
                CollectionAssert.DoesNotContain(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.SssMayFlattenShadow,
                    "SSS cannot flatten a shadow that has zero SDF strength.");

                face.SetFloat("_FaceShadowStrength", 1f);
                diagnostics = MikuWuwaFaceMaterialDiagnostics.Validate(face);
                CollectionAssert.Contains(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.SssMayFlattenShadow);

                face.SetVector(
                    "_FaceForward",
                    new Vector4(0f, -1f, 0f, 0f));
                face.SetVector(
                    "_FaceRight",
                    new Vector4(1f, 0f, 0f, 0f));
                face.SetVector(
                    "_FaceUp",
                    new Vector4(0f, 0f, 1f, 0f));
                face.SetFloat("_FaceSdfSoftChannel", 2f);
                face.SetColor("_ShadowTint", new Color(0.4f, 0.4f, 0.4f));
                face.SetFloat("_FaceSdfDebugMode", 0f);
                face.SetFloat("_FaceShadowSoftness", 0.067f);
                face.SetFloat("_SkinSSSIntensity", 0.12f);
                diagnostics = MikuWuwaFaceMaterialDiagnostics.Validate(face);
                CollectionAssert.DoesNotContain(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.SdfRequired);
                CollectionAssert.DoesNotContain(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.SdfStrengthZero);
                CollectionAssert.DoesNotContain(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.BasisInvalid);
                CollectionAssert.DoesNotContain(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.TransitionTooWide);

                face.SetFloat("_FaceSdfMirrorBlendWidth", 0.3f);
                diagnostics = MikuWuwaFaceMaterialDiagnostics.Validate(face);
                CollectionAssert.Contains(
                    diagnostics,
                    MikuWuwaFaceMaterialDiagnostics.TransitionTooWide,
                    "The diagnostic must inspect the public mirror blend width.");
            }
            finally
            {
                Object.DestroyImmediate(face);
            }
        }

        [Test]
        public void WuwaFaceDiagnosticsReportImportSettingsWithoutReimporting()
        {
            AssetDatabase.CreateFolder("Assets", "MikuWuwaTutorialTests");
            var assetPath = TestFolder + "/InvalidFaceSdf.png";
            var absolutePath = Path.Combine(
                Application.dataPath,
                "MikuWuwaTutorialTests/InvalidFaceSdf.png");
            var source = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try
            {
                source.SetPixel(0, 0, Color.gray);
                source.Apply(false, false);
                File.WriteAllBytes(absolutePath, source.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var face = new Material(Shader.Find("MIKU/Wuwa/Face"));
            try
            {
                face.SetTexture(
                    "_FaceSDF",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
                var serializedMaterial = EditorJsonUtility.ToJson(face);

                CollectionAssert.Contains(
                    MikuWuwaFaceMaterialDiagnostics.Validate(face),
                    MikuWuwaFaceMaterialDiagnostics.ImportSettingsInvalid);

                importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.sRGBTexture, Is.True);
                Assert.That(importer.mipmapEnabled, Is.True);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(
                    EditorJsonUtility.ToJson(face),
                    Is.EqualTo(serializedMaterial),
                    "Diagnostics must not change the material or reimport its SDF.");

                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                CollectionAssert.DoesNotContain(
                    MikuWuwaFaceMaterialDiagnostics.Validate(face),
                    MikuWuwaFaceMaterialDiagnostics.ImportSettingsInvalid,
                    "Explicit 1-u mirroring must not depend on Repeat wrapping.");
            }
            finally
            {
                Object.DestroyImmediate(face);
            }
        }

        [Test]
        public void WuwaMaterialToolsApplyProfileAndSyncKeywords()
        {
            var face = new Material(Shader.Find("MIKU/Wuwa/Face"));
            var texture = new Texture2D(1, 1);
            try
            {
                face.SetTexture("_FaceSDF", texture);
                face.SetFloat("_UseHairShadow", 1f);
                Assert.That(
                    MikuWuwaMaterialTools.ApplyRecommendedProfile(face),
                    Is.True);
                Assert.That(face.IsKeywordEnabled("_WUWA_HAIR_SHADOW_ON"), Is.True);
                Assert.That(face.GetFloat("_FaceSoftChannelStrength"), Is.EqualTo(1f));
                Assert.That(face.GetFloat("_OutlineDistanceMode"), Is.EqualTo(1f));

                MikuWuwaMaterialTools.SyncKeywords(face);
                Assert.That(face.IsKeywordEnabled("_WUWA_HAIR_SHADOW_ON"), Is.True);

                Assert.Throws<System.ArgumentNullException>(() =>
                    MikuWuwaMaterialTools.ApplyRecommendedProfile(null));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(face);
            }
        }

        [Test]
        public void WuwaPackedBindingSelectsLinearPackedEncoding()
        {
            var body = new Material(Shader.Find("MIKU/Wuwa/Body"));
            var texture = new Texture2D(1, 1);
            try
            {
                MikuFixedWorkflowTextureBindings.Bind(
                    body,
                    "wuwa_toon",
                    new[]
                    {
                        new MikuToonTextureBinding
                        {
                            role = "WuwaPackedNormalRoughnessMetallic",
                            texture = texture,
                        },
                    });
                Assert.That(body.GetTexture("_NormalMap"), Is.SameAs(texture));
                Assert.That(body.GetFloat("_NormalMapEncoding"), Is.EqualTo(1f));
                Assert.Throws<System.InvalidOperationException>(() =>
                    MikuFixedWorkflowTextureBindings.ValidateForShader(
                        body.shader,
                        "wuwa_toon",
                        new[]
                        {
                            new MikuToonTextureBinding
                            {
                                role = "NormalMap",
                                texture = texture,
                            },
                            new MikuToonTextureBinding
                            {
                                role = "WuwaPackedNormalRoughnessMetallic",
                                texture = texture,
                            },
                        }));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void AllWuwaShadersCompileAndContainTutorialPbrContracts()
        {
            foreach (var part in new[] { "Body", "Hair", "Face", "Eye", "Effect" })
            {
                var shader = Shader.Find("MIKU/Wuwa/" + part);
                Assert.That(shader, Is.Not.Null, part);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, part);
                if (part == "Effect")
                    continue;
                var source = File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "../Packages/com.miku.shaderconverter/Runtime/Wuwa/Wuwa_" +
                    part + ".shader"));
                StringAssert.Contains("BRDFData", source);
                StringAssert.Contains("SAMPLE_GI", source);
                StringAssert.Contains("_MAIN_LIGHT_SHADOWS", source);
            }
            var common = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "../Packages/com.miku.shaderconverter/Runtime/Wuwa/WuwaCommon.hlsl"));
            StringAssert.Contains("InitializeBRDFData", common);
            StringAssert.Contains("DirectBRDFSpecular", common);
            StringAssert.Contains("EnvironmentBRDF", common);
            StringAssert.Contains("GlossyEnvironmentReflection", common);
        }

        [Test]
        public void CloneAndUpgradeSceneLeavesSourceAssetsUnchanged()
        {
            AssetDatabase.CreateFolder("Assets", "MikuWuwaTutorialTests");
            var sourceMaterialPath = TestFolder + "/Source.mat";
            var sourceScenePath = TestFolder + "/Source.unity";
            var destinationScenePath = TestFolder + "/Upgraded.unity";
            var destinationMaterialFolder = TestFolder + "/Materials3";
            var sourceMaterial = new Material(Shader.Find("MIKU/Wuwa/Body"));
            sourceMaterial.SetFloat("_MatcapSaturation", 0.8f);
            AssetDatabase.CreateAsset(sourceMaterial, sourceMaterialPath);

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var root = new GameObject("WuWaSource");
            root.AddComponent<MeshRenderer>().sharedMaterial = sourceMaterial;
            Assert.That(
                EditorSceneManager.SaveScene(scene, sourceScenePath),
                Is.True);

            var result = MikuWuwa3Migration.CloneAndUpgradeScene(
                sourceScenePath,
                destinationScenePath,
                destinationMaterialFolder);

            Assert.That(result.ScenePath, Is.EqualTo(destinationScenePath));
            Assert.That(result.MaterialPaths.Count, Is.EqualTo(1));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<Material>(sourceMaterialPath)
                    .GetFloat("_MatcapSaturation"),
                Is.EqualTo(0.8f));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<Material>(result.MaterialPaths[0])
                    .GetFloat("_MatcapSaturation"),
                Is.EqualTo(0.1f));
        }

        [Test]
        public void CloneAndUpgradeSceneRollsBackEveryCreatedAssetOnFailure()
        {
            AssetDatabase.CreateFolder("Assets", "MikuWuwaTutorialTests");
            var sourceScenePath = TestFolder + "/Source.unity";
            var destinationScenePath = TestFolder + "/Failed.unity";
            var destinationMaterialFolder = TestFolder + "/FailedMaterials";
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Assert.That(
                EditorSceneManager.SaveScene(scene, sourceScenePath),
                Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                MikuWuwa3Migration.CloneAndUpgradeScene(
                    sourceScenePath,
                    destinationScenePath,
                    destinationMaterialFolder,
                    () => throw new InvalidOperationException("injected")));

            Assert.That(
                AssetDatabase.LoadMainAssetAtPath(destinationScenePath),
                Is.Null);
            Assert.That(
                AssetDatabase.IsValidFolder(destinationMaterialFolder),
                Is.False);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(sourceScenePath),
                Is.Not.Null);
        }

        [Test]
        public void Uv7DerivationPreservesFourUvsColorsAndSkinning()
        {
            AssetDatabase.CreateFolder("Assets", "MikuWuwaTutorialTests");
            var source = new Mesh
            {
                name = "SyntheticWuWaSkinned",
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
                normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                tangents = new[]
                {
                    new Vector4(1f, 0f, 0f, 1f),
                    new Vector4(1f, 0f, 0f, 1f),
                    new Vector4(1f, 0f, 0f, 1f),
                },
                colors = new[]
                {
                    new Color(1f, 0.2f, 0f, 1f),
                    new Color(1f, 0.6f, 0f, 1f),
                    new Color(1f, 1f, 0f, 1f),
                },
                triangles = new[] { 0, 1, 2 },
                bindposes = new[] { Matrix4x4.identity },
                boneWeights = new[]
                {
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                },
            };
            for (var channel = 0; channel < 4; channel++)
                source.SetUVs(channel, new[]
                {
                    new Vector4(0f, channel, 0f, 0f),
                    new Vector4(1f, channel, 0f, 0f),
                    new Vector4(0f, channel + 1f, 0f, 0f),
                });
            var sourcePath = TestFolder + "/Synthetic.asset";
            var derivedPath = TestFolder + "/Synthetic_UV7.asset";
            AssetDatabase.CreateAsset(source, sourcePath);

            var derived = MikuToonMeshAssetCreator
                .CreateOrUpdateSmoothNormalAsset(source, derivedPath);

            Assert.That(MikuToonMeshAssetCreator.HasUv7(derived), Is.True);
            Assert.That(derived.colors, Is.EqualTo(source.colors));
            Assert.That(derived.boneWeights, Has.Length.EqualTo(3));
            Assert.That(derived.bindposes, Has.Length.EqualTo(1));
            var uv = new System.Collections.Generic.List<Vector4>();
            for (var channel = 0; channel < 4; channel++)
            {
                derived.GetUVs(channel, uv);
                Assert.That(uv, Has.Count.EqualTo(3), "UV" + channel);
                uv.Clear();
            }
        }
    }
}
