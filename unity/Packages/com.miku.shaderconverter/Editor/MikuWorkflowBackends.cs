// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
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
        static readonly IReadOnlyDictionary<string, IMikuWorkflowBackend> Backends =
            new Dictionary<string, IMikuWorkflowBackend>(StringComparer.Ordinal)
            {
                ["standard_pbr"] = new EditableGraphBackend(
                    "standard_pbr",
                    StandardWrapperTemplate),
                ["generic_toon"] = new StaticGenericToonBackend(),
                ["genshin_toon"] = new StaticGameBackend(
                    "genshin_toon", "MIKU/Genshin/"),
                ["wuwa_toon"] = new StaticGameBackend(
                    "wuwa_toon", "MIKU/Wuwa/"),
                ["hsr_toon"] = new StaticGameBackend(
                    "hsr_toon", "MIKU/HSR/"),
            };
        static readonly HashSet<string> GameWorkflows =
            new HashSet<string>(
                new[] { "genshin_toon", "wuwa_toon", "hsr_toon" },
                StringComparer.Ordinal);
        static readonly HashSet<string> GameParts =
            new HashSet<string>(
                new[] { "Body", "Hair", "Face", "Eye", "Effect" },
                StringComparer.Ordinal);

        public static IMikuWorkflowBackend Resolve(JObject materialIr)
        {
            if (materialIr == null)
                throw new InvalidDataException("MIKU_MATERIAL_IR_MISSING");
            var isV1 = string.Equals(
                           materialIr["documentKind"]?.Value<string>(),
                           "miku-material-ir-1.0",
                           StringComparison.Ordinal) &&
                       string.Equals(
                           materialIr["schemaVersion"]?.Value<string>(),
                           "1.0",
                           StringComparison.Ordinal);
            var isV2 = MikuSurfaceModelBackends.IsMaterialIr2(materialIr);
            if (!isV1 && !isV2)
                throw new InvalidDataException("MIKU_MATERIAL_IR_SCHEMA_INVALID");
            if (materialIr["version"] != null)
                throw new InvalidDataException("MIKU_LEGACY_VERSION_FIELD");
            var workflow = materialIr["workflow"] as JObject
                ?? throw new InvalidDataException("MIKU_WORKFLOW_MISSING");
            var kind = workflow["kind"]?.Value<string>() ?? "";
            if (!Backends.TryGetValue(kind, out var backend))
                throw new InvalidDataException("MIKU_WORKFLOW_UNSUPPORTED:" + kind);
            var part = workflow["part"]?.Value<string>();
            if (GameWorkflows.Contains(kind))
            {
                if (string.IsNullOrWhiteSpace(part) || !GameParts.Contains(part))
                    throw new InvalidDataException(
                        "MIKU_WORKFLOW_PART_REQUIRED:" + kind);
            }
            else if (part != null)
            {
                throw new InvalidDataException(
                    "MIKU_WORKFLOW_PART_NOT_ALLOWED:" + kind);
            }
            if (string.Equals(kind, "standard_pbr", StringComparison.Ordinal))
            {
                var wrapperTemplate = isV2
                    ? MikuSurfaceModelBackends
                        .Resolve(materialIr)
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

        static string ResolvePart(JObject materialIr)
        {
            var value = materialIr["workflow"]?["part"]?.Value<string>() ?? "";
            return value switch
            {
                "Hair" => "Hair",
                "Face" => "Face",
                "Eye" => "Eye",
                "Effect" => "Effect",
                _ => "Body",
            };
        }

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
                var shaderName = shaderPrefix + ResolvePart(materialIr);
                var shader = Shader.Find(shaderName);
                if (shader == null && shaderName.EndsWith("/Effect", StringComparison.Ordinal))
                    shader = Shader.Find(shaderPrefix + "Body");
                return shader != null
                    ? shader
                    : throw new InvalidDataException(
                        "MIKU_WORKFLOW_SHADER_MISSING:" + shaderName);
            }
        }

        sealed class StaticGenericToonBackend : IMikuWorkflowBackend
        {
            public string Kind => "generic_toon";
            public bool UsesEditableGraph => false;
            public string WrapperTemplatePath => string.Empty;

            public Shader ResolveShader(
                JObject materialIr,
                Shader editableGraphShader)
            {
                const string shaderName =
                    "Miku/GenericToon/GenericOpaque";
                return Shader.Find(shaderName)
                    ?? throw new InvalidDataException(
                        "MIKU_WORKFLOW_SHADER_MISSING:" + shaderName);
            }
        }
    }
}
