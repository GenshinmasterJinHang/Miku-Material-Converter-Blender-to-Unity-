"""Target-neutral closure graph and Blender 5.2 closure-weight solver."""

from __future__ import annotations

import math
from dataclasses import dataclass
from enum import Enum
from typing import Any, Iterable, Mapping, Sequence

from .contracts import canonical_hash, stable_uuid
from .socket_conversion import (
    ColorManagementContext,
    ImplicitSocketConversionRegistry,
    SocketConversionError,
    canonical_socket_type,
)


class ClosureKind(str, Enum):
    NULL = "Null"
    PRINCIPLED = "Principled"
    DIFFUSE = "Diffuse"
    GLOSSY = "Glossy"
    METALLIC = "Metallic"
    EMISSION = "Emission"
    TRANSPARENT = "Transparent"
    GLASS = "Glass"
    REFRACTION = "Refraction"
    TRANSLUCENT = "Translucent"
    SUBSURFACE = "SubsurfaceScattering"
    SHEEN = "Sheen"
    VOLUME = "Volume"
    HOLDOUT = "Holdout"
    MIX = "Mix"
    ADD = "Add"
    SHADER_TO_RGB_BARRIER = "ShaderToRgbBarrier"
    UNSUPPORTED = "Unsupported"


class ClosureDomain(str, Enum):
    SURFACE_SCATTERING = "SurfaceScattering"
    SURFACE_TRANSMISSION = "SurfaceTransmission"
    TRANSPARENT_PASS_THROUGH = "TransparentPassThrough"
    EMISSION = "Emission"
    REFRACTION = "Refraction"
    VOLUME = "Volume"
    HOLDOUT = "Holdout"
    UNSUPPORTED = "Unsupported"


class AddShaderEnergyPolicy(str, Enum):
    PRESERVE_BLENDER = "PreserveBlender"
    ENERGY_CONSERVING_APPROXIMATION = "EnergyConservingApproximation"
    CLAMP_FOR_REALTIME_SAFETY = "ClampForRealtimeSafety"


class FidelityPolicy(str, Enum):
    ALLOW_DECLARED_APPROXIMATION = "AllowDeclaredApproximation"
    STRICT = "Strict"


_CLOSURE_OPS: dict[str, tuple[ClosureKind, ClosureDomain]] = {
    "Shader.PrincipledBSDF": (
        ClosureKind.PRINCIPLED,
        ClosureDomain.SURFACE_SCATTERING,
    ),
    "Shader.DiffuseBSDF": (
        ClosureKind.DIFFUSE,
        ClosureDomain.SURFACE_SCATTERING,
    ),
    "Shader.AnisotropicBSDF": (
        ClosureKind.GLOSSY,
        ClosureDomain.SURFACE_SCATTERING,
    ),
    "Shader.GlossyBSDF": (
        ClosureKind.GLOSSY,
        ClosureDomain.SURFACE_SCATTERING,
    ),
    "Shader.MetallicBSDF": (
        ClosureKind.METALLIC,
        ClosureDomain.SURFACE_SCATTERING,
    ),
    "Shader.Emission": (ClosureKind.EMISSION, ClosureDomain.EMISSION),
    "Shader.TransparentBSDF": (
        ClosureKind.TRANSPARENT,
        ClosureDomain.TRANSPARENT_PASS_THROUGH,
    ),
    "Shader.GlassBSDF": (ClosureKind.GLASS, ClosureDomain.REFRACTION),
    "Shader.RefractionBSDF": (
        ClosureKind.REFRACTION,
        ClosureDomain.REFRACTION,
    ),
    "Shader.TranslucentBSDF": (
        ClosureKind.TRANSLUCENT,
        ClosureDomain.SURFACE_TRANSMISSION,
    ),
    "Shader.SubsurfaceScattering": (
        ClosureKind.SUBSURFACE,
        ClosureDomain.SURFACE_SCATTERING,
    ),
    "Shader.SheenBSDF": (
        ClosureKind.SHEEN,
        ClosureDomain.SURFACE_SCATTERING,
    ),
    "Shader.Volume": (ClosureKind.VOLUME, ClosureDomain.VOLUME),
    "Shader.Holdout": (ClosureKind.HOLDOUT, ClosureDomain.HOLDOUT),
    "Shader.ToRGB": (
        ClosureKind.SHADER_TO_RGB_BARRIER,
        ClosureDomain.UNSUPPORTED,
    ),
}

_RUNTIME_WEIGHT_OPS = {
    "Input.LayerWeight",
    "Input.Fresnel",
    "Input.CameraData",
    "Input.Geometry",
    "Input.ViewDirection",
    "Input.Time",
    "Input.LightPath",
}


