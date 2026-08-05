// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Miku.ShaderConverter.Editor.Tests
{
    public sealed class MikuGameToon225Tests
    {
        const string TestFolder = "Assets/MikuGameToon225Tests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void ShaderFamilyVersionIs2211()
        {
            Assert.That(
                MikuToonMaterialRecipe.CurrentShaderFamilyVersion,
                Is.EqualTo("2.2.11"));
        }

        [Test]
        public void AuthoredAndFallbackMasksRejectNonSkinRegions()
        {
            Assert.That(MikuGameToonCpuMath.HighValueMask(0.95f), Is.Zero);
            Assert.That(MikuGameToonCpuMath.HighValueMask(0.995f), Is.EqualTo(1f));
            Assert.That(
                MikuGameToonCpuMath.StarRailBodySkinMask(
                    5f / 255f,
                    new Color(0.82f, 0.55f, 0.48f)),
                Is.GreaterThan(0.9f));
            Assert.That(
                MikuGameToonCpuMath.StarRailBodySkinMask(
                    5f / 255f,
                    new Color(0.18f, 0.42f, 0.85f)),
                Is.LessThan(0.001f));
            Assert.That(
                MikuGameToonCpuMath.WarmPaleFaceMask(
                    new Color(0.88f, 0.67f, 0.60f)),
                Is.GreaterThan(0.8f));
            Assert.That(
                MikuGameToonCpuMath.WarmPaleFaceMask(
                    new Color(0.08f, 0.12f, 0.30f)),
                Is.LessThan(0.001f));
        }

        [Test]
        public void SkinToneAndSssHaveZeroInfluenceOutsideMask()
        {
            var source = new Color(0.21f, 0.43f, 0.77f, 1f);
            var toned = MikuGameToonCpuMath.ApplySkinTone(
                source,
                0f,
                1.2f,
                0.5f,
                new Color(1f, 0.93f, 0.90f));
            Assert.That(toned, Is.EqualTo(source));
            var sss = MikuGameToonCpuMath.SkinSss(
                source,
                0f,
                Vector3.forward,
                Vector3.forward,
                Vector3.back,
                Color.white,
                0f,
                0.2f,
                0.35f,
                new Color(1f, 0.5f, 0.4f));
            Assert.That(sss.r, Is.Zero);
            Assert.That(sss.g, Is.Zero);
            Assert.That(sss.b, Is.Zero);
            Assert.That(float.IsNaN(sss.r) || float.IsInfinity(sss.r), Is.False);
        }

        [Test]
        public void GenshinSoftShoulderIsMonotonicFiniteAndPreservesRgbRatio()
        {
            var previousPeak = 0f;
            for (var index = 0; index <= 100; index++)
            {
                var scale = index * 0.03f;
                var input = new Color(scale, scale * 0.5f, scale * 0.25f, 1f);
                var output = MikuGameToonCpuMath.CompressNonEmissive(
                    input,
                    1f,
                    0.72f,
                    0.98f);
                var peak = Mathf.Max(output.r, output.g, output.b);
                Assert.That(peak, Is.GreaterThanOrEqualTo(previousPeak - 0.00001f));
                Assert.That(float.IsFinite(peak), Is.True);
                if (output.r > 0.0001f)
                {
                    Assert.That(output.g / output.r, Is.EqualTo(0.5f).Within(0.0001f));
                    Assert.That(output.b / output.r, Is.EqualTo(0.25f).Within(0.0001f));
                }
                previousPeak = peak;
            }

            var legacyInput = new Color(1f, 0.72f, 0.31f, 1f);
            Assert.That(
                MikuGameToonCpuMath.CompressNonEmissive(
                    legacyInput,
                    0f,
                    0.72f,
                    0.98f),
                Is.EqualTo(legacyInput));
        }

        [Test]
        public void WuwaFaceIdTextureDrivesIdKeyword()
        {
            var material = new Material(Shader.Find("MIKU/Wuwa/Face"));
            var texture = new Texture2D(1, 1);
            try
            {
                material.SetTexture("_FaceID", texture);
                MikuManualTextureKeywordUtility.SyncKeywords(material);
                Assert.That(material.IsKeywordEnabled("_WUWA_ID_ON"), Is.True);
                material.SetTexture("_FaceID", null);
                MikuManualTextureKeywordUtility.SyncKeywords(material);
                Assert.That(material.IsKeywordEnabled("_WUWA_ID_ON"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void WuwaEyeBindsAuthoredTexturesAndUvTransforms()
        {
            var shader = Shader.Find("MIKU/Wuwa/Eye");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var mask = new Texture2D(1, 1);
            try
            {
                foreach (var property in new[]
                {
                    "_EyeHET",
                    "_EyeHDMF",
                    "_EyeUpperHighlight",
                    "_EyeLowerHighlight",
                    "_EyeEG",
                    "_EyeHETScleraStrength",
                    "_EyeHETPupilStrength",
                    "_EyeHDMFHighlightStrength",
                    "_EyeUpperHighlightOffset",
                    "_EyeLowerHighlightOffset",
                    "_EyeUpperHighlightScale",
                    "_EyeLowerHighlightScale",
                    "_EyeUpperHighlightUVRow0",
                    "_EyeUpperHighlightUVRow1",
                    "_EyeBaseEmissionStrength",
                    "_EmissionStrength",
                })
                    Assert.That(material.HasProperty(property), Is.True, property);

                Assert.That(material.GetFloat("_EyeBaseBrightness"), Is.EqualTo(1.2f));
                Assert.That(
                    material.GetVector("_EyeUpperHighlightScale"),
                    Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
                Assert.That(
                    material.GetVector("_EyeLowerHighlightScale"),
                    Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
                Assert.That(material.GetFloat("_EyeBaseEmissionStrength"), Is.Zero);
                Assert.That(material.GetFloat("_EmissionStrength"), Is.EqualTo(1f));

                MikuFixedWorkflowTextureBindings.Bind(
                    material,
                    "wuwa_toon",
                    new[]
                    {
                        new MikuToonTextureBinding { role = "EyeHET", texture = mask },
                        new MikuToonTextureBinding { role = "EyeHDMF", texture = mask },
                        new MikuToonTextureBinding
                        {
                            role = "EyeUpperHighlight",
                            texture = mask,
                            uvTransform = new MikuToonUvTransform
                            {
                                row0 = new Vector3(0.68f, 0f, 0.13f),
                                row1 = new Vector3(0f, 1.27f, -0.05f),
                            },
                        },
                        new MikuToonTextureBinding { role = "EyeLowerHighlight", texture = mask },
                        new MikuToonTextureBinding { role = "EyeEG", texture = mask },
                    });
                Assert.That(
                    material.IsKeywordEnabled("_WUWA_EYE_HET_ON"),
                    Is.True);
                Assert.That(
                    material.IsKeywordEnabled("_WUWA_EYE_EG_ON"),
                    Is.True);
                Assert.That(material.IsKeywordEnabled("_WUWA_EYE_HDMF_ON"), Is.True);
                Assert.That(
                    material.IsKeywordEnabled("_WUWA_EYE_UPPER_HIGHLIGHT_ON"),
                    Is.True);
                Assert.That(
                    material.IsKeywordEnabled("_WUWA_EMISSION_ON"),
                    Is.False);
                Assert.That(
                    material.GetVector("_EyeUpperHighlightUVRow0"),
                    Is.EqualTo(new Vector4(0.68f, 0f, 0.13f, 0f)));
                Assert.That(
                    material.GetVector("_EyeUpperHighlightUVRow1"),
                    Is.EqualTo(new Vector4(0f, 1.27f, -0.05f, 0f)));

                var source = File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "../Packages/com.miku.shaderconverter/Runtime/Wuwa/Wuwa_Eye.shader"));
                Assert.That(source.Split(new[] { "_EyeHET, sampler_EyeHET" },
                    System.StringSplitOptions.None).Length - 1, Is.EqualTo(1));
                StringAssert.Contains("float pupilMask = saturate(1.0 - hdmf.a)", source);
                StringAssert.Contains("baseColor * hetMask * lerp", source);
                StringAssert.Contains("_EyeEG, sampler_EyeEG", source);
                StringAssert.Contains("return half4(hdmf.bbb, 1.0)", source);
                Assert.That(
                    source.Split(new[] { "hdmf.b" },
                        System.StringSplitOptions.None).Length - 1,
                    Is.EqualTo(1));
                StringAssert.DoesNotContain("SAMPLE_TEXTURE2D(_EmissionMap", source);
            }
            finally
            {
                Object.DestroyImmediate(mask);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void WuwaEyeMasksUvAndEgFallbackMatchShaderContract()
        {
            var upper = MikuGameToonCpuMath.WuwaEyeAffineUv(
                new Vector2(0.5f, 0.5f),
                new Vector3(0.68f, 0f, 0.13f),
                new Vector3(0f, 1.27f, -0.05f));
            Assert.That(upper.x, Is.EqualTo(0.47f).Within(0.0001f));
            Assert.That(upper.y, Is.EqualTo(0.585f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaEyeAuthoredHighlightMask(0.04f),
                Is.Zero);
            Assert.That(
                MikuGameToonCpuMath.WuwaEyeAuthoredHighlightMask(0.9041452f),
                Is.EqualTo(1f));
            Assert.That(MikuGameToonCpuMath.WuwaEyePupilMask(1f), Is.Zero);
            Assert.That(MikuGameToonCpuMath.WuwaEyePupilMask(0f), Is.EqualTo(1f));
            Assert.That(MikuGameToonCpuMath.WuwaEyeEmissionWeight(0f, 5f), Is.Zero);
            Assert.That(MikuGameToonCpuMath.WuwaEyeEmissionWeight(0.5f, 2f), Is.EqualTo(1f));
            Assert.That(MikuGameToonCpuMath.WuwaEyeEmissionWeight(1f, 2f), Is.EqualTo(2f));
            var leftOffset = MikuGameToonCpuMath.WuwaEyeEgLightOffset(
                Vector3.left,
                Vector3.right,
                Vector3.up,
                0.08f);
            var rightOffset = MikuGameToonCpuMath.WuwaEyeEgLightOffset(
                Vector3.right,
                Vector3.right,
                Vector3.up,
                0.08f);
            Assert.That(leftOffset.x, Is.EqualTo(-0.08f).Within(0.0001f));
            Assert.That(rightOffset.x, Is.EqualTo(0.08f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaEyeEgLightOffset(
                    Vector3.right,
                    Vector3.zero,
                    Vector3.up,
                    0.08f),
                Is.EqualTo(Vector2.zero));
            Assert.That(
                MikuGameToonCpuMath.WuwaStockingMask(
                    new Color(0f, 0.68f, 0f, 1f)),
                Is.Zero);
            Assert.That(
                MikuGameToonCpuMath.WuwaStockingMask(
                    new Color(0f, 0.72f, 0f, 1f)),
                Is.EqualTo(1f));
        }

        [Test]
        public void WuwaEyeSyntheticPixelsPreserveHetAndIgnoreHdmfBlue()
        {
            var baseColor = new Color(0.8f, 0.6f, 0.4f, 1f);
            var scleraColor = new Color(1f, 0.75f, 0.5f, 1f);
            var pupilColor = new Color(0.25f, 0.5f, 1f, 1f);
            var scleraHdmf = new Color(0f, 0f, 0f, 1f);
            var black = MikuGameToonCpuMath.WuwaEyeHetEmission(
                baseColor,
                0f,
                scleraHdmf,
                scleraColor,
                2f,
                pupilColor,
                3f);
            var gray = MikuGameToonCpuMath.WuwaEyeHetEmission(
                baseColor,
                0.5f,
                scleraHdmf,
                scleraColor,
                2f,
                pupilColor,
                3f);
            var white = MikuGameToonCpuMath.WuwaEyeHetEmission(
                baseColor,
                1f,
                scleraHdmf,
                scleraColor,
                2f,
                pupilColor,
                3f);
            Assert.That(black, Is.EqualTo(new Color(0f, 0f, 0f, 0f)));
            Assert.That(gray.r, Is.EqualTo(white.r * 0.5f));
            Assert.That(gray.g, Is.EqualTo(white.g * 0.5f));
            Assert.That(gray.b, Is.EqualTo(white.b * 0.5f));

            var blueChanged = MikuGameToonCpuMath.WuwaEyeHetEmission(
                baseColor,
                1f,
                new Color(0f, 0f, 1f, 1f),
                scleraColor,
                2f,
                pupilColor,
                3f);
            Assert.That(blueChanged, Is.EqualTo(white));

            var pupil = MikuGameToonCpuMath.WuwaEyeHetEmission(
                baseColor,
                1f,
                new Color(0f, 0f, 0f, 0f),
                scleraColor,
                2f,
                pupilColor,
                3f);
            Assert.That(pupil, Is.Not.EqualTo(white));
            Assert.That(pupil.r, Is.EqualTo(baseColor.r * pupilColor.r * 3f));
            Assert.That(pupil.g, Is.EqualTo(baseColor.g * pupilColor.g * 3f));
            Assert.That(pupil.b, Is.EqualTo(baseColor.b * pupilColor.b * 3f));
        }

        [Test]
        public void WuwaEyeRecipePersistsAndReappliesUvTransform()
        {
            AssetDatabase.CreateFolder("Assets", "MikuGameToon225Tests");
            var shader = Shader.Find("MIKU/Wuwa/Eye");
            var generated = new Material(shader) { name = "Eye Generated" };
            var user = new Material(shader) { name = "Eye User" };
            var texture = new Texture2D(1, 1) { name = "Upper" };
            AssetDatabase.CreateAsset(generated, TestFolder + "/Eye.generated.mat");
            AssetDatabase.CreateAsset(user, TestFolder + "/Eye.mat");
            AssetDatabase.CreateAsset(texture, TestFolder + "/Upper.asset");
            var recipePath = TestFolder + "/Eye-" +
                System.Guid.NewGuid().ToString("N") +
                ".toon-recipe.asset";
            var recipe = MikuToonRecipeUtility.CreateOrUpdateImported(
                recipePath,
                generated,
                user,
                "wuwa_toon",
                "Eye",
                new[]
                {
                    new MikuToonTextureBinding
                    {
                        role = "EyeHET",
                        texture = texture,
                    },
                    new MikuToonTextureBinding
                    {
                        role = "EyeUpperHighlight",
                        texture = texture,
                        uvTransform = new MikuToonUvTransform
                        {
                            row0 = new Vector3(0.68f, 0f, 0.13f),
                            row1 = new Vector3(0f, 1.27f, -0.05f),
                        },
                    },
                });
            Assert.That(recipe.gamePart, Is.EqualTo(MikuGameMaterialPart.Eye));
            Assert.That(recipe.textureBindings.Length, Is.EqualTo(2));
            Assert.That(
                recipe.textureBindings.Single(item =>
                    item.role == "EyeUpperHighlight").uvTransform.row0,
                Is.EqualTo(new Vector3(0.68f, 0f, 0.13f)));
            Assert.That(
                recipe.textureBindings.Single(item =>
                    item.role == "EyeHET").uvTransform,
                Is.Null);
            AssetDatabase.ImportAsset(
                recipePath,
                ImportAssetOptions.ForceUpdate);
            recipe = AssetDatabase.LoadAssetAtPath<MikuToonMaterialRecipe>(
                recipePath);
            Assert.That(
                recipe.textureBindings.Single(item =>
                    item.role == "EyeHET").uvTransform,
                Is.Null);
            MikuToonRecipeUtility.ApplySelection(recipe);
            Assert.That(
                generated.GetVector("_EyeUpperHighlightUVRow0"),
                Is.EqualTo(new Vector4(0.68f, 0f, 0.13f, 0f)));
            Assert.That(
                generated.GetVector("_EyeUpperHighlightUVRow1"),
                Is.EqualTo(new Vector4(0f, 1.27f, -0.05f, 0f)));
        }

        [Test]
        public void WuwaMaterialFaceBasisUsesObjectMatrixAndRepairsHandedness()
        {
            var matrix = Matrix4x4.TRS(
                new Vector3(2f, -1f, 7f),
                Quaternion.Euler(24f, 137f, -11f),
                new Vector3(-2f, 3f, 4f));
            MikuGameToonCpuMath.WuwaFaceBasis(
                matrix,
                Vector3.right,
                Vector3.forward,
                Vector3.down,
                out var right,
                out var up,
                out var forward);
            Assert.That(Vector3.Dot(right, up), Is.Zero.Within(0.00001f));
            Assert.That(Vector3.Dot(right, forward), Is.Zero.Within(0.00001f));
            Assert.That(Vector3.Dot(up, forward), Is.Zero.Within(0.00001f));
            Assert.That(
                Vector3.Dot(Vector3.Cross(forward, right), up),
                Is.GreaterThan(0.999f));
            Assert.That(right.sqrMagnitude, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(up.sqrMagnitude, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(forward.sqrMagnitude, Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void WuwaStockingsRequireTheIdMapResourceAndEnableAtomically()
        {
            var material = new Material(Shader.Find("MIKU/Wuwa/Body"));
            var id = new Texture2D(1, 1);
            var wrong = new Texture2D(1, 1);
            try
            {
                MikuFixedWorkflowTextureBindings.Bind(
                    material,
                    "wuwa_toon",
                    new[]
                    {
                        new MikuToonTextureBinding { role = "IDMap", texture = id },
                        new MikuToonTextureBinding { role = "StockingsMap", texture = id },
                    });
                Assert.That(
                    material.IsKeywordEnabled("_WUWA_STOCKINGS_ON"),
                    Is.True);
                Assert.That(material.GetFloat("_UseStockings"), Is.EqualTo(1f));
                Assert.That(material.GetTexture("_IDMap"), Is.SameAs(id));
                Assert.That(material.GetTexture("_StockingsMap"), Is.SameAs(id));

                var error = Assert.Throws<System.InvalidOperationException>(() =>
                    MikuFixedWorkflowTextureBindings.Bind(
                        material,
                        "wuwa_toon",
                        new[]
                        {
                            new MikuToonTextureBinding { role = "IDMap", texture = id },
                            new MikuToonTextureBinding { role = "StockingsMap", texture = wrong },
                        }));
                Assert.That(
                    error.Message,
                    Is.EqualTo("MIKU_WUWA_STOCKINGS_ID_SOURCE_MISMATCH"));
            }
            finally
            {
                Object.DestroyImmediate(id);
                Object.DestroyImmediate(wrong);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void WuwaBodyEmissionStrengthScalesOneSampleWithoutDrivingKeyword()
        {
            var material = new Material(Shader.Find("MIKU/Wuwa/Body"));
            var emission = new Texture2D(1, 1);
            try
            {
                Assert.That(material.HasProperty("_BodyEmissionStrength"), Is.True);
                Assert.That(material.GetFloat("_BodyEmissionStrength"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_MatcapStrength"), Is.EqualTo(0.15f));

                material.SetFloat("_BodyEmissionStrength", 0f);
                material.SetTexture("_EmissionMap", null);
                MikuManualTextureKeywordUtility.SyncKeywords(material);
                Assert.That(material.IsKeywordEnabled("_WUWA_EMISSION_ON"), Is.False);

                material.SetTexture("_EmissionMap", emission);
                MikuManualTextureKeywordUtility.SyncKeywords(material);
                Assert.That(material.IsKeywordEnabled("_WUWA_EMISSION_ON"), Is.True);

                var source = File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "../Packages/com.miku.shaderconverter/Runtime/Wuwa/Wuwa_Body.shader"));
                StringAssert.Contains(
                    "SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _BodyEmissionStrength",
                    source);
                Assert.That(
                    source.Split(new[] { "float _BodyEmissionStrength;" },
                        System.StringSplitOptions.None).Length - 1,
                    Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(emission);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void WuwaRecommendedProfileCalibratesEyeFaceHairAndEffect()
        {
            var body = new Material(Shader.Find("MIKU/Wuwa/Body"));
            var eye = new Material(Shader.Find("MIKU/Wuwa/Eye"));
            var face = new Material(Shader.Find("MIKU/Wuwa/Face"));
            var hair = new Material(Shader.Find("MIKU/Wuwa/Hair"));
            var effect = new Material(Shader.Find("MIKU/Wuwa/Effect"));
            try
            {
                body.SetFloat("_BodyEmissionStrength", 0f);
                body.SetFloat("_MatcapStrength", 0f);
                eye.SetFloat("_EmissionStrength", 0f);
                face.SetFloat("_FaceFlatness", 0f);
                hair.SetFloat("_HairBaseBrightness", 0f);
                effect.SetFloat("_PrimaryEmissionStrength", 0f);
                Assert.That(
                    MikuGameToonMaterialProfiles.ApplyRecommended(body, false),
                    Is.True);
                Assert.That(
                    MikuGameToonMaterialProfiles.ApplyRecommended(eye, false),
                    Is.True);
                Assert.That(
                    MikuGameToonMaterialProfiles.ApplyRecommended(face, false),
                    Is.True);
                Assert.That(
                    MikuGameToonMaterialProfiles.ApplyRecommended(hair, false),
                    Is.True);
                Assert.That(
                    MikuGameToonMaterialProfiles.ApplyRecommended(effect, false),
                    Is.True);
                Assert.That(body.GetFloat("_BodyEmissionStrength"), Is.EqualTo(1f));
                Assert.That(body.GetFloat("_MatcapStrength"), Is.EqualTo(0.15f));
                Assert.That(eye.GetFloat("_EyeBaseBrightness"), Is.EqualTo(1.2f));
                Assert.That(eye.GetFloat("_EmissionStrength"), Is.EqualTo(1f));
                Assert.That(
                    eye.GetFloat("_EyeBaseEmissionStrength"),
                    Is.Zero);
                Assert.That(
                    eye.GetFloat("_EyeHighlightThreshold"),
                    Is.EqualTo(0.04000002f));
                Assert.That(
                    eye.GetVector("_EyeUpperHighlightScale"),
                    Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
                Assert.That(
                    eye.GetVector("_EyeLowerHighlightScale"),
                    Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
                Assert.That(face.GetFloat("_FaceFlatness"), Is.EqualTo(1f));
                Assert.That(face.GetFloat("_FaceBaseCurvePower"), Is.EqualTo(1.2f));
                Assert.That(face.GetFloat("_FaceBaseBrightness"), Is.EqualTo(1f));
                Assert.That(face.GetFloat("_FaceFinalBrightness"), Is.EqualTo(1f));
                Assert.That(
                    face.GetVector("_FaceForward"),
                    Is.EqualTo(new Vector4(0f, -1f, 0f, 0f)));
                Assert.That(
                    hair.GetFloat("_HairBaseBrightness"),
                    Is.EqualTo(1.2f));
                Assert.That(
                    effect.GetFloat("_PrimaryEmissionStrength"),
                    Is.EqualTo(1.4f));
            }
            finally
            {
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(eye);
                Object.DestroyImmediate(face);
                Object.DestroyImmediate(hair);
                Object.DestroyImmediate(effect);
            }
        }

        [Test]
        public void MissingRequiredBodyMaskDisablesSssAndReportsDiagnostic()
        {
            var material = new Material(Shader.Find("MIKU/Wuwa/Body"));
            try
            {
                material.SetTexture("_IDMap", null);
                LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex(
                        "MIKU_SKIN_MASK_TEXTURE_MISSING:MIKU/Wuwa/Body:_IDMap"));
                MikuGameToonMaterialProfiles.ApplyRecommended(material);
                Assert.That(material.GetFloat("_SkinSSSIntensity"), Is.Zero);
                Assert.That(material.GetFloat("_SkinToneBrightness"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_SkinToneWhitening"), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RecommendedMigrationIsIdempotentAndNeverTouchesUserMaterial()
        {
            AssetDatabase.CreateFolder("Assets", "MikuGameToon225Tests");
            var shader = Shader.Find("MIKU/Wuwa/Face");
            var generated = new Material(shader) { name = "Generated" };
            var user = new Material(shader) { name = "User" };
            generated.SetFloat("_FaceBaseBrightness", 1.5f);
            user.SetFloat("_FaceBaseBrightness", 1.5f);
            AssetDatabase.CreateAsset(generated, TestFolder + "/Generated.mat");
            AssetDatabase.CreateAsset(user, TestFolder + "/User.mat");
            var recipePath = TestFolder + "/Generated.toon-recipe.asset";

            MikuToonRecipeUtility.CreateOrUpdateImported(
                recipePath,
                generated,
                user,
                "wuwa_toon",
                "Face",
                new Dictionary<string, Texture2D>());
            Assert.That(generated.GetFloat("_FaceBaseBrightness"), Is.EqualTo(1f));
            Assert.That(user.GetFloat("_FaceBaseBrightness"), Is.EqualTo(1.5f));

            generated.SetFloat("_FaceBaseBrightness", 0.91f);
            MikuToonRecipeUtility.CreateOrUpdateImported(
                recipePath,
                generated,
                user,
                "wuwa_toon",
                "Face",
                new Dictionary<string, Texture2D>());
            Assert.That(generated.GetFloat("_FaceBaseBrightness"), Is.EqualTo(0.91f));
            Assert.That(user.GetFloat("_FaceBaseBrightness"), Is.EqualTo(1.5f));
        }

        [Test]
        public void AnimeVolumeProfileHasDeterministicNeutralVividGrade()
        {
            AssetDatabase.CreateFolder("Assets", "MikuGameToon225Tests");
            var path = TestFolder + "/AnimeProfile.asset";
            var profile = MikuAnimeVolumeProfileFactory.CreateOrReplace(path);
            Assert.That(profile.components.Count, Is.EqualTo(10));
            Assert.That(
                profile.components.Select(item => item.GetType().Name),
                Is.EqualTo(new[]
                {
                    nameof(Tonemapping),
                    nameof(WhiteBalance),
                    nameof(ChannelMixer),
                    nameof(LiftGammaGain),
                    nameof(ShadowsMidtonesHighlights),
                    nameof(SplitToning),
                    nameof(ColorCurves),
                    nameof(ColorAdjustments),
                    nameof(Bloom),
                    nameof(Vignette),
                }));
            Assert.That(profile.components.All(item => item.active), Is.True);

            Assert.That(profile.TryGet(out Tonemapping tonemapping), Is.True);
            AssertOverride(tonemapping.mode, TonemappingMode.Neutral);

            Assert.That(profile.TryGet(out WhiteBalance whiteBalance), Is.True);
            AssertOverride(whiteBalance.temperature, 0f);
            AssertOverride(whiteBalance.tint, 0f);

            Assert.That(profile.TryGet(out ChannelMixer channelMixer), Is.True);
            AssertOverride(channelMixer.redOutRedIn, 100f);
            AssertOverride(channelMixer.redOutGreenIn, 0f);
            AssertOverride(channelMixer.redOutBlueIn, 0f);
            AssertOverride(channelMixer.greenOutRedIn, 0f);
            AssertOverride(channelMixer.greenOutGreenIn, 100f);
            AssertOverride(channelMixer.greenOutBlueIn, 0f);
            AssertOverride(channelMixer.blueOutRedIn, 0f);
            AssertOverride(channelMixer.blueOutGreenIn, 0f);
            AssertOverride(channelMixer.blueOutBlueIn, 100f);

            Assert.That(profile.TryGet(out LiftGammaGain liftGammaGain), Is.True);
            AssertOverride(
                liftGammaGain.lift,
                new Vector4(1f, 1f, 1f, 0f));
            AssertOverride(
                liftGammaGain.gamma,
                new Vector4(1f, 1f, 1f, 0f));
            AssertOverride(
                liftGammaGain.gain,
                new Vector4(1f, 1f, 1f, 0f));

            Assert.That(
                profile.TryGet(out ShadowsMidtonesHighlights smh),
                Is.True);
            AssertOverride(
                smh.shadows,
                new Vector4(1f, 1f, 1f, 0f));
            AssertOverride(
                smh.midtones,
                new Vector4(1f, 1f, 1f, 0f));
            AssertOverride(
                smh.highlights,
                new Vector4(1f, 1f, 1f, 0f));
            AssertOverride(smh.shadowsStart, 0f);
            AssertOverride(smh.shadowsEnd, 0.35f);
            AssertOverride(smh.highlightsStart, 0.58f);
            AssertOverride(smh.highlightsEnd, 1f);

            Assert.That(profile.TryGet(out SplitToning splitToning), Is.True);
            AssertOverride(
                splitToning.shadows,
                Color.gray);
            AssertOverride(
                splitToning.highlights,
                Color.gray);
            AssertOverride(splitToning.balance, 0f);

            Assert.That(profile.TryGet(out ColorCurves curves), Is.True);
            AssertCurve(
                curves.master,
                new Vector2(0f, 0f),
                new Vector2(0.12f, 0.10f),
                new Vector2(0.28f, 0.32f),
                new Vector2(0.50f, 0.59f),
                new Vector2(0.75f, 0.84f),
                new Vector2(1f, 1f));
            AssertCurve(
                curves.red,
                new Vector2(0f, 0f),
                new Vector2(0.25f, 0.25f),
                new Vector2(0.50f, 0.50f),
                new Vector2(0.75f, 0.75f),
                new Vector2(1f, 1f));
            AssertCurve(
                curves.green,
                new Vector2(0f, 0f),
                new Vector2(0.25f, 0.25f),
                new Vector2(0.50f, 0.50f),
                new Vector2(0.75f, 0.75f),
                new Vector2(1f, 1f));
            AssertCurve(
                curves.blue,
                new Vector2(0f, 0f),
                new Vector2(0.25f, 0.25f),
                new Vector2(0.50f, 0.50f),
                new Vector2(0.75f, 0.75f),
                new Vector2(1f, 1f));
            AssertCurve(
                curves.hueVsHue,
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1f, 0.5f));
            AssertCurve(
                curves.hueVsSat,
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1f, 0.5f));
            AssertCurve(
                curves.satVsSat,
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1f, 0.5f));
            AssertCurve(
                curves.lumVsSat,
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1f, 0.5f));

            Assert.That(profile.TryGet(out ColorAdjustments color), Is.True);
            AssertOverride(color.postExposure, 0.35f);
            AssertOverride(color.contrast, 16f);
            AssertOverride(color.saturation, 8f);
            AssertOverride(color.hueShift, 0f);
            AssertOverride(color.colorFilter, Color.white);

            Assert.That(profile.TryGet(out Bloom bloom), Is.True);
            AssertOverride(bloom.threshold, 0.85f);
            AssertOverride(bloom.intensity, 0.20f);
            AssertOverride(bloom.scatter, 0.65f);
            AssertOverride(bloom.clamp, 4f);
            AssertOverride(bloom.tint, Color.white);
            AssertOverride(bloom.highQualityFiltering, true);

            Assert.That(profile.TryGet(out Vignette vignette), Is.True);
            AssertOverride(vignette.color, Color.black);
            AssertOverride(vignette.center, new Vector2(0.5f, 0.5f));
            AssertOverride(vignette.intensity, 0.04f);
            AssertOverride(vignette.smoothness, 0.50f);
            AssertOverride(vignette.rounded, false);

            var firstSemanticTypes = profile.components
                .Select(item => item.GetType().FullName)
                .ToArray();
            AssetDatabase.DeleteAsset(path);
            var rebuilt = MikuAnimeVolumeProfileFactory.CreateOrReplace(path);
            Assert.That(
                rebuilt.components.Select(item => item.GetType().FullName),
                Is.EqualTo(firstSemanticTypes));
        }

        [Test]
        public void SkinShadersExposeSharedPropertiesAndEndfieldUsesRefineMask()
        {
            foreach (var shaderName in new[]
            {
                "MIKU/Genshin/Body", "MIKU/Genshin/Face",
                "MIKU/HSR/Body", "MIKU/HSR/Face",
                "MIKU/Wuwa/Body", "MIKU/Wuwa/Face",
            })
            {
                var material = new Material(Shader.Find(shaderName));
                try
                {
                    foreach (var property in new[]
                    {
                        "_SkinSSSIntensity", "_SSSColor", "_SSSArea",
                        "_SkinToneBrightness", "_SkinToneWhitening",
                        "_SkinToneTarget", "_SkinMaskDebugMode",
                    })
                        Assert.That(material.HasProperty(property), Is.True, shaderName + ":" + property);
                }
                finally
                {
                    Object.DestroyImmediate(material);
                }
            }

            var source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "../Packages/com.miku.shaderconverter/Runtime/Endfield/EndfieldCommon.hlsl"));
            StringAssert.Contains("saturate(refine.r)", source);
            StringAssert.DoesNotContain("lerp(0.5, 1.5, saturate(refine.r))", source);
        }

        static void AssertOverride(FloatParameter parameter, float expected)
        {
            Assert.That(parameter.overrideState, Is.True);
            Assert.That(parameter.value, Is.EqualTo(expected).Within(0.0001f));
        }

        static void AssertOverride<T>(
            VolumeParameter<T> parameter,
            T expected)
        {
            Assert.That(parameter.overrideState, Is.True);
            Assert.That(parameter.value, Is.EqualTo(expected));
        }

        static void AssertCurve(
            TextureCurveParameter parameter,
            params Vector2[] points)
        {
            Assert.That(parameter.overrideState, Is.True);
            foreach (var point in points)
                Assert.That(
                    parameter.value.Evaluate(point.x),
                    Is.EqualTo(point.y).Within(0.0001f));
        }
    }
}
