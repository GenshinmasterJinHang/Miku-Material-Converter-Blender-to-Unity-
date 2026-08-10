// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Miku.ShaderConverter.Editor.Tests
{
    /// <summary>
    /// Tutorial-compliance coverage for the Wuwa 2.3.0 shader work:
    /// simplified CookTorrance specular, MatCap-on-albedo, vertical gradient,
    /// Fresnel-step rim, tutorial outline distance, eye light response, and
    /// the idempotent hair-shadow renderer-feature installer.
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
            Assert.That(rough, Is.GreaterThan(0f));

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
        }

        [Test]
        public void WuwaGradientValueSelectsChannelAndInvert()
        {
            Assert.That(
                MikuGameToonCpuMath.WuwaGradientValue(
                    new Vector2(0f, 0.2f),
                    new Vector2(0f, 0.4f),
                    new Vector2(0f, 0.9f),
                    3f,
                    0f),
                Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaGradientValue(
                    new Vector2(0f, 0.2f),
                    new Vector2(0f, 0.4f),
                    new Vector2(0f, 0.9f),
                    3f,
                    1f),
                Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(
                MikuGameToonCpuMath.WuwaGradientValue(
                    new Vector2(0f, 0.2f),
                    new Vector2(0f, 0.4f),
                    new Vector2(0f, 0.9f),
                    1f,
                    0f),
                Is.EqualTo(0.4f).Within(0.0001f));
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
                })
                    Assert.That(body.HasProperty(property), Is.True, property);
                Assert.That(body.GetFloat("_MatcapSaturation"), Is.EqualTo(0.1f));
                Assert.That(body.GetFloat("_Roughness"), Is.EqualTo(0.6f));
                Assert.That(body.GetFloat("_GradientUVIndex"), Is.EqualTo(3f));
                Assert.That(body.GetFloat("_OutlineDistanceMode"), Is.EqualTo(1f));
                Assert.That(body.GetFloat("_OutlineVertexColorMask"), Is.EqualTo(1f));

                Assert.That(hair.HasProperty("_VerticalGradientStrength"), Is.True);
                Assert.That(hair.GetFloat("_OutlineDistanceMode"), Is.EqualTo(1f));

                Assert.That(face.GetFloat("_FaceSoftChannelStrength"), Is.EqualTo(1f));
                Assert.That(face.GetFloat("_UseHairShadow"), Is.EqualTo(1f));
                Assert.That(face.GetFloat("_VerticalGradientStrength"), Is.Zero);

                Assert.That(eye.GetFloat("_EyeShadowStart"), Is.EqualTo(0.25f));
                Assert.That(eye.GetFloat("_EyeShadowEnd"), Is.EqualTo(0.55f));
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
            try
            {
                body.SetFloat("_MatcapSaturation", 0.8f);
                body.SetFloat("_VerticalGradientStrength", 0f);
                face.SetFloat("_FaceSoftChannelStrength", 0f);
                face.SetFloat("_UseHairShadow", 0f);
                eye.SetColor("_EyeLitTint", Color.gray);
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
                Assert.That(body.GetFloat("_VerticalGradientStrength"), Is.EqualTo(0.35f));
                Assert.That(face.GetFloat("_FaceSoftChannelStrength"), Is.EqualTo(1f));
                Assert.That(face.GetFloat("_UseHairShadow"), Is.EqualTo(1f));
                Assert.That(eye.GetColor("_EyeLitTint"), Is.EqualTo(Color.white));
                Assert.That(hair.GetFloat("_VerticalGradientStrength"), Is.EqualTo(0.35f));

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
                Assert.That(
                    MikuWuwaRendererFeatureInstaller.CountFeatures(renderer),
                    Is.EqualTo(1));

                var second = MikuWuwaRendererFeatureInstaller.Install(renderer);
                Assert.That(second.created, Is.False);
                Assert.That(second.feature, Is.SameAs(first.feature));
                Assert.That(
                    MikuWuwaRendererFeatureInstaller.CountFeatures(renderer),
                    Is.EqualTo(1));
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
    }
}
