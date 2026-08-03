// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    internal static class MikuFixedWorkflowTextureBindings
    {
        static readonly IReadOnlyDictionary<string, HashSet<string>> Roles =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["genshin_toon"] = Set(
                    "BaseMap", "LightMap", "ShadowRampMap", "MetalMap",
                    "EmissionMap", "HairRampMap", "HairSpecMap", "FaceSDF"),
                ["wuwa_toon"] = Set(
                    "BaseMap", "NormalMap", "IDMap", "MatCap", "EmissionMap",
                    "HairHM", "FaceSDF", "FaceID", "FaceHET", "SkinRamp",
                    "EyeHET", "EyeHDMF", "EyeUpperHighlight",
                    "EyeLowerHighlight", "EyeEG", "StockingsMap"),
                ["hsr_toon"] = Set(
                    "BaseMap", "LightMap", "BodyCoolRamp", "BodyWarmRamp",
                    "StockingsMap", "HairCoolRamp", "HairWarmRamp", "FaceMap",
                    "EmissionMap"),
                ["endfield_toon"] = Set(
                    "BaseMap", "NormalMap", "MaterialParamMap",
                    "DiffRampMap", "SpecRampMap", "ShadowLut",
                    "EmissionMap", "MatCap", "SplitNormalMap",
                    "OutlineMask", "SpecularMask", "LineMap", "StrokeMap",
                    "FaceSDF", "FaceSDFMask", "EmotionMap", "HighlightMap",
                    "HairShadowMap", "EyeShadowMap", "EffectMask",
                    "ColorLut", "FaceAreaMap", "FaceRefineMap",
                    "HairRefineMap", "HairShiftMap", "HairLineMap",
                    "SpecularRefineF0", "SpecularRefineColor"),
            };

        internal static bool IsFixed(string workflow) =>
            string.Equals(workflow, "genshin_toon", StringComparison.Ordinal) ||
            string.Equals(workflow, "wuwa_toon", StringComparison.Ordinal) ||
            string.Equals(workflow, "hsr_toon", StringComparison.Ordinal) ||
            string.Equals(workflow, "endfield_toon", StringComparison.Ordinal);

        internal static bool IsGame(string workflow) =>
            string.Equals(workflow, "genshin_toon", StringComparison.Ordinal) ||
            string.Equals(workflow, "wuwa_toon", StringComparison.Ordinal) ||
            string.Equals(workflow, "hsr_toon", StringComparison.Ordinal) ||
            string.Equals(workflow, "endfield_toon", StringComparison.Ordinal);

        internal static bool IsRoleAllowed(string workflow, string role) =>
            Roles.TryGetValue(workflow, out var roles) && roles.Contains(role);

        internal static string NormalizePart(string workflow, string part)
        {
            var candidate = string.IsNullOrWhiteSpace(part) ? "Body" : part;
            var allowed = string.Equals(
                workflow,
                "endfield_toon",
                StringComparison.Ordinal)
                ? Set(
                    "Body", "Skin", "Hair", "Face", "Eye", "Mouth",
                    "Overlay", "Effect", "HairShadow")
                : string.Equals(workflow, "wuwa_toon", StringComparison.Ordinal)
                    ? Set("Body", "Hair", "Face", "Eye", "Effect")
                    : Set("Body", "Hair", "Face", "Eye");
            if (allowed.Contains(candidate))
                return candidate;
            throw new ArgumentException(
                "MIKU_WORKFLOW_PART_INVALID:" + workflow + ":" + candidate);
        }

        internal static string ShaderName(
            string workflow,
            string part)
        {
            var family = workflow switch
            {
                "genshin_toon" => "Genshin",
                "wuwa_toon" => "Wuwa",
                "hsr_toon" => "HSR",
                "endfield_toon" => "Endfield",
                _ => throw new ArgumentException(
                    "MIKU_WORKFLOW_UNSUPPORTED:" + workflow),
            };
            return "MIKU/" + family + "/" + NormalizePart(workflow, part);
        }

        internal static IReadOnlyList<MikuGameMaterialPart> AllowedParts(
            string workflow)
        {
            return workflow switch
            {
                "genshin_toon" => new[]
                {
                    MikuGameMaterialPart.Body,
                    MikuGameMaterialPart.Hair,
                    MikuGameMaterialPart.Face,
                    MikuGameMaterialPart.Eye,
                },
                "wuwa_toon" => new[]
                {
                    MikuGameMaterialPart.Body,
                    MikuGameMaterialPart.Hair,
                    MikuGameMaterialPart.Face,
                    MikuGameMaterialPart.Eye,
                    MikuGameMaterialPart.Effect,
                },
                "hsr_toon" => new[]
                {
                    MikuGameMaterialPart.Body,
                    MikuGameMaterialPart.Hair,
                    MikuGameMaterialPart.Face,
                    MikuGameMaterialPart.Eye,
                },
                "endfield_toon" => new[]
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
                },
                _ => throw new ArgumentException(
                    "MIKU_WORKFLOW_UNSUPPORTED:" + workflow),
            };
        }

        internal static string TextureProperty(string workflow, string role)
        {
            if (string.Equals(
                    workflow,
                    "endfield_toon",
                    StringComparison.Ordinal))
            {
                return role switch
                {
                    "BaseMap" => "_BaseMap",
                    "NormalMap" => "_NormalMap",
                    "MaterialParamMap" => "_MaterialParamMap",
                    "DiffRampMap" => "_DiffRampMap",
                    "SpecRampMap" => "_SpecRampMap",
                    "ShadowLut" => "_ShadowLutTex",
                    "ColorLut" => "_ColorLutTex",
                    "EmissionMap" => "_EmissionMap",
                    "MatCap" => "_MatCap",
                    "SplitNormalMap" => "_SplitNormalMap",
                    "OutlineMask" => "_OutlineMask",
                    "SpecularMask" => "_SpecularMask",
                    "LineMap" => "_LineMap",
                    "StrokeMap" => "_StrokeMap",
                    "FaceSDF" => "_SDFLightmap",
                    "FaceSDFMask" => "_SDFMask",
                    "FaceAreaMap" => "_FaceAreaMap",
                    "FaceRefineMap" => "_FaceRefineMap",
                    "HairRefineMap" => "_HairRefineMap",
                    "HairShiftMap" => "_HairShiftMap",
                    "HairLineMap" => "_HairLineMap",
                    "SpecularRefineF0" => "_SpecularRefineF0Tex",
                    "SpecularRefineColor" => "_SpecularRefineColorTex",
                    "EmotionMap" => "_EmotionMap",
                    "HighlightMap" => "_HighlightMap",
                    "HairShadowMap" => "_BaseMap",
                    "EyeShadowMap" => "_BaseMap",
                    "EffectMask" => "_EffectMask",
                    _ => "",
                };
            }
            if (role == "BaseMap")
                return "_BaseMap";
            if (role == "FaceSDF")
                return string.Equals(
                    workflow,
                    "genshin_toon",
                    StringComparison.Ordinal)
                    ? "_FaceSDFMap"
                    : string.Equals(
                        workflow,
                        "wuwa_toon",
                        StringComparison.Ordinal)
                        ? "_FaceSDF"
                        : "";
            return role switch
            {
                "LightMap" => "_LightMap",
                "ShadowRampMap" => "_ShadowRampMap",
                "MetalMap" => "_MetalMap",
                "EmissionMap" => "_EmissionMap",
                "HairRampMap" => "_HairRampMap",
                "HairSpecMap" => "_HairSpecMap",
                "NormalMap" => "_NormalMap",
                "IDMap" => "_IDMap",
                "MatCap" => "_MatCap",
                "FaceID" => "_FaceID",
                "FaceHET" => "_FaceHET",
                "SkinRamp" => "_SkinRamp",
                "HairHM" => "_HairHM",
                "EyeHET" => "_EyeHET",
                "EyeHDMF" => "_EyeHDMF",
                "EyeUpperHighlight" => "_EyeUpperHighlight",
                "EyeLowerHighlight" => "_EyeLowerHighlight",
                "EyeEG" => "_EyeEG",
                "BodyCoolRamp" => "_BodyCoolRamp",
                "BodyWarmRamp" => "_BodyWarmRamp",
                "StockingsMap" => "_StockingsMap",
                "FaceMap" => "_FaceMap",
                "HairCoolRamp" => "_HairCoolRamp",
                "HairWarmRamp" => "_HairWarmRamp",
                _ => "",
            };
        }

        internal static void Bind(
            Material material,
            string workflow,
            IEnumerable<MikuToonTextureBinding> bindings)
        {
            if (material == null)
                return;
            ResetEyeUvTransforms(material, workflow);
            var stockingsBindingSupplied = false;
            foreach (var binding in bindings ?? Array.Empty<MikuToonTextureBinding>())
            {
                var role = CanonicalRole(material, workflow, binding.role, out var migrated);
                stockingsBindingSupplied |= string.Equals(
                    role,
                    "StockingsMap",
                    StringComparison.Ordinal);
                if (migrated)
                    Debug.LogWarning(
                        "MIKU_ENDFIELD_ROLE_MIGRATED:" +
                        binding.role + ":" + role,
                        material);
                var property = TextureProperty(workflow, role);
                if (!string.IsNullOrEmpty(property) && material.HasProperty(property))
                {
                    material.SetTexture(property, binding.texture);
                    ApplyEyeUvTransform(material, workflow, role, binding.uvTransform);
                }
            }
            ValidateWuwaStockingsSource(
                material,
                workflow,
                stockingsBindingSupplied);
            MikuManualTextureKeywordUtility.SyncKeywords(material);
        }

        static void ResetEyeUvTransforms(Material material, string workflow)
        {
            if (!string.Equals(workflow, "wuwa_toon", StringComparison.Ordinal) ||
                material?.shader == null ||
                !string.Equals(
                    material.shader.name,
                    "MIKU/Wuwa/Eye",
                    StringComparison.Ordinal))
                return;
            foreach (var role in new[]
                     {
                         "EyeUpperHighlight",
                         "EyeLowerHighlight",
                         "EyeEG",
                     })
                SetUvRows(
                    material,
                    role,
                    new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 1f, 0f));
        }

        static void ApplyEyeUvTransform(
            Material material,
            string workflow,
            string role,
            MikuToonUvTransform transform)
        {
            if (!string.Equals(workflow, "wuwa_toon", StringComparison.Ordinal) ||
                transform == null ||
                !string.Equals(transform.coordinateSpace, "UV0", StringComparison.Ordinal) ||
                !string.Equals(transform.operation, "Affine2D", StringComparison.Ordinal))
                return;
            if (!Finite(transform.row0) || !Finite(transform.row1))
                throw new InvalidOperationException(
                    "MIKU_FIXED_TEXTURE_UV_MATRIX_INVALID:" + role);
            SetUvRows(material, role, transform.row0, transform.row1);
        }

        static void SetUvRows(
            Material material,
            string role,
            Vector3 row0,
            Vector3 row1)
        {
            var prefix = role switch
            {
                "EyeUpperHighlight" => "_EyeUpperHighlightUV",
                "EyeLowerHighlight" => "_EyeLowerHighlightUV",
                "EyeEG" => "_EyeEGUV",
                _ => "",
            };
            if (string.IsNullOrEmpty(prefix))
                return;
            var row0Property = prefix + "Row0";
            var row1Property = prefix + "Row1";
            if (material.HasProperty(row0Property))
                material.SetVector(
                    row0Property,
                    new Vector4(row0.x, row0.y, row0.z, 0f));
            if (material.HasProperty(row1Property))
                material.SetVector(
                    row1Property,
                    new Vector4(row1.x, row1.y, row1.z, 0f));
        }

        static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        static void ValidateWuwaStockingsSource(
            Material material,
            string workflow,
            bool stockingsBindingSupplied)
        {
            if (!stockingsBindingSupplied ||
                !string.Equals(workflow, "wuwa_toon", StringComparison.Ordinal) ||
                material?.shader == null ||
                !string.Equals(material.shader.name, "MIKU/Wuwa/Body", StringComparison.Ordinal) ||
                !material.HasProperty("_StockingsMap"))
                return;
            var stockings = material.GetTexture("_StockingsMap");
            if (stockings == null)
                return;
            var id = material.HasProperty("_IDMap")
                ? material.GetTexture("_IDMap")
                : null;
            if (id != stockings)
                throw new InvalidOperationException(
                    "MIKU_WUWA_STOCKINGS_ID_SOURCE_MISMATCH");
        }

        internal static string CanonicalRole(
            Material material,
            string workflow,
            string role,
            out bool migrated)
        {
            migrated = false;
            if (!string.Equals(workflow, "endfield_toon", StringComparison.Ordinal))
                return role;
            var shaderName = material?.shader != null ? material.shader.name : "";
            var replacement = role switch
            {
                "ShadowLut" => "ColorLut",
                "FaceSDFMask" => "FaceAreaMap",
                "LineMap" => "HairLineMap",
                "StrokeMap" => "HairShiftMap",
                "OutlineMask" when shaderName == "MIKU/Endfield/Face" =>
                    "FaceRefineMap",
                "OutlineMask" when shaderName == "MIKU/Endfield/Hair" =>
                    "HairRefineMap",
                _ => role,
            };
            migrated = !string.Equals(replacement, role, StringComparison.Ordinal);
            return replacement;
        }

        static HashSet<string> Set(params string[] values) =>
            new HashSet<string>(values, StringComparer.Ordinal);
    }
}
