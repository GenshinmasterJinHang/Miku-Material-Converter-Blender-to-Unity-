// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Miku.ShaderConverter.Editor
{
    /// <summary>
    /// A version-specific generator selected from the target-neutral
    /// SurfaceModelPlan. Implementations may share the Shader Graph 17.4
    /// serializer, but model selection never depends on Blender node types.
    /// </summary>
    internal interface ISurfaceGraphGenerator
    {
        string Kind { get; }
        string WrapperTemplatePath { get; }
        JObject WrapperContract(JObject materialIr);
        string GenerateSubGraph(JObject materialIr, string materialId);
    }

    internal static class MikuSurfaceModelBackends
    {
        static readonly IReadOnlyDictionary<string, ISurfaceGraphGenerator>
            Generators =
                new Dictionary<string, ISurfaceGraphGenerator>(
                    StringComparer.Ordinal)
                {
                    ["OpaquePBR"] = new ShaderGraph17SurfaceGenerator(
                        "OpaquePBR",
                        MikuWorkflowBackends.StandardWrapperTemplate),
                    ["CutoutPBR"] = new ShaderGraph17SurfaceGenerator(
                        "CutoutPBR",
                        MikuWorkflowBackends.DitheredWrapperTemplate),
                    ["TransparentLit"] = new ShaderGraph17SurfaceGenerator(
                        "TransparentLit",
                        MikuWorkflowBackends.DielectricWrapperTemplate),
                    ["TransparentEmission"] =
                        new ShaderGraph17SurfaceGenerator(
                            "TransparentEmission",
                            MikuWorkflowBackends.DielectricWrapperTemplate),
                    ["RefractiveGlass"] =
                        new ShaderGraph17SurfaceGenerator(
                            "RefractiveGlass",
                            MikuWorkflowBackends.DielectricWrapperTemplate),
                    ["CustomMultiLobe"] =
                        new ShaderGraph17SurfaceGenerator(
                            "CustomMultiLobe",
                            MikuWorkflowBackends.DielectricWrapperTemplate),
                };

        public static bool HasSurfaceModelPlan(JObject materialIr)
        {
            var documentKind = materialIr?["documentKind"]?.Value<string>();
            var validDocument = string.Equals(
                                    documentKind,
                                    "miku-material-ir-1.0",
                                    StringComparison.Ordinal) ||
                                string.Equals(
                                    documentKind,
                                    "miku-material-ir-2.0",
                                    StringComparison.Ordinal);
            return validDocument &&
                   string.Equals(
                       materialIr?["surfaceModelPlan"]?["schema"]
                           ?.Value<string>(),
                       "miku-surface-model-plan-1.0",
                       StringComparison.Ordinal);
        }

        public static ISurfaceGraphGenerator Resolve(JObject materialIr)
        {
            if (!HasSurfaceModelPlan(materialIr))
                throw new InvalidDataException(
                    "MIKU_SURFACE_MODEL_IR_VERSION_REQUIRED");
            var plan = materialIr["surfaceModelPlan"] as JObject
                ?? throw new InvalidDataException(
                    "MIKU_SURFACE_MODEL_PLAN_MISSING");
            if (!string.Equals(
                    plan["schema"]?.Value<string>(),
                    "miku-surface-model-plan-1.0",
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "MIKU_SURFACE_MODEL_PLAN_SCHEMA_INVALID");
            var kind = plan["kind"]?.Value<string>() ?? "";
            if (string.Equals(
                    kind,
                    "UnsupportedSurface",
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "MIKU_SURFACE_MODEL_UNSUPPORTED");
            if (!Generators.TryGetValue(kind, out var generator))
                throw new InvalidDataException(
                    "MIKU_SURFACE_MODEL_UNKNOWN:" + kind);
            if (RequiresClearCoat(materialIr) &&
                (string.Equals(
                     kind,
                     "OpaquePBR",
                     StringComparison.Ordinal) ||
                 string.Equals(
                     kind,
                     "CustomMultiLobe",
                     StringComparison.Ordinal)))
            {
                return new ShaderGraph17SurfaceGenerator(
                    kind,
                    MikuWorkflowBackends.ClearCoatWrapperTemplate);
            }
            return generator;
        }

        internal static bool RequiresClearCoat(JObject materialIr)
        {
            return (materialIr?["surfaceModelPlan"]?["approximations"]
                    as JArray ?? new JArray())
                .OfType<JObject>()
                .Any(item => string.Equals(
                    item["kind"]?.Value<string>(),
                    "Urp17ClearCoat",
                    StringComparison.Ordinal));
        }

        internal static bool UsesSourceMeshPbrProjection(JObject materialIr)
        {
            return (materialIr?["surfaceModelPlan"]?["approximations"]
                    as JArray ?? new JArray())
                .OfType<JObject>()
                .Any(item => string.Equals(
                    item["kind"]?.Value<string>(),
                    "SourceMeshFidelityPbrProjection",
                    StringComparison.Ordinal));
        }

        internal static bool RequiresMaterialTextureBinding(
            JObject materialIr,
            string bindingKey)
        {
            return !(
                UsesSourceMeshPbrProjection(materialIr) &&
                (bindingKey ?? "").StartsWith(
                    "_MIKU_Baked_",
                    StringComparison.Ordinal));
        }

        sealed class ShaderGraph17SurfaceGenerator :
            ISurfaceGraphGenerator
        {
            public ShaderGraph17SurfaceGenerator(
                string kind,
                string wrapperTemplatePath)
            {
                Kind = kind;
                WrapperTemplatePath = wrapperTemplatePath;
            }

            public string Kind { get; }
            public string WrapperTemplatePath { get; }

            public string GenerateSubGraph(
                JObject materialIr,
                string materialId)
            {
                var actual = materialIr["surfaceModelPlan"]?
                    ["kind"]?.Value<string>() ?? "";
                if (!string.Equals(actual, Kind, StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "MIKU_SURFACE_MODEL_BACKEND_MISMATCH:" +
                        Kind + ":" + actual);
                return MikuShaderGraph17RuntimeBackend.Generate(
                    materialIr,
                    materialId);
            }

            public JObject WrapperContract(JObject materialIr)
            {
                var plan = materialIr["surfaceModelPlan"] as JObject
                    ?? throw new InvalidDataException(
                        "MIKU_SURFACE_MODEL_PLAN_MISSING");
                var renderState = plan["renderStatePlan"] as JObject
                    ?? throw new InvalidDataException(
                        "MIKU_RENDER_STATE_PLAN_MISSING");
                var surfaceType =
                    renderState["surfaceType"]?.Value<string>() ??
                    "Opaque";
                var alphaClip =
                    renderState["alphaClip"]?.Value<bool>() ?? false;
                var blendMode =
                    renderState["blendMode"]?.Value<string>() ?? "";
                return new JObject
                {
                    ["schema"] = "miku-surface-1.0",
                    ["model"] = string.Equals(
                        Kind,
                        "RefractiveGlass",
                        StringComparison.Ordinal)
                        ? "DielectricScreenRefraction"
                        : "StandardLit",
                    ["renderMethod"] = alphaClip
                        ? "Dithered"
                        : string.Equals(
                            surfaceType,
                            "Transparent",
                            StringComparison.Ordinal)
                            ? "AlphaBlend"
                            : "Opaque",
                    ["blendMode"] = blendMode,
                    ["renderFace"] = "Front",
                    ["clearCoat"] = RequiresClearCoat(materialIr),
                    ["coverageChannel"] = "Alpha",
                    ["transmissionColorChannel"] = "TransmissionColor",
                    ["transmissionWeightChannel"] = "TransmissionWeight",
                    ["iorChannel"] = "IOR",
                    ["roughnessChannel"] = "Roughness",
                    ["normalChannel"] = "Normal",
                    ["thicknessChannel"] = "Thickness",
                };
            }
        }
    }
}
