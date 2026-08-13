// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Miku.ShaderConverter.Runtime.Genshin;
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
        public void OutlineVertexMaskUsesMikuGreenChannel()
        {
            Assert.That(
                MikuGenshinShaderMath.OutlineVertexMask(
                    new Color(0f, 0.2f, 0f, 0.4f)),
                Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(
                MikuGenshinShaderMath.OutlineVertexMask(
                    new Color(0f, 0.2f, 0f, 0f)),
                Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void TutorialAoHalfLambertAndSpecularMatchContract()
        {
            Assert.That(MikuGenshinShaderMath.TutorialAo(0.2f), Is.Zero);
            Assert.That(MikuGenshinShaderMath.TutorialAo(0.25f),
                Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(MikuGenshinShaderMath.TutorialAo(0.3f), Is.EqualTo(1f));
            Assert.That(
                MikuGenshinShaderMath.TutorialLightingSignal(
                    0.64f, 0.3f, 0.5f, 1.14f),
                Is.EqualTo(1f).Within(1e-6f));
            var specular = MikuGenshinShaderMath.TutorialSpecular(
                new Color(0.8f, 0.5f, 0.25f, 1f),
                1f, 0.5f, 1f, 16f, 2f, 1f, 0.5f);
            Assert.That(specular.r, Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(specular.g, Is.EqualTo(0.25f).Within(1e-6f));
        }

        [Test]
        public void RealtimeShadowBlendsFinalColorWithoutChangingToonSignal()
        {
            var signal = MikuGenshinShaderMath.TutorialLightingSignal(
                0.2f, 0.3f, 0.5f, 1.14f);
            Assert.That(signal, Is.EqualTo(
                MikuGenshinShaderMath.TutorialLightingSignal(
                    0.2f, 0.3f, 0.5f, 1.14f)));
            Assert.That(
                MikuGenshinShaderMath.TutorialLightingSignal(
                    0.2f, 0.3f, 0.5f, 1.14f, 0f),
                Is.EqualTo(signal),
                "The compatible overload must not feed visibility into the ramp coordinate.");

            var toon = new Color(0.8f, 0.6f, 0.4f, 1f);
            var darkest = new Color(0.2f, 0.1f, 0.05f, 1f);
            Assert.That(
                MikuGenshinShaderMath.ApplyMainShadow(
                    toon, darkest, 0f, 0f),
                Is.EqualTo(toon));
            Assert.That(
                MikuGenshinShaderMath.ApplyMainShadow(
                    toon, darkest, 0f, 1f),
                Is.EqualTo(darkest));
            Assert.That(
                MikuGenshinShaderMath.ApplyMainShadow(
                    toon, darkest, 0.5f, 1f),
                Is.EqualTo(Color.Lerp(toon, darkest, 0.5f)));
            Assert.That(
                MikuGenshinShaderMath.MainShadowVisibility(0.4f, 1f, 0f),
                Is.EqualTo(1f));
            Assert.That(
                MikuGenshinShaderMath.MainShadowVisibility(0.4f, 1f, 1f),
                Is.EqualTo(0.4f).Within(1e-6f));
        }

        [Test]
        public void ShadowSmoothUsesAuthoredNormalizedWidth()
        {
            var sample = 0.95f;
            var narrow = MikuGenshinShaderMath.ToonTransition(sample, 0.01f);
            var wide = MikuGenshinShaderMath.ToonTransition(sample, 0.12f);
            Assert.That(narrow, Is.Zero);
            Assert.That(wide, Is.GreaterThan(0f));
            Assert.That(wide, Is.LessThan(1f));
        }

        [Test]
        public void TutorialMetalMatchesEnvironmentMapContract()
        {
            var baseColor = new Color(0.8f, 0.4f, 0.2f, 1f);
            var environment = new Color(0.1f, 0.2f, 0.4f, 1f);
            Assert.That(MikuGenshinShaderMath.TutorialMetalMask(0.9f), Is.Zero);
            Assert.That(MikuGenshinShaderMath.TutorialMetalMask(0.9001f), Is.EqualTo(1f));
            Assert.That(
                MikuGenshinShaderMath.TutorialMetal(
                    baseColor, 1f, 0f, environment, 1f),
                Is.EqualTo(environment));
            Assert.That(
                MikuGenshinShaderMath.TutorialMetal(
                    baseColor, 1f, 1f, environment, 2f),
                Is.EqualTo(baseColor * 2f));
            Assert.That(
                MikuGenshinShaderMath.TutorialMetal(
                    baseColor, 0.9f, 1f, environment, 2f),
                Is.EqualTo(Color.clear));
        }

        [Test]
        public void TextureAuditClassifiesFurinaRoleSuffixes()
        {
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyGenshinFileName(
                    "Avatar_Girl_Tex_FaceLightmap.png"),
                Is.EqualTo(
                    MikuGenshinTextureProfile.FaceSdfLinearRepeatNoMips));
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyGenshinFileName(
                    "Avatar_Girl_Sword_Funingna_Body_Shadow_Ramp.png"),
                Is.EqualTo(MikuGenshinTextureProfile.RampColorClampNoMips));
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyGenshinFileName(
                    "Face_SDF_Official.tga"),
                Is.EqualTo(MikuGenshinTextureProfile.FaceSdfLinearRepeatNoMips));
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyGenshinFileName(
                    "Skin_Ramp.png"),
                Is.EqualTo(MikuGenshinTextureProfile.RampColorClampNoMips));
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyGenshinFileName(
                    "Avatar_Girl_Sword_Funingna_Body_Normalmap.png"),
                Is.EqualTo(MikuGenshinTextureProfile.NormalMap));
        }

        [Test]
        public void GenshinRecommendedProfileWritesShadowAndMetalDefaults()
        {
            var body = new Material(Shader.Find("MIKU/Genshin/Body"));
            var hair = new Material(Shader.Find("MIKU/Genshin/Hair"));
            var face = new Material(Shader.Find("MIKU/Genshin/Face"));
            try
            {
                body.SetFloat("_MetalIntensity", 2f);
                body.SetFloat("_MainShadowInfluence", 1f);
                hair.SetFloat("_MainShadowInfluence", 1f);
                face.SetFloat("_MainShadowInfluence", 1f);
                Assert.That(
                    MikuGameToonMaterialProfiles.ApplyRecommended(body, false),
                    Is.True);
                Assert.That(
                    MikuGameToonMaterialProfiles.ApplyRecommended(hair, false),
                    Is.True);
                Assert.That(
                    MikuGameToonMaterialProfiles.ApplyRecommended(face, false),
                    Is.True);
                Assert.That(body.GetFloat("_MetalIntensity"), Is.EqualTo(1f));
                Assert.That(
                    body.GetFloat("_MainShadowInfluence"),
                    Is.EqualTo(0.25f));
                Assert.That(
                    hair.GetFloat("_MainShadowInfluence"),
                    Is.EqualTo(0.35f));
                Assert.That(face.GetFloat("_MainShadowInfluence"), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(hair);
                Object.DestroyImmediate(face);
            }
        }

        [Test]
        public void TextureAuditPreservesNpotRampRowsAndIsIdempotent()
        {
            const string folder = "Assets/MikuGenshinTextureAuditTests";
            const string texturePath =
                folder + "/Avatar_Hutao_Body_Shadow_Ramp.png";
            const string reportPath = folder + "/audit.json";
            if (AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder);
            AssetDatabase.CreateFolder("Assets", "MikuGenshinTextureAuditTests");
            var source = new Texture2D(256, 20, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color[256 * 20];
                for (var y = 0; y < 20; y++)
                for (var x = 0; x < 256; x++)
                    pixels[y * 256 + x] = new Color(y / 19f, x / 255f, 0.5f, 1f);
                source.SetPixels(pixels);
                source.Apply(false, false);
                File.WriteAllBytes(
                    Path.Combine(
                        Directory.GetParent(Application.dataPath)?.FullName ?? "",
                        texturePath),
                    source.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(source);
            }

            try
            {
                AssetDatabase.ImportAsset(
                    texturePath,
                    ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                Assert.That(importer, Is.Not.Null);
                importer.npotScale = TextureImporterNPOTScale.ToNearest;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.crunchedCompression = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                var standalone = importer.GetPlatformTextureSettings("Standalone");
                standalone.overridden = true;
                standalone.maxTextureSize = 128;
                importer.SetPlatformTextureSettings(standalone);
                importer.SaveAndReimport();

                Assert.That(
                    MikuGameToonTextureImportAuditWindow.ApplyGenshinFolder(
                        folder,
                        reportPath),
                    Is.EqualTo(1));
                importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                var ramp = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                Assert.That(importer, Is.Not.Null);
                Assert.That(ramp, Is.Not.Null);
                Assert.That(ramp.width, Is.EqualTo(256));
                Assert.That(ramp.height, Is.EqualTo(20));
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
                Assert.That(
                    importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(importer.crunchedCompression, Is.False);
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(
                    importer.GetPlatformTextureSettings("Standalone").overridden,
                    Is.False);
                Assert.That(
                    MikuGameToonTextureImportAuditWindow.ApplyGenshinFolder(
                        folder,
                        reportPath),
                    Is.Zero);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
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
            StringAssert.Contains("MikuGenshinBackface", body);
            StringAssert.Contains("MikuToonOutline", body);
            StringAssert.DoesNotContain("SV_IsFrontFace", body);
            StringAssert.Contains("float2 uv1 : TEXCOORD1", body);
            StringAssert.Contains("MikuGenshinBackface", hair);
            StringAssert.Contains("MikuToonOutline", hair);
            StringAssert.DoesNotContain("SV_IsFrontFace", hair);
            StringAssert.Contains("float2 uv1 : TEXCOORD1", hair);
            StringAssert.DoesNotContain("_GENSHIN_DOUBLE_SIDED", face);
        }

        [Test]
        public void EveryGenshinMainLightProgramDeclaresTheUrpForwardPlusVariant()
        {
            var cases = new[]
            {
                new { File = "Genshin_Body.shader", ExpectedPrograms = 2 },
                new { File = "Genshin_Hair.shader", ExpectedPrograms = 2 },
                new { File = "Genshin_Face.shader", ExpectedPrograms = 1 },
            };
            foreach (var item in cases)
            {
                var source = File.ReadAllText(GenshinPath(item.File));
                var mainLightPrograms = HlslPrograms(source)
                    .FindAll(program => program.Contains("GetMainLight("));
                Assert.That(
                    mainLightPrograms,
                    Has.Count.EqualTo(item.ExpectedPrograms),
                    item.File);
                foreach (var program in mainLightPrograms)
                {
                    const string versionCondition =
                        "#if UNITY_VERSION >= 60010000";
                    const string clusterPragma =
                        "#pragma multi_compile _ _CLUSTER_LIGHT_LOOP";
                    const string forwardPlusPragma =
                        "#pragma multi_compile _ _FORWARD_PLUS";
                    Assert.That(
                        Count(program, versionCondition),
                        Is.EqualTo(1),
                        item.File);
                    Assert.That(
                        Count(program, clusterPragma),
                        Is.EqualTo(1),
                        item.File);
                    Assert.That(
                        Count(program, forwardPlusPragma),
                        Is.EqualTo(1),
                        item.File);
                    var condition = program.IndexOf(
                        versionCondition,
                        StringComparison.Ordinal);
                    var cluster = program.IndexOf(
                        clusterPragma,
                        condition,
                        StringComparison.Ordinal);
                    var alternate = program.IndexOf(
                        "#else",
                        cluster,
                        StringComparison.Ordinal);
                    var forwardPlus = program.IndexOf(
                        forwardPlusPragma,
                        alternate,
                        StringComparison.Ordinal);
                    var end = program.IndexOf(
                        "#endif",
                        forwardPlus,
                        StringComparison.Ordinal);
                    Assert.That(cluster, Is.GreaterThan(condition), item.File);
                    Assert.That(alternate, Is.GreaterThan(cluster), item.File);
                    Assert.That(
                        forwardPlus,
                        Is.GreaterThan(alternate),
                        item.File);
                    Assert.That(end, Is.GreaterThan(forwardPlus), item.File);
                    StringAssert.Contains(
                        "#pragma multi_compile _ _MAIN_LIGHT_SHADOWS " +
                        "_MAIN_LIGHT_SHADOWS_CASCADE " +
                        "_MAIN_LIGHT_SHADOWS_SCREEN",
                        program,
                        item.File);
                    StringAssert.Contains(
                        "#pragma multi_compile_fragment _ _SHADOWS_SOFT " +
                        "_SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM " +
                        "_SHADOWS_SOFT_HIGH",
                        program,
                        item.File);
                    StringAssert.Contains(
                        "output.shadowCoord = GetShadowCoord(pos);",
                        program,
                        item.File);
                    StringAssert.DoesNotContain(
                        "TransformWorldToShadowCoord(pos.positionWS)",
                        program,
                        item.File);
                }
            }
        }

        [Test]
        public void GenshinShadersSeparateToonShadowAndEnvironmentMetalContracts()
        {
            var common = File.ReadAllText(GenshinPath("GenshinCommon.hlsl"));
            StringAssert.Contains(
                "float Genshin_MainShadowVisibility(",
                common);
            StringAssert.Contains("float3 Genshin_ApplyMainShadow(", common);
            StringAssert.Contains(
                "float transition = max(0.001, bodyShadowSmooth);",
                common);
            StringAssert.Contains(
                "float transition = max(0.001, hairShadowSmooth);",
                common);
            StringAssert.Contains(
                "return 1.0 - step(lightMapRed, 0.9);",
                common);
            StringAssert.Contains(
                "return matCap * mask * max(metalIntensity, 0.0);",
                common);
            StringAssert.DoesNotContain("bodyShadowSmooth * 0.02", common);
            StringAssert.DoesNotContain("hairShadowSmooth * 0.02", common);

            foreach (var item in new[]
                     {
                         new { File = "Genshin_Body.shader", Influence = "0.25" },
                         new { File = "Genshin_Hair.shader", Influence = "0.35" },
                         new { File = "Genshin_Face.shader", Influence = "0" },
                     })
            {
                var source = File.ReadAllText(GenshinPath(item.File));
                StringAssert.Contains(
                    "_MainShadowInfluence (\"Realtime Main Shadow Influence\", Range(0,1)) = " +
                    item.Influence,
                    source,
                    item.File);
            }
        }

        [Test]
        public void GenshinShadersImportWithoutErrorsAndExposeTutorialKeywords()
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
                        "_GENSHIN_METALMAP_ON",
                        "_GENSHIN_NORMALMAP_ON", "_GENSHIN_EMISSION_ON",
                    },
                },
                new
                {
                    Name = "MIKU/Genshin/Hair",
                    Keywords = new[]
                    {
                        "_AREA_HAIR",
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
                foreach (var keyword in item.Keywords)
                {
                    Assert.That(
                        shader.keywordSpace.FindKeyword(keyword).isValid,
                        Is.True,
                        item.Name + ":" + keyword);
                }
                Assert.That(
                    ShaderUtil.ShaderHasError(shader),
                    Is.False,
                    item.Name);
            }
        }

        [Test]
        public void MaterialStateSettersSynchronizeAlphaAndUv1Backface()
        {
            var shader = Shader.Find("MIKU/Genshin/Body");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                MikuGenshinMaterialState.SetAlphaMode(
                    material,
                    MikuGenshinAlphaMode.DiffuseAlphaEmission);
                Assert.That(material.GetFloat("_DiffuseA"), Is.EqualTo(2f));
                Assert.That(
                    MikuGenshinMaterialState.GetAlphaMode(material),
                    Is.EqualTo(MikuGenshinAlphaMode.DiffuseAlphaEmission));
                MikuGenshinMaterialState.SetUv1Backface(material, true);
                Assert.That(material.GetFloat("_UseUv1Backface"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_DoubleSided"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_BackUV1"), Is.EqualTo(1f));
                Assert.That(material.GetShaderPassEnabled("MikuGenshinBackface"), Is.True);
                Assert.That(material.IsKeywordEnabled("_GENSHIN_DOUBLE_SIDED"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void KeywordSynchronizationKeepsUnlitEyeSupported()
        {
            var shader = Shader.Find("MIKU/Genshin/Eye");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(material.HasProperty("_DiffuseA"), Is.False);
                Assert.DoesNotThrow(() =>
                    MikuManualTextureKeywordUtility.SyncKeywords(material));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        static List<string> HlslPrograms(string source)
        {
            var programs = new List<string>();
            var searchFrom = 0;
            while (true)
            {
                var start = source.IndexOf(
                    "HLSLPROGRAM",
                    searchFrom,
                    StringComparison.Ordinal);
                if (start < 0)
                    return programs;
                var end = source.IndexOf(
                    "ENDHLSL",
                    start,
                    StringComparison.Ordinal);
                Assert.That(end, Is.GreaterThan(start));
                programs.Add(source.Substring(start, end - start));
                searchFrom = end + "ENDHLSL".Length;
            }
        }

        static int Count(string source, string value)
        {
            var count = 0;
            var searchFrom = 0;
            while (true)
            {
                var index = source.IndexOf(
                    value,
                    searchFrom,
                    StringComparison.Ordinal);
                if (index < 0)
                    return count;
                count++;
                searchFrom = index + value.Length;
            }
        }

    }
}