def _normalize_socket(value: Any) -> str:
    return "".join(
        character
        for character in str(value or "").lower()
        if character.isalnum()
    )


def _canonical_unconnected_closure_parameter(
    name: str,
    value: Any,
) -> Any:
    """Map Blender's unconnected closure-normal sentinel to neutral tangent space."""

    if _normalize_socket(name) not in {"normal", "coatnormal"}:
        return value
    if not isinstance(value, (list, tuple)) or len(value) != 3:
        return value
    try:
        components = [float(component) for component in value]
    except (TypeError, ValueError):
        return value
    if not all(math.isfinite(component) for component in components):
        return value
    if all(abs(component) <= 1.0e-12 for component in components):
        return [0.0, 0.0, 1.0]
    return value


def _node_op(node: Mapping[str, Any]) -> str:
    return str(node.get("op") or node.get("type") or "")


def _stable_expression(kind: str, payload: Mapping[str, Any]) -> dict[str, Any]:
    body = {"kind": kind, **dict(payload)}
    body["id"] = stable_uuid(
        "miku-weight-expression",
        f"{kind}:{canonical_hash(body)}",
    )
    return body


def constant_weight(value: float, *, source: Mapping[str, Any] | None = None) -> dict[str, Any]:
    if not math.isfinite(float(value)):
        raise ValueError("MIKU_INVALID_WEIGHT_NUMBER")
    payload: dict[str, Any] = {
        "valueType": "Float",
        "value": float(value),
    }
    if source:
        payload["source"] = dict(source)
    return _stable_expression("Constant", payload)


def _constant_value(expression: Mapping[str, Any]) -> float | None:
    if expression.get("kind") != "Constant":
        return None
    value = expression.get("value")
    if not isinstance(value, (int, float)) or not math.isfinite(float(value)):
        return None
    return float(value)


def multiply_weight(
    left: Mapping[str, Any],
    right: Mapping[str, Any],
) -> dict[str, Any]:
    left_constant = _constant_value(left)
    right_constant = _constant_value(right)
    if left_constant == 0.0 or right_constant == 0.0:
        return constant_weight(0.0)
    if left_constant == 1.0:
        return dict(right)
    if right_constant == 1.0:
        return dict(left)
    if left_constant is not None and right_constant is not None:
        return constant_weight(left_constant * right_constant)
    return _stable_expression(
        "Multiply",
        {"valueType": "Float", "inputs": [dict(left), dict(right)]},
    )


def add_weight(expressions: Iterable[Mapping[str, Any]]) -> dict[str, Any]:
    flattened: list[dict[str, Any]] = []
    constant_total = 0.0
    for expression in expressions:
        if expression.get("kind") == "Add":
            nested = expression.get("inputs") or []
            flattened.extend(dict(item) for item in nested if isinstance(item, Mapping))
            continue
        value = _constant_value(expression)
        if value is None:
            flattened.append(dict(expression))
        else:
            constant_total += value
    if constant_total != 0.0 or not flattened:
        flattened.append(constant_weight(constant_total))
    flattened = [
        item for item in flattened if _constant_value(item) != 0.0
    ] or [constant_weight(0.0)]
    if len(flattened) == 1:
        return flattened[0]
    flattened.sort(key=lambda item: str(item.get("id") or ""))
    return _stable_expression(
        "Add",
        {"valueType": "Float", "inputs": flattened},
    )


def one_minus_weight(expression: Mapping[str, Any]) -> dict[str, Any]:
    value = _constant_value(expression)
    if value is not None:
        return constant_weight(1.0 - value)
    return _stable_expression(
        "OneMinus",
        {"valueType": "Float", "input": dict(expression)},
    )


def clamp_weight(expression: Mapping[str, Any]) -> dict[str, Any]:
    value = _constant_value(expression)
    if value is not None:
        return constant_weight(min(1.0, max(0.0, value)))
    return _stable_expression(
        "Clamp",
        {
            "valueType": "Float",
            "minimum": 0.0,
            "maximum": 1.0,
            "input": dict(expression),
        },
    )


@dataclass(frozen=True)
class ClosureBudget:
    max_lobes: int = 8
    max_specular_lobes: int = 4
    max_transmission_lobes: int = 2
    max_refraction_samples: int = 2
    max_distinct_normals: int = 4
    max_dynamic_weights: int = 16
    max_estimated_alu: int = 512
    max_texture_samples: int = 32

    def to_document(self) -> dict[str, int]:
        return {
            "maxLobes": self.max_lobes,
            "maxSpecularLobes": self.max_specular_lobes,
            "maxTransmissionLobes": self.max_transmission_lobes,
            "maxRefractionSamples": self.max_refraction_samples,
            "maxDistinctNormals": self.max_distinct_normals,
            "maxDynamicWeights": self.max_dynamic_weights,
            "maxEstimatedAlu": self.max_estimated_alu,
            "maxTextureSamples": self.max_texture_samples,
        }


