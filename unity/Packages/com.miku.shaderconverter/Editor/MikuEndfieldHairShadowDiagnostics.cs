// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    internal readonly struct MikuEndfieldHairShadowDiagnostic
    {
        internal MikuEndfieldHairShadowDiagnostic(
            string code,
            string message,
            UnityEngine.Object context)
        {
            Code = code;
            Message = message;
            Context = context;
        }

        internal string Code { get; }
        internal string Message { get; }
        internal UnityEngine.Object Context { get; }
    }

    internal static class MikuEndfieldHairShadowDiagnostics
    {
        internal const string OffsetMeshMissing =
            "MIKU_ENDFIELD_HAIR_SHADOW_OFFSET_MESH_MISSING";
        internal const string StencilMismatch =
            "MIKU_ENDFIELD_HAIR_SHADOW_STENCIL_MISMATCH";

        const string HairShadowShader = "MIKU/Endfield/HairShadow";
        const string FaceShader = "MIKU/Endfield/Face";
        const string MenuPath = "Tools/Miku/Validate Selected Endfield Hair Shadow";

        internal static IReadOnlyList<MikuEndfieldHairShadowDiagnostic> Validate(
            GameObject root)
        {
            var diagnostics = new List<MikuEndfieldHairShadowDiagnostic>();
            if (root == null)
            {
                diagnostics.Add(new MikuEndfieldHairShadowDiagnostic(
                    OffsetMeshMissing,
                    "Select the character root containing the offset hair-shadow mesh.",
                    null));
                return diagnostics;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var shadowRenderers = renderers
                .Where(renderer => UsesShader(renderer, HairShadowShader))
                .ToArray();
            if (shadowRenderers.Length == 0)
            {
                diagnostics.Add(new MikuEndfieldHairShadowDiagnostic(
                    OffsetMeshMissing,
                    "No renderer uses MIKU/Endfield/HairShadow under the selected root.",
                    root));
                return diagnostics;
            }

            foreach (var renderer in shadowRenderers)
            {
                if (SharedMesh(renderer) == null ||
                    UsesShader(renderer, FaceShader))
                {
                    diagnostics.Add(new MikuEndfieldHairShadowDiagnostic(
                        OffsetMeshMissing,
                        "HairShadow requires a dedicated renderer with a valid offset mesh.",
                        renderer));
                }
            }

            var faceMaterials = renderers
                .SelectMany(renderer => renderer.sharedMaterials ??
                    Array.Empty<Material>())
                .Where(material => UsesShader(material, FaceShader))
                .Distinct()
                .ToArray();
            foreach (var renderer in shadowRenderers)
            foreach (var shadowMaterial in renderer.sharedMaterials ??
                         Array.Empty<Material>())
            {
                if (!UsesShader(shadowMaterial, HairShadowShader))
                    continue;
                if (!faceMaterials.Any(faceMaterial =>
                        StencilContractsMatch(shadowMaterial, faceMaterial)))
                {
                    diagnostics.Add(new MikuEndfieldHairShadowDiagnostic(
                        StencilMismatch,
                        "HairShadow stencil Ref/ReadMask does not match a Face " +
                        "material that writes the same stencil bits with Replace.",
                        shadowMaterial));
                }
            }

            return diagnostics;
        }

        [MenuItem(MenuPath)]
        static void ValidateSelected()
        {
            var diagnostics = Validate(Selection.activeGameObject);
            if (diagnostics.Count == 0)
            {
                Debug.Log(
                    "MIKU_ENDFIELD_HAIR_SHADOW_VALID",
                    Selection.activeGameObject);
                return;
            }

            foreach (var diagnostic in diagnostics)
            {
                Debug.LogWarning(
                    diagnostic.Code + ":" + diagnostic.Message,
                    diagnostic.Context);
            }
        }

        [MenuItem(MenuPath, true)]
        static bool CanValidateSelected() => Selection.activeGameObject != null;

        static bool StencilContractsMatch(
            Material shadowMaterial,
            Material faceMaterial)
        {
            if (!HasProperties(
                    shadowMaterial,
                    "_StencilRef",
                    "_StencilReadMask") ||
                !HasProperties(
                    faceMaterial,
                    "_StencilRef",
                    "_StencilWriteMask",
                    "_StencilPass"))
                return false;

            var shadowRef = Mathf.RoundToInt(
                shadowMaterial.GetFloat("_StencilRef"));
            var readMask = Mathf.RoundToInt(
                shadowMaterial.GetFloat("_StencilReadMask"));
            var faceRef = Mathf.RoundToInt(faceMaterial.GetFloat("_StencilRef"));
            var writeMask = Mathf.RoundToInt(
                faceMaterial.GetFloat("_StencilWriteMask"));
            var pass = Mathf.RoundToInt(faceMaterial.GetFloat("_StencilPass"));
            var sharedMask = readMask & writeMask;
            return sharedMask != 0 && pass == 2 &&
                (shadowRef & sharedMask) == (faceRef & sharedMask);
        }

        static Mesh SharedMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
                return skinned.sharedMesh;
            return renderer is MeshRenderer
                ? renderer.GetComponent<MeshFilter>()?.sharedMesh
                : null;
        }

        static bool UsesShader(Renderer renderer, string shaderName) =>
            (renderer.sharedMaterials ?? Array.Empty<Material>())
            .Any(material => UsesShader(material, shaderName));

        static bool UsesShader(Material material, string shaderName) =>
            material != null && material.shader != null &&
            string.Equals(
                material.shader.name,
                shaderName,
                StringComparison.Ordinal);

        static bool HasProperties(Material material, params string[] properties) =>
            material != null && properties.All(material.HasProperty);
    }
}
