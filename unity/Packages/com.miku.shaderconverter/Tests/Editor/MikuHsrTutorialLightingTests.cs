// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Miku.ShaderConverter.Editor.Tests
{
    public sealed class MikuHsrTutorialLightingTests
    {
        static string HsrPath(string name) =>
            Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Packages/com.miku.shaderconverter/Runtime/HSR",
                name));

        [Test]
        public void ShadowAoMultipliesTheTutorialHalfLambertSignal()
        {
            Assert.That(
                MikuHsrShaderMath.TutorialShadowAoHalfLambert(
                    -1f, 1f),
                Is.Zero.Within(1e-6f));
            Assert.That(
                MikuHsrShaderMath.TutorialShadowAoHalfLambert(
                    0f, 0.25f),
                Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(
                MikuHsrShaderMath.TutorialShadowAoHalfLambert(
                    1f, 0.125f),
                Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(
                MikuHsrShaderMath.TutorialShadowAoHalfLambert(
                    1f, 0.25f),
                Is.EqualTo(1f).Within(1e-6f));
            Assert.That(
                MikuHsrShaderMath.TutorialShadowAoHalfLambert(
                    1f, 0f),
                Is.Zero.Within(1e-6f));
        }

        [Test]
        public void RampUUsesTheTutorialPointEightFiveAndPointOneFiveScale()
        {
            Assert.That(
                MikuHsrShaderMath.TutorialRampU(-1f),
                Is.EqualTo(0.15f).Within(1e-6f));
            Assert.That(
                MikuHsrShaderMath.TutorialRampU(0.5f),
                Is.EqualTo(0.575f).Within(1e-6f));
            Assert.That(
                MikuHsrShaderMath.TutorialRampU(2f),
                Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void SpecularUsesInvertedBlueAsASoftThreshold()
        {
            Assert.That(
                MikuHsrShaderMath.TutorialSpecularMask(
                    0.44f, 1f, 0.5f, 0.1f),
                Is.Zero.Within(1e-6f));
            Assert.That(
                MikuHsrShaderMath.TutorialSpecularMask(
                    0.54f, 1f, 0.5f, 0.1f),
                Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(
                MikuHsrShaderMath.TutorialSpecularMask(
                    0.64f, 1f, 0.5f, 0.1f),
                Is.EqualTo(1f).Within(1e-5f));
            Assert.That(
                MikuHsrShaderMath.TutorialSpecularMask(
                    0.54f, 1f, 0.25f, 0.1f),
                Is.Zero.Within(1e-5f));
            Assert.That(
                MikuHsrShaderMath.TutorialSpecularMask(
                    0.54f, 1f, 0.75f, 0.1f),
                Is.EqualTo(1f).Within(1e-5f));

            Assert.That(
                MikuHsrShaderMath.TutorialSpecularMask(
                    1.04f - 0.5f, 1f, 0.5f, -1f),
                Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void FaceSpecularAndNoseLineMasksRemainControllable()
        {
            Assert.That(
                MikuHsrShaderMath.FaceSpecularWeight(
                    0.54f, 1f, 0.5f, 0.1f, 2f, 0.25f),
                Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(
                MikuHsrShaderMath.FaceSpecularWeight(
                    1f, 1f, 1f, 0.1f, -1f, 1f),
                Is.Zero.Within(1e-6f));

            Assert.That(
                MikuHsrShaderMath.NoseLineMask(
                    1f, 0.0625f, 3f, 2f),
                Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(
                MikuHsrShaderMath.NoseLineMask(
                    1f, 0.125f, 3f, 2f),
                Is.EqualTo(1f).Within(1e-6f));
            Assert.That(
                MikuHsrShaderMath.NoseLineMask(
                    0f, 1f, 3f, 10f),
                Is.Zero.Within(1e-6f));
        }

        [Test]
        public void SourcesExposeTheTutorialLightingContract()
        {
            var common = File.ReadAllText(HsrPath("HSRCommon.hlsl"));
            StringAssert.Contains(
                "dot(halfLambert.xx, shadowAO.xx)",
                common);
            StringAssert.Contains(
                "1.04 - saturate(thresholdMask)",
                common);
            StringAssert.Contains("lightMap.b,", common);
            StringAssert.Contains("HSR_ComputeFaceSpecular", common);
            StringAssert.Contains("HSR_FaceNoseLineMask", common);

            foreach (var name in new[] { "HSR_Body.shader", "HSR_Hair.shader" })
            {
                var source = File.ReadAllText(HsrPath(name));
                StringAssert.Contains("_SpecularSoftness", source, name);
                StringAssert.Contains(
                    "HSR_TutorialShadowAoHalfLambert(",
                    source,
                    name);
                StringAssert.Contains("HSR_ComputeSpecular(", source, name);
                StringAssert.Contains(
                    "_SpecularExponent, _SpecularSoftness,",
                    source,
                    name);
                StringAssert.Contains(", 0.15)", source, name);
                StringAssert.Contains("_ShadowThresholdCenter", source, name);
                StringAssert.Contains("_ShadowThresholdSoftness", source, name);
                StringAssert.Contains("_ShadowRampOffset", source, name);
            }

            var face = File.ReadAllText(HsrPath("HSR_Face.shader"));
            foreach (var marker in new[]
            {
                "_FaceSpecularThresholdMask",
                "_FaceSpecularExponent",
                "_FaceSpecularSoftness",
                "_FaceSpecularStrength",
                "_FaceSpecularColor",
                "_NoseLinePower",
                "_NoseLineStrength",
                "_NoseLineColor",
                "HSR_ComputeFaceSpecular(",
                "HSR_FaceNoseLineMask(",
                "faceMap.b",
            })
                StringAssert.Contains(marker, face);
            StringAssert.Contains("+ specular", face);
            StringAssert.DoesNotContain("_LightMap", face);
        }

        [Test]
        public void HsrShadersExposeSafeVisibleDefaultsAndCompile()
        {
            foreach (var shaderName in new[]
            {
                "MIKU/HSR/Body",
                "MIKU/HSR/Hair",
                "MIKU/HSR/Face",
            })
            {
                var shader = Shader.Find(shaderName);
                Assert.That(shader, Is.Not.Null, shaderName);
                Assert.That(
                    ShaderUtil.ShaderHasError(shader),
                    Is.False,
                    shaderName);
            }

            foreach (var shaderName in new[]
            {
                "MIKU/HSR/Body",
                "MIKU/HSR/Hair",
            })
            {
                var material = new Material(Shader.Find(shaderName));
                try
                {
                    Assert.That(material.HasProperty("_SpecularSoftness"),
                        Is.True, shaderName);
                    Assert.That(material.GetFloat("_SpecularSoftness"),
                        Is.GreaterThanOrEqualTo(1e-5f), shaderName);
                    foreach (var legacyProperty in new[]
                    {
                        "_ShadowThresholdCenter",
                        "_ShadowThresholdSoftness",
                        "_ShadowRampOffset",
                    })
                        Assert.That(material.HasProperty(legacyProperty),
                            Is.True, shaderName);
                }
                finally
                {
                    Object.DestroyImmediate(material);
                }
            }

            var face = new Material(Shader.Find("MIKU/HSR/Face"));
            try
            {
                foreach (var property in new[]
                {
                    "_FaceSpecularThresholdMask",
                    "_FaceSpecularExponent",
                    "_FaceSpecularSoftness",
                    "_FaceSpecularStrength",
                    "_FaceSpecularColor",
                    "_NoseLinePower",
                    "_NoseLineStrength",
                    "_NoseLineColor",
                })
                    Assert.That(face.HasProperty(property), Is.True, property);

                Assert.That(face.HasProperty("_LightMap"), Is.False);
                Assert.That(face.GetFloat("_FaceSpecularExponent"),
                    Is.GreaterThanOrEqualTo(1f));
                Assert.That(face.GetFloat("_FaceSpecularSoftness"),
                    Is.GreaterThanOrEqualTo(1e-5f));
                Assert.That(face.GetFloat("_FaceSpecularStrength"),
                    Is.GreaterThan(0f));
                Assert.That(face.GetFloat("_NoseLinePower"),
                    Is.GreaterThanOrEqualTo(0.1f));
                Assert.That(face.GetFloat("_NoseLineStrength"),
                    Is.GreaterThan(1f));
                Assert.That(face.GetColor("_FaceSpecularColor").maxColorComponent,
                    Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(face);
            }
        }

        [Test]
        public void RecommendedHsrFaceProfileRestoresVisibleDetailControls()
        {
            var face = new Material(Shader.Find("MIKU/HSR/Face"));
            try
            {
                face.SetFloat("_FaceSpecularStrength", 0f);
                face.SetFloat("_NoseLineStrength", 0f);

                Assert.That(
                    MikuGameToonMaterialProfiles.ApplyRecommended(
                        face,
                        logMissingMask: false),
                    Is.True);
                Assert.That(
                    face.GetFloat("_FaceSpecularStrength"),
                    Is.EqualTo(0.12f).Within(1e-6f));
                Assert.That(
                    face.GetFloat("_NoseLineStrength"),
                    Is.EqualTo(8f).Within(1e-6f));
                Assert.That(
                    face.GetFloat("_NoseLinePower"),
                    Is.EqualTo(3f).Within(1e-6f));
                Assert.That(
                    face.GetColor("_NoseLineColor").maxColorComponent,
                    Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(face);
            }
        }
    }
}