class _GraphIndex:
    def __init__(self, graph: Mapping[str, Any]) -> None:
        self.nodes = {
            str(node.get("id") or ""): node
            for node in graph.get("nodes", []) or []
            if isinstance(node, Mapping) and str(node.get("id") or "")
        }
        self.incoming: dict[tuple[str, str], dict[str, str]] = {}
        for edge in graph.get("edges", []) or []:
            if not isinstance(edge, Mapping):
                continue
            source = edge.get("from") or {}
            target = edge.get("to") or {}
            if not isinstance(source, Mapping) or not isinstance(target, Mapping):
                continue
            self.incoming[
                (
                    str(target.get("node") or ""),
                    _normalize_socket(target.get("socket")),
                )
            ] = {
                "node": str(source.get("node") or ""),
                "socket": str(source.get("socket") or ""),
            }

    def input_record(
        self,
        node: Mapping[str, Any],
        names: Sequence[str],
    ) -> Mapping[str, Any]:
        normalized = {_normalize_socket(name) for name in names}
        for record in node.get("inputs", []) or []:
            if not isinstance(record, Mapping):
                continue
            if {
                _normalize_socket(record.get("id")),
                _normalize_socket(record.get("name")),
            } & normalized:
                return record
        return {}

    def input_source(
        self,
        node: Mapping[str, Any],
        names: Sequence[str],
    ) -> Mapping[str, str] | None:
        record = self.input_record(node, names)
        node_id = str(node.get("id") or "")
        if record:
            socket = _normalize_socket(record.get("id") or record.get("name"))
            source = self.incoming.get((node_id, socket))
            if source is not None:
                return source
        for name in names:
            source = self.incoming.get((node_id, _normalize_socket(name)))
            if source is not None:
                return source
        return None

    def output_record(
        self,
        endpoint: Mapping[str, Any],
    ) -> Mapping[str, Any]:
        node = self.nodes.get(str(endpoint.get("node") or ""), {})
        socket = _normalize_socket(endpoint.get("socket"))
        for record in node.get("outputs", []) or []:
            if not isinstance(record, Mapping):
                continue
            if socket in {
                _normalize_socket(record.get("id")),
                _normalize_socket(record.get("name")),
            }:
                return record
        return {}


class WeightExpressionCompiler:
    """Compile a source value endpoint into a symbolic scalar weight DAG."""

    def __init__(
        self,
        index: _GraphIndex,
        *,
        color_management: ColorManagementContext | None = None,
    ) -> None:
        self.index = index
        self.registry = ImplicitSocketConversionRegistry(color_management)
        self._cache: dict[tuple[str, str, str], dict[str, Any]] = {}

    def compile_input(
        self,
        node: Mapping[str, Any],
        names: Sequence[str],
        fallback: float,
    ) -> dict[str, Any]:
        record = self.index.input_record(node, names)
        source = self.index.input_source(node, names)
        if source is None:
            value = (
                fallback
                if not record or record.get("default") is None
                else record.get("default")
            )
            source_type = canonical_socket_type(
                record.get("valueType") if record else "Float"
            )
            try:
                converted = self.registry.convert(value, source_type, "Float")
            except (SocketConversionError, TypeError, ValueError):
                converted = fallback
            return constant_weight(
                float(converted),
                source={
                    "nodeId": str(node.get("id") or ""),
                    "socketId": str(
                        record.get("id") or record.get("name") or names[0]
                    ),
                },
            )
        return self.compile_endpoint(source, target_type="Float")

    def compile_endpoint(
        self,
        endpoint: Mapping[str, Any],
        *,
        target_type: str = "Float",
        visiting: frozenset[tuple[str, str]] = frozenset(),
    ) -> dict[str, Any]:
        node_id = str(endpoint.get("node") or "")
        socket_id = str(endpoint.get("socket") or "")
        key = (node_id, socket_id, canonical_socket_type(target_type))
        if key in self._cache:
            return dict(self._cache[key])
        cycle_key = (node_id, _normalize_socket(socket_id))
        if cycle_key in visiting:
            raise ValueError(f"MIKU_WEIGHT_EXPRESSION_CYCLE:{node_id}:{socket_id}")
        node = self.index.nodes.get(node_id)
        if node is None:
            raise ValueError(f"MIKU_WEIGHT_SOURCE_MISSING:{node_id}:{socket_id}")
        output = self.index.output_record(endpoint)
        source_type = canonical_socket_type(output.get("valueType") or "Float")
        op = _node_op(node)
        inputs: dict[str, Any] = {}
        next_visiting = visiting | {cycle_key}
        for record in node.get("inputs", []) or []:
            if not isinstance(record, Mapping):
                continue
            input_id = str(record.get("id") or record.get("name") or "")
            source = self.index.incoming.get(
                (node_id, _normalize_socket(input_id))
            )
            if source is not None:
                inputs[input_id] = self.compile_endpoint(
                    source,
                    target_type=canonical_socket_type(
                        record.get("valueType") or "Float"
                    ),
                    visiting=next_visiting,
                )
            elif (
                bool(record.get("enabled", True))
                and not bool(record.get("isUnavailable", False))
            ):
                inputs[input_id] = {
                    "kind": "ConstantValue",
                    "valueType": canonical_socket_type(
                        record.get("valueType") or "Float"
                    ),
                    "value": record.get("default"),
                }
        if op == "Input.LayerWeight":
            kind = "LayerWeight"
        elif op == "Input.Fresnel":
            kind = "Fresnel"
        elif op.startswith("Texture."):
            kind = "Texture"
        elif op.startswith(("Math", "Color", "Vector", "Converter", "Utility")):
            kind = "Math"
        elif op in _RUNTIME_WEIGHT_OPS:
            kind = "ViewDependent"
        else:
            kind = "Parameter"
        expression = _stable_expression(
            kind,
            {
                "valueType": source_type,
                "operation": op,
                "outputSocket": socket_id,
                "inputs": inputs,
                "source": {
                    "nodeId": node_id,
                    "socketId": socket_id,
                    "groupPath": list(
                        ((node.get("source") or {}).get("groupPath") or [])
                    ),
                },
            },
        )
        target = canonical_socket_type(target_type)
        if source_type != target:
            conversion = self.registry.resolve(source_type, target)
            expression = _stable_expression(
                "ImplicitConversion",
                {
                    "valueType": target,
                    "input": expression,
                    "conversion": conversion.to_document(),
                },
            )
        self._cache[key] = dict(expression)
        return expression


