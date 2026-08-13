// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System.IO;
using Miku.ShaderConverter.Editor;
using Miku.ShaderConverter.Runtime.Endfield;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Miku.ShaderConverter.Tests.Editor
{
    public sealed class MikuEndfieldTutorialLightingTests
    {
        [Test]
        public void LightingControllerPublishesSanitizedSharedStateAndClearsIt()
        {
            var gameObject = new GameObject("Miku Endfield Lighting Test");
            try
            {
                var controller = gameObject.AddComponent<MikuEndfieldLightingController>();
                Assert.That(
                    controller.TopLightColor,
                    Is.EqualTo(new Color(1f, 0.98f, 0.95f, 1f)));
                Assert.That(
                    Shader.GetGlobalVector("_MikuEndfieldTopLightParams"),
                    Is.EqualTo(new Vector4(0.5f, 0.5f, 0.18f, 0.85f)));
                Assert.That(
                    Shader.GetGlobalFloat("_MikuEndfieldCameraForwardBlend"),
                    Is.EqualTo(1f));
                controller.DayStrength = 0.25f;
                controller.TopLightColor = new Color(2f, 1f, 0.5f, 1f);
                controller.TopLightDirection = Vector3.zero;
                controller.TopLightNormalScale = 0.4f;
                controller.TopLightNormalOffset = 0.6f;
                controller.DayOneTopStrength = 0.2f;
                controller.DayZeroTopStrength = 0.6f;
                controller.CameraForwardBlend = 0.75f;
                controller.BackLightStrength = 1.4f;
                controller.Apply();

                Assert.That(
                    Shader.GetGlobalFloat("_MikuEndfieldLightingAvailable"),
                    Is.EqualTo(1f));
                Assert.That(
                    Shader.GetGlobalFloat("_MikuEndfieldDayStrength"),
                    Is.EqualTo(0.25f).Within(1e-6f));
                Assert.That(
                    Shader.GetGlobalVector("_MikuEndfieldTopLightDirection"),
                    Is.EqualTo((Vector4)Vector3.up));
                Assert.That(
                    Shader.GetGlobalVector("_MikuEndfieldTopLightParams"),
                    Is.EqualTo(new Vector4(0.4f, 0.6f, 0.2f, 0.6f)));
                Assert.That(
                    Shader.GetGlobalFloat("_MikuEndfieldCameraForwardBlend"),
                    Is.EqualTo(0.75f).Within(1e-6f));
                Assert.That(
                    Shader.GetGlobalFloat("_MikuEndfieldBackLightStrength"),
                    Is.EqualTo(1.4f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }

            Assert.That(
                Shader.GetGlobalFloat("_MikuEndfieldLightingAvailable"),
                Is.EqualTo(0f));
        }

        [Test]
        public void DuplicateLightingControllersKeepOneOwnerAndWarnOnce()
        {
            var firstObject = new GameObject("Miku Endfield Lighting Owner");
            GameObject secondObject = null;
            try
            {
                var first = firstObject.AddComponent<MikuEndfieldLightingController>();
                first.DayStrength = 0.2f;
                first.Apply();

                LogAssert.Expect(
                    LogType.Warning,
                    "MIKU_ENDFIELD_LIGHTING_CONTROLLER_DUPLICATE");
                secondObject = new GameObject("Miku Endfield Lighting Duplicate");
                var second = secondObject.AddComponent<MikuEndfieldLightingController>();
                second.DayStrength = 0.9f;
                second.Apply();
                second.Apply();

                var expectedDayStrength =
                    first.GetInstanceID() < second.GetInstanceID()
                        ? 0.2f
                        : 0.9f;
                Assert.That(
                    Shader.GetGlobalFloat("_MikuEndfieldDayStrength"),
                    Is.EqualTo(expectedDayStrength).Within(1e-6f));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (secondObject != null)
                    Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(firstObject);
            }
        }

        [Test]
        public void TutorialMathKeepsLegacyOffStateAndBoundsNewResponses()
        {
            Assert.That(
                MikuEndfieldShaderMath.FaceSignNormal(Vector3.forward, true),
                Is.EqualTo(Vector3.forward));
            Assert.That(
                MikuEndfieldShaderMath.FaceSignNormal(Vector3.forward, false),
                Is.EqualTo(Vector3.back));

            var legacySignal = MikuEndfieldShaderMath.BackLightSignal(
                0f,
                Vector3.back,
                Vector3.forward,
                1f,
                1f,
                0f);
            var tutorialSignal = MikuEndfieldShaderMath.BackLightSignal(
                0f,
                Vector3.back,
                Vector3.forward,
                1f,
                1f,
                1f);
            Assert.That(legacySignal, Is.EqualTo(0f));
            Assert.That(tutorialSignal, Is.EqualTo(0.421875f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.BackLightSignal(
                    0f,
                    Vector3.back,
                    Vector3.up,
                    1f,
                    1f,
                    1f),
                Is.EqualTo(0f));

            var virtualLight = MikuEndfieldShaderMath.StylizedSpecularLightDirection(
                Vector3.right,
                Vector3.forward,
                0f,
                1f,
                1f);
            Assert.That(virtualLight.z, Is.GreaterThan(0.8f));
            var virtualHalf = MikuEndfieldShaderMath.StylizedSpecularHalfDirection(
                Vector3.up,
                Vector3.forward,
                Vector3.right,
                Vector3.forward,
                0f,
                1f,
                1f);
            Assert.That(virtualHalf.z, Is.GreaterThan(virtualHalf.x));

            var dayZero = MikuEndfieldShaderMath.ThreeLayerLit(
                0f, 1f, 1f, 0.8f, 0.2f, 1f, 1f, 1f);
            var dayOne = MikuEndfieldShaderMath.ThreeLayerLit(
                1f, 1f, 1f, 0.8f, 0.2f, 1f, 1f, 1f);
            Assert.That(dayZero, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(dayOne, Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.DarkInDark(Color.white, 0.65f).r,
                Is.EqualTo(0.65f).Within(1e-6f));
            var neutralRamp = MikuEndfieldShaderMath.RampColorEffect(
                new Color(0.5f, 0.5f, 0.5f, 1f),
                1f);
            Assert.That(neutralRamp.r, Is.EqualTo(1f).Within(1e-6f));

            var single = new Color(0.2f, 0.15f, 0.1f, 1f);
            var multiple = MikuEndfieldShaderMath.DfgMultiscatter(
                single,
                new Color(0.5f, 0.3f, 0.2f, 1f),
                0.8f,
                0.2f,
                1f);
            Assert.That(multiple.r, Is.GreaterThan(single.r));
            Assert.That(multiple.g, Is.GreaterThan(single.g));
            Assert.That(multiple.b, Is.GreaterThan(single.b));
            Assert.That(multiple.maxColorComponent, Is.LessThanOrEqualTo(8f));
            foreach (var channel in new[] { multiple.r, multiple.g, multiple.b })
            {
                Assert.That(float.IsNaN(channel), Is.False);
                Assert.That(float.IsInfinity(channel), Is.False);
            }
            foreach (var smoothness in new[] { 0f, 1f })
            foreach (var normalDotView in new[] { 0f, 1f })
            {
                var endpoint = MikuEndfieldShaderMath.DfgMultiscatter(
                    single,
                    new Color(0.04f, 0.5f, 1f, 1f),
                    smoothness,
                    normalDotView,
                    1f);
                foreach (var channel in new[] { endpoint.r, endpoint.g, endpoint.b })
                {
                    Assert.That(float.IsNaN(channel), Is.False);
                    Assert.That(float.IsInfinity(channel), Is.False);
                    Assert.That(channel, Is.InRange(0f, 8f));
                }
            }
            var nonFinite = MikuEndfieldShaderMath.DfgMultiscatter(
                single,
                new Color(float.NaN, 0.5f, 1f, 1f),
                float.PositiveInfinity,
                float.NegativeInfinity,
                1f);
            Assert.That(nonFinite, Is.EqualTo(single));

            var raw = new Color(0.25f, 0.5f, 0.75f, 1f);
            var maskEmission = MikuEndfieldShaderMath.Emission(
                raw, Color.white, 0, 0.4f, 1f);
            var rgbEmission = MikuEndfieldShaderMath.Emission(
                raw, Color.white, 1, 0.4f, 1f);
            var alphaEmission = MikuEndfieldShaderMath.Emission(
                raw, Color.white, 2, 0.4f, 1f);
            Assert.That(maskEmission.g, Is.EqualTo(0.25f));
            Assert.That(rgbEmission.g, Is.EqualTo(0.5f));
            Assert.That(alphaEmission.g, Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void TutorialMasksFollowSigmoidAlphaFaceAndHairContracts()
        {
            Assert.That(
                MikuEndfieldShaderMath.CharacterShadow(
                    0.25f, 0.5f, 0.1f, 0f, 1f, 0f),
                Is.EqualTo(0.25f));
            var shadowLow = MikuEndfieldShaderMath.CharacterShadow(
                0.25f, 0.5f, 0.1f, 0f, 1f, 1f);
            var shadowHigh = MikuEndfieldShaderMath.CharacterShadow(
                0.75f, 0.5f, 0.1f, 0f, 1f, 1f);
            Assert.That(shadowLow, Is.LessThan(shadowHigh));

            var clothNoAlpha = MikuEndfieldShaderMath.ClothSssArea(
                0.2f, 0f, 2f);
            var clothFullAlpha = MikuEndfieldShaderMath.ClothSssArea(
                0.2f, 1f, 2f);
            Assert.That(clothNoAlpha, Is.Not.EqualTo(clothFullAlpha));

            Assert.That(
                MikuEndfieldShaderMath.FaceSssStrength(-0.5f, 0.8f, 0f),
                Is.EqualTo(0f));
            Assert.That(
                MikuEndfieldShaderMath.FaceSssStrength(-0.5f, 0.8f, 1f),
                Is.EqualTo(0.8f).Within(1e-6f));
            var leftFaceNormal = MikuEndfieldShaderMath.FaceSdfNormal(
                0.75f, false, 0f, Vector3.forward);
            var rightFaceNormal = MikuEndfieldShaderMath.FaceSdfNormal(
                0.75f, true, 0f, Vector3.forward);
            Assert.That(leftFaceNormal.x, Is.EqualTo(-rightFaceNormal.x).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.FaceSdfNormal(
                    0.75f, true, 1f, Vector3.forward),
                Is.EqualTo(Vector3.forward));

            var hairUv = MikuEndfieldShaderMath.HairLutUv(
                0.5f,
                Vector2.up,
                Vector2.up,
                2f);
            var reverseHairUv = MikuEndfieldShaderMath.HairLutUv(
                -0.5f,
                Vector2.up,
                Vector2.up,
                2f);
            Assert.That(hairUv.x, Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(hairUv.y, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(reverseHairUv.y, Is.EqualTo(0f));
            var finalF0 = MikuEndfieldShaderMath.HairFinalF0(
                Color.white,
                new Color(0.04f, 0.04f, 0.04f, 1f),
                new Color(0.08f, 0.06f, 0.07f, 1f),
                1f,
                0f,
                40f);
            Assert.That(finalF0.r, Is.EqualTo(0.36f).Within(1e-6f));

            Assert.That(
                MikuEndfieldShaderMath.TutorialCorneaNormal(
                    new Vector2(0.6f, 0.5f), 1f).x,
                Is.LessThan(0f));
            Assert.That(
                MikuEndfieldShaderMath.TutorialCorneaNormal(
                    new Vector2(0.99f, 0.99f), 1f),
                Is.EqualTo(Vector3.forward));
            var eyeBright = MikuEndfieldShaderMath.EyeBrightTrick(
                Color.white,
                new Color(0.4f, 0.4f, 0.4f, 1f),
                new Color(0.2f, 0.2f, 0.2f, 1f),
                1f,
                1f);
            Assert.That(eyeBright.r, Is.EqualTo(0.5f).Within(1e-6f));
            var matcapBrdf = MikuEndfieldShaderMath.EyeMatcapBrdf(
                new Color(0.2f, 0.3f, 0.4f, 0.5f),
                2f,
                Color.white,
                0.5f);
            Assert.That(matcapBrdf.r, Is.EqualTo(0.65f).Within(1e-6f));
        }

        [Test]
        public void TutorialDirectLightSeparatesNightTopLightFromDayKeyLight()
        {
            var legacy = new Color(1f, 0.8f, 0.6f, 1f);
            var top = new Color(0.4f, 0.5f, 0.8f, 1f);
            var disabled = MikuEndfieldShaderMath.TutorialDirectLight(
                Vector3.up, legacy, Vector3.up, top, 0f,
                0.5f, 0.5f, 0.18f, 0.85f, 0f, 1f);
            var night = MikuEndfieldShaderMath.TutorialDirectLight(
                Vector3.up, legacy, Vector3.up, top, 0f,
                0.5f, 0.5f, 0.18f, 0.85f, 1f, 1f);
            var day = MikuEndfieldShaderMath.TutorialDirectLight(
                Vector3.up, legacy, Vector3.up, top, 1f,
                0.5f, 0.5f, 0.18f, 0.85f, 1f, 1f);
            // In shaded bands the main light desaturates to its luminance and
            // the top light keeps its authored color; in lit bands the top
            // light whitens so it only tints the shadow fill.
            var shadowed = MikuEndfieldShaderMath.TutorialDirectLight(
                Vector3.up, legacy, Vector3.up, top, 1f,
                0.5f, 0.5f, 0.18f, 0.85f, 1f, 0f);

            Assert.That(disabled, Is.EqualTo(legacy));
            Assert.That(night.r, Is.EqualTo(0.85f).Within(1e-6f));
            Assert.That(day.r, Is.EqualTo(1.18f).Within(1e-6f));
            Assert.That(shadowed.r, Is.EqualTo(0.909f).Within(1e-6f));
        }

        [Test]
        public void ArticleSpecularFaceSdfEyeProjectionAndRefineFollowTutorialMath()
        {
            // Reference D*V: saturated peak clamps to 20 and baseline is zero.
            Assert.That(
                MikuEndfieldShaderMath.TutorialSpecularDV(
                    1f, 1f, 0.0078f),
                Is.EqualTo(20f).Within(1e-4f));
            Assert.That(
                MikuEndfieldShaderMath.TutorialSpecularDV(
                    0f, 1f, 0.0078f),
                Is.EqualTo(0f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.TutorialSpecularDV(
                    0.5f, 1f, 0.2f),
                Is.GreaterThan(0f));

            // Ramp luminance control is clamped to [0, 1.5].
            var neutral = MikuEndfieldShaderMath.RampColorControl(
                new Color(0.6f, 0.5f, 0.4f, 1f),
                new Color(0.6f, 0.5f, 0.4f, 1f));
            Assert.That(neutral, Is.EqualTo(1f).Within(1e-6f));
            var darkened = MikuEndfieldShaderMath.RampColorControl(
                new Color(0.6f, 0.5f, 0.4f, 1f),
                new Color(0.15f, 0.125f, 0.1f, 1f));
            Assert.That(darkened, Is.EqualTo(1.5f).Within(1e-6f));

            // Face SDF: front fully lit, back fully dark, side half lit.
            Assert.That(
                MikuEndfieldShaderMath.FaceSdfLightArticle(
                    0.5f, 1f, 0f, 0f),
                Is.EqualTo(1f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.FaceSdfLightArticle(
                    0.5f, -1f, 0f, 0f),
                Is.EqualTo(0f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.FaceSdfLightArticle(
                    0.5f, 0f, 0f, 0f),
                Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.FaceSdfLightArticle(
                    0.99f, 1f, 0f, 0f),
                Is.EqualTo(1f).Within(1e-6f));

            // F0-refine UV: with lerp=1 the u axis is NoV^2 and v is the
            // flipped roughness-AO axis.
            var refineUv = MikuEndfieldShaderMath.RefineF0Uv(
                0.8f, 0.6f, 0.3f, 0.5f, 1f);
            Assert.That(refineUv.x, Is.EqualTo(0.36f).Within(1e-5f));
            Assert.That(refineUv.y, Is.EqualTo(0.85f).Within(1e-5f));

            // Eye light projection stays on the face plane.
            var projected = MikuEndfieldShaderMath.EyeFlattenedLightDirection(
                new Vector3(1f, 0.4f, 1f),
                Vector3.right,
                Vector3.forward);
            Assert.That(
                Vector3.Dot(projected, Vector3.up),
                Is.EqualTo(0f).Within(1e-5f));

            // Skin/Face reference has no NoF band: the day-zero weight is
            // plain AO x shadow.
            var noNof = MikuEndfieldShaderMath.ThreeLayerLit(
                0f, 0.5f, 1f, 0.8f, 0.2f, 0f, 1f, 0f);
            Assert.That(noNof, Is.EqualTo(0.5f).Within(1e-6f));
            var withNof = MikuEndfieldShaderMath.ThreeLayerLit(
                0f, 0.5f, 1f, 0.8f, 0.2f, 1f, 1f, 1f);
            Assert.That(withNof, Is.EqualTo(0.1f).Within(1e-6f));

            // Face SSS view edge follows the 0.85/0.15 remap.
            Assert.That(
                MikuEndfieldShaderMath.FaceSssViewEdge(1f),
                Is.EqualTo(0f).Within(1e-6f));
            Assert.That(
                MikuEndfieldShaderMath.FaceSssViewEdge(0f),
                Is.EqualTo(0.85f).Within(1e-6f));

            // Specular envelope keeps the minimum AO floor at day zero.
            var envelopeDayZero = MikuEndfieldShaderMath.TutorialSpecularEnvelope(
                0f, 1f, 0f, 0.5f);
            Assert.That(envelopeDayZero, Is.EqualTo(0.25f).Within(1e-6f));
            var envelopeDayOne = MikuEndfieldShaderMath.TutorialSpecularEnvelope(
                0f, 1f, 1f, 0.5f);
            Assert.That(envelopeDayOne, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void EndfieldShadersExposeTutorialControlsAndBodyIsDoubleSided()
        {
            AssertShaderProperties("MIKU/Endfield/Body",
                "_DarkInDarkStrength", "_NoFStrength", "_ClothSssStrength",
                "_EmissionMapMode", "_LightRimStrength", "_ShadowCenter",
                "_ShadowSigmoidSmoothness", "_NoFPowStrength",
                "_RefineF0U_lerp");
            AssertShaderProperties("MIKU/Endfield/Skin",
                "_SkinRoughness", "_SkinReflectivity", "_NoFStrength");
            AssertShaderProperties("MIKU/Endfield/Face",
                "_MikuHeadAxesValid", "_FaceSdfNormalStrength",
                "_FaceRimMaskStrength",
                "_FaceRimSideStrength", "_RimLightArea",
                "_RimLightDiffuseColorEffect");
            AssertShaderProperties("MIKU/Endfield/Hair",
                "_HairFlatten", "_HairViewDirYOffset", "_HairLutVPower",
                "_HairBaseF0", "_HairBackF0", "_HairBackF0ToHPower",
                "_NoFPowStrength");
            AssertShaderProperties("MIKU/Endfield/Eye",
                "_DiffRampMap", "_EyeRampStrength", "_EyeAlphaColor",
                "_MatCapAlphaColor", "_SelfAoShadowStrength",
                "_DarkInDarkStrength", "_NoFStrength", "_MatCapUvScale");
            AssertShaderProperties("MIKU/Endfield/Overlay",
                "_LightingMode", "_NormalMap", "_MaterialParamMap",
                "_EmissionMapMode");

            var body = new Material(Shader.Find("MIKU/Endfield/Body"));
            try
            {
                Assert.That(body.GetFloat("_Cull"), Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(body);
            }

            foreach (var shaderName in new[]
            {
                "MIKU/Endfield/Body",
                "MIKU/Endfield/Skin",
                "MIKU/Endfield/Face",
                "MIKU/Endfield/Hair",
                "MIKU/Endfield/Eye",
                "MIKU/Endfield/Overlay",
            })
            {
                var material = new Material(Shader.Find(shaderName));
                try
                {
                    Assert.That(
                        material.GetFloat("_DarkInDarkStrength"),
                        Is.EqualTo(0.65f).Within(1e-6f),
                        shaderName);
                }
                finally
                {
                    Object.DestroyImmediate(material);
                }
            }
        }

        [Test]
        public void EndfieldOutlineStateSynchronizesPropertyPassAndLegacyState()
        {
            foreach (var shaderName in new[]
            {
                "MIKU/Endfield/Body",
                "MIKU/Endfield/Skin",
                "MIKU/Endfield/Face",
                "MIKU/Endfield/Hair",
            })
            {
                var material = new Material(Shader.Find(shaderName));
                try
                {
                    Assert.That(material.HasProperty("_UseOutline"), Is.True);
                    Assert.That(
                        material.FindPass(
                            MikuEndfieldMaterialState.OutlinePassName),
                        Is.GreaterThanOrEqualTo(0));
                    MikuEndfieldMaterialState.Synchronize(material);
                    Assert.That(
                        MikuEndfieldMaterialState.GetOutlineEnabled(material),
                        Is.True);

                    MikuEndfieldMaterialState.SetOutlineEnabled(material, false);
                    Assert.That(material.GetFloat("_UseOutline"), Is.EqualTo(0f));
                    Assert.That(
                        material.GetShaderPassEnabled(
                            MikuEndfieldMaterialState.OutlinePassName),
                        Is.False);

                    material.SetFloat("_UseOutline", 1f);
                    MikuEndfieldMaterialState.Synchronize(material);
                    Assert.That(
                        MikuEndfieldMaterialState.GetOutlineEnabled(material),
                        Is.True);

                    material.SetFloat(
                        "_MikuEndfieldMaterialStateVersion",
                        0f);
                    material.SetFloat("_UseOutline", 1f);
                    material.SetShaderPassEnabled(
                        MikuEndfieldMaterialState.OutlinePassName,
                        false);
                    MikuEndfieldMaterialState.Synchronize(material);
                    Assert.That(material.GetFloat("_UseOutline"), Is.EqualTo(0f));
                    Assert.That(
                        MikuEndfieldMaterialState.GetOutlineEnabled(material),
                        Is.False);
                }
                finally
                {
                    Object.DestroyImmediate(material);
                }
            }
        }

        [Test]
        public void EndfieldOutlineDisabledPassSurvivesAssetReload()
        {
            var path = "Assets/MikuEndfieldOutlineState-" +
                       System.Guid.NewGuid().ToString("N") + ".mat";
            var material = new Material(Shader.Find("MIKU/Endfield/Hair"));
            try
            {
                AssetDatabase.CreateAsset(material, path);
                MikuEndfieldMaterialState.SetOutlineEnabled(material, false);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate);

                var reloaded = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(
                    MikuEndfieldMaterialState.GetOutlineEnabled(reloaded),
                    Is.False);
                Assert.That(
                    File.ReadAllText(Path.GetFullPath(path)),
                    Does.Contain("- Outline"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (material != null && !AssetDatabase.Contains(material))
                    Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void EndfieldOutlineStateRejectsUnsupportedEnable()
        {
            var eye = new Material(Shader.Find("MIKU/Endfield/Eye"));
            try
            {
                MikuEndfieldMaterialState.SetOutlineEnabled(eye, false);
                var error = Assert.Throws<System.InvalidOperationException>(
                    () => MikuEndfieldMaterialState.SetOutlineEnabled(
                        eye,
                        true));
                Assert.That(
                    error.Message,
                    Is.EqualTo(
                        "MIKU_ENDFIELD_OUTLINE_UNSUPPORTED:" +
                        "MIKU/Endfield/Eye"));
            }
            finally
            {
                Object.DestroyImmediate(eye);
            }
        }

        [Test]
        public void SharedHlslCarriesFrontFaceAndTutorialLightingContracts()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Packages/com.miku.shaderconverter/Runtime/Endfield/" +
                "EndfieldCommon.hlsl"));
            var source = File.ReadAllText(path);

            StringAssert.Contains("FRONT_FACE_TYPE isFrontFace", source);
            StringAssert.Contains("IS_FRONT_VFACE(isFrontFace, 1.0, -1.0)", source);
            StringAssert.Contains("EndfieldThreeLayerDiffuse", source);
            StringAssert.Contains("EndfieldBackLightSignal", source);
            StringAssert.Contains("EndfieldEnvironmentBrdfMultiscatter", source);
            StringAssert.Contains("EndfieldClothSubsurface", source);
            StringAssert.Contains("EndfieldStylizedViewDirection", source);
            StringAssert.Contains("EndfieldStylizedSpecularLightDirection", source);
            StringAssert.Contains("EndfieldStylizedSpecularHalfDirection", source);
            StringAssert.Contains("float pitchMask = saturate(0.75 - abs(cameraForwardWS.y))", source);
            StringAssert.Contains("float Ess =", source);
            StringAssert.Contains("float Ems =", source);
            StringAssert.Contains("float3 Favg =", source);
            StringAssert.Contains("float3 Fms =", source);
            StringAssert.Contains("float3 finalF0 = lutF0 * 7.0 + backF0", source);
            StringAssert.Contains("saturate(1.0 - ToH_lut * ToH_lut)", source);
            StringAssert.Contains("float3 authoredRgbAlpha", source);
            StringAssert.Contains("float3 deepColor = darkColor * max(_DarkInDarkStrength, 0.0)", source);
            StringAssert.Contains("float rampChroma =", source);
            StringAssert.Contains("float3 tutorialBrightColor", source);
            StringAssert.Contains("float3 tutorialMatcapBrdf", source);
            StringAssert.Contains(
                "corneaNormalVS.xy * (0.5 * max(_MatCapUvScale, 0.0))",
                source);
            StringAssert.Contains("bool EndfieldIsFinite3", source);
        }

        [Test]
        public void EndfieldShadowCasterUsesUrpBiasAndKeepsDepthOnlyUnbiased()
        {
            var root = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Packages/com.miku.shaderconverter/Runtime/Endfield");
            var common = File.ReadAllText(Path.Combine(
                root,
                "EndfieldCommon.hlsl"));
            var passLibrary = File.ReadAllText(Path.Combine(
                root,
                "EndfieldPassLibrary.shader"));

            Assert.That(common, Does.Contain(
                "EndfieldDepthVaryings EndfieldShadowVertex"));
            Assert.That(common, Does.Contain(
                "TransformObjectToWorldNormal(input.normalOS)"));
            Assert.That(common, Does.Contain("ApplyShadowBias("));
            Assert.That(common, Does.Contain("ApplyShadowClamping("));
            Assert.That(common, Does.Contain("_LightDirection"));
            Assert.That(common, Does.Contain("_LightPosition"));
            Assert.That(passLibrary, Does.Contain(
                "#pragma vertex EndfieldShadowVertex"));
            Assert.That(passLibrary, Does.Contain(
                "#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW"));

            var depthStart = common.IndexOf(
                "EndfieldDepthVaryings EndfieldDepthVertex",
                System.StringComparison.Ordinal);
            var depthEnd = common.IndexOf(
                "half4 EndfieldDepthFragment",
                depthStart,
                System.StringComparison.Ordinal);
            var depthVertex = common.Substring(depthStart, depthEnd - depthStart);
            Assert.That(depthVertex, Does.Contain("TransformObjectToHClip"));
            Assert.That(depthVertex, Does.Not.Contain("ApplyShadowBias"));
        }

        [Test]
        public void EndfieldMatCapUvScaleDefaultsToLegacyCompatibleOne()
        {
            var eye = new Material(Shader.Find("MIKU/Endfield/Eye"));
            try
            {
                Assert.That(eye.HasProperty("_MatCapUvScale"), Is.True);
                Assert.That(
                    eye.GetFloat("_MatCapUvScale"),
                    Is.EqualTo(1f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(eye);
            }
        }

        [Test]
        public void ChangedEndfieldShadersCompileWithoutErrors()
        {
            foreach (var shaderName in new[]
            {
                "MIKU/Endfield/Body",
                "MIKU/Endfield/Skin",
                "MIKU/Endfield/Face",
                "MIKU/Endfield/Hair",
                "MIKU/Endfield/Eye",
                "MIKU/Endfield/Mouth",
                "MIKU/Endfield/Overlay",
                "MIKU/Endfield/Effect",
                "MIKU/Endfield/HairShadow",
                "Hidden/MIKU/Endfield/PassLibrary",
                "Hidden/MIKU/Endfield/FullScreenColorLut",
            })
            {
                var shader = Shader.Find(shaderName);
                Assert.That(shader, Is.Not.Null, shaderName);
                Assert.That(
                    ShaderUtil.ShaderHasError(shader),
                    Is.False,
                    shaderName);
            }
        }

        static void AssertShaderProperties(string shaderName, params string[] names)
        {
            var shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, shaderName);
            var material = new Material(shader);
            try
            {
                foreach (var name in names)
                    Assert.That(material.HasProperty(name), Is.True,
                        $"{shaderName} is missing {name}");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
