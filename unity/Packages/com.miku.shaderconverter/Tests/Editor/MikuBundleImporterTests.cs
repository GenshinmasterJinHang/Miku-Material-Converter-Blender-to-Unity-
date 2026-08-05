using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Miku.ShaderConverter.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Miku.ShaderConverter.Editor.Tests
{
    public sealed class MikuBundleImporterTests
    {
        const string OutputRoot = "Assets/MikuTests/BundleImporter";
        const string TestPipelinePath = "Assets/MikuTests/TestUrpPipeline.asset";
        const string CurrentProfileHash = "b08ac3e4506bf127709cef9b42679dbca836615e62eaf2df9b4ca79ff6393f16";
        const string CurrentProfileHashV2 = "4970ecd6266173f8c60341e10fd26eafe1cbd6d918428aacea5b3e40eef46ff6";
        const string CurrentProfileHashV22 = "50bb9fb048707256b3882a757253a3fc685e791395b5bc9872fb7daf98129848";
        const string CurrentProfileHashV21 = "e847380c02ecf8e16e4496a0709b7ccf8946f71b4cc16622f901bcc41768f331";
        const string Package103ProfileHash = "b9e8f39f08ed1d76da8e6af18ae58e14ea84cc05a009a0b7d4479978d629841b";
        const string Package202ProfileHash = "4e30b6e4da6d9d1c7a3e2805355ac5354fa751b14e2458c162099cbc2d10b397";
        const string Package200And201ProfileHash = "549551f13909f1c56da9effb58a635eb3e813e9be4c17325211c53abc1ea997c";
        const string Package120ProfileHash = "72d2487e908af41734e6c6212232f5080b47cab7e09af536c552160b71de628d";
        const string PreviousProfileHash = "b5198d826633a92f5c712cd7337d7f722edd238d3ae1ab42778dd6b780e491b3";
        const string Package110ProfileHash = "e5af9bcb4e02c54e556d8aed0653182d767b841cf3705b46be653dbf8c914b4a";
        const string Exporter110ProfileHash = "a42e43993e27ec18f409b1d574ab2ecc088c93de0a03b2c0ca66f3fbd25b1890";
        const string Version100ProfileHash = "a251a0e02eee217296349135b27974060d4f040cda1c1419423ec410ec844e89";
        const string LegacyPresentationProfileHash = "7793b8dfcd7c766360ca686a48bfd2309731179e87bb5330b5600fbfd893197a";
        string bundleRoot;
        RenderPipelineAsset previousDefaultPipeline;
        RenderPipelineAsset previousQualityPipeline;
        UniversalRenderPipelineAsset testPipeline;

        [TestCase("6000.0.0f1", true)]
        [TestCase("6000.2.9f1", true)]
        [TestCase("6000.4.5f1", true)]
        [TestCase("6000.5.0f1", true)]
        [TestCase("6000.5.4f1", false)]
        [TestCase("6000.6.0f1", true)]
        public void UnityMajorVersionAcceptedWithWarningsWhenNotCertified(
            string version,
            bool expectsWarning)
        {
            var diagnostics = new List<string>();
            MikuRuntimeCompatibility.ValidateUnityVersion(version, diagnostics);
            Assert.That(
                diagnostics.Any(item => item.StartsWith(
                    "MIKU_UNITY_VERSION_UNVALIDATED:",
                    StringComparison.Ordinal)),
                Is.EqualTo(expectsWarning));
        }

        [TestCase("5999.9.9f1")]
        [TestCase("6001.0.0f1")]
        [TestCase("7000.0.0f1")]
        [TestCase("not-a-version")]
        public void UnityWrongMajorRejected(
            string version)
        {
            var error = Assert.Throws<InvalidDataException>(() =>
                MikuRuntimeCompatibility.ValidateUnityVersion(
                    version,
                    new List<string>()));
            Assert.That(
                error?.Message,
                Does.StartWith("MIKU_UNITY_VERSION_UNSUPPORTED:"));
        }

        [TestCase("17.0.0", true)]
        [TestCase("17.2.4", true)]
        [TestCase("17.4.0", true)]
        [TestCase("17.5.4", false)]
        [TestCase("17.6.0", true)]
        [TestCase("17.0.0-preview.1", true)]
        public void PackageMajorVersionAcceptedWithWarningsWhenNotCertified(
            string version,
            bool expectsWarning)
        {
            var diagnostics = new List<string>();
            MikuRuntimeCompatibility.ValidatePackageVersion(
                version,
                "MIKU_URP_VERSION_UNSUPPORTED",
                "MIKU_URP_VERSION_UNVALIDATED",
                diagnostics);
            Assert.That(diagnostics.Count > 0, Is.EqualTo(expectsWarning));
        }

        [TestCase("16.9.9")]
        [TestCase("16.5.0")]
        [TestCase("18.0.0")]
        [TestCase("19.0.0")]
        [TestCase("missing")]
        public void PackageWrongMajorRejected(string version)
        {
            var error = Assert.Throws<InvalidDataException>(() =>
                MikuRuntimeCompatibility.ValidatePackageVersion(
                    version,
                    "MIKU_URP_VERSION_UNSUPPORTED",
                    "MIKU_URP_VERSION_UNVALIDATED",
                    new List<string>()));
            Assert.That(
                error?.Message,
                Does.StartWith("MIKU_URP_VERSION_UNSUPPORTED:"));
        }

        [Test]
        public void VersionParsersOrderPrereleaseBeforeFinalRelease()
        {
            Assert.That(
                MikuPackageVersion.Parse("17.4.0-preview.1").CompareTo(
                    MikuPackageVersion.Parse("17.4.0")),
                Is.LessThan(0));
            Assert.That(
                MikuUnityVersion.Parse("6000.4.5rc1").CompareTo(
                    MikuUnityVersion.Parse("6000.4.5f1")),
                Is.LessThan(0));
        }

        [TestCase("17.0.0", "ShaderGraph17_0Adapter")]
        [TestCase("17.1.3", "ShaderGraph17_1Adapter")]
        [TestCase("17.2.0", "ShaderGraph17_2Adapter")]
        [TestCase("17.3.1", "ShaderGraph17_3Adapter")]
        [TestCase("17.4.0", "ShaderGraph17_4Adapter")]
        [TestCase("17.5.0", "ShaderGraph17_5Adapter")]
        [TestCase("17.6.0", "ShaderGraph17_6Adapter")]
        [TestCase("17.7.0", "ShaderGraph17_6Adapter")]
        [TestCase("17.99.0", "ShaderGraph17_6Adapter")]
        public void ShaderGraphMinorSelectsExplicitAdapter(
            string version,
            string expected)
        {
            Assert.That(
                MikuShaderGraph17RuntimeBackend.AdapterNameForVersion(version),
                Is.EqualTo(expected));
        }

        [Test]
        public void PortableHybridRejectsMeshBoundBundleResources()
        {
            var plan = new JObject
            {
                ["mode"] = "PreferNative",
                ["bakeJobs"] = new JArray
                {
                    new JObject
                    {
                        ["route"] = "ReusableBake",
                        ["coordinateDomain"] = "UV0",
                        ["meshBindingRequired"] = false,
                    },
                },
            };
            var bundle = new JObject
            {
                ["resources"] = new JArray
                {
                    new JObject
                    {
                        ["kind"] = "Texture2D",
                        ["semantic"] = "BakedExpression",
                    },
                },
            };
            Assert.DoesNotThrow(() => InvokeVoid(
                "ValidatePortableHybridBundle",
                plan,
                bundle));

            ((JObject)((JArray)bundle["resources"])[0])["meshBinding"] =
                new JObject { ["sha256"] = new string('0', 64) };
            var error = Assert.Throws<TargetInvocationException>(() =>
                InvokeVoid(
                    "ValidatePortableHybridBundle",
                    plan,
                    bundle));
            Assert.That(
                error?.InnerException?.Message,
                Does.Contain("MIKU_PORTABLE_RESOURCE_MESH_BOUND"));
        }

        [SetUp]
        public void SetUp()
        {
            previousDefaultPipeline = GraphicsSettings.defaultRenderPipeline;
            previousQualityPipeline = QualitySettings.renderPipeline;
            if (!AssetDatabase.IsValidFolder("Assets/MikuTests"))
                AssetDatabase.CreateFolder("Assets", "MikuTests");
            testPipeline = UniversalRenderPipelineAsset.Create();
            AssetDatabase.CreateAsset(testPipeline, TestPipelinePath);
            var serializedPipeline = new SerializedObject(testPipeline);
            var rendererDataList =
                serializedPipeline.FindProperty("m_RendererDataList");
            for (var index = 0;
                 rendererDataList != null &&
                 index < rendererDataList.arraySize;
                 index++)
            {
                var rendererData = rendererDataList
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue;
                if (rendererData != null &&
                    !AssetDatabase.Contains(rendererData))
                    AssetDatabase.AddObjectToAsset(
                        rendererData,
                        testPipeline);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                TestPipelinePath,
                ImportAssetOptions.ForceSynchronousImport);
            testPipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                    TestPipelinePath);
            GraphicsSettings.defaultRenderPipeline = testPipeline;
            QualitySettings.renderPipeline = testPipeline;
            bundleRoot = Path.Combine(
                Directory.GetParent(UnityEngine.Application.dataPath).FullName,
                "Library",
                "MikuTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(bundleRoot);
        }

        [TearDown]
        public void TearDown()
        {
            QualitySettings.renderPipeline = previousQualityPipeline;
            GraphicsSettings.defaultRenderPipeline = previousDefaultPipeline;
            AssetDatabase.DeleteAsset("Assets/MikuTests");
            if (Directory.Exists(bundleRoot))
                Directory.Delete(bundleRoot, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void StableAssetGuidDoesNotDependOnBackendVersion()
        {
            var first = Invoke<string>("StableAssetGuid", "source", "material", "WrapperGraph");
            var second = Invoke<string>("StableAssetGuid", "source", "material", "WrapperGraph");
            var changedRole = Invoke<string>("StableAssetGuid", "source", "material", "GeneratedSubGraph");
            Assert.That(first, Has.Length.EqualTo(32));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(changedRole, Is.Not.EqualTo(first));
        }

        [TestCase("../outside.json")]
        [TestCase("C:/outside.json")]
        [TestCase("/outside.json")]
        [TestCase("AUX/data.json")]
        public void UnsafeRelativePathsAreRejected(string path)
        {
            var error = Assert.Throws<TargetInvocationException>(() => Invoke<string>("NormalizeRelativePath", path));
            Assert.That(error.InnerException, Is.TypeOf<InvalidDataException>());
        }

        [Test]
        public void EditableTexturePropertiesKeepCanonicalTemplateReferences()
        {
            Assert.That(
                Invoke<string>("EditableTextureProperty", "BaseColor"),
                Is.EqualTo("_BaseMap"));
            Assert.That(
                Invoke<string>("EditableTextureProperty", "Normal"),
                Is.EqualTo("_BumpMap"));
            Assert.That(
                Invoke<string>("EditableTextureProperty", "Roughness"),
                Is.EqualTo("_RoughnessMap"));
            Assert.That(
                Invoke<string>("EditableTextureProperty", "Height"),
                Is.EqualTo("_MIKU_HeightMap"));
        }

        [TestCase("standard_pbr", true)]
        [TestCase("genshin_toon", false)]
        [TestCase("wuwa_toon", false)]
        [TestCase("hsr_toon", false)]
        public void WorkflowRegistrySelectsOneBackendPerMaterialIr(
            string kind,
            bool editable)
        {
            var workflow = new JObject { ["kind"] = kind };
            if (!editable)
                workflow["part"] = "Body";
            var ir = new JObject
            {
                ["documentKind"] = "miku-material-ir-1.0",
                ["schemaVersion"] = "1.0",
                ["workflow"] = workflow,
            };

            var backend = MikuWorkflowBackends.Resolve(ir);
            Assert.That(backend.Kind, Is.EqualTo(kind));
            Assert.That(backend.UsesEditableGraph, Is.EqualTo(editable));
            Assert.That(
                backend.WrapperTemplatePath,
                Is.EqualTo(
                    kind == "standard_pbr"
                        ? MikuWorkflowBackends.StandardWrapperTemplate
                        : MikuWorkflowBackends.StandardWrapperTemplate));
        }

        [Test]
        public void WorkflowRegistryRejectsLegacyVersionField()
        {
            var ir = new JObject
            {
                ["documentKind"] = "miku-material-ir-1.0",
                ["schemaVersion"] = "1.0",
                ["version"] = "miku-4.0",
                ["workflow"] = new JObject { ["kind"] = "standard_pbr" },
            };

            var error = Assert.Throws<InvalidDataException>(
                () => MikuWorkflowBackends.Resolve(ir));
            Assert.That(error.Message, Is.EqualTo("MIKU_LEGACY_VERSION_FIELD"));
        }

        [Test]
        public void Bundle20MeshBoundTextureWithoutSourceMeshIsRejected()
        {
            var bundlePath = WriteValidBundle(
                includeResource: true,
                targetProfileHash: CurrentProfileHashV2,
                explicitMaterialIrV2: SurfaceIr(
                    "StandardLit",
                    "Opaque"),
                bundleKind: "migr-bundle-2.0");
            var bundle = JObject.Parse(File.ReadAllText(bundlePath));
            var resource = (JObject)((JArray)bundle["resources"])[0];
            resource["meshBinding"] = new JObject
            {
                ["kind"] = "MeshFingerprintSet",
                ["sha256"] = new string('2', 64),
                ["meshes"] = new JArray(),
            };
            bundle["sealedDigest"] =
                Invoke<string>("ComputeSealedDigest", bundle);
            bundle["canonicalHash"] = Invoke<string>(
                "CanonicalHash",
                bundle,
                "canonicalHash");
            File.WriteAllText(
                bundlePath,
                bundle.ToString(Formatting.None),
                new UTF8Encoding(false));

            var result = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = bundlePath,
                    outputRoot = OutputRoot,
                });

            Assert.That(result.success, Is.False);
            Assert.That(
                result.diagnostics,
                Has.Some.Contains(
                    "MIKU_LEGACY_MESH_BOUND_BUNDLE_UNSAFE"));
        }

        [Test]
        public void LegacyBundle21SourceMeshDoesNotCreatePrefabOrBindings()
        {
            var bundlePath = WriteValidBundle(
                sourceId: "source-mesh-fixture",
                materialId: "source-mesh-material",
                sourceName: "SourceMeshFixture",
                targetProfileHash: CurrentProfileHashV21,
                explicitMaterialIrV2: SurfaceModelIr2("OpaquePBR"),
                includeSourceMesh: true,
                bundleKind: "migr-bundle-2.1");

            var result = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = bundlePath,
                    outputRoot = OutputRoot + "/SourceMesh",
                    createMaterialVariant = true,
                });

            Assert.That(
                result.success,
                Is.True,
                string.Join(" | ", result.diagnostics));
            Assert.That(
                result.diagnostics,
                Has.Some.EqualTo(
                    "MIKU_SOURCE_MESH_IGNORED_EXPLICIT_TOOL_REQUIRED"));
            Assert.That(
                result.assetPaths,
                Has.None.Matches<string>(path =>
                    path.EndsWith(".source.glb", StringComparison.Ordinal) ||
                    path.EndsWith(".prefab", StringComparison.Ordinal) ||
                    path.EndsWith(
                        ".meshbinding.asset",
                        StringComparison.Ordinal)));
        }

        [Test]
        public void CurrentMikuSourceMeshCreatesStablePrefabAndBinding()
        {
            var bundlePath = WriteValidBundle(
                sourceId: "miku-source-mesh-fixture",
                materialId: "miku-source-mesh-material",
                sourceName: "MikuSourceMeshFixture",
                targetProfileHash: CurrentProfileHash,
                explicitMaterialIrV2: SurfaceModelIr2("OpaquePBR"),
                includeSourceMesh: true,
                bundleKind: "miku-bundle-1.0");
            var request = new MikuImportRequest
            {
                bundlePath = bundlePath,
                outputRoot = OutputRoot,
                createMaterialVariant = false,
            };

            var first = MikuBundleImporter.Import(request);
            Assert.That(
                first.success,
                Is.True,
                string.Join(" | ", first.diagnostics));
            Assert.That(
                first.diagnostics,
                Has.Some.StartsWith("MIKU_SOURCE_MESH_FIDELITY_PREFAB:"));
            var prefabPath = first.assetPaths.Single(path =>
                path.EndsWith(".prefab", StringComparison.Ordinal));
            var bindingPath = first.assetPaths.Single(path =>
                path.EndsWith(
                    ".meshbinding.asset",
                    StringComparison.Ordinal));
            var glbPath = first.assetPaths.Single(path =>
                path.EndsWith(".source.glb", StringComparison.Ordinal));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var description =
                AssetDatabase.LoadAssetAtPath<MikuMeshBindingDescription>(
                    bindingPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(description, Is.Not.Null);
            Assert.That(description.generatedPrefab, Is.EqualTo(prefab));
            Assert.That(description.material, Is.Not.Null);
            Assert.That(description.rendererBindings, Has.Count.EqualTo(1));
            Assert.That(
                description.rendererBindings[0].unityMeshFingerprint,
                Has.Length.EqualTo(64));
            var importedMesh = prefab
                .GetComponentsInChildren<MeshFilter>(true)
                .Single()
                .sharedMesh;
            Assert.That(importedMesh.vertexCount, Is.EqualTo(3));
            Assert.That(importedMesh.subMeshCount, Is.EqualTo(1));
            Assert.That(
                importedMesh.HasVertexAttribute(VertexAttribute.TexCoord0),
                Is.True);
            var prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            var bindingGuid = AssetDatabase.AssetPathToGUID(bindingPath);
            var glbGuid = AssetDatabase.AssetPathToGUID(glbPath);

            var second = MikuBundleImporter.Import(request);
            Assert.That(
                second.success,
                Is.True,
                string.Join(" | ", second.diagnostics));
            Assert.That(
                AssetDatabase.AssetPathToGUID(prefabPath),
                Is.EqualTo(prefabGuid));
            Assert.That(
                AssetDatabase.AssetPathToGUID(bindingPath),
                Is.EqualTo(bindingGuid));
            Assert.That(
                AssetDatabase.AssetPathToGUID(glbPath),
                Is.EqualTo(glbGuid));
        }

        [TestCase(
            "StandardLit",
            "Opaque",
            "MikuStandardTemplate.shadergraph")]
        [TestCase(
            "StandardLit",
            "AlphaBlend",
            "MikuAlphaBlendTemplate.shadergraph")]
        [TestCase(
            "StandardLit",
            "Dithered",
            "MikuDitheredTemplate.shadergraph")]
        [TestCase(
            "DielectricScreenRefraction",
            "AlphaBlend",
            "MikuDielectricTemplate.shadergraph")]
        public void SurfaceContractSelectsVersionedWrapper(
            string model,
            string renderMethod,
            string expectedFile)
        {
            var ir = SurfaceIr(model, renderMethod);
            var backend = MikuWorkflowBackends.Resolve(ir);
            Assert.That(
                backend.WrapperTemplatePath,
                Does.EndWith(expectedFile));
        }

        [Test]
        public void SurfaceContractRejectsUnknownCompanionSchema()
        {
            var ir = SurfaceIr("StandardLit", "AlphaBlend");
            ir["surfaceContract"]["schema"] = "miku-surface-9.0";
            var error = Assert.Throws<InvalidDataException>(
                () => MikuWorkflowBackends.Resolve(ir));
            Assert.That(
                error.Message,
                Is.EqualTo("MIKU_SURFACE_CONTRACT_INVALID:schema"));
        }

        [TestCase(null, 0)]
        [TestCase("Alpha", 0)]
        [TestCase("Premultiply", 1)]
        [TestCase("Additive", 2)]
        [TestCase("Multiply", 3)]
        public void WrapperUsesShaderGraph17AlphaModeMapping(
            string blendMode,
            int expectedAlphaMode)
        {
            var contract = (JObject)SurfaceIr(
                "StandardLit",
                "AlphaBlend")["surfaceContract"];
            if (blendMode != null)
                contract["blendMode"] = blendMode;
            var wrapper = MikuShaderGraph17RuntimeBackend
                .ApplyWrapperContract(
                    File.ReadAllText(
                        ToAbsolute(
                            MikuWorkflowBackends
                                .AlphaBlendWrapperTemplate),
                        Encoding.UTF8),
                    contract);
            var target = ParseMultiJson(wrapper).Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(
                    ".UniversalTarget",
                    StringComparison.Ordinal));

            Assert.That(
                target["m_AlphaMode"]?.Value<int>(),
                Is.EqualTo(expectedAlphaMode));
        }

        [Test]
        public void WrapperRejectsUnknownExplicitBlendMode()
        {
            var contract = (JObject)SurfaceIr(
                "StandardLit",
                "AlphaBlend")["surfaceContract"];
            contract["blendMode"] = "FutureBlend";

            var error = Assert.Throws<InvalidDataException>(() =>
                MikuShaderGraph17RuntimeBackend.ApplyWrapperContract(
                    File.ReadAllText(
                        ToAbsolute(
                            MikuWorkflowBackends
                                .AlphaBlendWrapperTemplate),
                        Encoding.UTF8),
                    contract));

            Assert.That(
                error.Message,
                Is.EqualTo(
                    "MIKU_BLEND_MODE_UNSUPPORTED:FutureBlend"));
        }

        [TestCase("OpaquePBR", "MikuStandardTemplate.shadergraph")]
        [TestCase("CutoutPBR", "MikuDitheredTemplate.shadergraph")]
        [TestCase("TransparentLit", "MikuDielectricTemplate.shadergraph")]
        [TestCase(
            "TransparentEmission",
            "MikuDielectricTemplate.shadergraph")]
        [TestCase("RefractiveGlass", "MikuDielectricTemplate.shadergraph")]
        [TestCase("CustomMultiLobe", "MikuDielectricTemplate.shadergraph")]
        public void SurfaceModelRegistrySelectsVersionedGenerator(
            string kind,
            string expectedTemplate)
        {
            var ir = SurfaceModelIr2(kind);

            var generator = MikuSurfaceModelBackends.Resolve(ir);

            Assert.That(generator.Kind, Is.EqualTo(kind));
            Assert.That(
                generator.WrapperTemplatePath,
                Does.EndWith(expectedTemplate));
            Assert.That(
                MikuWorkflowBackends.Resolve(ir).WrapperTemplatePath,
                Is.EqualTo(generator.WrapperTemplatePath));
        }

        [Test]
        public void OpaquePrincipledCoatUsesUnityAuthoredClearCoatWrapper()
        {
            var ir = ClearCoatSurfaceModelIr2();
            var generator = MikuSurfaceModelBackends.Resolve(ir);

            Assert.That(
                generator.WrapperTemplatePath,
                Does.EndWith("MikuClearCoatTemplate.shadergraph"));
            Assert.That(
                MikuWorkflowBackends.Resolve(ir).WrapperTemplatePath,
                Is.EqualTo(generator.WrapperTemplatePath));
            Assert.That(
                generator.WrapperContract(ir)["clearCoat"]?.Value<bool>(),
                Is.True);
        }

        [Test]
        public void ClearCoatOutputsCompileAndConnectToUrpMasterStack()
        {
            var ir = ClearCoatSurfaceModelIr2();
            var generator = MikuSurfaceModelBackends.Resolve(ir);
            const string materialId = "clear-coat-fixture";
            const string subGraphGuid =
                "2c2bf4b114dc4a9392433214a04aa92f";
            var generated = generator.GenerateSubGraph(ir, materialId);

            Assert.That(generated, Does.Contain("\"m_DisplayName\": \"Coat Mask\""));
            Assert.That(
                generated,
                Does.Contain("\"m_DisplayName\": \"Coat Smoothness\""));
            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.OneMinusNode"));
            Assert.That(
                generator.GenerateSubGraph(ir, materialId),
                Is.EqualTo(generated));

            var contract = generator.WrapperContract(ir);
            var wrapper = MikuShaderGraph17RuntimeBackend.GenerateWrapper(
                File.ReadAllText(
                    ToAbsolute(generator.WrapperTemplatePath),
                    Encoding.UTF8),
                generated,
                materialId,
                subGraphGuid,
                contract);
            wrapper = MikuShaderGraph17RuntimeBackend.ApplyWrapperContract(
                wrapper,
                contract);
            var objects = ParseMultiJson(wrapper);
            var target = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(".UniversalTarget", StringComparison.Ordinal));
            var subTargetId =
                target["m_ActiveSubTarget"]?["m_Id"]?.Value<string>();
            var subTarget = objects.Single(item =>
                string.Equals(
                    item["m_ObjectId"]?.Value<string>(),
                    subTargetId,
                    StringComparison.Ordinal));

            Assert.That(subTarget["m_ClearCoat"]?.Value<bool>(), Is.True);
            AssertDirectWrapperEdge(
                wrapper,
                "Coat Mask",
                "SurfaceDescription.CoatMask");
            AssertDirectWrapperEdge(
                wrapper,
                "Coat Smoothness",
                "SurfaceDescription.CoatSmoothness");
            Assert.That(
                MikuShaderGraph17RuntimeBackend
                    .WrapperRenderContractMatches(wrapper, contract),
                Is.True);
        }

        [Test]
        public void ClearCoatContractRejectsExistingStandardWrapper()
        {
            var ir = ClearCoatSurfaceModelIr2();
            var contract = MikuSurfaceModelBackends
                .Resolve(ir)
                .WrapperContract(ir);
            var standardWrapper = File.ReadAllText(
                ToAbsolute(
                    MikuWorkflowBackends.StandardWrapperTemplate),
                Encoding.UTF8);

            Assert.That(
                MikuShaderGraph17RuntimeBackend
                    .WrapperRenderContractMatches(
                        standardWrapper,
                        contract),
                Is.False);
        }

        [Test]
        public void MaterialJsonAllowsCorpusDepthButRetainsABoundedLimit()
        {
            var method = typeof(MikuBundleImporter).GetMethod(
                "ParseJson",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(
                    typeof(MikuBundleImporter).FullName,
                    "ParseJson");
            JToken supported = new JValue(0);
            for (var index = 0; index < 96; index++)
                supported = new JObject { ["value"] = supported };
            var supportedPath = Path.Combine(bundleRoot, "depth-96.json");
            File.WriteAllText(
                supportedPath,
                supported.ToString(Formatting.None),
                new UTF8Encoding(false));

            var parsed = method.Invoke(
                null,
                new object[] { supportedPath, "MIKU_TEST_JSON_INVALID" });

            Assert.That(parsed, Is.TypeOf<JObject>());

            JToken excessive = new JValue(0);
            for (var index = 0; index < 160; index++)
                excessive = new JObject { ["value"] = excessive };
            var excessivePath = Path.Combine(bundleRoot, "depth-160.json");
            File.WriteAllText(
                excessivePath,
                excessive.ToString(Formatting.None),
                new UTF8Encoding(false));

            var raised = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(
                    null,
                    new object[]
                    {
                        excessivePath,
                        "MIKU_TEST_JSON_INVALID",
                    }));
            Assert.That(raised.InnerException, Is.TypeOf<InvalidDataException>());
            Assert.That(
                raised.InnerException.Message,
                Does.StartWith("MIKU_TEST_JSON_INVALID:"));
        }

        [Test]
        public void SourceMeshPbrProjectionIgnoresSupersededExpressionIsland()
        {
            var ir = SurfaceModelIr2("OpaquePBR");
            ir["surfaceModelPlan"]["approximations"] = new JArray
            {
                new JObject
                {
                    ["kind"] = "SourceMeshFidelityPbrProjection",
                    ["algorithmVersion"] = "miku-source-mesh-pbr-1",
                },
            };
            ir["resources"] = new JArray
            {
                new JObject
                {
                    ["semantic"] = "ExpressionIsland",
                    ["bindingKey"] = "_MIKU_Baked_superseded",
                },
            };
            ir["expressions"] = new JArray
            {
                new JObject
                {
                    ["id"] = "superseded-expression",
                    ["op"] = "Texture.SampleBaked2D",
                    ["valueType"] = "Color",
                    ["space"] = "None",
                    ["stage"] = "Fragment",
                    ["uniformity"] = "Varying",
                    ["inputs"] = new JObject(),
                    ["params"] = new JObject
                    {
                        ["referenceName"] = "_MIKU_Baked_superseded",
                        ["usage"] = "Color",
                        ["channel"] = "RGB",
                    },
                },
            };

            Assert.That(
                MikuSurfaceModelBackends.RequiresMaterialTextureBinding(
                    ir,
                    "_MIKU_Baked_superseded"),
                Is.False);
            Assert.That(
                MikuBundleImporter.RequiresMaterialTextureBinding(
                    ir,
                    "BaseColor"),
                Is.True);
            Assert.That(
                MikuBundleImporter.RequiresMaterialTextureBinding(
                    ir,
                    "IOR"),
                Is.False);
            Assert.That(
                MikuSurfaceModelBackends.Resolve(ir).WrapperTemplatePath,
                Is.EqualTo(MikuWorkflowBackends.StandardWrapperTemplate));
            Assert.DoesNotThrow(() =>
                MikuSurfaceModelBackends.Resolve(ir).GenerateSubGraph(
                    ir,
                    "source-mesh-projection"));
        }

        [Test]
        public void CustomMultiLobeSkipsNonAuthoritativeCompatibilityBindings()
        {
            var ir = SurfaceModelIr2("CustomMultiLobe");

            Assert.That(
                MikuBundleImporter.RequiresMaterialTextureBinding(ir, "IOR"),
                Is.False);
            Assert.That(
                MikuBundleImporter.RequiresMaterialTextureBinding(
                    ir,
                    "BaseColor"),
                Is.False);
            Assert.That(
                MikuBundleImporter.RequiresMaterialTextureBinding(
                    ir,
                    "Emission"),
                Is.False);
            Assert.That(
                MikuBundleImporter.RequiresMaterialTextureBinding(
                    ir,
                    "_MIKU_Baked_reachable"),
                Is.True);
        }

        [Test]
        public void CustomMultiLobeGeneratesImportableUnlitUrpGraph()
        {
            var ir = SurfaceModelIr2("CustomMultiLobe");
            var generator = MikuSurfaceModelBackends.Resolve(ir);
            const string materialId = "closure-aware-multi-lobe";
            const string subGraphGuid =
                "2feeb6ed731b44ab8cdf9d218e73d261";
            var generated = generator.GenerateSubGraph(ir, materialId);
            var objects = ParseMultiJson(generated);
            var customFunctions = objects.Where(item =>
                string.Equals(
                    item["m_Type"]?.Value<string>(),
                    "UnityEditor.ShaderGraph.CustomFunctionNode",
                    StringComparison.Ordinal)).ToArray();
            Assert.That(customFunctions, Has.Length.EqualTo(2));
            Assert.That(
                objects.Count(item => string.Equals(
                    item["m_Type"]?.Value<string>(),
                    "UnityEditor.ShaderGraph.AndNode",
                    StringComparison.Ordinal)),
                Is.EqualTo(2),
                "Each lobe must reject non-finite normal magnitudes.");
            Assert.That(
                customFunctions.All(item => string.Equals(
                    item["m_FunctionSource"]?.Value<string>(),
                    "8ce39b4252824e4bbd28e2cf5dfcd3a5",
                    StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                generator.GenerateSubGraph(ir, materialId),
                Is.EqualTo(generated));

            var graph = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(".GraphData", StringComparison.Ordinal));
            var byId = objects
                .Where(item => item["m_ObjectId"] != null)
                .ToDictionary(
                    item => item["m_ObjectId"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            foreach (var customFunction in customFunctions)
            {
                var customFunctionId =
                    customFunction["m_ObjectId"].Value<string>();
                var normalSlot = (customFunction["m_Slots"] as JArray)
                    .OfType<JObject>()
                    .Select(reference =>
                        byId[reference["m_Id"].Value<string>()])
                    .Single(slot => string.Equals(
                        slot["m_DisplayName"]?.Value<string>(),
                        "NormalWS",
                        StringComparison.Ordinal));
                var normalSlotId = normalSlot["m_Id"].Value<int>();
                var normalEdge = (graph["m_Edges"] as JArray)
                    .OfType<JObject>()
                    .Single(edge =>
                        string.Equals(
                            edge["m_InputSlot"]?["m_Node"]?["m_Id"]
                                ?.Value<string>(),
                            customFunctionId,
                            StringComparison.Ordinal) &&
                        edge["m_InputSlot"]?["m_SlotId"]?.Value<int>() ==
                        normalSlotId);
                var normalSourceId =
                    normalEdge["m_OutputSlot"]?["m_Node"]?["m_Id"]
                        ?.Value<string>() ?? "";
                Assert.That(
                    byId[normalSourceId]["m_Type"]?.Value<string>(),
                    Is.EqualTo("UnityEditor.ShaderGraph.NormalizeNode"),
                    "Each lobe must receive a validated, normalized normal.");
            }

            var contract = generator.WrapperContract(ir);
            var template = File.ReadAllText(
                ToAbsolute(generator.WrapperTemplatePath),
                Encoding.UTF8);
            var wrapper =
                MikuShaderGraph17RuntimeBackend.GenerateWrapper(
                    template,
                    generated,
                    materialId,
                    subGraphGuid,
                    contract);
            wrapper = MikuShaderGraph17RuntimeBackend
                .ApplyWrapperContract(wrapper, contract);
            Assert.That(
                wrapper,
                Does.Contain("UniversalUnlitSubTarget"),
                "Non-coat closure radiance is final lighting and must use " +
                "the unlit wrapper Base Color path.");
            var assetRoot = OutputRoot + "/SurfaceModels";
            Directory.CreateDirectory(ToAbsolute(assetRoot));
            var subGraphPath =
                assetRoot + "/custom.generated.shadersubgraph";
            var wrapperPath = assetRoot + "/custom.shadergraph";
            File.WriteAllText(
                ToAbsolute(subGraphPath),
                generated,
                new UTF8Encoding(false));
            File.WriteAllText(
                ToAbsolute(subGraphPath + ".meta"),
                "fileFormatVersion: 2\n" +
                "guid: " + subGraphGuid + "\n" +
                "NativeFormatImporter:\n" +
                "  externalObjects: {}\n" +
                "  mainObjectFileID: 11400000\n" +
                "  userData:\n" +
                "  assetBundleName:\n" +
                "  assetBundleVariant:\n",
                new UTF8Encoding(false));
            File.WriteAllText(
                ToAbsolute(wrapperPath),
                wrapper,
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(
                subGraphPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(
                wrapperPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(wrapperPath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }

        // Regression: BuildClosureComposite used to skip AddClearCoatOutputs,
        // so any CustomMultiLobe material whose surface plan declared an
        // Urp17ClearCoat approximation produced a subgraph missing the
        // "Coat Mask" / "Coat Smoothness" output. The wrapper assembler then
        // failed with MIKU_WRAPPER_SUBGRAPH_OUTPUT_MISSING:Coat Mask.
        [Test]
        public void ClosureCompositeClearCoatSurfaceExposesCoatMaskOutput()
        {
            var ir = ClearCoatSurfaceModelIr2();
            ir["surfaceModelPlan"]["kind"] = "CustomMultiLobe";
            var generator = MikuSurfaceModelBackends.Resolve(ir);
            Assert.That(
                generator.WrapperTemplatePath,
                Does.EndWith("MikuClearCoatTemplate.shadergraph"));
            const string materialId = "closure-clear-coat-multi-lobe";
            var generated = generator.GenerateSubGraph(ir, materialId);

            Assert.That(
                generated,
                Does.Contain("\"m_DisplayName\": \"Coat Mask\""),
                "BuildClosureComposite must add Coat Mask output when " +
                "surfaceModelPlan.approximations declares Urp17ClearCoat.");
            Assert.That(
                generated,
                Does.Contain("\"m_DisplayName\": \"Coat Smoothness\""));
        }

        [Test]
        public void ClosureCompositeClearCoatAggregatesMultiplePrincipledTerms()
        {
            var ir = ClearCoatSurfaceModelIr2();
            ir["surfaceModelPlan"]["kind"] = "CustomMultiLobe";
            var terms = (JArray)ir["weightedClosures"]["terms"];
            var second = (JObject)terms[0].DeepClone();
            second["id"] = "principled-second";
            second["closureId"] = "closure-principled-second";
            second["finalWeight"] = new JObject
            {
                ["kind"] = "Constant",
                ["valueType"] = "Float",
                ["value"] = 0.5f,
            };
            terms.Add(second);

            var generator = MikuSurfaceModelBackends.Resolve(ir);
            var generated = generator.GenerateSubGraph(
                ir,
                "closure-clear-coat-multi-principled");
            var objects = ParseMultiJson(generated);
            Assert.That(
                objects.Count(item => string.Equals(
                    item["m_Type"]?.Value<string>(),
                    "UnityEditor.ShaderGraph.DivideNode",
                    StringComparison.Ordinal)),
                Is.GreaterThanOrEqualTo(1),
                "Coat smoothness must be normalized by coat contribution.");
            Assert.That(
                objects.Count(item => string.Equals(
                    item["m_Type"]?.Value<string>(),
                    "UnityEditor.ShaderGraph.MinimumNode",
                    StringComparison.Ordinal)),
                Is.GreaterThanOrEqualTo(1),
                "Coat smoothness must be capped below the singular limit.");
        }

        [Test]
        public void RiskyMultiLobeInputsReportFiniteSanitization()
        {
            var ir = SurfaceModelIr2("CustomMultiLobe");
            var first = (JObject)ir["weightedClosures"]["terms"][0];
            first["parameters"]["Roughness"] = new JObject
            {
                ["kind"] = "Constant",
                ["valueType"] = "Float",
                ["value"] = 0f,
            };

            Assert.That(
                Invoke<bool>("RequiresClosureFiniteSanitization", ir),
                Is.True);
            ir["surfaceModelPlan"]["kind"] = "OpaquePBR";
            Assert.That(
                Invoke<bool>("RequiresClosureFiniteSanitization", ir),
                Is.False);
        }

        [TestCase(
            "MikuAlphaBlendTemplate.shadergraph",
            1,
            2,
            false,
            false)]
        [TestCase(
            "MikuDitheredTemplate.shadergraph",
            0,
            1,
            true,
            false)]
        [TestCase(
            "MikuDielectricTemplate.shadergraph",
            1,
            2,
            false,
            true)]
        public void UnityAuthoredWrappersKeepExactUrpTargetContract(
            string fileName,
            int surfaceType,
            int zWrite,
            bool alphaClip,
            bool unlit)
        {
            var path = ToAbsolute(
                "Packages/com.miku.shaderconverter/Templates/" + fileName);
            var objects = ParseMultiJson(File.ReadAllText(path, Encoding.UTF8));
            var target = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(".UniversalTarget", StringComparison.Ordinal));
            Assert.That(target["m_SurfaceType"]?.Value<int>(), Is.EqualTo(surfaceType));
            Assert.That(target["m_ZWriteControl"]?.Value<int>(), Is.EqualTo(zWrite));
            Assert.That(target["m_AlphaClip"]?.Value<bool>(), Is.EqualTo(alphaClip));
            Assert.That(target["m_RenderFace"]?.Value<int>(), Is.Zero);
            if (unlit)
            {
                var subTargetId =
                    target["m_ActiveSubTarget"]?["m_Id"]?.Value<string>();
                var subTarget = objects.Single(item =>
                    item["m_ObjectId"]?.Value<string>() == subTargetId);
                Assert.That(
                    subTarget["m_Type"]?.Value<string>(),
                    Does.EndWith(".UniversalUnlitSubTarget"));
            }
        }

        [Test]
        public void WrapperRenderContractMismatchRequiresExplicitRegeneration()
        {
            var alphaContract =
                (JObject)SurfaceIr("StandardLit", "AlphaBlend")
                    ["surfaceContract"];
            var ditherContract =
                (JObject)SurfaceIr("StandardLit", "Dithered")
                    ["surfaceContract"];
            var wrapper = File.ReadAllText(
                ToAbsolute(
                    "Packages/com.miku.shaderconverter/Templates/" +
                    "MikuAlphaBlendTemplate.shadergraph"),
                Encoding.UTF8);
            var type = typeof(MikuBundleImporter).Assembly.GetType(
                "Miku.ShaderConverter.Editor." +
                "MikuShaderGraph17RuntimeBackend",
                true);
            var method = type.GetMethod(
                "WrapperRenderContractMatches",
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(
                method.Invoke(
                    null,
                    new object[] { wrapper, alphaContract }),
                Is.True);
            Assert.That(
                method.Invoke(
                    null,
                    new object[] { wrapper, ditherContract }),
                Is.False);
        }

        [Test]
        public void CanonicalJsonMatchesPythonFloatSpelling()
        {
            var value = new JObject
            {
                ["integral"] = 1.0,
                ["exponent"] = 0.000001,
                ["negativeZero"] = -0.0,
                ["shortestRoundTrip"] = 0.84999990463256836,
                ["shortestLowerNeighbor"] = 0.84687358140945435,
                ["shortestPreviousPrecision"] = 0.00052638433407992125,
                ["fixedUpperBoundary"] = 1000000000000000.0,
                ["scientificUpperBoundary"] = 10000000000000000.0,
                ["fixedLowerBoundary"] = 0.0001,
            };

            Assert.That(
                Invoke<string>("CanonicalJson", value),
                Is.EqualTo(
                    "{\"exponent\":1e-06," +
                    "\"fixedLowerBoundary\":0.0001," +
                    "\"fixedUpperBoundary\":1000000000000000.0," +
                    "\"integral\":1.0," +
                    "\"negativeZero\":0.0," +
                    "\"scientificUpperBoundary\":1e+16," +
                    "\"shortestLowerNeighbor\":0.8468735814094543," +
                    "\"shortestPreviousPrecision\":0.0005263843340799212," +
                    "\"shortestRoundTrip\":0.8499999046325684}"));
        }

        [Test]
        public void VerifiedBundleCanGenerateGraphsWithoutCreatingReviewMaterial()
        {
            var bundlePath = WriteValidBundle();
            var first = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = bundlePath,
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = false,
            });
            Assert.That(first.success, Is.True, string.Join(" | ", first.diagnostics));
            Assert.That(first.assetPaths.Any(path => path.EndsWith(".shadergraph", StringComparison.Ordinal)), Is.True);
            Assert.That(first.assetPaths.Any(path => path.EndsWith(".generated.shadersubgraph", StringComparison.Ordinal)), Is.True);
            Assert.That(first.assetPaths.Any(path => path.EndsWith(".mat", StringComparison.Ordinal)), Is.False);
            Assert.That(File.Exists(ToAbsolute(first.receiptPath)), Is.True);
            var receipt = JObject.Parse(
                File.ReadAllText(
                    ToAbsolute(first.receiptPath),
                    Encoding.UTF8));
            Assert.That(receipt["diagnostics"], Is.TypeOf<JArray>());

            var wrapper = first.assetPaths.Single(path => path.EndsWith(".shadergraph", StringComparison.Ordinal));
            var before = File.ReadAllBytes(ToAbsolute(wrapper));
            var second = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = bundlePath,
                outputRoot = OutputRoot,
                fullRegeneration = false,
                createMaterialVariant = false,
            });
            Assert.That(second.success, Is.True, string.Join(" | ", second.diagnostics));
            Assert.That(File.ReadAllBytes(ToAbsolute(wrapper)), Is.EqualTo(before));
        }

        [Test]
        public void VerifiedDielectricBundleImportsWithoutPinkShader()
        {
            testPipeline.supportsCameraOpaqueTexture = false;
            var fixture = SurfaceIr(
                "DielectricScreenRefraction",
                "AlphaBlend");
            ((JArray)fixture["channels"]).Add(
                ConstantChannel(
                    "Emission",
                    new JArray(1.5, 0.25, 0.05)));
            var bundlePath = WriteValidBundle(
                sourceId: "dielectric-source",
                materialId:
                    "55555555-5555-4555-8555-555555555555",
                sourceName: "Dielectric Fixture",
                explicitChannels: (JArray)fixture["channels"],
                explicitSurfaceContract:
                    (JObject)fixture["surfaceContract"]);
            var imported = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = bundlePath,
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            });

            Assert.That(
                imported.success,
                Is.True,
                string.Join(" | ", imported.diagnostics));
            Assert.That(
                imported.diagnostics.Any(item =>
                    item.StartsWith(
                        "MIKU_URP_OPAQUE_TEXTURE_REQUIRED:",
                        StringComparison.Ordinal)),
                Is.True);
            var materialPath = imported.assetPaths.Single(
                item => item.EndsWith(
                    ".generated.mat",
                    StringComparison.Ordinal));
            var material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            foreach (var property in new[]
                     {
                         "_IOR",
                         "_TransmissionWeight",
                         "_Opacity",
                         "_RefractionStrength",
                         "_ReflectionStrength",
                         "_Thickness",
                     })
                Assert.That(material.HasProperty(property), Is.True, property);
            Assert.That(material.GetFloat("_IOR"), Is.EqualTo(1.5f));
            Assert.That(
                material.GetFloat("_TransmissionWeight"),
                Is.EqualTo(1.0f));
            Assert.That(material.GetFloat("_Opacity"), Is.EqualTo(1.0f));
            Assert.That(
                material.GetFloat("_RefractionStrength"),
                Is.EqualTo(0.05f));
            Assert.That(
                material.GetFloat("_ReflectionStrength"),
                Is.EqualTo(1.0f));
            Assert.That(material.GetFloat("_Thickness"), Is.EqualTo(0.1f));
            Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.white));
            var wrapper = imported.assetPaths.Single(item =>
                item.EndsWith(".shadergraph", StringComparison.Ordinal));
            var wrapperText =
                File.ReadAllText(ToAbsolute(wrapper), Encoding.UTF8);
            Assert.That(
                wrapperText,
                Does.Contain("UniversalUnlitSubTarget"));
            AssertDirectWrapperEdge(
                wrapperText,
                "Base Color",
                "SurfaceDescription.BaseColor");
            AssertDirectWrapperEdge(
                wrapperText,
                "Alpha",
                "SurfaceDescription.Alpha");

            var variantPath = imported.assetPaths.Single(
                item => item.EndsWith(".mat", StringComparison.Ordinal) &&
                    !item.EndsWith(
                        ".generated.mat",
                        StringComparison.Ordinal));
            var variant =
                AssetDatabase.LoadAssetAtPath<Material>(variantPath);
            material.SetFloat("_TransmissionWeight", 0.0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            Assert.That(
                variant.GetFloat("_TransmissionWeight"),
                Is.EqualTo(0.0f));

            var regenerated = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = bundlePath,
                    outputRoot = OutputRoot,
                    fullRegeneration = true,
                    createMaterialVariant = true,
                });

            Assert.That(
                regenerated.success,
                Is.True,
                string.Join(" | ", regenerated.diagnostics));
            material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            variant =
                AssetDatabase.LoadAssetAtPath<Material>(variantPath);
            Assert.That(
                material.GetFloat("_TransmissionWeight"),
                Is.EqualTo(1.0f));
            Assert.That(
                variant.GetFloat("_TransmissionWeight"),
                Is.EqualTo(1.0f));
        }

        [Test]
        public void VerifiedTextureResourceCreatesBoundMaterialWithStableGuids()
        {
            var bundlePath = WriteValidBundle(true);
            var request = new MikuImportRequest
            {
                bundlePath = bundlePath,
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            };
            var first = MikuBundleImporter.Import(request);
            Assert.That(first.success, Is.True, string.Join(" | ", first.diagnostics));
            var materialPath = first.assetPaths.Single(
                path => path.EndsWith(".generated.mat", StringComparison.Ordinal));
            var variantPath = first.assetPaths.Single(
                path => path.EndsWith(".mat", StringComparison.Ordinal) &&
                    !path.EndsWith(".generated.mat", StringComparison.Ordinal));
            var material = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(materialPath);
            var variant = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(variantPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(variant, Is.Not.Null);
            Assert.That(variant.isVariant, Is.True);
            Assert.That(variant.parent, Is.EqualTo(material));
            Assert.That(material.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"));
            Assert.That(material.shader.isSupported, Is.True);
            Assert.That(
                material.GetTexturePropertyNames().Count(name => material.GetTexture(name) != null),
                Is.EqualTo(1));
            var materialGuid = AssetDatabase.AssetPathToGUID(materialPath);
            var variantGuid = AssetDatabase.AssetPathToGUID(variantPath);
            var variantBytes = File.ReadAllBytes(ToAbsolute(variantPath));
            var texturePath = first.assetPaths.Single(path => path.EndsWith("/BaseColor.png", StringComparison.Ordinal));
            var textureGuid = AssetDatabase.AssetPathToGUID(texturePath);

            request.fullRegeneration = false;
            var second = MikuBundleImporter.Import(request);
            Assert.That(second.success, Is.True, string.Join(" | ", second.diagnostics));
            Assert.That(AssetDatabase.AssetPathToGUID(materialPath), Is.EqualTo(materialGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(variantPath), Is.EqualTo(variantGuid));
            Assert.That(File.ReadAllBytes(ToAbsolute(variantPath)), Is.EqualTo(variantBytes));
            Assert.That(AssetDatabase.AssetPathToGUID(texturePath), Is.EqualTo(textureGuid));
        }

        [Test]
        public void Bundle22JpegHeightImportsLinearTextureAndBindsProperty()
        {
            var ir = SurfaceModelIr2("OpaquePBR");
            ir["expressions"] = new JArray
            {
                Expression(
                    "height",
                    "Texture.SampleImage2D",
                    "Scalar",
                    "Fragment",
                    new JObject
                    {
                        ["resourceId"] = "resource-base-color",
                        ["referenceName"] = "_MIKU_HeightMap",
                        ["semantic"] = "Height",
                        ["usage"] = "Scalar",
                        ["channel"] = "R",
                        ["colorSpace"] = "Linear",
                        ["uvSet"] = "UV0",
                        ["lodMode"] = "Auto",
                    }),
                Expression(
                    "midlevel",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.5f }),
                Expression(
                    "strength",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.1f }),
                Expression(
                    "height-normal",
                    "Vector.NormalFromHeight",
                    "Float3",
                    "Fragment",
                    new JObject
                    {
                        ["bumpStrengthReference"] =
                            "_MIKU_BumpStrength",
                        ["bumpDistanceReference"] =
                            "_MIKU_BumpDistance",
                    },
                    inputs: new JObject
                    {
                        ["Height"] = new JObject
                        {
                            ["expressionId"] = "height",
                        },
                        ["Midlevel"] = new JObject
                        {
                            ["expressionId"] = "midlevel",
                        },
                        ["Strength"] = new JObject
                        {
                            ["expressionId"] = "strength",
                        },
                    }),
            };
            ir["channels"] = new JArray
            {
                new JObject
                {
                    ["semantic"] = "Normal",
                    ["valueType"] = "Float3",
                    ["stage"] = "Fragment",
                    ["required"] = true,
                    ["value"] = new JObject
                    {
                        ["kind"] = "Expression",
                        ["expressionId"] = "height-normal",
                    },
                },
                ConstantChannel("Alpha", 1.0),
            };

            var result = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = WriteValidBundle(
                        includeResource: true,
                        sourceName: "Bundle22JpegHeight",
                        targetProfileHash: CurrentProfileHashV22,
                        explicitMaterialIrV2: ir,
                        resourceSemantic: "Height",
                        resourceUsage: "Scalar",
                        resourceMediaType: "image/jpeg",
                        resourceExtension: ".jpg",
                        resourceColorSpace: "Linear",
                        resourceChannel: "R",
                        bundleKind: "miku-bundle-1.0",
                        toolVersion: "2.2.0"),
                    outputRoot = OutputRoot,
                    fullRegeneration = true,
                    createMaterialVariant = true,
                });

            Assert.That(
                result.success,
                Is.True,
                string.Join(" | ", result.diagnostics));
            var texturePath = result.assetPaths.Single(path =>
                path.EndsWith(
                    "/Textures/Height.jpg",
                    StringComparison.Ordinal));
            var importer =
                AssetImporter.GetAtPath(texturePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.sRGBTexture, Is.False);
            Assert.That(importer.textureType, Is.EqualTo(
                TextureImporterType.Default));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            var materialPath = result.assetPaths.Single(path =>
                path.EndsWith(".generated.mat", StringComparison.Ordinal));
            var material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material.HasProperty("_MIKU_HeightMap"), Is.True);
            Assert.That(
                material.GetTexture("_MIKU_HeightMap"),
                Is.EqualTo(
                    AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)));
            var subGraphPath = result.assetPaths.Single(path =>
                path.EndsWith(
                    ".generated.shadersubgraph",
                    StringComparison.Ordinal));
            Assert.That(
                File.ReadAllText(ToAbsolute(subGraphPath), Encoding.UTF8),
                Does.Contain("SampleTexture2DNode"));
        }

        [Test]
        public void Bundle22DirectXNormalFlipsGreenChannelOnImport()
        {
            var ir = SurfaceModelIr2("OpaquePBR");
            ir["expressions"] = new JArray
            {
                Expression(
                    "normal-map",
                    "Texture.SampleImage2D",
                    "Float3",
                    "Fragment",
                    new JObject
                    {
                        ["resourceId"] = "resource-base-color",
                        ["referenceName"] = "_BumpMap",
                        ["semantic"] = "Normal",
                        ["usage"] = "Normal",
                        ["channel"] = "RGB",
                        ["colorSpace"] = "Linear",
                        ["uvSet"] = "UV0",
                        ["lodMode"] = "Auto",
                        ["normalConvention"] =
                            "TangentDirectXNegativeY",
                    }),
            };
            ir["channels"] = new JArray
            {
                new JObject
                {
                    ["semantic"] = "Normal",
                    ["valueType"] = "Float3",
                    ["stage"] = "Fragment",
                    ["required"] = true,
                    ["value"] = new JObject
                    {
                        ["kind"] = "Expression",
                        ["expressionId"] = "normal-map",
                    },
                },
                ConstantChannel("Alpha", 1.0),
            };

            var result = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = WriteValidBundle(
                        includeResource: true,
                        sourceName: "Bundle22DirectXNormal",
                        targetProfileHash: CurrentProfileHashV22,
                        explicitMaterialIrV2: ir,
                        resourceBindingKey: "Normal",
                        resourceSemantic: "Normal",
                        resourceUsage: "Normal",
                        resourceColorSpace: "Linear",
                        resourceChannel: "RGB",
                        resourceNormalConvention:
                            "TangentDirectXNegativeY",
                        bundleKind: "miku-bundle-1.0",
                        toolVersion: "2.2.0"),
                    outputRoot = OutputRoot,
                    fullRegeneration = true,
                    createMaterialVariant = true,
                });

            Assert.That(
                result.success,
                Is.True,
                string.Join(" | ", result.diagnostics));
            var texturePath = result.assetPaths.Single(path =>
                path.EndsWith(
                    "/Textures/Normal.png",
                    StringComparison.Ordinal));
            var importer =
                AssetImporter.GetAtPath(texturePath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(
                importer.textureType,
                Is.EqualTo(TextureImporterType.NormalMap));
            Assert.That(importer.flipGreenChannel, Is.True);
        }

        [Test]
        public void StandardPbrInspectorExposesCanonicalPbrAuthoringControls()
        {
            var imported = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = WriteValidBundle(),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            });
            Assert.That(imported.success, Is.True, string.Join(" | ", imported.diagnostics));
            var materialPath = imported.assetPaths.Single(
                path => path.EndsWith(".generated.mat", StringComparison.Ordinal));
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var shader = material.shader;
            var visible = Enumerable.Range(0, shader.GetPropertyCount())
                .Where(index =>
                    (shader.GetPropertyFlags(index) &
                     ShaderPropertyFlags.HideInInspector) == 0)
                .Select(shader.GetPropertyName)
                .ToArray();
            Assert.That(
                visible,
                Is.EquivalentTo(new[]
                {
                    "_BaseMap",
                    "_BaseColor",
                    "_MetallicMap",
                    "_Metallic",
                    "_RoughnessMap",
                    "_Roughness",
                    "_BumpMap",
                    "_NormalStrength",
                    "_OcclusionStrength",
                    "_EmissionMap",
                    "_EmissionColor",
                    "_EmissionStrength",
                }));
            foreach (var hidden in new[]
            {
                "_ColorRamp_004Tex",
                "_Group_011_Input_1_Default",
                "_Value",
                "_MIKU_UseBakedParity",
                "_AlphaMap",
            })
            {
                Assert.That(material.HasProperty(hidden), Is.True, hidden);
                var index = shader.FindPropertyIndex(hidden);
                Assert.That(index, Is.GreaterThanOrEqualTo(0), hidden);
                Assert.That(
                    shader.GetPropertyFlags(index) &
                    ShaderPropertyFlags.HideInInspector,
                    Is.Not.EqualTo(0),
                    hidden);
            }
            Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.white));
            Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(1.0f));
            Assert.That(material.GetFloat("_Roughness"), Is.EqualTo(1.0f));
            Assert.That(material.GetFloat("_NormalStrength"), Is.EqualTo(1.0f));
            Assert.That(material.GetColor("_EmissionColor"), Is.EqualTo(Color.white));
            Assert.That(material.GetFloat("_EmissionStrength"), Is.EqualTo(1.0f));
            Assert.That(
                material.renderQueue,
                Is.LessThan((int)RenderQueue.Transparent));
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }

        [Test]
        public void StandardPbrTextureResourcesPopulateAllInspectorMapSlots()
        {
            var imported = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = WriteStandardPbrTextureBundle(),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            });
            Assert.That(imported.success, Is.True, string.Join(" | ", imported.diagnostics));
            var materialPath = imported.assetPaths.Single(
                path => path.EndsWith(".generated.mat", StringComparison.Ordinal));
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material, Is.Not.Null);
            foreach (var property in new[]
            {
                "_BaseMap",
                "_MetallicMap",
                "_RoughnessMap",
                "_BumpMap",
                "_EmissionMap",
            })
            {
                Assert.That(material.GetTexture(property), Is.Not.Null, property);
            }
        }

        [Test]
        public void HiddenStandardPbrPropertiesRemainIndependentPerMaterial()
        {
            var imported = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = WriteValidBundle(),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            });
            Assert.That(imported.success, Is.True, string.Join(" | ", imported.diagnostics));
            var baseMaterialPath = imported.assetPaths.Single(
                path => path.EndsWith(".generated.mat", StringComparison.Ordinal));
            var shader = AssetDatabase.LoadAssetAtPath<Material>(baseMaterialPath).shader;
            var textureA = new Texture2D(2, 4);
            var textureB = new Texture2D(8, 16);
            const string textureAPath = "Assets/MikuTests/HiddenA.asset";
            const string textureBPath = "Assets/MikuTests/HiddenB.asset";
            AssetDatabase.CreateAsset(textureA, textureAPath);
            AssetDatabase.CreateAsset(textureB, textureBPath);
            var materialA = new Material(shader);
            var materialB = new Material(shader);
            const string materialAPath = "Assets/MikuTests/HiddenA.mat";
            const string materialBPath = "Assets/MikuTests/HiddenB.mat";
            materialA.SetTexture("_ColorRamp_004Tex", textureA);
            materialA.SetFloat("_Group_011_Input_1_Default", 0.25f);
            materialB.SetTexture("_ColorRamp_004Tex", textureB);
            materialB.SetFloat("_Group_011_Input_1_Default", 0.75f);
            AssetDatabase.CreateAsset(materialA, materialAPath);
            AssetDatabase.CreateAsset(materialB, materialBPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(materialAPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(materialBPath, ImportAssetOptions.ForceSynchronousImport);

            var reloadedA = AssetDatabase.LoadAssetAtPath<Material>(materialAPath);
            var reloadedB = AssetDatabase.LoadAssetAtPath<Material>(materialBPath);
            Assert.That(reloadedA.GetTexture("_ColorRamp_004Tex"), Is.EqualTo(textureA));
            Assert.That(reloadedB.GetTexture("_ColorRamp_004Tex"), Is.EqualTo(textureB));
            Assert.That(
                reloadedA.GetFloat("_Group_011_Input_1_Default"),
                Is.EqualTo(0.25f));
            Assert.That(
                reloadedB.GetFloat("_Group_011_Input_1_Default"),
                Is.EqualTo(0.75f));
        }

        [Test]
        public void StandardPbrConstantsBindCanonicalRawValues()
        {
            var channels = new JArray
            {
                ConstantChannel("BaseColor", new JArray(0.2, 0.3, 0.4, 1.0)),
                ConstantChannel("Metalness", 0.35),
                ConstantChannel("Roughness", 0.7),
                ConstantChannel("Normal", new JArray(0.0, 0.0, 1.0)),
                ConstantChannel("Emission", new JArray(2.0, 1.0, 0.5, 1.0)),
                ConstantChannel("AmbientOcclusion", 0.8),
                ConstantChannel("Alpha", 0.4),
            };
            var imported = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    explicitChannels: channels),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            });
            Assert.That(imported.success, Is.True, string.Join(" | ", imported.diagnostics));
            Assert.That(
                imported.diagnostics,
                Does.Contain("MIKU_STANDARD_PBR_ALPHA_IGNORED_OPAQUE"));
            var path = imported.assetPaths.Single(
                item => item.EndsWith(".generated.mat", StringComparison.Ordinal));
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(material.GetColor("_BaseColor").r, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(0.35f));
            Assert.That(material.GetFloat("_Roughness"), Is.EqualTo(0.7f));
            Assert.That(material.GetFloat("_NormalStrength"), Is.EqualTo(1.0f));
            Assert.That(material.GetColor("_EmissionColor").r, Is.EqualTo(2.0f));
            Assert.That(material.GetFloat("_EmissionStrength"), Is.EqualTo(1.0f));
            Assert.That(material.GetFloat("_OcclusionStrength"), Is.EqualTo(0.8f));
        }

        [Test]
        public void Package103ClosureZeroNormalsMigrateInMemoryWithDiagnostic()
        {
            var ir = SurfaceModelIr2("CustomMultiLobe");
            var term = ((JArray)ir["weightedClosures"]["terms"])
                .OfType<JObject>()
                .First();
            ((JObject)term["parameters"])["Normal"] = new JObject
            {
                ["kind"] = "Constant",
                ["valueType"] = "Float3",
                ["value"] = new JArray(0f, 0f, 0f),
            };
            ir["closureGraph"] = new JObject
            {
                ["root"] = new JObject
                {
                    ["parameters"] = new JObject
                    {
                        ["Coat Normal"] = new JObject
                        {
                            ["kind"] = "Constant",
                            ["valueType"] = "Float3",
                            ["value"] = new JArray(0f, 0f, 0f),
                        },
                        ["Normal"] = new JObject
                        {
                            ["kind"] = "ValueExpression",
                            ["valueType"] = "Float3",
                            ["expressionId"] = "explicit-zero-expression",
                        },
                    },
                },
            };
            var diagnostics = new List<string>();

            MikuBundleImporter.NormalizeLegacyClosureZeroNormals(
                ir,
                Package103ProfileHash,
                diagnostics);

            Assert.That(
                term["parameters"]?["Normal"]?["value"],
                Is.EqualTo(new JArray(0f, 0f, 1f)));
            Assert.That(
                ir["closureGraph"]?["root"]?["parameters"]?
                    ["Coat Normal"]?["value"],
                Is.EqualTo(new JArray(0f, 0f, 1f)));
            Assert.That(
                ir["closureGraph"]?["root"]?["parameters"]?
                    ["Normal"]?["expressionId"]?.Value<string>(),
                Is.EqualTo("explicit-zero-expression"));
            Assert.That(
                diagnostics,
                Is.EqualTo(new[]
                {
                    "MIKU_LEGACY_CLOSURE_ZERO_NORMAL_NORMALIZED",
                }));
        }

        [Test]
        public void LegacyZeroNormalIsNeutralInGraphAndMaterialBinding()
        {
            var channels = new JArray
            {
                ConstantChannel("Normal", new JArray(0.0, 0.0, 0.0)),
            };
            var ir = new JObject
            {
                ["expressions"] = new JArray(),
                ["parameters"] = new JArray(),
                ["channels"] = channels.DeepClone(),
            };

            var generated = GenerateRuntimeSubGraph(
                ir,
                "legacy-zero-normal-fixture");
            var objects = ParseMultiJson(generated);
            var graph = objects.Single(
                item => (item["m_Type"]?.Value<string>() ?? "")
                    .EndsWith(".GraphData", StringComparison.Ordinal));
            var output = objects.Single(
                item => (item["m_Type"]?.Value<string>() ?? "")
                    .EndsWith(".SubGraphOutputNode", StringComparison.Ordinal));
            var byId = objects
                .Where(item => item["m_ObjectId"] != null)
                .ToDictionary(
                    item => item["m_ObjectId"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            var normalSlot = (output["m_Slots"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(reference => byId[reference["m_Id"].Value<string>()])
                .Single(slot => string.Equals(
                    slot["m_DisplayName"]?.Value<string>(),
                    "Normal TS",
                    StringComparison.Ordinal));
            var edge = (graph["m_Edges"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Single(item =>
                    string.Equals(
                        item["m_InputSlot"]?["m_Node"]?["m_Id"]?.Value<string>(),
                        output["m_ObjectId"]?.Value<string>(),
                        StringComparison.Ordinal) &&
                    item["m_InputSlot"]?["m_SlotId"]?.Value<int>() ==
                        normalSlot["m_Id"]?.Value<int>());
            var normalNode = byId[
                edge["m_OutputSlot"]["m_Node"]["m_Id"].Value<string>()];
            Assert.That(
                normalNode["m_Type"]?.Value<string>(),
                Does.EndWith(".Vector4Node"));
            var componentSlots = (normalNode["m_Slots"] as JArray ??
                                  new JArray())
                .OfType<JObject>()
                .Select(reference => byId[reference["m_Id"].Value<string>()])
                .Where(slot => slot["m_Id"]?.Value<int>() > 0)
                .ToDictionary(
                    slot => slot["m_Id"].Value<int>(),
                    slot => slot["m_Value"].Value<float>());
            Assert.That(componentSlots[1], Is.Zero);
            Assert.That(componentSlots[2], Is.Zero);
            Assert.That(componentSlots[3], Is.EqualTo(1.0f));

            var imported = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = WriteValidBundle(explicitChannels: channels),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            });
            Assert.That(
                imported.success,
                Is.True,
                string.Join(" | ", imported.diagnostics));
            Assert.That(
                imported.diagnostics,
                Does.Contain("MIKU_LEGACY_ZERO_NORMAL_NORMALIZED"));
            var materialPath = imported.assetPaths.Single(
                item => item.EndsWith(".generated.mat", StringComparison.Ordinal));
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material.GetTexture("_BumpMap"), Is.Null);
            Assert.That(material.GetFloat("_NormalStrength"), Is.EqualTo(1.0f));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
        }

        [Test]
        [Category("Miku.Graphics")]
        public void LayerWeightPreviewIsLitAndViewDependent()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Requires a real D3D11 graphics device.");

            var expressions = new JArray
            {
                Expression(
                    "color-a",
                    "Constant",
                    "Color",
                    "Both",
                    new JObject
                    {
                        ["value"] = new JArray(0.95, 0.12, 0.04),
                    }),
                Expression(
                    "color-b",
                    "Constant",
                    "Color",
                    "Both",
                    new JObject
                    {
                        ["value"] = new JArray(0.03, 0.25, 0.95),
                    }),
                Expression(
                    "blend",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.5 }),
                Expression("normal", "Input.Normal", "Float3", "Fragment"),
                Expression(
                    "view",
                    "Input.ViewDirection",
                    "Float3",
                    "Fragment"),
                Expression(
                    "facing",
                    "Math.LayerWeightFacing",
                    "Scalar",
                    "Fragment",
                    null,
                    new JObject
                    {
                        ["Blend"] = new JObject
                        {
                            ["expressionId"] = "blend",
                        },
                        ["Normal"] = new JObject
                        {
                            ["expressionId"] = "normal",
                        },
                        ["ViewDirection"] = new JObject
                        {
                            ["expressionId"] = "view",
                        },
                    }),
                Expression(
                    "coating-color",
                    "Math.Lerp",
                    "Color",
                    "Fragment",
                    null,
                    new JObject
                    {
                        ["A"] = new JObject
                        {
                            ["expressionId"] = "color-a",
                        },
                        ["B"] = new JObject
                        {
                            ["expressionId"] = "color-b",
                        },
                        ["T"] = new JObject
                        {
                            ["expressionId"] = "facing",
                        },
                    }),
            };
            var channels = new JArray
            {
                new JObject
                {
                    ["semantic"] = "BaseColor",
                    ["valueType"] = "Color",
                    ["required"] = true,
                    ["value"] = new JObject
                    {
                        ["kind"] = "Expression",
                        ["expressionId"] = "coating-color",
                    },
                },
                ConstantChannel("Metalness", 0.25),
                ConstantChannel("Roughness", 0.3),
                ConstantChannel("Normal", new JArray(0.0, 0.0, 0.0)),
                ConstantChannel("Emission", new JArray(0.0, 0.0, 0.0)),
                ConstantChannel("AmbientOcclusion", 1.0),
                ConstantChannel("Alpha", 1.0),
            };
            var imported = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    sourceId: "layer-weight-preview-source",
                    materialId: "22222222-2222-4222-8222-222222222222",
                    sourceName: "Layer Weight Preview",
                    explicitChannels: channels,
                    explicitExpressions: expressions),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            });
            Assert.That(
                imported.success,
                Is.True,
                string.Join(" | ", imported.diagnostics));
            var materialPath = imported.assetPaths.Single(
                item => item.EndsWith(".generated.mat", StringComparison.Ordinal));
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);

            var preview = new PreviewRenderUtility();
            Texture2D image = null;
            try
            {
                preview.camera.transform.position = new Vector3(0.0f, 0.0f, -4.0f);
                preview.camera.transform.LookAt(Vector3.zero);
                preview.camera.fieldOfView = 30.0f;
                preview.camera.nearClipPlane = 0.1f;
                preview.camera.farClipPlane = 20.0f;
                preview.ambientColor = new Color(0.25f, 0.25f, 0.25f, 1.0f);
                preview.lights[0].intensity = 1.4f;
                preview.lights[0].transform.rotation =
                    Quaternion.Euler(35.0f, 35.0f, 0.0f);
                preview.lights[1].intensity = 0.8f;
                preview.lights[1].transform.rotation =
                    Quaternion.Euler(340.0f, 210.0f, 0.0f);
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.GetComponent<Renderer>().sharedMaterial = material;
                preview.AddSingleGO(sphere);
                preview.BeginStaticPreview(new Rect(0.0f, 0.0f, 128.0f, 128.0f));
                preview.Render(true, true);
                image = preview.EndStaticPreview();

                var center = MeanColor(image, 64, 64, 18);
                var centerLuminance =
                    0.2126f * center.r +
                    0.7152f * center.g +
                    0.0722f * center.b;
                Assert.That(
                    centerLuminance,
                    Is.GreaterThan(0.1f),
                    "Layer Weight preview center must not be black.");
                Assert.That(
                    MaximumRingColorDistance(image, center, 64, 64, 36, 54),
                    Is.GreaterThan(0.05f),
                    "Layer Weight preview must retain center-to-edge color variation.");
            }
            finally
            {
                if (image != null)
                    UnityEngine.Object.DestroyImmediate(image);
                preview.Cleanup();
            }
        }

        [Test]
        public void RetiredGenericToonImportFailsBeforeWritingAssets()
        {
            var result = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    false,
                    "retired-generic-source",
                    "44444444-4444-4444-8444-444444444444",
                    "RetiredGeneric",
                    "generic_toon"),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = false,
            });
            Assert.That(
                result.success,
                Is.False);
            Assert.That(
                result.diagnostics,
                Has.Some.StartsWith("MIKU_IMPORT_FAILED:MIKU_WORKFLOW_RETIRED:generic_toon"));
            Assert.That(
                result.assetPaths,
                Is.Empty);
        }

        [Test]
        public void LegacyPresentationProfileIsAcceptedButUnknownProfileIsRejected()
        {
            var legacy = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    targetProfileHash: LegacyPresentationProfileHash),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = false,
            });
            Assert.That(legacy.success, Is.True, string.Join(" | ", legacy.diagnostics));
            Assert.That(
                legacy.diagnostics,
                Does.Contain("MIKU_TARGET_PROFILE_LEGACY_PRESENTATION_COMPATIBILITY"));

            var unknown = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    sourceId: "unknown-profile-source",
                    materialId: "55555555-5555-4555-8555-555555555555",
                    sourceName: "UnknownProfile",
                    targetProfileHash: new string('a', 64)),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = false,
            });
            Assert.That(unknown.success, Is.False);
            Assert.That(
                string.Join(" | ", unknown.diagnostics),
                Does.Contain("MIKU_TARGET_PROFILE_MISMATCH"));
        }

        [Test]
        public void Package120StandardSurfaceRemainsImportable()
        {
            var fixture = SurfaceIr("StandardLit", "Dithered");
            var imported = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    sourceId: "package-120-standard-source",
                    materialId: "package-120-standard-material",
                    sourceName: "Package120Standard",
                    targetProfileHash: Package120ProfileHash,
                    explicitChannels: (JArray)fixture["channels"],
                    explicitSurfaceContract:
                        (JObject)fixture["surfaceContract"]),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = false,
            });

            Assert.That(
                imported.success,
                Is.True,
                string.Join(" | ", imported.diagnostics));
            Assert.That(
                imported.diagnostics,
                Does.Contain(
                    "MIKU_TARGET_PROFILE_1_2_0_SURFACE_COMPATIBILITY"));
            var wrapper = imported.assetPaths.Single(item =>
                item.EndsWith(".shadergraph", StringComparison.Ordinal));
            AssertDirectWrapperEdge(
                File.ReadAllText(ToAbsolute(wrapper), Encoding.UTF8),
                "Alpha",
                "SurfaceDescription.Alpha");
        }

        [Test]
        public void Package120DielectricRequiresExporter121Reexport()
        {
            var fixture = SurfaceIr(
                "DielectricScreenRefraction",
                "AlphaBlend");
            var imported = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    sourceId: "package-120-dielectric-source",
                    materialId: "package-120-dielectric-material",
                    sourceName: "Package120Dielectric",
                    targetProfileHash: Package120ProfileHash,
                    explicitChannels: (JArray)fixture["channels"],
                    explicitSurfaceContract:
                        (JObject)fixture["surfaceContract"]),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = false,
            });

            Assert.That(imported.success, Is.False);
            Assert.That(
                string.Join(" | ", imported.diagnostics),
                Does.Contain(
                    "MIKU_DIELECTRIC_REEXPORT_REQUIRED_1_2_1"));
        }

        [TestCase(PreviousProfileHash)]
        [TestCase(Package110ProfileHash)]
        [TestCase(Exporter110ProfileHash)]
        [TestCase(Version100ProfileHash)]
        public void Known110And100ProfilesRemainAccepted(string profileHash)
        {
            var suffix = profileHash.Substring(0, 8);
            var imported = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    sourceId: "compat-source-" + suffix,
                    materialId: "compat-material-" + suffix,
                    sourceName: "Compat" + suffix,
                    targetProfileHash: profileHash),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = false,
            });
            Assert.That(
                imported.success,
                Is.True,
                string.Join(" | ", imported.diagnostics));
            Assert.That(
                imported.diagnostics,
                Does.Contain(
                    "MIKU_TARGET_PROFILE_LEGACY_PRESENTATION_COMPATIBILITY"));
        }

        [Test]
        public void DifferentSourcesWithSameNameUseDistinctIdentityDirectories()
        {
            var firstBundle = WriteValidBundle(
                false,
                "source-a",
                "11111111-1111-4111-8111-111111111111",
                "Rock");
            var first = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = firstBundle,
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = false,
            });
            Assert.That(first.success, Is.True, string.Join(" | ", first.diagnostics));

            var secondBundle = WriteValidBundle(
                false,
                "source-b",
                "22222222-2222-4222-8222-222222222222",
                "Rock");
            var second = MikuBundleImporter.Import(new MikuImportRequest
            {
                bundlePath = secondBundle,
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = false,
            });
            Assert.That(second.success, Is.True, string.Join(" | ", second.diagnostics));

            var firstGraph = first.assetPaths.Single(
                path => path.EndsWith(".shadergraph", StringComparison.Ordinal));
            var secondGraph = second.assetPaths.Single(
                path => path.EndsWith(".shadergraph", StringComparison.Ordinal));
            Assert.That(firstGraph, Does.Contain("/Rock__111111111111/"));
            Assert.That(secondGraph, Does.Contain("/Rock__222222222222/"));
            Assert.That(firstGraph, Is.Not.EqualTo(secondGraph));
        }

        [Test]
        public void RenameReusesUserAssetsAndKeepsPrefabAndSceneReferences()
        {
            const string sourceId = "rename-source";
            const string materialId = "33333333-3333-4333-8333-333333333333";
            var firstBundle = WriteValidBundle(
                false,
                sourceId,
                materialId,
                "Before");
            var request = new MikuImportRequest
            {
                bundlePath = firstBundle,
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            };
            var first = MikuBundleImporter.Import(request);
            Assert.That(first.success, Is.True, string.Join(" | ", first.diagnostics));
            var wrapperPath = first.assetPaths.Single(
                path => path.EndsWith(".shadergraph", StringComparison.Ordinal));
            var variantPath = first.assetPaths.Single(
                path => path.EndsWith(".mat", StringComparison.Ordinal) &&
                        !path.EndsWith(".generated.mat", StringComparison.Ordinal));
            var wrapperGuid = AssetDatabase.AssetPathToGUID(wrapperPath);
            var variantGuid = AssetDatabase.AssetPathToGUID(variantPath);
            var variant = AssetDatabase.LoadAssetAtPath<Material>(variantPath);

            var reference = GameObject.CreatePrimitive(PrimitiveType.Cube);
            reference.GetComponent<Renderer>().sharedMaterial = variant;
            const string prefabPath = "Assets/MikuTests/RenameReference.prefab";
            PrefabUtility.SaveAsPrefabAsset(reference, prefabPath);
            UnityEngine.Object.DestroyImmediate(reference);

            const string scenePath = "Assets/MikuTests/RenameReference.unity";
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var sceneObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sceneObject.GetComponent<Renderer>().sharedMaterial = variant;
            EditorSceneManager.SaveScene(scene, scenePath);

            var renamedBundle = WriteValidBundle(
                false,
                sourceId,
                materialId,
                "After");
            request.bundlePath = renamedBundle;
            request.fullRegeneration = false;
            var second = MikuBundleImporter.Import(request);
            Assert.That(second.success, Is.True, string.Join(" | ", second.diagnostics));
            Assert.That(
                second.assetPaths.Single(
                    path => path.EndsWith(".shadergraph", StringComparison.Ordinal)),
                Is.EqualTo(wrapperPath));
            Assert.That(
                second.assetPaths.Single(
                    path => path.EndsWith(".mat", StringComparison.Ordinal) &&
                            !path.EndsWith(".generated.mat", StringComparison.Ordinal)),
                Is.EqualTo(variantPath));
            Assert.That(AssetDatabase.AssetPathToGUID(wrapperPath), Is.EqualTo(wrapperGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(variantPath), Is.EqualTo(variantGuid));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(
                AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(
                        prefab.GetComponent<Renderer>().sharedMaterial)),
                Is.EqualTo(variantGuid));
            var reopened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            try
            {
                var renderer = reopened.GetRootGameObjects()
                    .Single()
                    .GetComponent<Renderer>();
                Assert.That(
                    AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(renderer.sharedMaterial)),
                    Is.EqualTo(variantGuid));
            }
            finally
            {
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
            }
        }

        [Test]
        public void ExistingLegacyNamedIdentityDirectoryIsReused()
        {
            var legacyRoot = OutputRoot + "/Fixture";
            WriteIdentityDocument(legacyRoot, "source-fixture", "material-fixture");

            var location = Invoke<object>(
                "ResolveMaterialIdentityLocation",
                OutputRoot,
                "Renamed",
                "source-fixture",
                "material-fixture");
            var materialRoot = (string)location.GetType()
                .GetField("materialRoot", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(location);

            Assert.That(materialRoot, Is.EqualTo(legacyRoot));
        }

        [Test]
        public void ConflictingTargetDirectoryListsRequestedAndExistingIds()
        {
            var target = OutputRoot + "/Fixture__materialfixt";
            WriteIdentityDocument(target, "other-source", "other-material");

            var error = Assert.Throws<TargetInvocationException>(() =>
                Invoke<object>(
                    "ResolveMaterialIdentityLocation",
                    OutputRoot,
                    "Fixture",
                    "source-fixture",
                    "material-fixture"));
            Assert.That(error.InnerException, Is.TypeOf<InvalidDataException>());
            Assert.That(error.InnerException.Message, Does.Contain("MIKU_OUTPUT_IDENTITY_CONFLICT"));
            Assert.That(error.InnerException.Message, Does.Contain("requestedSourceId=source-fixture"));
            Assert.That(error.InnerException.Message, Does.Contain("existingSourceId=other-source"));
        }

        [Test]
        public void DuplicateIdentityDirectoriesAreRejected()
        {
            WriteIdentityDocument(
                OutputRoot + "/First",
                "source-fixture",
                "material-fixture");
            WriteIdentityDocument(
                OutputRoot + "/Second",
                "source-fixture",
                "material-fixture");

            var error = Assert.Throws<TargetInvocationException>(() =>
                Invoke<object>(
                    "ResolveMaterialIdentityLocation",
                    OutputRoot,
                    "Fixture",
                    "source-fixture",
                    "material-fixture"));
            Assert.That(error.InnerException, Is.TypeOf<InvalidDataException>());
            Assert.That(
                error.InnerException.Message,
                Does.Contain("MIKU_OUTPUT_IDENTITY_DUPLICATE"));
            Assert.That(error.InnerException.Message, Does.Contain("/First"));
            Assert.That(error.InnerException.Message, Does.Contain("/Second"));
        }

        [Test]
        public void IdentityOutsideRequestedOutputRootIsReused()
        {
            var originalRoot =
                OutputRoot + "/Original/Fixture__materialfixt";
            WriteIdentityDocument(
                originalRoot,
                "source-fixture",
                "material-fixture");

            var location = Invoke<object>(
                "ResolveMaterialIdentityLocation",
                OutputRoot + "/Requested",
                "Fixture",
                "source-fixture",
                "material-fixture");
            var type = location.GetType();

            Assert.That(
                type.GetField(
                        "materialRoot",
                        BindingFlags.Instance | BindingFlags.Public)
                    .GetValue(location),
                Is.EqualTo(originalRoot));
            Assert.That(
                type.GetField(
                        "reusedOutsideOutputRoot",
                        BindingFlags.Instance | BindingFlags.Public)
                    .GetValue(location),
                Is.EqualTo(true));
        }

        [Test]
        public void StableGuidCollisionReportsRoleAndBothPaths()
        {
            var existingPath =
                OutputRoot + "/ExistingGuidOwner.txt";
            Directory.CreateDirectory(
                Path.GetDirectoryName(ToAbsolute(existingPath)));
            File.WriteAllText(
                ToAbsolute(existingPath),
                "owner",
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(
                existingPath,
                ImportAssetOptions.ForceSynchronousImport);
            var guid = AssetDatabase.AssetPathToGUID(existingPath);
            var requestedPath =
                OutputRoot + "/Requested/Fixture.shadergraph";

            var error = Assert.Throws<TargetInvocationException>(() =>
                InvokeVoid(
                    "ValidateStableGuidOwnership",
                    "WrapperGraph",
                    requestedPath,
                    guid,
                    null));

            Assert.That(
                error.InnerException,
                Is.TypeOf<InvalidDataException>());
            Assert.That(
                error.InnerException.Message,
                Does.Contain(
                    "MIKU_ASSET_GUID_COLLISION:role=WrapperGraph"));
            Assert.That(
                error.InnerException.Message,
                Does.Contain("existingPath=" + existingPath));
            Assert.That(
                error.InnerException.Message,
                Does.Contain("requestedPath=" + requestedPath));
            Assert.That(
                File.Exists(ToAbsolute(requestedPath)),
                Is.False);
        }

        [Test]
        public void SecondOutputRootReusesGeneratedAssetsAndGuids()
        {
            var bundlePath = WriteValidBundle(
                sourceId: "cross-root-source",
                materialId: "cross-root-material",
                sourceName: "CrossRoot");
            var first = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = bundlePath,
                    outputRoot = OutputRoot + "/FirstRoot",
                    fullRegeneration = true,
                    createMaterialVariant = true,
                });
            Assert.That(
                first.success,
                Is.True,
                string.Join(" | ", first.diagnostics));
            var firstAssets = first.assetPaths
                .Where(path =>
                    path.EndsWith(
                        ".shadergraph",
                        StringComparison.Ordinal) ||
                    path.EndsWith(
                        ".shadersubgraph",
                        StringComparison.Ordinal) ||
                    path.EndsWith(
                        ".mat",
                        StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var firstGuids = firstAssets.ToDictionary(
                path => path,
                AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);

            var second = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = bundlePath,
                    outputRoot = OutputRoot + "/SecondRoot",
                    fullRegeneration = false,
                    createMaterialVariant = true,
                });

            Assert.That(
                second.success,
                Is.True,
                string.Join(" | ", second.diagnostics));
            Assert.That(
                second.diagnostics.Any(item => item.StartsWith(
                    "MIKU_OUTPUT_IDENTITY_REUSED_OUTSIDE_OUTPUT_ROOT:",
                    StringComparison.Ordinal)),
                Is.True);
            var secondAssets = second.assetPaths
                .Where(path =>
                    path.EndsWith(
                        ".shadergraph",
                        StringComparison.Ordinal) ||
                    path.EndsWith(
                        ".shadersubgraph",
                        StringComparison.Ordinal) ||
                    path.EndsWith(
                        ".mat",
                        StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(secondAssets, Is.EqualTo(firstAssets));
            foreach (var path in secondAssets)
            {
                Assert.That(
                    AssetDatabase.AssetPathToGUID(path),
                    Is.EqualTo(firstGuids[path]));
            }
            Assert.That(
                Directory.Exists(
                    ToAbsolute(OutputRoot + "/SecondRoot")),
                Is.False);
        }

        [Test]
        public void IncompleteTransactionIsRestoredFromExternalBackup()
        {
            const string materialRoot = "Assets/MikuTests/CrashRecovery/Material";
            var absoluteRoot = ToAbsolute(materialRoot);
            Directory.CreateDirectory(absoluteRoot);
            var owned = Path.Combine(absoluteRoot, "owned.txt");
            File.WriteAllText(owned, "before", new UTF8Encoding(false));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var project = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            var transaction = Path.Combine(
                project,
                "Library",
                "Miku",
                "Transactions",
                "test-" + Guid.NewGuid().ToString("N"));
            var journal = Path.Combine(transaction, "transaction.json");
            var backup = Path.Combine(transaction, "backup");
            InvokeVoid(
                "BeginTransaction",
                journal,
                materialRoot,
                absoluteRoot,
                backup,
                new string('a', 64));
            File.WriteAllText(owned, "corrupted", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(absoluteRoot, "partial.txt"), "partial", new UTF8Encoding(false));

            InvokeVoid("RecoverIncompleteTransactionsNow");

            Assert.That(File.ReadAllText(owned, Encoding.UTF8), Is.EqualTo("before"));
            Assert.That(File.Exists(Path.Combine(absoluteRoot, "partial.txt")), Is.False);
            var state = JObject.Parse(File.ReadAllText(journal, Encoding.UTF8));
            Assert.That(state["status"]?.Value<string>(), Is.EqualTo("rolled-back"));
        }

        [Test]
        public void RuntimeCameraAndTimeUseStructuredNativeShaderGraphNodes()
        {
            var expressions = new JArray
            {
                Expression(
                    "camera-distance",
                    "Input.Camera.ViewDistance",
                    "Scalar",
                    "Fragment"),
                Expression(
                    "time-seconds",
                    "Input.Time.Seconds",
                    "Scalar",
                    "Both",
                    new JObject
                    {
                        ["contract"] = "miku_time_v1",
                        ["sourceFps"] = 24f,
                        ["frameStart"] = 1,
                    }),
                Expression(
                    "camera-time-add",
                    "Math.Add",
                    "Scalar",
                    "Fragment",
                    null,
                    new JObject
                    {
                        ["A"] = new JObject { ["expressionId"] = "camera-distance" },
                        ["B"] = new JObject { ["expressionId"] = "time-seconds" },
                    }),
            };
            var ir = new JObject
            {
                ["expressions"] = expressions,
                ["parameters"] = new JArray(),
                ["channels"] = new JArray
                {
                    ExpressionChannel("Roughness", "camera-time-add"),
                },
            };

            var generated = GenerateRuntimeSubGraph(ir, "runtime-fixture");

            Assert.That(generated, Does.Contain("UnityEditor.ShaderGraph.PositionNode"));
            Assert.That(generated, Does.Contain("UnityEditor.ShaderGraph.LengthNode"));
            Assert.That(generated, Does.Contain("UnityEditor.ShaderGraph.TimeNode"));
            Assert.That(generated, Does.Contain("_MIKU_EffectTimeScale"));
            Assert.That(generated, Does.Contain("_MIKU_EffectTimeOffset"));
            Assert.That(generated, Does.Contain("_MIKU_EffectTimeOverride"));
            Assert.That(generated, Does.Contain("_MIKU_EffectUseTimeOverride"));
            Assert.That(generated, Does.Not.Contain("CustomFunctionNode"));
            Assert.That(
                GenerateRuntimeSubGraph(ir, "runtime-fixture"),
                Is.EqualTo(generated));
        }

        [Test]
        public void OverlayBumpAndDitherUseStructuredFragmentNodes()
        {
            var expressions = new JArray
            {
                Expression(
                    "base",
                    "Constant",
                    "Color",
                    "Both",
                    new JObject { ["value"] = new JArray(0.25, 0.75, 0.4) }),
                Expression(
                    "blend",
                    "Constant",
                    "Color",
                    "Both",
                    new JObject { ["value"] = new JArray(0.8, 0.2, 0.6) }),
                Expression(
                    "factor",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.5 }),
                Expression(
                    "overlay",
                    "Color.Overlay",
                    "Color",
                    "Fragment",
                    inputs: new JObject
                    {
                        ["A"] = new JObject { ["expressionId"] = "base" },
                        ["B"] = new JObject { ["expressionId"] = "blend" },
                        ["T"] = new JObject { ["expressionId"] = "factor" },
                    }),
                Expression(
                    "height",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.75 }),
                Expression(
                    "midlevel",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.5 }),
                Expression(
                    "strength",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 1.0 }),
                Expression(
                    "normal-from-height",
                    "Vector.NormalFromHeight",
                    "Float3",
                    "Fragment",
                    inputs: new JObject
                    {
                        ["Height"] = new JObject { ["expressionId"] = "height" },
                        ["Midlevel"] = new JObject { ["expressionId"] = "midlevel" },
                        ["Strength"] = new JObject { ["expressionId"] = "strength" },
                    }),
            };
            var ir = SurfaceIr("StandardLit", "Dithered");
            ir["expressions"] = expressions;
            ir["channels"] = new JArray
            {
                ExpressionColorChannel("BaseColor", "overlay"),
                ConstantChannel("Metalness", 0.0),
                ConstantChannel("Roughness", 0.5),
                ExpressionColorChannel("Normal", "normal-from-height"),
                ConstantChannel("Emission", new JArray(0.0, 0.0, 0.0)),
                ConstantChannel("AmbientOcclusion", 1.0),
                ConstantChannel("Alpha", 0.65),
            };

            var generated = GenerateRuntimeSubGraph(ir, "overlay-bump-dither");

            Assert.That(generated, Does.Contain("UnityEditor.ShaderGraph.StepNode"));
            Assert.That(generated, Does.Contain("UnityEditor.ShaderGraph.LerpNode"));
            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.NormalFromHeightNode"));
            Assert.That(generated, Does.Contain("UnityEditor.ShaderGraph.DitherNode"));
            Assert.That(
                GenerateRuntimeSubGraph(ir, "overlay-bump-dither"),
                Is.EqualTo(generated));
        }

        [Test]
        public void ObjectMappingNoiseAndNormalUsePortableRuntimeNodes()
        {
            JObject Ref(string id) => new JObject
            {
                ["expressionId"] = id,
            };
            var objectPosition = Expression(
                "object-position",
                "Input.TextureCoordinate.Object",
                "Float3",
                "Fragment");
            objectPosition["space"] = "Object";
            var expressions = new JArray
            {
                objectPosition,
                Expression(
                    "location",
                    "Constant",
                    "Float3",
                    "Both",
                    new JObject
                    {
                        ["value"] = new JArray(0.1, 0.2, 0.3),
                    }),
                Expression(
                    "rotation",
                    "Constant",
                    "Float3",
                    "Both",
                    new JObject
                    {
                        ["value"] = new JArray(0.0, 0.0, 0.5),
                    }),
                Expression(
                    "scale-vector",
                    "Constant",
                    "Float3",
                    "Both",
                    new JObject
                    {
                        ["value"] = new JArray(2.0, 2.0, 2.0),
                    }),
                Expression(
                    "mapping",
                    "Vector.Mapping",
                    "Float3",
                    "Fragment",
                    new JObject
                    {
                        ["vectorType"] = "POINT",
                        ["transformOrder"] = "ScaleRotateTranslate",
                    },
                    new JObject
                    {
                        ["Vector"] = Ref("object-position"),
                        ["Location"] = Ref("location"),
                        ["Rotation"] = Ref("rotation"),
                        ["Scale"] = Ref("scale-vector"),
                    }),
                Expression(
                    "noise-scale",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 5.0 }),
                Expression(
                    "detail",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 2.0 }),
                Expression(
                    "roughness",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.5 }),
                Expression(
                    "lacunarity",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 2.0 }),
                Expression(
                    "distortion",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.1 }),
                Expression(
                    "noise",
                    "Texture.Noise.Factor",
                    "Scalar",
                    "Fragment",
                    new JObject
                    {
                        ["dimensions"] = "3D",
                        ["translationQuality"] = "Approximate",
                    },
                    new JObject
                    {
                        ["Vector"] = Ref("mapping"),
                        ["Scale"] = Ref("noise-scale"),
                        ["Detail"] = Ref("detail"),
                        ["Roughness"] = Ref("roughness"),
                        ["Lacunarity"] = Ref("lacunarity"),
                        ["Distortion"] = Ref("distortion"),
                    }),
                Expression(
                    "midlevel",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.0 }),
                Expression(
                    "strength",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.3 }),
                Expression(
                    "normal",
                    "Vector.NormalFromHeight",
                    "Float3",
                    "Fragment",
                    inputs: new JObject
                    {
                        ["Height"] = Ref("noise"),
                        ["Midlevel"] = Ref("midlevel"),
                        ["Strength"] = Ref("strength"),
                    }),
            };
            var ir = SurfaceIr("StandardLit", "Opaque");
            ir["expressions"] = expressions;
            ir["channels"] = new JArray
            {
                ConstantChannel(
                    "BaseColor",
                    new JArray(0.8, 0.2, 0.1)),
                ConstantChannel("Metalness", 0.0),
                ConstantChannel("Roughness", 0.5),
                ExpressionColorChannel("Normal", "normal"),
                ConstantChannel("Emission", new JArray(0.0, 0.0, 0.0)),
                ConstantChannel("AmbientOcclusion", 1.0),
                ConstantChannel("Alpha", 1.0),
            };

            var generated = GenerateRuntimeSubGraph(
                ir,
                "portable-object-noise");

            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.PositionNode"));
            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.CustomFunctionNode"));
            Assert.That(generated, Does.Contain("Miku_MappingPoint"));
            Assert.That(
                generated,
                Does.Contain("Miku_NoiseTexture3D_Factor"));
            Assert.That(
                generated,
                Does.Contain("9575c9a31f694f23952aa6e758fbb75e"));
            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.NormalFromHeightNode"));
            Assert.That(generated, Does.Not.Contain("_MIKU_Baked_"));
            Assert.That(
                GenerateRuntimeSubGraph(ir, "portable-object-noise"),
                Is.EqualTo(generated));
        }

        [Test]
        public void DielectricUsesSceneRefractionProbeAndIndependentCoverage()
        {
            var ir = SurfaceIr(
                "DielectricScreenRefraction",
                "AlphaBlend");
            ((JArray)ir["channels"]).Add(
                ConstantChannel(
                    "Emission",
                    new JArray(2.0, 0.5, 0.1)));
            var generated = GenerateRuntimeSubGraph(ir, "dielectric-fixture");

            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.SceneColorNode"));
            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.ReflectionProbeNode"));
            Assert.That(generated, Does.Contain("_IOR"));
            Assert.That(generated, Does.Contain("_TransmissionWeight"));
            Assert.That(generated, Does.Contain("_Opacity"));
            Assert.That(generated, Does.Contain("_RefractionStrength"));
            Assert.That(generated, Does.Contain("_ReflectionStrength"));
            Assert.That(generated, Does.Contain("_Thickness"));
            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.OneMinusNode"));
        }

        [TestCase("LINEAR")]
        [TestCase("B_SPLINE")]
        public void MultiStopColorRampReusesStableElementNodes(
            string interpolation)
        {
            var expressions = new JArray
            {
                Expression(
                    "ramp-factor",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.37f }),
                Expression(
                    "three-stop-ramp",
                    "Color.Ramp",
                    "Color",
                    "Fragment",
                    new JObject
                    {
                        ["interpolation"] = interpolation,
                        ["colorMode"] = "RGB",
                        ["hueInterpolation"] = "NEAR",
                        ["output"] = "Color",
                        ["elements"] = new JArray
                        {
                            new JObject
                            {
                                ["position"] = 0.09f,
                                ["color"] =
                                    new JArray(0.07f, 0.06f, 0.64f, 1f),
                            },
                            new JObject
                            {
                                ["position"] = 0.29f,
                                ["color"] =
                                    new JArray(0.09f, 0.55f, 0.82f, 1f),
                            },
                            new JObject
                            {
                                ["position"] = 0.51f,
                                ["color"] =
                                    new JArray(0.18f, 0.86f, 1f, 1f),
                            },
                        },
                    },
                    new JObject
                    {
                        ["Factor"] = new JObject
                        {
                            ["expressionId"] = "ramp-factor",
                        },
                    }),
            };
            var ir = SurfaceIr("StandardLit", "Dithered");
            ir["expressions"] = expressions;
            ((JArray)ir["channels"]).Add(
                ExpressionColorChannel(
                    "BaseColor",
                    "three-stop-ramp"));

            var generated = GenerateRuntimeSubGraph(
                ir,
                "three-stop-ramp-" + interpolation);
            var objectIds = ParseMultiJson(generated)
                .Select(item => item["m_ObjectId"]?.Value<string>())
                .Where(item => !string.IsNullOrEmpty(item))
                .ToArray();

            Assert.That(
                objectIds.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(objectIds.Length));
            Assert.That(
                GenerateRuntimeSubGraph(
                    ir,
                    "three-stop-ramp-" + interpolation),
                Is.EqualTo(generated));
        }

        [Test]
        public void AdapterReportsDuplicateStableNodeRole()
        {
            var backend = typeof(MikuBundleImporter).Assembly.GetType(
                "Miku.ShaderConverter.Editor." +
                "MikuShaderGraph17RuntimeBackend",
                true);
            var adapterType = backend.GetNestedType(
                "ShaderGraph17_4Adapter",
                BindingFlags.NonPublic);
            var adapter = Activator.CreateInstance(adapterType, true);
            var createSubGraph = adapterType.GetMethod(
                "CreateSubGraph",
                BindingFlags.Instance | BindingFlags.Public);
            var createNode = adapterType.GetMethod(
                "CreateNode",
                BindingFlags.Instance | BindingFlags.Public);
            var graph = createSubGraph.Invoke(
                adapter,
                new object[] { "duplicate-role-fixture" });
            var args = new object[]
            {
                graph,
                "duplicate-role-fixture",
                "same-role",
                "Vector1Node",
                Vector2.zero,
            };
            createNode.Invoke(adapter, args);

            var error = Assert.Throws<TargetInvocationException>(
                () => createNode.Invoke(adapter, args));
            var root = error.InnerException;
            while (root is TargetInvocationException invocation &&
                   invocation.InnerException != null)
                root = invocation.InnerException;
            Assert.That(
                root.Message,
                Does.StartWith(
                    "MIKU_SHADERGRAPH_DUPLICATE_NODE_ID:same-role:"));
        }

        [Test]
        public void HueAndBakedExpressionTextureUseNativeShaderGraphNodes()
        {
            var expressions = new JArray
            {
                Expression(
                    "texture",
                    "Texture.SampleBaked2D",
                    "Color",
                    "Fragment",
                    new JObject
                    {
                        ["resourceId"] = "resource-island",
                        ["referenceName"] = "_MIKU_Baked_0123456789abcdef0123",
                        ["usage"] = "Color",
                        ["channel"] = "RGB",
                        ["colorSpace"] = "Linear",
                        ["uvSet"] = "UV0",
                    }),
                Expression(
                    "hue",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.75f }),
                Expression(
                    "saturation",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 1.2f }),
                Expression(
                    "value",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.8f }),
                Expression(
                    "factor",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 1.0f }),
                Expression(
                    "hsv",
                    "Color.HueSaturationValue",
                    "Color",
                    "Fragment",
                    inputs: new JObject
                    {
                        ["Color"] = new JObject { ["expressionId"] = "texture" },
                        ["Hue"] = new JObject { ["expressionId"] = "hue" },
                        ["Saturation"] = new JObject { ["expressionId"] = "saturation" },
                        ["Value"] = new JObject { ["expressionId"] = "value" },
                        ["Factor"] = new JObject { ["expressionId"] = "factor" },
                    }),
            };
            var ir = new JObject
            {
                ["expressions"] = expressions,
                ["parameters"] = new JArray(),
                ["channels"] = new JArray(
                    ExpressionChannel("BaseColor", "hsv")),
            };

            var generated = GenerateRuntimeSubGraph(
                ir,
                "hue-texture-fixture");

            Assert.That(
                generated,
                Does.Contain(
                    "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty"));
            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.SampleTexture2DNode"));
            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.UVNode"));
            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.ColorspaceConversionNode"));
            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.FractionNode"));
            Assert.That(generated, Does.Not.Contain("CustomFunctionNode"));
            var wrapper = GenerateRuntimeWrapper(
                generated,
                "hue-texture-fixture",
                "4c8adeb0338dff2498fc4a5852e3d131");
            Assert.That(
                wrapper,
                Does.Contain("_MIKU_Baked_0123456789abcdef0123"));
            Assert.That(
                wrapper,
                Does.Contain(
                    "UnityEditor.ShaderGraph.Texture2DMaterialSlot"));
        }

        [TestCase("TransparentEmission")]
        [TestCase("TransparentLit")]
        [TestCase("CustomMultiLobe")]
        public void ClosureCompositeUsesPerLobeWorldNormalsAndGeometryInputs(
            string surfaceKind)
        {
            const string bindingKey =
                "_MIKU_Baked_02e3bea4d308f49c5dc8";
            var ir = ClosureNormalIr2(
                surfaceKind,
                bindingKey);

            var generated = GenerateRuntimeSubGraph(
                ir,
                "closure-normal-" + surfaceKind);
            var objects = ParseMultiJson(generated);
            var graph = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(".GraphData", StringComparison.Ordinal));
            var byId = objects
                .Where(item => item["m_ObjectId"] != null)
                .ToDictionary(
                    item => item["m_ObjectId"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            var customFunctions = objects.Where(item => string.Equals(
                item["m_Type"]?.Value<string>(),
                "UnityEditor.ShaderGraph.CustomFunctionNode",
                StringComparison.Ordinal)).ToArray();
            var normalSources = customFunctions.Select(function =>
            {
                var edge = (graph["m_Edges"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .Single(item => string.Equals(
                            item["m_InputSlot"]?["m_Node"]?["m_Id"]?
                                .Value<string>(),
                            function["m_ObjectId"]?.Value<string>(),
                            StringComparison.Ordinal) &&
                        item["m_InputSlot"]?["m_SlotId"]?.Value<int>() == 1);
                return byId[edge["m_OutputSlot"]?["m_Node"]?["m_Id"]?
                    .Value<string>() ?? ""];
            }).ToArray();

            Assert.That(generated, Does.Contain(bindingKey));
            Assert.That(
                generated,
                Does.Contain(
                    "UnityEditor.ShaderGraph.NormalFromHeightNode"));
            if (string.Equals(
                    surfaceKind,
                    "TransparentEmission",
                    StringComparison.Ordinal))
            {
                Assert.That(customFunctions, Is.Empty);
            }
            else
            {
                Assert.That(customFunctions, Has.Length.EqualTo(2));
                Assert.That(
                    normalSources.Select(item =>
                        item["m_Type"]?.Value<string>()),
                    Is.All.EqualTo(
                        "UnityEditor.ShaderGraph.NormalizeNode"));
                Assert.That(
                    normalSources.Select(item =>
                        item["m_ObjectId"]?.Value<string>()).Distinct().Count(),
                    Is.EqualTo(2));
            }
            Assert.That(objects.Any(item => string.Equals(
                item["m_Type"]?.Value<string>(),
                "UnityEditor.ShaderGraph.NormalVectorNode",
                StringComparison.Ordinal)), Is.True);
        }

        [TestCase("TransparentEmission")]
        [TestCase("TransparentLit")]
        [TestCase("CustomMultiLobe")]
        public void UnlitClosureCompositeRoutesEvaluatedRadianceThroughBaseColor(
            string surfaceKind)
        {
            var generated = GenerateRuntimeSubGraph(
                SurfaceModelIr2(surfaceKind),
                "closure-radiance-" + surfaceKind);
            var objects = ParseMultiJson(generated);
            var graph = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(".GraphData", StringComparison.Ordinal));
            var output = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(".SubGraphOutputNode", StringComparison.Ordinal));
            var byId = objects
                .Where(item => item["m_ObjectId"] != null)
                .ToDictionary(
                    item => item["m_ObjectId"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            var outputSlots = (output["m_Slots"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(reference => byId[
                    reference["m_Id"]?.Value<string>() ?? ""])
                .ToDictionary(
                    slot => slot["m_DisplayName"]?.Value<string>() ?? "",
                    slot => slot["m_Id"]?.Value<int>() ?? -1,
                    StringComparer.Ordinal);
            JObject SourceFor(string displayName)
            {
                var edge = (graph["m_Edges"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .Single(item =>
                        string.Equals(
                            item["m_InputSlot"]?["m_Node"]?["m_Id"]?
                                .Value<string>(),
                            output["m_ObjectId"]?.Value<string>(),
                            StringComparison.Ordinal) &&
                        item["m_InputSlot"]?["m_SlotId"]?.Value<int>() ==
                            outputSlots[displayName]);
                return byId[
                    edge["m_OutputSlot"]?["m_Node"]?["m_Id"]?
                        .Value<string>() ?? ""];
            }

            Assert.That(
                SourceFor("Base Color")["m_Type"]?.Value<string>(),
                Does.EndWith(".AddNode"));
            var emissionSource = SourceFor("Emission");
            Assert.That(
                emissionSource["m_Type"]?.Value<string>(),
                Does.EndWith(".Vector3Node"));
            Assert.That(
                (emissionSource["m_Slots"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .Select(reference => byId[
                        reference["m_Id"]?.Value<string>() ?? ""])
                    .Where(slot => slot["m_Id"]?.Value<int>() > 0)
                    .Select(slot => slot["m_Value"]?.Value<float>() ?? 0f),
                Has.All.EqualTo(0f));
        }

        [Test]
        public void LitClearCoatClosureRoutesEvaluatedRadianceThroughEmission()
        {
            var ir = ClearCoatSurfaceModelIr2();
            ir["surfaceModelPlan"]["kind"] = "CustomMultiLobe";
            var generated = GenerateRuntimeSubGraph(
                ir,
                "closure-radiance-clear-coat");
            var objects = ParseMultiJson(generated);
            var graph = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(".GraphData", StringComparison.Ordinal));
            var output = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(".SubGraphOutputNode", StringComparison.Ordinal));
            var byId = objects
                .Where(item => item["m_ObjectId"] != null)
                .ToDictionary(
                    item => item["m_ObjectId"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            var outputSlots = (output["m_Slots"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(reference => byId[
                    reference["m_Id"]?.Value<string>() ?? ""])
                .ToDictionary(
                    slot => slot["m_DisplayName"]?.Value<string>() ?? "",
                    slot => slot["m_Id"]?.Value<int>() ?? -1,
                    StringComparer.Ordinal);
            JObject SourceFor(string displayName)
            {
                var edge = (graph["m_Edges"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .Single(item =>
                        string.Equals(
                            item["m_InputSlot"]?["m_Node"]?["m_Id"]?
                                .Value<string>(),
                            output["m_ObjectId"]?.Value<string>(),
                            StringComparison.Ordinal) &&
                        item["m_InputSlot"]?["m_SlotId"]?.Value<int>() ==
                            outputSlots[displayName]);
                return byId[
                    edge["m_OutputSlot"]?["m_Node"]?["m_Id"]?
                        .Value<string>() ?? ""];
            }

            Assert.That(
                SourceFor("Base Color")["m_Type"]?.Value<string>(),
                Does.EndWith(".Vector3Node"));
            Assert.That(
                SourceFor("Emission")["m_Type"]?.Value<string>(),
                Does.EndWith(".AddNode"));
        }

        [Test]
        public void BakedClosureWeightCreatesReachableShaderProperty()
        {
            const string bindingKey =
                "_MIKU_Baked_8fc6524ac7651f9b08aa";
            var generated = GenerateRuntimeSubGraph(
                BakedClosureWeightIr2(bindingKey),
                "baked-closure-weight");

            Assert.That(generated, Does.Contain(bindingKey));
            Assert.That(
                generated,
                Does.Contain(
                    "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty"));
            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.SampleTexture2DNode"));
        }

        [Test]
        public void BakedClosureWeightBundleImportsTextureBinding()
        {
            const string bindingKey =
                "_MIKU_Baked_8fc6524ac7651f9b08aa";
            var result = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = WriteValidBundle(
                        includeResource: true,
                        sourceName: "BakedClosureWeightBundle",
                        targetProfileHash: CurrentProfileHashV2,
                        explicitMaterialIrV2:
                            BakedClosureWeightIr2(bindingKey),
                        resourceBindingKey: bindingKey,
                        resourceSemantic: "BakedClosureWeight",
                        resourceUsage: "Scalar"),
                    outputRoot = OutputRoot,
                    fullRegeneration = true,
                    createMaterialVariant = true,
                });

            Assert.That(
                result.success,
                Is.True,
                string.Join(" | ", result.diagnostics));
            var subGraphPath = result.assetPaths.Single(path =>
                path.EndsWith(
                    ".generated.shadersubgraph",
                    StringComparison.Ordinal));
            Assert.That(
                File.ReadAllText(ToAbsolute(subGraphPath), Encoding.UTF8),
                Does.Contain(bindingKey));
            var materialPath = result.assetPaths.Single(path =>
                path.EndsWith(".generated.mat", StringComparison.Ordinal));
            var material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.HasProperty(bindingKey), Is.True);
            Assert.That(material.GetTexture(bindingKey), Is.Not.Null);
        }

        [Test]
        public void UnreferencedBakedResourceFailsGeneration()
        {
            const string bindingKey =
                "_MIKU_Baked_deadbeefdeadbeefdead";
            var ir = SurfaceModelIr2("TransparentEmission");
            ir["resources"] = new JArray
            {
                new JObject
                {
                    ["id"] = "resource-unreferenced",
                    ["bindingKey"] = bindingKey,
                },
            };

            var error = Assert.Throws<TargetInvocationException>(
                () => GenerateRuntimeSubGraph(
                    ir,
                    "unreferenced-baked-resource"));

            Assert.That(
                error.InnerException?.Message,
                Is.EqualTo(
                    "MIKU_GENERATED_RESOURCE_UNREFERENCED:" +
                    bindingKey));
        }

        [Test]
        public void Package202TransparentEmissionNormalImportsBakedProperty()
        {
            const string bindingKey =
                "_MIKU_Baked_02e3bea4d308f49c5dc8";
            var request = new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    includeResource: true,
                    sourceName: "Pkg202EmissionNormal",
                    targetProfileHash: Package202ProfileHash,
                    explicitMaterialIrV2: ClosureNormalIr2(
                        "TransparentEmission",
                        bindingKey),
                    resourceBindingKey: bindingKey,
                    resourceSemantic: "BakedNormalHeight",
                    resourceUsage: "Scalar",
                    toolVersion: "2.0.2"),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            };

            var result = MikuBundleImporter.Import(request);

            Assert.That(
                result.success,
                Is.True,
                string.Join(" | ", result.diagnostics));
            var subGraphPath = result.assetPaths.Single(path =>
                path.EndsWith(
                    ".generated.shadersubgraph",
                    StringComparison.Ordinal));
            var wrapperPath = result.assetPaths.Single(path =>
                path.EndsWith(".shadergraph", StringComparison.Ordinal));
            Assert.That(
                File.ReadAllText(ToAbsolute(subGraphPath), Encoding.UTF8),
                Does.Contain(bindingKey));
            Assert.That(
                File.ReadAllText(ToAbsolute(wrapperPath), Encoding.UTF8),
                Does.Contain(bindingKey));
            var materialPath = result.assetPaths.Single(path =>
                path.EndsWith(".generated.mat", StringComparison.Ordinal));
            var material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.HasProperty(bindingKey), Is.True);
            Assert.That(material.GetTexture(bindingKey), Is.Not.Null);
            var shader =
                AssetDatabase.LoadAssetAtPath<Shader>(wrapperPath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }

        [Test]
        public void RuntimeSubGraphSamplesChannelTextureResources()
        {
            var ir = new JObject
            {
                ["expressions"] = new JArray
                {
                    Expression(
                        "view",
                        "Input.ViewDirection",
                        "Vector3",
                        "Fragment"),
                },
                ["parameters"] = new JArray(),
                ["resources"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = "roughness-resource",
                        ["channel"] = "R",
                        ["colorSpace"] = "Linear",
                    },
                },
                ["channels"] = new JArray
                {
                    ExpressionChannel("BaseColor", "view"),
                    new JObject
                    {
                        ["semantic"] = "Roughness",
                        ["valueType"] = "Scalar",
                        ["required"] = true,
                        ["value"] = new JObject
                        {
                            ["kind"] = "TextureResource",
                            ["resourceId"] = "roughness-resource",
                        },
                    },
                },
            };

            var generated = GenerateRuntimeSubGraph(
                ir,
                "channel-texture-fixture");

            Assert.That(generated, Does.Contain("_RoughnessMap"));
            Assert.That(
                generated,
                Does.Contain("UnityEditor.ShaderGraph.SampleTexture2DNode"));
        }

        [Test]
        public void RuntimeBundleImportsAndRegeneratesOwnedSubGraphWithoutChangingWrapper()
        {
            var expressions = new JArray
            {
                Expression(
                    "camera-distance",
                    "Input.Camera.ViewDistance",
                    "Scalar",
                    "Fragment"),
            };
            var request = new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    explicitChannels: new JArray
                    {
                        ExpressionChannel("Roughness", "camera-distance"),
                    },
                    explicitExpressions: expressions),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            };

            var first = MikuBundleImporter.Import(request);

            Assert.That(first.success, Is.True, string.Join(" | ", first.diagnostics));
            Assert.That(
                first.diagnostics,
                Does.Contain("MIKU_RUNTIME_INPUT_PRESERVED"));
            var wrapperPath = first.assetPaths.Single(
                path => path.EndsWith(".shadergraph", StringComparison.Ordinal));
            var subGraphPath = first.assetPaths.Single(
                path => path.EndsWith(
                    ".generated.shadersubgraph",
                    StringComparison.Ordinal));
            var wrapperBytes = File.ReadAllBytes(ToAbsolute(wrapperPath));
            var subGraphBytes = File.ReadAllBytes(ToAbsolute(subGraphPath));
            Assert.That(
                Encoding.UTF8.GetString(subGraphBytes),
                Does.Contain("UnityEditor.ShaderGraph.PositionNode"));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(wrapperPath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);

            request.fullRegeneration = false;
            var second = MikuBundleImporter.Import(request);

            Assert.That(second.success, Is.True, string.Join(" | ", second.diagnostics));
            Assert.That(
                File.ReadAllBytes(ToAbsolute(wrapperPath)),
                Is.EqualTo(wrapperBytes));
            Assert.That(
                File.ReadAllBytes(ToAbsolute(subGraphPath)),
                Is.EqualTo(subGraphBytes));
        }

        [Test]
        public void ReachableFaceSignImportsOnFirstAttemptAndRegeneratesDeterministically()
        {
            var ir = SurfaceModelIr2("OpaquePBR");
            ir["parameters"] = new JArray();
            ir["expressions"] = new JArray
            {
                Expression(
                    "blend",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.35f }),
                Expression(
                    "normal",
                    "Input.Normal",
                    "Float3",
                    "Fragment"),
                Expression(
                    "view",
                    "Input.ViewDirection",
                    "Float3",
                    "Fragment"),
                Expression(
                    "front",
                    "Input.IsFrontFace",
                    "Boolean",
                    "Fragment"),
                Expression(
                    "layer-weight-fresnel",
                    "Math.LayerWeightFresnel",
                    "Scalar",
                    "Fragment",
                    null,
                    new JObject
                    {
                        ["Blend"] = new JObject
                        {
                            ["expressionId"] = "blend",
                        },
                        ["Normal"] = new JObject
                        {
                            ["expressionId"] = "normal",
                        },
                        ["ViewDirection"] = new JObject
                        {
                            ["expressionId"] = "view",
                        },
                        ["IsFrontFace"] = new JObject
                        {
                            ["expressionId"] = "front",
                        },
                    }),
            };
            var roughness = ((JArray)ir["channels"])
                .OfType<JObject>()
                .Single(channel => string.Equals(
                    channel["semantic"]?.Value<string>(),
                    "Roughness",
                    StringComparison.Ordinal));
            roughness.Replace(
                ExpressionChannel(
                    "Roughness",
                    "layer-weight-fresnel"));
            Assert.That(
                MikuBundleImporter.HasRuntimeVertexDisplacement(ir),
                Is.False);

            var request = new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    sourceName: "FaceSignFirstImport",
                    targetProfileHash: CurrentProfileHashV22,
                    explicitMaterialIrV2: ir),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            };
            Assert.That(
                AssetDatabase.IsValidFolder(request.outputRoot),
                Is.False);

            var first = MikuBundleImporter.Import(request);

            Assert.That(
                first.success,
                Is.True,
                string.Join(" | ", first.diagnostics));
            Assert.That(
                first.diagnostics,
                Does.Contain("MIKU_SURFACE_MODEL_PRESERVED:OpaquePBR"));
            var wrapperPath = first.assetPaths.Single(
                path => path.EndsWith(
                    ".shadergraph",
                    StringComparison.Ordinal));
            var subGraphPath = first.assetPaths.Single(
                path => path.EndsWith(
                    ".generated.shadersubgraph",
                    StringComparison.Ordinal));
            var wrapperBytes =
                File.ReadAllBytes(ToAbsolute(wrapperPath));
            var subGraphBytes =
                File.ReadAllBytes(ToAbsolute(subGraphPath));
            var subGraphGuid =
                AssetDatabase.AssetPathToGUID(subGraphPath);
            Assert.That(subGraphGuid, Has.Length.EqualTo(32));
            Assert.That(
                AssetDatabase.GUIDToAssetPath(subGraphGuid),
                Is.EqualTo(subGraphPath));
            Assert.That(
                Encoding.UTF8.GetString(subGraphBytes),
                Does.Contain(
                    "UnityEditor.ShaderGraph.IsFrontFaceNode"));
            Assert.That(
                Encoding.UTF8.GetString(wrapperBytes),
                Does.Contain(subGraphGuid));
            AssertNoDirectWrapperEdge(
                Encoding.UTF8.GetString(wrapperBytes),
                "Vertex Position",
                "VertexDescription.Position");

            var shader =
                AssetDatabase.LoadAssetAtPath<Shader>(wrapperPath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var compileErrors = ShaderUtil.GetShaderMessages(shader)
                .Where(message => string.Equals(
                    message.severity.ToString(),
                    "Error",
                    StringComparison.OrdinalIgnoreCase))
                .Select(message => message.message)
                .ToArray();
            Assert.That(
                compileErrors,
                Is.Empty,
                string.Join(" | ", compileErrors));

            request.fullRegeneration = false;
            var second = MikuBundleImporter.Import(request);

            Assert.That(
                second.success,
                Is.True,
                string.Join(" | ", second.diagnostics));
            Assert.That(
                File.ReadAllBytes(ToAbsolute(wrapperPath)),
                Is.EqualTo(wrapperBytes));
            Assert.That(
                File.ReadAllBytes(ToAbsolute(subGraphPath)),
                Is.EqualTo(subGraphBytes));
            Assert.That(
                AssetDatabase.AssetPathToGUID(subGraphPath),
                Is.EqualTo(subGraphGuid));
            Assert.That(
                AssetDatabase.GUIDToAssetPath(subGraphGuid),
                Is.EqualTo(subGraphPath));
        }

        [Test]
        public void ClosureAwareV2BundleImportsCustomMultiLobeShader()
        {
            var request = new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    sourceName: "ClosureAwareV2",
                    targetProfileHash: CurrentProfileHashV2,
                    explicitMaterialIrV2:
                        SurfaceModelIr2("CustomMultiLobe")),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            };

            var result = MikuBundleImporter.Import(request);

            Assert.That(
                result.success,
                Is.True,
                string.Join(" | ", result.diagnostics));
            Assert.That(
                result.diagnostics,
                Does.Contain(
                    "MIKU_SURFACE_MODEL_PRESERVED:CustomMultiLobe"));
            var wrapperPath = result.assetPaths.Single(path =>
                path.EndsWith(".shadergraph", StringComparison.Ordinal));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(wrapperPath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var materialPath = result.assetPaths.Single(path =>
                path.EndsWith(".generated.mat", StringComparison.Ordinal));
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material.GetColor("_EmissionColor"), Is.EqualTo(Color.white));
            Assert.That(material.GetFloat("_EmissionStrength"), Is.EqualTo(1f));
        }

        [Test]
        public void ExternalPackage103MultiLobeRegressionBundlesImportWhenProvided()
        {
            var fixtureRoot = Environment.GetEnvironmentVariable(
                "MIKU_103_REGRESSION_BUNDLE_ROOT");
            if (string.IsNullOrWhiteSpace(fixtureRoot))
            {
                Assert.Ignore(
                    "Set MIKU_103_REGRESSION_BUNDLE_ROOT to the immutable " +
                    "Miku 1.0.3 output directory.");
            }

            var fixtures = new[]
            {
                (Directory: "彩色镀层5__70dcd51d8b5b", File: "彩色镀层5.mikubundle", HasMesh: false),
                (Directory: "彩色镀层8__576e51791e32", File: "彩色镀层8.mikubundle", HasMesh: true),
                (Directory: "凹凸石3__b4c02f01f6e4", File: "凹凸石3.mikubundle", HasMesh: true),
            };
            foreach (var fixture in fixtures)
            {
                var result = MikuBundleImporter.Import(new MikuImportRequest
                {
                    bundlePath = Path.Combine(
                        fixtureRoot,
                        fixture.Directory,
                        fixture.File),
                    outputRoot = OutputRoot + "/Package103Regression",
                    fullRegeneration = true,
                    createMaterialVariant = true,
                });
                Assert.That(
                    result.success,
                    Is.True,
                    fixture.File + ": " +
                    string.Join(" | ", result.diagnostics));

                if (fixture.File.StartsWith("彩色镀层", StringComparison.Ordinal))
                {
                    Assert.That(
                        result.diagnostics,
                        Does.Contain(
                            "MIKU_LEGACY_CLOSURE_ZERO_NORMAL_NORMALIZED"));
                }
                Assert.That(
                    result.assetPaths.Any(path => path.EndsWith(
                        ".prefab",
                        StringComparison.Ordinal)),
                    Is.EqualTo(fixture.HasMesh));

                foreach (var texturePath in result.assetPaths.Where(path =>
                    path.EndsWith(".exr", StringComparison.OrdinalIgnoreCase)))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(
                        texturePath);
                    Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
                    Assert.That(importer.sRGBTexture, Is.False);
                }
                foreach (var texturePath in result.assetPaths.Where(path =>
                    fixture.File.StartsWith("凹凸石", StringComparison.Ordinal) &&
                    path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(
                        texturePath);
                    Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.NormalMap));
                    Assert.That(importer.flipGreenChannel, Is.False);
                }

                foreach (var prefabPath in result.assetPaths.Where(path =>
                    path.EndsWith(".prefab", StringComparison.Ordinal)))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        prefabPath);
                    Assert.That(prefab, Is.Not.Null);
                    Assert.That(
                        prefab.GetComponentsInChildren<Renderer>(true)
                            .SelectMany(renderer => renderer.sharedMaterials)
                            .Any(material => material != null),
                        Is.True,
                        fixture.File + " Source Mesh material slot is unbound.");
                }
            }
        }

        [Test]
        public void ClosureAwareV2BundleImportsClearCoatShader()
        {
            var request = new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    sourceName: "ClosureAwareClearCoat",
                    targetProfileHash: CurrentProfileHashV2,
                    explicitMaterialIrV2:
                        ClearCoatSurfaceModelIr2()),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = false,
            };

            var result = MikuBundleImporter.Import(request);

            Assert.That(
                result.success,
                Is.True,
                string.Join(" | ", result.diagnostics));
            Assert.That(
                result.diagnostics,
                Does.Contain("MIKU_SURFACE_MODEL_PRESERVED:OpaquePBR"));
            var wrapperPath = result.assetPaths.Single(path =>
                path.EndsWith(".shadergraph", StringComparison.Ordinal));
            var wrapper = File.ReadAllText(
                ToAbsolute(wrapperPath),
                Encoding.UTF8);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(wrapperPath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            Assert.That(wrapper, Does.Contain("\"m_ClearCoat\": true"));
            AssertDirectWrapperEdge(
                wrapper,
                "Coat Mask",
                "SurfaceDescription.CoatMask");
            AssertDirectWrapperEdge(
                wrapper,
                "Coat Smoothness",
                "SurfaceDescription.CoatSmoothness");
        }

        [Test]
        public void Package203KeepsBoundedCompatibilityWithPackage200Profile()
        {
            var imported = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = WriteValidBundle(
                        sourceName: "Package200V2Compatibility",
                        targetProfileHash:
                            Package200And201ProfileHash,
                        explicitMaterialIrV2:
                            SurfaceModelIr2("OpaquePBR")),
                    outputRoot = OutputRoot,
                    fullRegeneration = true,
                    createMaterialVariant = false,
                });

            Assert.That(
                imported.success,
                Is.True,
                string.Join(" | ", imported.diagnostics));
            Assert.That(
                imported.diagnostics,
                Does.Contain(
                    "MIKU_TARGET_PROFILE_2_0_X_COMPATIBILITY"));
        }

        [Test]
        public void Package200ProfileCannotClaimClearCoatSupport()
        {
            var imported = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = WriteValidBundle(
                        sourceName: "Package200InvalidClearCoat",
                        targetProfileHash:
                            Package200And201ProfileHash,
                        explicitMaterialIrV2:
                            ClearCoatSurfaceModelIr2()),
                    outputRoot = OutputRoot,
                    fullRegeneration = true,
                    createMaterialVariant = false,
                });

            Assert.That(imported.success, Is.False);
            Assert.That(
                string.Join(" | ", imported.diagnostics),
                Does.Contain(
                    "MIKU_COAT_PROFILE_REEXPORT_REQUIRED_2_0_2"));
        }

        [TestCase("TransparentEmission")]
        [TestCase("TransparentLit")]
        public void ClosureAwareV2BundleImportsTransparentSurface(
            string surfaceKind)
        {
            var request = new MikuImportRequest
            {
                bundlePath = WriteValidBundle(
                    sourceName: "ClosureAware" + surfaceKind,
                    targetProfileHash: CurrentProfileHashV2,
                    explicitMaterialIrV2:
                        SurfaceModelIr2(surfaceKind)),
                outputRoot = OutputRoot,
                fullRegeneration = true,
                createMaterialVariant = true,
            };

            var result = MikuBundleImporter.Import(request);

            Assert.That(
                result.success,
                Is.True,
                string.Join(" | ", result.diagnostics));
            Assert.That(
                result.diagnostics,
                Does.Contain(
                    "MIKU_SURFACE_MODEL_PRESERVED:" + surfaceKind));
            var wrapperPath = result.assetPaths.Single(path =>
                path.EndsWith(".shadergraph", StringComparison.Ordinal));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(wrapperPath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            Assert.That(
                MikuShaderGraph17RuntimeBackend
                    .WrapperRenderContractMatches(
                        File.ReadAllText(
                            ToAbsolute(wrapperPath),
                            Encoding.UTF8),
                        MikuSurfaceModelBackends
                            .Resolve(SurfaceModelIr2(surfaceKind))
                            .WrapperContract(
                                SurfaceModelIr2(surfaceKind))),
                Is.True);
            var target = ParseMultiJson(
                    File.ReadAllText(
                        ToAbsolute(wrapperPath),
                        Encoding.UTF8))
                .Single(item =>
                    (item["m_Type"]?.Value<string>() ?? "")
                    .EndsWith(
                        ".UniversalTarget",
                        StringComparison.Ordinal));
            Assert.That(
                target["m_AlphaMode"]?.Value<int>(),
                Is.EqualTo(1));
            var baseMaterialPath = result.assetPaths.Single(path =>
                path.EndsWith(
                    ".generated.mat",
                    StringComparison.Ordinal));
            var baseMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    baseMaterialPath);
            Assert.That(baseMaterial, Is.Not.Null);
            Assert.That(
                baseMaterial.GetTag(
                    "RenderType",
                    false),
                Is.EqualTo("Transparent"));
        }

        [TestCase("TransparentEmission")]
        [TestCase("TransparentLit")]
        [TestCase("CustomMultiLobe")]
        public void ClosureCompositeMaterialUsesNeutralBaseColorTint(
            string surfaceKind)
        {
            var imported = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = WriteValidBundle(
                        sourceName: "ClosureTint" + surfaceKind,
                        targetProfileHash: CurrentProfileHashV2,
                        explicitMaterialIrV2:
                            SurfaceModelIr2(surfaceKind)),
                    outputRoot = OutputRoot,
                    fullRegeneration = true,
                    createMaterialVariant = true,
                });

            Assert.That(
                imported.success,
                Is.True,
                string.Join(" | ", imported.diagnostics));
            var materialPath = imported.assetPaths.Single(path =>
                path.EndsWith(
                    ".generated.mat",
                    StringComparison.Ordinal));
            var material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(
                material.GetColor("_BaseColor"),
                Is.EqualTo(Color.white));
        }

        [Test]
        public void RuntimeTimeControlsAreFinalShaderMaterialProperties()
        {
            var expressions = new JArray
            {
                Expression(
                    "time-seconds",
                    "Input.Time.Seconds",
                    "Scalar",
                    "Both",
                    new JObject
                    {
                        ["contract"] = "miku_time_v1",
                        ["sourceFps"] = 24f,
                        ["frameStart"] = 1,
                    }),
            };
            var imported = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = WriteValidBundle(
                        sourceId: "time-source",
                        materialId: "time-material",
                        sourceName: "Runtime Time",
                        explicitChannels: new JArray
                        {
                            ExpressionChannel(
                                "Roughness",
                                "time-seconds"),
                        },
                        explicitExpressions: expressions),
                    outputRoot = OutputRoot,
                    fullRegeneration = true,
                    createMaterialVariant = true,
                });

            Assert.That(
                imported.success,
                Is.True,
                string.Join(" | ", imported.diagnostics));
            var materialPath = imported.assetPaths.Single(
                path => path.EndsWith(
                    ".generated.mat",
                    StringComparison.Ordinal));
            var material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(material, Is.Not.Null);
            foreach (var reference in new[]
                     {
                         "_MIKU_EffectTimeScale",
                         "_MIKU_EffectTimeOffset",
                         "_MIKU_EffectTimeOverride",
                         "_MIKU_EffectUseTimeOverride",
                     })
            {
                Assert.That(
                    material.HasProperty(reference),
                    Is.True,
                    reference);
            }
            Assert.That(
                material.GetFloat("_MIKU_EffectTimeScale"),
                Is.EqualTo(1f));
            Assert.That(
                material.GetFloat("_MIKU_EffectTimeOffset"),
                Is.EqualTo(0f));
            Assert.That(
                material.GetFloat("_MIKU_EffectTimeOverride"),
                Is.EqualTo(0f));
            Assert.That(
                material.GetFloat("_MIKU_EffectUseTimeOverride"),
                Is.EqualTo(0f));
            Assert.That(
                ShaderUtil.ShaderHasError(material.shader),
                Is.False);
        }

        [Test]
        public void PhysicalFresnelAndLayerWeightExpandToMathNodes()
        {
            var expressions = new JArray
            {
                Expression(
                    "ior",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 1.45f }),
                Expression("normal", "Input.Normal", "Float3", "Fragment"),
                Expression("view", "Input.ViewDirection", "Float3", "Fragment"),
                Expression("front", "Input.IsFrontFace", "Boolean", "Fragment"),
                Expression(
                    "fresnel",
                    "Math.DielectricFresnel",
                    "Scalar",
                    "Fragment",
                    null,
                    new JObject
                    {
                        ["IOR"] = new JObject { ["expressionId"] = "ior" },
                        ["Normal"] = new JObject { ["expressionId"] = "normal" },
                        ["ViewDirection"] = new JObject { ["expressionId"] = "view" },
                        ["IsFrontFace"] = new JObject { ["expressionId"] = "front" },
                    }),
            };
            var ir = new JObject
            {
                ["expressions"] = expressions,
                ["parameters"] = new JArray(),
                ["channels"] = new JArray
                {
                    ExpressionChannel("Roughness", "fresnel"),
                },
            };

            var generated = GenerateRuntimeSubGraph(ir, "fresnel-fixture");

            Assert.That(generated, Does.Contain("UnityEditor.ShaderGraph.DotProductNode"));
            Assert.That(generated, Does.Contain("UnityEditor.ShaderGraph.SquareRootNode"));
            Assert.That(generated, Does.Contain("UnityEditor.ShaderGraph.BranchNode"));
            Assert.That(generated, Does.Not.Contain("UnityEditor.ShaderGraph.FresnelNode"));
            Assert.That(generated, Does.Not.Contain("CustomFunctionNode"));
        }

        [Test]
        public void MaterialHeightChannelUsesVertexLodZeroAndIrDefaults()
        {
            var ir = SurfaceIr("StandardLit", "Opaque");
            ir["resources"] = new JArray
            {
                new JObject
                {
                    ["id"] = "height-resource",
                    ["semantic"] = "Height",
                    ["channel"] = "R",
                    ["colorSpace"] = "Linear",
                },
            };
            ir["expressions"] = new JArray
            {
                Expression(
                    "material-height",
                    "Input.MaterialChannel",
                    "Scalar",
                    "Vertex",
                    new JObject
                    {
                        ["semantic"] = "Height",
                        ["uvSet"] = "UV0",
                        ["lod"] = 0,
                    }),
                Expression(
                    "position-object",
                    "Input.Position.Object",
                    "Float3",
                    "Vertex"),
                Expression(
                    "normal-object",
                    "Input.Normal.Object",
                    "Float3",
                    "Vertex"),
                Expression(
                    "vertex-displacement",
                    "Vector.Displacement",
                    "Float3",
                    "Vertex",
                    new JObject
                    {
                        ["midlevel"] = 0.25f,
                        ["scale"] = -0.125f,
                        ["midlevelReference"] = "_MIKU_HeightMidlevel",
                        ["scaleReference"] = "_MIKU_HeightScale",
                    },
                    new JObject
                    {
                        ["Height"] = new JObject
                        {
                            ["expressionId"] = "material-height",
                        },
                        ["Position"] = new JObject
                        {
                            ["expressionId"] = "position-object",
                        },
                        ["Normal"] = new JObject
                        {
                            ["expressionId"] = "normal-object",
                        },
                    }),
            };
            ir["channels"] = new JArray
            {
                new JObject
                {
                    ["semantic"] = "Height",
                    ["valueType"] = "Scalar",
                    ["stage"] = "Fragment",
                    ["required"] = true,
                    ["value"] = new JObject
                    {
                        ["kind"] = "TextureResource",
                        ["resourceId"] = "height-resource",
                    },
                },
                ExpressionChannel("Displacement", "vertex-displacement"),
                ConstantChannel("Alpha", 1.0),
            };

            var generated = GenerateRuntimeSubGraph(ir, "material-height-fixture");

            Assert.That(generated, Does.Contain("SampleTexture2DLODNode"));
            Assert.That(generated, Does.Contain("_MIKU_HeightMap"));
            Assert.That(generated, Does.Contain("_MIKU_HeightMidlevel"));
            Assert.That(generated, Does.Contain("_MIKU_HeightScale"));
            Assert.That(generated, Does.Contain("0.25"));
            Assert.That(generated, Does.Contain("-0.125"));
        }

        [Test]
        public void CameraExpressionCannotFeedVertexStage()
        {
            var ir = new JObject
            {
                ["expressions"] = new JArray
                {
                    Expression(
                        "camera-distance",
                        "Input.Camera.ViewDistance",
                        "Scalar",
                        "Fragment"),
                },
                ["parameters"] = new JArray(),
                ["channels"] = new JArray
                {
                    new JObject
                    {
                        ["semantic"] = "VertexPosition",
                        ["valueType"] = "Scalar",
                        ["stage"] = "Vertex",
                        ["required"] = true,
                        ["value"] = new JObject
                        {
                            ["kind"] = "Expression",
                            ["expressionId"] = "camera-distance",
                        },
                    },
                },
            };

            var error = Assert.Throws<TargetInvocationException>(
                () => GenerateRuntimeSubGraph(ir, "camera-vertex-fixture"));

            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(
                error.InnerException.Message,
                Does.StartWith("shader_stage_conflict:"));
        }

        [Test]
        public void StaticPbrTexturesGenerateNormalBlendAndVertexDisplacement()
        {
            JObject Ref(string id)
            {
                return new JObject { ["expressionId"] = id };
            }

            JObject Sample(
                string id,
                string reference,
                string semantic,
                string usage,
                string channel,
                string stage,
                string lodMode)
            {
                return Expression(
                    id,
                    "Texture.SampleImage2D",
                    usage == "Scalar" ? "Scalar" :
                    usage == "Normal" ? "Float3" : "Color",
                    stage,
                    new JObject
                    {
                        ["resourceId"] = "resource-" + id,
                        ["referenceName"] = reference,
                        ["semantic"] = semantic,
                        ["usage"] = usage,
                        ["channel"] = channel,
                        ["colorSpace"] =
                            usage == "Color" ? "sRGB" : "Linear",
                        ["uvSet"] = "UV0",
                        ["lodMode"] = lodMode,
                    });
            }

            var ir = SurfaceIr("StandardLit", "Opaque");
            ir["expressions"] = new JArray
            {
                Sample(
                    "normal-sample",
                    "_BumpMap",
                    "Normal",
                    "Normal",
                    "RGB",
                    "Fragment",
                    "Auto"),
                Expression(
                    "normal-strength-value",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.8f }),
                Expression(
                    "normal-strength",
                    "Vector.NormalStrength",
                    "Float3",
                    "Fragment",
                    inputs: new JObject
                    {
                        ["Normal"] = Ref("normal-sample"),
                        ["Strength"] = Ref("normal-strength-value"),
                    }),
                Sample(
                    "height-fragment",
                    "_MIKU_HeightMap",
                    "Height",
                    "Scalar",
                    "R",
                    "Fragment",
                    "Auto"),
                Expression(
                    "height-midlevel-fragment",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.5f }),
                Expression(
                    "height-strength-fragment",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.1f }),
                Expression(
                    "height-normal",
                    "Vector.NormalFromHeight",
                    "Float3",
                    "Fragment",
                    new JObject
                    {
                        ["bumpStrengthReference"] =
                            "_MIKU_BumpStrength",
                        ["bumpDistanceReference"] =
                            "_MIKU_BumpDistance",
                    },
                    inputs: new JObject
                    {
                        ["Height"] = Ref("height-fragment"),
                        ["Midlevel"] = Ref("height-midlevel-fragment"),
                        ["Strength"] = Ref("height-strength-fragment"),
                    }),
                Expression(
                    "normal-blend",
                    "Vector.NormalBlend",
                    "Float3",
                    "Fragment",
                    new JObject { ["blendMode"] = "Reoriented" },
                    new JObject
                    {
                        ["Base"] = Ref("normal-strength"),
                        ["Detail"] = Ref("height-normal"),
                    }),
                Sample(
                    "roughness-sample",
                    "_RoughnessMap",
                    "Roughness",
                    "Scalar",
                    "R",
                    "Fragment",
                    "Auto"),
                Sample(
                    "height-vertex",
                    "_MIKU_HeightMap",
                    "Height",
                    "Scalar",
                    "R",
                    "Vertex",
                    "Explicit0"),
                Expression(
                    "position-object",
                    "Input.Position.Object",
                    "Float3",
                    "Vertex"),
                Expression(
                    "normal-object",
                    "Input.Normal.Object",
                    "Float3",
                    "Vertex"),
                Expression(
                    "vertex-displacement",
                    "Vector.Displacement",
                    "Float3",
                    "Vertex",
                    new JObject
                    {
                        ["midlevel"] = 0.5f,
                        ["scale"] = 0.1f,
                        ["midlevelReference"] =
                            "_MIKU_HeightMidlevel",
                        ["scaleReference"] = "_MIKU_HeightScale",
                    },
                    new JObject
                    {
                        ["Height"] = Ref("height-vertex"),
                        ["Position"] = Ref("position-object"),
                        ["Normal"] = Ref("normal-object"),
                    }),
            };
            ir["channels"] = new JArray
            {
                ExpressionChannel("Roughness", "roughness-sample"),
                new JObject
                {
                    ["semantic"] = "Normal",
                    ["valueType"] = "Float3",
                    ["stage"] = "Fragment",
                    ["required"] = true,
                    ["value"] = new JObject
                    {
                        ["kind"] = "Expression",
                        ["expressionId"] = "normal-blend",
                    },
                },
                new JObject
                {
                    ["semantic"] = "Displacement",
                    ["valueType"] = "Float3",
                    ["stage"] = "Vertex",
                    ["required"] = true,
                    ["value"] = new JObject
                    {
                        ["kind"] = "Expression",
                        ["expressionId"] = "vertex-displacement",
                    },
                },
                ConstantChannel("Alpha", 1.0),
            };
            var materialId = "static-pbr-runtime";
            var runtime = GenerateRuntimeSubGraph(ir, materialId);

            Assert.That(runtime, Does.Contain("SampleTexture2DLODNode"));
            Assert.That(runtime, Does.Contain("NormalStrengthNode"));
            Assert.That(runtime, Does.Contain("NormalBlendNode"));
            Assert.That(runtime, Does.Contain("NormalFromHeightNode"));
            Assert.That(runtime, Does.Contain("OneMinusNode"));
            Assert.That(runtime, Does.Contain("_MIKU_HeightMidlevel"));
            Assert.That(runtime, Does.Contain("_MIKU_HeightScale"));
            Assert.That(runtime, Does.Contain("_MIKU_BumpStrength"));
            Assert.That(runtime, Does.Contain("_MIKU_BumpDistance"));
            Assert.That(
                MikuBundleImporter.HasRuntimeVertexDisplacement(ir),
                Is.True);
            var wrapper = MikuShaderGraph17RuntimeBackend.GenerateWrapper(
                File.ReadAllText(
                    ToAbsolute(
                        MikuWorkflowBackends.StandardWrapperTemplate),
                    Encoding.UTF8),
                runtime,
                materialId,
                "0123456789abcdef0123456789abcdef",
                ir["surfaceContract"] as JObject);
            wrapper = MikuBundleImporter.ApplyRuntimeWrapperVertexContract(
                wrapper,
                ir);
            AssertDirectWrapperEdge(
                wrapper,
                "Vertex Position",
                "VertexDescription.Position");
        }

        [Test]
        public void PackedPbrChannelsReuseOneFragmentSampleAndNeutralizeOcclusion()
        {
            JObject Ref(string id)
            {
                return new JObject { ["expressionId"] = id };
            }

            var bindings = new JArray
            {
                new JObject
                {
                    ["semantic"] = "Metalness",
                    ["channel"] = "R",
                },
                new JObject
                {
                    ["semantic"] = "Roughness",
                    ["channel"] = "G",
                },
                new JObject
                {
                    ["semantic"] = "AmbientOcclusion",
                    ["channel"] = "B",
                },
                new JObject
                {
                    ["semantic"] = "Alpha",
                    ["channel"] = "A",
                },
            };
            JObject Sample(string id, string semantic, string channel)
            {
                return Expression(
                    id,
                    "Texture.SampleImage2D",
                    "Scalar",
                    "Fragment",
                    new JObject
                    {
                        ["resourceId"] = "packed-resource",
                        ["referenceName"] = "_MIKU_Packed_test",
                        ["semantic"] = semantic,
                        ["usage"] = "Scalar",
                        ["channel"] = channel,
                        ["colorSpace"] = "Linear",
                        ["uvSet"] = "UV0",
                        ["lodMode"] = "Auto",
                        ["packed"] = true,
                        ["channelBindings"] = bindings.DeepClone(),
                    });
            }

            var ir = SurfaceIr("StandardLit", "AlphaBlend");
            ir["expressions"] = new JArray
            {
                Sample("packed-metalness", "Metalness", "R"),
                Sample("packed-roughness", "Roughness", "G"),
                Sample(
                    "packed-ambient-occlusion",
                    "AmbientOcclusion",
                    "B"),
                Sample("packed-alpha", "Alpha", "A"),
                Expression(
                    "separate-component",
                    "Vector.Component",
                    "Scalar",
                    "Fragment",
                    new JObject { ["component"] = "G" },
                    new JObject
                    {
                        ["Input"] = Ref("packed-roughness"),
                    }),
                Expression(
                    "explicit-invert",
                    "Math.OneMinus",
                    "Scalar",
                    "Fragment",
                    inputs: new JObject
                    {
                        ["A"] = Ref("separate-component"),
                    }),
            };
            ir["channels"] = new JArray
            {
                ExpressionChannel("Metalness", "packed-metalness"),
                ExpressionChannel("Roughness", "explicit-invert"),
                ExpressionChannel(
                    "AmbientOcclusion",
                    "packed-ambient-occlusion"),
                ExpressionChannel("Alpha", "packed-alpha"),
            };

            var runtime = GenerateRuntimeSubGraph(ir, "packed-runtime");
            var objects = ParseMultiJson(runtime);
            Assert.That(
                objects.Count(item =>
                    (item["m_Type"]?.Value<string>() ?? "")
                    .EndsWith(
                        ".SampleTexture2DNode",
                        StringComparison.Ordinal)),
                Is.EqualTo(1));
            Assert.That(
                objects.Count(item =>
                    string.Equals(
                        item["m_OverrideReferenceName"]?.Value<string>(),
                        "_MIKU_Packed_test",
                        StringComparison.Ordinal)),
                Is.EqualTo(1));
            Assert.That(
                runtime,
                Does.Contain(
                    "Packed PBR Map (R=Metalness, G=Roughness, " +
                    "B=AmbientOcclusion, A=Alpha)"));
            Assert.That(runtime, Does.Contain("SplitNode"));
            Assert.That(runtime, Does.Contain("OneMinusNode"));
            Assert.That(runtime, Does.Contain("_OcclusionStrength"));
            Assert.That(runtime, Does.Contain("_Opacity"));
            Assert.That(runtime, Does.Contain("_AlphaClipThreshold"));

            var graph = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(".GraphData", StringComparison.Ordinal));
            var byId = objects
                .Where(item => item["m_ObjectId"] != null)
                .ToDictionary(
                    item => item["m_ObjectId"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            var output = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(
                    ".SubGraphOutputNode",
                    StringComparison.Ordinal));
            var occlusionSlot = (output["m_Slots"] as JArray ??
                                 new JArray())
                .OfType<JObject>()
                .Select(item =>
                    byId[item["m_Id"]?.Value<string>() ?? ""])
                .Single(item => string.Equals(
                    item["m_DisplayName"]?.Value<string>(),
                    "Occlusion",
                    StringComparison.Ordinal));
            var occlusionEdge = (graph["m_Edges"] as JArray ??
                                 new JArray())
                .OfType<JObject>()
                .Single(edge =>
                    string.Equals(
                        edge["m_InputSlot"]?["m_Node"]?["m_Id"]
                            ?.Value<string>(),
                        output["m_ObjectId"]?.Value<string>(),
                        StringComparison.Ordinal) &&
                    edge["m_InputSlot"]?["m_SlotId"]?.Value<int>() ==
                    occlusionSlot["m_Id"]?.Value<int>());
            var occlusionSource = byId[
                occlusionEdge["m_OutputSlot"]?["m_Node"]?["m_Id"]
                    ?.Value<string>() ?? ""];
            Assert.That(
                occlusionSource["m_Type"]?.Value<string>() ?? "",
                Does.EndWith(".Vector1Node"));
        }

        static JObject Expression(
            string id,
            string op,
            string valueType,
            string stage,
            JObject parameters = null,
            JObject inputs = null)
        {
            return new JObject
            {
                ["id"] = id,
                ["op"] = op,
                ["valueType"] = valueType,
                ["space"] = "None",
                ["stage"] = stage,
                ["uniformity"] = stage == "Both" ? "Uniform" : "Varying",
                ["inputs"] = inputs ?? new JObject(),
                ["params"] = parameters ?? new JObject(),
            };
        }

        static JObject ExpressionChannel(string semantic, string expressionId)
        {
            return new JObject
            {
                ["semantic"] = semantic,
                ["valueType"] = "Scalar",
                ["required"] = true,
                ["value"] = new JObject
                {
                    ["kind"] = "Expression",
                    ["expressionId"] = expressionId,
                },
            };
        }

        static JObject ExpressionColorChannel(
            string semantic,
            string expressionId)
        {
            var channel = ExpressionChannel(semantic, expressionId);
            channel["valueType"] = "Color";
            return channel;
        }

        static JObject SurfaceIr(string model, string renderMethod)
        {
            var channels = new JArray
            {
                ConstantChannel("Alpha", 1.0),
            };
            var contract = new JObject
            {
                ["schema"] = "miku-surface-1.0",
                ["model"] = model,
                ["renderMethod"] = renderMethod,
                ["renderFace"] = "Both",
                ["coverageChannel"] = "Alpha",
            };
            if (model == "DielectricScreenRefraction")
            {
                channels.Add(
                    ConstantChannel(
                        "TransmissionColor",
                        new JArray(1.0, 1.0, 1.0)));
                channels.Add(ConstantChannel("TransmissionWeight", 1.0));
                channels.Add(ConstantChannel("IOR", 1.5));
                channels.Add(ConstantChannel("Thickness", 0.1));
                channels.Add(ConstantChannel("Roughness", 0.0));
                channels.Add(
                    ConstantChannel(
                        "Normal",
                        new JArray(0.0, 0.0, 1.0)));
                contract["transmissionColorChannel"] = "TransmissionColor";
                contract["transmissionWeightChannel"] = "TransmissionWeight";
                contract["iorChannel"] = "IOR";
                contract["thicknessChannel"] = "Thickness";
                contract["roughnessChannel"] = "Roughness";
                contract["normalChannel"] = "Normal";
            }
            return new JObject
            {
                ["documentKind"] = "miku-material-ir-1.0",
                ["schemaVersion"] = "1.0",
                ["workflow"] = new JObject { ["kind"] = "standard_pbr" },
                ["channels"] = channels,
                ["expressions"] = new JArray(),
                ["surfaceContract"] = contract,
            };
        }

        static JObject SurfaceModelIr2(string kind)
        {
            JObject ConstantParameter(
                JToken value,
                string valueType)
            {
                return new JObject
                {
                    ["kind"] = "Constant",
                    ["valueType"] = valueType,
                    ["value"] = value,
                };
            }

            JObject Term(
                string id,
                string closureKind,
                string domain,
                float weight,
                JObject parameters)
            {
                return new JObject
                {
                    ["id"] = id,
                    ["closureId"] = "closure-" + id,
                    ["closureKind"] = closureKind,
                    ["domain"] = domain,
                    ["parameters"] = parameters,
                    ["distribution"] = "MULTI_GGX",
                    ["finalWeight"] = new JObject
                    {
                        ["id"] = "weight-" + id,
                        ["kind"] = "Constant",
                        ["valueType"] = "Float",
                        ["value"] = weight,
                    },
                    ["weightTrace"] = new JArray(),
                    ["source"] = new JObject
                    {
                        ["nodeId"] = "node-" + id,
                        ["socketId"] = "Closure",
                        ["groupPath"] = new JArray(),
                    },
                };
            }

            var transparent = kind.StartsWith(
                                  "Transparent",
                                  StringComparison.Ordinal) ||
                              string.Equals(
                                  kind,
                                  "RefractiveGlass",
                                  StringComparison.Ordinal);
            var terms = new JArray();
            if (string.Equals(
                    kind,
                    "CustomMultiLobe",
                    StringComparison.Ordinal) ||
                string.Equals(
                    kind,
                    "TransparentLit",
                    StringComparison.Ordinal))
            {
                terms.Add(Term(
                    "diffuse",
                    "Diffuse",
                    "SurfaceScattering",
                    0.65f,
                    new JObject
                    {
                        ["Color"] = ConstantParameter(
                            new JArray(0.7f, 0.2f, 0.1f, 1f),
                            "Color"),
                        ["Roughness"] = ConstantParameter(
                            new JValue(0.45f),
                            "Float"),
                    }));
                terms.Add(Term(
                    "glossy",
                    "Glossy",
                    "SurfaceScattering",
                    0.35f,
                    new JObject
                    {
                        ["Color"] = ConstantParameter(
                            new JArray(0.8f, 0.9f, 1f, 1f),
                            "Color"),
                        ["Roughness"] = ConstantParameter(
                            new JValue(0.12f),
                            "Float"),
                    }));
            }
            if (string.Equals(
                    kind,
                    "TransparentEmission",
                    StringComparison.Ordinal))
            {
                terms.Add(Term(
                    "emission",
                    "Emission",
                    "Emission",
                    0.4f,
                    new JObject
                    {
                        ["Color"] = ConstantParameter(
                            new JArray(0.2f, 0.7f, 1f, 1f),
                            "Color"),
                        ["Strength"] = ConstantParameter(
                            new JValue(3f),
                            "Float"),
                    }));
            }
            if (kind.StartsWith(
                    "Transparent",
                    StringComparison.Ordinal))
            {
                terms.Add(Term(
                    "transparent",
                    "Transparent",
                    "TransparentPassThrough",
                    0.6f,
                    new JObject
                    {
                        ["Color"] = ConstantParameter(
                            new JArray(1f, 1f, 1f, 1f),
                            "Color"),
                    }));
            }
            var renderState = new JObject
            {
                ["surfaceType"] = transparent
                    ? "Transparent"
                    : "Opaque",
                ["blendMode"] = transparent
                    ? "Premultiply"
                    : "Off",
                ["alphaClip"] = string.Equals(
                    kind,
                    "CutoutPBR",
                    StringComparison.Ordinal),
            };
            return new JObject
            {
                ["documentKind"] = "miku-material-ir-1.0",
                ["schemaVersion"] = "1.0",
                ["workflow"] = new JObject { ["kind"] = "standard_pbr" },
                ["channels"] = (JArray)SurfaceIr(
                    "DielectricScreenRefraction",
                    transparent ? "AlphaBlend" : "Opaque")["channels"],
                ["expressions"] = new JArray(),
                ["weightedClosures"] = new JObject
                {
                    ["schema"] = "miku-weighted-closures-1.0",
                    ["terms"] = terms,
                },
                ["surfaceModelPlan"] = new JObject
                {
                    ["schema"] = "miku-surface-model-plan-1.0",
                    ["kind"] = kind,
                    ["renderStatePlan"] = renderState,
                    ["shaderRequirements"] = new JObject
                    {
                        ["requiresOpaqueTexture"] = false,
                    },
                    ["transparentCompositePlan"] = transparent
                        ? new JObject
                        {
                            ["transmittanceKind"] = "Scalar",
                            ["premultiplyCount"] = 1,
                        }
                        : null,
                },
            };
        }

        static JObject ClearCoatSurfaceModelIr2()
        {
            var ir = SurfaceModelIr2("OpaquePBR");
            ((JArray)ir["weightedClosures"]["terms"]).Add(
                new JObject
                {
                    ["id"] = "principled",
                    ["closureId"] = "closure-principled",
                    ["closureKind"] = "Principled",
                    ["domain"] = "SurfaceScattering",
                    ["parameters"] = new JObject
                    {
                        ["Coat Weight"] = new JObject
                        {
                            ["kind"] = "Constant",
                            ["valueType"] = "Float",
                            ["value"] = 0.25f,
                        },
                        ["Coat Roughness"] = new JObject
                        {
                            ["kind"] = "Constant",
                            ["valueType"] = "Float",
                            ["value"] = 0.03f,
                        },
                    },
                    ["distribution"] = "MULTI_GGX",
                    ["finalWeight"] = new JObject
                    {
                        ["id"] = "weight-principled",
                        ["kind"] = "Constant",
                        ["valueType"] = "Float",
                        ["value"] = 1f,
                    },
                    ["weightTrace"] = new JArray(),
                    ["source"] = new JObject
                    {
                        ["nodeId"] = "node-principled",
                        ["socketId"] = "Closure",
                        ["groupPath"] = new JArray(),
                    },
                });
            ir["surfaceModelPlan"]["approximations"] = new JArray
            {
                new JObject
                {
                    ["kind"] = "Urp17ClearCoat",
                    ["algorithm"] = "miku-urp17-clear-coat-1",
                },
            };
            return ir;
        }

        static JObject ClosureNormalIr2(
            string kind,
            string bindingKey)
        {
            const string resourceId = "resource-closure-baked";
            var ir = SurfaceModelIr2(kind);
            var expressions = new JArray
            {
                Expression(
                    "closure-baked",
                    "Texture.SampleBaked2D",
                    "Scalar",
                    "Fragment",
                    new JObject
                    {
                        ["resourceId"] = resourceId,
                        ["referenceName"] = bindingKey,
                        ["usage"] = "Scalar",
                        ["channel"] = "R",
                        ["colorSpace"] = "Linear",
                        ["uvSet"] = "UV0",
                    }),
                Expression(
                    "normal-midlevel",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.5f }),
                Expression(
                    "normal-strength",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 1f }),
                Expression(
                    "closure-normal",
                    "Vector.NormalFromHeight",
                    "Float3",
                    "Fragment",
                    inputs: new JObject
                    {
                        ["Height"] = new JObject
                        {
                            ["expressionId"] = "closure-baked",
                        },
                        ["Midlevel"] = new JObject
                        {
                            ["expressionId"] = "normal-midlevel",
                        },
                        ["Strength"] = new JObject
                        {
                            ["expressionId"] = "normal-strength",
                        },
                    }),
                Expression(
                    "geometry-normal",
                    "Input.Normal",
                    "Float3",
                    "Fragment"),
                Expression(
                    "view-direction",
                    "Input.ViewDirection",
                    "Float3",
                    "Fragment"),
                Expression(
                    "layer-blend",
                    "Constant",
                    "Scalar",
                    "Both",
                    new JObject { ["value"] = 0.5f }),
                Expression(
                    "layer-facing",
                    "Math.LayerWeightFacing",
                    "Scalar",
                    "Fragment",
                    inputs: new JObject
                    {
                        ["Normal"] = new JObject
                        {
                            ["expressionId"] = "geometry-normal",
                        },
                        ["ViewDirection"] = new JObject
                        {
                            ["expressionId"] = "view-direction",
                        },
                        ["Blend"] = new JObject
                        {
                            ["expressionId"] = "layer-blend",
                        },
                    }),
            };
            ((JObject)expressions
                .OfType<JObject>()
                .Single(item => string.Equals(
                    item["id"]?.Value<string>(),
                    "closure-normal",
                    StringComparison.Ordinal)))["space"] = "Tangent";
            ir["expressions"] = expressions;
            ir["resources"] = new JArray
            {
                new JObject
                {
                    ["id"] = resourceId,
                    ["bindingKey"] = bindingKey,
                },
            };
            var channels = (JArray)ir["channels"];
            var normal = channels
                .OfType<JObject>()
                .Single(channel => string.Equals(
                    channel["semantic"]?.Value<string>(),
                    "Normal",
                    StringComparison.Ordinal));
            normal["valueType"] = "Float3";
            normal["value"] = new JObject
            {
                ["kind"] = "Expression",
                ["expressionId"] = "closure-normal",
            };
            var firstTerm = ((JArray)ir["weightedClosures"]["terms"])
                .OfType<JObject>()
                .First();
            foreach (var term in ((JArray)ir["weightedClosures"]["terms"])
                .OfType<JObject>()
                .Where(item => string.Equals(
                    item["domain"]?.Value<string>(),
                    "SurfaceScattering",
                    StringComparison.Ordinal)))
            {
                ((JObject)term["parameters"])["Normal"] = new JObject
                {
                    ["kind"] = "ValueExpression",
                    ["valueType"] = "Float3",
                    ["expressionId"] = "closure-normal",
                    ["source"] = new JObject
                    {
                        ["nodeId"] = "normal-source",
                        ["socketId"] = "Normal",
                    },
                };
            }
            firstTerm["finalWeight"] = new JObject
            {
                ["id"] = "weight-runtime-expression",
                ["kind"] = "Parameter",
                ["valueType"] = "Float",
                ["expressionId"] = "layer-facing",
            };
            return ir;
        }

        static JObject BakedClosureWeightIr2(string bindingKey)
        {
            const string resourceId = "resource-closure-weight";
            var ir = SurfaceModelIr2("TransparentEmission");
            ir["expressions"] = new JArray
            {
                Expression(
                    "baked-closure-weight",
                    "Texture.SampleBaked2D",
                    "Scalar",
                    "Fragment",
                    new JObject
                    {
                        ["resourceId"] = resourceId,
                        ["referenceName"] = bindingKey,
                        ["usage"] = "Scalar",
                        ["channel"] = "R",
                        ["colorSpace"] = "Linear",
                        ["uvSet"] = "UV0",
                    }),
            };
            ir["resources"] = new JArray
            {
                new JObject
                {
                    ["id"] = resourceId,
                    ["bindingKey"] = bindingKey,
                },
            };
            var firstTerm = ((JArray)ir["weightedClosures"]["terms"])
                .OfType<JObject>()
                .First();
            firstTerm["finalWeight"] = new JObject
            {
                ["id"] = "weight-baked-expression",
                ["kind"] = "Parameter",
                ["valueType"] = "Float",
                ["expressionId"] = "baked-closure-weight",
            };
            return ir;
        }

        static string GenerateRuntimeSubGraph(JObject ir, string materialId)
        {
            var type = typeof(MikuBundleImporter).Assembly.GetType(
                "Miku.ShaderConverter.Editor.MikuShaderGraph17RuntimeBackend",
                true);
            var method = type.GetMethod(
                "Generate",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(type.FullName, "Generate");
            return (string)method.Invoke(null, new object[] { ir, materialId });
        }

        static List<JObject> ParseMultiJson(string value)
        {
            var objects = new List<JObject>();
            using var textReader = new StringReader(value);
            using var jsonReader = new JsonTextReader(textReader)
            {
                SupportMultipleContent = true,
            };
            while (jsonReader.Read())
            {
                if (jsonReader.TokenType == JsonToken.StartObject)
                    objects.Add(JObject.Load(jsonReader));
            }
            return objects;
        }

        static void AssertDirectWrapperEdge(
            string wrapperText,
            string outputName,
            string blockName)
        {
            Assert.That(
                HasDirectWrapperEdge(wrapperText, outputName, blockName),
                Is.True,
                outputName + " must feed " + blockName + " directly.");
        }

        static void AssertNoDirectWrapperEdge(
            string wrapperText,
            string outputName,
            string blockName)
        {
            Assert.That(
                HasDirectWrapperEdge(wrapperText, outputName, blockName),
                Is.False,
                outputName + " must not feed " + blockName + " directly.");
        }

        static bool HasDirectWrapperEdge(
            string wrapperText,
            string outputName,
            string blockName)
        {
            var objects = ParseMultiJson(wrapperText);
            var graph = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                    .EndsWith(".GraphData", StringComparison.Ordinal));
            var byId = objects
                .Where(item => item["m_ObjectId"] != null)
                .ToDictionary(
                    item => item["m_ObjectId"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            var subGraph = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                    .EndsWith(".SubGraphNode", StringComparison.Ordinal));
            var outputSlot = (subGraph["m_Slots"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(item => byId[
                    item["m_Id"]?.Value<string>() ?? ""])
                .Single(slot =>
                    slot["m_SlotType"]?.Value<int>() == 1 &&
                    string.Equals(
                        slot["m_DisplayName"]?.Value<string>(),
                        outputName,
                        StringComparison.Ordinal));
            var block = objects.Single(item =>
                string.Equals(
                    item["m_Name"]?.Value<string>(),
                    blockName,
                    StringComparison.Ordinal));
            return (graph["m_Edges"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Any(edge =>
                    string.Equals(
                        edge["m_OutputSlot"]?["m_Node"]?["m_Id"]
                            ?.Value<string>(),
                        subGraph["m_ObjectId"]?.Value<string>(),
                        StringComparison.Ordinal) &&
                    edge["m_OutputSlot"]?["m_SlotId"]?.Value<int>() ==
                        outputSlot["m_Id"]?.Value<int>() &&
                    string.Equals(
                        edge["m_InputSlot"]?["m_Node"]?["m_Id"]
                            ?.Value<string>(),
                        block["m_ObjectId"]?.Value<string>(),
                        StringComparison.Ordinal) &&
                    edge["m_InputSlot"]?["m_SlotId"]?.Value<int>() == 0);
        }

        static Color MeanColor(
            Texture2D image,
            int centerX,
            int centerY,
            int radius)
        {
            var sum = Color.black;
            var count = 0;
            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                for (var x = centerX - radius; x <= centerX + radius; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    if (dx * dx + dy * dy > radius * radius)
                        continue;
                    sum += image.GetPixel(x, y);
                    count++;
                }
            }
            return sum / Math.Max(count, 1);
        }

        static float MaximumRingColorDistance(
            Texture2D image,
            Color center,
            int centerX,
            int centerY,
            int innerRadius,
            int outerRadius)
        {
            var maximum = 0.0f;
            for (var y = centerY - outerRadius;
                 y <= centerY + outerRadius;
                 y++)
            {
                for (var x = centerX - outerRadius;
                     x <= centerX + outerRadius;
                     x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    var distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared < innerRadius * innerRadius ||
                        distanceSquared > outerRadius * outerRadius)
                        continue;
                    var pixel = image.GetPixel(x, y);
                    var luminance =
                        0.2126f * pixel.r +
                        0.7152f * pixel.g +
                        0.0722f * pixel.b;
                    if (luminance <= 0.02f)
                        continue;
                    var dr = pixel.r - center.r;
                    var dg = pixel.g - center.g;
                    var db = pixel.b - center.b;
                    maximum = Math.Max(
                        maximum,
                        (float)Math.Sqrt(dr * dr + dg * dg + db * db));
                }
            }
            return maximum;
        }

        static string GenerateRuntimeWrapper(
            string generatedSubGraph,
            string materialId,
            string subGraphGuid)
        {
            var type = typeof(MikuBundleImporter).Assembly.GetType(
                "Miku.ShaderConverter.Editor.MikuShaderGraph17RuntimeBackend",
                true);
            var method = type.GetMethod(
                "GenerateWrapper",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic)
                ?? throw new MissingMethodException(
                    type.FullName,
                    "GenerateWrapper");
            var templatePath = ToAbsolute(
                "Packages/com.miku.shaderconverter/Templates/" +
                "MikuStandardTemplate.shadergraph");
            return (string)method.Invoke(
                null,
                new object[]
                {
                    File.ReadAllText(templatePath, Encoding.UTF8),
                    generatedSubGraph,
                    materialId,
                    subGraphGuid,
                    null,
                });
        }

        void WriteIdentityDocument(
            string materialRoot,
            string sourceId,
            string materialId)
        {
            var absoluteRoot = ToAbsolute(materialRoot);
            Directory.CreateDirectory(absoluteRoot);
            var identity = new JObject
            {
                ["schema"] = "miku-generated-asset-identity-1.0",
                ["persistentSourceId"] = sourceId,
                ["persistentMaterialId"] = materialId,
                ["assets"] = new JArray(),
            };
            File.WriteAllText(
                Path.Combine(absoluteRoot, "Fixture.miku-assets.json"),
                identity.ToString(Formatting.Indented),
                new UTF8Encoding(false));
        }

        static JObject ConstantChannel(string semantic, JToken value)
        {
            return new JObject
            {
                ["semantic"] = semantic,
                ["valueType"] = value is JArray ? "Color" : "Scalar",
                ["required"] = true,
                ["value"] = new JObject
                {
                    ["kind"] = "Constant",
                    ["value"] = value,
                },
            };
        }

        string WriteValidBundle(
            bool includeResource = false,
            string sourceId = "source-fixture",
            string materialId = "material-fixture",
            string sourceName = "Fixture",
            string workflowKind = "standard_pbr",
            string targetProfileHash = CurrentProfileHash,
            JArray explicitChannels = null,
            JArray explicitExpressions = null,
            JObject explicitSurfaceContract = null,
            JObject explicitMaterialIrV2 = null,
            string resourceBindingKey = null,
            string resourceSemantic = "BaseColor",
            string resourceUsage = null,
            string toolVersion = "2.1.0",
            bool includeSourceMesh = false,
            string resourceMediaType = "image/png",
            string resourceExtension = ".png",
            string resourceColorSpace = "sRGB",
            string resourceChannel = "RGBA",
            string resourceNormalConvention = null,
            JArray resourceChannelBindings = null,
            string bundleKind = null)
        {
            var resources = new JArray();
            var artifacts = new JArray();
            var channels = explicitChannels ?? new JArray();
            if (includeResource)
            {
                var texture = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
                texture.SetPixels(new[]
                {
                    UnityEngine.Color.red,
                    UnityEngine.Color.green,
                    UnityEngine.Color.blue,
                    UnityEngine.Color.white,
                });
                texture.Apply();
                var bytes = string.Equals(
                    resourceMediaType,
                    "image/jpeg",
                    StringComparison.Ordinal)
                    ? texture.EncodeToJPG()
                    : texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);
                var resourceName =
                    string.Equals(
                        resourceSemantic,
                        "BaseColor",
                        StringComparison.Ordinal)
                        ? "BaseColor"
                        : resourceSemantic;
                var resourcePath = Path.Combine(
                    bundleRoot,
                    "Baked",
                    resourceName + resourceExtension);
                Directory.CreateDirectory(Path.GetDirectoryName(resourcePath));
                File.WriteAllBytes(resourcePath, bytes);
                var resource = new JObject
                {
                    ["id"] = "resource-base-color",
                    ["relativePath"] =
                        "Baked/" + resourceName + resourceExtension,
                    ["sha256"] = Sha256File(resourcePath),
                    ["byteLength"] = bytes.Length,
                    ["mediaType"] = resourceMediaType,
                    ["semantic"] = resourceSemantic,
                    ["channel"] = resourceChannel,
                    ["colorSpace"] = resourceColorSpace,
                    ["width"] = 2,
                    ["height"] = 2,
                    ["channelCount"] = 4,
                    ["componentBytes"] = 1,
                };
                if (!string.IsNullOrEmpty(resourceBindingKey))
                    resource["bindingKey"] = resourceBindingKey;
                if (!string.IsNullOrEmpty(resourceUsage))
                    resource["usage"] = resourceUsage;
                if (!string.IsNullOrEmpty(resourceNormalConvention))
                    resource["normalConvention"] =
                        resourceNormalConvention;
                if (resourceChannelBindings != null)
                    resource["channelBindings"] =
                        resourceChannelBindings.DeepClone();
                resources.Add(resource);
                artifacts.Add(new JObject
                {
                    ["id"] = resource["id"],
                    ["relativePath"] = resource["relativePath"],
                    ["sha256"] = resource["sha256"],
                    ["byteLength"] = resource["byteLength"],
                });
                channels.Add(new JObject
                {
                    ["semantic"] = "BaseColor",
                    ["valueType"] = "Color",
                    ["required"] = true,
                    ["value"] = new JObject
                    {
                        ["kind"] = "TextureResource",
                        ["resourceId"] = resource["id"],
                    },
                });
            }
            if (includeSourceMesh)
            {
                var bytes = MinimalSourceMeshGlb();
                var relativePath = "SourceMesh/fixture.glb";
                var resourcePath = Path.Combine(
                    bundleRoot,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar));
                Directory.CreateDirectory(
                    Path.GetDirectoryName(resourcePath));
                File.WriteAllBytes(resourcePath, bytes);
                var meshFingerprint = new string('b', 64);
                var bindingFingerprint = new string('a', 64);
                var resource = new JObject
                {
                    ["id"] = "resource-source-mesh",
                    ["kind"] = "SourceMesh",
                    ["semantic"] = "SourceMesh",
                    ["relativePath"] = relativePath,
                    ["sha256"] = Sha256File(resourcePath),
                    ["byteLength"] = bytes.Length,
                    ["mediaType"] = "model/gltf-binary",
                    ["meshBinding"] = new JObject
                    {
                        ["kind"] = "MeshFingerprintSet",
                        ["sha256"] = bindingFingerprint,
                        ["meshes"] = new JArray
                        {
                            new JObject
                            {
                                ["object"] = "SourceSphere",
                                ["sha256"] = meshFingerprint,
                                ["vertices"] = 3,
                                ["polygons"] = 1,
                                ["indices"] = 3,
                                ["uv"] = "UVMap",
                                ["uvCount"] = 3,
                                ["materialSlots"] =
                                    new JArray("Material"),
                            },
                        },
                        ["coordinateConvention"] =
                            "BlenderObjectToUnityObject",
                        ["normalConvention"] =
                            "TangentOpenGLPositiveY",
                    },
                    ["rendererBindings"] = new JArray
                    {
                        new JObject
                        {
                            ["rendererPath"] = "SourceSphere",
                            ["sourceObject"] = "SourceSphere",
                            ["meshIndex"] = 0,
                            ["materialSlots"] = new JArray(0),
                            ["meshFingerprint"] = meshFingerprint,
                            ["sourceVertices"] = 3,
                            ["sourcePolygons"] = 1,
                            ["sourceUv"] = "UVMap",
                            ["exportedVertices"] = 3,
                            ["exportedIndices"] = 3,
                            ["hasUv0"] = true,
                        },
                    },
                    ["meshCount"] = 1,
                    ["vertexCount"] = 3,
                    ["indexCount"] = 3,
                    ["hasUv0"] = true,
                };
                resources.Add(resource);
                artifacts.Add(new JObject
                {
                    ["id"] = resource["id"],
                    ["relativePath"] = resource["relativePath"],
                    ["sha256"] = resource["sha256"],
                    ["byteLength"] = resource["byteLength"],
                });
            }
            var v2 = explicitMaterialIrV2 != null;
            var irPayload = explicitMaterialIrV2 != null
                ? (JObject)explicitMaterialIrV2.DeepClone()
                : new JObject
                {
                    ["workflow"] = new JObject { ["kind"] = workflowKind },
                    ["channels"] = channels,
                    ["expressions"] =
                        explicitExpressions ?? new JArray(),
                    ["parameters"] = new JArray(),
                };
            irPayload.Remove("documentKind");
            irPayload.Remove("schemaVersion");
            irPayload.Remove("toolVersion");
            irPayload.Remove("id");
            irPayload.Remove("canonicalHash");
            if (!v2 && explicitSurfaceContract != null)
                irPayload["surfaceContract"] = explicitSurfaceContract;
            var documents = new[]
            {
                (
                    "ir",
                    v2
                        ? "miku-material-ir-1.0"
                        : "miku-material-ir-1.0",
                    irPayload),
                (
                    "plan",
                    v2
                        ? "miku-conversion-plan-1.0"
                        : "miku-conversion-plan-1.0",
                    new JObject()),
                ("sourceMap", "miku-blender-source-map-1.0", new JObject()),
                (
                    "manifest",
                    v2
                        ? "miku-conversion-manifest-1.0"
                        : "miku-conversion-manifest-1.0",
                    new JObject
                {
                    ["persistentSourceId"] = sourceId,
                    ["persistentMaterialId"] = materialId,
                    ["targetProfileHash"] = targetProfileHash,
                    ["bakeJobs"] = new JArray(),
                    ["completion"] = new JObject
                    {
                        ["status"] = "completed",
                        ["exitCode"] = 0,
                        ["marker"] = "MIKU_CONVERSION_COMPLETE",
                        ["artifacts"] = artifacts,
                    },
                }),
            };
            var references = new JObject();
            foreach (var item in documents)
            {
                var document = NewDocument(
                    item.Item2,
                    item.Item3,
                    toolVersion);
                var path = Path.Combine(bundleRoot, "material." + item.Item1 + ".json");
                File.WriteAllText(path, document.ToString(Formatting.None), new UTF8Encoding(false));
                references[item.Item1] = new JObject
                {
                    ["relativePath"] = Path.GetFileName(path),
                    ["sha256"] = Sha256File(path),
                    ["byteLength"] = new FileInfo(path).Length,
                    ["mediaType"] = "application/json",
                };
            }
            ((JObject)references["sourceMap"])["editorOnly"] = true;
            var bundle = new JObject
            {
                ["materialKey"] = sourceName,
                ["sourceName"] = sourceName,
                ["persistentSourceId"] = sourceId,
                ["persistentMaterialId"] = materialId,
                ["targetProfileHash"] = targetProfileHash,
                ["ir"] = references["ir"],
                ["plan"] = references["plan"],
                ["manifest"] = references["manifest"],
                ["sourceMap"] = references["sourceMap"],
                ["resources"] = resources,
            };
            bundle["sealedDigest"] = Invoke<string>("ComputeSealedDigest", bundle);
            bundle = NewDocument(
                bundleKind ??
                (includeSourceMesh
                    ? "miku-bundle-1.0"
                    : v2 ? "miku-bundle-1.0" : "miku-bundle-1.0"),
                bundle,
                toolVersion);
            var bundlePath = Path.Combine(bundleRoot, sourceName + ".mikubundle");
            File.WriteAllText(bundlePath, bundle.ToString(Formatting.None), new UTF8Encoding(false));
            return bundlePath;
        }

        string WriteStandardPbrTextureBundle()
        {
            var bundlePath = WriteValidBundle(
                includeResource: true,
                sourceName: "StandardPbrTextures");
            var bundle = JObject.Parse(File.ReadAllText(bundlePath));
            var irPath = Path.Combine(
                bundleRoot,
                (string)bundle["ir"]["relativePath"]);
            var manifestPath = Path.Combine(
                bundleRoot,
                (string)bundle["manifest"]["relativePath"]);
            var ir = JObject.Parse(File.ReadAllText(irPath));
            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            var resources = (JArray)bundle["resources"];
            var channels = (JArray)ir["channels"];
            var artifacts = (JArray)manifest["completion"]["artifacts"];
            foreach (var semantic in new[]
            {
                "Metalness",
                "Roughness",
                "Normal",
                "Emission",
            })
            {
                var bytes = FixturePngBytes(semantic);
                var id = "resource-" + semantic.ToLowerInvariant();
                var relativePath = "Baked/" + semantic + ".png";
                var absolutePath = Path.Combine(
                    bundleRoot,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
                File.WriteAllBytes(absolutePath, bytes);
                var colorData = semantic == "Emission";
                var vectorData = semantic == "Normal";
                var resource = new JObject
                {
                    ["id"] = id,
                    ["relativePath"] = relativePath,
                    ["sha256"] = Sha256File(absolutePath),
                    ["byteLength"] = bytes.Length,
                    ["mediaType"] = "image/png",
                    ["semantic"] = semantic,
                    ["bindingKey"] = semantic,
                    ["channel"] = vectorData || colorData ? "RGB" : "R",
                    ["colorSpace"] = colorData ? "sRGB" : "Linear",
                    ["width"] = 2,
                    ["height"] = 2,
                    ["channelCount"] = 4,
                    ["componentBytes"] = 1,
                };
                if (vectorData)
                    resource["normalConvention"] = "TangentOpenGLPositiveY";
                resources.Add(resource);
                artifacts.Add(new JObject
                {
                    ["id"] = id,
                    ["relativePath"] = relativePath,
                    ["sha256"] = resource["sha256"],
                    ["byteLength"] = bytes.Length,
                });
                channels.Add(new JObject
                {
                    ["semantic"] = semantic,
                    ["valueType"] = vectorData ? "Float3" : colorData ? "Color" : "Scalar",
                    ["required"] = true,
                    ["value"] = new JObject
                    {
                        ["kind"] = "TextureResource",
                        ["resourceId"] = id,
                    },
                });
            }

            var irDocument = NewDocument(
                "miku-material-ir-1.0",
                ir,
                "2.1.0");
            File.WriteAllText(
                irPath,
                irDocument.ToString(Formatting.None),
                new UTF8Encoding(false));
            var manifestDocument = NewDocument(
                "miku-conversion-manifest-1.0",
                manifest,
                "2.1.0");
            File.WriteAllText(
                manifestPath,
                manifestDocument.ToString(Formatting.None),
                new UTF8Encoding(false));
            bundle["ir"] = new JObject
            {
                ["relativePath"] = Path.GetFileName(irPath),
                ["sha256"] = Sha256File(irPath),
                ["byteLength"] = new FileInfo(irPath).Length,
                ["mediaType"] = "application/json",
            };
            bundle["manifest"] = new JObject
            {
                ["relativePath"] = Path.GetFileName(manifestPath),
                ["sha256"] = Sha256File(manifestPath),
                ["byteLength"] = new FileInfo(manifestPath).Length,
                ["mediaType"] = "application/json",
            };
            bundle["sealedDigest"] = Invoke<string>(
                "ComputeSealedDigest",
                bundle);
            var finalBundle = NewDocument(
                "miku-bundle-1.0",
                bundle,
                "2.1.0");
            File.WriteAllText(
                bundlePath,
                finalBundle.ToString(Formatting.None),
                new UTF8Encoding(false));
            return bundlePath;
        }

        static byte[] FixturePngBytes(string semantic)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var color = semantic == "Metalness"
                ? Color.gray
                : semantic == "Roughness"
                    ? new Color(0.35f, 0.35f, 0.35f, 1.0f)
                    : semantic == "Normal"
                        ? new Color(0.5f, 0.5f, 1.0f, 1.0f)
                        : semantic == "Emission"
                            ? new Color(1.0f, 0.25f, 0.05f, 1.0f)
                            : Color.white;
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            var bytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return bytes;
        }

        static byte[] MinimalSourceMeshGlb()
        {
            return Convert.FromBase64String(
                "Z2xURgIAAAAABAAAfAMAAEpTT057ImFzc2V0Ijp7InZlcnNpb24iOiIyLjAiLCJnZW5lcmF0b3IiOiJNaUdSIHRlc3QifSwic2NlbmUiOjAsInNjZW5lcyI6W3sibm9kZXMiOlswXX1dLCJub2RlcyI6W3sibmFtZSI6IlNvdXJjZVNwaGVyZSIsIm1lc2giOjB9XSwibWVzaGVzIjpbeyJuYW1lIjoiU291cmNlU3BoZXJlIiwicHJpbWl0aXZlcyI6W3siYXR0cmlidXRlcyI6eyJQT1NJVElPTiI6MCwiTk9STUFMIjoxLCJURVhDT09SRF8wIjoyfSwiaW5kaWNlcyI6MywibWF0ZXJpYWwiOjB9XX1dLCJtYXRlcmlhbHMiOlt7Im5hbWUiOiJNYXRlcmlhbCJ9XSwiYnVmZmVycyI6W3siYnl0ZUxlbmd0aCI6MTA0fV0sImJ1ZmZlclZpZXdzIjpbeyJidWZmZXIiOjAsImJ5dGVPZmZzZXQiOjAsImJ5dGVMZW5ndGgiOjM2LCJ0YXJnZXQiOjM0OTYyfSx7ImJ1ZmZlciI6MCwiYnl0ZU9mZnNldCI6MzYsImJ5dGVMZW5ndGgiOjM2LCJ0YXJnZXQiOjM0OTYyfSx7ImJ1ZmZlciI6MCwiYnl0ZU9mZnNldCI6NzIsImJ5dGVMZW5ndGgiOjI0LCJ0YXJnZXQiOjM0OTYyfSx7ImJ1ZmZlciI6MCwiYnl0ZU9mZnNldCI6OTYsImJ5dGVMZW5ndGgiOjYsInRhcmdldCI6MzQ5NjN9XSwiYWNjZXNzb3JzIjpbeyJidWZmZXJWaWV3IjowLCJjb21wb25lbnRUeXBlIjo1MTI2LCJjb3VudCI6MywidHlwZSI6IlZFQzMiLCJtaW4iOlswLDAsMF0sIm1heCI6WzEsMSwwXX0seyJidWZmZXJWaWV3IjoxLCJjb21wb25lbnRUeXBlIjo1MTI2LCJjb3VudCI6MywidHlwZSI6IlZFQzMifSx7ImJ1ZmZlclZpZXciOjIsImNvbXBvbmVudFR5cGUiOjUxMjYsImNvdW50IjozLCJ0eXBlIjoiVkVDMiJ9LHsiYnVmZmVyVmlldyI6MywiY29tcG9uZW50VHlwZSI6NTEyMywiY291bnQiOjMsInR5cGUiOiJTQ0FMQVIiLCJtaW4iOlswXSwibWF4IjpbMl19XX0gaAAAAEJJTgAAAAAAAAAAAAAAAAAAAIA/AAAAAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAgD8AAAAAAAAAAAAAgD8AAAAAAAAAAAAAgD8AAAAAAAAAAAAAgD8AAAAAAAAAAAAAgD8AAAEAAgAAAA==");
        }

        static JObject NewDocument(
            string kind,
            JObject payload,
            string toolVersion)
        {
            var document = (JObject)payload.DeepClone();
            var v2 = kind.EndsWith("-2.0", StringComparison.Ordinal) ||
                kind.EndsWith("-2.1", StringComparison.Ordinal) ||
                kind.EndsWith("-2.2", StringComparison.Ordinal);
            document["documentKind"] = kind;
            document["schemaVersion"] = kind.EndsWith(
                "-2.2",
                StringComparison.Ordinal)
                ? "2.2"
                : kind.EndsWith(
                "-2.1",
                StringComparison.Ordinal)
                ? "2.1"
                : v2 ? "2.0" : "1.0";
            document["toolVersion"] = v2 ? toolVersion : "1.2.1";
            document["id"] = kind + "-fixture";
            // Match a bundle read from disk: Newtonsoft serializes Single
            // values using short decimal text and reads them back as Double.
            var normalized = JObject.Parse(
                document.ToString(Formatting.None));
            normalized["canonicalHash"] = Invoke<string>(
                "CanonicalHash",
                normalized,
                "canonicalHash");
            return normalized;
        }

        static T Invoke<T>(string method, params object[] args)
        {
            var target = typeof(MikuBundleImporter).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(method);
            return (T)target.Invoke(null, args);
        }

        static void InvokeVoid(string method, params object[] args)
        {
            var target = typeof(MikuBundleImporter).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(method);
            target.Invoke(null, args);
        }

        static string ToAbsolute(string assetPath)
        {
            return Path.Combine(
                Directory.GetParent(UnityEngine.Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static string Sha256File(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(item => item.ToString("x2")));
        }
    }
}