class ClosureGraphBuilder:
    """Build a semantic closure tree without flattening closure parameters."""

    def __init__(
        self,
        graph: Mapping[str, Any],
        *,
        color_management: ColorManagementContext | None = None,
    ) -> None:
        self.graph = graph
        self.index = _GraphIndex(graph)
        self.weight_compiler = WeightExpressionCompiler(
            self.index,
            color_management=color_management,
        )
        self.diagnostics: list[dict[str, Any]] = []

    def build(self) -> dict[str, Any]:
        output = self._active_material_output()
        if output is None:
            raise ValueError("MIKU_ACTIVE_MATERIAL_OUTPUT_MISSING")
        surface = self.index.input_source(output, ("Surface",))
        if surface is None:
            raise ValueError("MIKU_ACTIVE_SURFACE_MISSING")
        root = self._build_endpoint(surface)
        return {
            "schema": "miku-closure-1.0",
            "root": root,
            "rootClosureId": root["id"],
            "diagnostics": list(self.diagnostics),
        }

    def _active_material_output(self) -> Mapping[str, Any] | None:
        outputs = [
            node
            for node in self.index.nodes.values()
            if _node_op(node) == "Output.Material"
        ]
        return min(
            outputs,
            key=lambda node: (
                not bool((node.get("params") or {}).get("isActiveOutput")),
                str((node.get("params") or {}).get("target") or "ALL")
                not in {"EEVEE", "ALL"},
                str((node.get("params") or {}).get("target") or "ALL")
                != "EEVEE",
                str(node.get("id") or ""),
            ),
            default=None,
        )

    def _build_endpoint(
        self,
        endpoint: Mapping[str, Any],
        visiting: frozenset[str] = frozenset(),
    ) -> dict[str, Any]:
        node_id = str(endpoint.get("node") or "")
        if node_id in visiting:
            raise ValueError(f"MIKU_CLOSURE_GRAPH_CYCLE:{node_id}")
        node = self.index.nodes.get(node_id)
        if node is None:
            raise ValueError(f"MIKU_CLOSURE_NODE_MISSING:{node_id}")
        op = _node_op(node)
        common = {
            "id": stable_uuid("miku-closure", node_id),
            "sourceNodeId": node_id,
            "sourceSocketId": str(endpoint.get("socket") or ""),
            "groupPath": list(
                ((node.get("source") or {}).get("groupPath") or [])
            ),
            "sourceSocketOrder": [
                str(record.get("id") or record.get("name") or "")
                for record in node.get("inputs", []) or []
                if isinstance(record, Mapping)
            ],
        }
        next_visiting = visiting | {node_id}
        if op in {"Shader.Mix", "Shader.Add"}:
            first = self.index.input_source(node, ("Shader",))
            second = self.index.input_source(
                node,
                ("Shader_001", "Shader 2"),
            )
            kind = ClosureKind.MIX if op == "Shader.Mix" else ClosureKind.ADD
            if first is None or second is None:
                self.diagnostics.append(
                    {
                        "severity": "info",
                        "code": "MIKU_NULL_CLOSURE_IMPLICIT",
                        "translationQuality": "Exact",
                        "nodeId": node_id,
                        "missingInputs": [
                            name
                            for name, endpoint_value in (
                                ("Shader", first),
                                ("Shader_001", second),
                            )
                            if endpoint_value is None
                        ],
                        "message": (
                            "An unconnected closure input was preserved as the "
                            "zero closure used by Blender's closure algebra."
                        ),
                    }
                )

            def branch(
                endpoint_value: Mapping[str, Any] | None,
                socket_id: str,
            ) -> dict[str, Any]:
                if endpoint_value is not None:
                    return self._build_endpoint(
                        endpoint_value,
                        next_visiting,
                    )
                return {
                    "id": stable_uuid(
                        "miku-null-closure",
                        f"{node_id}:{socket_id}",
                    ),
                    "sourceNodeId": node_id,
                    "sourceSocketId": socket_id,
                    "groupPath": list(common["groupPath"]),
                    "sourceSocketOrder": [],
                    "kind": ClosureKind.NULL.value,
                    "domain": ClosureDomain.UNSUPPORTED.value,
                }

            result = {
                **common,
                "kind": kind.value,
                "domain": ClosureDomain.UNSUPPORTED.value,
                "first": branch(first, "Shader"),
                "second": branch(second, "Shader_001"),
            }
            if kind == ClosureKind.MIX:
                raw_factor = self.weight_compiler.compile_input(
                    node,
                    ("Factor", "Fac"),
                    0.5,
                )
                result["factor"] = clamp_weight(raw_factor)
                result["factorConversion"] = self._factor_conversion(
                    node,
                    ("Factor", "Fac"),
                )
            return result
        definition = _CLOSURE_OPS.get(op)
        if definition is None:
            self.diagnostics.append(
                {
                    "severity": "error",
                    "code": "WEIGHT0009",
                    "translationQuality": "Unsupported",
                    "nodeId": node_id,
                    "message": f"Unsupported required closure operation: {op}",
                }
            )
            kind, domain = ClosureKind.UNSUPPORTED, ClosureDomain.UNSUPPORTED
        else:
            kind, domain = definition
        return {
            **common,
            "kind": kind.value,
            "domain": domain.value,
            "operation": op,
            "localWeight": self._local_weight(node),
            "parameters": self._parameter_inputs(node),
            "distribution": str(
                (node.get("params") or {}).get("distribution") or ""
            ),
        }

    def _factor_conversion(
        self,
        node: Mapping[str, Any],
        names: Sequence[str],
    ) -> dict[str, Any]:
        source = self.index.input_source(node, names)
        if source is None:
            record = self.index.input_record(node, names)
            source_type = record.get("valueType") or "Float"
        else:
            source_type = self.index.output_record(source).get("valueType") or "Float"
        return self.weight_compiler.registry.resolve(
            source_type,
            "Float",
        ).to_document()

    def _local_weight(self, node: Mapping[str, Any]) -> dict[str, Any]:
        record = self.index.input_record(node, ("Weight",))
        if (
            not record
            or bool(record.get("isUnavailable", False))
            or not bool(record.get("enabled", True))
        ):
            return constant_weight(
                1.0,
                source={
                    "nodeId": str(node.get("id") or ""),
                    "socketId": str(
                        record.get("id") or record.get("name") or "Weight"
                    ),
                    "socketAvailable": False,
                },
            )
        return self.weight_compiler.compile_input(node, ("Weight",), 1.0)

    def _parameter_inputs(
        self,
        node: Mapping[str, Any],
    ) -> dict[str, dict[str, Any]]:
        parameters: dict[str, dict[str, Any]] = {}
        node_id = str(node.get("id") or "")
        for record in node.get("inputs", []) or []:
            if not isinstance(record, Mapping):
                continue
            name = str(record.get("id") or record.get("name") or "")
            normalized = _normalize_socket(name)
            if normalized in {
                "weight",
                "shader",
                "shader001",
                "fac",
                "factor",
            }:
                continue
            source = self.index.incoming.get((node_id, normalized))
            if source is None:
                parameters[name] = {
                    "kind": "Constant",
                    "valueType": canonical_socket_type(
                        record.get("valueType") or "Float"
                    ),
                    "value": _canonical_unconnected_closure_parameter(
                        name,
                        record.get("default"),
                    ),
                    "source": {"nodeId": node_id, "socketId": name},
                }
            else:
                output = self.index.output_record(source)
                parameters[name] = {
                    "kind": "ValueExpression",
                    "valueType": canonical_socket_type(
                        output.get("valueType") or "Float"
                    ),
                    "source": {
                        "nodeId": str(source.get("node") or ""),
                        "socketId": str(source.get("socket") or ""),
                    },
                }
        return parameters


