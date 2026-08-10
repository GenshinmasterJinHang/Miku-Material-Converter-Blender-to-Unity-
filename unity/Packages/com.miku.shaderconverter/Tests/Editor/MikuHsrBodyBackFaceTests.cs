// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Miku.ShaderConverter.Editor.Tests
{
    public sealed class MikuHsrBodyBackFaceTests
    {
        static string HsrPath(string name) =>
            Path.Combine(
                Application.dataPath,
                "../Packages/com.miku.shaderconverter/Runtime/HSR",
                name);

        [Test]
        public void BodyExposesDoubleSidedBackFaceContract()
        {
            var body = File.ReadAllText(HsrPath("HSR_Body.shader"));
            foreach (var marker in new[]
            {
                "_DoubleSided",
                "_HSR_DOUBLE_SIDED",
                "_Cull",
                "_BackUV1",
                "TEXCOORD1",
                "VFACE",
                "Cull [_Cull]",
            })
                StringAssert.Contains(marker, body);

            foreach (var other in new[]
            {
                "HSR_Face.shader",
                "HSR_Hair.shader",
                "HSR_Eye.shader",
            })
            {
                var source = File.ReadAllText(HsrPath(other));
                StringAssert.DoesNotContain(
                    "_HSR_DOUBLE_SIDED",
                    source,
                    other);
                StringAssert.DoesNotContain("_BackUV1", source, other);
            }
        }

        [Test]
        public void DoubleSidedVariantCompiles()
        {
            var shader = Shader.Find("MIKU/HSR/Body");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                material.EnableKeyword("_HSR_DOUBLE_SIDED");
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void KeywordUtilitySyncsDoubleSided()
        {
            var shader = Shader.Find("MIKU/HSR/Body");
            var material = new Material(shader);
            try
            {
                material.SetFloat("_DoubleSided", 1f);
                MikuManualTextureKeywordUtility.SyncKeywords(material);
                Assert.That(
                    material.IsKeywordEnabled("_HSR_DOUBLE_SIDED"),
                    Is.True);

                material.SetFloat("_DoubleSided", 0f);
                MikuManualTextureKeywordUtility.SyncKeywords(material);
                Assert.That(
                    material.IsKeywordEnabled("_HSR_DOUBLE_SIDED"),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
