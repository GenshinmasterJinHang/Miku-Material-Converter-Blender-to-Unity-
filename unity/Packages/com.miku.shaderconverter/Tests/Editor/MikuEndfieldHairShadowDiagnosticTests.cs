// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System.Linq;
using Miku.ShaderConverter.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Miku.ShaderConverter.Tests.Editor
{
    public sealed class MikuEndfieldHairShadowDiagnosticTests
    {
        [Test]
        public void ValidOffsetMeshAndMatchingFaceStencilHaveNoDiagnostics()
        {
            var setup = CreateSetup();
            try
            {
                Assert.That(
                    MikuEndfieldHairShadowDiagnostics.Validate(setup.root),
                    Is.Empty);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void MissingOffsetMeshAndStencilMismatchAreActionable()
        {
            var setup = CreateSetup();
            try
            {
                setup.shadowFilter.sharedMesh = null;
                setup.shadowMaterial.SetFloat("_StencilRef", 37f);

                var codes = MikuEndfieldHairShadowDiagnostics
                    .Validate(setup.root)
                    .Select(diagnostic => diagnostic.Code)
                    .ToArray();
                CollectionAssert.Contains(
                    codes,
                    MikuEndfieldHairShadowDiagnostics.OffsetMeshMissing);
                CollectionAssert.Contains(
                    codes,
                    MikuEndfieldHairShadowDiagnostics.StencilMismatch);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void MissingHairShadowRendererReportsOffsetMeshDiagnostic()
        {
            var root = new GameObject("Endfield Character Without Hair Shadow");
            try
            {
                var diagnostics = MikuEndfieldHairShadowDiagnostics.Validate(root);
                Assert.That(
                    diagnostics.Single().Code,
                    Is.EqualTo(MikuEndfieldHairShadowDiagnostics.OffsetMeshMissing));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static Setup CreateSetup()
        {
            var root = new GameObject("Endfield Hair Shadow Diagnostic Root");
            var faceObject = new GameObject("Face Receiver");
            faceObject.transform.SetParent(root.transform, false);
            var faceFilter = faceObject.AddComponent<MeshFilter>();
            var faceRenderer = faceObject.AddComponent<MeshRenderer>();
            var faceMesh = TriangleMesh("Face Receiver Mesh");
            faceFilter.sharedMesh = faceMesh;
            var faceMaterial = new Material(Shader.Find("MIKU/Endfield/Face"));
            faceRenderer.sharedMaterial = faceMaterial;

            var shadowObject = new GameObject("Offset Fringe Shadow Mesh");
            shadowObject.transform.SetParent(root.transform, false);
            shadowObject.transform.localPosition = new Vector3(0f, 0f, -0.001f);
            var shadowFilter = shadowObject.AddComponent<MeshFilter>();
            var shadowRenderer = shadowObject.AddComponent<MeshRenderer>();
            var shadowMesh = TriangleMesh("Offset Fringe Shadow Mesh");
            shadowFilter.sharedMesh = shadowMesh;
            var shadowMaterial = new Material(
                Shader.Find("MIKU/Endfield/HairShadow"));
            shadowRenderer.sharedMaterial = shadowMaterial;

            return new Setup(
                root,
                faceMaterial,
                faceMesh,
                shadowMaterial,
                shadowMesh,
                shadowFilter);
        }

        static Mesh TriangleMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
            };
            mesh.triangles = new[] { 0, 1, 2 };
            return mesh;
        }

        sealed class Setup
        {
            internal Setup(
                GameObject root,
                Material faceMaterial,
                Mesh faceMesh,
                Material shadowMaterial,
                Mesh shadowMesh,
                MeshFilter shadowFilter)
            {
                this.root = root;
                this.faceMaterial = faceMaterial;
                this.faceMesh = faceMesh;
                this.shadowMaterial = shadowMaterial;
                this.shadowMesh = shadowMesh;
                this.shadowFilter = shadowFilter;
            }

            internal readonly GameObject root;
            internal readonly Material faceMaterial;
            internal readonly Mesh faceMesh;
            internal readonly Material shadowMaterial;
            internal readonly Mesh shadowMesh;
            internal readonly MeshFilter shadowFilter;

            internal void Dispose()
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(faceMaterial);
                Object.DestroyImmediate(faceMesh);
                Object.DestroyImmediate(shadowMaterial);
                Object.DestroyImmediate(shadowMesh);
            }
        }
    }
}