class ClosureWeightFlattener:
    """Propagate weights through arbitrary Mix/Add nesting."""

    def flatten(
        self,
        root: Mapping[str, Any],
        inherited_weight: Mapping[str, Any] | None = None,
    ) -> dict[str, Any]:
        terms: list[dict[str, Any]] = []
        self._visit(
            root,
            inherited_weight or constant_weight(1.0),
            [],
            terms,
        )
        terms.sort(key=lambda item: str(item.get("id") or ""))
        return {
            "schema": "miku-weighted-closures-1.0",
            "terms": terms,
        }

    def _visit(
        self,
        closure: Mapping[str, Any],
        inherited: Mapping[str, Any],
        trace: list[dict[str, Any]],
        terms: list[dict[str, Any]],
    ) -> None:
        kind = str(closure.get("kind") or ClosureKind.UNSUPPORTED.value)
        node_id = str(closure.get("sourceNodeId") or "")
        if kind == ClosureKind.NULL.value:
            return
        if kind == ClosureKind.MIX.value:
            factor = closure.get("factor")
            if not isinstance(factor, Mapping):
                raise ValueError(f"MIKU_MIX_FACTOR_MISSING:{node_id}")
            first_weight = multiply_weight(
                inherited,
                one_minus_weight(factor),
            )
            second_weight = multiply_weight(inherited, factor)
            self._visit(
                _required_mapping(closure, "first"),
                first_weight,
                [
                    *trace,
                    {
                        "nodeId": node_id,
                        "operation": "Mix",
                        "branch": "First",
                        "parentWeightId": inherited.get("id"),
                        "factorWeightId": factor.get("id"),
                        "resultWeightId": first_weight.get("id"),
                    },
                ],
                terms,
            )
            self._visit(
                _required_mapping(closure, "second"),
                second_weight,
                [
                    *trace,
                    {
                        "nodeId": node_id,
                        "operation": "Mix",
                        "branch": "Second",
                        "parentWeightId": inherited.get("id"),
                        "factorWeightId": factor.get("id"),
                        "resultWeightId": second_weight.get("id"),
                    },
                ],
                terms,
            )
            return
        if kind == ClosureKind.ADD.value:
            for branch, key in (("First", "first"), ("Second", "second")):
                self._visit(
                    _required_mapping(closure, key),
                    inherited,
                    [
                        *trace,
                        {
                            "nodeId": node_id,
                            "operation": "Add",
                            "branch": branch,
                            "parentWeightId": inherited.get("id"),
                            "resultWeightId": inherited.get("id"),
                        },
                    ],
                    terms,
                )
            return
        local = closure.get("localWeight")
        if not isinstance(local, Mapping):
            local = constant_weight(1.0)
        final_weight = multiply_weight(inherited, local)
        path_key = "/".join(
            f"{item['nodeId']}:{item['operation']}:{item['branch']}"
            for item in trace
        )
        term_id = stable_uuid(
            "miku-weighted-closure",
            f"{closure.get('id')}:{path_key}",
        )
        parameters = dict(closure.get("parameters") or {})
        dependencies = _weight_dependencies(final_weight)
        normal_expression = _named_parameter(parameters, "Normal")
        tangent_expression = _named_parameter(parameters, "Tangent")
        roughness_expression = _named_parameter(parameters, "Roughness")
        ior_expression = _named_parameter(parameters, "IOR")
        terms.append(
            {
                "id": term_id,
                "stableTermId": term_id,
                "closureId": str(closure.get("id") or ""),
                "sourceClosureId": str(closure.get("id") or ""),
                "closureKind": kind,
                "domain": str(
                    closure.get("domain") or ClosureDomain.UNSUPPORTED.value
                ),
                "closureDomain": str(
                    closure.get("domain") or ClosureDomain.UNSUPPORTED.value
                ),
                "parameters": parameters,
                "distribution": str(closure.get("distribution") or ""),
                "weightExpression": final_weight,
                "localWeightExpression": dict(local),
                "finalWeight": final_weight,
                "finalWeightExpression": final_weight,
                "normalExpression": normal_expression,
                "tangentExpression": tangent_expression,
                "roughnessExpression": roughness_expression,
                "iorExpression": ior_expression,
                "dynamicDependencies": dependencies,
                "viewDependent": _weight_has_kind(
                    final_weight,
                    {
                        "ViewDependent",
                        "LayerWeight",
                        "Fresnel",
                    },
                ),
                "sceneDependent": _weight_has_kind(
                    final_weight,
                    {"Texture"},
                ),
                "meshDependent": _weight_has_source_operation(
                    final_weight,
                    {
                        "Input.Geometry",
                        "Input.Normal",
                        "Input.Position",
                        "Input.VertexColor",
                    },
                ),
                "weightTrace": [
                    *trace,
                    {
                        "nodeId": node_id,
                        "operation": "LocalWeight",
                        "parentWeightId": inherited.get("id"),
                        "localWeightId": local.get("id"),
                        "resultWeightId": final_weight.get("id"),
                    },
                ],
                "sourcePaths": [list(trace)],
                "fidelity": "Exact",
                "diagnostics": [],
                "source": {
                    "nodeId": node_id,
                    "socketId": str(closure.get("sourceSocketId") or ""),
                    "groupPath": list(closure.get("groupPath") or []),
                },
            }
        )


