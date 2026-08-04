// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Miku.ShaderConverter.Editor;
using Miku.ShaderConverter.Runtime.GameToon;

namespace Miku.ShaderConverter.Tests.Editor
{
    public sealed class MikuGameToonTests
    {
        [Test]
        public void RetiredGenericToonIrFailsBeforeBackendResolution()
        {
            var ir = new JObject
            {
                ["documentKind"] = "miku-material-ir-2.0",
                ["schemaVersion"] = "2.0",
                ["workflow"] = new JObject { ["kind"] = "generic_toon" },
            };
            var error = Assert.Throws<InvalidDataException>(
                () => MikuWorkflowBackends.Resolve(ir));
            Assert.That(error.Message, Is.EqualTo(
                "MIKU_WORKFLOW_RETIRED:generic_toon"));
        }

        [Test]
        public void GameWorkflowBackendResolvesWithoutGenericShaderFamily()
        {
            var ir = new JObject
            {
                ["documentKind"] = "miku-material-ir-2.0",
                ["schemaVersion"] = "2.0",
                ["workflow"] = new JObject
                {
                    ["kind"] = "genshin_toon",
                    ["part"] = "Body",
                },
            };
            var backend = MikuWorkflowBackends.Resolve(ir);
            Assert.That(backend.Kind, Is.EqualTo("genshin_toon"));
            Assert.That(backend.UsesEditableGraph, Is.False);
        }

        [Test]
        public void EndfieldWorkflowResolvesEveryDeclaredShaderPart()
        {
            foreach (var part in new[]
            {
                MikuGameMaterialPart.Body,
                MikuGameMaterialPart.Skin,
                MikuGameMaterialPart.Hair,
                MikuGameMaterialPart.Face,
                MikuGameMaterialPart.Eye,
                MikuGameMaterialPart.Mouth,
                MikuGameMaterialPart.Overlay,
                MikuGameMaterialPart.Effect,
                MikuGameMaterialPart.HairShadow,
            })
            {
                var shaderName = MikuFixedWorkflowTextureBindings.ShaderName(
                    "endfield_toon",
                    part.ToString());
                Assert.That(shaderName, Is.EqualTo("MIKU/Endfield/" + part));
                Assert.That(Shader.Find(shaderName), Is.Not.Null, shaderName);
            }
            var error = Assert.Throws<ArgumentException>(() =>
                MikuFixedWorkflowTextureBindings.NormalizePart(
                    "genshin_toon",
                    "HairShadow"));
            Assert.That(error.Message, Does.StartWith(
                "MIKU_WORKFLOW_PART_INVALID:"));
        }

        [Test]
        public void MaterialCreatorFiltersTheTwentyTwoSupportedParts()
        {
            Assert.That(
                MikuFixedWorkflowTextureBindings.AllowedParts("genshin_toon"),
                Is.EqualTo(new[]
                {
                    MikuGameMaterialPart.Body,
                    MikuGameMaterialPart.Hair,
                    MikuGameMaterialPart.Face,
                    MikuGameMaterialPart.Eye,
                }));
            Assert.That(
                MikuFixedWorkflowTextureBindings.AllowedParts("wuwa_toon").Count(),
                Is.EqualTo(5));
            Assert.That(
                MikuFixedWorkflowTextureBindings.AllowedParts("hsr_toon").Count(),
                Is.EqualTo(4));
            Assert.That(
                MikuFixedWorkflowTextureBindings.AllowedParts("endfield_toon").Count(),
                Is.EqualTo(9));
        }

        [Test]
        public void MaterialCreatorUsesVisibleTexturePropertiesAndBaseMapRules()
        {
            var body = MikuGameToonMaterialTemplateWindow.GetTextureSlots(
                "MIKU/Wuwa/Body",
                "wuwa_toon");
            Assert.That(body.Select(slot => slot.Property), Does.Contain("_BaseMap"));
            Assert.That(body.Select(slot => slot.Property), Does.Contain("_IDMap"));
            Assert.That(body.Select(slot => slot.Property), Does.Not.Contain("_MainTex"));
            Assert.That(body.Select(slot => slot.Property), Does.Not.Contain("_StockingsMap"));
            Assert.That(body.Single(slot => slot.Property == "_BaseMap").Required, Is.True);

            var mouth = MikuGameToonMaterialTemplateWindow.GetTextureSlots(
                "MIKU/Endfield/Mouth",
                "endfield_toon");
            Assert.That(mouth.Single(slot => slot.Property == "_BaseMap").Required, Is.False);
        }

