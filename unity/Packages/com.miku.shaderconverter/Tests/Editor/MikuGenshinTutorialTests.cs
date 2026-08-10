// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Miku.ShaderConverter.Editor.Tests
{
    public sealed class MikuGenshinTutorialTests
    {
        static string GenshinPath(string name) =>
            Path.Combine(
                Application.dataPath,
                "../Packages/com.miku.shaderconverter/Runtime/Genshin",
                name);

        [Test]
        public void RampRowsFollowTutorialOneFourThreeFiveTwoMapping()
        {
            Assert.That(
                MikuGenshinShaderMath.RampRow(0f, 1f, 4f, 3f, 5f, 2f, 0f),
                Is.EqualTo(0.95f).Within(1e-6f));
            Assert.That(
                MikuGenshinShaderMath.RampRow(0.3f, 1f, 4f, 3f, 5f, 2f, 0f),
                Is.EqualTo(0.65f).Within(1e-6f));
            Assert.That(
                MikuGenshinShaderMath.RampRow(0.5f, 1f, 4f, 3f, 5f, 2f, 0f),
                Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(
                MikuGenshinShaderMath.RampRow(0.7f, 1f, 4f, 3f, 5f, 2f, 0f),
                Is.EqualTo(0.55f).Within(1e-6f));
            Assert.That(
                MikuGenshinShaderMath.RampRow(1f, 1f, 4f, 3f, 5f, 2f, 0f),
                Is.EqualTo(0.85f).Within(1e-6f));
            Assert.That(
                MikuGenshinShaderMath.RampRow(0f, 1f, 4f, 3f, 5f, 2f, 1f),
                Is.EqualTo(0.45f).Within(1e-6f));
        }

        [Test]
        public void OutlineVertexMaskPrefersAlphaWithGreenFallback()
        {
            Assert.That(
                MikuGenshinShaderMath.OutlineVertexMask(
                    new Color(0f, 0.2f, 0f, 0.4f)),
                Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(
                MikuGenshinShaderMath.OutlineVertexMask(
                    new Color(0f, 0.2f, 0f, 0f)),
                Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void OutlineRegionColorsFollowLightMapAlphaChain()
        {
            var c0 = new Color(0.1f, 0f, 0f, 1f);
            var c1 = new Color(0f, 0.2f, 0f, 1f);
            var c2 = new Color(0f, 0f, 0.3f, 1f);
            var c3 = new Color(0.4f, 0.4f, 0f, 1f);
            var c4 = new Color(0.5f, 0f, 0.5f, 1f);
            Assert.That(
                MikuGenshinShaderMath.OutlineRegionColor(
                    0f, c0, c1, c2, c3, c4, true),
                Is.EqualTo(c0));
            Assert.That(
                MikuGenshinShaderMath.OutlineRegionColor(
                    0.3f, c0, c1, c2, c3, c4, true),
                Is.EqualTo(c1));
            Assert.That(
                MikuGenshinShaderMath.OutlineRegionColor(
                    0.5f, c0, c1, c2, c3, c4, true),
                Is.EqualTo(c2));
            Assert.That(
                MikuGenshinShaderMath.OutlineRegionColor(
                    0.7f, c0, c1, c2, c3, c4, true),
                Is.EqualTo(c3));
            Assert.That(
                MikuGenshinShaderMath.OutlineRegionColor(
                    1f, c0, c1, c2, c3, c4, true),
                Is.EqualTo(c4));
        }

        [Test]
        public void DiffuseAlphaMasksMatchTutorialSmoothsteps()
        {
            Assert.That(
                MikuGenshinShaderMath.DiffuseAlphaEmissionMask(0.25f),
                Is.EqualTo(0.15625f).Within(1e-6f));
            Assert.That(
                MikuGenshinShaderMath.DiffuseAlphaEmissionMask(0.5f),
                Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(
                MikuGenshinShaderMath.DiffuseAlphaClipMask(0.375f),
                Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(
                MikuGenshinShaderMath.DiffuseAlphaClipMask(0.05f),
                Is.EqualTo(0f).Within(1e-6f));
            Assert.That(
                MikuGenshinShaderMath.DiffuseAlphaClipMask(0.7f),
                Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void NormalMapDecodeMatchesTutorialScale()
        {
            var unit = new Vector3(0.5f, 0f, 0.8660254f);
            var decoded = MikuGenshinShaderMath.DecodeNormalMap(unit, 1f);
            Assert.That(
                Vector3.Dot(decoded, unit),
                Is.GreaterThanOrEqualTo(1f - 1e-5f));
            Assert.That(
                MikuGenshinShaderMath.DecodeNormalMap(unit, 0f),
                Is.EqualTo(Vector3.forward));
            var doubled = MikuGenshinShaderMath.DecodeNormalMap(unit, 2f);
            Assert.That(doubled.x, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(doubled.y, Is.Zero);
            Assert.That(doubled.z, Is.Zero);
        }

        [Test]
        public void SkinToneGatingOnlyAffectsMaskedRegions()
        {
            var blue = new Color(0.36f, 0.48f, 0.71f, 1f);
            var purpleTone = new Color(0.82f, 0.55f, 0.65f, 1f);
            Assert.That(
                MikuGenshinShaderMath.MaskedSkinTone(blue, purpleTone, 0f),
                Is.EqualTo(blue));
            Assert.That(
                MikuGenshinShaderMath.MaskedSkinTone(blue, purpleTone, 1f),
                Is.EqualTo(purpleTone));
            var half = MikuGenshinShaderMath.MaskedSkinTone(
                blue,
                purpleTone,
                0.5f);
            Assert.That(half.r, Is.EqualTo(0.59f).Within(1e-6f));
        }

        [Test]
        public void BackFaceUvSelectionMatchesTutorialSecondUvSet()
        {
            var uv0 = new Vector2(0.1f, 0.2f);
            var uv1 = new Vector2(0.9f, 0.8f);
            Assert.That(
                MikuGenshinShaderMath.BackFaceUv(uv0, uv1, true, true),
                Is.EqualTo(uv0));
            Assert.That(
                MikuGenshinShaderMath.BackFaceUv(uv0, uv1, false, true),
                Is.EqualTo(uv1));
            Assert.That(
                MikuGenshinShaderMath.BackFaceUv(uv0, uv1, false, false),
                Is.EqualTo(uv0));
        }

        [Test]
        public void GenshinSourcesExposeTutorialContracts()
        {
            var common = File.ReadAllText(GenshinPath("GenshinCommon.hlsl"));
            foreach (var contract in new[]
            {
                "Genshin_TutorialRampRow",
                "Genshin_OutlineVertexMask",
                "Genshin_OutlineRegionColor",
                "Genshin_DiffuseAlphaEmission",
                "Genshin_DiffuseAlphaClip",
            })
                StringAssert.Contains(contract, common);

            var body = File.ReadAllText(GenshinPath("Genshin_Body.shader"));
            var hair = File.ReadAllText(GenshinPath("Genshin_Hair.shader"));
            var face = File.ReadAllText(GenshinPath("Genshin_Face.shader"));
            foreach (var source in new[] { body, hair })
            {
                StringAssert.Contains("_GENSHIN_NORMALMAP_ON", source);
                StringAssert.Contains("_NormalMap", source);
                StringAssert.Contains("_BumpScale", source);
                StringAssert.Contains("Genshin_DecodeNormalMap", source);
                StringAssert.Contains("TransformTangentToWorld", source);
                StringAssert.Contains("tangentWS : TEXCOORD6", source);
                StringAssert.Contains("bitangentWS : TEXCOORD7", source);
            }
            StringAssert.Contains(
                "lerp(diffuse, Genshin_ReferenceSkinTone(" +
                "diffuse, _HighlightCompression), skinMask)",
                body);
            StringAssert.DoesNotContain("_GENSHIN_NORMALMAP_ON", face);
            foreach (var source in new[] { body, hair, face })
            {
                StringAssert.Contains("_DiffuseA", source);
                StringAssert.Contains("_Cutoff", source);
                StringAssert.Contains("_Glow", source);
                StringAssert.Contains("_Flicker", source);
                StringAssert.Contains("_LightmapA0", source);
                StringAssert.Contains("_OutlineColorMode", source);
                StringAssert.Contains(
                    "Genshin_OutlineVertexMask(input.vertexColor)",
                    source);
            }
            StringAssert.Contains("_GENSHIN_DOUBLE_SIDED", body);
            StringAssert.Contains("SV_IsFrontFace", body);
            StringAssert.Contains("float2 uv1 : TEXCOORD1", body);
            StringAssert.Contains("_GENSHIN_DOUBLE_SIDED", hair);
            StringAssert.Contains("SV_IsFrontFace", hair);
            StringAssert.Contains("float2 uv1 : TEXCOORD1", hair);
            StringAssert.DoesNotContain("_GENSHIN_DOUBLE_SIDED", face);
        }

        [Test]
        public void GenshinShadersCompileWithTutorialKeywords()
        {
            var cases = new[]
            {
                new
                {
                    Name = "MIKU/Genshin/Body",
                    Keywords = new[]
                    {
                        "_AREA_UPPER_BODY", "_AREA_LOWER_BODY",
                        "_AREA_CLOTH", "_AREA_SKIN",
                        "_GENSHIN_DOUBLE_SIDED", "_GENSHIN_METALMAP_ON",
                        "_GENSHIN_NORMALMAP_ON", "_GENSHIN_EMISSION_ON",
                    },
                },
                new
                {
                    Name = "MIKU/Genshin/Hair",
                    Keywords = new[]
                    {
                        "_AREA_HAIR", "_GENSHIN_DOUBLE_SIDED",
                        "_GENSHIN_METALMAP_ON", "_GENSHIN_NORMALMAP_ON",
                        "_GENSHIN_EMISSION_ON",
                    },
                },
                new
                {
                    Name = "MIKU/Genshin/Face",
                    Keywords = new[]
                    {
                        "_AREA_FACE", "_GENSHIN_EMISSION_ON",
                    },
                },
            };
            foreach (var item in cases)
            {
                var shader = Shader.Find(item.Name);
                Assert.That(shader, Is.Not.Null, item.Name);
                var material = new Material(shader);
                try
                {
                    foreach (var keyword in item.Keywords)
                        material.EnableKeyword(keyword);
                    material.SetFloat("_DiffuseA", 2f);
                    material.SetFloat("_OutlineColorMode", 1f);
                    if (material.HasProperty("_Cull"))
                        material.SetInt("_Cull", 0);
                    Assert.That(
                        ShaderUtil.ShaderHasError(shader),
                        Is.False,
                        item.Name);
                }
                finally
                {
                    Object.DestroyImmediate(material);
                }
            }
        }
    }
}