def _normalized_parameter_name(value: Any) -> str:
    return "".join(
        character
        for character in str(value or "").lower()
        if character.isalnum()
    )


def _named_parameter(
    parameters: Mapping[str, Any],
    name: str,
) -> Any:
    expected = _normalized_parameter_name(name)
    for key, value in parameters.items():
        if _normalized_parameter_name(key) == expected:
            return dict(value) if isinstance(value, Mapping) else value
    return None


def _weight_dependencies(expression: Any) -> list[dict[str, Any]]:
    records: dict[tuple[str, str], dict[str, Any]] = {}

    def visit(value: Any) -> None:
        if isinstance(value, Mapping):
            source = value.get("source")
            if isinstance(source, Mapping):
                node_id = str(source.get("nodeId") or "")
                socket_id = str(source.get("socketId") or "")
                if node_id or socket_id:
                    records[(node_id, socket_id)] = {
                        "nodeId": node_id,
                        "socketId": socket_id,
                        "kind": str(value.get("kind") or ""),
                        "operation": str(value.get("operation") or ""),
                    }
            for nested in value.values():
                visit(nested)
        elif isinstance(value, Sequence) and not isinstance(
            value,
            (str, bytes),
        ):
            for nested in value:
                visit(nested)

    visit(expression)
    return [
        records[key]
        for key in sorted(records)
    ]


