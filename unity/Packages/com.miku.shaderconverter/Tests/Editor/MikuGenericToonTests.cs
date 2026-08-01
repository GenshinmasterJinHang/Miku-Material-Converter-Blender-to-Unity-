// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Miku.ShaderConverter.Editor;
using Miku.ShaderConverter.Runtime.GenericToon;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Miku.ShaderConverter.Editor.Tests
{
    public sealed class MikuGenericToonTests
    {
        static readonly string[] Semantics =
        {
            "Face",
            "BodySkin",
            "Hair",
            "Eye",
            "Mouth",
            "Cloth",
            "MetalAccessory",
            "GenericOpaque",
        };

        static readonly string[] Passes =
        {
            "UniversalForwardOnly",
            "ShadowCaster",
            "DepthOnly",
            "DepthNormalsOnly",
            "MotionVectors",
            "MikuToonOutline",
            "MikuToonCharacterMask",
        };

        [Test]
        public void TopLevelMenusOpenDedicatedWindowsAndLegacyMenusAreAbsent()
        {
            var previousSelection = Selection.activeObject;
            var previousIgnoreFailingMessages =
                LogAssert.ignoreFailingMessages;
            var mesh = new Mesh { name = "ExplicitSelectedMesh" };
            Selection.activeObject = mesh;
            // Batchmode has no graphics device, but the menu contract still
            // requires the real EditorWindow entry points to be exercised.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                Assert.That(
                    EditorApplication.ExecuteMenuItem(
                        "Miku/Generic Toon/Material Builder"),
                    Is.True);
                Assert.That(
                    EditorApplication.ExecuteMenuItem(
                        "Miku/Generic Toon/Mesh/Smooth Normal Generator"),
                    Is.True);
                Assert.That(
                    EditorApplication.ExecuteMenuItem(
                        "Miku/Generic Toon/Mesh/Vertex Color Initializer"),
                    Is.True);
                Assert.That(
                    EditorApplication.ExecuteMenuItem(
                        "Miku/Generic Toon/Mesh/Combined Mesh Data"),
                    Is.True);
                Assert.That(
                    EditorApplication.ExecuteMenuItem(
                        "Miku/Generic Toon/Rendering/Screen Rim Installer"),
                    Is.True);

                var smooth = Resources
                    .FindObjectsOfTypeAll<
                        MikuSmoothNormalGeneratorWindow>()
                    .Single();
                var colors = Resources
                    .FindObjectsOfTypeAll<
                        MikuVertexColorInitializerWindow>()
                    .Single();
                var combined = Resources
                    .FindObjectsOfTypeAll<MikuToonMeshDataTool>()
                    .Single();
                Assert.That(smooth.SourceMesh, Is.SameAs(mesh));
                Assert.That(colors.SourceMesh, Is.SameAs(mesh));
                Assert.That(combined.SourceMesh, Is.SameAs(mesh));

                Assert.That(
                    EditorApplication.ExecuteMenuItem(
                        "Window/Miku/Generic Toon Material Builder"),
                    Is.False);
                Assert.That(
                    EditorApplication.ExecuteMenuItem(
                        "Window/Miku/Generic Toon Mesh Data"),
                    Is.False);
                Assert.That(
                    EditorApplication.ExecuteMenuItem(
                        "Window/Miku/Generic Toon Screen Rim Installer"),
                    Is.False);
                Assert.That(
                    EditorApplication.ExecuteMenuItem(
                        "Miku/Import Metal Corpus (73 materials)"),
                    Is.False);
            }
            finally
            {
                foreach (var window in Resources
                             .FindObjectsOfTypeAll<EditorWindow>()
                             .Where(item =>
                                 item is MikuGenericToonMaterialBuilder ||
                                 item is MikuToonMeshToolWindow ||
                                 item is MikuToonRendererFeatureInstaller))
                    window.Close();
                Selection.activeObject = previousSelection;
                UnityEngine.Object.DestroyImmediate(mesh);
                LogAssert.ignoreFailingMessages =
                    previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void FixedSemanticShadersExposeOneStablePropertyAndPassContract()
        {
            var screenRim = Shader.Find(
                "Hidden/Miku/GenericToon/ScreenRimComposite");
            Assert.That(screenRim, Is.Not.Null);
            Assert.That(
                ShaderUtil.ShaderHasError(screenRim),
                Is.False,
                string.Join(
                    " | ",
                    ShaderUtil.GetShaderMessages(screenRim)
                        .Select(item => item.message)));

            string[] expectedProperties = null;
            foreach (var semantic in Semantics)
            {
                var shader = Shader.Find("Miku/GenericToon/" + semantic);
                Assert.That(shader, Is.Not.Null, semantic);
                var propertyNames = Enumerable
                    .Range(0, ShaderUtil.GetPropertyCount(shader))
                    .Select(index =>
                        ShaderUtil.GetPropertyName(shader, index))
                    .ToArray();
                if (expectedProperties == null)
                    expectedProperties = propertyNames;
                else
                    Assert.That(
                        propertyNames,
                        Is.EqualTo(expectedProperties),
                        semantic + ": property contract");
                Assert.That(
                    ShaderUtil.ShaderHasError(shader),
                    Is.False,
                    semantic + ": shader compile");
                var material = new Material(shader);
                try
                {
                    foreach (var property in new[]
                             {
                                 "_MIKU_BaseMap",
                                 "_MIKU_BaseColor",
                                 "_MIKU_Cutoff",
                                 "_MIKU_OutlineWidth",
                                 "_MIKU_RimColor",
                                 "_MIKU_RimIntensity",
                                 "_MIKU_RimWidth",
                             })
                        Assert.That(
                            material.HasProperty(property),
                            Is.True,
                            semantic + ":" + property);
                    foreach (var pass in Passes)
                        Assert.That(
                            material.FindPass(pass),
                            Is.GreaterThanOrEqualTo(0),
                            semantic + ":" + pass);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            var packageInfo =
                UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                    "Packages/com.miku.shaderconverter/package.json");
            Assert.That(packageInfo, Is.Not.Null);
            var common = File.ReadAllText(Path.Combine(
                packageInfo.resolvedPath,
                "Runtime",
                "GenericToon",
                "MikuGenericToonCommon.hlsl"));
            Assert.That(
                common,
                Does.Contain("CBUFFER_START(UnityPerMaterial)"));
            foreach (var property in expectedProperties)
                Assert.That(
                    common,
                    Does.Contain(property),
                    "UnityPerMaterial declaration: " + property);
        }

        [Test]
        public void GenericToonBackendResolvesStaticGenericOpaqueShader()
        {
            var ir = new JObject
            {
                ["documentKind"] = "miku-material-ir-1.0",
                ["schemaVersion"] = "1.0",
                ["workflow"] = new JObject
                {
                    ["kind"] = "generic_toon",
                },
            };
            var backend = MikuWorkflowBackends.Resolve(ir);
            Assert.That(backend.UsesEditableGraph, Is.False);
            Assert.That(backend.ResolveShader(ir, null).name,
                Is.EqualTo("Miku/GenericToon/GenericOpaque"));
        }

        [Test]
        public void RecipeRebuildIsAThreeWayMerge()
        {
            var sourceShader =
                Shader.Find("Universal Render Pipeline/Lit");
            var targetShader =
                Shader.Find("Miku/GenericToon/GenericOpaque");
            Assert.That(sourceShader, Is.Not.Null);
            Assert.That(targetShader, Is.Not.Null);
            var source = new Material(sourceShader);
            var target = new Material(targetShader);
            var originalTexture = new Texture2D(1, 1);
            var updatedTexture = new Texture2D(1, 1);
            var recipe = ScriptableObject.CreateInstance<
                MikuToonMaterialRecipe>();
            try
            {
                source.SetTexture("_BaseMap", originalTexture);
                source.SetColor("_BaseColor", Color.white);
                if (source.HasProperty("_Color"))
                    source.SetColor("_Color", Color.white);
                target.SetTexture(
                    MikuToonRecipeUtility.BaseMap,
                    originalTexture);
                target.SetColor(
                    MikuToonRecipeUtility.BaseColor,
                    Color.red);
                recipe.sourceMaterial = source;
                recipe.userMaterial = target;
                recipe.albedoMode = MikuToonAlbedoMode.Auto;
                recipe.lastSyncedTexture = originalTexture;
                recipe.lastSyncedColor = Color.white;

                source.SetTexture("_BaseMap", updatedTexture);
                source.SetColor("_BaseColor", Color.green);
                if (source.HasProperty("_Color"))
                    source.SetColor("_Color", Color.green);
                MikuToonRecipeUtility.Rebuild(recipe);

                Assert.That(
                    target.GetTexture(MikuToonRecipeUtility.BaseMap),
                    Is.SameAs(updatedTexture));
                Assert.That(
                    target.GetColor(MikuToonRecipeUtility.BaseColor),
                    Is.EqualTo(Color.red));
                Assert.That(
                    recipe.lastSyncedColor,
                    Is.EqualTo(Color.green));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recipe);
                UnityEngine.Object.DestroyImmediate(originalTexture);
                UnityEngine.Object.DestroyImmediate(updatedTexture);
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void MaterialBuilderCreatesOwnedBaseUserMaterialAndRecipe()
        {
            const string root = "Assets/MikuTests";
            const string sourcePath = root + "/BuilderSource.mat";
            if (!AssetDatabase.IsValidFolder(root))
                AssetDatabase.CreateFolder("Assets", "MikuTests");
            var sourceShader =
                Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(sourceShader, Is.Not.Null);
            var source = new Material(sourceShader)
            {
                name = "BuilderSource",
            };
            AssetDatabase.CreateAsset(source, sourcePath);
            var originalShader = source.shader;
            var result =
                MikuGenericToonMaterialBuilder.CreateAssets(
                    source,
                    MikuToonSemantic.BodySkin,
                    MikuToonAlbedoMode.Solid,
                    null,
                    Color.cyan,
                    root,
                    "BuilderOutput");
            try
            {
                Assert.That(
                    AssetDatabase.GetAssetPath(result.generatedBase),
                    Does.EndWith("BuilderOutput.generated.mat"));
                Assert.That(
                    AssetDatabase.GetAssetPath(result.userMaterial),
                    Does.EndWith("BuilderOutput.mat"));
                Assert.That(
                    AssetDatabase.GetAssetPath(result.recipe),
                    Does.EndWith("BuilderOutput.toon-recipe.asset"));
                Assert.That(
                    result.generatedBase,
                    Is.Not.SameAs(result.userMaterial));
                Assert.That(
                    AssetDatabase.GetAssetPath(
                        result.recipe.generatedBaseMaterial),
                    Is.EqualTo(
                        AssetDatabase.GetAssetPath(
                            result.generatedBase)));
                Assert.That(
                    AssetDatabase.GetAssetPath(
                        result.recipe.userMaterial),
                    Is.EqualTo(
                        AssetDatabase.GetAssetPath(
                            result.userMaterial)));
                Assert.That(result.recipe.stableGuid, Has.Length.EqualTo(32));
                Assert.That(
                    result.recipe.initialPreset.sssStrength,
                    Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(
                    result.generatedBase.shader.name,
                    Is.EqualTo("Miku/GenericToon/BodySkin"));
                Assert.That(
                    result.userMaterial.GetColor(
                        MikuToonRecipeUtility.BaseColor),
                    Is.EqualTo(Color.cyan));
                Assert.That(source.shader, Is.SameAs(originalShader));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [Test]
        public void SmoothNormalsAndVertexColorsAreDeterministicAndNonDestructive()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                    Vector3.zero,
                    Vector3.up,
                    Vector3.forward,
                },
                normals = new[]
                {
                    Vector3.back,
                    Vector3.back,
                    Vector3.back,
                    Vector3.right,
                    Vector3.right,
                    Vector3.right,
                },
                subMeshCount = 2,
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
            var originalTriangles0 = mesh.GetTriangles(0);
            var originalTriangles1 = mesh.GetTriangles(1);
            try
            {
                MikuToonMeshData.GenerateSmoothNormals(
                    mesh,
                    0.0001f,
                    180f,
                    false,
                    false);
                var uv7 = new List<Vector3>();
                mesh.GetUVs(7, uv7);
                Assert.That(uv7.Count, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.GetTriangles(0), Is.EqualTo(originalTriangles0));
                Assert.That(mesh.GetTriangles(1), Is.EqualTo(originalTriangles1));
                Assert.Throws<InvalidOperationException>(() =>
                    MikuToonMeshData.GenerateSmoothNormals(
                        mesh,
                        0.0001f,
                        180f,
                        false,
                        false));

                MikuToonMeshData.InitializeVertexColors(
                    mesh,
                    MikuVertexColorWriteMode.Replace);
                Assert.That(mesh.colors32, Has.Length.EqualTo(mesh.vertexCount));
                foreach (var color in mesh.colors32)
                    Assert.That(
                        color,
                        Is.EqualTo(new Color32(255, 255, 255, 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void VertexColorModesPreserveReplaceAndMergeSelectedChannels()
        {
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero },
                colors32 = new[] { new Color32(1, 2, 3, 4) },
            };
            try
            {
                MikuToonMeshData.InitializeVertexColors(
                    mesh,
                    MikuVertexColorWriteMode.Preserve);
                Assert.That(
                    mesh.colors32[0],
                    Is.EqualTo(new Color32(1, 2, 3, 4)));

                MikuToonMeshData.InitializeVertexColors(
                    mesh,
                    MikuVertexColorWriteMode.Merge,
                    mergeR: true,
                    mergeG: false,
                    mergeB: true,
                    mergeA: false);
                Assert.That(
                    mesh.colors32[0],
                    Is.EqualTo(new Color32(255, 2, 255, 4)));

                MikuToonMeshData.InitializeVertexColors(
                    mesh,
                    MikuVertexColorWriteMode.Replace);
                Assert.That(
                    mesh.colors32[0],
                    Is.EqualTo(new Color32(255, 255, 255, 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void MeshAssetCreatorClonesOutputWithoutChangingSource()
        {
            const string root = "Assets/MikuTests";
            if (!AssetDatabase.IsValidFolder(root))
                AssetDatabase.CreateFolder("Assets", "MikuTests");
            var source = new Mesh
            {
                name = "SourceMesh",
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
                normals = new[]
                {
                    Vector3.forward,
                    Vector3.forward,
                    Vector3.forward,
                },
                triangles = new[] { 0, 1, 2 },
                colors32 = new[]
                {
                    new Color32(1, 2, 3, 4),
                    new Color32(5, 6, 7, 8),
                    new Color32(9, 10, 11, 12),
                },
            };
            var sourceColors = source.colors32.ToArray();
            try
            {
                var generated = MikuToonMeshAssetCreator.CreateAsset(
                    source,
                    root,
                    "GeneratedMesh",
                    writeNormals: true,
                    positionTolerance: 0.0001f,
                    smoothingAngle: 60f,
                    includeBoneWeightSignature: true,
                    overwriteExistingUv7: false,
                    writeColors: true,
                    colorMode: MikuVertexColorWriteMode.Replace,
                    mergeR: true,
                    mergeG: true,
                    mergeB: true,
                    mergeA: true);
                Assert.That(generated, Is.Not.SameAs(source));
                Assert.That(
                    AssetDatabase.GetAssetPath(generated),
                    Does.StartWith(root + "/GeneratedMesh"));
                Assert.That(source.colors32, Is.EqualTo(sourceColors));
                var sourceUv7 = new List<Vector3>();
                source.GetUVs(7, sourceUv7);
                Assert.That(sourceUv7, Is.Empty);
                var generatedUv7 = new List<Vector3>();
                generated.GetUVs(7, generatedUv7);
                Assert.That(generatedUv7, Has.Count.EqualTo(3));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport);
                if (source != null)
                    UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void ScreenRimInstallerRollsBackAndDeduplicates()
        {
            const string root = "Assets/MikuTests";
            const string assetPath =
                root + "/ToonRendererFeaturePipeline.asset";
            if (!AssetDatabase.IsValidFolder(root))
                AssetDatabase.CreateFolder("Assets", "MikuTests");
            var pipeline = UniversalRenderPipelineAsset.Create();
            AssetDatabase.CreateAsset(pipeline, assetPath);
            var serializedPipeline = new SerializedObject(pipeline);
            var rendererDataList =
                serializedPipeline.FindProperty("m_RendererDataList");
            Assert.That(rendererDataList, Is.Not.Null);
            Assert.That(rendererDataList.arraySize, Is.GreaterThan(0));
            var rendererData = rendererDataList
                .GetArrayElementAtIndex(0)
                .objectReferenceValue as ScriptableRendererData;
            Assert.That(rendererData, Is.Not.Null);
            if (!AssetDatabase.Contains(rendererData))
                AssetDatabase.AddObjectToAsset(rendererData, pipeline);
            AssetDatabase.SaveAssets();

            try
            {
                var previousIgnoreFailingMessages =
                    LogAssert.ignoreFailingMessages;
                try
                {
                    // Opening an EditorWindow in -nographics batchmode emits
                    // graphics-device errors even though the window instance
                    // and its state are created correctly.
                    LogAssert.ignoreFailingMessages = true;
                    var installerWindow =
                        MikuToonRendererFeatureInstaller.OpenWindow(
                            rendererData);
                    Assert.That(
                        installerWindow.SelectedRendererData,
                        Is.SameAs(rendererData));
                    Assert.That(
                        installerWindow.PreviewText,
                        Does.Contain("Apply will add one feature"));
                    Assert.That(
                        MikuToonRendererFeatureInstaller.CountFeatures(
                            rendererData),
                        Is.Zero);
                    installerWindow.Close();
                }
                finally
                {
                    LogAssert.ignoreFailingMessages =
                        previousIgnoreFailingMessages;
                }

                Assert.Throws<InvalidOperationException>(() =>
                    MikuToonRendererFeatureInstaller.Install(
                        rendererData,
                        () => throw new InvalidOperationException(
                            "injected failure")));
                Assert.That(
                    MikuToonRendererFeatureInstaller.CountFeatures(
                        rendererData),
                    Is.Zero);
                Assert.That(
                    AssetDatabase.LoadAllAssetsAtPath(assetPath)
                        .OfType<MikuToonScreenRimRendererFeature>(),
                    Is.Empty);

                var first =
                    MikuToonRendererFeatureInstaller.Install(rendererData);
                var second =
                    MikuToonRendererFeatureInstaller.Install(rendererData);
                Assert.That(first.created, Is.True);
                Assert.That(second.created, Is.False);
                Assert.That(second.feature, Is.SameAs(first.feature));
                Assert.That(
                    MikuToonRendererFeatureInstaller.CountFeatures(
                        rendererData),
                    Is.EqualTo(1));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [Test]
        public void LegacySelectedAssetMigrationPreservesMetadataGuidAndCurves()
        {
            const string root = "Assets/MikuTests";
            const string legacyPath =
                root + "/Fixture.migr-assets.json";
            const string upgradedPath =
                root + "/Fixture.miku-assets.json";
            const string clipPath = root + "/LegacyClip.anim";
            if (!AssetDatabase.IsValidFolder(root))
                AssetDatabase.CreateFolder("Assets", "MikuTests");
            var absolute = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                legacyPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(
                absolute,
                new JObject
                {
                    ["schema"] =
                        "migr-generated-asset-identity-1.0",
                    ["persistentSourceId"] = "stable-source",
                    ["persistentMaterialId"] = "stable-material",
                    ["owner"] = "MiGR",
                    ["canonicalHash"] = new string('0', 64),
                }.ToString());
            AssetDatabase.ImportAsset(
                legacyPath,
                ImportAssetOptions.ForceSynchronousImport);
            var legacyGuid = AssetDatabase.AssetPathToGUID(legacyPath);

            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
            var binding = EditorCurveBinding.FloatCurve(
                "",
                typeof(Renderer),
                "material._MIGR_RimIntensity");
            AnimationUtility.SetEditorCurve(
                clip,
                binding,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            try
            {
                Assert.That(
                    MikuLegacyAssetMigration.MigrateGeneratedMetadata(
                        legacyPath,
                        false),
                    Is.EqualTo(1));
                Assert.That(
                    AssetDatabase.AssetPathToGUID(legacyPath),
                    Is.EqualTo(legacyGuid));
                Assert.That(
                    MikuLegacyAssetMigration.MigrateGeneratedMetadata(
                        legacyPath,
                        true),
                    Is.EqualTo(1));
                Assert.That(
                    AssetDatabase.AssetPathToGUID(upgradedPath),
                    Is.EqualTo(legacyGuid));
                var document = JObject.Parse(
                    File.ReadAllText(
                        Path.Combine(
                            Directory.GetParent(Application.dataPath)
                                .FullName,
                            upgradedPath.Replace(
                                '/',
                                Path.DirectorySeparatorChar))));
                Assert.That(
                    document["schema"]?.Value<string>(),
                    Is.EqualTo(
                        "miku-generated-asset-identity-1.0"));
                Assert.That(
                    document["persistentSourceId"]?.Value<string>(),
                    Is.EqualTo("stable-source"));
                Assert.That(
                    document["owner"]?.Value<string>(),
                    Is.EqualTo("Miku"));
                Assert.That(
                    document["canonicalHash"]?.Value<string>(),
                    Has.Length.EqualTo(64));

                Assert.That(
                    MikuLegacyAssetMigration.MigrateClip(
                        clipPath,
                        false),
                    Is.EqualTo(1));
                Assert.That(
                    MikuLegacyAssetMigration.MigrateClip(
                        clipPath,
                        true),
                    Is.EqualTo(1));
                Assert.That(
                    AnimationUtility.GetCurveBindings(clip)
                        .Single().propertyName,
                    Is.EqualTo(
                        "material._MIKU_RimIntensity"));
                Assert.That(
                    MikuLegacyAssetMigration.UpgradePropertyName(
                        "_MGIR_BaseColor"),
                    Is.EqualTo("_MIKU_BaseColor"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
