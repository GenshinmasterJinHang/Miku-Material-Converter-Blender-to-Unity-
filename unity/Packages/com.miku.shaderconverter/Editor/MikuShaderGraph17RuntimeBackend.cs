using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    /// <summary>
    /// Shader Graph 17.4 structured Sub Graph backend for MaterialIR 1.x/2.x
    /// value expressions and closure-aware surface models. All Shader Graph
    /// internal API access is isolated in the nested version adapter.
    /// </summary>
    internal static class MikuShaderGraph17RuntimeBackend
    {
        internal const string TimeScaleReference = "_MIKU_EffectTimeScale";
        internal const string TimeOffsetReference = "_MIKU_EffectTimeOffset";
        internal const string TimeOverrideReference = "_MIKU_EffectTimeOverride";
        internal const string UseTimeOverrideReference = "_MIKU_EffectUseTimeOverride";

        sealed class Handle
        {
            public object node;
            public int slot;
        }

        sealed class Builder
        {
            readonly JObject ir;
            readonly string materialId;
            readonly ShaderGraph17_4Adapter adapter = new ShaderGraph17_4Adapter();
            readonly Dictionary<string, JObject> expressions;
            readonly Dictionary<string, Handle> built =
                new Dictionary<string, Handle>(StringComparer.Ordinal);
            readonly HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            readonly Dictionary<string, Handle> properties =
                new Dictionary<string, Handle>(StringComparer.Ordinal);
            readonly Dictionary<string, Handle> imageSamples =
                new Dictionary<string, Handle>(StringComparer.Ordinal);
            object graph;
            object output;
            int positionIndex;
            Handle effectSeconds;
            IDictionary<string, JObject> channelsBySemantic;

            public Builder(JObject ir, string materialId)
            {
                this.ir = ir ?? throw new ArgumentNullException(nameof(ir));
                this.materialId = materialId ?? "material";
                expressions = (ir["expressions"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .ToDictionary(
                        item => item["id"]?.Value<string>() ?? "",
                        item => item,
                        StringComparer.Ordinal);
            }

            public string Build()
            {
                graph = adapter.CreateSubGraph(materialId);
                output = adapter.GetOutput(graph);
                var channels = (ir["channels"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .ToDictionary(
                        item => item["semantic"]?.Value<string>() ?? "",
                        item => item,
                        StringComparer.Ordinal);
                channelsBySemantic = channels;
                var surface = ir["surfaceContract"] as JObject;
                var surfacePlan = ir["surfaceModelPlan"] as JObject;
                var surfaceKind =
                    surfacePlan?["kind"]?.Value<string>() ?? "";
                if (string.Equals(
                        surfaceKind,
                        "UnsupportedSurface",
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "MIKU_SURFACE_MODEL_UNSUPPORTED");
                if (string.Equals(
                        surfaceKind,
                        "RefractiveGlass",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        surface?["model"]?.Value<string>(),
                        "DielectricScreenRefraction",
                        StringComparison.Ordinal))
                    BuildDielectric(channels, surface);
                else if (
                    string.Equals(
                        surfaceKind,
                        "TransparentEmission",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        surfaceKind,
                        "TransparentLit",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        surfaceKind,
                        "CustomMultiLobe",
                        StringComparison.Ordinal))
                    BuildClosureComposite(channels, surfacePlan);
                else
                    BuildStandard(channels, surface);
                AddVertexPositionOutput(channels);
                ValidateBakedResourceReachability();
                return adapter.Serialize(graph);
            }

            void AddVertexPositionOutput(
                IDictionary<string, JObject> channels)
            {
                var position = Native(
                    "vertex-position:object",
                    "PositionNode",
                    0,
                    "Object");
                if (channels.TryGetValue("Displacement", out var channel) &&
                    channel["value"] is JObject value &&
                    string.Equals(
                        value["kind"]?.Value<string>(),
                        "Expression",
                        StringComparison.Ordinal))
                    position = BuildExpression(
                        value["expressionId"]?.Value<string>() ?? "");
                AddOutput("Vertex Position", "Vector3", position);
            }

            void BuildStandard(
                IDictionary<string, JObject> channels,
                JObject surface)
            {
                var baseColor = ResolveChannel(
                    channels,
                    "BaseColor",
                    new JArray(0.8f, 0.8f, 0.8f));
                var ambientOcclusion = ResolveChannel(
                    channels,
                    "AmbientOcclusion",
                    new JValue(1f));
                var occlusionStrength = Property(
                    "occlusion-strength",
                    "Occlusion Strength",
                    "_OcclusionStrength",
                    new JValue(1f));
                var effectiveOcclusion = Lerp(
                    "base-color-ambient-occlusion",
                    Literal(
                        "base-color-ambient-occlusion-neutral",
                        new JValue(1f),
                        "Scalar"),
                    ambientOcclusion,
                    occlusionStrength);
                AddOutput(
                    "Base Color",
                    "Vector3",
                    Binary(
                        "base-color-with-ambient-occlusion",
                        "MultiplyNode",
                        baseColor,
                        effectiveOcclusion));
                AddChannel(channels, "Metalness", "Metallic", "Vector1", new JValue(0f));
                var roughness = ResolveChannel(channels, "Roughness", new JValue(0.5f));
                AddOutput("Smoothness", "Vector1", Unary("smoothness", "OneMinusNode", roughness));
                AddChannel(channels, "Normal", "Normal TS", "Vector3", new JArray(0f, 0f, 1f));
                AddChannel(channels, "Emission", "Emission", "Vector3", new JArray(0f, 0f, 0f));
                AddOutput(
                    "Occlusion",
                    "Vector1",
                    Literal(
                        "occlusion-neutral-after-base-color",
                        new JValue(1f),
                        "Scalar"));
                var coverage = ResolveChannel(channels, "Alpha", new JValue(1f));
                var renderMethod =
                    surface?["renderMethod"]?.Value<string>() ?? "Opaque";
                var opacity = Property(
                    "surface-opacity",
                    "Opacity",
                    "_Opacity",
                    new JValue(1f));
                if (!string.Equals(renderMethod, "Opaque", StringComparison.Ordinal))
                    coverage = Binary(
                        "surface-opacity",
                        "MultiplyNode",
                        coverage,
                        opacity);
                if (string.Equals(renderMethod, "Dithered", StringComparison.Ordinal))
                    coverage = Dither("surface-dither", coverage);
                AddOutput("Alpha", "Vector1", coverage);
                AddOutput(
                    "Alpha Clip Threshold",
                    "Vector1",
                    Property(
                        "alpha-clip-threshold",
                        "Alpha Clip Threshold",
                        "_AlphaClipThreshold",
                        new JValue(
                            string.Equals(
                                renderMethod,
                                "Dithered",
                                StringComparison.Ordinal)
                                ? 0f
                                : 0.5f)));
                if (MikuSurfaceModelBackends.RequiresClearCoat(ir))
                    AddClearCoatOutputs();
            }

            void AddClearCoatOutputs()
            {
                var terms = (ir["weightedClosures"]?["terms"] as JArray ??
                             new JArray())
                    .OfType<JObject>()
                    .Where(item => string.Equals(
                        item["closureKind"]?.Value<string>(),
                        "Principled",
                        StringComparison.Ordinal))
                    .OrderBy(
                        item => item["id"]?.Value<string>() ?? "",
                        StringComparer.Ordinal)
                    .ToArray();
                if (terms.Length == 0)
                    throw new InvalidOperationException(
                        "MIKU_COAT_PRINCIPLED_TERM_MISSING");

                Handle coatMask = null;
                Handle coatSmoothnessNumerator = null;
                foreach (var term in terms)
                {
                    var termId = term["id"]?.Value<string>() ?? "term";
                    var weight = BuildWeight(
                        term["finalWeight"] as JObject,
                        termId + ":coat-weight");
                    var termCoatMask = ClosureParameter(
                        term,
                        new[] { "Coat Weight", "Coat_Weight" },
                        new JValue(0f),
                        "Scalar");
                    var termCoatRoughness = ClosureParameter(
                        term,
                        new[] { "Coat Roughness", "Coat_Roughness" },
                        new JValue(0.03f),
                        "Scalar");
                    var weightedMask = Binary(
                        termId + ":coat-mask-weighted",
                        "MultiplyNode",
                        termCoatMask,
                        weight);
                    var weightedSmoothness = Binary(
                        termId + ":coat-smoothness-weighted",
                        "MultiplyNode",
                        Unary(
                            termId + ":coat-smoothness",
                            "OneMinusNode",
                            termCoatRoughness),
                        weightedMask);
                    coatMask = coatMask == null
                        ? weightedMask
                        : Binary(
                            termId + ":coat-mask-sum",
                            "AddNode",
                            coatMask,
                            weightedMask);
                    coatSmoothnessNumerator = coatSmoothnessNumerator == null
                        ? weightedSmoothness
                        : Binary(
                            termId + ":coat-smoothness-sum",
                            "AddNode",
                            coatSmoothnessNumerator,
                            weightedSmoothness);
                }
                var safeCoatDenominator = Binary(
                    "coat-smoothness-denominator",
                    "MaximumNode",
                    coatMask,
                    Literal(
                        "coat-smoothness-epsilon",
                        new JValue(0.0001f),
                        "Scalar"));
                var normalizedCoatSmoothness = Binary(
                    "coat-smoothness-average",
                    "DivideNode",
                    coatSmoothnessNumerator,
                    safeCoatDenominator);
                normalizedCoatSmoothness = Binary(
                    "coat-smoothness-safe-maximum",
                    "MinimumNode",
                    normalizedCoatSmoothness,
                    Literal(
                        "coat-smoothness-safe-limit",
                        new JValue(0.999f),
                        "Scalar"));
                AddOutput(
                    "Coat Mask",
                    "Vector1",
                    Unary(
                        "coat-mask-saturate",
                        "SaturateNode",
                        coatMask));
                AddOutput(
                    "Coat Smoothness",
                    "Vector1",
                    Unary(
                        "coat-smoothness-saturate",
                        "SaturateNode",
                        normalizedCoatSmoothness));
            }

            void BuildClosureComposite(
                IDictionary<string, JObject> channels,
                JObject surfacePlan)
            {
                var terms = (ir["weightedClosures"]?["terms"] as JArray ??
                             new JArray())
                    .OfType<JObject>()
                    .OrderBy(
                        item => item["id"]?.Value<string>() ?? "",
                        StringComparer.Ordinal)
                    .ToArray();
                if (terms.Length == 0)
                    throw new InvalidOperationException(
                        "MIKU_WEIGHTED_CLOSURES_MISSING");

                var normalTs = ResolveChannel(
                    channels,
                    "Normal",
                    new JArray(0f, 0f, 1f));
                var geometricNormalWs = Native(
                    "closure-geometric-normal-ws",
                    "NormalVectorNode",
                    0,
                    "World");
                var positionWs = Native(
                    "closure-position-ws",
                    "PositionNode",
                    0,
                    "World");
                var viewWs = Native(
                    "closure-view-ws",
                    "ViewDirectionNode",
                    0,
                    "World");
                var screen = Native(
                    "closure-screen-position",
                    "ScreenPositionNode",
                    0);
                var radiance = Literal(
                    "closure-radiance-zero",
                    new JArray(0f, 0f, 0f),
                    "Float3");
                var scalarTransmittance = Literal(
                    "closure-transmittance-zero",
                    new JValue(0f),
                    "Scalar");
                var coloredBackground = Literal(
                    "closure-background-zero",
                    new JArray(0f, 0f, 0f),
                    "Float3");
                var transparentComposite =
                    surfacePlan?["transparentCompositePlan"] as JObject;
                var coloredComposite = string.Equals(
                    transparentComposite?
                        ["transmittanceKind"]?.Value<string>(),
                    "Colored",
                    StringComparison.Ordinal);
                Handle sceneColor = null;

                foreach (var term in terms)
                {
                    var termId = term["id"]?.Value<string>() ?? "term";
                    var domain = term["domain"]?.Value<string>() ?? "";
                    var weight = BuildWeight(
                        term["finalWeight"] as JObject,
                        termId + ":weight");
                    if (string.Equals(
                            domain,
                            "Emission",
                            StringComparison.Ordinal))
                    {
                        var emissionColor = ClosureParameter(
                            term,
                            new[] { "Color" },
                            new JArray(0f, 0f, 0f, 1f),
                            "Color");
                        var strength = ClosureParameter(
                            term,
                            new[] { "Strength" },
                            new JValue(1f),
                            "Scalar");
                        radiance = Binary(
                            termId + ":emission-sum",
                            "AddNode",
                            radiance,
                            Binary(
                                termId + ":emission-weight",
                                "MultiplyNode",
                                Binary(
                                    termId + ":emission-strength",
                                    "MultiplyNode",
                                    emissionColor,
                                    strength),
                                weight));
                        continue;
                    }
                    if (string.Equals(
                            domain,
                            "TransparentPassThrough",
                            StringComparison.Ordinal))
                    {
                        if (coloredComposite)
                        {
                            if (sceneColor == null)
                            {
                                var sceneNode = adapter.CreateNode(
                                    graph,
                                    materialId,
                                    "closure-scene-color",
                                    "SceneColorNode",
                                    Position());
                                adapter.Connect(
                                    graph,
                                    screen.node,
                                    screen.slot,
                                    sceneNode,
                                    0);
                                sceneColor = new Handle
                                {
                                    node = sceneNode,
                                    slot = 1,
                                };
                            }
                            var tint = ClosureParameter(
                                term,
                                new[] { "Color" },
                                new JArray(1f, 1f, 1f, 1f),
                                "Color");
                            coloredBackground = Binary(
                                termId + ":background-sum",
                                "AddNode",
                                coloredBackground,
                                Binary(
                                    termId + ":background-weight",
                                    "MultiplyNode",
                                    Binary(
                                        termId + ":background-tint",
                                        "MultiplyNode",
                                        sceneColor,
                                        tint),
                                    weight));
                        }
                        else
                        {
                            scalarTransmittance = Binary(
                                termId + ":transmittance-sum",
                                "AddNode",
                                scalarTransmittance,
                                weight);
                        }
                        continue;
                    }
                    if (string.Equals(
                            domain,
                            "SurfaceScattering",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            domain,
                            "SurfaceTransmission",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            domain,
                            "Refraction",
                            StringComparison.Ordinal))
                    {
                        radiance = Binary(
                            termId + ":lobe-sum",
                            "AddNode",
                            radiance,
                            EvaluateLobe(
                                term,
                                positionWs,
                                geometricNormalWs,
                                viewWs,
                                screen,
                                weight));
                        continue;
                    }
                    if (!string.Equals(
                            domain,
                            "Refraction",
                            StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "MIKU_CLOSURE_DOMAIN_UNSUPPORTED:" + domain);
                }

                Handle coverage;
                if (coloredComposite)
                {
                    radiance = Binary(
                        "closure-colored-composite",
                        "AddNode",
                        radiance,
                        coloredBackground);
                    coverage = Literal(
                        "closure-colored-composite-alpha",
                        new JValue(1f),
                        "Scalar");
                }
                else
                {
                    coverage = Unary(
                        "closure-alpha-saturate",
                        "SaturateNode",
                        Binary(
                            "closure-one-minus-transmittance",
                            "SubtractNode",
                            Literal(
                                "closure-alpha-one",
                                new JValue(1f),
                                "Scalar"),
                            scalarTransmittance));
                }

                var usesLitClearCoatWrapper =
                    MikuSurfaceModelBackends.RequiresClearCoat(ir);
                var zeroRadiance = Literal(
                    "closure-output-zero",
                    new JArray(0f, 0f, 0f),
                    "Float3");
                AddOutput(
                    "Base Color",
                    "Vector3",
                    usesLitClearCoatWrapper ? zeroRadiance : radiance);
                AddOutput(
                    "Metallic",
                    "Vector1",
                    Literal(
                        "closure-metallic-zero",
                        new JValue(0f),
                        "Scalar"));
                AddOutput(
                    "Smoothness",
                    "Vector1",
                    Literal(
                        "closure-smoothness-zero",
                        new JValue(0f),
                        "Scalar"));
                AddOutput(
                    "Normal TS",
                    "Vector3",
                    normalTs);
                AddOutput(
                    "Emission",
                    "Vector3",
                    usesLitClearCoatWrapper ? radiance : zeroRadiance);
                AddOutput(
                    "Occlusion",
                    "Vector1",
                    Literal(
                        "closure-occlusion-one",
                        new JValue(1f),
                        "Scalar"));
                AddOutput("Alpha", "Vector1", coverage);
                AddOutput(
                    "Alpha Clip Threshold",
                    "Vector1",
                    Literal(
                        "closure-alpha-clip-zero",
                        new JValue(0f),
                        "Scalar"));
                if (usesLitClearCoatWrapper)
                    AddClearCoatOutputs();
            }

            Handle EvaluateLobe(
                JObject term,
                Handle positionWs,
                Handle normalWs,
                Handle viewWs,
                Handle screen,
                Handle weight)
            {
                var termId = term["id"]?.Value<string>() ?? "term";
                var kind = term["closureKind"]?.Value<string>() ?? "";
                var kindValue = kind switch
                {
                    "Diffuse" => 0f,
                    "Glossy" => 1f,
                    "Metallic" => 2f,
                    "Principled" => 3f,
                    "SubsurfaceScattering" => 0f,
                    "Translucent" => 0f,
                    "Glass" => 1f,
                    "Refraction" => 1f,
                    _ => throw new InvalidOperationException(
                        "MIKU_CLOSURE_LOBE_UNSUPPORTED:" + kind),
                };
                var baseColor = ClosureParameter(
                    term,
                    new[] { "Base Color", "Base_Color", "Color" },
                    new JArray(0.8f, 0.8f, 0.8f, 1f),
                    "Color");
                var roughness = ClosureParameter(
                    term,
                    new[] { "Roughness" },
                    new JValue(0.5f),
                    "Scalar");
                var metallic = ClosureParameter(
                    term,
                    new[] { "Metallic" },
                    new JValue(kindValue == 2f ? 1f : 0f),
                    "Scalar");
                normalWs = SafeLobeNormalWorld(
                    termId,
                    ClosureNormalWorld(term, normalWs),
                    normalWs);
                var lobeKind = Literal(
                    termId + ":lobe-kind",
                    new JValue(kindValue),
                    "Scalar");
                var node = adapter.CreateMultiLobeNode(
                    graph,
                    materialId,
                    termId,
                    Position());
                var inputs = new[]
                {
                    positionWs,
                    normalWs,
                    viewWs,
                    screen,
                    baseColor,
                    roughness,
                    metallic,
                    lobeKind,
                    weight,
                };
                for (var index = 0; index < inputs.Length; index++)
                    adapter.Connect(
                        graph,
                        inputs[index].node,
                        inputs[index].slot,
                        node,
                        index);
                return new Handle { node = node, slot = 9 };
            }

            Handle SafeLobeNormalWorld(
                string termId,
                Handle lobeNormalWs,
                Handle geometricNormalWs)
            {
                var lengthSquared = Binary(
                    termId + ":normal-length-squared",
                    "DotProductNode",
                    lobeNormalWs,
                    lobeNormalWs);
                var nonZero = Compare(
                    termId + ":normal-nonzero",
                    "Greater",
                    lengthSquared,
                    Literal(
                        termId + ":normal-epsilon",
                        new JValue(0.0001f),
                        "Scalar"));
                var finiteMagnitude = Compare(
                    termId + ":normal-finite-magnitude",
                    "Less",
                    lengthSquared,
                    Literal(
                        termId + ":normal-maximum-length-squared",
                        new JValue(100000000f),
                        "Scalar"));
                var valid = Binary(
                    termId + ":normal-valid",
                    "AndNode",
                    nonZero,
                    finiteMagnitude);
                return Unary(
                    termId + ":normal-safe-normalize",
                    "NormalizeNode",
                    Branch(
                        termId + ":normal-fallback",
                        valid,
                        lobeNormalWs,
                        geometricNormalWs));
            }

            Handle ClosureNormalWorld(
                JObject term,
                Handle geometricNormalWs)
            {
                var parameter = FindClosureParameter(
                    term,
                    new[] { "Normal" });
                if (parameter == null)
                    return geometricNormalWs;

                var termId = term["id"]?.Value<string>() ?? "term";
                var role = termId + ":parameter:normal";
                if (string.Equals(
                        parameter["kind"]?.Value<string>(),
                        "Constant",
                        StringComparison.Ordinal))
                {
                    var value = parameter["value"];
                    if (IsLegacyZeroNormal(value) || IsNeutralNormal(value))
                        return geometricNormalWs;
                    return TransformNormal(
                        role + ":tangent-to-world",
                        Literal(
                            role,
                            value ?? new JArray(0f, 0f, 1f),
                            parameter["valueType"]?.Value<string>() ??
                            "Float3"),
                        "Tangent",
                        "World");
                }

                var expressionId =
                    parameter["expressionId"]?.Value<string>() ?? "";
                if (string.IsNullOrEmpty(expressionId))
                    throw new InvalidOperationException(
                        "MIKU_CLOSURE_PARAMETER_REQUIRES_BAKE:" + role);
                if (!expressions.TryGetValue(expressionId, out var expression))
                    throw new InvalidOperationException(
                        "MIKU_EXPRESSION_REFERENCE_MISSING:" + expressionId);

                var normal = BuildExpression(expressionId);
                var space =
                    parameter["space"]?.Value<string>() ??
                    expression["space"]?.Value<string>() ??
                    "Tangent";
                if (string.Equals(space, "World", StringComparison.Ordinal) ||
                    string.Equals(
                        space,
                        "AbsoluteWorld",
                        StringComparison.Ordinal))
                    return normal;
                if (string.Equals(space, "Tangent", StringComparison.Ordinal) ||
                    string.Equals(space, "Object", StringComparison.Ordinal))
                    return TransformNormal(
                        role + ":" + space.ToLowerInvariant() + "-to-world",
                        normal,
                        space,
                        "World");
                throw new InvalidOperationException(
                    "MIKU_CLOSURE_NORMAL_SPACE_UNSUPPORTED:" + space);
            }

            Handle ClosureParameter(
                JObject term,
                IEnumerable<string> aliases,
                JToken fallback,
                string valueType)
            {
                var normalized = new HashSet<string>(
                    aliases.Select(NormalizeSemanticName),
                    StringComparer.Ordinal);
                var parameter = FindClosureParameter(term, aliases);
                if (parameter == null)
                    return Literal(
                        (term["id"]?.Value<string>() ?? "term") +
                        ":parameter:" + normalized.First(),
                        fallback,
                        valueType);
                var role =
                    (term["id"]?.Value<string>() ?? "term") +
                    ":parameter:" + normalized.First();
                if (string.Equals(
                        parameter["kind"]?.Value<string>(),
                        "Constant",
                        StringComparison.Ordinal))
                    return Literal(
                        role,
                        parameter["value"] ?? fallback,
                        parameter["valueType"]?.Value<string>() ??
                        valueType);
                var expressionId =
                    parameter["expressionId"]?.Value<string>() ?? "";
                if (!string.IsNullOrEmpty(expressionId))
                    return BuildExpression(expressionId);
                throw new InvalidOperationException(
                    "MIKU_CLOSURE_PARAMETER_REQUIRES_BAKE:" + role);
            }

            static JObject FindClosureParameter(
                JObject term,
                IEnumerable<string> aliases)
            {
                var normalized = new HashSet<string>(
                    aliases.Select(NormalizeSemanticName),
                    StringComparer.Ordinal);
                return (term["parameters"] as JObject)?
                    .Properties()
                    .FirstOrDefault(item => normalized.Contains(
                        NormalizeSemanticName(item.Name)))
                    ?.Value as JObject;
            }

            Handle BuildWeight(JObject expression, string role)
            {
                if (expression == null)
                    throw new InvalidOperationException(
                        "MIKU_CLOSURE_WEIGHT_MISSING:" + role);
                var kind = expression["kind"]?.Value<string>() ?? "";
                if (string.Equals(kind, "Constant", StringComparison.Ordinal) ||
                    string.Equals(
                        kind,
                        "ConstantValue",
                        StringComparison.Ordinal))
                    return Literal(
                        role,
                        expression["value"] ?? new JValue(0f),
                        "Scalar");
                if (string.Equals(kind, "Multiply", StringComparison.Ordinal) ||
                    string.Equals(kind, "Add", StringComparison.Ordinal))
                {
                    var inputs = (expression["inputs"] as JArray ??
                                  new JArray())
                        .OfType<JObject>()
                        .ToArray();
                    if (inputs.Length == 0)
                        return Literal(
                            role + ":identity",
                            new JValue(
                                string.Equals(
                                    kind,
                                    "Multiply",
                                    StringComparison.Ordinal)
                                    ? 1f
                                    : 0f),
                            "Scalar");
                    var result = BuildWeight(inputs[0], role + ":0");
                    for (var index = 1; index < inputs.Length; index++)
                    {
                        result = Binary(
                            role + ":" + index,
                            string.Equals(
                                kind,
                                "Multiply",
                                StringComparison.Ordinal)
                                ? "MultiplyNode"
                                : "AddNode",
                            result,
                            BuildWeight(
                                inputs[index],
                                role + ":" + index + ":input"));
                    }
                    return result;
                }
                if (string.Equals(kind, "OneMinus", StringComparison.Ordinal))
                    return Unary(
                        role,
                        "OneMinusNode",
                        BuildWeight(
                            expression["input"] as JObject,
                            role + ":input"));
                if (string.Equals(kind, "Clamp", StringComparison.Ordinal))
                    return Unary(
                        role,
                        "SaturateNode",
                        BuildWeight(
                            expression["input"] as JObject,
                            role + ":input"));
                if (string.Equals(
                        kind,
                        "ImplicitConversion",
                        StringComparison.Ordinal))
                    return ConvertWeight(
                        BuildWeight(
                            expression["input"] as JObject,
                            role + ":input"),
                        expression["conversion"] as JObject,
                        role);
                var expressionId =
                    expression["expressionId"]?.Value<string>() ?? "";
                if (!string.IsNullOrEmpty(expressionId))
                    return BuildExpression(expressionId);
                throw new InvalidOperationException(
                    "MIKU_CLOSURE_WEIGHT_REQUIRES_BAKE:" + role + ":" + kind);
            }

            Handle ConvertWeight(
                Handle input,
                JObject conversion,
                string role)
            {
                var kind =
                    conversion?["conversionKind"]?.Value<string>() ??
                    "Identity";
                if (string.Equals(kind, "Identity", StringComparison.Ordinal) ||
                    string.Equals(kind, "BoolToFloat", StringComparison.Ordinal) ||
                    string.Equals(kind, "IntToFloat", StringComparison.Ordinal))
                    return input;
                if (string.Equals(
                        kind,
                        "ColorToFloatLuminance",
                        StringComparison.Ordinal))
                {
                    var values = conversion?["colorManagement"]?
                        ["luminanceCoefficients"] as JArray ??
                        new JArray(0.2126f, 0.7152f, 0.0722f);
                    return Binary(
                        role + ":luminance",
                        "DotProductNode",
                        input,
                        Literal(
                            role + ":luminance-coefficients",
                            values,
                            "Float3"));
                }
                if (string.Equals(
                        kind,
                        "VectorToFloatAverage",
                        StringComparison.Ordinal))
                {
                    var sourceType =
                        conversion?["sourceType"]?.Value<string>() ??
                        "Vector3";
                    var dimensions = sourceType.EndsWith(
                        "2",
                        StringComparison.Ordinal)
                        ? 2
                        : sourceType.EndsWith("4", StringComparison.Ordinal)
                            ? 4
                            : 3;
                    return Binary(
                        role + ":average",
                        "DotProductNode",
                        input,
                        Literal(
                            role + ":average-coefficients",
                            new JArray(
                                Enumerable.Repeat(
                                    1f / dimensions,
                                    dimensions)),
                            "Float" + dimensions));
                }
                throw new InvalidOperationException(
                    "MIKU_IMPLICIT_CONVERSION_UNSUPPORTED:" + kind);
            }

            static string NormalizeSemanticName(string value)
            {
                return new string(
                    (value ?? "")
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant)
                    .ToArray());
            }

            void BuildDielectric(
                IDictionary<string, JObject> channels,
                JObject surface)
            {
                var transmissionColor = ResolveChannel(
                    channels,
                    "TransmissionColor",
                    new JArray(1f, 1f, 1f));
                var transmissionWeight = OpticalProperty(
                    channels,
                    "TransmissionWeight",
                    "Transmission Weight",
                    "_TransmissionWeight",
                    1f);
                var ior = OpticalProperty(
                    channels,
                    "IOR",
                    "Index of Refraction",
                    "_IOR",
                    1.5f);
                var thickness = OpticalProperty(
                    channels,
                    "Thickness",
                    "Thickness",
                    "_Thickness",
                    0.1f);
                var roughness = ResolveChannel(
                    channels,
                    "Roughness",
                    new JValue(0f));
                var normal = ResolveChannel(
                    channels,
                    "Normal",
                    new JArray(0f, 0f, 1f));
                var emission = ResolveChannel(
                    channels,
                    "Emission",
                    new JArray(0f, 0f, 0f));
                var opacity = Property(
                    "glass-opacity",
                    "Opacity",
                    "_Opacity",
                    new JValue(1f));
                var coverage = Binary(
                    "glass-coverage",
                    "MultiplyNode",
                    ResolveChannel(channels, "Alpha", new JValue(1f)),
                    opacity);
                var refractionStrength = Property(
                    "glass-refraction-strength",
                    "Refraction Strength",
                    "_RefractionStrength",
                    new JValue(0.05f));
                var reflectionStrength = Property(
                    "glass-reflection-strength",
                    "Reflection Strength",
                    "_ReflectionStrength",
                    new JValue(1f));
                var one = Literal("glass-one", new JValue(1f), "Scalar");
                var etaOffset = Binary(
                    "glass-eta-offset",
                    "SubtractNode",
                    one,
                    Binary("glass-ior-reciprocal", "DivideNode", one, ior));
                var offsetScale = Binary(
                    "glass-offset-scale",
                    "MultiplyNode",
                    Binary(
                        "glass-offset-refraction",
                        "MultiplyNode",
                        etaOffset,
                        refractionStrength),
                    thickness);
                var screen = Native(
                    "glass-screen-position",
                    "ScreenPositionNode",
                    0);
                var offset = Binary(
                    "glass-screen-offset",
                    "MultiplyNode",
                    normal,
                    offsetScale);
                var sceneUv = Binary(
                    "glass-scene-uv",
                    "AddNode",
                    screen,
                    offset);
                var sceneNode = adapter.CreateNode(
                    graph,
                    materialId,
                    "glass-scene-color",
                    "SceneColorNode",
                    Position());
                adapter.Connect(graph, sceneUv.node, sceneUv.slot, sceneNode, 0);
                var sceneColor = new Handle { node = sceneNode, slot = 1 };
                var refraction = Binary(
                    "glass-refraction-weight",
                    "MultiplyNode",
                    Binary(
                        "glass-refraction-tint",
                        "MultiplyNode",
                        sceneColor,
                        transmissionColor),
                    transmissionWeight);
                var probeNode = adapter.CreateNode(
                    graph,
                    materialId,
                    "glass-reflection-probe",
                    "ReflectionProbeNode",
                    Position());
                var lod = Binary(
                    "glass-reflection-lod",
                    "MultiplyNode",
                    roughness,
                    Literal("glass-max-lod", new JValue(6f), "Scalar"));
                adapter.Connect(graph, lod.node, lod.slot, probeNode, 2);
                var reflectionProbe = new Handle { node = probeNode, slot = 3 };
                var worldNormal = Native(
                    "glass-world-normal",
                    "NormalVectorNode",
                    0,
                    "World");
                var worldView = Native(
                    "glass-world-view",
                    "ViewDirectionNode",
                    0,
                    "World");
                var cosine = Unary(
                    "glass-cosine",
                    "AbsoluteNode",
                    Binary(
                        "glass-view-normal-dot",
                        "DotProductNode",
                        Unary("glass-view-normalized", "NormalizeNode", worldView),
                        Unary("glass-normal-normalized", "NormalizeNode", worldNormal)));
                var f0Ratio = Binary(
                    "glass-f0-ratio",
                    "DivideNode",
                    Binary("glass-ior-minus-one", "SubtractNode", ior, one),
                    Binary("glass-ior-plus-one", "AddNode", ior, one));
                var f0 = Binary(
                    "glass-f0",
                    "MultiplyNode",
                    f0Ratio,
                    f0Ratio);
                var schlick = Binary(
                    "glass-schlick-add",
                    "AddNode",
                    f0,
                    Binary(
                        "glass-schlick-tail",
                        "MultiplyNode",
                        Binary("glass-one-minus-f0", "SubtractNode", one, f0),
                        Binary(
                            "glass-schlick-power",
                            "PowerNode",
                            Binary(
                                "glass-one-minus-cosine",
                                "SubtractNode",
                                one,
                                cosine),
                            Literal(
                                "glass-schlick-five",
                                new JValue(5f),
                                "Scalar"))));
                var reflection = Binary(
                    "glass-reflection",
                    "MultiplyNode",
                    Binary(
                        "glass-reflection-fresnel",
                        "MultiplyNode",
                        reflectionProbe,
                        schlick),
                    reflectionStrength);
                var finalColor = Binary(
                    "glass-final-emission",
                    "AddNode",
                    Binary(
                        "glass-final-optical",
                        "AddNode",
                        refraction,
                        reflection),
                    emission);
                AddOutput("Base Color", "Vector3", finalColor);
                AddOutput("Metallic", "Vector1", Literal("glass-metallic", new JValue(0f), "Scalar"));
                AddOutput("Smoothness", "Vector1", Unary("glass-smoothness", "OneMinusNode", roughness));
                AddOutput("Normal TS", "Vector3", normal);
                AddOutput("Emission", "Vector3", Literal("glass-emission-zero", new JArray(0f, 0f, 0f), "Float3"));
                AddOutput("Occlusion", "Vector1", Literal("glass-occlusion", new JValue(1f), "Scalar"));
                if (string.Equals(
                        surface?["renderMethod"]?.Value<string>(),
                        "Dithered",
                        StringComparison.Ordinal))
                    coverage = Dither("glass-dither", coverage);
                AddOutput("Alpha", "Vector1", coverage);
                AddOutput(
                    "Alpha Clip Threshold",
                    "Vector1",
                    Literal(
                        "glass-alpha-clip-threshold",
                        new JValue(
                            string.Equals(
                                surface?["renderMethod"]?.Value<string>(),
                                "Dithered",
                                StringComparison.Ordinal)
                                ? 0f
                                : 0.5f),
                        "Scalar"));
            }

            void AddChannel(
                IDictionary<string, JObject> channels,
                string semantic,
                string outputName,
                string concreteType,
                JToken fallback)
            {
                AddOutput(outputName, concreteType, ResolveChannel(channels, semantic, fallback));
            }

            Handle ResolveChannel(
                IDictionary<string, JObject> channels,
                string semantic,
                JToken fallback,
                bool explicitLod = false)
            {
                if (!channels.TryGetValue(semantic, out var channel))
                    return Literal("channel:" + semantic, fallback, ValueType(fallback));
                if (channel["value"] is JObject value)
                {
                    if (string.Equals(
                            value["kind"]?.Value<string>(),
                            "Expression",
                            StringComparison.Ordinal))
                        return BuildExpression(value["expressionId"]?.Value<string>() ?? "");
                    if (string.Equals(
                            value["kind"]?.Value<string>(),
                            "Constant",
                            StringComparison.Ordinal))
                    {
                        var constant = value["value"] ?? fallback;
                        if (string.Equals(
                                semantic,
                                "Normal",
                                StringComparison.Ordinal) &&
                            IsLegacyZeroNormal(constant))
                            constant = fallback;
                        return Literal(
                            "channel:" + semantic,
                            constant,
                            channel["valueType"]?.Value<string>() ?? ValueType(fallback));
                    }
                    if (string.Equals(
                            value["kind"]?.Value<string>(),
                            "TextureResource",
                            StringComparison.Ordinal))
                        return SampleChannelTexture(
                            "channel:" + semantic,
                            semantic,
                            value["resourceId"]?.Value<string>() ?? "",
                            explicitLod);
                }
                var channelDefault = channel["default"];
                if (channelDefault == null ||
                    channelDefault.Type == JTokenType.Null)
                    channelDefault = fallback;
                if (string.Equals(
                        semantic,
                        "Normal",
                        StringComparison.Ordinal) &&
                    IsLegacyZeroNormal(channelDefault))
                    channelDefault = fallback;
                return Literal(
                    "channel:" + semantic,
                    channelDefault,
                    channel["valueType"]?.Value<string>() ?? ValueType(fallback));
            }

            Handle OpticalProperty(
                IDictionary<string, JObject> channels,
                string semantic,
                string displayName,
                string referenceName,
                float fallback)
            {
                if (channels.TryGetValue(semantic, out var channel) &&
                    channel["value"] is JObject value &&
                    string.Equals(
                        value["kind"]?.Value<string>(),
                        "Expression",
                        StringComparison.Ordinal))
                    return ResolveChannel(channels, semantic, new JValue(fallback));
                var scalar = channels.TryGetValue(semantic, out channel)
                    ? channel["value"]?["value"]?.Value<float>() ??
                      channel["default"]?.Value<float>() ??
                      fallback
                    : fallback;
                return Property(
                    "optical:" + semantic,
                    displayName,
                    referenceName,
                    new JValue(scalar));
            }

            static bool IsLegacyZeroNormal(JToken value)
            {
                if (!(value is JArray array) || array.Count < 3)
                    return false;
                const float epsilon = 0.0001f;
                for (var index = 0; index < 3; index++)
                {
                    var component = array[index].Value<float>();
                    if (float.IsNaN(component) ||
                        float.IsInfinity(component) ||
                        Math.Abs(component) > epsilon)
                        return false;
                }
                return true;
            }

            static bool IsNeutralNormal(JToken value)
            {
                if (!(value is JArray array) || array.Count < 3)
                    return false;
                const float epsilon = 0.0001f;
                var expected = new[] { 0f, 0f, 1f };
                for (var index = 0; index < 3; index++)
                {
                    var component = array[index].Value<float>();
                    if (float.IsNaN(component) ||
                        float.IsInfinity(component) ||
                        Math.Abs(component - expected[index]) > epsilon)
                        return false;
                }
                return true;
            }

            void AddOutput(string name, string concreteType, Handle value)
            {
                var slot = adapter.AddOutput(output, name, concreteType);
                adapter.Connect(graph, value.node, value.slot, output, slot);
            }

            Handle BuildExpression(string id)
            {
                if (!expressions.TryGetValue(id, out var expression))
                    throw new InvalidOperationException(
                        "MIKU_EXPRESSION_REFERENCE_MISSING:" + id);
                var op = expression["op"]?.Value<string>() ?? "";
                if (built.TryGetValue(id, out var cached))
                    return cached;
                if (!visiting.Add(id))
                    throw new InvalidOperationException("MIKU_EXPRESSION_CYCLE:" + id);
                try
                {
                    Handle result;
                    switch (op)
                    {
                        case "Constant":
                            result = Literal(id, expression["params"]?["value"], expression["valueType"]?.Value<string>());
                            break;
                        case "Parameter":
                            result = ParameterExpression(expression);
                            break;
                        case "Input.ViewDirection":
                            result = Native(id, "ViewDirectionNode", 0, "World");
                            break;
                        case "Input.TextureCoordinate.Object":
                            result = Native(id, "PositionNode", 0, "Object");
                            break;
                        case "Input.Position.Object":
                            result = Native(id, "PositionNode", 0, "Object");
                            break;
                        case "Input.Normal.Object":
                            result = Native(id, "NormalVectorNode", 0, "Object");
                            break;
                        case "Input.MaterialChannel":
                            var materialSemantic =
                                expression["params"]?["semantic"]?.Value<string>() ?? "";
                            if (string.IsNullOrEmpty(materialSemantic))
                                throw new InvalidOperationException(
                                    "MIKU_MATERIAL_CHANNEL_SEMANTIC_MISSING:" + id);
                            result = ResolveChannel(
                                channelsBySemantic,
                                materialSemantic,
                                new JValue(0f),
                                string.Equals(
                                    expression["stage"]?.Value<string>(),
                                    "Vertex",
                                    StringComparison.Ordinal));
                            break;
                        case "Input.Normal":
                            result = Native(id, "NormalVectorNode", 0, "World");
                            break;
                        case "Input.IsFrontFace":
                            result = Native(id, "IsFrontFaceNode", 0);
                            break;
                        case "Input.Camera.ViewVector":
                            result = CameraViewVector(id);
                            break;
                        case "Input.Camera.ViewZDepth":
                            result = CameraViewZ(id);
                            break;
                        case "Input.Camera.ViewDistance":
                            result = CameraDistance(id);
                            break;
                        case "Input.Time.Seconds":
                        case "Input.Time.Frame":
                        case "Input.Time.Sine":
                        case "Input.Time.Cosine":
                            result = TimeExpression(id, op, expression["params"] as JObject);
                            break;
                        case "Input.Time.LegacyTime":
                        case "Input.Time.LegacySine":
                        case "Input.Time.LegacyCosine":
                        case "Input.Time.LegacyDelta":
                        case "Input.Time.LegacySmoothDelta":
                            result = LegacyTime(id, op);
                            break;
                        case "Input.LightPath.CameraRay":
                            result = LightPath(id, true);
                            break;
                        case "Input.LightPath.ShadowRay":
                            result = LightPath(id, false);
                            break;
                        case "Math.DielectricFresnel":
                            result = DielectricFresnel(id, expression, false);
                            break;
                        case "Math.LayerWeightFresnel":
                            result = DielectricFresnel(id, expression, true);
                            break;
                        case "Math.LayerWeightFacing":
                            result = LayerWeightFacing(id, expression);
                            break;
                        case "Math.Lerp":
                            result = Ternary(id, "LerpNode", expression, "A", "B", "T");
                            break;
                        case "Color.Overlay":
                            result = Overlay(id, expression);
                            break;
                        case "Color.Ramp":
                            result = ColorRamp(id, expression);
                            break;
                        case "Vector.Component":
                            result = VectorComponent(id, expression);
                            break;
                        case "Vector.NormalFromHeight":
                            result = NormalFromHeight(id, expression);
                            break;
                        case "Vector.NormalStrength":
                            result = NormalStrength(id, expression);
                            break;
                        case "Vector.NormalBlend":
                            result = NormalBlend(id, expression);
                            break;
                        case "Vector.Displacement":
                            result = Displacement(id, expression);
                            break;
                        case "Vector.Mapping":
                            result = MappingPoint(id, expression);
                            break;
                        case "Texture.Noise.Factor":
                            result = NoiseFactor(id, expression);
                            break;
                        case "Color.HueSaturationValue":
                            result = HueSaturationValue(id, expression);
                            break;
                        case "Texture.SampleBaked2D":
                            result = SampleBakedTexture(id, expression);
                            break;
                        case "Texture.SampleImage2D":
                            result = SampleImageTexture(id, expression);
                            break;
                        default:
                            result = GenericMath(id, op, expression);
                            break;
                    }
                    built[id] = result;
                    return result;
                }
                finally
                {
                    visiting.Remove(id);
                }
            }

            Handle GenericMath(string id, string op, JObject expression)
            {
                var binary = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Math.Add"] = "AddNode",
                    ["Math.Subtract"] = "SubtractNode",
                    ["Math.Multiply"] = "MultiplyNode",
                    ["Math.Divide"] = "DivideNode",
                    ["Math.Power"] = "PowerNode",
                    ["Math.Minimum"] = "MinimumNode",
                    ["Math.Maximum"] = "MaximumNode",
                    ["Math.Dot"] = "DotProductNode",
                    ["Math.Modulo"] = "ModuloNode",
                };
                var unary = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Math.OneMinus"] = "OneMinusNode",
                    ["Math.Absolute"] = "AbsoluteNode",
                    ["Math.Sine"] = "SineNode",
                    ["Math.Cosine"] = "CosineNode",
                    ["Math.Normalize"] = "NormalizeNode",
                    ["Math.Length"] = "LengthNode",
                };
                if (binary.TryGetValue(op, out var binaryNode))
                    return Binary(id, binaryNode, Input(expression, "A"), Input(expression, "B"));
                if (unary.TryGetValue(op, out var unaryNode))
                    return Unary(id, unaryNode, Input(expression, "A"));
                if (string.Equals(op, "Math.MultiplyAdd", StringComparison.Ordinal))
                    return Binary(
                        id + ":add",
                        "AddNode",
                        Binary(
                            id + ":multiply",
                            "MultiplyNode",
                            Input(expression, "A"),
                            Input(expression, "B")),
                        Input(expression, "C"));
                if (string.Equals(op, "Math.Logarithm", StringComparison.Ordinal))
                    return Binary(
                        id + ":divide",
                        "DivideNode",
                        Unary(id + ":value-log", "LogNode", Input(expression, "A")),
                        Unary(id + ":base-log", "LogNode", Input(expression, "B")));
                if (string.Equals(op, "Math.GreaterThan", StringComparison.Ordinal) ||
                    string.Equals(op, "Math.LessThan", StringComparison.Ordinal))
                {
                    var comparison = adapter.CreateNode(
                        graph,
                        materialId,
                        id + ":comparison",
                        "ComparisonNode",
                        Position());
                    adapter.SetEnum(
                        comparison,
                        "comparisonType",
                        string.Equals(op, "Math.GreaterThan", StringComparison.Ordinal)
                            ? "Greater"
                            : "Less");
                    var a = Input(expression, "A");
                    var b = Input(expression, "B");
                    adapter.Connect(graph, a.node, a.slot, comparison, 0);
                    adapter.Connect(graph, b.node, b.slot, comparison, 1);
                    return new Handle { node = comparison, slot = 2 };
                }
                throw new InvalidOperationException("MIKU_RUNTIME_INPUT_UNSUPPORTED:" + op);
            }

            Handle Lerp(string role, Handle a, Handle b, Handle t)
            {
                var node = adapter.CreateNode(
                    graph,
                    materialId,
                    role,
                    "LerpNode",
                    Position());
                adapter.Connect(graph, a.node, a.slot, node, 0);
                adapter.Connect(graph, b.node, b.slot, node, 1);
                adapter.Connect(graph, t.node, t.slot, node, 2);
                return new Handle { node = node, slot = 3 };
            }

            Handle Dither(string role, Handle coverage)
            {
                var node = adapter.CreateNode(
                    graph,
                    materialId,
                    role,
                    "DitherNode",
                    Position());
                adapter.Connect(graph, coverage.node, coverage.slot, node, 0);
                return new Handle { node = node, slot = 2 };
            }

            Handle Overlay(string role, JObject expression)
            {
                var a = Input(expression, "A");
                var b = Input(expression, "B");
                var t = Input(expression, "T");
                var one = Literal(role + ":one", new JValue(1f), "Scalar");
                var two = Literal(role + ":two", new JValue(2f), "Scalar");
                var low = Binary(
                    role + ":low",
                    "MultiplyNode",
                    Binary(role + ":low-product", "MultiplyNode", a, b),
                    two);
                var high = Binary(
                    role + ":high",
                    "SubtractNode",
                    one,
                    Binary(
                        role + ":high-product",
                        "MultiplyNode",
                        Binary(
                            role + ":high-complements",
                            "MultiplyNode",
                            Binary(role + ":one-minus-a", "SubtractNode", one, a),
                            Binary(role + ":one-minus-b", "SubtractNode", one, b)),
                        two));
                var selector = Binary(
                    role + ":selector",
                    "StepNode",
                    Literal(role + ":half", new JValue(0.5f), "Scalar"),
                    a);
                return Lerp(
                    role + ":factor",
                    a,
                    Lerp(role + ":piecewise", low, high, selector),
                    t);
            }

            Handle ColorRamp(string role, JObject expression)
            {
                var factor = Input(expression, "Factor");
                var parameters = expression["params"] as JObject ?? new JObject();
                var elements = (parameters["elements"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .OrderBy(item => item["position"]?.Value<float>() ?? 0f)
                    .ToArray();
                if (elements.Length < 2)
                    throw new InvalidOperationException(
                        "MIKU_RUNTIME_COLOR_RAMP_UNSUPPORTED:" + role);
                var outputAlpha = string.Equals(
                    parameters["output"]?.Value<string>(),
                    "Alpha",
                    StringComparison.Ordinal);
                var elementHandles = new Dictionary<int, Handle>();
                Handle Element(int index)
                {
                    if (elementHandles.TryGetValue(index, out var existing))
                        return existing;
                    var color = elements[index]["color"] as JArray ?? new JArray();
                    var created = Literal(
                        role + ":element:" + index,
                        outputAlpha
                            ? (JToken)new JValue(
                                color.Count > 3 ? color[3].Value<float>() : 1f)
                            : new JArray(
                                color.Take(4).Select(item => item.Value<float>())),
                        outputAlpha ? "Scalar" : "Color");
                    elementHandles[index] = created;
                    return created;
                }
                var result = Element(0);
                var interpolation =
                    parameters["interpolation"]?.Value<string>() ?? "LINEAR";
                for (var index = 1; index < elements.Length; index++)
                {
                    var start = elements[index - 1]["position"]?.Value<float>() ?? 0f;
                    var end = elements[index]["position"]?.Value<float>() ?? 1f;
                    if (string.Equals(
                            interpolation,
                            "CONSTANT",
                            StringComparison.Ordinal))
                    {
                        var constantSelector = Binary(
                            role + ":segment:" + index + ":constant-selector",
                            "StepNode",
                            Literal(
                                role + ":segment:" + index + ":end",
                                new JValue(end),
                                "Scalar"),
                            factor);
                        result = Lerp(
                            role + ":segment:" + index + ":constant-select",
                            result,
                            Element(index),
                            constantSelector);
                        continue;
                    }
                    var width = Math.Max(end - start, 0.00001f);
                    var local = Unary(
                        role + ":segment:" + index + ":saturate",
                        "SaturateNode",
                        Binary(
                            role + ":segment:" + index + ":normalize",
                            "DivideNode",
                            Binary(
                                role + ":segment:" + index + ":offset",
                                "SubtractNode",
                                factor,
                                Literal(
                                    role + ":segment:" + index + ":start",
                                    new JValue(start),
                                    "Scalar")),
                            Literal(
                                role + ":segment:" + index + ":width",
                                new JValue(width),
                                "Scalar")));
                    if (string.Equals(interpolation, "EASE", StringComparison.Ordinal))
                    {
                        var local2 = Binary(
                            role + ":segment:" + index + ":t2",
                            "MultiplyNode",
                            local,
                            local);
                        local = Binary(
                            role + ":segment:" + index + ":ease",
                            "MultiplyNode",
                            local2,
                            Binary(
                                role + ":segment:" + index + ":ease-curve",
                                "SubtractNode",
                                Literal(
                                    role + ":segment:" + index + ":three",
                                    new JValue(3f),
                                    "Scalar"),
                                Binary(
                                    role + ":segment:" + index + ":two-t",
                                    "MultiplyNode",
                                    Literal(
                                        role + ":segment:" + index + ":two",
                                        new JValue(2f),
                                        "Scalar"),
                                    local)));
                    }
                    else if (string.Equals(
                                 interpolation,
                                 "B_SPLINE",
                                 StringComparison.Ordinal))
                    {
                        var t2 = Binary(role + ":segment:" + index + ":t2", "MultiplyNode", local, local);
                        var t3 = Binary(role + ":segment:" + index + ":t3", "MultiplyNode", t2, local);
                        var numerator = Binary(
                            role + ":segment:" + index + ":bspline-1",
                            "AddNode",
                            Literal(role + ":segment:" + index + ":one", new JValue(1f), "Scalar"),
                            Binary(role + ":segment:" + index + ":three-t", "MultiplyNode", Literal(role + ":segment:" + index + ":three", new JValue(3f), "Scalar"), local));
                        numerator = Binary(role + ":segment:" + index + ":bspline-2", "AddNode", numerator, Binary(role + ":segment:" + index + ":three-t2", "MultiplyNode", Literal(role + ":segment:" + index + ":three-2", new JValue(3f), "Scalar"), t2));
                        numerator = Binary(role + ":segment:" + index + ":bspline-3", "SubtractNode", numerator, Binary(role + ":segment:" + index + ":two-t3", "MultiplyNode", Literal(role + ":segment:" + index + ":two", new JValue(2f), "Scalar"), t3));
                        local = Binary(role + ":segment:" + index + ":bspline", "DivideNode", numerator, Literal(role + ":segment:" + index + ":six", new JValue(6f), "Scalar"));
                    }
                    var segment = Lerp(
                        role + ":segment:" + index + ":color",
                        Element(index - 1),
                        Element(index),
                        local);
                    var selector = Binary(
                        role + ":segment:" + index + ":selector",
                        "StepNode",
                        Literal(
                            role + ":segment:" + index + ":threshold",
                            new JValue(start),
                            "Scalar"),
                        factor);
                    result = Lerp(
                        role + ":segment:" + index + ":select",
                        result,
                        segment,
                        selector);
                }
                return result;
            }

            Handle NormalFromHeight(string role, JObject expression)
            {
                var adjusted = Binary(
                    role + ":adjusted-height",
                    "SubtractNode",
                    Input(expression, "Height"),
                    Input(expression, "Midlevel"));
                var node = adapter.CreateNode(
                    graph,
                    materialId,
                    role,
                    "NormalFromHeightNode",
                    Position());
                adapter.SetEnum(node, "outputSpace", "Tangent");
                adapter.Connect(graph, adjusted.node, adjusted.slot, node, 0);
                var strength = Input(expression, "Strength");
                var parameters =
                    expression["params"] as JObject ?? new JObject();
                if (parameters["bumpStrengthReference"] != null)
                    strength = Binary(
                        role + ":authoring-strength",
                        "MultiplyNode",
                        strength,
                        Property(
                            role + ":authoring-strength-property",
                            "Bump Strength",
                            parameters["bumpStrengthReference"]
                                ?.Value<string>() ??
                            "_MIKU_BumpStrength",
                            new JValue(1f)));
                if (parameters["bumpDistanceReference"] != null)
                    strength = Binary(
                        role + ":authoring-distance",
                        "MultiplyNode",
                        strength,
                        Property(
                            role + ":authoring-distance-property",
                            "Bump Distance",
                            parameters["bumpDistanceReference"]
                                ?.Value<string>() ??
                            "_MIKU_BumpDistance",
                            new JValue(1f)));
                adapter.Connect(graph, strength.node, strength.slot, node, 2);
                return new Handle { node = node, slot = 1 };
            }

            Handle VectorComponent(string role, JObject expression)
            {
                var node = adapter.CreateNode(
                    graph,
                    materialId,
                    role,
                    "SplitNode",
                    Position());
                var input = Input(expression, "Input");
                adapter.Connect(graph, input.node, input.slot, node, 0);
                var component =
                    expression["params"]?["component"]?.Value<string>() ??
                    "R";
                var slot = component switch
                {
                    "R" => 1,
                    "G" => 2,
                    "B" => 3,
                    "A" => 4,
                    _ => throw new InvalidOperationException(
                        "MIKU_VECTOR_COMPONENT_INVALID:" + component),
                };
                return new Handle { node = node, slot = slot };
            }

            Handle NormalStrength(string role, JObject expression)
            {
                var node = adapter.CreateNode(
                    graph,
                    materialId,
                    role,
                    "NormalStrengthNode",
                    Position());
                var normal = Input(expression, "Normal");
                var strength = Input(expression, "Strength");
                adapter.Connect(graph, normal.node, normal.slot, node, 0);
                adapter.Connect(graph, strength.node, strength.slot, node, 1);
                return new Handle { node = node, slot = 2 };
            }

            Handle NormalBlend(string role, JObject expression)
            {
                var node = adapter.CreateNode(
                    graph,
                    materialId,
                    role,
                    "NormalBlendNode",
                    Position());
                adapter.SetEnum(node, "blendMode", "Reoriented");
                var baseNormal = Input(expression, "Base");
                var detailNormal = Input(expression, "Detail");
                adapter.Connect(
                    graph,
                    baseNormal.node,
                    baseNormal.slot,
                    node,
                    0);
                adapter.Connect(
                    graph,
                    detailNormal.node,
                    detailNormal.slot,
                    node,
                    1);
                return new Handle { node = node, slot = 2 };
            }

            Handle Displacement(string role, JObject expression)
            {
                var parameters =
                    expression["params"] as JObject ?? new JObject();
                var midlevel = Property(
                    role + ":midlevel",
                    "Height Midlevel",
                    parameters["midlevelReference"]?.Value<string>() ??
                    "_MIKU_HeightMidlevel",
                    parameters["midlevel"] ?? new JValue(0.5f));
                var scale = Property(
                    role + ":scale",
                    "Height Scale",
                    parameters["scaleReference"]?.Value<string>() ??
                    "_MIKU_HeightScale",
                    parameters["scale"] ?? new JValue(1f));
                var adjusted = Binary(
                    role + ":adjusted-height",
                    "SubtractNode",
                    Input(expression, "Height"),
                    midlevel);
                var offsetAmount = Binary(
                    role + ":scaled-height",
                    "MultiplyNode",
                    adjusted,
                    scale);
                var offset = Binary(
                    role + ":normal-offset",
                    "MultiplyNode",
                    Input(expression, "Normal"),
                    offsetAmount);
                return Binary(
                    role + ":position",
                    "AddNode",
                    Input(expression, "Position"),
                    offset);
            }

            Handle MappingPoint(string role, JObject expression)
            {
                var parameters = expression["params"] as JObject ?? new JObject();
                var vectorType =
                    parameters["vectorType"]?.Value<string>() ?? "POINT";
                if (!string.Equals(
                        vectorType,
                        "POINT",
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "MIKU_PORTABLE_MESH_BAKE_REQUIRED:Vector.Mapping:" +
                        vectorType);
                var node = adapter.CreateMappingPointNode(
                    graph,
                    materialId,
                    role,
                    Position());
                adapter.Connect(
                    graph,
                    Input(expression, "Vector").node,
                    Input(expression, "Vector").slot,
                    node,
                    0);
                adapter.Connect(
                    graph,
                    Input(expression, "Location").node,
                    Input(expression, "Location").slot,
                    node,
                    1);
                adapter.Connect(
                    graph,
                    Input(expression, "Rotation").node,
                    Input(expression, "Rotation").slot,
                    node,
                    2);
                adapter.Connect(
                    graph,
                    Input(expression, "Scale").node,
                    Input(expression, "Scale").slot,
                    node,
                    3);
                return new Handle { node = node, slot = 4 };
            }

            Handle NoiseFactor(string role, JObject expression)
            {
                var parameters = expression["params"] as JObject ?? new JObject();
                var dimensions =
                    parameters["dimensions"]?.Value<string>() ?? "3D";
                if (!string.Equals(
                        dimensions,
                        "3D",
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "MIKU_PORTABLE_MESH_BAKE_REQUIRED:Texture.Noise:" +
                        dimensions);
                var node = adapter.CreateNoiseFactor3DNode(
                    graph,
                    materialId,
                    role,
                    Position());
                var names = new[]
                {
                    "Vector",
                    "Scale",
                    "Detail",
                    "Roughness",
                    "Lacunarity",
                    "Distortion",
                };
                for (var index = 0; index < names.Length; index++)
                {
                    var input = Input(expression, names[index]);
                    adapter.Connect(
                        graph,
                        input.node,
                        input.slot,
                        node,
                        index);
                }
                return new Handle { node = node, slot = 6 };
            }

            Handle Input(JObject expression, string name)
            {
                var id = expression["inputs"]?[name]?["expressionId"]?.Value<string>() ?? "";
                return BuildExpression(id);
            }

            Handle Native(string role, string type, int outputSlot, string space = null)
            {
                var node = adapter.CreateNode(graph, materialId, role, type, Position());
                if (!string.IsNullOrEmpty(space))
                    adapter.SetEnum(node, "space", space);
                return new Handle { node = node, slot = outputSlot };
            }

            Handle TransformNormal(
                string role,
                Handle normal,
                string from,
                string to)
            {
                var node = adapter.CreateNode(
                    graph,
                    materialId,
                    role,
                    "TransformNode",
                    Position());
                adapter.SetCoordinateSpaceConversion(node, from, to);
                adapter.Connect(graph, normal.node, normal.slot, node, 0);
                return new Handle { node = node, slot = 1 };
            }

            Handle Literal(string role, JToken value, string valueType)
            {
                var dimensions = Dimensions(value, valueType);
                var node = adapter.CreateNode(
                    graph,
                    materialId,
                    role,
                    dimensions == 1 ? "Vector1Node" : "Vector" + dimensions + "Node",
                    Position());
                var values = Components(value, dimensions);
                for (var index = 0; index < dimensions; index++)
                    adapter.SetSlotValue(node, index + 1, new JValue(values[index]));
                return new Handle { node = node, slot = 0 };
            }

            Handle Binary(string role, string type, Handle a, Handle b)
            {
                var node = adapter.CreateNode(graph, materialId, role, type, Position());
                adapter.Connect(graph, a.node, a.slot, node, 0);
                adapter.Connect(graph, b.node, b.slot, node, 1);
                return new Handle { node = node, slot = 2 };
            }

            Handle Unary(string role, string type, Handle value)
            {
                var node = adapter.CreateNode(graph, materialId, role, type, Position());
                adapter.Connect(graph, value.node, value.slot, node, 0);
                return new Handle { node = node, slot = 1 };
            }

            Handle Ternary(
                string role,
                string type,
                JObject expression,
                string aName,
                string bName,
                string cName)
            {
                var node = adapter.CreateNode(graph, materialId, role, type, Position());
                adapter.Connect(graph, Input(expression, aName).node, Input(expression, aName).slot, node, 0);
                adapter.Connect(graph, Input(expression, bName).node, Input(expression, bName).slot, node, 1);
                adapter.Connect(graph, Input(expression, cName).node, Input(expression, cName).slot, node, 2);
                return new Handle { node = node, slot = 3 };
            }

            Handle Branch(string role, Handle predicate, Handle whenTrue, Handle whenFalse)
            {
                var node = adapter.CreateNode(graph, materialId, role, "BranchNode", Position());
                adapter.Connect(graph, predicate.node, predicate.slot, node, 0);
                adapter.Connect(graph, whenTrue.node, whenTrue.slot, node, 1);
                adapter.Connect(graph, whenFalse.node, whenFalse.slot, node, 2);
                return new Handle { node = node, slot = 3 };
            }

            Handle Compare(string role, string comparison, Handle a, Handle b)
            {
                var node = adapter.CreateNode(graph, materialId, role, "ComparisonNode", Position());
                adapter.SetEnum(node, "comparisonType", comparison);
                adapter.Connect(graph, a.node, a.slot, node, 0);
                adapter.Connect(graph, b.node, b.slot, node, 1);
                return new Handle { node = node, slot = 2 };
            }

            Handle CameraPosition(string role)
            {
                return Native(role, "PositionNode", 0, "View");
            }

            Handle CameraSplit(string role, out object split)
            {
                var position = CameraPosition(role + ":position-view");
                split = adapter.CreateNode(graph, materialId, role + ":split", "SplitNode", Position());
                adapter.Connect(graph, position.node, position.slot, split, 0);
                return position;
            }

            Handle CameraViewVector(string role)
            {
                CameraSplit(role, out var split);
                var minusOne = Literal(role + ":minus-one", new JValue(-1f), "Scalar");
                var negateZ = Binary(
                    role + ":negate-z",
                    "MultiplyNode",
                    new Handle { node = split, slot = 3 },
                    minusOne);
                var combine = adapter.CreateNode(graph, materialId, role + ":combine", "CombineNode", Position());
                adapter.Connect(graph, split, 1, combine, 0);
                adapter.Connect(graph, split, 2, combine, 1);
                adapter.Connect(graph, negateZ.node, negateZ.slot, combine, 2);
                return Unary(
                    role + ":normalize",
                    "NormalizeNode",
                    new Handle { node = combine, slot = 5 });
            }

            Handle CameraViewZ(string role)
            {
                CameraSplit(role, out var split);
                return Unary(
                    role + ":abs-z",
                    "AbsoluteNode",
                    new Handle { node = split, slot = 3 });
            }

            Handle CameraDistance(string role)
            {
                return Unary(role + ":length", "LengthNode", CameraPosition(role + ":position-view"));
            }

            Handle ParameterExpression(JObject expression)
            {
                var parameterId = expression["params"]?["parameterId"]?.Value<string>() ?? "";
                var parameter = (ir["parameters"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .FirstOrDefault(item => string.Equals(
                        item["id"]?.Value<string>(),
                        parameterId,
                        StringComparison.Ordinal));
                if (parameter == null)
                    throw new InvalidOperationException("MIKU_PARAMETER_MISSING:" + parameterId);
                var reference = parameter["referenceName"]?.Value<string>() ?? "";
                return Property(
                    parameterId,
                    parameter["displayName"]?.Value<string>() ?? parameterId,
                    reference,
                    parameter["default"] ?? new JValue(0f));
            }

            Handle Property(string role, string displayName, string referenceName, JToken value)
            {
                if (properties.TryGetValue(referenceName, out var cached))
                    return cached;
                var propertyIdentity = referenceName.StartsWith(
                    "_MIKU_Effect",
                    StringComparison.Ordinal)
                    ? "miku-runtime-time-v1"
                    : materialId;
                var property = adapter.CreateFloatProperty(
                    graph,
                    propertyIdentity,
                    role,
                    displayName,
                    referenceName,
                    value?.Value<float>() ?? 0f);
                var node = adapter.CreatePropertyNode(
                    graph,
                    materialId,
                    role,
                    property,
                    Position());
                var result = new Handle { node = node, slot = 0 };
                properties[referenceName] = result;
                return result;
            }

            Handle SampleBakedTexture(string role, JObject expression)
            {
                var parameters = expression["params"] as JObject ?? new JObject();
                var referenceName =
                    parameters["referenceName"]?.Value<string>() ?? "";
                if (string.IsNullOrEmpty(referenceName))
                    throw new InvalidOperationException(
                        "MIKU_EXPRESSION_TEXTURE_REFERENCE_MISSING:" + role);
                if (!properties.TryGetValue(referenceName, out var texture))
                {
                    var usage = parameters["usage"]?.Value<string>() ?? "Color";
                    var property = adapter.CreateTextureProperty(
                        graph,
                        materialId,
                        role,
                        "Miku Baked " + role.Substring(0, Math.Min(8, role.Length)),
                        referenceName,
                        string.Equals(usage, "Normal", StringComparison.Ordinal));
                    var propertyNode = adapter.CreatePropertyNode(
                        graph,
                        materialId,
                        role,
                        property,
                        Position());
                    texture = new Handle { node = propertyNode, slot = 0 };
                    properties[referenceName] = texture;
                }
                var uv = Native(role + ":uv0", "UVNode", 0);
                adapter.SetEnum(uv.node, "uvChannel", "UV0");
                var explicitLod = string.Equals(
                    parameters["lodMode"]?.Value<string>(),
                    "Explicit0",
                    StringComparison.Ordinal);
                var sample = adapter.CreateNode(
                    graph,
                    materialId,
                    role + ":sample",
                    explicitLod
                        ? "SampleTexture2DLODNode"
                        : "SampleTexture2DNode",
                    Position());
                var normal = string.Equals(
                    parameters["usage"]?.Value<string>(),
                    "Normal",
                    StringComparison.Ordinal);
                if (normal)
                {
                    adapter.SetEnum(sample, "textureType", "Normal");
                    adapter.SetEnum(sample, "normalMapSpace", "Tangent");
                }
                adapter.Connect(graph, texture.node, texture.slot, sample, 1);
                adapter.Connect(graph, uv.node, uv.slot, sample, 2);
                if (explicitLod)
                {
                    var lod = Literal(
                        role + ":lod-zero",
                        new JValue(0f),
                        "Scalar");
                    adapter.Connect(graph, lod.node, lod.slot, sample, 4);
                }
                var channel = parameters["channel"]?.Value<string>() ?? "RGB";
                var slot = explicitLod
                    ? (string.Equals(channel, "R", StringComparison.Ordinal) ? 6 : 5)
                    : (string.Equals(channel, "R", StringComparison.Ordinal) ? 4 : 0);
                return new Handle { node = sample, slot = slot };
            }

            Handle SampleImageTexture(string role, JObject expression)
            {
                var parameters =
                    expression["params"] as JObject ?? new JObject();
                var referenceName =
                    parameters["referenceName"]?.Value<string>() ?? "";
                if (string.IsNullOrEmpty(referenceName))
                    throw new InvalidOperationException(
                        "MIKU_EXPRESSION_TEXTURE_REFERENCE_MISSING:" + role);
                if (!properties.TryGetValue(referenceName, out var texture))
                {
                    var usage =
                        parameters["usage"]?.Value<string>() ?? "Color";
                    var semantic =
                        parameters["semantic"]?.Value<string>() ?? "Image";
                    var resourceId =
                        parameters["resourceId"]?.Value<string>() ??
                        referenceName;
                    var property = adapter.CreateTextureProperty(
                        graph,
                        materialId,
                        "image-property:" + resourceId,
                        TextureDisplayName(parameters, semantic),
                        referenceName,
                        string.Equals(
                            usage,
                            "Normal",
                            StringComparison.Ordinal));
                    var propertyNode = adapter.CreatePropertyNode(
                        graph,
                        materialId,
                        "image-property:" + resourceId,
                        property,
                        Position());
                    texture = new Handle
                    {
                        node = propertyNode,
                        slot = 0,
                    };
                    properties[referenceName] = texture;
                }
                var explicitLod = string.Equals(
                    parameters["lodMode"]?.Value<string>(),
                    "Explicit0",
                    StringComparison.Ordinal);
                var normal = string.Equals(
                    parameters["usage"]?.Value<string>(),
                    "Normal",
                    StringComparison.Ordinal);
                var resourceKey =
                    parameters["resourceId"]?.Value<string>() ??
                    referenceName;
                var sampleKey =
                    resourceKey + "|" +
                    (explicitLod ? "lod0" : "fragment") + "|" +
                    (normal ? "normal" : "default") + "|UV0";
                if (!imageSamples.TryGetValue(sampleKey, out var sampleHandle))
                {
                    var sampleRole =
                        "image-sample:" + resourceKey + ":" +
                        (explicitLod ? "lod0" : "fragment");
                    var uv = Native(sampleRole + ":uv0", "UVNode", 0);
                    adapter.SetEnum(uv.node, "uvChannel", "UV0");
                    var sample = adapter.CreateNode(
                        graph,
                        materialId,
                        sampleRole,
                        explicitLod
                            ? "SampleTexture2DLODNode"
                            : "SampleTexture2DNode",
                        Position());
                    if (normal)
                    {
                        adapter.SetEnum(sample, "textureType", "Normal");
                        adapter.SetEnum(sample, "normalMapSpace", "Tangent");
                    }
                    adapter.Connect(
                        graph,
                        texture.node,
                        texture.slot,
                        sample,
                        1);
                    adapter.Connect(graph, uv.node, uv.slot, sample, 2);
                    if (explicitLod)
                    {
                        var lod = Literal(
                            sampleRole + ":lod-zero",
                            new JValue(0f),
                            "Scalar");
                        adapter.Connect(
                            graph,
                            lod.node,
                            lod.slot,
                            sample,
                            4);
                    }
                    sampleHandle = new Handle
                    {
                        node = sample,
                        slot = explicitLod ? 5 : 4,
                    };
                    imageSamples[sampleKey] = sampleHandle;
                }
                var channel =
                    parameters["channel"]?.Value<string>() ?? "RGB";
                var slot = channel switch
                {
                    "R" => sampleHandle.slot,
                    "G" => sampleHandle.slot + 1,
                    "B" => sampleHandle.slot + 2,
                    "A" => sampleHandle.slot + 3,
                    _ => 0,
                };
                return new Handle
                {
                    node = sampleHandle.node,
                    slot = slot,
                };
            }

            static string TextureDisplayName(
                JObject parameters,
                string semantic)
            {
                if (!(parameters["channelBindings"] is JArray bindings) ||
                    bindings.Count < 2)
                    return semantic + " Map";
                var channelOrder = new Dictionary<string, int>(
                    StringComparer.Ordinal)
                {
                    ["R"] = 0,
                    ["G"] = 1,
                    ["B"] = 2,
                    ["A"] = 3,
                };
                var mapping = bindings
                    .OfType<JObject>()
                    .OrderBy(
                        item => channelOrder.TryGetValue(
                            item["channel"]?.Value<string>() ?? "",
                            out var order)
                            ? order
                            : int.MaxValue)
                    .ThenBy(
                        item =>
                            item["semantic"]?.Value<string>() ?? "",
                        StringComparer.Ordinal)
                    .Select(
                        item =>
                            (item["channel"]?.Value<string>() ?? "?") +
                            "=" +
                            (item["semantic"]?.Value<string>() ?? "?"));
                return "Packed PBR Map (" +
                       string.Join(", ", mapping) +
                       ")";
            }

            Handle SampleChannelTexture(
                string role,
                string semantic,
                string resourceId,
                bool explicitLod = false)
            {
                if (string.IsNullOrEmpty(resourceId))
                    throw new InvalidOperationException(
                        "MIKU_CHANNEL_TEXTURE_REFERENCE_MISSING:" + semantic);
                var resource = (ir["resources"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .FirstOrDefault(item => string.Equals(
                        item["id"]?.Value<string>(),
                        resourceId,
                        StringComparison.Ordinal));
                if (resource == null)
                    throw new InvalidOperationException(
                        "MIKU_CHANNEL_TEXTURE_RESOURCE_MISSING:" + resourceId);
                var usage = string.Equals(
                    semantic,
                    "Normal",
                    StringComparison.Ordinal)
                    ? "Normal"
                    : (
                        string.Equals(
                            ChannelTextureReference(semantic),
                            "_BaseMap",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            ChannelTextureReference(semantic),
                            "_EmissionMap",
                            StringComparison.Ordinal)
                            ? "Color"
                            : "Scalar");
                return SampleBakedTexture(
                    role,
                    new JObject
                    {
                        ["params"] = new JObject
                        {
                            ["resourceId"] = resourceId,
                            ["referenceName"] =
                                ChannelTextureReference(semantic),
                            ["usage"] = usage,
                            ["channel"] =
                                resource["channel"]?.Value<string>() ??
                                (usage == "Scalar" ? "R" : "RGB"),
                            ["colorSpace"] =
                                resource["colorSpace"]?.Value<string>() ??
                                "Linear",
                            ["uvSet"] = "UV0",
                            ["lodMode"] = explicitLod ? "Explicit0" : "Implicit",
                        },
                    });
            }

            void ValidateBakedResourceReachability()
            {
                foreach (var resource in
                         (ir["resources"] as JArray ?? new JArray())
                         .OfType<JObject>())
                {
                    var bindingKey =
                        resource["bindingKey"]?.Value<string>() ?? "";
                    if (!bindingKey.StartsWith(
                            "_MIKU_Baked_",
                            StringComparison.Ordinal))
                        continue;
                    if (MikuSurfaceModelBackends
                        .UsesSourceMeshPbrProjection(ir))
                        continue;
                    if (!properties.ContainsKey(bindingKey))
                        throw new InvalidOperationException(
                            "MIKU_GENERATED_RESOURCE_UNREFERENCED:" +
                            bindingKey);
                }
            }

            static string ChannelTextureReference(string semantic)
            {
                return semantic switch
                {
                    "BaseColor" => "_BaseMap",
                    "Metalness" => "_MetallicMap",
                    "Roughness" => "_RoughnessMap",
                    "Normal" => "_BumpMap",
                    "Height" => "_MIKU_HeightMap",
                    "Emission" => "_EmissionMap",
                    "EmissionMask" => "_MIKU_EmissionMask",
                    "Alpha" => "_AlphaMap",
                    "AmbientOcclusion" => "_OcclusionMap",
                    _ => "_MIKU_" + semantic,
                };
            }

            Handle HueSaturationValue(string role, JObject expression)
            {
                var color = Input(expression, "Color");
                var rgbToHsv = adapter.CreateNode(
                    graph,
                    materialId,
                    role + ":rgb-hsv",
                    "ColorspaceConversionNode",
                    Position());
                adapter.SetColorspaceConversion(rgbToHsv, "RGB", "HSV");
                adapter.Connect(graph, color.node, color.slot, rgbToHsv, 0);
                var split = adapter.CreateNode(
                    graph,
                    materialId,
                    role + ":split-hsv",
                    "SplitNode",
                    Position());
                adapter.Connect(graph, rgbToHsv, 1, split, 0);
                var hueShift = Binary(
                    role + ":hue-shift",
                    "SubtractNode",
                    Input(expression, "Hue"),
                    Literal(role + ":hue-center", new JValue(0.5f), "Scalar"));
                var wrappedHue = Unary(
                    role + ":hue-wrap",
                    "FractionNode",
                    Binary(
                        role + ":hue-add",
                        "AddNode",
                        new Handle { node = split, slot = 1 },
                        hueShift));
                var saturation = Binary(
                    role + ":saturation",
                    "MultiplyNode",
                    new Handle { node = split, slot = 2 },
                    Input(expression, "Saturation"));
                var clampedSaturation = adapter.CreateNode(
                    graph,
                    materialId,
                    role + ":saturation-clamp",
                    "ClampNode",
                    Position());
                adapter.Connect(
                    graph,
                    saturation.node,
                    saturation.slot,
                    clampedSaturation,
                    0);
                adapter.SetSlotValue(clampedSaturation, 1, new JValue(0f));
                adapter.SetSlotValue(clampedSaturation, 2, new JValue(1f));
                var value = Binary(
                    role + ":value",
                    "MultiplyNode",
                    new Handle { node = split, slot = 3 },
                    Input(expression, "Value"));
                var combine = adapter.CreateNode(
                    graph,
                    materialId,
                    role + ":combine-hsv",
                    "CombineNode",
                    Position());
                adapter.Connect(graph, wrappedHue.node, wrappedHue.slot, combine, 0);
                adapter.Connect(graph, clampedSaturation, 3, combine, 1);
                adapter.Connect(graph, value.node, value.slot, combine, 2);
                var hsvToRgb = adapter.CreateNode(
                    graph,
                    materialId,
                    role + ":hsv-rgb",
                    "ColorspaceConversionNode",
                    Position());
                adapter.SetColorspaceConversion(hsvToRgb, "HSV", "RGB");
                adapter.Connect(graph, combine, 5, hsvToRgb, 0);
                var lerp = adapter.CreateNode(
                    graph,
                    materialId,
                    role + ":factor",
                    "LerpNode",
                    Position());
                adapter.Connect(graph, color.node, color.slot, lerp, 0);
                adapter.Connect(graph, hsvToRgb, 1, lerp, 1);
                var factor = Input(expression, "Factor");
                adapter.Connect(graph, factor.node, factor.slot, lerp, 2);
                return new Handle { node = lerp, slot = 3 };
            }

            Handle EffectSeconds()
            {
                if (effectSeconds != null)
                    return effectSeconds;
                var unityTime = Native("time:unity", "TimeNode", 0);
                var scale = Property("time-scale", "Effect Time Scale", TimeScaleReference, new JValue(1f));
                var offset = Property("time-offset", "Effect Time Offset", TimeOffsetReference, new JValue(0f));
                var timeOverride = Property("time-override", "Effect Time Override", TimeOverrideReference, new JValue(0f));
                var useOverride = Property("time-use-override", "Use Effect Time Override", UseTimeOverrideReference, new JValue(0f));
                var saturated = Unary("time-use-override-saturate", "SaturateNode", useOverride);
                var lerp = adapter.CreateNode(graph, materialId, "time-base", "LerpNode", Position());
                adapter.Connect(graph, unityTime.node, unityTime.slot, lerp, 0);
                adapter.Connect(graph, timeOverride.node, timeOverride.slot, lerp, 1);
                adapter.Connect(graph, saturated.node, saturated.slot, lerp, 2);
                var scaled = Binary(
                    "time-scaled",
                    "MultiplyNode",
                    new Handle { node = lerp, slot = 3 },
                    scale);
                effectSeconds = Binary("time-seconds", "AddNode", scaled, offset);
                return effectSeconds;
            }

            Handle TimeExpression(string role, string op, JObject parameters)
            {
                var seconds = EffectSeconds();
                if (op == "Input.Time.Seconds")
                    return seconds;
                if (op == "Input.Time.Sine")
                    return Unary(role, "SineNode", seconds);
                if (op == "Input.Time.Cosine")
                    return Unary(role, "CosineNode", seconds);
                var fps = Literal(
                    role + ":fps",
                    parameters?["sourceFps"] ?? new JValue(24f),
                    "Scalar");
                var start = Literal(
                    role + ":frame-start",
                    parameters?["frameStart"] ?? new JValue(1f),
                    "Scalar");
                return Binary(
                    role + ":frame",
                    "AddNode",
                    Binary(role + ":seconds-fps", "MultiplyNode", seconds, fps),
                    start);
            }

            Handle LightPath(string role, bool cameraRay)
            {
                var node = adapter.CreateLightPathNode(
                    graph,
                    materialId,
                    role,
                    Position());
                return new Handle
                {
                    node = node,
                    slot = cameraRay ? 0 : 1,
                };
            }

            Handle LegacyTime(string role, string op)
            {
                var slots = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Input.Time.LegacyTime"] = 0,
                    ["Input.Time.LegacySine"] = 1,
                    ["Input.Time.LegacyCosine"] = 2,
                    ["Input.Time.LegacyDelta"] = 3,
                    ["Input.Time.LegacySmoothDelta"] = 4,
                };
                return Native(role, "TimeNode", slots[op]);
            }

            Handle DielectricFresnel(string role, JObject expression, bool layerWeight)
            {
                var normal = Unary(role + ":normal", "NormalizeNode", Input(expression, "Normal"));
                var view = Unary(role + ":view", "NormalizeNode", Input(expression, "ViewDirection"));
                var c = Unary(
                    role + ":cos-abs",
                    "AbsoluteNode",
                    Binary(role + ":dot", "DotProductNode", view, normal));
                Handle eta;
                if (layerWeight)
                {
                    var blend = Input(expression, "Blend");
                    eta = Binary(
                        role + ":eta-clamp",
                        "MaximumNode",
                        Binary(
                            role + ":one-minus-blend",
                            "SubtractNode",
                            Literal(role + ":one", new JValue(1f), "Scalar"),
                            blend),
                        Literal(role + ":epsilon", new JValue(0.00001f), "Scalar"));
                    eta = Branch(
                        role + ":eta-face",
                        Input(expression, "IsFrontFace"),
                        Binary(
                            role + ":eta-reciprocal",
                            "DivideNode",
                            Literal(role + ":one-recip", new JValue(1f), "Scalar"),
                            eta),
                        eta);
                }
                else
                {
                    eta = Binary(
                        role + ":eta-clamp",
                        "MaximumNode",
                        Input(expression, "IOR"),
                        Literal(role + ":epsilon", new JValue(0.00001f), "Scalar"));
                    eta = Branch(
                        role + ":eta-face",
                        Input(expression, "IsFrontFace"),
                        eta,
                        Binary(
                            role + ":eta-reciprocal",
                            "DivideNode",
                            Literal(role + ":one-recip", new JValue(1f), "Scalar"),
                            eta));
                }
                return FresnelCos(role, c, eta);
            }

            Handle FresnelCos(string role, Handle c, Handle eta)
            {
                var one = Literal(role + ":one-fresnel", new JValue(1f), "Scalar");
                var eta2 = Binary(role + ":eta2", "MultiplyNode", eta, eta);
                var c2 = Binary(role + ":c2", "MultiplyNode", c, c);
                var g2 = Binary(
                    role + ":g2-add-c2",
                    "AddNode",
                    Binary(role + ":g2-minus-one", "SubtractNode", eta2, one),
                    c2);
                var positive = Compare(
                    role + ":g2-positive",
                    "Greater",
                    g2,
                    Literal(role + ":zero", new JValue(0f), "Scalar"));
                var g = Unary(
                    role + ":g",
                    "SquareRootNode",
                    Binary(
                        role + ":g2-safe",
                        "MaximumNode",
                        g2,
                        Literal(role + ":zero-safe", new JValue(0f), "Scalar")));
                var gMinusC = Binary(role + ":g-minus-c", "SubtractNode", g, c);
                var gPlusC = Binary(role + ":g-plus-c", "AddNode", g, c);
                var a = Binary(role + ":a", "DivideNode", gMinusC, gPlusC);
                var cgPlusC = Binary(role + ":c-g-plus-c", "MultiplyNode", c, gPlusC);
                var cgMinusC = Binary(role + ":c-g-minus-c", "MultiplyNode", c, gMinusC);
                var b = Binary(
                    role + ":b",
                    "DivideNode",
                    Binary(role + ":b-numerator", "SubtractNode", cgPlusC, one),
                    Binary(role + ":b-denominator", "AddNode", cgMinusC, one));
                var a2 = Binary(role + ":a2", "MultiplyNode", a, a);
                var b2 = Binary(role + ":b2", "MultiplyNode", b, b);
                var result = Binary(
                    role + ":fresnel-half",
                    "MultiplyNode",
                    Literal(role + ":half", new JValue(0.5f), "Scalar"),
                    Binary(
                        role + ":a2-times",
                        "MultiplyNode",
                        a2,
                        Binary(role + ":one-plus-b2", "AddNode", one, b2)));
                return Branch(role + ":tir", positive, result, one);
            }

            Handle LayerWeightFacing(string role, JObject expression)
            {
                var normal = Unary(role + ":normal", "NormalizeNode", Input(expression, "Normal"));
                var view = Unary(role + ":view", "NormalizeNode", Input(expression, "ViewDirection"));
                var facing = Unary(
                    role + ":facing-abs",
                    "AbsoluteNode",
                    Binary(role + ":dot", "DotProductNode", view, normal));
                var blend = Input(expression, "Blend");
                var half = Literal(role + ":half", new JValue(0.5f), "Scalar");
                var notHalf = Compare(role + ":not-half", "NotEqual", blend, half);
                var clamped = adapter.CreateNode(graph, materialId, role + ":clamp", "ClampNode", Position());
                adapter.Connect(graph, blend.node, blend.slot, clamped, 0);
                adapter.SetSlotValue(clamped, 1, new JValue(0f));
                adapter.SetSlotValue(clamped, 2, new JValue(0.99999f));
                var clampHandle = new Handle { node = clamped, slot = 3 };
                var belowHalf = Compare(role + ":below-half", "Less", clampHandle, half);
                var exponent = Branch(
                    role + ":exponent",
                    belowHalf,
                    Binary(
                        role + ":twice-blend",
                        "MultiplyNode",
                        Literal(role + ":two", new JValue(2f), "Scalar"),
                        clampHandle),
                    Binary(
                        role + ":half-over-one-minus",
                        "DivideNode",
                        half,
                        Binary(
                            role + ":one-minus-blend",
                            "SubtractNode",
                            Literal(role + ":one-exp", new JValue(1f), "Scalar"),
                            clampHandle)));
                var powered = Binary(role + ":pow", "PowerNode", facing, exponent);
                var shaped = Branch(role + ":half-rule", notHalf, powered, facing);
                return Unary(role + ":one-minus", "OneMinusNode", shaped);
            }

            Vector2 Position()
            {
                var index = positionIndex++;
                return new Vector2(20f + (index % 7) * 260f, 20f + (index / 7) * 180f);
            }
        }

        public static bool HasRuntimeExpressions(JObject ir)
        {
            return (ir?["expressions"] as JArray)?.Count > 0;
        }

        public static string Generate(JObject ir, string materialId)
        {
            ValidateShaderStages(ir);
            return StabilizeMultiJson(
                new Builder(ir, materialId).Build(),
                materialId);
        }

        public static string GenerateWrapper(
            string templateText,
            string runtimeSubGraphText,
            string materialId,
            string subGraphGuid,
            JObject surfaceContract)
        {
            var objects = ParseMultiJson(templateText);
            var runtimeObjects = ParseMultiJson(runtimeSubGraphText);
            var graph = objects.First(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith("GraphData", StringComparison.Ordinal));
            var runtimeGraph = runtimeObjects.First(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith("GraphData", StringComparison.Ordinal));
            var byId = objects
                .Where(item => !string.IsNullOrEmpty(
                    item["m_ObjectId"]?.Value<string>()))
                .ToDictionary(
                    item => item["m_ObjectId"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            var runtimeById = runtimeObjects
                .Where(item => !string.IsNullOrEmpty(
                    item["m_ObjectId"]?.Value<string>()))
                .ToDictionary(
                    item => item["m_ObjectId"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            var subGraphNode = objects.First(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith("SubGraphNode", StringComparison.Ordinal));
            var serializedSubGraph =
                subGraphNode["m_SerializedSubGraph"];
            if (serializedSubGraph?.Type == JTokenType.String)
            {
                var reference = JObject.Parse(
                    serializedSubGraph.Value<string>() ?? "{}");
                reference["subGraph"]["guid"] = subGraphGuid;
                subGraphNode["m_SerializedSubGraph"] =
                    reference.ToString(Formatting.Indented);
            }
            else
            {
                serializedSubGraph["subGraph"]["guid"] =
                    subGraphGuid;
            }
            var priorPropertyIds = new HashSet<int>(
                (subGraphNode["m_PropertyIds"] as JArray ?? new JArray())
                .Values<int>());
            var priorInputSlots = (subGraphNode["m_Slots"] as JArray ??
                                   new JArray())
                .OfType<JObject>()
                .Where(reference =>
                {
                    var id = reference["m_Id"]?.Value<string>() ?? "";
                    return byId.TryGetValue(id, out var slot) &&
                           priorPropertyIds.Contains(
                               slot["m_Id"]?.Value<int>() ?? 0);
                })
                .ToArray();
            var priorInputObjectIds = new HashSet<string>(
                priorInputSlots.Select(item =>
                    item["m_Id"]?.Value<string>() ?? ""),
                StringComparer.Ordinal);
            var inputSlotTemplates = priorInputSlots
                .Select(item => byId[item["m_Id"].Value<string>()])
                .Where(item =>
                {
                    var type = item["m_Type"]?.Value<string>() ?? "";
                    return type.EndsWith(
                               "Vector1MaterialSlot",
                               StringComparison.Ordinal) ||
                           type.EndsWith(
                               "Texture2DMaterialSlot",
                               StringComparison.Ordinal);
                })
                .ToList();
            if (!inputSlotTemplates.Any(item =>
                    (item["m_Type"]?.Value<string>() ?? "")
                    .EndsWith(
                        "Texture2DMaterialSlot",
                        StringComparison.Ordinal)))
            {
                var textureSlotTemplate = objects.FirstOrDefault(item =>
                    (item["m_Type"]?.Value<string>() ?? "")
                    .EndsWith(
                        "Texture2DMaterialSlot",
                        StringComparison.Ordinal));
                if (textureSlotTemplate != null)
                    inputSlotTemplates.Add(textureSlotTemplate);
            }
            subGraphNode["m_Slots"] = new JArray(
                (subGraphNode["m_Slots"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Where(item => !priorInputObjectIds.Contains(
                    item["m_Id"]?.Value<string>() ?? "")));
            subGraphNode["m_PropertyGuids"] = new JArray();
            subGraphNode["m_PropertyIds"] = new JArray();
            graph["m_Edges"] = new JArray(
                (graph["m_Edges"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Where(edge =>
                    !string.Equals(
                        edge["m_InputSlot"]?["m_Node"]?["m_Id"]
                            ?.Value<string>(),
                        subGraphNode["m_ObjectId"]?.Value<string>(),
                        StringComparison.Ordinal) ||
                    !priorPropertyIds.Contains(
                        edge["m_InputSlot"]?["m_SlotId"]?.Value<int>() ?? 0)));
            objects.RemoveAll(item => priorInputObjectIds.Contains(
                item["m_ObjectId"]?.Value<string>() ?? ""));

            var runtimeOutputId =
                runtimeGraph["m_OutputNode"]?["m_Id"]?.Value<string>() ?? "";
            if (!runtimeById.TryGetValue(
                    runtimeOutputId,
                    out var runtimeOutputNode))
                throw new InvalidDataException(
                    "MIKU_WRAPPER_SUBGRAPH_OUTPUT_NODE_MISSING");
            var wrapperOutputSlots = (subGraphNode["m_Slots"] as JArray ??
                                      new JArray())
                .OfType<JObject>()
                .Select(reference =>
                {
                    byId.TryGetValue(
                        reference["m_Id"]?.Value<string>() ?? "",
                        out var slot);
                    return slot;
                })
                .Where(slot =>
                    slot != null &&
                    slot["m_SlotType"]?.Value<int>() == 1)
                .ToList();
            foreach (var reference in
                     (runtimeOutputNode["m_Slots"] as JArray ??
                      new JArray()).OfType<JObject>())
            {
                if (!runtimeById.TryGetValue(
                        reference["m_Id"]?.Value<string>() ?? "",
                        out var runtimeSlot))
                    continue;
                var displayName =
                    runtimeSlot["m_DisplayName"]?.Value<string>() ?? "";
                if (wrapperOutputSlots.Any(slot =>
                        string.Equals(
                            slot["m_DisplayName"]?.Value<string>(),
                            displayName,
                            StringComparison.Ordinal)))
                    continue;
                var runtimeType =
                    runtimeSlot["m_Type"]?.Value<string>() ?? "";
                var template = wrapperOutputSlots.FirstOrDefault(slot =>
                    string.Equals(
                        slot["m_Type"]?.Value<string>(),
                        runtimeType,
                        StringComparison.Ordinal));
                if (template == null)
                    throw new InvalidDataException(
                        "MIKU_WRAPPER_SUBGRAPH_OUTPUT_TYPE_UNSUPPORTED:" +
                        runtimeType);
                var outputSlot = (JObject)template.DeepClone();
                var outputObjectId = StableId(
                    materialId,
                    "wrapper-subgraph-output:" + displayName);
                outputSlot["m_ObjectId"] = outputObjectId;
                outputSlot["m_Id"] = runtimeSlot["m_Id"]?.DeepClone();
                outputSlot["m_DisplayName"] = displayName;
                outputSlot["m_ShaderOutputName"] =
                    runtimeSlot["m_ShaderOutputName"]?.DeepClone() ??
                    new JValue(displayName);
                outputSlot["m_SlotType"] = 1;
                outputSlot["m_Value"] =
                    runtimeSlot["m_Value"]?.DeepClone() ??
                    outputSlot["m_Value"];
                outputSlot["m_DefaultValue"] =
                    runtimeSlot["m_DefaultValue"]?.DeepClone() ??
                    outputSlot["m_DefaultValue"];
                objects.Add(outputSlot);
                byId[outputObjectId] = outputSlot;
                wrapperOutputSlots.Add(outputSlot);
                ((JArray)subGraphNode["m_Slots"]).Add(
                    new JObject { ["m_Id"] = outputObjectId });
            }

            var wrapperFloatTemplate = objects.First(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(
                    "Vector1ShaderProperty",
                    StringComparison.Ordinal));
            var wrapperTextureTemplate = objects.First(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(
                    "Texture2DShaderProperty",
                    StringComparison.Ordinal));
            var propertyNodeTemplates = objects.Where(item =>
                string.Equals(
                    item["m_Type"]?.Value<string>(),
                    "UnityEditor.ShaderGraph.PropertyNode",
                    StringComparison.Ordinal) &&
                byId.TryGetValue(
                    item["m_Property"]?["m_Id"]?.Value<string>() ?? "",
                    out var property) &&
                (
                    (property["m_Type"]?.Value<string>() ?? "")
                    .EndsWith(
                        "Vector1ShaderProperty",
                        StringComparison.Ordinal) ||
                    (property["m_Type"]?.Value<string>() ?? "")
                    .EndsWith(
                        "Texture2DShaderProperty",
                        StringComparison.Ordinal)
                )).ToArray();
            var wrapperProperties = objects
                .Where(item =>
                    (item["m_Type"]?.Value<string>() ?? "")
                    .Contains("ShaderProperty"))
                .ToDictionary(
                    PropertyReference,
                    item => item,
                    StringComparer.Ordinal);
            var internalCategory = objects.FirstOrDefault(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                    .EndsWith("CategoryData", StringComparison.Ordinal) &&
                string.IsNullOrEmpty(item["m_Name"]?.Value<string>()));
            var runtimeProperties = (runtimeGraph["m_Properties"] as JArray ??
                                     new JArray())
                .OfType<JObject>()
                .Select(reference =>
                    runtimeById[reference["m_Id"].Value<string>()])
                .Where(item =>
                {
                    var type = item["m_Type"]?.Value<string>() ?? "";
                    return type.EndsWith(
                               "Vector1ShaderProperty",
                               StringComparison.Ordinal) ||
                           type.EndsWith(
                               "Texture2DShaderProperty",
                               StringComparison.Ordinal);
                })
                .OrderBy(PropertyReference, StringComparer.Ordinal)
                .ToArray();
            var nodeIndex = 0;
            foreach (var runtimeProperty in runtimeProperties)
            {
                var referenceName = PropertyReference(runtimeProperty);
                var displayName =
                    runtimeProperty["m_Name"]?.Value<string>() ??
                    referenceName;
                var runtimeType =
                    runtimeProperty["m_Type"]?.Value<string>() ?? "";
                var isTexture = runtimeType.EndsWith(
                    "Texture2DShaderProperty",
                    StringComparison.Ordinal);
                var defaultValue = isTexture
                    ? null
                    : (JToken)new JValue(
                        runtimeProperty["m_Value"]?.Value<float>() ?? 0f);
                if (!wrapperProperties.TryGetValue(
                        referenceName,
                        out var wrapperProperty))
                {
                    wrapperProperty =
                        (JObject)(isTexture
                            ? wrapperTextureTemplate
                            : wrapperFloatTemplate).DeepClone();
                    var propertyId = StableId(
                        materialId,
                        "wrapper-property:" + referenceName);
                    wrapperProperty["m_ObjectId"] = propertyId;
                    wrapperProperty["m_Guid"]["m_GuidSerialized"] =
                        FormatGuid(StableId(
                            materialId,
                            "wrapper-property-guid:" + referenceName));
                    wrapperProperty["m_Name"] = displayName;
                    wrapperProperty["m_RefNameGeneratedByDisplayName"] =
                        displayName;
                    wrapperProperty["m_DefaultReferenceName"] =
                        referenceName;
                    wrapperProperty["m_OverrideReferenceName"] =
                        referenceName;
                    wrapperProperty["m_GeneratePropertyBlock"] = true;
                    wrapperProperty["overrideHLSLDeclaration"] = false;
                    wrapperProperty["hlslDeclarationOverride"] = 0;
                    wrapperProperty["m_Hidden"] =
                        !IsVisiblePbrRuntimeProperty(referenceName);
                    if (!isTexture)
                        wrapperProperty["m_Value"] = defaultValue;
                    else
                    {
                        wrapperProperty["m_DefaultType"] =
                            runtimeProperty["m_DefaultType"]?.DeepClone() ??
                            wrapperProperty["m_DefaultType"];
                        wrapperProperty["m_UseTilingAndOffset"] = false;
                        wrapperProperty["m_UseTexelSize"] = false;
                        wrapperProperty["m_IsHDR"] = false;
                    }
                    objects.Add(wrapperProperty);
                    byId[propertyId] = wrapperProperty;
                    ((JArray)graph["m_Properties"]).Add(
                        new JObject { ["m_Id"] = propertyId });
                    (internalCategory?["m_ChildObjectList"] as JArray)?.Add(
                        new JObject { ["m_Id"] = propertyId });
                    wrapperProperties[referenceName] = wrapperProperty;
                }
                else
                {
                    wrapperProperty["m_GeneratePropertyBlock"] = true;
                    wrapperProperty["overrideHLSLDeclaration"] = false;
                    wrapperProperty["hlslDeclarationOverride"] = 0;
                    if (IsVisiblePbrRuntimeProperty(referenceName))
                        wrapperProperty["m_Hidden"] = false;
                }

                var propertyNodeTemplate = propertyNodeTemplates.First(item =>
                {
                    var propertyId =
                        item["m_Property"]?["m_Id"]?.Value<string>() ?? "";
                    var propertyType = byId[propertyId]["m_Type"]
                        ?.Value<string>() ?? "";
                    return isTexture
                        ? propertyType.EndsWith(
                            "Texture2DShaderProperty",
                            StringComparison.Ordinal)
                        : propertyType.EndsWith(
                            "Vector1ShaderProperty",
                            StringComparison.Ordinal);
                });
                var propertyNodeSlotTemplate = byId[
                    propertyNodeTemplate["m_Slots"][0]["m_Id"].Value<string>()];
                var propertyNode =
                    (JObject)propertyNodeTemplate.DeepClone();
                var propertyNodeId = StableId(
                    materialId,
                    "wrapper-property-node:" + referenceName);
                var propertyNodeSlotId = StableId(
                    materialId,
                    "wrapper-property-node-slot:" + referenceName);
                propertyNode["m_ObjectId"] = propertyNodeId;
                propertyNode["m_Property"]["m_Id"] =
                    wrapperProperty["m_ObjectId"];
                propertyNode["m_Slots"] = new JArray(
                    new JObject { ["m_Id"] = propertyNodeSlotId });
                propertyNode["m_DrawState"]["m_Position"] =
                    new JObject
                    {
                        ["serializedVersion"] = "2",
                        ["x"] = -1800f,
                        ["y"] = -600f + nodeIndex * 150f,
                        ["width"] = 220f,
                        ["height"] = 120f,
                    };
                var propertyNodeSlot =
                    (JObject)propertyNodeSlotTemplate.DeepClone();
                propertyNodeSlot["m_ObjectId"] = propertyNodeSlotId;
                propertyNodeSlot["m_DisplayName"] = displayName;
                objects.Add(propertyNode);
                objects.Add(propertyNodeSlot);
                ((JArray)graph["m_Nodes"]).Add(
                    new JObject { ["m_Id"] = propertyNodeId });

                var inputSlotTemplate = inputSlotTemplates.First(item =>
                    isTexture
                        ? (item["m_Type"]?.Value<string>() ?? "")
                            .EndsWith(
                                "Texture2DMaterialSlot",
                                StringComparison.Ordinal)
                        : (item["m_Type"]?.Value<string>() ?? "")
                            .EndsWith(
                                "Vector1MaterialSlot",
                                StringComparison.Ordinal));
                var inputSlot = (JObject)inputSlotTemplate.DeepClone();
                var inputSlotObjectId = StableId(
                    materialId,
                    "wrapper-subgraph-input:" + referenceName);
                var runtimePropertyGuid =
                    runtimeProperty["m_Guid"]?["m_GuidSerialized"]
                        ?.Value<string>() ??
                    throw new InvalidOperationException(
                        "MIKU_RUNTIME_PROPERTY_GUID_MISSING:" +
                        referenceName);
                var propertySlotId =
                    new Guid(runtimePropertyGuid).GetHashCode();
                inputSlot["m_ObjectId"] = inputSlotObjectId;
                inputSlot["m_Id"] = propertySlotId;
                inputSlot["m_DisplayName"] = displayName;
                inputSlot["m_ShaderOutputName"] = referenceName;
                inputSlot["m_SlotType"] = 0;
                if (!isTexture)
                {
                    inputSlot["m_Value"] = defaultValue;
                    inputSlot["m_DefaultValue"] = defaultValue;
                }
                objects.Add(inputSlot);
                ((JArray)subGraphNode["m_Slots"]).Add(
                    new JObject { ["m_Id"] = inputSlotObjectId });
                ((JArray)subGraphNode["m_PropertyGuids"]).Add(
                    runtimePropertyGuid);
                ((JArray)subGraphNode["m_PropertyIds"]).Add(propertySlotId);
                ((JArray)graph["m_Edges"]).Add(
                    new JObject
                    {
                        ["m_OutputSlot"] = new JObject
                        {
                            ["m_Node"] = new JObject
                            {
                                ["m_Id"] = propertyNodeId,
                            },
                            ["m_SlotId"] = 0,
                        },
                        ["m_InputSlot"] = new JObject
                        {
                            ["m_Node"] = new JObject
                            {
                                ["m_Id"] =
                                    subGraphNode["m_ObjectId"],
                            },
                            ["m_SlotId"] = propertySlotId,
                        },
                    });
                nodeIndex++;
            }
            ConnectSurfaceOutputs(
                graph,
                runtimeGraph,
                byId,
                runtimeById,
                subGraphNode,
                surfaceContract);
            return string.Join(
                       "\n\n",
                       objects.Select(item =>
                           item.ToString(Formatting.Indented))) +
                   "\n";
        }

        static void ConnectSurfaceOutputs(
            JObject graph,
            JObject runtimeGraph,
            IDictionary<string, JObject> wrapperById,
            IDictionary<string, JObject> runtimeById,
            JObject subGraphNode,
            JObject surfaceContract)
        {
            ConnectSurfaceOutput(
                graph,
                runtimeGraph,
                wrapperById,
                runtimeById,
                subGraphNode,
                "Vertex Position",
                "VertexDescription.Position");
            if (surfaceContract == null)
                return;
            if (string.Equals(
                    surfaceContract["model"]?.Value<string>(),
                    "DielectricScreenRefraction",
                    StringComparison.Ordinal))
            {
                ConnectSurfaceOutput(
                    graph,
                    runtimeGraph,
                    wrapperById,
                    runtimeById,
                    subGraphNode,
                    "Base Color",
                    "SurfaceDescription.BaseColor");
            }
            if (!string.Equals(
                    surfaceContract["renderMethod"]?.Value<string>(),
                    "Opaque",
                    StringComparison.Ordinal))
            {
                ConnectSurfaceOutput(
                    graph,
                    runtimeGraph,
                    wrapperById,
                    runtimeById,
                    subGraphNode,
                    "Alpha",
                    "SurfaceDescription.Alpha");
            }
            if (surfaceContract["clearCoat"]?.Value<bool>() ?? false)
            {
                ConnectSurfaceOutput(
                    graph,
                    runtimeGraph,
                    wrapperById,
                    runtimeById,
                    subGraphNode,
                    "Coat Mask",
                    "SurfaceDescription.CoatMask");
                ConnectSurfaceOutput(
                    graph,
                    runtimeGraph,
                    wrapperById,
                    runtimeById,
                    subGraphNode,
                    "Coat Smoothness",
                    "SurfaceDescription.CoatSmoothness");
            }
        }

        static void ConnectSurfaceOutput(
            JObject graph,
            JObject runtimeGraph,
            IDictionary<string, JObject> wrapperById,
            IDictionary<string, JObject> runtimeById,
            JObject subGraphNode,
            string outputName,
            string blockName)
        {
            var outputId =
                runtimeGraph["m_OutputNode"]?["m_Id"]?.Value<string>() ?? "";
            if (!runtimeById.TryGetValue(outputId, out var outputNode))
                throw new InvalidDataException(
                    "MIKU_WRAPPER_SUBGRAPH_OUTPUT_NODE_MISSING");
            var outputSlot = (outputNode["m_Slots"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(item =>
                {
                    runtimeById.TryGetValue(
                        item["m_Id"]?.Value<string>() ?? "",
                        out var slot);
                    return slot;
                })
                .FirstOrDefault(slot =>
                    slot != null &&
                    string.Equals(
                        slot["m_DisplayName"]?.Value<string>(),
                        outputName,
                        StringComparison.Ordinal));
            if (outputSlot == null)
                throw new InvalidDataException(
                    "MIKU_WRAPPER_SUBGRAPH_OUTPUT_MISSING:" + outputName);
            var block = wrapperById.Values.FirstOrDefault(item =>
                string.Equals(
                    item["m_Name"]?.Value<string>(),
                    blockName,
                    StringComparison.Ordinal));
            if (block == null)
                throw new InvalidDataException(
                    "MIKU_WRAPPER_SURFACE_BLOCK_MISSING:" + blockName);
            var edges = graph["m_Edges"] as JArray ?? new JArray();
            var blockId = block["m_ObjectId"]?.Value<string>() ?? "";
            var retained = edges
                .OfType<JObject>()
                .Where(edge => !string.Equals(
                    edge["m_InputSlot"]?["m_Node"]?["m_Id"]
                        ?.Value<string>(),
                    blockId,
                    StringComparison.Ordinal))
                .ToArray();
            edges.Clear();
            foreach (var edge in retained)
                edges.Add(edge);
            edges.Add(
                new JObject
                {
                    ["m_OutputSlot"] = new JObject
                    {
                        ["m_Node"] = new JObject
                        {
                            ["m_Id"] =
                                subGraphNode["m_ObjectId"]?.Value<string>(),
                        },
                        ["m_SlotId"] =
                            outputSlot["m_Id"]?.Value<int>() ?? 0,
                    },
                    ["m_InputSlot"] = new JObject
                    {
                        ["m_Node"] = new JObject
                        {
                            ["m_Id"] = blockId,
                        },
                        ["m_SlotId"] = 0,
                    },
                });
            graph["m_Edges"] = edges;
        }

        public static string[] RuntimePropertyReferences(
            string runtimeSubGraphText)
        {
            var objects = ParseMultiJson(runtimeSubGraphText);
            var graph = objects.First(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith("GraphData", StringComparison.Ordinal));
            var byId = objects
                .Where(item => !string.IsNullOrEmpty(
                    item["m_ObjectId"]?.Value<string>()))
                .ToDictionary(
                    item => item["m_ObjectId"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            return (graph["m_Properties"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(item => byId[item["m_Id"].Value<string>()])
                .Select(PropertyReference)
                .Where(item => !string.IsNullOrEmpty(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
        }

        public static string ApplyWrapperContract(
            string wrapperText,
            JObject surfaceContract)
        {
            if (surfaceContract == null)
                return wrapperText;
            var objects = ParseMultiJson(wrapperText);
            var target = objects.FirstOrDefault(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(".UniversalTarget", StringComparison.Ordinal));
            if (target == null)
                throw new InvalidDataException(
                    "MIKU_WRAPPER_UNIVERSAL_TARGET_MISSING");
            var renderMethod =
                surfaceContract["renderMethod"]?.Value<string>() ?? "Opaque";
            target["m_SurfaceType"] =
                string.Equals(
                    renderMethod,
                    "AlphaBlend",
                    StringComparison.Ordinal) ||
                string.Equals(
                    surfaceContract["model"]?.Value<string>(),
                    "DielectricScreenRefraction",
                    StringComparison.Ordinal)
                    ? 1
                    : 0;
            target["m_ZWriteControl"] =
                string.Equals(
                    renderMethod,
                    "AlphaBlend",
                    StringComparison.Ordinal) ||
                string.Equals(
                    surfaceContract["model"]?.Value<string>(),
                    "DielectricScreenRefraction",
                    StringComparison.Ordinal)
                    ? 2
                    : string.Equals(
                        renderMethod,
                        "Dithered",
                        StringComparison.Ordinal)
                        ? 1
                        : 0;
            target["m_AlphaMode"] = WrapperAlphaMode(surfaceContract);
            target["m_AlphaClip"] = string.Equals(
                renderMethod,
                "Dithered",
                StringComparison.Ordinal);
            var renderFace =
                surfaceContract["renderFace"]?.Value<string>() ?? "Both";
            target["m_RenderFace"] = string.Equals(
                renderFace,
                "Front",
                StringComparison.Ordinal)
                ? 2
                : string.Equals(
                    renderFace,
                    "Back",
                    StringComparison.Ordinal)
                    ? 1
                    : 0;
            var subTarget = ResolveActiveSubTarget(objects, target);
            var clearCoat =
                surfaceContract["clearCoat"]?.Value<bool>() ?? false;
            if (clearCoat &&
                (subTarget == null ||
                 !(subTarget["m_Type"]?.Value<string>() ?? "")
                 .EndsWith(
                     ".UniversalLitSubTarget",
                     StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    "MIKU_WRAPPER_CLEAR_COAT_SUBTARGET_MISSING");
            }
            if (subTarget != null &&
                (subTarget["m_Type"]?.Value<string>() ?? "")
                .EndsWith(
                    ".UniversalLitSubTarget",
                    StringComparison.Ordinal))
                subTarget["m_ClearCoat"] = clearCoat;
            return string.Join(
                       "\n\n",
                       objects.Select(item =>
                           item.ToString(Formatting.Indented))) +
                   "\n";
        }

        static int WrapperAlphaMode(JObject surfaceContract)
        {
            var blendMode =
                surfaceContract["blendMode"]?.Value<string>() ?? "";
            if (string.IsNullOrEmpty(blendMode) ||
                string.Equals(
                    blendMode,
                    "Alpha",
                    StringComparison.Ordinal) ||
                string.Equals(
                    blendMode,
                    "Off",
                    StringComparison.Ordinal))
                return 0;
            if (string.Equals(
                    blendMode,
                    "Premultiply",
                    StringComparison.Ordinal))
                return 1;
            if (string.Equals(
                    blendMode,
                    "Additive",
                    StringComparison.Ordinal))
                return 2;
            if (string.Equals(
                    blendMode,
                    "Multiply",
                    StringComparison.Ordinal))
                return 3;
            throw new InvalidDataException(
                "MIKU_BLEND_MODE_UNSUPPORTED:" + blendMode);
        }

        public static bool WrapperRenderContractMatches(
            string wrapperText,
            JObject surfaceContract)
        {
            if (surfaceContract == null)
                return true;
            var objects = ParseMultiJson(wrapperText);
            var target = objects.FirstOrDefault(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith(".UniversalTarget", StringComparison.Ordinal));
            if (target == null)
                return false;
            var expectedText =
                ApplyWrapperContract(wrapperText, surfaceContract);
            var expectedObjects = ParseMultiJson(expectedText);
            var expectedTarget = expectedObjects
                .First(item =>
                    (item["m_Type"]?.Value<string>() ?? "")
                    .EndsWith(".UniversalTarget", StringComparison.Ordinal));
            foreach (var field in new[]
                     {
                         "m_SurfaceType",
                         "m_ZWriteControl",
                         "m_AlphaMode",
                         "m_RenderFace",
                         "m_AlphaClip",
                     })
            {
                if (!JToken.DeepEquals(target[field], expectedTarget[field]))
                    return false;
            }
            var actualSubTarget = ResolveActiveSubTarget(objects, target);
            var expectedSubTarget =
                ResolveActiveSubTarget(expectedObjects, expectedTarget);
            var expectedClearCoat =
                expectedSubTarget?["m_ClearCoat"]?.Value<bool>() ?? false;
            var actualClearCoat =
                actualSubTarget?["m_ClearCoat"]?.Value<bool>() ?? false;
            if (actualClearCoat != expectedClearCoat)
                return false;
            if (string.Equals(
                    surfaceContract["model"]?.Value<string>(),
                    "DielectricScreenRefraction",
                    StringComparison.Ordinal))
            {
                var subTargetReference =
                    target["m_ActiveSubTarget"]?["m_Id"]?.Value<string>() ??
                    "";
                var subTarget = objects.FirstOrDefault(item =>
                    string.Equals(
                        item["m_ObjectId"]?.Value<string>(),
                        subTargetReference,
                        StringComparison.Ordinal));
                if (subTarget == null ||
                    !(subTarget["m_Type"]?.Value<string>() ?? "")
                    .EndsWith(
                        ".UniversalUnlitSubTarget",
                        StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        static JObject ResolveActiveSubTarget(
            IEnumerable<JObject> objects,
            JObject target)
        {
            var subTargetReference =
                target?["m_ActiveSubTarget"]?["m_Id"]?.Value<string>() ?? "";
            return objects.FirstOrDefault(item => string.Equals(
                item["m_ObjectId"]?.Value<string>(),
                subTargetReference,
                StringComparison.Ordinal));
        }

        static List<JObject> ParseMultiJson(string text)
        {
            var objects = new List<JObject>();
            using (var reader = new JsonTextReader(
                new StringReader(text ?? ""))
            {
                SupportMultipleContent = true,
            })
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.StartObject)
                        objects.Add(JObject.Load(reader));
                }
            }
            return objects;
        }

        static string PropertyReference(JObject property)
        {
            return property["m_OverrideReferenceName"]?.Value<string>() ??
                   property["m_DefaultReferenceName"]?.Value<string>() ??
                   "";
        }

        static bool IsVisiblePbrRuntimeProperty(string referenceName)
        {
            return referenceName == "_OcclusionMap" ||
                   referenceName == "_AlphaMap" ||
                   referenceName == "_MIKU_HeightMap" ||
                   referenceName == "_MIKU_EmissionMask" ||
                   referenceName == "_MIKU_BumpStrength" ||
                   referenceName == "_MIKU_BumpDistance" ||
                   referenceName == "_MIKU_HeightMidlevel" ||
                   referenceName == "_MIKU_HeightScale" ||
                   referenceName == "_Opacity" ||
                   referenceName == "_AlphaClipThreshold" ||
                   referenceName.StartsWith(
                       "_MIKU_Packed_",
                       StringComparison.Ordinal);
        }

        static string FormatGuid(string value)
        {
            var compact = (value ?? "").Replace("-", "");
            if (compact.Length != 32)
                throw new InvalidOperationException(
                    "MIKU_RUNTIME_PROPERTY_GUID_INVALID:" + value);
            return compact.Substring(0, 8) + "-" +
                   compact.Substring(8, 4) + "-" +
                   compact.Substring(12, 4) + "-" +
                   compact.Substring(16, 4) + "-" +
                   compact.Substring(20, 12);
        }

        static void ValidateShaderStages(JObject ir)
        {
            var expressions = (ir?["expressions"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Where(item => !string.IsNullOrEmpty(item["id"]?.Value<string>()))
                .ToDictionary(
                    item => item["id"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            bool DependsOnFragment(string expressionId, ISet<string> visiting)
            {
                if (!expressions.TryGetValue(expressionId ?? "", out var expression))
                    return false;
                if (string.Equals(
                        expression["stage"]?.Value<string>(),
                        "Fragment",
                        StringComparison.Ordinal))
                    return true;
                if (!visiting.Add(expressionId))
                    return false;
                try
                {
                    return (expression["inputs"] as JObject ?? new JObject())
                        .Properties()
                        .Select(property =>
                            property.Value?["expressionId"]?.Value<string>() ?? "")
                        .Any(child => DependsOnFragment(child, visiting));
                }
                finally
                {
                    visiting.Remove(expressionId);
                }
            }

            foreach (var channel in (ir?["channels"] as JArray ?? new JArray())
                         .OfType<JObject>())
            {
                if (!string.Equals(
                        channel["stage"]?.Value<string>(),
                        "Vertex",
                        StringComparison.Ordinal))
                    continue;
                var value = channel["value"] as JObject;
                if (!string.Equals(
                        value?["kind"]?.Value<string>(),
                        "Expression",
                        StringComparison.Ordinal))
                    continue;
                var expressionId = value["expressionId"]?.Value<string>() ?? "";
                if (DependsOnFragment(
                        expressionId,
                        new HashSet<string>(StringComparer.Ordinal)))
                    throw new InvalidOperationException(
                        "shader_stage_conflict:" +
                        (channel["semantic"]?.Value<string>() ?? "channel") +
                        ":" +
                        expressionId);
            }
        }

        static string StabilizeMultiJson(string text, string materialId)
        {
            var objects = ParseMultiJson(text);
            var byOldId = objects
                .Where(item => !string.IsNullOrEmpty(item["m_ObjectId"]?.Value<string>()))
                .ToDictionary(
                    item => item["m_ObjectId"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var parent in objects)
            {
                var parentId = parent["m_ObjectId"]?.Value<string>() ?? "";
                foreach (var slotReference in parent["m_Slots"] as JArray ?? new JArray())
                {
                    var oldSlotId = slotReference?["m_Id"]?.Value<string>() ?? "";
                    if (string.IsNullOrEmpty(oldSlotId) ||
                        !byOldId.TryGetValue(oldSlotId, out var slot))
                        continue;
                    replacements[oldSlotId] = StableId(
                        materialId,
                        "slot:" +
                        parentId +
                        ":" +
                        (slot["m_Id"]?.Value<int>() ?? 0) +
                        ":" +
                        (slot["m_Type"]?.Value<string>() ?? ""));
                }
            }
            var remaining = objects
                .Where(item =>
                {
                    var id = item["m_ObjectId"]?.Value<string>() ?? "";
                    var type = item["m_Type"]?.Value<string>() ?? "";
                    return !string.IsNullOrEmpty(id) &&
                           !replacements.ContainsKey(id) &&
                           !type.EndsWith("Node", StringComparison.Ordinal) &&
                           !type.Contains("ShaderProperty") &&
                           !type.EndsWith("GraphData", StringComparison.Ordinal);
                })
                .Select(item => new
                {
                    item,
                    id = item["m_ObjectId"].Value<string>(),
                    fingerprint = Regex.Replace(
                        item.ToString(Formatting.None),
                        "[0-9a-f]{32}",
                        "<id>",
                        RegexOptions.CultureInvariant),
                })
                .OrderBy(item => item.fingerprint, StringComparer.Ordinal)
                .ThenBy(item => item.id, StringComparer.Ordinal)
                .ToArray();
            for (var index = 0; index < remaining.Length; index++)
            {
                replacements[remaining[index].id] = StableId(
                    materialId,
                    "serialized:" +
                    index +
                    ":" +
                    remaining[index].fingerprint);
            }
            foreach (var item in objects)
                ReplaceIds(item, replacements);
            var ordered = objects
                .OrderBy(
                    item => item["m_Type"]?.Value<string>()?.EndsWith(
                        "GraphData",
                        StringComparison.Ordinal) == true
                        ? 0
                        : 1)
                .ThenBy(
                    item => item["m_ObjectId"]?.Value<string>() ?? "",
                    StringComparer.Ordinal);
            return string.Join(
                       "\n\n",
                       ordered.Select(item => item.ToString(Formatting.Indented))) +
                   "\n";
        }

        static void ReplaceIds(
            JToken token,
            IDictionary<string, string> replacements)
        {
            if (token is JValue value &&
                value.Type == JTokenType.String &&
                replacements.TryGetValue(value.Value<string>() ?? "", out var replacement))
            {
                value.Value = replacement;
                return;
            }
            foreach (var child in token.Children().ToArray())
                ReplaceIds(child, replacements);
        }

        static string ValueType(JToken value)
        {
            return value is JArray array && array.Count >= 3 ? "Float3" : "Scalar";
        }

        static int Dimensions(JToken value, string valueType)
        {
            var normalized = (valueType ?? "").ToLowerInvariant();
            if (normalized.Contains("4") || normalized == "color") return 4;
            if (normalized.Contains("3")) return 3;
            if (normalized.Contains("2")) return 2;
            if (value is JArray array) return Math.Max(1, Math.Min(4, array.Count));
            return 1;
        }

        static float[] Components(JToken value, int dimensions)
        {
            var result = new float[dimensions];
            if (value is JArray array)
            {
                for (var index = 0; index < dimensions; index++)
                    result[index] = index < array.Count ? array[index].Value<float>() : 0f;
            }
            else
            {
                result[0] = value?.Value<float>() ?? 0f;
            }
            return result;
        }

        sealed class ShaderGraph17_4Adapter
        {
            const BindingFlags AnyInstance =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            const BindingFlags AnyStatic =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            readonly Dictionary<string, Type> types =
                new Dictionary<string, Type>(StringComparer.Ordinal);

            public object CreateSubGraph(string materialId)
            {
                var result = New("UnityEditor.ShaderGraph.GraphData");
                Set(result, "isSubGraph", true);
                Set(result, "path", "Miku/Generated");
                OverrideId(result, StableId(materialId, "subgraph:root"));
                var output = New("UnityEditor.ShaderGraph.SubGraphOutputNode");
                OverrideId(output, StableId(materialId, "subgraph:output"));
                AddNode(result, output, "subgraph:output");
                Set(result, "outputNode", output);
                return result;
            }

            public object GetOutput(object graph) => Get(graph, "outputNode");

            public object CreateNode(
                object graph,
                string materialId,
                string role,
                string type,
                Vector2 position)
            {
                var node = New("UnityEditor.ShaderGraph." + type);
                OverrideId(node, StableId(materialId, "expression:" + role));
                AddNode(graph, node, role);
                SetPosition(node, position);
                return node;
            }

            public object CreateMultiLobeNode(
                object graph,
                string materialId,
                string role,
                Vector2 position)
            {
                var node = CreateNode(
                    graph,
                    materialId,
                    "custom-lobe:" + role,
                    "CustomFunctionNode",
                    position);
                SetEnum(node, "sourceType", "File");
                Set(node, "functionName", "MikuEvaluateLobe");
                Set(
                    node,
                    "functionSource",
                    "8ce39b4252824e4bbd28e2cf5dfcd3a5");
                Set(node, "functionSourceUsePragmas", true);
                AddCustomFunctionSlot(
                    node,
                    0,
                    "PositionWS",
                    "Vector3",
                    true);
                AddCustomFunctionSlot(
                    node,
                    1,
                    "NormalWS",
                    "Vector3",
                    true);
                AddCustomFunctionSlot(
                    node,
                    2,
                    "ViewDirectionWS",
                    "Vector3",
                    true);
                AddCustomFunctionSlot(
                    node,
                    3,
                    "ScreenPosition",
                    "Vector4",
                    true);
                AddCustomFunctionSlot(
                    node,
                    4,
                    "BaseColor",
                    "Vector4",
                    true);
                AddCustomFunctionSlot(
                    node,
                    5,
                    "Roughness",
                    "Vector1",
                    true);
                AddCustomFunctionSlot(
                    node,
                    6,
                    "Metallic",
                    "Vector1",
                    true);
                AddCustomFunctionSlot(
                    node,
                    7,
                    "LobeKind",
                    "Vector1",
                    true);
                AddCustomFunctionSlot(
                    node,
                    8,
                    "Weight",
                    "Vector1",
                    true);
                AddCustomFunctionSlot(
                    node,
                    9,
                    "Out",
                    "Vector3",
                    false);
                return node;
            }

            public object CreateMappingPointNode(
                object graph,
                string materialId,
                string role,
                Vector2 position)
            {
                var node = CreateFileCustomFunctionNode(
                    graph,
                    materialId,
                    "mapping-point:" + role,
                    "Miku_MappingPoint",
                    "9575c9a31f694f23952aa6e758fbb75e",
                    position);
                AddCustomFunctionSlot(node, 0, "vector", "Vector3", true);
                AddCustomFunctionSlot(node, 1, "location", "Vector3", true);
                AddCustomFunctionSlot(node, 2, "rotation", "Vector3", true);
                AddCustomFunctionSlot(node, 3, "scale", "Vector3", true);
                AddCustomFunctionSlot(node, 4, "result", "Vector3", false);
                return node;
            }

            public object CreateLightPathNode(
                object graph,
                string materialId,
                string role,
                Vector2 position)
            {
                var node = CreateFileCustomFunctionNode(
                    graph,
                    materialId,
                    "light-path:" + role,
                    "Miku_LightPath",
                    "61cbfd9c16e84b3e8b9ec745ddc6365f",
                    position);
                AddCustomFunctionSlot(
                    node,
                    0,
                    "IsCameraRay",
                    "Vector1",
                    false);
                AddCustomFunctionSlot(
                    node,
                    1,
                    "IsShadowRay",
                    "Vector1",
                    false);
                return node;
            }

            public object CreateNoiseFactor3DNode(
                object graph,
                string materialId,
                string role,
                Vector2 position)
            {
                var node = CreateFileCustomFunctionNode(
                    graph,
                    materialId,
                    "noise-factor-3d:" + role,
                    "Miku_NoiseTexture3D_Factor",
                    "9575c9a31f694f23952aa6e758fbb75e",
                    position);
                AddCustomFunctionSlot(node, 0, "position", "Vector3", true);
                AddCustomFunctionSlot(node, 1, "scale", "Vector1", true);
                AddCustomFunctionSlot(node, 2, "detail", "Vector1", true);
                AddCustomFunctionSlot(node, 3, "roughness", "Vector1", true);
                AddCustomFunctionSlot(node, 4, "lacunarity", "Vector1", true);
                AddCustomFunctionSlot(node, 5, "distortion", "Vector1", true);
                AddCustomFunctionSlot(node, 6, "factor", "Vector1", false);
                return node;
            }

            object CreateFileCustomFunctionNode(
                object graph,
                string materialId,
                string role,
                string functionName,
                string sourceGuid,
                Vector2 position)
            {
                var node = CreateNode(
                    graph,
                    materialId,
                    role,
                    "CustomFunctionNode",
                    position);
                SetEnum(node, "sourceType", "File");
                Set(node, "functionName", functionName);
                Set(
                    node,
                    "functionSource",
                    sourceGuid);
                Set(node, "functionSourceUsePragmas", true);
                return node;
            }

            public object CreateFloatProperty(
                object graph,
                string materialId,
                string role,
                string displayName,
                string referenceName,
                float defaultValue)
            {
                var property = New("UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty");
                OverrideId(property, StableId(materialId, "property:" + role));
                InvokeIfPresent(
                    property,
                    "OverrideGuid",
                    StableId(materialId, "property-guid:" + role),
                    role);
                Set(property, "displayName", displayName);
                Set(property, "overrideReferenceName", referenceName);
                Set(property, "generatePropertyBlock", true);
                var value = Property(property.GetType(), "value");
                value?.SetValue(property, defaultValue, null);
                Invoke(graph, "AddGraphInput", property, -1);
                return property;
            }

            public object CreateTextureProperty(
                object graph,
                string materialId,
                string role,
                string displayName,
                string referenceName,
                bool normalMap)
            {
                var property = New(
                    "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty");
                OverrideId(property, StableId(materialId, "property:" + role));
                InvokeIfPresent(
                    property,
                    "OverrideGuid",
                    StableId(materialId, "property-guid:" + role),
                    role);
                Set(property, "displayName", displayName);
                Set(property, "overrideReferenceName", referenceName);
                Set(property, "generatePropertyBlock", true);
                Set(property, "useTilingAndOffset", false);
                Set(property, "useTexelSize", false);
                Set(property, "isHDR", false);
                SetEnum(
                    property,
                    "defaultType",
                    normalMap ? "NormalMap" : "White");
                Invoke(graph, "AddGraphInput", property, -1);
                return property;
            }

            public object CreatePropertyNode(
                object graph,
                string materialId,
                string role,
                object property,
                Vector2 position)
            {
                var node = CreateNode(
                    graph,
                    materialId,
                    "property-node:" + role,
                    "PropertyNode",
                    position);
                Set(node, "property", property);
                return node;
            }

            public int AddOutput(object output, string name, string concreteType)
            {
                var enumType = TypeOf("UnityEditor.ShaderGraph.ConcreteSlotValueType");
                var value = Enum.Parse(enumType, concreteType, true);
                var slotId = Convert.ToInt32(Invoke(output, "AddSlot", value), CultureInfo.InvariantCulture);
                var slot = GetSlot(output, slotId);
                SetField(slot, "m_DisplayName", name);
                SetField(slot, "m_ShaderOutputName", Sanitize(name));
                return slotId;
            }

            void AddCustomFunctionSlot(
                object node,
                int slotId,
                string name,
                string concreteType,
                bool input)
            {
                var slotType = Enum.Parse(
                    TypeOf("UnityEditor.Graphing.SlotType"),
                    input ? "Input" : "Output",
                    true);
                var stage = Enum.Parse(
                    TypeOf(
                        "UnityEditor.ShaderGraph.ShaderStageCapability"),
                    "Fragment",
                    true);
                object[] arguments;
                switch (concreteType)
                {
                    case "Vector1":
                        arguments = new object[]
                        {
                            slotId,
                            name,
                            name,
                            slotType,
                            0f,
                            stage,
                            null,
                            false,
                            false,
                        };
                        break;
                    case "Vector3":
                        arguments = new object[]
                        {
                            slotId,
                            name,
                            name,
                            slotType,
                            Vector3.zero,
                            stage,
                            null,
                            null,
                            null,
                            false,
                        };
                        break;
                    case "Vector4":
                        arguments = new object[]
                        {
                            slotId,
                            name,
                            name,
                            slotType,
                            Vector4.zero,
                            stage,
                            null,
                            null,
                            null,
                            null,
                            false,
                        };
                        break;
                    default:
                        throw new InvalidOperationException(
                            "MIKU_CUSTOM_FUNCTION_SLOT_TYPE_UNSUPPORTED:" +
                            concreteType);
                }
                var slotClass =
                    "UnityEditor.ShaderGraph." +
                    concreteType +
                    "MaterialSlot";
                var slot = Activator.CreateInstance(
                    TypeOf(slotClass),
                    AnyInstance,
                    null,
                    arguments,
                    CultureInfo.InvariantCulture);
                Invoke(node, "AddSlot", slot, true);
            }

            public void Connect(object graph, object fromNode, int fromSlot, object toNode, int toSlot)
            {
                var from = Get(GetSlot(fromNode, fromSlot), "slotReference");
                var to = Get(GetSlot(toNode, toSlot), "slotReference");
                var method = graph.GetType().GetMethods(AnyInstance)
                    .First(item => item.Name == "Connect" && item.GetParameters().Length == 2);
                try
                {
                    if (method.Invoke(graph, new[] { from, to }) != null)
                        return;
                    throw new InvalidOperationException(
                        $"MIKU_SHADERGRAPH_CONNECTION_REJECTED:{fromSlot}:{toSlot}");
                }
                catch (TargetInvocationException error)
                {
                    var root = RootCause(error);
                    throw new InvalidOperationException(
                        "MIKU_SHADERGRAPH_CONNECTION_REJECTED:" +
                        fromSlot + ":" + toSlot + ":" + root.Message,
                        root);
                }
            }

            public void SetSlotValue(object node, int slotId, JToken value)
            {
                var slot = GetSlot(node, slotId);
                var property = slot == null ? null : Property(slot.GetType(), "value");
                if (property == null || !property.CanWrite)
                    return;
                object converted;
                if (property.PropertyType == typeof(float))
                    converted = value.Value<float>();
                else if (property.PropertyType == typeof(Vector2))
                {
                    var values = Components(value, 2);
                    converted = new Vector2(values[0], values[1]);
                }
                else if (property.PropertyType == typeof(Vector3))
                {
                    var values = Components(value, 3);
                    converted = new Vector3(values[0], values[1], values[2]);
                }
                else if (property.PropertyType == typeof(Vector4))
                {
                    var values = Components(value, 4);
                    converted = new Vector4(values[0], values[1], values[2], values[3]);
                }
                else
                    converted = Convert.ChangeType(
                        value.ToObject(property.PropertyType),
                        property.PropertyType,
                        CultureInfo.InvariantCulture);
                property.SetValue(slot, converted, null);
            }

            public void SetEnum(object target, string name, string value)
            {
                var property = Property(target.GetType(), name);
                if (property != null && property.GetSetMethod(true) != null)
                {
                    property.SetValue(target, Enum.Parse(property.PropertyType, value, true), null);
                    return;
                }
                var serialized = "m_" + char.ToUpperInvariant(name[0]) + name.Substring(1);
                var field = FieldOrNull(target.GetType(), name) ??
                            FieldOrNull(target.GetType(), serialized) ??
                            throw new MissingFieldException(target.GetType().FullName, name);
                field.SetValue(target, Enum.Parse(field.FieldType, value, true));
            }

            public void SetColorspaceConversion(
                object node,
                string from,
                string to)
            {
                var conversionType = TypeOf(
                    "UnityEditor.ShaderGraph.ColorspaceConversion");
                var colorspaceType = TypeOf(
                    "UnityEditor.ShaderGraph.Colorspace");
                var conversion = Activator.CreateInstance(
                    conversionType,
                    true);
                var fromValue = Enum.Parse(colorspaceType, from, true);
                var toValue = Enum.Parse(colorspaceType, to, true);
                Set(conversion, "from", fromValue);
                Set(conversion, "to", toValue);
                Set(node, "conversion", conversion);
            }

            public void SetCoordinateSpaceConversion(
                object node,
                string from,
                string to)
            {
                var conversionType = TypeOf(
                    "UnityEditor.ShaderGraph.CoordinateSpaceConversion");
                var coordinateSpaceType = TypeOf(
                    "UnityEditor.ShaderGraph.Internal.CoordinateSpace");
                var conversion = Activator.CreateInstance(
                    conversionType,
                    true);
                Set(
                    conversion,
                    "from",
                    Enum.Parse(coordinateSpaceType, from, true));
                Set(
                    conversion,
                    "to",
                    Enum.Parse(coordinateSpaceType, to, true));
                Set(node, "conversion", conversion);
                SetEnum(node, "conversionType", "Normal");
                Set(node, "normalize", true);
            }

            public string Serialize(object graph)
            {
                Invoke(graph, "ValidateGraph");
                var type = TypeOf("UnityEditor.ShaderGraph.Serialization.MultiJson");
                var method = type.GetMethod("Serialize", AnyStatic) ??
                             throw new MissingMethodException(type.FullName, "Serialize");
                return (string)method.Invoke(null, new[] { graph });
            }

            object GetSlot(object node, int slotId)
            {
                return Slots(node).FirstOrDefault(
                    slot => Convert.ToInt32(Get(slot, "id"), CultureInfo.InvariantCulture) == slotId);
            }

            IEnumerable<object> Slots(object node)
            {
                var type = node.GetType();
                while (type != null && type.Name != "AbstractMaterialNode")
                    type = type.BaseType;
                if (type == null)
                    throw new MissingMemberException(node.GetType().FullName, "m_Slots");
                var values = (IEnumerable)type
                    .GetField("m_Slots", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(node);
                foreach (var item in values)
                {
                    var value = Get(item, "value");
                    if (value != null)
                        yield return value;
                }
            }

            void AddNode(object graph, object node, string role)
            {
                var method = graph.GetType().GetMethods(AnyInstance)
                    .Where(item => item.Name == "AddNode")
                    .OrderBy(item => item.GetParameters().Length)
                    .First();
                try
                {
                    method.Invoke(
                        graph,
                        method.GetParameters().Length == 1
                            ? new[] { node }
                            : new[] { node, false });
                }
                catch (TargetInvocationException error)
                {
                    var root = RootCause(error);
                    var code = root is ArgumentException
                        ? "MIKU_SHADERGRAPH_DUPLICATE_NODE_ID:"
                        : "MIKU_SHADERGRAPH_ADD_NODE_FAILED:";
                    throw new InvalidOperationException(
                        code + role + ":" + root.Message,
                        root);
                }
            }

            static Exception RootCause(Exception error)
            {
                var current = error;
                while (current is TargetInvocationException invocation &&
                       invocation.InnerException != null)
                    current = invocation.InnerException;
                return current;
            }

            void SetPosition(object node, Vector2 position)
            {
                var state = Get(node, "drawState");
                var property = Property(state.GetType(), "position");
                property.SetValue(
                    state,
                    new Rect(position.x, position.y, 220f, 120f),
                    null);
                Set(node, "drawState", state);
            }

            object New(string fullName)
            {
                return Activator.CreateInstance(TypeOf(fullName), true);
            }

            Type TypeOf(string fullName)
            {
                if (types.TryGetValue(fullName, out var cached))
                    return cached;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(fullName, false);
                    if (type == null)
                        continue;
                    types[fullName] = type;
                    return type;
                }
                throw new TypeLoadException("MIKU_SHADERGRAPH17_TYPE_MISSING:" + fullName);
            }

            static object Invoke(object target, string name, params object[] arguments)
            {
                var method = target.GetType().GetMethods(AnyInstance)
                    .Where(item => item.Name == name)
                    .FirstOrDefault(item =>
                    {
                        var parameters = item.GetParameters();
                        if (parameters.Length != arguments.Length)
                            return false;
                        return parameters.Select((parameter, index) =>
                                arguments[index] == null ||
                                parameter.ParameterType.IsInstanceOfType(arguments[index]))
                            .All(value => value);
                    });
                if (method == null)
                    throw new MissingMethodException(target.GetType().FullName, name);
                return method.Invoke(target, arguments);
            }

            static void InvokeIfPresent(object target, string name, params object[] arguments)
            {
                try
                {
                    Invoke(target, name, arguments);
                }
                catch (MissingMethodException)
                {
                    // A GUID override is a deterministic enhancement. Object IDs
                    // remain stable on adapters where this optional API moved.
                }
            }

            static object Get(object target, string name)
            {
                if (target == null)
                    return null;
                var property = Property(target.GetType(), name);
                if (property != null)
                    return property.GetValue(target, null);
                var field = FieldOrNull(target.GetType(), name);
                if (field != null)
                    return field.GetValue(target);
                throw new MissingMemberException(target.GetType().FullName, name);
            }

            static void Set(object target, string name, object value)
            {
                var property = Property(target.GetType(), name);
                if (property != null && property.GetSetMethod(true) != null)
                {
                    property.SetValue(target, value, null);
                    return;
                }
                var field = FieldOrNull(target.GetType(), name);
                if (field == null)
                    throw new MissingMemberException(target.GetType().FullName, name);
                field.SetValue(target, value);
            }

            static PropertyInfo Property(Type type, string name)
            {
                while (type != null)
                {
                    var property = type.GetProperty(name, AnyInstance);
                    if (property != null)
                        return property;
                    type = type.BaseType;
                }
                return null;
            }

            static FieldInfo FieldOrNull(Type type, string name)
            {
                while (type != null)
                {
                    var field = type.GetField(name, AnyInstance);
                    if (field != null)
                        return field;
                    type = type.BaseType;
                }
                return null;
            }

            static void SetField(object target, string name, object value)
            {
                var field = FieldOrNull(target.GetType(), name) ??
                            throw new MissingFieldException(target.GetType().FullName, name);
                field.SetValue(target, value);
            }

            static void OverrideId(object target, string id)
            {
                var method = target.GetType().GetMethods(AnyInstance)
                    .FirstOrDefault(item =>
                        item.Name == "OverrideObjectId" &&
                        item.GetParameters().Length == 1);
                if (method == null)
                    throw new MissingMethodException(target.GetType().FullName, "OverrideObjectId");
                method.Invoke(target, new object[] { id });
            }

            static string Sanitize(string value)
            {
                var characters = (value ?? "Value")
                    .Select(character => char.IsLetterOrDigit(character) ? character : '_')
                    .ToArray();
                var result = new string(characters).Trim('_');
                return string.IsNullOrEmpty(result) ? "Value" : result;
            }
        }

        static string StableId(string materialId, string role)
        {
            var bytes = Encoding.UTF8.GetBytes(
                "miku-shadergraph-17.4:" + (materialId ?? "material") + ":" + (role ?? "object"));
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Take(16).Select(value => value.ToString("x2")));
        }
    }
}