def _weight_has_kind(
    expression: Any,
    kinds: set[str],
) -> bool:
    if isinstance(expression, Mapping):
        if str(expression.get("kind") or "") in kinds:
            return True
        return any(
            _weight_has_kind(value, kinds)
            for value in expression.values()
        )
    if isinstance(expression, Sequence) and not isinstance(
        expression,
        (str, bytes),
    ):
        return any(_weight_has_kind(value, kinds) for value in expression)
    return False


def _weight_has_source_operation(
    expression: Any,
    operations: set[str],
) -> bool:
    if isinstance(expression, Mapping):
        if str(expression.get("operation") or "") in operations:
            return True
        return any(
            _weight_has_source_operation(value, operations)
            for value in expression.values()
        )
    if isinstance(expression, Sequence) and not isinstance(
        expression,
        (str, bytes),
    ):
        return any(
            _weight_has_source_operation(value, operations)
            for value in expression
        )
    return False


class ClosureSimplifier:
    """Merge only terms with proved identical closure semantics."""

    def simplify(self, weighted_set: Mapping[str, Any]) -> dict[str, Any]:
        groups: dict[str, list[Mapping[str, Any]]] = {}
        for term in weighted_set.get("terms", []) or []:
            if not isinstance(term, Mapping):
                continue
            key = canonical_hash(
                {
                    "closureKind": term.get("closureKind"),
                    "domain": term.get("domain"),
                    "parameters": term.get("parameters") or {},
                    "distribution": term.get("distribution") or "",
                }
            )
            groups.setdefault(key, []).append(term)
        result: list[dict[str, Any]] = []
        simplifications: list[dict[str, Any]] = []
        for key in sorted(groups):
            group = sorted(
                groups[key],
                key=lambda item: str(item.get("id") or ""),
            )
            if len(group) == 1:
                result.append(dict(group[0]))
                continue
            weights = [
                item.get("finalWeight")
                for item in group
                if isinstance(item.get("finalWeight"), Mapping)
            ]
            merged = dict(group[0])
            merged["id"] = stable_uuid(
                "miku-simplified-closure",
                ":".join(str(item.get("id") or "") for item in group),
            )
            merged["stableTermId"] = merged["id"]
            merged["finalWeight"] = add_weight(weights)
            merged["weightExpression"] = merged["finalWeight"]
            merged["finalWeightExpression"] = merged["finalWeight"]
            merged["dynamicDependencies"] = _weight_dependencies(
                merged["finalWeight"]
            )
            merged["viewDependent"] = _weight_has_kind(
                merged["finalWeight"],
                {"ViewDependent", "LayerWeight", "Fresnel"},
            )
            merged["sceneDependent"] = _weight_has_kind(
                merged["finalWeight"],
                {"Texture"},
            )
            merged["meshDependent"] = _weight_has_source_operation(
                merged["finalWeight"],
                {
                    "Input.Geometry",
                    "Input.Normal",
                    "Input.Position",
                    "Input.VertexColor",
                },
            )
            merged["mergedFrom"] = [
                str(item.get("id") or "") for item in group
            ]
            merged["weightTrace"] = [
                {
                    "operation": "ExactEquivalentTermMerge",
                    "sourceTermIds": merged["mergedFrom"],
                    "resultWeightId": merged["finalWeight"].get("id"),
                }
            ]
            merged["sourcePaths"] = [
                path
                for item in group
                for path in item.get("sourcePaths", []) or []
            ]
            result.append(merged)
            simplifications.append(
                {
                    "kind": "Exact",
                    "algorithm": "IdenticalSemanticTermWeightSum-v1",
                    "sourceTermIds": merged["mergedFrom"],
                    "resultTermId": merged["id"],
                }
            )
        result.sort(key=lambda item: str(item.get("id") or ""))
        return {
            "schema": "miku-weighted-closures-1.0",
            "terms": result,
            "simplifications": simplifications,
            "approximations": [],
        }


