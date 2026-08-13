// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using Miku.ShaderConverter.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Miku.ShaderConverter.Tests.Editor
{
    public sealed class MikuGameToonOutlineTests
    {
        sealed class SaveIsolationProbe : ScriptableObject
        {
            public int value;
        }

        static readonly string[] ConcreteOutlineShaders =
        {
            "Genshin/Genshin_Body.shader",
            "Genshin/Genshin_Face.shader",
            "Genshin/Genshin_Hair.shader",
            "HSR/HSR_Body.shader",
            "HSR/HSR_Face.shader",
            "HSR/HSR_Hair.shader",
            "Wuwa/Wuwa_Body.shader",
            "Wuwa/Wuwa_Face.shader",
            "Wuwa/Wuwa_Hair.shader",
        };

        static readonly string[] EndfieldOutlineConsumers =
        {
            "Endfield/Endfield_Body.shader",
            "Endfield/Endfield_Skin.shader",
            "Endfield/Endfield_Face.shader",
            "Endfield/Endfield_Hair.shader",
        };

        [Test]
        public void SmoothNormalGeneratorDefaultsToOneMicrometerTolerance()
        {
            Assert.That(
                MikuToonMeshData.DefaultPositionTolerance,
                Is.EqualTo(0.000001f));
        }

        [Test]
        public void SmoothNormalGeneratorWritesSignedUnitTangentSpaceV2()
        {
            var mesh = CreateTriangle();
            try
            {
                Generate(mesh, mesh);

                Assert.That(
                    mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord7),
                    Is.EqualTo(4));
                var uv7 = ReadUv7(mesh);
                Assert.That(uv7, Has.Count.EqualTo(3));
                foreach (var encoded in uv7)
                {
                    AssertFiniteUnitHemisphere(encoded);
                    Assert.That(encoded.x, Is.EqualTo(0f).Within(1e-6f));
                    Assert.That(encoded.y, Is.EqualTo(0f).Within(1e-6f));
                    Assert.That(encoded.z, Is.EqualTo(1f).Within(1e-6f));

                    var reconstructed =
                        Vector3.right * encoded.x +
                        Vector3.back * encoded.y +
                        Vector3.up * encoded.z;
                    Assert.That(
                        Vector3.Dot(reconstructed.normalized, Vector3.up),
                        Is.GreaterThanOrEqualTo(1f - 1e-6f));
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ReversedWindingIsHemisphereAlignedBeforeAccumulation()
        {
            var mesh = new Mesh { name = "MirroredOutlineSeam" };
            mesh.vertices = new[]
            {
                Vector3.zero, Vector3.forward, Vector3.right,
                Vector3.zero, Vector3.forward, Vector3.right,
            };
            mesh.normals = Repeat(Vector3.up, 6);
            mesh.tangents = Repeat(new Vector4(1f, 0f, 0f, 1f), 6);
            mesh.triangles = new[] { 0, 1, 2, 3, 5, 4 };
            try
            {
                Generate(mesh, mesh);

                var uv7 = ReadUv7(mesh);
                Assert.That(uv7, Has.Count.EqualTo(6));
                foreach (var encoded in uv7)
                {
                    AssertFiniteUnitHemisphere(encoded);
                    Assert.That(encoded.z, Is.GreaterThanOrEqualTo(1f - 1e-6f));
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void TangentSpaceV2AveragesAcrossSubmeshes()
        {
            var mesh = CreateBentSeam(true, false);
            try
            {
                Generate(mesh, mesh);

                var uv7 = ReadUv7(mesh);
                Assert.That(uv7, Has.Count.EqualTo(6));
                AssertFiniteUnitHemisphere(uv7[0]);
                AssertFiniteUnitHemisphere(uv7[3]);
                var expected = new Vector3(0f, 1f, 1f).normalized;
                var normals = mesh.normals;
                var tangents = mesh.tangents;
                Assert.That(
                    Vector3.Dot(
                        DecodeTangentSpaceV2(uv7[0], normals[0], tangents[0]),
                        expected),
                    Is.GreaterThanOrEqualTo(1f - 1e-6f));
                Assert.That(
                    Vector3.Dot(
                        DecodeTangentSpaceV2(uv7[3], normals[3], tangents[3]),
                        expected),
                    Is.GreaterThanOrEqualTo(1f - 1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void TangentSpaceV2PreservesMirroredIslandHandedness()
        {
            var mesh = CreateBentSeam(false, true);
            try
            {
                Generate(mesh, mesh);

                var uv7 = ReadUv7(mesh);
                var normals = mesh.normals;
                var tangents = mesh.tangents;
                var expected = new Vector3(0f, 1f, 1f).normalized;
                AssertFiniteUnitHemisphere(uv7[3]);
                Assert.That(uv7[3].y, Is.LessThan(0f));
                Assert.That(
                    Vector3.Dot(
                        DecodeTangentSpaceV2(uv7[3], normals[3], tangents[3]),
                        expected),
                    Is.GreaterThanOrEqualTo(1f - 1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void NearOppositeTwoSidedVerticesAreNeverAveraged()
        {
            var mesh = CreateBentSeam(false, false);
            var normals = mesh.normals;
            for (var index = 0; index < 3; index++)
                normals[index] = Vector3.up;
            var nearOpposite = new Vector3(0f, -1f, 0.1f).normalized;
            for (var index = 3; index < 6; index++)
                normals[index] = nearOpposite;
            mesh.normals = normals;
            try
            {
                Generate(mesh, mesh);

                var encoded = ReadUv7(mesh)[0];
                var decoded = DecodeTangentSpaceV2(
                    encoded,
                    Vector3.up,
                    mesh.tangents[0]);
                Assert.That(
                    Vector3.Dot(decoded, Vector3.up),
                    Is.GreaterThanOrEqualTo(1f - 1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void WideSmoothingAngleCanBridgeBeyondNinetyDegrees()
        {
            var mesh = CreateBentSeam(false, false);
            var normals = mesh.normals;
            for (var index = 0; index < 3; index++)
                normals[index] = Vector3.up;
            var foldedNormal = new Vector3(0f, -0.5f, 0.8660254f);
            for (var index = 3; index < 6; index++)
                normals[index] = foldedNormal;
            mesh.normals = normals;
            try
            {
                MikuToonMeshData.GenerateSmoothNormalsFromSource(
                    mesh,
                    mesh,
                    MikuToonMeshData.DefaultPositionTolerance,
                    121f,
                    false,
                    true);

                var decoded = DecodeTangentSpaceV2(
                    ReadUv7(mesh)[0],
                    Vector3.up,
                    mesh.tangents[0]);
                Assert.That(decoded.z, Is.GreaterThan(0.25f));
                Assert.That(Vector3.Dot(decoded, Vector3.up), Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void RepeatedTangentSpaceV2GenerationIsStable()
        {
            var mesh = CreateBentSeam(true, true);
            try
            {
                Generate(mesh, mesh);
                var first = ReadUv7(mesh);
                Generate(mesh, mesh);
                var second = ReadUv7(mesh);

                Assert.That(second, Has.Count.EqualTo(first.Count));
                for (var index = 0; index < first.Count; index++)
                    Assert.That(second[index], Is.EqualTo(first[index]));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void TangentFailuresUseStableDiagnosticAndDoNotPartiallyWriteUv7()
        {
            var missing = CreateTriangle(false);
            var nonFinite = CreateTriangle();
            var nonFiniteTangents = nonFinite.tangents;
            nonFiniteTangents[1] = new Vector4(float.NaN, 0f, 0f, 1f);
            nonFinite.tangents = nonFiniteTangents;
            var degenerate = CreateTriangle();
            var degenerateTangents = degenerate.tangents;
            degenerateTangents[1] = new Vector4(0f, 1f, 0f, 1f);
            degenerate.tangents = degenerateTangents;
            try
            {
                AssertFailurePreservesUv7(
                    missing,
                    "MIKU_TOON_TANGENTS_REQUIRED");
                AssertFailurePreservesUv7(
                    nonFinite,
                    "MIKU_TOON_TANGENTS_REQUIRED");
                AssertFailurePreservesUv7(
                    degenerate,
                    "MIKU_TOON_TANGENTS_REQUIRED");
            }
            finally
            {
                Object.DestroyImmediate(missing);
                Object.DestroyImmediate(nonFinite);
                Object.DestroyImmediate(degenerate);
            }
        }

        [Test]
        public void CreateAssetGenerationFailureLeavesNoFolderOrAsset()
        {
            const string root = "Assets/MikuOutlineCreateTransactionTests";
            var source = CreateTriangle(false);
            AssetDatabase.DeleteAsset(root);
            try
            {
                var error = Assert.Throws<InvalidOperationException>(() =>
                    MikuToonMeshAssetCreator.CreateAsset(
                        source,
                        root + "/Nested",
                        "ShouldNotExist",
                        true,
                        MikuToonMeshData.DefaultPositionTolerance,
                        60f,
                        false,
                        true,
                        false,
                        MikuVertexColorWriteMode.Preserve,
                        true,
                        true,
                        true,
                        true));

                Assert.That(
                    error.Message,
                    Is.EqualTo("MIKU_TOON_TANGENTS_REQUIRED"));
                Assert.That(AssetDatabase.IsValidFolder(root), Is.False);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<Mesh>(
                        root + "/Nested/ShouldNotExist.asset"),
                    Is.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void CreateOrUpdateGenerationFailureLeavesNoFolderOrAsset()
        {
            const string root = "Assets/MikuOutlineUpdateTransactionTests";
            const string path = root + "/Nested/ShouldNotExist.asset";
            var source = CreateTriangle(false);
            AssetDatabase.DeleteAsset(root);
            try
            {
                var error = Assert.Throws<InvalidOperationException>(() =>
                    MikuToonMeshAssetCreator.CreateOrUpdateSmoothNormalAsset(
                        source,
                        path));

                Assert.That(
                    error.Message,
                    Is.EqualTo("MIKU_TOON_TANGENTS_REQUIRED"));
                Assert.That(AssetDatabase.IsValidFolder(root), Is.False);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<Mesh>(path),
                    Is.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void CreateOrUpdateCommitFailureRestoresExistingAssetExactly()
        {
            const string root = "Assets/MikuOutlineExistingTransactionTests";
            const string path = root + "/Existing.asset";
            AssetDatabase.DeleteAsset(root);
            AssetDatabase.CreateFolder("Assets", "MikuOutlineExistingTransactionTests");
            var source = CreateBentSeam(true, false);
            var existing = CreateTriangle();
            existing.name = "Existing";
            existing.SetUVs(7, new List<Vector4>
            {
                new Vector4(0.1f, 0.2f, 0.3f, 0.4f),
                new Vector4(0.5f, 0.6f, 0.7f, 0.8f),
                new Vector4(0.9f, 1.0f, 1.1f, 1.2f),
            });
            AssetDatabase.CreateAsset(existing, path);
            AssetDatabase.SaveAssets();
            try
            {
                var guidBefore = AssetDatabase.AssetPathToGUID(path);
                var hashBefore = AssetDatabase.GetAssetDependencyHash(path);
                var jsonBefore = EditorJsonUtility.ToJson(existing);
                var verticesBefore = existing.vertices;
                var uv7Before = ReadUv7(existing);

                Assert.Throws<IOException>(() =>
                    MikuToonMeshAssetCreator.CreateOrUpdateSmoothNormalAsset(
                        source,
                        path,
                        MikuToonMeshData.DefaultPositionTolerance,
                        180f,
                        false,
                        () => throw new IOException("injected commit failure")));

                existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.That(existing, Is.Not.Null);
                Assert.That(
                    AssetDatabase.AssetPathToGUID(path),
                    Is.EqualTo(guidBefore));
                Assert.That(
                    AssetDatabase.GetAssetDependencyHash(path),
                    Is.EqualTo(hashBefore));
                Assert.That(
                    EditorJsonUtility.ToJson(existing),
                    Is.EqualTo(jsonBefore));
                CollectionAssert.AreEqual(verticesBefore, existing.vertices);
                CollectionAssert.AreEqual(uv7Before, ReadUv7(existing));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void MeshAssetCreationDoesNotSaveUnrelatedDirtyAssets()
        {
            const string root = "Assets/MikuOutlineSaveIsolationTests";
            const string probePath = root + "/Unrelated.asset";
            const string meshPath = root + "/Outline.asset";
            AssetDatabase.DeleteAsset(root);
            AssetDatabase.CreateFolder("Assets", "MikuOutlineSaveIsolationTests");
            var probe = ScriptableObject.CreateInstance<SaveIsolationProbe>();
            probe.value = 1;
            AssetDatabase.CreateAsset(probe, probePath);
            AssetDatabase.SaveAssetIfDirty(probe);
            var bytesBefore = File.ReadAllBytes(probePath);
            probe.value = 2;
            EditorUtility.SetDirty(probe);
            var source = CreateTriangle();
            try
            {
                var generated =
                    MikuToonMeshAssetCreator.CreateOrUpdateSmoothNormalAsset(
                        source,
                        meshPath);

                Assert.That(generated, Is.Not.Null);
                CollectionAssert.AreEqual(
                    bytesBefore,
                    File.ReadAllBytes(probePath));
                Assert.That(probe.value, Is.EqualTo(2));
                Assert.That(EditorUtility.IsDirty(probe), Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void NonFiniteMeshDataUsesStableDiagnosticsAndDoesNotWriteUv7()
        {
            var nonFinitePosition = CreateTriangle();
            var vertices = nonFinitePosition.vertices;
            vertices[0] = new Vector3(float.PositiveInfinity, 0f, 0f);
            nonFinitePosition.SetVertices(
                vertices,
                0,
                vertices.Length,
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices);

            var nonFiniteNormal = CreateTriangle();
            var normals = nonFiniteNormal.normals;
            normals[0] = new Vector3(0f, float.NaN, 0f);
            nonFiniteNormal.SetNormals(
                normals,
                0,
                normals.Length,
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices);
            try
            {
                AssertFailurePreservesUv7(
                    nonFinitePosition,
                    "MIKU_TOON_MESH_DATA_INVALID:position");
                AssertFailurePreservesUv7(
                    nonFiniteNormal,
                    "MIKU_TOON_MESH_DATA_INVALID:normal");
            }
            finally
            {
                Object.DestroyImmediate(nonFinitePosition);
                Object.DestroyImmediate(nonFiniteNormal);
            }
        }

        [Test]
        public void InvalidTopologyIndexUsesStableDiagnosticAndDoesNotWriteUv7()
        {
            var source = CreateTriangle();
            source.SetIndexBufferParams(3, IndexFormat.UInt16);
            source.SetIndexBufferData(
                new ushort[] { 0, 1, 12 },
                0,
                0,
                3,
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices);
            source.subMeshCount = 1;
            source.SetSubMesh(
                0,
                new SubMeshDescriptor(0, 3, MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices);
            try
            {
                AssertFailurePreservesUv7(
                    source,
                    "MIKU_TOON_MESH_DATA_INVALID:index");
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1080f, 1920f)]
        public void ClipSpaceAspectCorrectionUsesScreenHeightUnits(
            float screenWidth,
            float screenHeight)
        {
            const float screenHeightFraction = 0.001f;
            foreach (var sourceDirection in new[]
            {
                Vector2.right,
                Vector2.up,
                new Vector2(1f, 1f).normalized,
            })
            {
                foreach (var projectionYFlip in new[] { 1f, -1f })
                {
                    var projectedDirection = new Vector2(
                        sourceDirection.x * screenHeight / screenWidth,
                        sourceDirection.y * projectionYFlip);
                    var pixelDirection = new Vector2(
                        projectedDirection.x * screenWidth,
                        projectedDirection.y * screenHeight).normalized;
                    var clipDirection = new Vector2(
                        pixelDirection.x * screenHeight / screenWidth,
                        pixelDirection.y);
                    var ndcOffset = clipDirection *
                        (2f * screenHeightFraction);
                    var pixelOffset = new Vector2(
                        ndcOffset.x * screenWidth * 0.5f,
                        ndcOffset.y * screenHeight * 0.5f);
                    Assert.That(
                        pixelOffset.magnitude,
                        Is.EqualTo(screenHeightFraction * screenHeight)
                            .Within(1e-5f));
                    if (Mathf.Abs(sourceDirection.y) > 1e-6f)
                        Assert.That(
                            Mathf.Sign(pixelOffset.y),
                            Is.EqualTo(Mathf.Sign(
                                sourceDirection.y * projectionYFlip)));
                }
            }
        }

        [Test]
        public void AllThirteenOutlineConsumersUseSharedV2AndFamilyWidthContract()
        {
            var consumerCount = 0;
            foreach (var relativePath in ConcreteOutlineShaders)
            {
                var outlinePass = ExtractOutlineSection(RuntimeSource(relativePath));
                StringAssert.Contains("ZWrite Off", outlinePass, relativePath);
                StringAssert.DoesNotContain("ZWrite On", outlinePass, relativePath);
                StringAssert.Contains("Cull Front", outlinePass, relativePath);
                StringAssert.Contains("ZTest LEqual", outlinePass, relativePath);
                StringAssert.Contains(
                    "MikuGameToonOutline.hlsl",
                    outlinePass,
                    relativePath);
                StringAssert.Contains(
                    "MikuGameToonOutlineNormalTangentSpaceV2",
                    outlinePass,
                    relativePath);
                StringAssert.Contains(
                    "MikuGameToonOutlinePositionCS",
                    outlinePass,
                    relativePath);
                StringAssert.Contains(
                    "float outlineCoverage",
                    outlinePass,
                    relativePath);
                StringAssert.Contains(
                    "MikuGameToonOutlineClipCoverage(input.outlineCoverage)",
                    outlinePass,
                    relativePath);
                StringAssert.Contains("tangentOS : TANGENT", outlinePass, relativePath);
                if (relativePath.StartsWith("Genshin/", StringComparison.Ordinal))
                {
                    StringAssert.Contains(
                        "vertexColor : COLOR",
                        outlinePass,
                        relativePath);
                    StringAssert.Contains(
                        "input.vertexColor",
                        outlinePass,
                        relativePath);
                }
                else if (relativePath.StartsWith(
                             "Wuwa/",
                             StringComparison.Ordinal))
                {
                    StringAssert.Contains(
                        "float4 color : COLOR",
                        outlinePass,
                        relativePath);
                    StringAssert.Contains(
                        "input.color",
                        outlinePass,
                        relativePath);
                    StringAssert.Contains(
                        "float4(1.0, 1.0, 1.0, 1.0)",
                        outlinePass,
                        relativePath);
                }
                else
                {
                    StringAssert.DoesNotContain(
                        "vertexColor : COLOR",
                        outlinePass,
                        relativePath);
                    StringAssert.Contains(
                        "float4(1.0, 1.0, 1.0, 1.0)",
                        outlinePass,
                        relativePath);
                    if (relativePath.StartsWith("HSR/", StringComparison.Ordinal))
                    {
                        StringAssert.Contains(
                            "MikuGameToonOutlinePositionCSWithLegacyMode",
                            outlinePass,
                            relativePath);
                    }
                }
                StringAssert.Contains(
                    "float4 smoothNormalData : TEXCOORD7",
                    outlinePass,
                    relativePath);
                StringAssert.Contains("pos.positionCS", outlinePass, relativePath);
                StringAssert.DoesNotContain(
                    "vertexColor.a",
                    outlinePass,
                    relativePath);
                consumerCount++;
            }

            var passLibrary = ExtractOutlineSection(RuntimeSource(
                "Endfield/EndfieldPassLibrary.shader"));
            StringAssert.Contains("ZWrite Off", passLibrary);
            StringAssert.DoesNotContain("ZWrite On", passLibrary);
            StringAssert.Contains("Cull Front", passLibrary);
            StringAssert.Contains("ZTest LEqual", passLibrary);
            StringAssert.Contains(
                "\"LightMode\"=\"MikuToonOutline\"",
                passLibrary);
            StringAssert.DoesNotContain("SRPDefaultUnlit", passLibrary);
            var endfieldCommon = RuntimeSource("Endfield/EndfieldCommon.hlsl");
            StringAssert.Contains(
                "float4 smoothNormalData : TEXCOORD7",
                endfieldCommon);
            var endfieldOutline = ExtractFunctionSection(
                endfieldCommon,
                "EndfieldOutlineVaryings EndfieldOutlineVertex",
                "half4 EndfieldOutlineFragment");
            StringAssert.Contains("input.smoothNormalData", endfieldOutline);
            StringAssert.Contains("position.positionCS", endfieldOutline);
            StringAssert.Contains(
                "MikuGameToonOutlineNormalTangentSpaceV2",
                endfieldOutline);
            StringAssert.Contains("MikuGameToonOutlinePositionCS", endfieldOutline);
            StringAssert.DoesNotContain("input.color.a", endfieldOutline);
            StringAssert.Contains(
                "float outlineCoverage : TEXCOORD1",
                endfieldCommon);
            StringAssert.Contains(
                "MikuGameToonOutlineClipCoverage(input.outlineCoverage)",
                endfieldCommon);
            StringAssert.Contains(
                "MikuGameToonOutlineCoverageWithVertexMask",
                endfieldCommon);

            foreach (var relativePath in EndfieldOutlineConsumers)
            {
                var source = RuntimeSource(relativePath);
                StringAssert.Contains(
                    "UsePass \"Hidden/MIKU/Endfield/PassLibrary/Outline\"",
                    source,
                    relativePath);
                StringAssert.Contains(
                    "_UseOutline (\"Use Outline\", Float) = 1",
                    source,
                    relativePath);
                consumerCount++;
            }

            var shared = RuntimeSource("GameToon/MikuGameToonOutline.hlsl");
            StringAssert.Contains("return saturate(vertexColor.g);", shared);
            StringAssert.Contains("A=face correction", shared);
            StringAssert.Contains(
                "MikuGameToonOutlineCoverageWithDistanceMultiplier",
                shared);
            StringAssert.Contains(
                "MikuGameToonOutlineCoverageWithLegacyMode",
                shared);
            StringAssert.Contains(
                "MikuGameToonOutlineCoverageWithVertexMask",
                shared);
            StringAssert.Contains(
                "void MikuGameToonOutlineClipCoverage(float coverage)",
                shared);
            StringAssert.Contains(
                "coverage > MIKU_GAME_TOON_OUTLINE_EPSILON",
                shared);
            StringAssert.Contains(
                "!MikuGameToonOutlineFinite1(outlineWidth)",
                shared);
            StringAssert.Contains(
                "MIKU_GAME_TOON_OUTLINE_V2_MARKER 2.0",
                shared);
            StringAssert.Contains(
                "MIKU_GAME_TOON_OUTLINE_V2_MARKER_TOLERANCE 1e-3",
                shared);
            StringAssert.Contains(
                "smoothNormalData.w - MIKU_GAME_TOON_OUTLINE_V2_MARKER",
                shared);
            StringAssert.Contains("if (!isTangentSpaceV2)", shared);
            StringAssert.Contains("legacyNormalOS", shared);
            StringAssert.Contains(
                "dot(legacyNormalOS, sourceNormalOS) < 0.0",
                shared);
            StringAssert.Contains(
                "MIKU_GAME_TOON_OUTLINE_MIN_DISTANCE_MULTIPLIER 0.25",
                shared);
            StringAssert.Contains(
                "MIKU_GAME_TOON_OUTLINE_MAX_DISTANCE_MULTIPLIER 4.0",
                shared);
            StringAssert.Contains(
                "referenceDistance /",
                shared);
            StringAssert.Contains(
                "max(\n            referenceDistance /",
                shared);
            StringAssert.Contains(
                "legacyConstantResponse",
                shared);
            StringAssert.Contains(
                "TransformWorldToHClipDir",
                shared);
            StringAssert.Contains(
                "projectedDirection * max(",
                shared);
            StringAssert.Contains(
                "_ScreenParams.y / max(_ScreenParams.x, 1.0)",
                shared);
            StringAssert.Contains("positionCS.xy +=", shared);
            StringAssert.Contains(
                "2.0 * screenHeightWidth * positionCS.w",
                shared);
            StringAssert.DoesNotContain(
                "TransformWorldToHClip(positionWS +",
                shared);
            StringAssert.DoesNotContain(
                "positionWS + outlineNormalWS",
                shared);
            Assert.That(consumerCount, Is.EqualTo(13));
        }

        [Test]
        public void AllThirteenOutlineConsumerShadersCompileWithoutErrors()
        {
            foreach (var shaderName in new[]
            {
                "MIKU/Genshin/Body", "MIKU/Genshin/Face", "MIKU/Genshin/Hair",
                "MIKU/HSR/Body", "MIKU/HSR/Face", "MIKU/HSR/Hair",
                "MIKU/Wuwa/Body", "MIKU/Wuwa/Face", "MIKU/Wuwa/Hair",
                "MIKU/Endfield/Body", "MIKU/Endfield/Skin",
                "MIKU/Endfield/Face", "MIKU/Endfield/Hair",
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

        static Mesh CreateTriangle(bool includeTangents = true)
        {
            var mesh = new Mesh { name = "OutlineTriangle" };
            mesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.forward,
                Vector3.right,
            };
            mesh.normals = Repeat(Vector3.up, 3);
            if (includeTangents)
                mesh.tangents = Repeat(new Vector4(1f, 0f, 0f, 1f), 3);
            mesh.triangles = new[] { 0, 1, 2 };
            return mesh;
        }

        static Mesh CreateBentSeam(
            bool splitSubmeshes,
            bool mirroredSecondIsland)
        {
            var firstNormal = new Vector3(0f, 1f, 0.25f).normalized;
            var secondNormal = new Vector3(0f, 0.25f, 1f).normalized;
            var mesh = new Mesh { name = "BentOutlineSeam" };
            mesh.vertices = new[]
            {
                Vector3.zero, Vector3.forward, Vector3.right,
                Vector3.zero, Vector3.right, Vector3.up,
            };
            mesh.normals = new[]
            {
                firstNormal, firstNormal, firstNormal,
                secondNormal, secondNormal, secondNormal,
            };
            var tangents = Repeat(new Vector4(1f, 0f, 0f, 1f), 6);
            if (mirroredSecondIsland)
            {
                for (var index = 3; index < tangents.Length; index++)
                {
                    var tangent = tangents[index];
                    tangent.w = -1f;
                    tangents[index] = tangent;
                }
            }
            mesh.tangents = tangents;
            if (splitSubmeshes)
            {
                mesh.subMeshCount = 2;
                mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
            }
            else
            {
                mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
            }
            return mesh;
        }

        static Vector3 DecodeTangentSpaceV2(
            Vector4 encoded,
            Vector3 normal,
            Vector4 tangent)
        {
            normal.Normalize();
            var tangentAxis = new Vector3(tangent.x, tangent.y, tangent.z);
            tangentAxis -= normal * Vector3.Dot(normal, tangentAxis);
            tangentAxis.Normalize();
            var bitangentAxis = Vector3.Cross(normal, tangentAxis) *
                (tangent.w < 0f ? -1f : 1f);
            return (
                tangentAxis * encoded.x +
                bitangentAxis * encoded.y +
                normal * encoded.z).normalized;
        }

        static T[] Repeat<T>(T value, int count)
        {
            var values = new T[count];
            for (var index = 0; index < count; index++)
                values[index] = value;
            return values;
        }

        static void Generate(Mesh source, Mesh destination)
        {
            MikuToonMeshData.GenerateSmoothNormalsFromSource(
                source,
                destination,
                MikuToonMeshData.DefaultPositionTolerance,
                180f,
                false,
                true);
        }

        static List<Vector4> ReadUv7(Mesh mesh)
        {
            var values = new List<Vector4>();
            mesh.GetUVs(7, values);
            return values;
        }

        static void AssertFiniteUnitHemisphere(Vector4 value)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False);
            Assert.That(float.IsNaN(value.w) || float.IsInfinity(value.w), Is.False);
            Assert.That(
                new Vector3(value.x, value.y, value.z).magnitude,
                Is.EqualTo(1f).Within(1e-6f));
            Assert.That(value.z, Is.GreaterThanOrEqualTo(-1e-6f));
            Assert.That(value.w, Is.EqualTo(2f));
        }

        static void AssertFailurePreservesUv7(
            Mesh source,
            string expectedDiagnostic)
        {
            var destination = CreateTriangle();
            var sentinel = new[]
            {
                new Vector4(0.125f, 0.25f, 0.5f, 0.75f),
                new Vector4(0.25f, 0.5f, 0.75f, 1f),
                new Vector4(0.5f, 0.75f, 1f, 1.25f),
            };
            destination.SetUVs(7, new List<Vector4>(sentinel));
            try
            {
                var error = Assert.Throws<InvalidOperationException>(
                    () => Generate(source, destination));
                Assert.That(error.Message, Is.EqualTo(expectedDiagnostic));
                var actual = ReadUv7(destination);
                Assert.That(actual, Has.Count.EqualTo(sentinel.Length));
                for (var index = 0; index < sentinel.Length; index++)
                    Assert.That(actual[index], Is.EqualTo(sentinel[index]));
            }
            finally
            {
                Object.DestroyImmediate(destination);
            }
        }

        static string RuntimeSource(string relativePath)
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Packages/com.miku.shaderconverter/Runtime",
                relativePath));
            return File.ReadAllText(path);
        }

        static string ExtractOutlineSection(string source)
        {
            var start = source.IndexOf(
                "Name \"MikuToonOutline\"",
                StringComparison.Ordinal);
            if (start < 0)
            {
                start = source.IndexOf(
                    "Name \"Outline\"",
                    StringComparison.Ordinal);
            }
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            var end = source.IndexOf("ENDHLSL", start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            return source.Substring(start, end - start);
        }

        static string ExtractFunctionSection(
            string source,
            string startMarker,
            string endMarker)
        {
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            return source.Substring(start, end - start);
        }
    }
}
