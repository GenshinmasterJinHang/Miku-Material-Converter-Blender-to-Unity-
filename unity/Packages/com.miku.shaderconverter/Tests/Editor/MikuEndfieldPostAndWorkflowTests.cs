// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Miku.ShaderConverter.Editor.Tests
{
    public sealed class MikuEndfieldPostAndWorkflowTests
    {
        const string TestFolder = "Assets/MikuEndfieldPostAndWorkflowTests";

        sealed class SaveIsolationProbe : ScriptableObject
        {
            public int value;
        }

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestFolder);
            AssetDatabase.CreateFolder(
                "Assets",
                "MikuEndfieldPostAndWorkflowTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void WorkflowBackendResolvesAllNineEndfieldPartsWithoutFallback()
        {
            foreach (var part in new[]
                     {
                         "Body", "Skin", "Hair", "Face", "Eye", "Mouth",
                         "Overlay", "Effect", "HairShadow",
                     })
            {
                var ir = GameIr("endfield_toon", part);
                var backend = MikuWorkflowBackends.Resolve(ir);
                var shader = backend.ResolveShader(ir, null);
                Assert.That(shader.name, Is.EqualTo("MIKU/Endfield/" + part));
            }

            var unsupported = Assert.Throws<InvalidDataException>(() =>
                MikuWorkflowBackends.Resolve(
                    GameIr("genshin_toon", "Effect")));
            Assert.That(
                unsupported.Message,
                Is.EqualTo("MIKU_WORKFLOW_PART_INVALID:genshin_toon:Effect"));
            var unknown = Assert.Throws<InvalidDataException>(() =>
                MikuWorkflowBackends.Resolve(
                    GameIr("endfield_toon", "Unknown")));
            Assert.That(
                unknown.Message,
                Is.EqualTo("MIKU_WORKFLOW_PART_INVALID:endfield_toon:Unknown"));
        }

        [Test]
        public void EndfieldPartStateAlwaysRestoresPartCullAndDebugInvariants()
        {
            var expected = new[]
            {
                ("Body", 0f, 0f),
                ("Skin", 1f, 2f),
                ("Hair", 2f, 2f),
                ("Face", 3f, 2f),
                ("Eye", 4f, 2f),
                ("Mouth", 5f, 2f),
                ("Overlay", 6f, 0f),
                ("Effect", 7f, 0f),
                ("HairShadow", 8f, 0f),
            };
            foreach (var item in expected)
            {
                var material = new Material(
                    Shader.Find("MIKU/Endfield/" + item.Item1));
                try
                {
                    if (material.HasProperty("_PartMode"))
                        material.SetFloat("_PartMode", 99f);
                    if (material.HasProperty("_Cull"))
                        material.SetFloat("_Cull", 1f);
                    if (material.HasProperty("_DebugView"))
                        material.SetFloat("_DebugView", 9f);
                    MikuFixedWorkflowTextureBindings.ApplyEndfieldPartState(
                        material,
                        "endfield_toon");
                    Assert.That(
                        material.GetFloat("_PartMode"),
                        Is.EqualTo(item.Item2),
                        item.Item1);
                    Assert.That(
                        material.GetFloat("_Cull"),
                        Is.EqualTo(item.Item3),
                        item.Item1);
                    if (material.HasProperty("_DebugView"))
                        Assert.That(
                            material.GetFloat("_DebugView"),
                            Is.Zero,
                            item.Item1);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
        }

        [Test]
        public void EndfieldShadowBaseMapBindingsArePartAwareAndRejectAmbiguity()
        {
            var hairMaterial = new Material(
                Shader.Find("MIKU/Endfield/HairShadow"));
            var overlayMaterial = new Material(
                Shader.Find("MIKU/Endfield/Overlay"));
            var bodyMaterial = new Material(
                Shader.Find("MIKU/Endfield/Body"));
            var baseTexture = new Texture2D(1, 1);
            var hairTexture = new Texture2D(1, 1);
            var eyeTexture = new Texture2D(1, 1);
            try
            {
                MikuFixedWorkflowTextureBindings.Bind(
                    hairMaterial,
                    "endfield_toon",
                    new[]
                    {
                        Binding("BaseMap", baseTexture),
                        Binding("HairShadowMap", hairTexture),
                    });
                Assert.That(
                    hairMaterial.GetTexture("_BaseMap"),
                    Is.SameAs(hairTexture));

                MikuFixedWorkflowTextureBindings.Bind(
                    overlayMaterial,
                    "endfield_toon",
                    new[]
                    {
                        Binding("BaseMap", baseTexture),
                        Binding("EyeShadowMap", eyeTexture),
                    });
                Assert.That(
                    overlayMaterial.GetTexture("_BaseMap"),
                    Is.SameAs(eyeTexture));

                bodyMaterial.SetTexture("_BaseMap", baseTexture);
                MikuFixedWorkflowTextureBindings.Bind(
                    bodyMaterial,
                    "endfield_toon",
                    new[] { Binding("HairShadowMap", hairTexture) });
                Assert.That(
                    bodyMaterial.GetTexture("_BaseMap"),
                    Is.SameAs(baseTexture));

                var conflict = Assert.Throws<InvalidOperationException>(() =>
                    MikuFixedWorkflowTextureBindings.Bind(
                        overlayMaterial,
                        "endfield_toon",
                        new[]
                        {
                            Binding("HairShadowMap", hairTexture),
                            Binding("EyeShadowMap", eyeTexture),
                        }));
                Assert.That(
                    conflict.Message,
                    Is.EqualTo(
                        "MIKU_ENDFIELD_SHADOW_BASEMAP_ROLE_CONFLICT"));
                var missing = Assert.Throws<InvalidOperationException>(() =>
                    MikuFixedWorkflowTextureBindings.Bind(
                        hairMaterial,
                        "endfield_toon",
                        Array.Empty<MikuToonTextureBinding>()));
                Assert.That(
                    missing.Message,
                    Is.EqualTo(
                        "MIKU_ENDFIELD_HAIR_SHADOW_TEXTURE_REQUIRED"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baseTexture);
                UnityEngine.Object.DestroyImmediate(hairTexture);
                UnityEngine.Object.DestroyImmediate(eyeTexture);
                UnityEngine.Object.DestroyImmediate(hairMaterial);
                UnityEngine.Object.DestroyImmediate(overlayMaterial);
                UnityEngine.Object.DestroyImmediate(bodyMaterial);
            }
        }

        [Test]
        public void HairShadowSelectionWithoutTextureLeavesRecipeAndMaterialUnchanged()
        {
            var material = new Material(Shader.Find("MIKU/Endfield/Body"));
            var recipe = ScriptableObject.CreateInstance<MikuToonMaterialRecipe>();
            try
            {
                material.SetFloat("_Cull", 1f);
                material.SetFloat("_PartMode", 42f);
                material.renderQueue = 2450;
                material.EnableKeyword("MIKU_TRANSACTION_SENTINEL");
                recipe.generatedBaseMaterial = material;
                recipe.workflowKind = "endfield_toon";
                recipe.gamePart = MikuGameMaterialPart.HairShadow;
                recipe.textureBindings = Array.Empty<MikuToonTextureBinding>();

                var materialBefore = EditorJsonUtility.ToJson(material);
                var shaderBefore = material.shader;
                var keywordsBefore = material.shaderKeywords.ToArray();
                var renderQueueBefore = material.renderQueue;
                var partBefore = recipe.gamePart;

                var error = Assert.Throws<InvalidOperationException>(() =>
                    MikuToonRecipeUtility.ApplySelection(recipe));

                Assert.That(
                    error.Message,
                    Is.EqualTo("MIKU_ENDFIELD_HAIR_SHADOW_TEXTURE_REQUIRED"));
                Assert.That(material.shader, Is.SameAs(shaderBefore));
                Assert.That(
                    EditorJsonUtility.ToJson(material),
                    Is.EqualTo(materialBefore));
                CollectionAssert.AreEqual(keywordsBefore, material.shaderKeywords);
                Assert.That(material.renderQueue, Is.EqualTo(renderQueueBefore));
                Assert.That(recipe.gamePart, Is.EqualTo(partBefore));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recipe);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void EndfieldVolumeContainsOnlyNeutralBloomAndVignette()
        {
            var path = TestFolder + "/EndfieldProfile.asset";
            var profile = MikuEndfieldPostVolumeProfileFactory.CreateOrUpdate(
                path);
            Assert.That(
                profile.components.Select(item => item.GetType()),
                Is.EqualTo(new[]
                {
                    typeof(Tonemapping),
                    typeof(Bloom),
                    typeof(Vignette),
                }));
            Assert.That(profile.components.All(item => item.active), Is.True);
            Assert.That(profile.TryGet(out Tonemapping tonemapping), Is.True);
            AssertOverride(tonemapping.mode, TonemappingMode.Neutral);
            Assert.That(profile.TryGet(out Bloom bloom), Is.True);
            AssertOverride(bloom.threshold, 0.85f);
            AssertOverride(bloom.intensity, 0.20f);
            AssertOverride(bloom.scatter, 0.65f);
            AssertOverride(bloom.clamp, 4f);
            AssertOverride(bloom.highQualityFiltering, true);
            Assert.That(profile.TryGet(out Vignette vignette), Is.True);
            AssertOverride(vignette.intensity, 0.04f);

            var firstIds = profile.components
                .Select(LocalId)
                .ToArray();
            profile = MikuEndfieldPostVolumeProfileFactory.CreateOrUpdate(path);
            Assert.That(profile.components.Count, Is.EqualTo(3));
            Assert.That(
                profile.components.Select(
                    LocalId),
                Is.EqualTo(firstIds));
        }

        [Test]
        public void LutInstallerConfiguresImportFeatureAndAssetsIdempotently()
        {
            var renderer = CreateRendererData();
            var texture = CreateLut("GameLut.png");
            var output = TestFolder + "/Output";
            var first = MikuEndfieldPostProcessingInstaller.Install(
                renderer,
                texture,
                output,
                null);
            Assert.That(first.createdFeature, Is.True);
            Assert.That(
                MikuEndfieldPostProcessingInstaller.CountFeatures(renderer),
                Is.EqualTo(1));
            Assert.That(first.material.GetTexture("_LutTex"), Is.SameAs(first.lut));
            Assert.That(first.material.GetFloat("_Intensity"), Is.EqualTo(1f));
            Assert.That(first.profile.components.Count, Is.EqualTo(3));
            AssertFeature(first.feature, first.material);
            AssertLutImporter(first.lut, expectedMipmaps: false);

            var second = MikuEndfieldPostProcessingInstaller.Install(
                renderer,
                first.lut,
                output,
                null);
            Assert.That(second.createdFeature, Is.False);
            AssertSameAssetIdentity(first.feature, second.feature);
            AssertSameAssetIdentity(first.material, second.material);
            AssertSameAssetIdentity(first.profile, second.profile);
            Assert.That(
                MikuEndfieldPostProcessingInstaller.CountFeatures(renderer),
                Is.EqualTo(1));
        }

        [Test]
        public void LutInstallerDoesNotSaveUnrelatedDirtyAssets()
        {
            var probePath = TestFolder + "/Unrelated.asset";
            var probe = ScriptableObject.CreateInstance<SaveIsolationProbe>();
            probe.value = 1;
            AssetDatabase.CreateAsset(probe, probePath);
            AssetDatabase.SaveAssetIfDirty(probe);
            var bytesBefore = File.ReadAllBytes(probePath);
            probe.value = 2;
            EditorUtility.SetDirty(probe);
            var renderer = CreateRendererData();
            var texture = CreateLut("IsolationLut.png");

            MikuEndfieldPostProcessingInstaller.Install(
                renderer,
                texture,
                TestFolder + "/IsolationOutput",
                null);

            CollectionAssert.AreEqual(
                bytesBefore,
                File.ReadAllBytes(probePath));
            Assert.That(probe.value, Is.EqualTo(2));
            Assert.That(EditorUtility.IsDirty(probe), Is.True);
        }

        [Test]
        public void LutInstallerFailureRollsBackFeatureAssetsAndImportSettings()
        {
            var renderer = CreateRendererData();
            var texture = CreateLut("RollbackLut.png");
            var path = AssetDatabase.GetAssetPath(texture);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Compressed;
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var output = TestFolder + "/RollbackOutput";

            Assert.Throws<IOException>(() =>
                MikuEndfieldPostProcessingInstaller.Install(
                    renderer,
                    texture,
                    output,
                    () => throw new IOException("synthetic failure")));

            Assert.That(
                MikuEndfieldPostProcessingInstaller.CountFeatures(renderer),
                Is.Zero);
            Assert.That(AssetDatabase.IsValidFolder(output), Is.False);
            importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.sRGBTexture, Is.False);
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Compressed));
            Assert.That(
                importer.GetPlatformTextureSettings("Standalone").overridden,
                Is.True);
        }

        [Test]
        public void LutInstallerRejectsWrongLayoutAndNonProjectTextures()
        {
            var wrong = new Texture2D(32, 32);
            var transient = new Texture2D(1024, 32);
            try
            {
                var layout = Assert.Throws<InvalidOperationException>(() =>
                    MikuEndfieldPostProcessingInstaller.ValidateLutLayout(
                        wrong));
                Assert.That(
                    layout.Message,
                    Is.EqualTo(
                        "MIKU_ENDFIELD_LUT_LAYOUT_INVALID:" +
                        "expected=1024x32:actual=32x32"));
                var ownership = Assert.Throws<InvalidOperationException>(() =>
                    MikuEndfieldPostProcessingInstaller.ValidateLutLayout(
                        transient));
                Assert.That(
                    ownership.Message,
                    Is.EqualTo(
                        "MIKU_ENDFIELD_LUT_PROJECT_ASSET_REQUIRED"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wrong);
                UnityEngine.Object.DestroyImmediate(transient);
            }
        }

        [Test]
        public void FullScreenLutShaderPreservesHdrPeakAndAlpha()
        {
            var shader = Shader.Find(
                MikuEndfieldPostProcessingInstaller.LutShaderName);
            Assert.That(shader, Is.Not.Null);
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Packages/com.miku.shaderconverter/Runtime/Endfield/" +
                "MikuEndfieldFullScreenColorLut.shader"));
            var source = File.ReadAllText(path);
            StringAssert.Contains("const float size = 32.0", source);
            StringAssert.Contains("max(source.r, max(source.g, source.b))", source);
            StringAssert.Contains("MikuLinearToSrgb(normalized)", source);
            StringAssert.Contains("MikuSampleFlattenedLut(encoded) * peak", source);
            StringAssert.Contains("source.a", source);
            StringAssert.Contains("if (intensity <= 0.0)", source);
            StringAssert.DoesNotContain("ColorLookup", source);
        }

        static JObject GameIr(string workflow, string part)
        {
            return new JObject
            {
                ["documentKind"] = "miku-material-ir-2.0",
                ["schemaVersion"] = "2.0",
                ["workflow"] = new JObject
                {
                    ["kind"] = workflow,
                    ["part"] = part,
                },
            };
        }

        static MikuToonTextureBinding Binding(string role, Texture texture)
        {
            return new MikuToonTextureBinding
            {
                role = role,
                texture = texture,
            };
        }

        static UniversalRendererData CreateRendererData()
        {
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = "Renderer";
            AssetDatabase.CreateAsset(
                renderer,
                TestFolder + "/Renderer.asset");
            return renderer;
        }

        static Texture2D CreateLut(string fileName)
        {
            var source = new Texture2D(
                1024,
                32,
                TextureFormat.RGBA32,
                false,
                true);
            try
            {
                var pixels = Enumerable.Repeat(
                        new Color32(128, 128, 128, 255),
                        1024 * 32)
                    .ToArray();
                source.SetPixels32(pixels);
                source.Apply(false, false);
                var assetPath = TestFolder + "/" + fileName;
                var absolute = Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "..",
                    assetPath));
                File.WriteAllBytes(absolute, source.EncodeToPNG());
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        static void AssertFeature(
            FullScreenPassRendererFeature feature,
            Material material)
        {
            var serialized = new SerializedObject(feature);
            Assert.That(feature.isActive, Is.True);
            var injection = serialized.FindProperty("injectionPoint");
            var requirements = serialized.FindProperty("requirements");
            var passMaterial = serialized.FindProperty("passMaterial");
            var passIndex = serialized.FindProperty("passIndex");
            var fetch = serialized.FindProperty("fetchColorBuffer");
            var bindDepth = serialized.FindProperty(
                "bindDepthStencilAttachment");
            Assert.That(
                injection.enumNames[injection.enumValueIndex],
                Is.EqualTo("BeforeRenderingPostProcessing"));
            Assert.That(requirements.intValue, Is.Zero);
            AssertSameAssetIdentity(material, passMaterial.objectReferenceValue);
            Assert.That(passIndex.intValue, Is.Zero);
            Assert.That(fetch.boolValue, Is.True);
            Assert.That(bindDepth.boolValue, Is.False);
        }

        static void AssertLutImporter(
            Texture2D texture,
            bool expectedMipmaps)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(
                AssetDatabase.GetAssetPath(texture));
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.mipmapEnabled, Is.EqualTo(expectedMipmaps));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(
                importer.GetPlatformTextureSettings("Standalone").overridden,
                Is.False);
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

        static long LocalId(UnityEngine.Object value)
        {
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    value,
                    out _,
                    out long localId),
                Is.True);
            return localId;
        }

        static void AssertSameAssetIdentity(
            UnityEngine.Object expected,
            UnityEngine.Object actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(actual),
                Is.EqualTo(AssetDatabase.GetAssetPath(expected)));
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    expected,
                    out string expectedGuid,
                    out long expectedLocalId),
                Is.True);
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    actual,
                    out string actualGuid,
                    out long actualLocalId),
                Is.True);
            Assert.That(actualGuid, Is.EqualTo(expectedGuid));
            Assert.That(actualLocalId, Is.EqualTo(expectedLocalId));
        }
    }
}