def expression_is_dynamic(expression: Mapping[str, Any]) -> bool:
    kind = str(expression.get("kind") or "")
    if kind in {
        "Parameter",
        "Texture",
        "Math",
        "ViewDependent",
        "LayerWeight",
        "Fresnel",
    }:
        return True
    for key in ("input", "inputs"):
        value = expression.get(key)
        if isinstance(value, Mapping) and expression_is_dynamic(value):
            return True
        if isinstance(value, Sequence) and not isinstance(value, (str, bytes)):
            if any(
                expression_is_dynamic(item)
                for item in value
                if isinstance(item, Mapping)
            ):
                return True
    return False


def evaluate_weight(
    expression: Mapping[str, Any],
    values: Mapping[str, float] | None = None,
) -> float:
    """Evaluate a weight expression for deterministic tests and diagnostics."""

    values = values or {}
    kind = str(expression.get("kind") or "")
    if kind == "Constant":
        return float(expression.get("value") or 0.0)
    if kind in {"Parameter", "Texture", "Math", "ViewDependent", "LayerWeight", "Fresnel"}:
        expression_id = str(expression.get("id") or "")
        if expression_id not in values:
            raise KeyError(expression_id)
        return float(values[expression_id])
    if kind == "ImplicitConversion":
        value = evaluate_weight(_required_mapping(expression, "input"), values)
        return value
    if kind == "Multiply":
        inputs = _required_sequence(expression, "inputs")
        return math.prod(
            evaluate_weight(item, values)
            for item in inputs
            if isinstance(item, Mapping)
        )
    if kind == "Add":
        return sum(
            evaluate_weight(item, values)
            for item in _required_sequence(expression, "inputs")
            if isinstance(item, Mapping)
        )
    if kind == "OneMinus":
        return 1.0 - evaluate_weight(
            _required_mapping(expression, "input"),
            values,
        )
    if kind == "Clamp":
        value = evaluate_weight(_required_mapping(expression, "input"), values)
        return min(
            float(expression.get("maximum", 1.0)),
            max(float(expression.get("minimum", 0.0)), value),
        )
    raise ValueError(f"MIKU_WEIGHT_EXPRESSION_KIND_UNKNOWN:{kind}")


def _required_mapping(value: Mapping[str, Any], key: str) -> Mapping[str, Any]:
    result = value.get(key)
    if not isinstance(result, Mapping):
        raise ValueError(f"MIKU_REQUIRED_OBJECT_MISSING:{key}")
    return result


def _required_sequence(value: Mapping[str, Any], key: str) -> Sequence[Any]:
    result = value.get(key)
    if not isinstance(result, Sequence) or isinstance(result, (str, bytes)):
        raise ValueError(f"MIKU_REQUIRED_ARRAY_MISSING:{key}")
    return result


def build_weighted_closure_set(
    graph: Mapping[str, Any],
    *,
    color_management: ColorManagementContext | None = None,
) -> tuple[dict[str, Any], dict[str, Any], list[dict[str, Any]]]:
    builder = ClosureGraphBuilder(
        graph,
        color_management=color_management,
    )
    closure_graph = builder.build()
    weighted = ClosureWeightFlattener().flatten(closure_graph["root"])
    simplified = ClosureSimplifier().simplify(weighted)
    return closure_graph, simplified, builder.diagnostics
