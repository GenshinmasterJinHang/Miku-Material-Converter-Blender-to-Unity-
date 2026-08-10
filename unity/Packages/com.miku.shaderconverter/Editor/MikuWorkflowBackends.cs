// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    internal interface IMikuWorkflowBackend
    {
        string Kind { get; }
        bool UsesEditableGraph { get; }
        string WrapperTemplatePath { get; }
        Shader ResolveShader(JObject materialIr, Shader editableGraphShader);
    }

    internal static class MikuWorkflowBackends
    {
        internal const string StandardWrapperTemplate =
            "Packages/com.miku.shaderconverter/Templates/MikuStandardTemplate.shadergraph";
        internal const string ClearCoatWrapperTemplate =
            "Packages/com.miku.shaderconverter/Templates/MikuClearCoatTemplate.shadergraph";
        internal const string AlphaBlendWrapperTemplate =
            "Packages/com.miku.shaderconverter/Templates/MikuAlphaBlendTemplate.shadergraph";
        internal const string DitheredWrapperTemplate =
            "Packages/com.miku.shaderconverter/Templates/MikuDitheredTemplate.shadergraph";
        internal const string DielectricWrapperTemplate =
            "Packages/com.miku.shaderconverter/Templates/MikuDielectricTemplate.shadergraph";
        static readonly IReadOnlyDictionary<string, string> TemplateHashes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [StandardWrapperTemplate] = "c7c9b5ed0c068208d251d9b8d058a7175d5c1c2930bc6062f05a8e43463dfee6",
                [ClearCoatWrapperTemplate] = "22f41abb88fd47efcfb57fce41abab77301a7f1db216bbf9c3d640e8ec8c24d6",
                [AlphaBlendWrapperTemplate] = "8b959dfeb5d1f684b897c074647078789fc6fcd3113ee178374d9aaf86e5f845",
                [DitheredWrapperTemplate] = "c51786632f1b712e8e25060357940b5daa127942346d9f093c251df282cb7d2a",
                [DielectricWrapperTemplate] = "8694e3bb49c2ce279aff157ce722ec15c4e7793b970e64cb8100cd2c409d67df",
            };
        static readonly HashSet<string> PreflightedTemplateVersions =
            new HashSet<string>(StringComparer.Ordinal);
        static readonly IReadOnlyDictionary<string, IMikuWorkflowBackend> Backends =
            new Dictionary<string, IMikuWorkflowBackend>(StringComparer.Ordinal)
            {
                ["standard_pbr"] = new EditableGraphBackend(
                    "standard_pbr",
                    StandardWrapperTemplate),
                ["genshin_toon"] = new StaticGameBackend(
                    "genshin_toon", "MIKU/Genshin/"),
                ["wuwa_toon"] = new StaticGameBackend(
                    "wuwa_toon", "MIKU/Wuwa/"),
                ["hsr_toon"] = new StaticGameBackend(
                    "hsr_toon", "MIKU/HSR/"),
                ["endfield_toon"] = new StaticGameBackend(
                    "endfield_toon", "MIKU/Endfield/"),
            };
        static readonly HashSet<string> GameWorkflows =
            new HashSet<string>(
                new[]
                {
                    "genshin_toon", "wuwa_toon", "hsr_toon",
                    "endfield_toon",
                },
                StringComparer.Ordinal);
        static readonly IReadOnlyDictionary<string, HashSet<string>> GameParts =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["genshin_toon"] = Parts("Body", "Hair", "Face", "Eye"),
                ["wuwa_toon"] = Parts(
                    "Body", "Hair", "Face", "Eye", "Effect"),
                ["hsr_toon"] = Parts("Body", "Hair", "Face", "Eye"),
                ["endfield_toon"] = Parts(
                    "Body", "Skin", "Hair", "Face", "Eye", "Mouth",
                    "Overlay", "Effect", "HairShadow"),
            };

        internal static void PreflightTemplates(string shaderGraphVersion)
        {
            if (PreflightedTemplateVersions.Contains(shaderGraphVersion))
                return;
            var version = MikuPackageVersion.Parse(shaderGraphVersion);
            if (version.Major != 17 ||
                version.Minor < 0 ||
                version.Minor > 5 ||
                version.Prerelease)
                throw new InvalidDataException(
                    "MIKU_SHADERGRAPH_VERSION_UNSUPPORTED:" +
                    shaderGraphVersion + ":supported=17.0-17.5 stable");
            foreach (var item in TemplateHashes)
            {
                var package = UnityEditor.PackageManager.PackageInfo
                    .FindForAssetPath(item.Key);
                if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath))
                    throw new InvalidDataException(
                        "MIKU_SHADERGRAPH_TEMPLATE_PACKAGE_MISSING:" +
                        item.Key);
                const string packagePrefix =
                    "Packages/com.miku.shaderconverter/";
                if (!item.Key.StartsWith(
                        packagePrefix,
                        StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "MIKU_SHADERGRAPH_TEMPLATE_PATH_INVALID:" +
                        item.Key);
                var absolute = Path.Combine(
                    package.resolvedPath,
                    item.Key.Substring(packagePrefix.Length)
                        .Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolute))
                    throw new FileNotFoundException(
                        "MIKU_SHADERGRAPH_TEMPLATE_MISSING:" + item.Key,
                        absolute);
                using var sha256 = SHA256.Create();
                var actual = string.Concat(
                    sha256.ComputeHash(File.ReadAllBytes(absolute))
                        .Select(value => value.ToString("x2")));
                if (!string.Equals(actual, item.Value, StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "MIKU_SHADERGRAPH_TEMPLATE_IDENTITY_MISMATCH:" +
                        item.Key + ":expected=" + item.Value +
                        ":actual=" + actual);
                if (AssetDatabase.LoadAssetAtPath<Shader>(item.Key) == null)
                    throw new InvalidDataException(
                        "MIKU_SHADERGRAPH_TEMPLATE_IMPORT_FAILED:" +
                        shaderGraphVersion + ":" + item.Key);
            }
            PreflightedTemplateVersions.Add(shaderGraphVersion);
        }

        public static IMikuWorkflowBackend Resolve(JObject materialIr)
        {
            if (materialIr == null)
                throw new InvalidDataException("MIKU_MATERIAL_IR_MISSING");
            var documentKind = materialIr["documentKind"]?.Value<string>();
            var isV1 = string.Equals(
                documentKind,
                "miku-material-ir-1.0",
                StringComparison.Ordinal);
            var isV2 = string.Equals(
                documentKind,
                "miku-material-ir-2.0",
                StringComparison.Ordinal);
            if ((!isV1 && !isV2) ||
                !string.Equals(
                    materialIr["schemaVersion"]?.Value<string>(),
                    isV2 ? "2.0" : "1.0",
                    StringComparison.Ordinal))
                throw new InvalidDataException("MIKU_MATERIAL_IR_SCHEMA_INVALID");
            if (materialIr["version"] != null)
                throw new InvalidDataException("MIKU_LEGACY_VERSION_FIELD");
            var workflow = materialIr["workflow"] as JObject
                ?? throw new InvalidDataException("MIKU_WORKFLOW_MISSING");
            var kind = workflow["kind"]?.Value<string>() ?? "";
            if (string.Equals(kind, "generic_toon", StringComparison.Ordinal))
                throw new InvalidDataException(
                    "MIKU_WORKFLOW_RETIRED:generic_toon");
            if (!Backends.TryGetValue(kind, out var backend))
                throw new InvalidDataException("MIKU_WORKFLOW_UNSUPPORTED:" + kind);
            var part = workflow["part"]?.Value<string>();
            if (GameWorkflows.Contains(kind))
            {
                if (string.IsNullOrWhiteSpace(part))
                    throw new InvalidDataException(
                        "MIKU_WORKFLOW_PART_REQUIRED:" + kind);
                if (!GameParts.TryGetValue(kind, out var allowedParts) ||
                    !allowedParts.Contains(part))
                    throw new InvalidDataException(
                        "MIKU_WORKFLOW_PART_INVALID:" + kind + ":" + part);
            }
            else if (part != null)
            {
                throw new InvalidDataException(
                    "MIKU_WORKFLOW_PART_NOT_ALLOWED:" + kind);
            }
            if (string.Equals(kind, "standard_pbr", StringComparison.Ordinal))
            {
                var wrapperTemplate = isV2
                    ? MikuSurfaceModelBackends.HasSurfaceModelPlan(materialIr)
                        ? MikuSurfaceModelBackends.Resolve(materialIr)
                            .WrapperTemplatePath
                        : StandardWrapperTemplate
                    : MikuSurfaceModelBackends.HasSurfaceModelPlan(materialIr)
                        ? MikuSurfaceModelBackends.Resolve(materialIr)
                            .WrapperTemplatePath
                        : ResolveSurfaceContract(materialIr);
                return new EditableGraphBackend(kind, wrapperTemplate);
            }
            return backend;
        }

        static string ResolveSurfaceContract(JObject materialIr)
        {
            if (!(materialIr["surfaceContract"] is JObject contract))
                return StandardWrapperTemplate;
            var allowed = new HashSet<string>(
                new[]
                {
                    "schema",
                    "model",
                    "renderMethod",
                    "renderFace",
                    "coverageChannel",
                    "transmissionColorChannel",
                    "transmissionWeightChannel",
                    "iorChannel",
                    "roughnessChannel",
                    "normalChannel",
                    "thicknessChannel",
                },
                StringComparer.Ordinal);
            foreach (var property in contract.Properties())
            {
                if (!allowed.Contains(property.Name))
                    throw new InvalidDataException(
                        "MIKU_SURFACE_CONTRACT_FIELD_UNKNOWN:" + property.Name);
            }
            RequireSurfaceEnum(
                contract,
                "schema",
                new[] { "miku-surface-1.0" });
            var model = RequireSurfaceEnum(
                contract,
                "model",
                new[] { "StandardLit", "DielectricScreenRefraction" });
            var renderMethod = RequireSurfaceEnum(
                contract,
                "renderMethod",
                new[] { "Opaque", "AlphaBlend", "Dithered" });
            RequireSurfaceEnum(
                contract,
                "renderFace",
                new[] { "Front", "Back", "Both" });

            var channels = (materialIr["channels"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Where(item => item["semantic"]?.Type == JTokenType.String)
                .ToDictionary(
                    item => item["semantic"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            RequireSurfaceChannel(contract, channels, "coverageChannel", true);
            if (string.Equals(
                    model,
                    "DielectricScreenRefraction",
                    StringComparison.Ordinal))
            {
                foreach (var field in new[]
                         {
                             "transmissionColorChannel",
                             "transmissionWeightChannel",
                             "iorChannel",
                             "roughnessChannel",
                             "normalChannel",
                             "thicknessChannel",
                         })
                    RequireSurfaceChannel(contract, channels, field, false);
                return DielectricWrapperTemplate;
            }
            if (string.Equals(
                    renderMethod,
                    "AlphaBlend",
                    StringComparison.Ordinal))
                return AlphaBlendWrapperTemplate;
            if (string.Equals(
                    renderMethod,
                    "Dithered",
                    StringComparison.Ordinal))
                return DitheredWrapperTemplate;
            return StandardWrapperTemplate;
        }

        static string RequireSurfaceEnum(
            JObject contract,
            string field,
            IEnumerable<string> allowed)
        {
            var value = contract[field]?.Value<string>();
            if (string.IsNullOrWhiteSpace(value) ||
                !allowed.Contains(value, StringComparer.Ordinal))
                throw new InvalidDataException(
                    "MIKU_SURFACE_CONTRACT_INVALID:" + field);
            return value;
        }

        static void RequireSurfaceChannel(
            JObject contract,
            IReadOnlyDictionary<string, JObject> channels,
            string field,
            bool scalar)
        {
            var semantic = contract[field]?.Value<string>();
            if (string.IsNullOrWhiteSpace(semantic) ||
                !channels.TryGetValue(semantic, out var channel))
                throw new InvalidDataException(
                    "MIKU_SURFACE_CONTRACT_CHANNEL_MISSING:" + field);
            if (scalar &&
                !string.Equals(
                    channel["valueType"]?.Value<string>(),
                    "Scalar",
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "MIKU_SURFACE_CONTRACT_COVERAGE_NOT_SCALAR:" + semantic);
        }

        static string ResolvePart(string workflow, JObject materialIr)
        {
            var value = materialIr["workflow"]?["part"]?.Value<string>() ?? "";
            if (!GameParts.TryGetValue(workflow, out var allowedParts) ||
                !allowedParts.Contains(value))
                throw new InvalidDataException(
                    "MIKU_WORKFLOW_PART_INVALID:" + workflow + ":" + value);
            return value;
        }

        static HashSet<string> Parts(params string[] values) =>
            new HashSet<string>(values, StringComparer.Ordinal);

        sealed class EditableGraphBackend : IMikuWorkflowBackend
        {
            public EditableGraphBackend(string kind, string wrapperTemplatePath)
            {
                Kind = kind;
                WrapperTemplatePath = wrapperTemplatePath;
            }

            public string Kind { get; }
            public bool UsesEditableGraph => true;
            public string WrapperTemplatePath { get; }

            public Shader ResolveShader(JObject materialIr, Shader editableGraphShader)
            {
                return editableGraphShader != null
                    ? editableGraphShader
                    : throw new InvalidDataException(
                        "MIKU_EDITABLE_SHADERGRAPH_MISSING:" + Kind);
            }
        }

        sealed class StaticGameBackend : IMikuWorkflowBackend
        {
            readonly string shaderPrefix;

            public StaticGameBackend(string kind, string shaderPrefix)
            {
                Kind = kind;
                this.shaderPrefix = shaderPrefix;
            }

            public string Kind { get; }
            public bool UsesEditableGraph => false;
            public string WrapperTemplatePath => StandardWrapperTemplate;

            public Shader ResolveShader(JObject materialIr, Shader editableGraphShader)
            {
                var shaderName = shaderPrefix + ResolvePart(Kind, materialIr);
                var shader = Shader.Find(shaderName);
                return shader != null
                    ? shader
                    : throw new InvalidDataException(
                        "MIKU_WORKFLOW_SHADER_MISSING:" + shaderName);
            }
        }

    }
}