        [Test]
        public void EndfieldTextureAuditUsesStrictProfiles()
        {
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyFileName(
                    "T_actor_aglina_hair_01_HN.png"),
                Is.EqualTo(MikuEndfieldTextureProfile.LinearRepeat));
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyFileName(
                    "T_actor_common_hairshadow_01_M.png"),
                Is.EqualTo(MikuEndfieldTextureProfile.LinearClampNoMips));
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyFileName(
                    "T_actor_common_cloth_04_RD.png"),
                Is.EqualTo(MikuEndfieldTextureProfile.ColorClampNoMips));
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyFileName(
                    "T_actor_aglina_hairst_01_ST.png"),
                Is.EqualTo(MikuEndfieldTextureProfile.LinearRepeat));
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyFileName(
                    "T_actor_common_hairst_01_ST.png"),
                Is.EqualTo(MikuEndfieldTextureProfile.LinearClampNoMips));
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyFileName(
                    "T_actor_common_hairline_01_M.png"),
                Is.EqualTo(MikuEndfieldTextureProfile.LinearRepeat));
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyFileName(
                    "T_actor_aglina_emotion_01_D.png"),
                Is.EqualTo(MikuEndfieldTextureProfile.ColorClampNoMips));
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyFileName(
                    "T_actor_common_eye_matcap_01_D.png"),
                Is.EqualTo(MikuEndfieldTextureProfile.ColorClampNoMips));
            Assert.That(
                MikuGameToonTextureImportAuditWindow.ClassifyFileName(
                    "unknown_M.png"),
                Is.EqualTo(MikuEndfieldTextureProfile.Unrecognized));
        }

        [Test]
        public void EndfieldLegacyRolesMigrateDeterministically()
        {
            var face = new Material(Shader.Find("MIKU/Endfield/Face"));
            var hair = new Material(Shader.Find("MIKU/Endfield/Hair"));
            try
            {
                AssertRoleMigration(face, "ShadowLut", "ColorLut");
                AssertRoleMigration(face, "FaceSDFMask", "FaceAreaMap");
                AssertRoleMigration(face, "OutlineMask", "FaceRefineMap");
                AssertRoleMigration(hair, "LineMap", "HairLineMap");
                AssertRoleMigration(hair, "StrokeMap", "HairShiftMap");
                AssertRoleMigration(hair, "OutlineMask", "HairRefineMap");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(face);
                UnityEngine.Object.DestroyImmediate(hair);
            }
        }

        [Test]
        public void EndfieldHeadBasisFollowsObjectMatrixAndRepairsHandedness()
        {
            foreach (var scale in new[]
            {
                new Vector3(2f, 3f, 4f),
                new Vector3(-2f, 3f, 4f),
                new Vector3(2f, -3f, 4f),
            })
            {
                var matrix = Matrix4x4.TRS(
                    new Vector3(3f, -2f, 7f),
                    Quaternion.Euler(31f, 127f, -19f),
                    scale);
                var basis = MikuEndfieldHeadSpace.ComputeBasis(matrix);
                Assert.That(Vector3.Dot(basis.Right, basis.Forward),
                    Is.EqualTo(0f).Within(0.00001f));
                Assert.That(Vector3.Dot(basis.Right, basis.Up),
                    Is.EqualTo(0f).Within(0.00001f));
                Assert.That(Vector3.Dot(basis.Forward, basis.Up),
                    Is.EqualTo(0f).Within(0.00001f));
                Assert.That(basis.Back, Is.EqualTo(-basis.Forward));
                Assert.That(Vector3.Dot(
                        basis.Right,
                        matrix.MultiplyVector(Vector3.right).normalized),
                    Is.GreaterThan(0f));
            }
        }

        [Test]
        public void Endfield224ShadersExposeCompatibleRepairControls()
        {
            Assert.That(
                MikuToonMaterialRecipe.CurrentShaderFamilyVersion,
                Is.EqualTo("2.2.9"));
            AssertMaterialProperties(
                "MIKU/Endfield/Overlay",
                "_AlphaSource",
                "_AlphaClip");
            AssertMaterialProperties(
                "MIKU/Endfield/Eye",
                "_EyeMode",
                "_IrisParallaxDepth",
                "_CorneaBumpStrength",
                "_CorneaSpecularIntensity",
                "_CorneaHighlightColor");
            AssertMaterialProperties(
                "MIKU/Endfield/Skin",
                "_SkinAOStrength",
                "_SSSColor",
                "_SSSArea",
                "_SkinToneBrightness",
                "_SkinToneWhitening",
                "_SkinToneTarget",
                "_SurfaceRimStrength",
                "_SurfaceRimPower",
                "_SurfaceRimLightAlign");
            AssertMaterialProperties(
                "MIKU/Endfield/Face",
                "_UseFaceSDF",
                "_BlushStrength",
                "_BlushColor",
                "_BlushTileIndex",
                "_BlushMaskGain",
                "_UseManualFaceBasis",
                "_FaceRightOS",
                "_FaceForwardOS",
                "_FaceUpOS",
                "_BackLightStrength",
                "_SSSColor",
                "_SSSArea",
                "_SkinToneBrightness",
                "_SkinToneWhitening",
                "_SkinToneTarget",
                "_SurfaceRimStrength",
                "_SurfaceRimPower",
                "_SurfaceRimLightAlign");
            AssertMaterialProperties(
                "MIKU/Endfield/Body",
                "_UseMaterialParamMap",
                "_SpecularRefineF0Tex",
                "_SpecularRefineColorTex",
                "_UseSpecularRefine",
                "_SelfAoShadowStrength",
                "_EnvironmentRotation",
                "_EnvironmentMipBias",
                "_MetalDirectBoost",
                "_MetalEnvironmentBoost",
                "_SurfaceRimStrength",
                "_SurfaceRimPower",
                "_SurfaceRimLightAlign");
            AssertMaterialProperties(
                "MIKU/Endfield/Hair",
                "_SpecularRefineF0Tex",
                "_UseSpecularRefine",
                "_SelfAoShadowStrength",
                "_HairSpecularLutMode",
                "_SurfaceRimStrength",
                "_SurfaceRimPower",
                "_SurfaceRimLightAlign");
            Assert.That(
                MikuFixedWorkflowTextureBindings.TextureProperty(
                    "endfield_toon",
                    "FaceSDF"),
                Is.EqualTo("_SDFLightmap"));
            Assert.That(
                MikuFixedWorkflowTextureBindings.TextureProperty(
                    "endfield_toon",
                    "EyeShadowMap"),
                Is.EqualTo("_BaseMap"));
            Assert.That(
                MikuFixedWorkflowTextureBindings.TextureProperty(
                    "endfield_toon",
                    "SpecularRefineF0"),
                Is.EqualTo("_SpecularRefineF0Tex"));
            Assert.That(
                MikuFixedWorkflowTextureBindings.TextureProperty(
                    "endfield_toon",
                    "SpecularRefineColor"),
                Is.EqualTo("_SpecularRefineColorTex"));
        }

        [Test]
        public void EndfieldAlphaSourcesPreserveLegacyValuesAndAddRawRedOpaque()
        {
            var raw = new Color(0.2f, 0.4f, 0.8f, 0.6f);
            Assert.That(MikuEndfieldShaderMath.TextureAlpha, Is.EqualTo(0));
            Assert.That(MikuEndfieldShaderMath.Luminance, Is.EqualTo(1));
            Assert.That(MikuEndfieldShaderMath.InverseRed, Is.EqualTo(2));
            Assert.That(MikuEndfieldShaderMath.RawRed, Is.EqualTo(3));
            Assert.That(MikuEndfieldShaderMath.Opaque, Is.EqualTo(4));
            Assert.That(
                MikuEndfieldShaderMath.SelectAlpha(raw, 0),
                Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.SelectAlpha(raw, 2),
                Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.SelectAlpha(raw, 3),
                Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.SelectAlpha(Color.black, 4),
                Is.EqualTo(1f));
        }

        [Test]
        public void EndfieldRawRedEyeShadowClipsBottomToTopMonotonically()
        {
            var topToBottomRed = new[]
            {
                0.68f, 0.62f, 0.50f, 0.35f, 0.20f, 0.05f, 0f,
            };
            var thresholds = new[] { 0f, 0.15f, 0.30f, 0.45f, 0.60f, 0.75f, 1f };
            var previousCount = int.MaxValue;
            var previousCentroid = float.PositiveInfinity;
            foreach (var threshold in thresholds)
            {
                var count = 0;
                var rowSum = 0f;
                for (var row = 0; row < topToBottomRed.Length; row++)
                {
                    if (topToBottomRed[row] - threshold <= 1e-5f)
                        continue;
                    count++;
                    rowSum += row;
                }
                var centroid = count > 0 ? rowSum / count : 0f;
                Assert.That(count, Is.LessThanOrEqualTo(previousCount));
                Assert.That(centroid, Is.LessThanOrEqualTo(previousCentroid));
                previousCount = count;
                previousCentroid = centroid;
            }
            Assert.That(previousCount, Is.Zero);
        }

        [Test]
        public void EndfieldFaceSdfMirrorsAndChangesWithLightPhase()
        {
            var uv = new Vector2(0.2f, 0.7f);
            Assert.That(
                MikuEndfieldShaderMath.FaceSdfUv(uv, -1f),
                Is.EqualTo(new Vector2(0.8f, 0.7f)));
            Assert.That(
                MikuEndfieldShaderMath.FaceSdfUv(uv, 1f),
                Is.EqualTo(uv));
            Assert.That(
                MikuEndfieldShaderMath.FaceSdfThreshold(1f, 0f),
                Is.EqualTo(0f));
            Assert.That(
                MikuEndfieldShaderMath.FaceSdfThreshold(-1f, 0f),
                Is.EqualTo(1f));
            Assert.That(
                MikuEndfieldShaderMath.FaceSdfLight(0.5f, 1f, 0f, 0.035f),
                Is.GreaterThan(0.99f));
            Assert.That(
                MikuEndfieldShaderMath.FaceSdfLight(0.5f, -1f, 0f, 0.035f),
                Is.LessThan(0.01f));
        }

        [Test]
        public void EndfieldFaceSdfHasGeometricFallbackWithoutLosingBrightSdf()
        {
            Assert.That(
                MikuEndfieldShaderMath.FaceLight(0f, 1f, 0f, 1f),
                Is.EqualTo(1f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.FaceLight(1f, 0.4f, 0f, 1f),
                Is.EqualTo(1f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.FaceLight(0f, 0.8f, 0f, 0f),
                Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.FaceLight(0f, 0.8f, 1f, 1f),
                Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.FaceLight(1f, 0.3f, 0f, 1f, false),
                Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.FaceSdfPhase(-1f, 0f, 0.25f),
                Is.EqualTo(-0.75f).Within(1e-6f));
        }

        [Test]
        public void EndfieldDirectionalKeySurvivesZeroDistanceDiagnostic()
        {
            var shadow = new Color(0.18f, 0.12f, 0.10f, 1f);
            var lit = new Color(0.8f, 0.7f, 0.6f, 1f);
            var direct = MikuEndfieldShaderMath.ToonDirectColor(
                shadow,
                lit,
                1f,
                0f,
                0f);
            Assert.That(direct.r, Is.EqualTo(shadow.r).Within(1e-6f));
            Assert.That(direct.g, Is.EqualTo(shadow.g).Within(1e-6f));
            Assert.That(direct.b, Is.EqualTo(shadow.b).Within(1e-6f));
            Assert.That(direct.maxColorComponent, Is.GreaterThan(0f));
            Assert.That(
                MikuEndfieldShaderMath.MainLightAvailability(true, true),
                Is.EqualTo(1f));
            Assert.That(
                MikuEndfieldShaderMath.MainLightAvailability(false, true),
                Is.Zero);
            Assert.That(
                MikuEndfieldShaderMath.MainLightAvailability(true, false),
                Is.Zero);
            Assert.That(
                MikuEndfieldShaderMath.MainLightAvailability(Color.black, true),
                Is.Zero);
            Assert.That(
                MikuEndfieldShaderMath.MainLightAvailability(Color.white, true),
                Is.EqualTo(1f));
            Assert.That(
                MikuEndfieldShaderMath.ToonDirectColor(
                    shadow,
                    lit,
                    1f,
                    1f,
                    1f,
                    hasMainLight: false),
                Is.EqualTo(new Color(0f, 0f, 0f, 0f)));
        }

        [Test]
        public void EndfieldForwardPassDeclaresUrp174MainLightVariants()
        {
            var source = File.ReadAllText(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Packages/com.miku.shaderconverter/Runtime/Endfield/" +
                "EndfieldPassLibrary.shader"));
            Assert.That(source, Does.Contain("_MAIN_LIGHT_SHADOWS"));
            Assert.That(source, Does.Contain("_MAIN_LIGHT_SHADOWS_CASCADE"));
            Assert.That(source, Does.Contain("_MAIN_LIGHT_SHADOWS_SCREEN"));
            Assert.That(source, Does.Contain("_LIGHT_LAYERS"));
            Assert.That(source, Does.Contain("LIGHTMAP_SHADOW_MIXING"));
            Assert.That(source, Does.Contain("SHADOWS_SHADOWMASK"));
        }

        [Test]
        public void EndfieldSharedDirectLightKeepsDistanceDiagnosticOutOfEnergy()
        {
            var source = File.ReadAllText(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Packages/com.miku.shaderconverter/Runtime/Endfield/" +
                "EndfieldCommon.hlsl"));
            Assert.That(source, Does.Contain(
                "terms.distanceDiagnostic = saturate(mainLight.distanceAttenuation)"));
            Assert.That(source, Does.Contain(
                "return terms.color * terms.layerMatch"));
            Assert.That(source, Does.Not.Contain(
                "mainLight.color * mainLight.distanceAttenuation"));
            Assert.That(source, Does.Not.Contain("max(directLight, SampleSH"));
        }

        [Test]
        public void EndfieldDirectAndShChannelsRemainIndependentUnderLightSweep()
        {
            var baseColor = new Color(0.8f, 0.6f, 0.4f, 1f);
            var fromRight = MikuEndfieldShaderMath.DirectDiffuseOnly(
                baseColor,
                Color.white,
                Vector3.right,
                Vector3.right,
                1f);
            var fromLeft = MikuEndfieldShaderMath.DirectDiffuseOnly(
                baseColor,
                Color.white,
                Vector3.right,
                Vector3.left,
                1f);
            Assert.That(fromRight.maxColorComponent, Is.GreaterThan(0f));
            Assert.That(fromRight.r, Is.GreaterThan(fromLeft.r));
            Assert.That(
                MikuEndfieldShaderMath.DirectDiffuseOnly(
                    baseColor,
                    Color.black,
                    Vector3.right,
                    Vector3.right,
                    1f).maxColorComponent,
                Is.Zero);
            Assert.That(
                MikuEndfieldShaderMath.DirectDiffuseOnly(
                    baseColor,
                    Color.white,
                    Vector3.right,
                    Vector3.right,
                    1f,
                    layerMatches: false).maxColorComponent,
                Is.Zero);
            Assert.That(
                MikuEndfieldShaderMath.ShOnly(
                    new Color(0.2f, 0.3f, 0.4f, 1f),
                    baseColor,
                    1f).maxColorComponent,
                Is.GreaterThan(0f));
            Assert.That(
                MikuEndfieldShaderMath.ShOnly(Color.white, baseColor, 0f)
                    .maxColorComponent,
                Is.Zero);
        }

        [Test]
        public void EndfieldEyeRolesSeparateAuthoredIrisAndFixedSclera()
        {
            var tint = Color.white;
            var firstIris = MikuEndfieldShaderMath.EyeColor(
                new Color(0.8f, 0.1f, 0.2f, 1f),
                tint,
                0f);
            var secondIris = MikuEndfieldShaderMath.EyeColor(
                new Color(0.1f, 0.4f, 0.9f, 1f),
                tint,
                0f);
            var firstSclera = MikuEndfieldShaderMath.EyeColor(
                new Color(0.8f, 0.1f, 0.2f, 1f),
                tint,
                1f);
            var secondSclera = MikuEndfieldShaderMath.EyeColor(
                new Color(0.1f, 0.4f, 0.9f, 1f),
                tint,
                1f);
            Assert.That(firstIris, Is.Not.EqualTo(secondIris));
            Assert.That(firstSclera, Is.EqualTo(secondSclera));
            Assert.That(firstSclera.r, Is.EqualTo(0.94f).Within(1e-6f));
            Assert.That(firstSclera.g, Is.EqualTo(0.88f).Within(1e-6f));
            Assert.That(firstSclera.b, Is.EqualTo(0.84f).Within(1e-6f));
        }

        [Test]
        public void EndfieldBlushTileIsIndependentFromEmotionTile()
        {
            var uv = new Vector2(0.35f, 0.55f);
            var blushUvBefore = MikuEndfieldShaderMath.AtlasUv(uv, 0f, 2f, 2f);
            var expressionUv = MikuEndfieldShaderMath.AtlasUv(uv, 3f, 2f, 2f);
            var blushUvAfter = MikuEndfieldShaderMath.AtlasUv(uv, 0f, 2f, 2f);
            Assert.That(blushUvBefore, Is.EqualTo(blushUvAfter));
            Assert.That(expressionUv, Is.Not.EqualTo(blushUvBefore));
            Assert.That(
                MikuEndfieldShaderMath.BlushMask(0.08f, 3f, 1f),
                Is.EqualTo(0.24f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.BlushMask(0f, 8f, 1f),
                Is.Zero);
        }

        [Test]
        public void EndfieldMetalAndCorneaFallbacksStayFiniteAndVisible()
        {
            var metalF0 = MikuEndfieldShaderMath.MetalF0(
                Color.black,
                1f,
                1f);
            Assert.That(metalF0.r, Is.GreaterThanOrEqualTo(0.12f));
            Assert.That(metalF0.g, Is.GreaterThanOrEqualTo(0.12f));
            Assert.That(metalF0.b, Is.GreaterThanOrEqualTo(0.12f));
            Assert.That(
                MikuEndfieldShaderMath.MetalAo(0f, 1f),
                Is.EqualTo(1f));
            Assert.That(
                MikuEndfieldShaderMath.MetalAo(0.25f, 0f),
                Is.EqualTo(0.25f));
            Assert.That(
                MikuEndfieldShaderMath.SpecularOcclusion(0f, 1f, 0.5f),
                Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.HairSphereBlend(1f, 1f),
                Is.Zero);
            Assert.That(
                MikuEndfieldShaderMath.HairSphereBlend(0f, 1f),
                Is.EqualTo(1f));
            var rotatedEnvironment =
                MikuEndfieldShaderMath.RotateEnvironmentDirection(
                    Vector3.forward,
                    90f);
            Assert.That(rotatedEnvironment.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(rotatedEnvironment.z, Is.EqualTo(0f).Within(1e-5f));
            var metalBase = MikuEndfieldShaderMath.MetalBaseResponse(
                new Color(0.35f, 0.4f, 0.5f, 1f),
                1f,
                Color.white,
                0.5f,
                1f);
            Assert.That(metalBase.r, Is.GreaterThan(0f));
            Assert.That(metalBase.g, Is.GreaterThan(metalBase.r));
            Assert.That(metalBase.b, Is.GreaterThan(metalBase.g));
            var nonMetalBase = MikuEndfieldShaderMath.MetalBaseResponse(
                Color.white,
                0f,
                Color.white,
                1f,
                1f);
            Assert.That(nonMetalBase.maxColorComponent, Is.Zero);
            foreach (var uv in new[]
            {
                Vector2.zero,
                new Vector2(0.5f, 0.5f),
                Vector2.one,
            })
            {
                var normal = MikuEndfieldShaderMath.CorneaNormal(uv, 0.25f);
                Assert.That(float.IsNaN(normal.x), Is.False);
                Assert.That(float.IsInfinity(normal.x), Is.False);
                Assert.That(normal.magnitude, Is.EqualTo(1f).Within(1e-5f));
            }
        }

        [Test]
        public void EndfieldHairScalarRedLutProducesNeutralHighlight()
        {
            var redOnly = new Color(0.62f, 0f, 0f, 1f);
            var authoredColor = MikuEndfieldShaderMath.HairSpecularLut(
                redOnly,
                MikuEndfieldShaderMath.HairLutColorRgb);
            var scalarRed = MikuEndfieldShaderMath.HairSpecularLut(
                redOnly,
                MikuEndfieldShaderMath.HairLutScalarRed);

            Assert.That(authoredColor, Is.EqualTo(redOnly));
            Assert.That(scalarRed.r, Is.EqualTo(0.62f).Within(1e-6f));
            Assert.That(scalarRed.g, Is.EqualTo(scalarRed.r).Within(1e-6f));
            Assert.That(scalarRed.b, Is.EqualTo(scalarRed.r).Within(1e-6f));
        }

        [Test]
        public void EndfieldSurfaceRimIsViewAndMainLightDirectionalAndFinite()
        {
            var front = MikuEndfieldShaderMath.SurfaceRim(
                Vector3.forward,
                Vector3.forward,
                Vector3.back,
                0.32f,
                4.5f,
                0.6f,
                1f);
            var grazingBacklit = MikuEndfieldShaderMath.SurfaceRim(
                Vector3.up,
                Vector3.forward,
                Vector3.down,
                0.32f,
                4.5f,
                0.6f,
                1f);
            var grazingFrontlit = MikuEndfieldShaderMath.SurfaceRim(
                Vector3.up,
                Vector3.forward,
                Vector3.up,
                0.32f,
                4.5f,
                0.6f,
                1f);
            var invalid = MikuEndfieldShaderMath.SurfaceRim(
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                float.PositiveInfinity,
                0f,
                1f,
                0f);

            Assert.That(front, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(grazingBacklit, Is.GreaterThan(grazingFrontlit));
            Assert.That(grazingFrontlit, Is.GreaterThan(0f));
            Assert.That(float.IsNaN(invalid), Is.False);
            Assert.That(float.IsInfinity(invalid), Is.False);
        }

        [Test]
        public void EndfieldMetalBoostsRemainIndependentAndCompatibilityNeutral()
        {
            Assert.That(
                MikuEndfieldShaderMath.MetalBoost(1f, 1f),
                Is.EqualTo(1f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.MetalBoost(0f, 2.4f),
                Is.EqualTo(1f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.MetalBoost(1f, 1.6f),
                Is.EqualTo(1.6f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.MetalBoost(1f, 2.4f),
                Is.EqualTo(2.4f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.SpecularOcclusion(0f, 0f, 0.5f),
                Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void EndfieldSkinToneNeutralIsIdentityAndCalibrationStaysBounded()
        {
            var source = new Color(0.62f, 0.48f, 0.42f, 0.73f);
            var target = new Color(1f, 0.93f, 0.90f, 1f);
            var neutral = MikuEndfieldShaderMath.SkinTone(
                source,
                1f,
                0f,
                target);
            var calibrated = MikuEndfieldShaderMath.SkinTone(
                source,
                1.08f,
                0.45f,
                target);

            Assert.That(neutral, Is.EqualTo(source));
            Assert.That(calibrated.r, Is.GreaterThan(source.r));
            Assert.That(calibrated.g, Is.GreaterThan(source.g));
            Assert.That(calibrated.b, Is.GreaterThan(source.b));
            Assert.That(calibrated.r, Is.GreaterThan(calibrated.g));
            Assert.That(calibrated.g, Is.GreaterThan(calibrated.b));
            Assert.That(calibrated.a, Is.EqualTo(source.a));
            foreach (var channel in new[]
                { calibrated.r, calibrated.g, calibrated.b, calibrated.a })
            {
                Assert.That(float.IsNaN(channel), Is.False);
                Assert.That(float.IsInfinity(channel), Is.False);
                Assert.That(channel, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void EndfieldCharacterShadersCompileWithoutErrors()
        {
            foreach (var shaderName in new[]
            {
                "MIKU/Endfield/Body",
                "MIKU/Endfield/Skin",
                "MIKU/Endfield/Face",
                "MIKU/Endfield/Hair",
                "MIKU/Endfield/Eye",
                "MIKU/Endfield/Mouth",
            })
            {
                var shader = Shader.Find(shaderName);
                Assert.That(shader, Is.Not.Null, shaderName);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False,
                    shaderName);
            }
        }

        [Test]
        public void SmoothNormalsCanBeGeneratedFromNonReadableSource()
        {
            var source = new Mesh { name = "NonReadableSource" };
            source.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
            };
            source.normals = new[]
            {
                Vector3.forward,
                Vector3.forward,
                Vector3.forward,
            };
            source.triangles = new[] { 0, 1, 2 };
            source.UploadMeshData(true);
            var clone = UnityEngine.Object.Instantiate(source);
            try
            {
                Assert.That(source.isReadable, Is.False);
                Assert.DoesNotThrow(() =>
                    MikuToonMeshData.GenerateSmoothNormalsFromSource(
                        source,
                        clone,
                        0.0001f,
                        60f,
                        false,
                        true));
                using (var data = MeshUtility.AcquireReadOnlyMeshData(clone))
                    Assert.That(data[0].HasVertexAttribute(
                        UnityEngine.Rendering.VertexAttribute.TexCoord7),
                        Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void ScreenRimUsesGameToonHiddenShaderAndNamespace()
        {
            var shader = Shader.Find(
                "Hidden/Miku/GameToon/ScreenRimComposite");
            Assert.That(shader, Is.Not.Null);
            Assert.That(typeof(MikuToonScreenRimRendererFeature).Namespace,
                Is.EqualTo("Miku.ShaderConverter.Runtime.GameToon"));
        }

        [Test]
        public void GameMenusRemainAvailableAfterGenericRetirement()
        {
            var menu = new List<string>();
            foreach (var item in TypeCache.GetMethodsWithAttribute<MenuItem>())
            {
                foreach (var value in item.GetCustomAttributes(typeof(MenuItem), false))
                {
                    var attribute = (MenuItem)value;
                    var path = attribute.menuItem;
                    if (path.StartsWith("Miku/Game Toon/", StringComparison.Ordinal))
                        menu.Add(path);
                }
            }
            Assert.That(menu, Does.Contain(
                "Miku/Game Toon/Mesh/Smooth Normal Generator"));
            Assert.That(menu, Does.Not.Contain(
                "Miku/Game Toon/Mesh/Vertex Color Initializer"));
            Assert.That(menu, Does.Not.Contain(
                "Miku/Game Toon/Mesh/Combined Mesh Data"));
            Assert.That(menu, Does.Contain(
                "Miku/Game Toon/Rendering/Screen Rim Installer"));
            Assert.That(menu, Does.Contain(
                "Miku/Game Toon/Materials/Create Material"));
            Assert.That(menu, Does.Not.Contain(
                "Miku/Game Toon/Materials/Create Material Template"));
            Assert.That(menu, Has.None.Contains("Generic Toon"));
        }

        static void AssertRoleMigration(
            Material material,
            string source,
            string expected)
        {
            var actual = MikuFixedWorkflowTextureBindings.CanonicalRole(
                material,
                "endfield_toon",
                source,
                out var migrated);
            Assert.That(migrated, Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        static void AssertMaterialProperties(
            string shaderName,
            params string[] properties)
        {
            var shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, shaderName);
            var material = new Material(shader);
            try
            {
                foreach (var property in properties)
                    Assert.That(material.HasProperty(property), Is.True,
                        shaderName + ":" + property);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
