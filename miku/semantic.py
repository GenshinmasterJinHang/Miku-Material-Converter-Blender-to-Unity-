"""Conversion from source snapshots to target-neutral semantic regions.

This module intentionally emits closures and maximal opaque semantic regions,
not a copy of the Blender node list.  Source node/socket locations are kept in
the private SourceMap so Blender can replay a bake or parameter write later.
"""

from __future__ import annotations

import hashlib
from collections import defaultdict
from typing import Any, Iterable, Mapping

from .closure_ir import (
    AddShaderEnergyPolicy,
    ClosureBudget,
    FidelityPolicy,
    build_weighted_closure_set,
)
from .contracts import (
    RETIRED_WORKFLOW_KINDS,
    WORKFLOW_KINDS,
    canonical_hash,
    make_document,
    stable_uuid,
)
from .socket_conversion import ColorManagementContext
from .surface_models import build_surface_model_plan


_CLOSURES = {
    "Shader.PrincipledBSDF": "PrincipledClosure",
    "Shader.DiffuseBSDF": "DiffuseClosure",
    "Shader.AnisotropicBSDF": "AnisotropicClosure",
    "Shader.GlossyBSDF": "GlossyClosure",
    "Shader.MetallicBSDF": "MetallicClosure",
    "Shader.TransparentBSDF": "TransparentClosure",
    "Shader.GlassBSDF": "GlassClosure",
    "Shader.RefractionBSDF": "RefractionClosure",
    "Shader.TranslucentBSDF": "TranslucentClosure",
    "Shader.SubsurfaceScattering": "SubsurfaceClosure",
    "Shader.SheenBSDF": "SheenClosure",
    "Shader.Emission": "EmissionClosure",
    "Shader.Mix": "SurfaceMix",
    "Shader.Add": "SurfaceMix",
}
_MESH_OPS = {
    "Input.Geometry",
    "Input.ObjectInfo",
    "Input.Wireframe",
    "Input.AmbientOcclusion",
    "Vector.Bump",
    "Vector.Bevel",
    "Vector.Displacement",
}
_GEOMETRY_RUNTIME_OUTPUTS = frozenset({"incoming", "backfacing"})
_NON_BAKEABLE_RUNTIME_OPS = {
    "Input.ViewDirection",
    "Input.LayerWeight",
    "Input.Fresnel",
    "Input.CameraData",
    "Input.Time",
    "Input.LightPath",
    "Shader.ToRGB",
}
_PORTABLE_CONVERSION_MODES = {
    "Auto",
    "NativeOnly",
    "PreferNative",
    "ReusableBakeOnly",
}
_SOURCE_MESH_CONVERSION_MODES = {
    "AllowMeshBake",
    "FullPBRBake",
    "AppearanceSnapshot",
}
_REUSABLE_BAKE_CONVERSION_MODES = {
    "PreferNative",
    "ReusableBakeOnly",
}
_DEPENDENCY_DOMAIN_RANK = {
    "Uniform": 0,
    "UV0": 1,
    "MeshSurface": 2,
    "Runtime": 3,
}
_PURE_OP_PREFIXES = ("Math", "Vector", "Color", "Converter", "Texture", "Utility", "Input.")
_PBR_SEMANTICS = (
    "BaseColor",
    "Metalness",
    "Roughness",
    "Normal",
    "Emission",
    "Alpha",
    "AmbientOcclusion",
    "TransmissionColor",
    "TransmissionWeight",
    "IOR",
    "Thickness",
    "Height",
    "Displacement",
)
_NORMAL_CONVENTIONS = {
    "TangentOpenGLPositiveY",
    "TangentDirectXNegativeY",
}
_SCALAR_IMAGE_SEMANTICS = {
    "Metalness",
    "Roughness",
    "Alpha",
    "AmbientOcclusion",
    "Height",
    "EmissionMask",
}

_VALUE_TYPES = {
    "VALUE": "Scalar",
    "FLOAT": "Scalar",
    "BOOLEAN": "Boolean",
    "VECTOR": "Float3",
    "RGBA": "Color",
    "COLOR": "Color",
}


def _canonical_channel_default(semantic: str, value: Any) -> Any:
    """Keep Blender socket sentinels out of target-neutral channel defaults."""

    if (
        semantic != "Normal"
        or not isinstance(value, (list, tuple))
        or len(value) < 3
    ):
        return value
    try:
        components = [float(value[index]) for index in range(3)]
    except (TypeError, ValueError):
        return value
    if all(abs(component) <= 1e-8 for component in components):
        return [0.0, 0.0, 1.0]
    return value


def _normalize_socket(value: Any) -> str:
    return "".join(character for character in str(value or "").lower() if character.isalnum())


def _socket_record(
    node: Mapping[str, Any],
    socket: str,
    *,
    inputs: bool,
    value_type: str | None = None,
) -> Mapping[str, Any]:
    records = [
        record
        for record in node.get("inputs" if inputs else "outputs") or []
        if isinstance(record, Mapping)
        and bool(record.get("enabled", True))
        and not bool(record.get("isUnavailable", False))
    ]
    normalized = _normalize_socket(socket)
    candidates = [
        record
        for record in records
        if _normalize_socket(record.get("id")) == normalized
    ]
    if not candidates:
        candidates = [
            record
            for record in records
            if _normalize_socket(record.get("name")) == normalized
        ]
    if value_type is not None:
        typed = [
            record
            for record in candidates
            if _value_type(record, value_type) == value_type
        ]
        if typed:
            candidates = typed
    if len(candidates) > 1:
        raise ValueError(
            "MIKU_SOCKET_AMBIGUOUS:"
            f"{node.get('id') or ''}:{socket}:{value_type or 'Any'}"
        )
    if candidates:
        return candidates[0]
    return {}


def _value_type(record: Mapping[str, Any], fallback: str = "Scalar") -> str:
    raw = str(record.get("valueType") or record.get("type") or fallback)
    return _VALUE_TYPES.get(raw.upper(), raw)


class _RuntimeExpressionCompiler:
    """Lower the supported dynamic subset into a target-neutral expression DAG."""

    def __init__(
        self,
        graph: Mapping[str, Any],
        nodes: Mapping[str, Mapping[str, Any]],
        material_key: str,
        *,
        conversion_mode: str = "Auto",
        fidelity_policy: str | FidelityPolicy = (
            FidelityPolicy.ALLOW_DECLARED_APPROXIMATION
        ),
    ) -> None:
        self.graph = graph
        self.nodes = nodes
        self.material_key = material_key
        self.conversion_mode = str(conversion_mode or "Auto")
        self.strict_fidelity = (
            fidelity_policy.value
            if isinstance(fidelity_policy, FidelityPolicy)
            else str(fidelity_policy)
        ) == FidelityPolicy.STRICT.value
        self.expressions: list[dict[str, Any]] = []
        self._expressions_by_id: dict[str, dict[str, Any]] = {}
        self.expression_islands: list[dict[str, Any]] = []
        self.diagnostics: list[dict[str, Any]] = []
        self._cache: dict[tuple[str, str, str], str] = {}
        self._visiting: set[tuple[str, str, str]] = set()
        self._last_static_bake_leaf: dict[str, str] | None = None
        self._incoming: dict[tuple[str, str], Mapping[str, Any]] = {}
        for edge in graph.get("edges", []) or []:
            source = edge.get("from") or {}
            target = edge.get("to") or {}
            node_id = str(target.get("node") or "")
            socket = _normalize_socket(target.get("socket"))
            if node_id and socket:
                self._incoming[(node_id, socket)] = source

    def _static_source_requires_bake(
        self,
        source: Mapping[str, Any],
        seen: set[tuple[str, str]] | None = None,
    ) -> bool:
        """Return whether a runtime-independent source needs Blender evaluation."""

        node_id = str(source.get("node") or "")
        socket = str(source.get("socket") or "")
        key = (node_id, _normalize_socket(socket))
        node = self.nodes.get(node_id)
        if node is None:
            self._last_static_bake_leaf = {
                "node": node_id,
                "socket": socket,
            }
            return True
        if seen is None:
            seen = set()
        if key in seen:
            self._last_static_bake_leaf = {
                "node": node_id,
                "socket": socket,
            }
            return True
        seen.add(key)
        op = _node_op(node)
        params = node.get("params") if isinstance(node.get("params"), Mapping) else {}
        operation = str(params.get("operation") or "").upper()
        native = False
        if op in {"Input.Value", "Input.Color", "Utility.Reroute"}:
            native = True
        elif op == "Color.Ramp":
            ramp = params.get("colorRamp")
            ramp = ramp if isinstance(ramp, Mapping) else {}
            native = (
                len(ramp.get("elements", []) or []) >= 2
                and str(ramp.get("interpolation") or "LINEAR").upper()
                in {"LINEAR", "EASE", "B_SPLINE", "CONSTANT"}
            )
        elif op == "Color.HueSaturationValue":
            native = True
        elif op == "Math":
            native = operation in {
                "ADD",
                "SUBTRACT",
                "MULTIPLY",
                "DIVIDE",
                "POWER",
                "MINIMUM",
                "MAXIMUM",
                "GREATER_THAN",
                "LESS_THAN",
                "MULTIPLY_ADD",
                "MODULO",
                "LOGARITHM",
                "ABSOLUTE",
                "SINE",
                "COSINE",
            }
        elif op == "VectorMath":
            native = operation in {
                "ADD",
                "SUBTRACT",
                "MULTIPLY",
                "DIVIDE",
                "DOT_PRODUCT",
                "NORMALIZE",
                "LENGTH",
                "ABSOLUTE",
                "SCALE",
            }
        elif op in {
            "Math.Add",
            "Math.Subtract",
            "Math.Multiply",
            "Math.Divide",
            "Math.Power",
            "Math.Minimum",
            "Math.Maximum",
            "Math.Absolute",
            "Math.Sine",
            "Math.Cosine",
        }:
            native = True
        elif op in {"Math.Mix", "Color.Mix"}:
            native = str(params.get("blend_type") or "MIX").upper() in {
                "MIX",
                "ADD",
                "SUBTRACT",
                "OVERLAY",
                "MULTIPLY",
                "DARKEN",
                "SCREEN",
                "DIFFERENCE",
            }
        elif op == "Color.Invert":
            native = True
        elif op in {"Converter.SeparateColor", "Converter.SeparateXYZ"}:
            native = (
                op == "Converter.SeparateXYZ"
                or str(params.get("mode") or "RGB").upper() == "RGB"
            )
        elif op == "Input.TextureCoordinate":
            native = _normalize_socket(socket) == "object"
        elif op == "Vector.Mapping":
            native = (
                str(
                    params.get("vectorType")
                    or params.get("vector_type")
                    or "POINT"
                ).upper()
                == "POINT"
            )
        elif op == "Texture.Image":
            # Keep image validation in the compiler so malformed, unsupported,
            # or missing texture data produces its specific diagnostic instead
            # of being mistaken for a generic procedural bake candidate.
            native = True
        elif op == "Vector.NormalMap":
            native = (
                str(params.get("space") or "TANGENT").upper()
                == "TANGENT"
                and not str(params.get("uvMap") or "")
            )
        elif op in {
            "Vector.Bump",
            "Vector.Displacement",
            "Vector.DisplacementBump",
        }:
            native = True
        elif op == "Texture.Noise":
            native = _normalize_socket(socket) in {"fac", "factor"}
        if not native:
            # Remember the exact unsupported endpoint reached through native
            # wrappers. Portable-mode diagnostics can then name the actionable
            # leaf (for example Voronoi Color), rather than an outer Color Ramp
            # or HSV consumer.
            self._last_static_bake_leaf = {
                "node": node_id,
                "socket": socket,
            }
            return True
        for input_socket in node.get("inputs", []) or []:
            if not isinstance(input_socket, Mapping):
                continue
            incoming = self._incoming.get(
                (
                    node_id,
                    _normalize_socket(
                        input_socket.get("id") or input_socket.get("name")
                    ),
                )
            )
            if incoming and self._static_source_requires_bake(incoming, seen):
                return True
        return False

    @property
    def allows_source_mesh_bake(self) -> bool:
        return self.conversion_mode in _SOURCE_MESH_CONVERSION_MODES

    def _portable_mesh_bake_error(
        self,
        source: Mapping[str, Any],
        *,
        detail: str = "",
    ) -> ValueError:
        consumer_node_id = str(source.get("node") or "")
        consumer_socket = str(source.get("socket") or "")
        leaf = self._last_static_bake_leaf or dict(source)
        node_id = str(leaf.get("node") or consumer_node_id)
        socket = str(leaf.get("socket") or consumer_socket)
        node = self.nodes.get(node_id, {})
        op = _node_op(node) or "Unknown"
        path = ""
        if (node_id, _normalize_socket(socket)) != (
            consumer_node_id,
            _normalize_socket(consumer_socket),
        ):
            path = (
                ":consumerPath="
                f"{consumer_node_id}.{consumer_socket}<-{node_id}.{socket}"
            )
        suffix = f":{detail}" if detail else ""
        code = (
            "MIKU_PORTABLE_HYBRID_MESH_DEPENDENCY"
            if self.conversion_mode == "PreferNative"
            else "MIKU_SOURCE_MESH_FIDELITY_REQUIRED"
        )
        domain = self.static_dependency_domain(source)
        return ValueError(
            f"{code}:{op}:{node_id}:{socket}:dependencyDomain={domain}"
            f"{path}{suffix}"
        )

    @property
    def allows_reusable_bake(self) -> bool:
        return self.conversion_mode in _REUSABLE_BAKE_CONVERSION_MODES

    @staticmethod
    def _combine_dependency_domains(domains: Iterable[str]) -> str:
        values = [str(item or "Uniform") for item in domains]
        return max(
            values or ["Uniform"],
            key=lambda item: _DEPENDENCY_DOMAIN_RANK.get(item, 2),
        )

    def static_dependency_domain(
        self,
        source: Mapping[str, Any],
        seen: set[tuple[str, str]] | None = None,
    ) -> str:
        """Classify the source domain without treating UV pixels as mesh data."""

        node_id = str(source.get("node") or "")
        socket = str(source.get("socket") or "")
        key = (node_id, _normalize_socket(socket))
        node = self.nodes.get(node_id)
        if node is None:
            return "MeshSurface"
        if seen is None:
            seen = set()
        if key in seen:
            return "MeshSurface"
        seen.add(key)
        if self.depends_on_runtime(source):
            return "Runtime"
        op = _node_op(node)
        normalized_socket = _normalize_socket(socket)
        if op == "Input.TextureCoordinate":
            if normalized_socket == "uv":
                return "UV0"
            if normalized_socket in {"camera", "reflection"}:
                return "Runtime"
            return "MeshSurface"
        if op == "Input.UVMap":
            return "UV0"
        if op in _MESH_OPS or op in {
            "Input.Normal",
            "Input.Position",
            "Input.Tangent",
        }:
            return "MeshSurface"

        input_domains = []
        has_linked_vector_input = False
        for input_socket in node.get("inputs", []) or []:
            if not isinstance(input_socket, Mapping):
                continue
            normalized_input = _normalize_socket(
                input_socket.get("id") or input_socket.get("name")
            )
            incoming = self._incoming.get((node_id, normalized_input))
            if incoming:
                if normalized_input == "vector":
                    has_linked_vector_input = True
                input_domains.append(
                    self.static_dependency_domain(incoming, set(seen))
                )
        if op == "Texture.Image" and not has_linked_vector_input:
            input_domains.append("UV0")
        if op.startswith("Texture.") and not input_domains:
            # Blender procedural textures without an explicit vector use a
            # generated surface coordinate and therefore are not reusable UV
            # functions.
            input_domains.append("MeshSurface")
        return self._combine_dependency_domains(input_domains)

    def can_reusable_bake(self, source: Mapping[str, Any]) -> bool:
        return (
            self.allows_reusable_bake
            and self.static_dependency_domain(source) in {"Uniform", "UV0"}
        )

    def _baked_island(
        self,
        source: Mapping[str, Any],
        *,
        value_type: str,
        usage: str,
        coordinate_domain: str = "MeshSurface",
        mesh_binding_required: bool = True,
    ) -> str:
        node_id = str(source.get("node") or "")
        socket = str(source.get("socket") or "")
        cache_key = (
            node_id,
            _normalize_socket(socket),
            usage,
            coordinate_domain,
            bool(mesh_binding_required),
        )
        for island in self.expression_islands:
            if tuple(island.get("_cacheKey") or ()) == cache_key:
                return str(island["expressionId"])
        resource_id = stable_uuid(
            "miku-expression-island-resource",
            f"{self.material_key}:{node_id}:{socket}:{usage}",
        )
        reference_hash = hashlib.sha256(resource_id.encode("utf-8")).hexdigest()[:20]
        expression_id = self._emit(
            f"{node_id}:{socket}:baked-island:{usage}",
            "Texture.SampleBaked2D",
            value_type=value_type,
            space="Tangent" if usage == "Normal" else "None",
            stage="Fragment",
            uniformity="Varying",
            params={
                "resourceId": resource_id,
                "referenceName": f"_MIKU_Baked_{reference_hash}",
                "usage": usage,
                "channel": "RGB" if usage != "Scalar" else "R",
                "colorSpace": "Linear",
                "uvSet": "UV0",
                "coordinateDomain": coordinate_domain,
                "meshBindingRequired": bool(mesh_binding_required),
            },
            source=source,
        )
        self.expression_islands.append(
            {
                "_cacheKey": list(cache_key),
                "expressionId": expression_id,
                "resourceId": resource_id,
                "sourceNodeId": node_id,
                "sourceSocketId": socket,
                "usage": usage,
                "valueType": value_type,
                "referenceName": f"_MIKU_Baked_{reference_hash}",
                "coordinateDomain": coordinate_domain,
                "meshBindingRequired": bool(mesh_binding_required),
            }
        )
        return expression_id

    def depends_on_runtime(
        self,
        source: Mapping[str, Any],
        seen: set[tuple[str, str]] | None = None,
    ) -> bool:
        node_id = str(source.get("node") or "")
        socket = str(source.get("socket") or "")
        key = (node_id, _normalize_socket(socket))
        if not node_id or node_id not in self.nodes:
            return False
        if seen is None:
            seen = set()
        if key in seen:
            return False
        seen.add(key)
        node = self.nodes[node_id]
        op = _node_op(node)
        output_record = _socket_record(node, socket, inputs=False)
        output_driver = (
            output_record.get("driver")
            if isinstance(output_record.get("driver"), Mapping)
            else {}
        )
        if str(output_driver.get("kind") or "") in {
            "TimeAffine",
            "Externalized",
            "Unsupported",
        }:
            return True
        if op in _NON_BAKEABLE_RUNTIME_OPS:
            return True
        if (
            op == "Input.Geometry"
            and _normalize_socket(socket) in _GEOMETRY_RUNTIME_OUTPUTS
        ):
            return True
        for input_socket in node.get("inputs", []) or []:
            if not isinstance(input_socket, Mapping):
                continue
            incoming = self._incoming.get(
                (node_id, _normalize_socket(input_socket.get("id") or input_socket.get("name")))
            )
            if incoming and self.depends_on_runtime(incoming, seen):
                return True
        return False

    def requires_static_bake(self, source: Mapping[str, Any]) -> bool:
        """Return whether Blender must evaluate a runtime-independent source."""

        self._last_static_bake_leaf = None
        return (
            not self.depends_on_runtime(source)
            and self._static_source_requires_bake(source)
        )

    def compile(
        self,
        source: Mapping[str, Any],
        *,
        semantic: str = "",
    ) -> str:
        node_id = str(source.get("node") or "")
        socket = str(source.get("socket") or "")
        semantic = str(semantic or "")
        key = (node_id, _normalize_socket(socket), semantic)
        if key in self._cache:
            return self._cache[key]
        if key in self._visiting:
            raise ValueError(f"MIKU_EXPRESSION_CYCLE:{node_id}:{socket}")
        node = self.nodes.get(node_id)
        if node is None:
            raise ValueError(f"MIKU_EXPRESSION_SOURCE_MISSING:{node_id}")
        self._visiting.add(key)
        try:
            expression_id = self._compile_node_output(
                node,
                socket,
                semantic=semantic,
            )
            self._cache[key] = expression_id
            return expression_id
        finally:
            self._visiting.discard(key)

    def compile_baked(
        self,
        source: Mapping[str, Any],
        *,
        value_type: str,
        usage: str,
        coordinate_domain: str = "MeshSurface",
        mesh_binding_required: bool = True,
    ) -> str:
        """Represent one Blender-evaluated source as a baked expression."""

        return self._baked_island(
            source,
            value_type=value_type,
            usage=usage,
            coordinate_domain=coordinate_domain,
            mesh_binding_required=mesh_binding_required,
        )

    def _emit(
        self,
        role: str,
        op: str,
        *,
        value_type: str,
        space: str = "None",
        stage: str = "Fragment",
        uniformity: str = "Varying",
        inputs: Mapping[str, str] | None = None,
        params: Mapping[str, Any] | None = None,
        source: Mapping[str, Any] | None = None,
    ) -> str:
        expression_id = stable_uuid(
            "miku-expression",
            f"{self.material_key}:{role}:{op}",
        )
        record = {
            "id": expression_id,
            "op": op,
            "valueType": value_type,
            "space": space,
            "stage": stage,
            "uniformity": uniformity,
            "inputs": {
                name: {"expressionId": reference}
                for name, reference in sorted((inputs or {}).items())
            },
            "params": dict(params or {}),
        }
        if source:
            record["source"] = {
                "nodeId": str(source.get("node") or ""),
                "socketId": str(source.get("socket") or ""),
            }
        existing = self._expressions_by_id.get(expression_id)
        if existing is not None:
            if existing != record:
                collision_payload = {
                    key: value
                    for key, value in record.items()
                    if key != "id"
                }
                expression_id = stable_uuid(
                    "miku-expression-disambiguated",
                    canonical_hash(collision_payload),
                )
                record["id"] = expression_id
                disambiguated = self._expressions_by_id.get(
                    expression_id
                )
                if disambiguated is not None:
                    if disambiguated != record:
                        raise ValueError(
                            "MIKU_EXPRESSION_ID_COLLISION:"
                            f"{expression_id}"
                        )
                    return expression_id
            else:
                return expression_id
        self._expressions_by_id[expression_id] = record
        self.expressions.append(record)
        return expression_id

    def _constant(
        self,
        role: str,
        value: Any,
        value_type: str = "Scalar",
    ) -> str:
        return self._emit(
            role,
            "Constant",
            value_type=value_type,
            stage="Both",
            uniformity="Uniform",
            params={"value": value},
        )

    def _implicit(
        self,
        role: str,
        op: str,
        *,
        value_type: str,
        space: str,
        stage: str = "Fragment",
    ) -> str:
        return self._emit(
            role,
            op,
            value_type=value_type,
            space=space,
            stage=stage,
            uniformity="Varying",
        )

    def _input(
        self,
        node: Mapping[str, Any],
        socket: str,
        fallback: Any,
        value_type: str = "Scalar",
        usage: str | None = None,
        semantic: str = "",
    ) -> str:
        node_id = str(node.get("id") or "")
        record = _socket_record(
            node,
            socket,
            inputs=True,
            value_type=value_type,
        )
        record_socket = str(
            record.get("id") or record.get("name") or socket
        )
        incoming = self._incoming.get(
            (node_id, _normalize_socket(record_socket))
        )
        if incoming:
            height_channel = (
                self.graph.get("heightChannel")
                if isinstance(self.graph.get("heightChannel"), Mapping)
                else {}
            )
            height_source = (
                height_channel.get("source")
                if isinstance(height_channel.get("source"), Mapping)
                else {}
            )
            if (
                semantic in {"Height", "VertexHeight"}
                and str(incoming.get("node") or "")
                == str(height_source.get("node") or "")
                and _normalize_socket(incoming.get("socket"))
                == _normalize_socket(height_source.get("socket"))
            ):
                return self._emit(
                    f"{node_id}:{socket}:material-height",
                    "Input.MaterialChannel",
                    value_type="Scalar",
                    stage=(
                        "Vertex" if semantic == "VertexHeight" else "Fragment"
                    ),
                    uniformity="Varying",
                    params={
                        "semantic": "Height",
                        "uvSet": "UV0",
                        "lod": 0,
                    },
                    source=incoming,
                )
            incoming_node = self.nodes.get(
                str(incoming.get("node") or ""), {}
            )
            if (
                _node_op(incoming_node) == "Texture.Noise"
                and _normalize_socket(incoming.get("socket")) == "color"
                and value_type == "Scalar"
            ):
                incoming = {
                    **dict(incoming),
                    "socket": "Fac",
                }
                diagnostic_code = (
                    "MIKU_NOISE_COLOR_SCALAR_USES_FACTOR"
                )
                if not any(
                    item.get("code") == diagnostic_code
                    and item.get("nodeId")
                    == str(incoming.get("node") or "")
                    for item in self.diagnostics
                ):
                    self.diagnostics.append(
                        {
                            "severity": "info",
                            "code": diagnostic_code,
                            "translationQuality": "Equivalent",
                            "nodeId": str(
                                incoming.get("node") or ""
                            ),
                            "message": (
                                "A Noise Color output consumed by a "
                                "scalar socket was normalized to the "
                                "node's Factor output."
                            ),
                        }
                    )
            incoming_source = (
                incoming_node.get("source")
                if isinstance(incoming_node.get("source"), Mapping)
                else {}
            )
            if (
                not self.depends_on_runtime(incoming)
                and self._static_source_requires_bake(incoming)
            ):
                if not bool(incoming_source.get("blenderNodeName")):
                    raise ValueError(
                        "MIKU_BAKE_SOURCE_UNAVAILABLE:"
                        f"{incoming.get('node') or ''}:"
                        f"{incoming.get('socket') or ''}"
                    )
                resolved_usage = usage or (
                    "Color" if value_type == "Color" else "Scalar"
                )
                if self.can_reusable_bake(incoming):
                    return self._baked_island(
                        incoming,
                        value_type=value_type,
                        usage=resolved_usage,
                        coordinate_domain=self.static_dependency_domain(
                            incoming
                        ),
                        mesh_binding_required=False,
                    )
                if not self.allows_source_mesh_bake:
                    raise self._portable_mesh_bake_error(incoming)
                return self._baked_island(
                    incoming,
                    value_type=value_type,
                    usage=resolved_usage,
                )
            return self.compile(incoming, semantic=semantic)
        driver = record.get("driver") if isinstance(record.get("driver"), Mapping) else {}
        driver_kind = str(driver.get("kind") or "")
        if driver_kind == "TimeAffine":
            frame = self._emit(
                f"{node_id}:{socket}:driver-frame",
                "Input.Time.Frame",
                value_type="Scalar",
                stage="Both",
                uniformity="Uniform",
                params=dict(driver.get("timeContract") or {}),
            )
            scale = float(driver.get("scale", 1.0))
            offset = float(driver.get("offset", 0.0))
            current = frame
            if scale != 1.0:
                scale_id = self._constant(
                    f"{node_id}:{socket}:driver-scale",
                    scale,
                )
                current = self._emit(
                    f"{node_id}:{socket}:driver-multiply",
                    "Math.Multiply",
                    value_type="Scalar",
                    stage="Both",
                    uniformity="Uniform",
                    inputs={"A": current, "B": scale_id},
                )
            if offset != 0.0:
                offset_id = self._constant(
                    f"{node_id}:{socket}:driver-offset",
                    offset,
                )
                current = self._emit(
                    f"{node_id}:{socket}:driver-add",
                    "Math.Add",
                    value_type="Scalar",
                    stage="Both",
                    uniformity="Uniform",
                    inputs={"A": current, "B": offset_id},
                )
            return current
        if driver_kind == "Externalized":
            return self._emit(
                f"{node_id}:{socket}:driver-parameter",
                "Parameter",
                value_type=value_type,
                stage="Both",
                uniformity="Uniform",
                params={"parameterId": str(driver.get("parameterId") or "")},
            )
        if driver_kind == "Unsupported":
            raise ValueError(
                f"MIKU_TIME_DRIVER_UNSUPPORTED:{node_id}:{socket}:"
                f"{driver.get('reason') or 'unsafe driver'}"
            )
        return self._constant(
            f"{node_id}:{socket}:default",
            record.get("default", fallback),
            _value_type(record, value_type),
        )

    def _compile_color_ramp(
        self,
        node: Mapping[str, Any],
        socket: str,
        source: Mapping[str, Any],
    ) -> str:
        node_id = str(node.get("id") or "")
        params = node.get("params")
        params = params if isinstance(params, Mapping) else {}
        ramp = params.get("colorRamp")
        ramp = ramp if isinstance(ramp, Mapping) else {}
        elements = [
            item
            for item in ramp.get("elements", []) or []
            if isinstance(item, Mapping)
        ]
        elements.sort(key=lambda item: float(item.get("position", 0.0)))
        if len(elements) < 2:
            raise ValueError(
                f"MIKU_RUNTIME_COLOR_RAMP_UNSUPPORTED:{node_id}:"
                f"elementCount={len(elements)}"
            )
        interpolation = str(ramp.get("interpolation") or "LINEAR").upper()
        if interpolation not in {"LINEAR", "EASE", "B_SPLINE", "CONSTANT"}:
            raise ValueError(
                f"MIKU_RUNTIME_COLOR_RAMP_UNSUPPORTED:{node_id}:"
                f"interpolation={interpolation}"
            )
        if len(elements) > 2 or interpolation == "CONSTANT":
            normalized_socket = _normalize_socket(socket)
            value_type = "Scalar" if normalized_socket == "alpha" else "Color"
            if interpolation == "B_SPLINE":
                self.diagnostics.append(
                    {
                        "severity": "warning",
                        "code": "MIKU_COLOR_RAMP_BSPLINE_APPROXIMATE",
                        "translationQuality": "Approximate",
                        "nodeId": node_id,
                        "message": (
                            "Multi-stop Blender B-Spline Color Ramp is preserved "
                            "and expanded as piecewise cubic interpolation in "
                            "Shader Graph."
                        ),
                    }
                )
            return self._emit(
                f"{node_id}:ramp-multi:{normalized_socket}",
                "Color.Ramp",
                value_type=value_type,
                stage="Fragment",
                uniformity="Varying",
                inputs={
                    "Factor": self._input(node, "Fac", 0.0, "Scalar")
                },
                params={
                    "interpolation": interpolation,
                    "colorMode": str(ramp.get("colorMode") or "RGB"),
                    "hueInterpolation": str(
                        ramp.get("hueInterpolation") or "NEAR"
                    ),
                    "elements": elements,
                    "output": (
                        "Alpha" if normalized_socket == "alpha" else "Color"
                    ),
                },
                source=source,
            )
        first_position = float(elements[0].get("position", 0.0))
        second_position = float(elements[1].get("position", 1.0))
        width = second_position - first_position
        if width <= 1.0e-8:
            raise ValueError(
                f"MIKU_RUNTIME_COLOR_RAMP_UNSUPPORTED:{node_id}:"
                "coincidentElements"
            )
        factor = self._input(node, "Fac", 0.0, "Scalar")
        normalized = factor
        if first_position != 0.0:
            normalized = self._emit(
                f"{node_id}:ramp-offset",
                "Math.Subtract",
                value_type="Scalar",
                inputs={
                    "A": normalized,
                    "B": self._constant(
                        f"{node_id}:ramp-position-first",
                        first_position,
                    ),
                },
            )
        if width != 1.0:
            normalized = self._emit(
                f"{node_id}:ramp-scale",
                "Math.Divide",
                value_type="Scalar",
                inputs={
                    "A": normalized,
                    "B": self._constant(
                        f"{node_id}:ramp-position-width",
                        width,
                    ),
                },
            )
        normalized = self._emit(
            f"{node_id}:ramp-minimum",
            "Math.Minimum",
            value_type="Scalar",
            inputs={
                "A": normalized,
                "B": self._constant(f"{node_id}:ramp-one", 1.0),
            },
        )
        normalized = self._emit(
            f"{node_id}:ramp-maximum",
            "Math.Maximum",
            value_type="Scalar",
            inputs={
                "A": normalized,
                "B": self._constant(f"{node_id}:ramp-zero", 0.0),
            },
        )
        if interpolation == "EASE":
            squared = self._emit(
                f"{node_id}:ramp-ease-square",
                "Math.Multiply",
                value_type="Scalar",
                inputs={"A": normalized, "B": normalized},
            )
            doubled = self._emit(
                f"{node_id}:ramp-ease-double",
                "Math.Multiply",
                value_type="Scalar",
                inputs={
                    "A": self._constant(f"{node_id}:ramp-two", 2.0),
                    "B": normalized,
                },
            )
            curve = self._emit(
                f"{node_id}:ramp-ease-curve",
                "Math.Subtract",
                value_type="Scalar",
                inputs={
                    "A": self._constant(f"{node_id}:ramp-three", 3.0),
                    "B": doubled,
                },
            )
            normalized = self._emit(
                f"{node_id}:ramp-ease",
                "Math.Multiply",
                value_type="Scalar",
                inputs={"A": squared, "B": curve},
            )
        elif interpolation == "B_SPLINE":
            squared = self._emit(
                f"{node_id}:ramp-bspline-square",
                "Math.Multiply",
                value_type="Scalar",
                inputs={"A": normalized, "B": normalized},
            )
            cubed = self._emit(
                f"{node_id}:ramp-bspline-cube",
                "Math.Multiply",
                value_type="Scalar",
                inputs={"A": squared, "B": normalized},
            )
            three_t = self._emit(
                f"{node_id}:ramp-bspline-three-t",
                "Math.Multiply",
                value_type="Scalar",
                inputs={
                    "A": normalized,
                    "B": self._constant(
                        f"{node_id}:ramp-bspline-three", 3.0
                    ),
                },
            )
            three_t_squared = self._emit(
                f"{node_id}:ramp-bspline-three-t2",
                "Math.Multiply",
                value_type="Scalar",
                inputs={
                    "A": squared,
                    "B": self._constant(
                        f"{node_id}:ramp-bspline-three-squared", 3.0
                    ),
                },
            )
            two_t_cubed = self._emit(
                f"{node_id}:ramp-bspline-two-t3",
                "Math.Multiply",
                value_type="Scalar",
                inputs={
                    "A": cubed,
                    "B": self._constant(
                        f"{node_id}:ramp-bspline-two", 2.0
                    ),
                },
            )
            numerator = self._emit(
                f"{node_id}:ramp-bspline-linear-quadratic",
                "Math.Add",
                value_type="Scalar",
                inputs={
                    "A": self._constant(
                        f"{node_id}:ramp-bspline-one", 1.0
                    ),
                    "B": three_t,
                },
            )
            numerator = self._emit(
                f"{node_id}:ramp-bspline-add-quadratic",
                "Math.Add",
                value_type="Scalar",
                inputs={"A": numerator, "B": three_t_squared},
            )
            numerator = self._emit(
                f"{node_id}:ramp-bspline-subtract-cubic",
                "Math.Subtract",
                value_type="Scalar",
                inputs={"A": numerator, "B": two_t_cubed},
            )
            normalized = self._emit(
                f"{node_id}:ramp-bspline-weight",
                "Math.Divide",
                value_type="Scalar",
                inputs={
                    "A": numerator,
                    "B": self._constant(
                        f"{node_id}:ramp-bspline-six", 6.0
                    ),
                },
            )
        normalized_socket = _normalize_socket(socket)
        first_color = list(elements[0].get("color") or [0.0, 0.0, 0.0, 1.0])
        second_color = list(elements[1].get("color") or [1.0, 1.0, 1.0, 1.0])
        if normalized_socket == "alpha":
            first_value = first_color[3] if len(first_color) > 3 else 1.0
            second_value = second_color[3] if len(second_color) > 3 else 1.0
            value_type = "Scalar"
        else:
            grayscale = all(
                len(color) >= 3
                and abs(float(color[0]) - float(color[1])) <= 1.0e-8
                and abs(float(color[1]) - float(color[2])) <= 1.0e-8
                for color in (first_color, second_color)
            )
            if grayscale:
                first_value = float(first_color[0])
                second_value = float(second_color[0])
                value_type = "Scalar"
            else:
                first_value = first_color
                second_value = second_color
                value_type = "Color"
        return self._emit(
            f"{node_id}:ramp-result:{normalized_socket}",
            "Math.Lerp",
            value_type=value_type,
            inputs={
                "A": self._constant(
                    f"{node_id}:ramp-first:{normalized_socket}",
                    first_value,
                    value_type,
                ),
                "B": self._constant(
                    f"{node_id}:ramp-second:{normalized_socket}",
                    second_value,
                    value_type,
                ),
                "T": normalized,
            },
            source=source,
        )

    def _compile_node_output(
        self,
        node: Mapping[str, Any],
        socket: str,
        *,
        semantic: str = "",
    ) -> str:
        node_id = str(node.get("id") or "")
        op = _node_op(node)
        source = {"node": node_id, "socket": socket}
        output = _socket_record(node, socket, inputs=False)
        normalized = _normalize_socket(output.get("name") or socket)
        value_type = _value_type(output)

        if op == "Input.TextureCoordinate":
            if normalized != "object":
                if self.allows_source_mesh_bake:
                    return self._baked_island(
                        source,
                        value_type="Float3",
                        usage="Vector",
                    )
                raise self._portable_mesh_bake_error(
                    source,
                    detail=f"unsupportedCoordinate={normalized}",
                )
            return self._emit(
                f"{node_id}:{normalized}",
                "Input.TextureCoordinate.Object",
                value_type="Float3",
                space="Object",
                stage="Fragment",
                uniformity="Varying",
                source=source,
            )
        if op == "Vector.Mapping":
            params = (
                node.get("params")
                if isinstance(node.get("params"), Mapping)
                else {}
            )
            vector_type = str(
                params.get("vectorType")
                or params.get("vector_type")
                or "POINT"
            ).upper()
            if vector_type != "POINT":
                if self.allows_source_mesh_bake:
                    return self._baked_island(
                        source,
                        value_type="Float3",
                        usage="Vector",
                    )
                raise self._portable_mesh_bake_error(
                    source,
                    detail=f"mappingType={vector_type}",
                )
            return self._emit(
                f"{node_id}:{normalized}",
                "Vector.Mapping",
                value_type="Float3",
                space="Object",
                stage="Fragment",
                uniformity="Varying",
                inputs={
                    "Vector": self._input(
                        node,
                        "Vector",
                        [0.0, 0.0, 0.0],
                        "Float3",
                    ),
                    "Location": self._input(
                        node,
                        "Location",
                        [0.0, 0.0, 0.0],
                        "Float3",
                    ),
                    "Rotation": self._input(
                        node,
                        "Rotation",
                        [0.0, 0.0, 0.0],
                        "Float3",
                    ),
                    "Scale": self._input(
                        node,
                        "Scale",
                        [1.0, 1.0, 1.0],
                        "Float3",
                    ),
                },
                params={
                    "vectorType": "POINT",
                    "transformOrder": "ScaleRotateTranslate",
                    "rotationUnit": "Radians",
                },
                source=source,
            )
        if op in {"Converter.SeparateColor", "Converter.SeparateXYZ"}:
            params = (
                node.get("params")
                if isinstance(node.get("params"), Mapping)
                else {}
            )
            if (
                op == "Converter.SeparateColor"
                and str(params.get("mode") or "RGB").upper() != "RGB"
            ):
                raise ValueError(
                    "MIKU_PACKED_CHANNEL_MODE_UNSUPPORTED:"
                    f"{node_id}:{params.get('mode') or '<missing>'}"
                )
            component = {
                "r": "R",
                "red": "R",
                "x": "R",
                "g": "G",
                "green": "G",
                "y": "G",
                "b": "B",
                "blue": "B",
                "z": "B",
                "a": "A",
                "alpha": "A",
                "w": "A",
            }.get(normalized)
            if component is None:
                raise ValueError(
                    "MIKU_PACKED_CHANNEL_OUTPUT_UNSUPPORTED:"
                    f"{node_id}:{socket}"
                )
            input_name = (
                "Color"
                if op == "Converter.SeparateColor"
                else "Vector"
            )
            input_record = _socket_record(
                node,
                input_name,
                inputs=True,
            )
            incoming = self._incoming.get(
                (
                    node_id,
                    _normalize_socket(
                        input_record.get("id")
                        or input_record.get("name")
                        or input_name
                    ),
                )
            )
            if not incoming:
                return self._constant(
                    f"{node_id}:{normalized}:component-default",
                    0.0,
                )
            incoming_node = self.nodes.get(
                str(incoming.get("node") or ""),
                {},
            )
            if _node_op(incoming_node) == "Texture.Image":
                return self.compile(
                    {
                        "node": str(incoming.get("node") or ""),
                        "socket": component,
                    },
                    semantic=semantic,
                )
            return self._emit(
                f"{node_id}:{normalized}:component",
                "Vector.Component",
                value_type="Scalar",
                stage="Fragment",
                uniformity="Varying",
                inputs={
                    "Input": self.compile(
                        incoming,
                        semantic=semantic,
                    )
                },
                params={"component": component},
                source=source,
            )
        if op == "Color.Invert":
            invert_type = (
                "Scalar"
                if semantic in _SCALAR_IMAGE_SEMANTICS
                else "Color"
            )
            fallback = (
                0.0
                if invert_type == "Scalar"
                else [0.0, 0.0, 0.0, 1.0]
            )
            original = self._input(
                node,
                "Color",
                fallback,
                invert_type,
                "Scalar" if invert_type == "Scalar" else "Color",
                semantic=semantic,
            )
            inverted = self._emit(
                f"{node_id}:{normalized}:one-minus",
                "Math.OneMinus",
                value_type=invert_type,
                stage="Fragment",
                uniformity="Varying",
                inputs={"A": original},
                source=source,
            )
            return self._emit(
                f"{node_id}:{normalized}:factor",
                "Math.Lerp",
                value_type=invert_type,
                stage="Fragment",
                uniformity="Varying",
                inputs={
                    "A": original,
                    "B": inverted,
                    "T": self._input(node, "Fac", 1.0),
                },
                params={
                    "formula": "lerp(Color, 1-Color, Factor)",
                },
                source=source,
            )
        if op == "Texture.Image":
            params = (
                node.get("params")
                if isinstance(node.get("params"), Mapping)
                else {}
            )
            image = params.get("image")
            image = image if isinstance(image, Mapping) else {}
            image_source = str(image.get("source") or "").upper()
            image_format = str(
                image.get("fileFormat") or ""
            ).upper()
            projection = str(
                params.get("projection") or "FLAT"
            ).upper()
            interpolation = str(
                params.get("interpolation") or "LINEAR"
            ).upper()
            extension = str(
                params.get("extension") or "REPEAT"
            ).upper()
            if image_source != "FILE":
                raise ValueError(
                    "MIKU_IMAGE_SOURCE_UNSUPPORTED:"
                    f"{node_id}:{image_source or '<missing>'}"
                )
            if image_format not in {"PNG", "JPEG", "OPEN_EXR"}:
                raise ValueError(
                    "MIKU_IMAGE_FORMAT_UNSUPPORTED:"
                    f"{node_id}:{image_format or '<missing>'}"
                )
            if projection != "FLAT":
                raise ValueError(
                    "MIKU_IMAGE_PROJECTION_UNSUPPORTED:"
                    f"{node_id}:{projection}"
                )
            if interpolation not in {"LINEAR", "CLOSEST"}:
                raise ValueError(
                    "MIKU_IMAGE_INTERPOLATION_UNSUPPORTED:"
                    f"{node_id}:{interpolation}"
                )
            if extension not in {"REPEAT", "EXTEND"}:
                raise ValueError(
                    "MIKU_IMAGE_EXTENSION_UNSUPPORTED:"
                    f"{node_id}:{extension}"
                )
            if self._incoming.get((node_id, "vector")):
                raise ValueError(
                    "MIKU_IMAGE_UV_SOURCE_UNSUPPORTED:"
                    f"{node_id}:only implicit UV0 is supported"
                )
            normalized_semantic = str(semantic or "")
            if normalized_semantic in {
                "VertexHeight",
                "Height",
            }:
                resource_semantic = "Height"
            elif normalized_semantic in _PBR_SEMANTICS:
                resource_semantic = normalized_semantic
            elif normalized_semantic == "EmissionMask":
                resource_semantic = "EmissionMask"
            elif normalized == "alpha":
                resource_semantic = "Alpha"
            else:
                resource_semantic = "BaseColor"
            reference_names = {
                "BaseColor": "_BaseMap",
                "Metalness": "_MetallicMap",
                "Roughness": "_RoughnessMap",
                "Normal": "_BumpMap",
                "Emission": "_EmissionMap",
                "Alpha": "_AlphaMap",
                "AmbientOcclusion": "_OcclusionMap",
                "Height": "_MIKU_HeightMap",
                "EmissionMask": "_MIKU_EmissionMask",
            }
            usage = (
                "Normal"
                if resource_semantic == "Normal"
                else (
                    "Color"
                    if resource_semantic in {"BaseColor", "Emission"}
                    else "Scalar"
                )
            )
            color_space_name = str(
                image.get("colorSpaceName") or ""
            )
            color_space = (
                "sRGB"
                if color_space_name.strip().lower() == "srgb"
                else "Linear"
            )
            if usage in {"Normal", "Scalar"} and color_space != "Linear":
                raise ValueError(
                    "MIKU_DATA_TEXTURE_COLOR_SPACE_UNSUPPORTED:"
                    f"{node_id}:{resource_semantic}:{color_space_name}"
                )
            normal_convention = str(
                self.graph.get("normalConvention")
                or "TangentOpenGLPositiveY"
            )
            if usage == "Normal" and normal_convention not in _NORMAL_CONVENTIONS:
                raise ValueError(
                    "MIKU_NORMAL_CONVENTION_INVALID:"
                    f"{node_id}:{normal_convention}"
                )
            resource_id = stable_uuid(
                "miku-static-image-resource",
                (
                    f"{self.material_key}:"
                    f"{image.get('resourceBaseId') or node_id}"
                ),
            )
            explicit_channels = {
                "r": "R",
                "red": "R",
                "x": "R",
                "g": "G",
                "green": "G",
                "y": "G",
                "b": "B",
                "blue": "B",
                "z": "B",
                "a": "A",
                "alpha": "A",
            }
            channel = explicit_channels.get(
                normalized,
                "RGB" if usage in {"Color", "Normal"} else "R",
            )
            expression_value_type = (
                "Float3"
                if usage == "Normal"
                else ("Color" if usage == "Color" else "Scalar")
            )
            expression_stage = (
                "Vertex"
                if normalized_semantic == "VertexHeight"
                else "Fragment"
            )
            return self._emit(
                (
                    f"{node_id}:{normalized}:static-image:"
                    f"{resource_semantic}:"
                    f"{expression_stage.lower()}"
                ),
                "Texture.SampleImage2D",
                value_type=expression_value_type,
                space="Tangent" if usage == "Normal" else "None",
                stage=expression_stage,
                uniformity="Varying",
                params={
                    "resourceId": resource_id,
                    "referenceName": reference_names.get(
                        resource_semantic,
                        "_MIKU_" + resource_semantic + "Map",
                    ),
                    "semantic": resource_semantic,
                    "usage": usage,
                    "channel": channel,
                    "colorSpace": color_space,
                    "uvSet": "UV0",
                    "projection": projection,
                    "interpolation": interpolation,
                    "extension": extension,
                    "lodMode": (
                        "Explicit0"
                        if expression_stage == "Vertex"
                        else "Auto"
                    ),
                    **(
                        {
                            "normalConvention": str(
                                normal_convention
                            )
                        }
                        if usage == "Normal"
                        else {}
                    ),
                },
                source=source,
            )
        if op == "Vector.NormalMap":
            params = (
                node.get("params")
                if isinstance(node.get("params"), Mapping)
                else {}
            )
            normal_space = str(
                params.get("space") or "TANGENT"
            ).upper()
            if normal_space != "TANGENT" or str(
                params.get("uvMap") or ""
            ):
                raise ValueError(
                    "MIKU_NORMAL_MAP_SPACE_UNSUPPORTED:"
                    f"{node_id}:{normal_space}:"
                    f"{params.get('uvMap') or 'UV0'}"
                )
            normal_convention = str(
                self.graph.get("normalConvention")
                or "TangentOpenGLPositiveY"
            )
            if normal_convention not in _NORMAL_CONVENTIONS:
                raise ValueError(
                    "MIKU_NORMAL_CONVENTION_INVALID:"
                    f"{node_id}:{normal_convention}"
                )
            return self._emit(
                f"{node_id}:{normalized}:normal-strength",
                "Vector.NormalStrength",
                value_type="Float3",
                space="Tangent",
                stage="Fragment",
                uniformity="Varying",
                inputs={
                    "Normal": self._input(
                        node,
                        "Color",
                        [0.5, 0.5, 1.0, 1.0],
                        "Float3",
                        semantic="Normal",
                    ),
                    "Strength": self._input(
                        node,
                        "Strength",
                        1.0,
                    ),
                },
                params={
                    "normalSpace": "Tangent",
                    "normalConvention": normal_convention,
                },
                source=source,
            )
        if op == "Texture.Noise":
            if normalized not in {"fac", "factor"}:
                if self.allows_source_mesh_bake:
                    return self._baked_island(
                        source,
                        value_type=value_type,
                        usage="Color" if value_type == "Color" else "Scalar",
                    )
                raise self._portable_mesh_bake_error(
                    source,
                    detail=f"noiseOutput={normalized}",
                )
            if self.strict_fidelity:
                raise ValueError(
                    f"MIKU_APPROXIMATION_FORBIDDEN:Texture.Noise:{node_id}"
                )
            params = (
                node.get("params")
                if isinstance(node.get("params"), Mapping)
                else {}
            )
            dimensions = str(
                params.get("noiseDimensions")
                or params.get("noise_dimensions")
                or "3D"
            ).upper()
            if dimensions != "3D":
                if self.allows_source_mesh_bake:
                    return self._baked_island(
                        source,
                        value_type="Scalar",
                        usage="Scalar",
                    )
                raise self._portable_mesh_bake_error(
                    source,
                    detail=f"noiseDimensions={dimensions}",
                )
            diagnostic_code = "MIKU_NOISE_RUNTIME_APPROXIMATE"
            if not any(
                item.get("code") == diagnostic_code
                and item.get("nodeId") == node_id
                for item in self.diagnostics
            ):
                self.diagnostics.append(
                    {
                        "severity": "warning",
                        "code": diagnostic_code,
                        "translationQuality": "Approximate",
                        "nodeId": node_id,
                        "message": (
                            "Blender Noise Factor is evaluated by the "
                            "clean-room Miku runtime implementation."
                        ),
                    }
                )
            noise_inputs = {
                "Scale": self._input(node, "Scale", 5.0),
                "Detail": self._input(node, "Detail", 2.0),
                "Roughness": self._input(node, "Roughness", 0.5),
                "Lacunarity": self._input(node, "Lacunarity", 2.0),
                "Distortion": self._input(node, "Distortion", 0.0),
            }
            vector_record = _socket_record(
                node,
                "Vector",
                inputs=True,
                value_type="Float3",
            )
            vector_socket = _normalize_socket(
                vector_record.get("id")
                or vector_record.get("name")
                or "Vector"
            )
            if self._incoming.get((node_id, vector_socket)):
                noise_inputs["Vector"] = self._input(
                    node,
                    "Vector",
                    [0.0, 0.0, 0.0],
                    "Float3",
                )
            else:
                noise_inputs["Vector"] = self._implicit(
                    f"{node_id}:implicit-object-coordinate",
                    "Input.TextureCoordinate.Object",
                    value_type="Float3",
                    space="Object",
                )
            return self._emit(
                f"{node_id}:{normalized}",
                "Texture.Noise.Factor",
                value_type="Scalar",
                space="None",
                stage="Fragment",
                uniformity="Varying",
                inputs=noise_inputs,
                params={
                    "dimensions": dimensions,
                    "normalize": bool(params.get("normalize", False)),
                    "translationQuality": "Approximate",
                },
                source=source,
            )
        if op == "Input.Wireframe":
            if self.allows_source_mesh_bake:
                return self._baked_island(
                    source,
                    value_type="Scalar",
                    usage="Scalar",
                )
            raise self._portable_mesh_bake_error(
                source,
                detail="Source Mesh Fidelity is required",
            )
        if op == "Input.Geometry" and normalized == "incoming":
            return self._implicit(
                f"{node_id}:{normalized}",
                "Input.ViewDirection",
                value_type="Float3",
                space="World",
            )
        if op == "Input.Geometry" and normalized == "backfacing":
            front_face = self._emit(
                f"{node_id}:{normalized}:front-face",
                "Input.IsFrontFace",
                value_type="Boolean",
                stage="Fragment",
                uniformity="Varying",
                source=source,
            )
            return self._emit(
                f"{node_id}:{normalized}",
                "Math.OneMinus",
                value_type="Scalar",
                stage="Fragment",
                uniformity="Varying",
                inputs={"A": front_face},
                params={"formula": "1-IsFrontFace"},
                source=source,
            )
        if op == "Input.ViewDirection":
            return self._implicit(
                f"{node_id}:{normalized}",
                "Input.ViewDirection",
                value_type="Float3",
                space=str(output.get("space") or "World"),
            )
        if op == "Input.CameraData":
            camera_ops = {
                "viewvector": ("Input.Camera.ViewVector", "Float3", "View"),
                "viewzdepth": ("Input.Camera.ViewZDepth", "Scalar", "View"),
                "viewdistance": ("Input.Camera.ViewDistance", "Scalar", "None"),
            }
            if normalized not in camera_ops:
                raise ValueError(f"MIKU_CAMERA_OUTPUT_UNSUPPORTED:{socket}")
            camera_op, camera_type, camera_space = camera_ops[normalized]
            return self._emit(
                f"{node_id}:{normalized}",
                camera_op,
                value_type=camera_type,
                space=camera_space,
                stage="Fragment",
                uniformity="Varying",
                source=source,
            )
        if op == "Input.LightPath":
            light_path_ops = {
                "iscameraray": "Input.LightPath.CameraRay",
                "isshadowray": "Input.LightPath.ShadowRay",
            }
            light_path_op = light_path_ops.get(normalized)
            if light_path_op:
                return self._emit(
                    f"{node_id}:{normalized}",
                    light_path_op,
                    value_type="Scalar",
                    stage="Fragment",
                    uniformity="Varying",
                    source=source,
                )
            socket_name = str(output.get("name") or socket)
            raise ValueError(f"MIKU_LIGHT_PATH_UNSUPPORTED:{socket_name}")
        if op == "Input.Time":
            time_ops = {
                "seconds": "Input.Time.Seconds",
                "frame": "Input.Time.Frame",
                "sine": "Input.Time.Sine",
                "cosine": "Input.Time.Cosine",
                # Backward-compatible Input.Time sockets.
                "time": "Input.Time.LegacyTime",
                "sinetime": "Input.Time.LegacySine",
                "cosinetime": "Input.Time.LegacyCosine",
                "deltatime": "Input.Time.LegacyDelta",
                "smoothdelta": "Input.Time.LegacySmoothDelta",
            }
            if normalized not in time_ops:
                raise ValueError(f"MIKU_TIME_OUTPUT_UNSUPPORTED:{socket}")
            return self._emit(
                f"{node_id}:{normalized}",
                time_ops[normalized],
                value_type="Scalar",
                stage="Both",
                uniformity="Uniform",
                params=dict(node.get("params") or {}),
                source=source,
            )
        if op == "Input.Value":
            pseudo_node = {
                "id": node_id,
                "inputs": [
                    {
                        **dict(output),
                        "id": str(output.get("id") or socket),
                        "name": str(output.get("name") or socket),
                    }
                ],
            }
            return self._input(
                pseudo_node,
                str(output.get("id") or socket),
                output.get("default", 0.0),
                "Scalar",
            )
        if op == "Input.Color":
            pseudo_node = {
                "id": node_id,
                "inputs": [
                    {
                        **dict(output),
                        "id": str(output.get("id") or socket),
                        "name": str(output.get("name") or socket),
                    }
                ],
            }
            return self._input(
                pseudo_node,
                str(output.get("id") or socket),
                output.get("default", [0.0, 0.0, 0.0, 1.0]),
                "Color",
                "Color",
            )
        if op in {"Input.Fresnel", "Input.LayerWeight"}:
            blend_socket = "IOR" if op == "Input.Fresnel" else "Blend"
            blend_default = 1.45 if op == "Input.Fresnel" else 0.5
            normal = self._incoming.get((node_id, "normal"))
            normal_id = (
                self.compile(normal)
                if normal
                else self._implicit(
                    f"{node_id}:implicit-normal",
                    "Input.Normal",
                    value_type="Float3",
                    space="World",
                )
            )
            view_id = self._implicit(
                f"{node_id}:implicit-view",
                "Input.ViewDirection",
                value_type="Float3",
                space="World",
            )
            front_face_id = self._implicit(
                f"{node_id}:implicit-front-face",
                "Input.IsFrontFace",
                value_type="Boolean",
                space="None",
            )
            blend_id = self._input(
                node,
                blend_socket,
                blend_default,
                "Scalar",
            )
            if op == "Input.Fresnel":
                result_op = "Math.DielectricFresnel"
                input_name = "IOR"
            elif normalized == "fresnel":
                result_op = "Math.LayerWeightFresnel"
                input_name = "Blend"
            elif normalized == "facing":
                result_op = "Math.LayerWeightFacing"
                input_name = "Blend"
            else:
                raise ValueError(f"MIKU_LAYER_WEIGHT_OUTPUT_UNSUPPORTED:{socket}")
            return self._emit(
                f"{node_id}:{normalized}",
                result_op,
                value_type="Scalar",
                stage="Fragment",
                uniformity="Varying",
                inputs={
                    input_name: blend_id,
                    "Normal": normal_id,
                    "ViewDirection": view_id,
                    "IsFrontFace": front_face_id,
                },
                params={"blenderBlendHalfRule": op == "Input.LayerWeight"},
                source=source,
            )
        if op == "Utility.Reroute":
            incoming = self._incoming.get((node_id, "input"))
            if not incoming:
                raise ValueError(f"MIKU_REROUTE_INPUT_MISSING:{node_id}")
            return self.compile(incoming)
        if op == "Color.Ramp":
            return self._compile_color_ramp(node, socket, source)
        if op == "Color.HueSaturationValue":
            return self._emit(
                f"{node_id}:{normalized}",
                "Color.HueSaturationValue",
                value_type="Color",
                stage="Fragment",
                uniformity="Varying",
                inputs={
                    "Color": self._input(
                        node,
                        "Color",
                        [0.5, 0.5, 0.5, 1.0],
                        "Color",
                        "Color",
                    ),
                    "Hue": self._input(node, "Hue", 0.5),
                    "Saturation": self._input(
                        node, "Saturation", 1.0
                    ),
                    "Value": self._input(node, "Value", 1.0),
                    "Factor": self._input(
                        node, "Fac", 1.0
                    ),
                },
                params={
                    "hueCenter": 0.5,
                    "hueWrap": "Fraction",
                    "saturationClamp": [0.0, 1.0],
                },
                source=source,
            )
        if op == "Vector.Displacement":
            params = (
                node.get("params")
                if isinstance(node.get("params"), Mapping)
                else {}
            )
            if str(params.get("space") or "OBJECT").upper() != "OBJECT":
                raise ValueError(
                    "MIKU_DISPLACEMENT_SPACE_UNSUPPORTED:"
                    f"{node_id}:{params.get('space') or '<missing>'}"
                )
            midlevel_record = _socket_record(
                node,
                "Midlevel",
                inputs=True,
                value_type="Scalar",
            )
            scale_record = _socket_record(
                node,
                "Scale",
                inputs=True,
                value_type="Scalar",
            )
            if self._incoming.get(
                (
                    node_id,
                    _normalize_socket(
                        midlevel_record.get("id")
                        or midlevel_record.get("name")
                        or "Midlevel"
                    ),
                )
            ):
                raise ValueError(
                    "MIKU_DISPLACEMENT_MIDLEVEL_DYNAMIC_UNSUPPORTED:"
                    f"{node_id}"
                )
            if self._incoming.get(
                (
                    node_id,
                    _normalize_socket(
                        scale_record.get("id")
                        or scale_record.get("name")
                        or "Scale"
                    ),
                )
            ):
                raise ValueError(
                    "MIKU_DISPLACEMENT_SCALE_DYNAMIC_UNSUPPORTED:"
                    f"{node_id}"
                )
            midlevel = float(
                midlevel_record.get("default", 0.5)
            )
            scale = float(scale_record.get("default", 1.0))
            if semantic == "Normal":
                return self._emit(
                    f"{node_id}:{normalized}:normal-from-height",
                    "Vector.NormalFromHeight",
                    value_type="Float3",
                    space="Tangent",
                    stage="Fragment",
                    uniformity="Varying",
                    inputs={
                        "Height": self._input(
                            node,
                            "Height",
                            0.0,
                            "Scalar",
                            semantic="Height",
                        ),
                        "Midlevel": self._constant(
                            f"{node_id}:normal-midlevel",
                            midlevel,
                        ),
                        "Strength": self._constant(
                            f"{node_id}:normal-strength",
                            scale,
                        ),
                    },
                    params={
                        "normalSpace": "Tangent",
                        "derivativeMode": "FiniteDifference",
                    },
                    source=source,
                )
            return self._emit(
                f"{node_id}:{normalized}:vertex-position",
                "Vector.Displacement",
                value_type="Float3",
                space="Object",
                stage="Vertex",
                uniformity="Varying",
                inputs={
                    "Height": self._input(
                        node,
                        "Height",
                        0.0,
                        "Scalar",
                        semantic="VertexHeight",
                    ),
                    "Position": self._implicit(
                        f"{node_id}:object-position",
                        "Input.Position.Object",
                        value_type="Float3",
                        space="Object",
                        stage="Vertex",
                    ),
                    "Normal": self._implicit(
                        f"{node_id}:object-normal",
                        "Input.Normal.Object",
                        value_type="Float3",
                        space="Object",
                        stage="Vertex",
                    ),
                },
                params={
                    "midlevel": midlevel,
                    "scale": scale,
                    "midlevelReference":
                        "_MIKU_HeightMidlevel",
                    "scaleReference": "_MIKU_HeightScale",
                    "translationQuality": "Equivalent",
                },
                source=source,
            )
        if op in {"Vector.DisplacementBump", "Vector.Bump"}:
            if op == "Vector.Bump":
                if self.strict_fidelity:
                    raise ValueError(
                        f"MIKU_APPROXIMATION_FORBIDDEN:Vector.Bump:{node_id}"
                    )
                diagnostic_code = "MIKU_BUMP_KERNEL_APPROXIMATE"
                if not any(
                    item.get("code") == diagnostic_code
                    and item.get("nodeId") == node_id
                    for item in self.diagnostics
                ):
                    self.diagnostics.append(
                        {
                            "severity": "warning",
                            "code": diagnostic_code,
                            "translationQuality": "Approximate",
                            "nodeId": node_id,
                            "message": (
                                "Blender Bump is represented with Shader Graph "
                                "Normal From Height; derivative kernels may "
                                "differ slightly."
                            ),
                        }
                    )
            try:
                normal_record = _socket_record(
                    node,
                    "Normal",
                    inputs=True,
                    value_type="Float3",
                )
                normal_socket = str(
                    normal_record.get("id")
                    or normal_record.get("name")
                    or "Normal"
                )
                normal_incoming = self._incoming.get(
                    (node_id, _normalize_socket(normal_socket))
                )
                normal_default = normal_record.get(
                    "default",
                    [0.0, 0.0, 1.0],
                )
                neutral_unlinked = (
                    normal_incoming is None
                    and isinstance(normal_default, (list, tuple))
                    and len(normal_default) >= 3
                    and all(
                        abs(float(normal_default[index])) <= 1.0e-8
                        for index in range(3)
                    )
                )
                base_normal = (
                    self._constant(
                        f"{node_id}:neutral-base-normal",
                        [0.0, 0.0, 1.0],
                        "Float3",
                    )
                    if neutral_unlinked
                    else self._input(
                        node,
                        "Normal",
                        [0.0, 0.0, 1.0],
                        "Float3",
                        "Normal",
                        semantic="Normal",
                    )
                )
            except ValueError:
                if self.allows_source_mesh_bake:
                    return self._baked_island(
                        source,
                        value_type="Float3",
                        usage="Normal",
                    )
                raise
            strength = self._input(
                node,
                "Strength",
                1.0,
            )
            if op == "Vector.Bump":
                distance = self._input(node, "Distance", 1.0)
                strength = self._emit(
                    f"{node_id}:bump-strength-distance",
                    "Math.Multiply",
                    value_type="Scalar",
                    stage="Fragment",
                    uniformity="Varying",
                    inputs={"A": strength, "B": distance},
                )
                params = (
                    node.get("params")
                    if isinstance(node.get("params"), Mapping)
                    else {}
                )
                if bool(params.get("invert", False)):
                    strength = self._emit(
                        f"{node_id}:bump-invert",
                        "Math.Multiply",
                        value_type="Scalar",
                        stage="Fragment",
                        uniformity="Varying",
                        inputs={
                            "A": strength,
                            "B": self._constant(
                                f"{node_id}:bump-negative-one",
                                -1.0,
                            ),
                        },
                    )
            else:
                params = (
                    node.get("params")
                    if isinstance(node.get("params"), Mapping)
                    else {}
                )
                strength = self._constant(
                    f"{node_id}:displacement-bump-scale",
                    float(params.get("scale", 1.0)),
                )
            normal_from_height = self._emit(
                f"{node_id}:{normalized}:normal-from-height",
                "Vector.NormalFromHeight",
                value_type="Float3",
                space="Tangent",
                stage="Fragment",
                uniformity="Varying",
                inputs={
                    "Height": self._input(
                        node,
                        "Height",
                        0.0,
                        "Scalar",
                        "Scalar",
                        semantic="Height",
                    ),
                    "Midlevel": (
                        self._constant(
                            f"{node_id}:bump-midlevel",
                            (
                                float(
                                    (
                                        node.get("params")
                                        if isinstance(
                                            node.get("params"),
                                            Mapping,
                                        )
                                        else {}
                                    ).get("midlevel", 0.0)
                                )
                                if op == "Vector.DisplacementBump"
                                else 0.0
                            ),
                        )
                    ),
                    "Strength": strength,
                },
                params={
                    "normalSpace": "Tangent",
                    "derivativeMode": "FiniteDifference",
                    **(
                        {
                            "bumpStrengthReference":
                                "_MIKU_BumpStrength",
                            "bumpDistanceReference":
                                "_MIKU_BumpDistance",
                        }
                        if op in {
                            "Vector.Bump",
                            "Vector.DisplacementBump",
                        }
                        else {}
                    ),
                },
                source=source,
            )
            return self._emit(
                f"{node_id}:{normalized}:normal-blend",
                "Vector.NormalBlend",
                value_type="Float3",
                space="Tangent",
                stage="Fragment",
                uniformity="Varying",
                inputs={
                    "Base": base_normal,
                    "Detail": normal_from_height,
                },
                params={"blendMode": "Reoriented"},
                source=source,
            )

        params = node.get("params") if isinstance(node.get("params"), Mapping) else {}
        operation = str(params.get("operation") or "").upper()
        math_ops = {
            "ADD": ("Math.Add", ("Value", "Value_001")),
            "SUBTRACT": ("Math.Subtract", ("Value", "Value_001")),
            "MULTIPLY": ("Math.Multiply", ("Value", "Value_001")),
            "DIVIDE": ("Math.Divide", ("Value", "Value_001")),
            "POWER": ("Math.Power", ("Value", "Value_001")),
            "MINIMUM": ("Math.Minimum", ("Value", "Value_001")),
            "MAXIMUM": ("Math.Maximum", ("Value", "Value_001")),
            "GREATER_THAN": ("Math.GreaterThan", ("Value", "Value_001")),
            "LESS_THAN": ("Math.LessThan", ("Value", "Value_001")),
            "MODULO": ("Math.Modulo", ("Value", "Value_001")),
            "LOGARITHM": ("Math.Logarithm", ("Value", "Value_001")),
            "MULTIPLY_ADD": (
                "Math.MultiplyAdd",
                ("Value", "Value_001", "Value_002"),
            ),
            "ABSOLUTE": ("Math.Absolute", ("Value",)),
            "SINE": ("Math.Sine", ("Value",)),
            "COSINE": ("Math.Cosine", ("Value",)),
        }
        vector_ops = {
            "ADD": (
                "Math.Add",
                (("Vector", "Float3"), ("Vector_001", "Float3")),
                "Float3",
            ),
            "SUBTRACT": (
                "Math.Subtract",
                (("Vector", "Float3"), ("Vector_001", "Float3")),
                "Float3",
            ),
            "MULTIPLY": (
                "Math.Multiply",
                (("Vector", "Float3"), ("Vector_001", "Float3")),
                "Float3",
            ),
            "DIVIDE": (
                "Math.Divide",
                (("Vector", "Float3"), ("Vector_001", "Float3")),
                "Float3",
            ),
            "DOT_PRODUCT": (
                "Math.Dot",
                (("Vector", "Float3"), ("Vector_001", "Float3")),
                "Scalar",
            ),
            "NORMALIZE": (
                "Math.Normalize",
                (("Vector", "Float3"),),
                "Float3",
            ),
            "LENGTH": (
                "Math.Length",
                (("Vector", "Float3"),),
                "Scalar",
            ),
            "ABSOLUTE": (
                "Math.Absolute",
                (("Vector", "Float3"),),
                "Float3",
            ),
            "SCALE": (
                "Math.Multiply",
                (("Vector", "Float3"), ("Scale", "Scalar")),
                "Float3",
            ),
        }
        if op == "Math" and operation in math_ops:
            result_op, sockets = math_ops[operation]
            compiled = {
                chr(ord("A") + index): self._input(
                    node,
                    name,
                    0.0,
                    semantic=semantic,
                )
                for index, name in enumerate(sockets)
            }
            return self._emit(
                f"{node_id}:{normalized}",
                result_op,
                value_type="Scalar",
                stage="Fragment",
                uniformity="Varying",
                inputs=compiled,
                source=source,
            )
        direct_math_ops = {
            "Math.Add",
            "Math.Subtract",
            "Math.Multiply",
            "Math.Divide",
            "Math.Power",
            "Math.Minimum",
            "Math.Maximum",
            "Math.Absolute",
            "Math.Sine",
            "Math.Cosine",
        }
        if op in direct_math_ops:
            unary = op in {
                "Math.Absolute",
                "Math.Sine",
                "Math.Cosine",
            }
            result_type = value_type
            input_a_type = _value_type(
                _socket_record(node, "A", inputs=True),
                result_type,
            )
            compiled = {
                "A": self._input(
                    node,
                    "A",
                    0.0,
                    input_a_type,
                    "Color" if input_a_type == "Color" else None,
                    semantic=semantic,
                )
            }
            if not unary:
                input_b_type = _value_type(
                    _socket_record(node, "B", inputs=True),
                    "Scalar",
                )
                compiled["B"] = self._input(
                    node,
                    "B",
                    0.0,
                    input_b_type,
                    "Color" if input_b_type == "Color" else None,
                    semantic=semantic,
                )
            return self._emit(
                f"{node_id}:{normalized}",
                op,
                value_type=result_type,
                stage="Fragment",
                uniformity="Varying",
                inputs=compiled,
                source=source,
            )
        if op == "VectorMath" and operation in vector_ops:
            result_op, sockets, result_type = vector_ops[operation]
            compiled = {
                chr(ord("A") + index): self._input(
                    node,
                    name,
                    [0.0, 0.0, 0.0] if input_type == "Float3" else 1.0,
                    input_type,
                )
                for index, (name, input_type) in enumerate(sockets)
            }
            return self._emit(
                f"{node_id}:{normalized}",
                result_op,
                value_type=result_type,
                stage="Fragment",
                uniformity="Varying",
                inputs=compiled,
                source=source,
            )
        if op in {"Math.Mix", "Color.Mix"}:
            blend_type = str(params.get("blend_type") or "MIX").upper()
            if blend_type not in {
                "MIX",
                "ADD",
                "SUBTRACT",
                "OVERLAY",
                "MULTIPLY",
                "DARKEN",
                "SCREEN",
                "DIFFERENCE",
            }:
                raise ValueError(
                    f"MIKU_RUNTIME_MIX_UNSUPPORTED:{node_id}:"
                    f"blendType={blend_type}"
                )
            semantic = str(params.get("semantic") or semantic or "")
            mixed_type = "Float3" if semantic == "Normal" else value_type
            usage = (
                "Normal"
                if semantic == "Normal"
                else ("Color" if mixed_type == "Color" else "Scalar")
            )
            a_socket = (
                "A"
                if _socket_record(node, "A", inputs=True)
                else "Color1"
            )
            b_socket = (
                "B"
                if _socket_record(node, "B", inputs=True)
                else "Color2"
            )
            factor_socket = (
                "Factor"
                if _socket_record(node, "Factor", inputs=True)
                else "Fac"
            )
            result_inputs = {
                "A": self._input(
                    node,
                    a_socket,
                    [0.0, 0.0, 1.0]
                    if semantic == "Normal"
                    else 0.0,
                    mixed_type,
                    usage,
                    semantic=semantic,
                ),
                "B": self._input(
                    node,
                    b_socket,
                    [0.0, 0.0, 1.0]
                    if semantic == "Normal"
                    else 0.0,
                    mixed_type,
                    usage,
                    semantic=semantic,
                ),
                "T": self._input(node, factor_socket, 0.5),
            }
            arithmetic_ops = {
                "ADD": "Math.Add",
                "SUBTRACT": "Math.Subtract",
                "MULTIPLY": "Math.Multiply",
                "DARKEN": "Math.Minimum",
                "DIFFERENCE": "Math.Absolute",
            }
            if blend_type in arithmetic_ops:
                blended_inputs = {
                    "A": result_inputs["A"],
                    "B": result_inputs["B"],
                }
                blended = self._emit(
                    f"{node_id}:{normalized}:{blend_type.lower()}",
                    (
                        "Math.Subtract"
                        if blend_type == "DIFFERENCE"
                        else arithmetic_ops[blend_type]
                    ),
                    value_type=mixed_type,
                    stage="Fragment",
                    uniformity="Varying",
                    inputs=blended_inputs,
                    source=source,
                )
                if blend_type == "DIFFERENCE":
                    blended = self._emit(
                        f"{node_id}:{normalized}:difference-absolute",
                        "Math.Absolute",
                        value_type=mixed_type,
                        stage="Fragment",
                        uniformity="Varying",
                        inputs={"A": blended},
                        source=source,
                    )
                return self._emit(
                    f"{node_id}:{normalized}:{blend_type.lower()}-factor",
                    "Math.Lerp",
                    value_type=mixed_type,
                    stage="Fragment",
                    uniformity="Varying",
                    inputs={
                        "A": result_inputs["A"],
                        "B": blended,
                        "T": result_inputs["T"],
                    },
                    params={
                        "blendType": blend_type,
                    },
                    source=source,
                )
            if blend_type == "SCREEN":
                one_minus_a = self._emit(
                    f"{node_id}:{normalized}:screen-one-minus-a",
                    "Math.OneMinus",
                    value_type=mixed_type,
                    inputs={"A": result_inputs["A"]},
                    source=source,
                )
                one_minus_b = self._emit(
                    f"{node_id}:{normalized}:screen-one-minus-b",
                    "Math.OneMinus",
                    value_type=mixed_type,
                    inputs={"A": result_inputs["B"]},
                    source=source,
                )
                product = self._emit(
                    f"{node_id}:{normalized}:screen-product",
                    "Math.Multiply",
                    value_type=mixed_type,
                    inputs={"A": one_minus_a, "B": one_minus_b},
                    source=source,
                )
                screened = self._emit(
                    f"{node_id}:{normalized}:screen",
                    "Math.OneMinus",
                    value_type=mixed_type,
                    inputs={"A": product},
                    source=source,
                )
                return self._emit(
                    f"{node_id}:{normalized}:screen-factor",
                    "Math.Lerp",
                    value_type=mixed_type,
                    inputs={
                        "A": result_inputs["A"],
                        "B": screened,
                        "T": result_inputs["T"],
                    },
                    params={"blendType": "SCREEN"},
                    source=source,
                )
            result_op = (
                "Math.Lerp"
                if blend_type == "MIX"
                else "Color.Overlay"
            )
            result = self._emit(
                f"{node_id}:{normalized}",
                result_op,
                value_type=mixed_type,
                stage="Fragment",
                uniformity="Varying",
                inputs=result_inputs,
                params={
                    "blendType": blend_type,
                    "formula": (
                        "lerp(A, A<0.5 ? 2*A*B : "
                        "1-2*(1-A)*(1-B), T)"
                        if blend_type == "OVERLAY"
                        else "lerp(A,B,T)"
                    ),
                },
                source=source,
            )
            if semantic == "Normal":
                return self._emit(
                    f"{node_id}:{normalized}:normalized",
                    "Math.Normalize",
                    value_type="Float3",
                    space="Tangent",
                    stage="Fragment",
                    uniformity="Varying",
                    inputs={"A": result},
                    source=source,
                )
            return result
        raise ValueError(f"MIKU_RUNTIME_INPUT_UNSUPPORTED:{op}:{socket}")
def normalize_workflow_kind(value: Any) -> str:
    """Return one concrete Miku workflow or fail instead of guessing."""

    if isinstance(value, Mapping):
        value = value.get("kind")
    normalized = str(value or "standard_pbr").strip().lower()
    if normalized in RETIRED_WORKFLOW_KINDS:
        raise ValueError(f"MIKU_WORKFLOW_RETIRED:{normalized}")
    if normalized not in WORKFLOW_KINDS:
        raise ValueError(f"MIKU_WORKFLOW_UNSUPPORTED:{normalized}")
    return normalized


def normalize_workflow_part(value: Any) -> str:
    normalized = str(value or "Body").strip().title()
    if normalized not in {
        "Body",
        "Skin",
        "Hair",
        "Face",
        "Eye",
        "Mouth",
        "Overlay",
        "Effect",
        "Hairshadow",
    }:
        raise ValueError(f"MIKU_WORKFLOW_PART_UNSUPPORTED:{normalized}")
    return "HairShadow" if normalized == "Hairshadow" else normalized


def _node_op(node: Mapping[str, Any]) -> str:
    return str(node.get("op") or node.get("type") or "")


def _source_mesh_closure_fallback_graph(
    graph: Mapping[str, Any],
) -> dict[str, Any]:
    """Return a closure-valid graph used only to describe a full PBR bake.

    The original snapshot remains the bake source.  This substitute prevents
    a malformed legacy Mix/Add closure from blocking an explicitly requested
    source-mesh bake before the GPL worker can evaluate the real Blender tree.
    """

    nodes = [
        node
        for node in graph.get("nodes", []) or []
        if isinstance(node, Mapping)
    ]
    outputs = [
        node for node in nodes if _node_op(node) == "Output.Material"
    ]
    outputs.sort(
        key=lambda node: (
            0
            if str((node.get("params") or {}).get("target") or "ALL").upper()
            == "EEVEE"
            else 1,
            str(node.get("id") or ""),
        )
    )
    leaves = [
        node
        for node in nodes
        if _node_op(node) in _CLOSURES
        and _node_op(node) not in {"Shader.Mix", "Shader.Add"}
    ]
    leaves.sort(key=lambda node: str(node.get("id") or ""))
    if not outputs or not leaves:
        raise ValueError("MIKU_SOURCE_MESH_FALLBACK_CLOSURE_MISSING")
    output = outputs[0]
    leaf = leaves[0]

    def socket_id(
        node: Mapping[str, Any],
        key: str,
        preferred: set[str],
    ) -> str:
        sockets = [
            item
            for item in node.get(key, []) or []
            if isinstance(item, Mapping)
            and bool(item.get("enabled", True))
            and not bool(item.get("isUnavailable", False))
        ]
        matching = [
            item
            for item in sockets
            if str(
                item.get("valueType") or item.get("type") or ""
            ).upper()
            in preferred
        ]
        selected = (matching or sockets)[0]
        return str(selected.get("id") or selected.get("name") or "")

    source_socket = socket_id(
        leaf,
        "outputs",
        {"CLOSURE", "SHADER"},
    )
    target_socket = socket_id(
        output,
        "inputs",
        {"CLOSURE", "SHADER"},
    )
    output_id = str(output.get("id") or "")
    edges = [
        dict(edge)
        for edge in graph.get("edges", []) or []
        if isinstance(edge, Mapping)
        and not (
            str((edge.get("to") or {}).get("node") or "") == output_id
            and _normalize_socket(
                (edge.get("to") or {}).get("socket")
            )
            == _normalize_socket(target_socket)
        )
    ]
    edges.append(
        {
            "from": {
                "node": str(leaf.get("id") or ""),
                "socket": source_socket,
            },
            "to": {"node": output_id, "socket": target_socket},
        }
    )
    fallback = dict(graph)
    fallback["edges"] = edges
    return fallback


def _source_ref(node: Mapping[str, Any], socket: str = "") -> dict[str, str]:
    source = node.get("source") if isinstance(node.get("source"), Mapping) else {}
    return {
        "nodeId": str(node.get("id") or ""),
        "socketId": socket,
        "sourceId": str(source.get("stableId") or source.get("nodeId") or node.get("id") or ""),
    }


def _region_digest(material_key: str, node_ids: Iterable[str], semantics: Iterable[str]) -> str:
    payload = {"materialKey": material_key, "nodeIds": sorted(set(node_ids)), "semantics": sorted(set(semantics))}
    return canonical_hash(payload)


def _reachable_nodes(graph: Mapping[str, Any]) -> set[str]:
    nodes = {str(node.get("id")): node for node in graph.get("nodes", []) or [] if node.get("id")}
    edges = list(graph.get("edges", []) or [])
    outputs = [
        node
        for node in nodes.values()
        if _node_op(node) in {"Output.Material", "ShaderNodeOutputMaterial"}
    ]
    if not outputs:
        return set(nodes)
    output = min(
        outputs,
        key=lambda node: (
            not bool((node.get("params") or {}).get("isActiveOutput")),
            str((node.get("params") or {}).get("target") or "ALL")
            not in {"EEVEE", "ALL"},
            str((node.get("params") or {}).get("target") or "ALL") != "EEVEE",
            str(node.get("id") or ""),
        ),
    )
    incoming: dict[str, list[str]] = defaultdict(list)
    for edge in edges:
        source = edge.get("from") or {}
        target = edge.get("to") or {}
        if (
            source.get("node")
            and target.get("node")
            and (
                str(target.get("node")) != str(output.get("id"))
                or _normalize_socket(target.get("socket")) == "surface"
            )
        ):
            incoming[str(target["node"])].append(str(source["node"]))
    result: set[str] = set()
    stack = [str(output.get("id"))]
    while stack:
        node_id = stack.pop()
        if node_id in result:
            continue
        result.add(node_id)
        stack.extend(incoming.get(node_id, []))
    return result


def build_source_map(graph: Mapping[str, Any], *, source_blend_id: str = "", material_key: str = "") -> dict[str, Any]:
    nodes = {str(node.get("id")): node for node in graph.get("nodes", []) or [] if node.get("id")}
    reachable = _reachable_nodes(graph)
    regions: dict[str, list[dict[str, str]]] = defaultdict(list)
    for node_id in sorted(reachable):
        node = nodes[node_id]
        op = _node_op(node)
        semantic = _CLOSURES.get(op, "OpaqueSemanticRegion")
        region_id = stable_uuid("miku-source-region", f"{material_key}:{semantic}:{node_id}")
        regions[region_id].append(_source_ref(node))
    bindings = []
    for parameter in graph.get("parameters", []) or []:
        if not isinstance(parameter, Mapping):
            continue
        bindings.append(
            {
                "parameterId": str(parameter.get("id") or stable_uuid("miku-parameter", f"{material_key}:{parameter.get('name','')}")),
                "nodeId": str(parameter.get("nodeId") or ""),
                "socketId": str(parameter.get("socketId") or ""),
                "writeKind": str(parameter.get("writeKind") or "socket-default"),
            }
        )
    expression_compiler = _RuntimeExpressionCompiler(graph, nodes, material_key)
    semantic_slots = ((graph.get("standardPbrSemantic") or {}).get("slots") or {})
    if isinstance(semantic_slots, Mapping):
        for semantic in sorted(semantic_slots):
            slot = semantic_slots.get(semantic)
            source = (
                slot.get("source")
                if isinstance(slot, Mapping)
                and isinstance(slot.get("source"), Mapping)
                else None
            )
            if source and expression_compiler.depends_on_runtime(source):
                try:
                    expression_compiler.compile(source, semantic=semantic)
                except ValueError:
                    # MaterialIR carries the authoritative structured
                    # unsupported diagnostic. SourceMap records only bindings
                    # that were safely represented.
                    pass
    expression_bindings = [
        {
            "expressionId": str(expression["id"]),
            "nodeId": str((expression.get("source") or {}).get("nodeId") or ""),
            "socketId": str(
                (expression.get("source") or {}).get("socketId") or ""
            ),
        }
        for expression in expression_compiler.expressions
        if isinstance(expression.get("source"), Mapping)
    ]
    payload = {
        "source": {
            "sourceBlendId": source_blend_id,
            "materialKey": material_key,
            "sourceLocator": str(graph.get("sourceLocator") or ""),
            "sourceContentHash": str(graph.get("sourceContentHash") or ""),
        },
        "materialBindings": [{"materialKey": material_key, "nodeCount": len(reachable)}],
        "regionBindings": [{"regionId": key, "sources": value} for key, value in sorted(regions.items())],
        "parameterBindings": bindings,
        "expressionBindings": sorted(
            expression_bindings,
            key=lambda item: (
                item["expressionId"],
                item["nodeId"],
                item["socketId"],
            ),
        ),
    }
    payload["sourceMapHash"] = canonical_hash(payload)
    return make_document("miku-blender-source-map-1.0", payload)


def _parameter_records(graph: Mapping[str, Any], material_key: str) -> list[dict[str, Any]]:
    records = []
    for item in graph.get("parameters", []) or []:
        if not isinstance(item, Mapping):
            continue
        parameter_id = str(item.get("id") or stable_uuid("miku-parameter", f"{material_key}:{item.get('name','')}"))
        semantic = str(item.get("semantic") or item.get("name") or "Value")
        records.append(
            {
                "id": parameter_id,
                "semantic": semantic,
                "displayName": str(item.get("displayName") or item.get("name") or semantic),
                "referenceName": f"_MIKU_{semantic}_{hashlib.sha256(parameter_id.encode()).hexdigest()[:20]}",
                "default": item.get("default"),
                "mutability": str(item.get("mutability") or "Live"),
                "scope": str(item.get("scope") or "PerMaterial"),
                "updateAction": str(item.get("updateAction") or "None"),
                "runtimeEditable": bool(item.get("runtimeEditable", False)),
            }
        )
    return sorted(records, key=lambda item: item["id"])


def _bind_closure_value_expressions(
    closure_graph: Mapping[str, Any],
    weighted_closures: Mapping[str, Any],
    compiler: _RuntimeExpressionCompiler,
    authoritative_channel_expressions: Mapping[str, str] | None = None,
) -> list[dict[str, Any]]:
    """Resolve linked closure leaves through the typed value-expression DAG."""

    diagnostics: list[dict[str, Any]] = []
    authoritative_channel_expressions = dict(
        authoritative_channel_expressions or {}
    )
    diagnostic_keys: set[tuple[str, str, str, str]] = set()
    weighted_terms = [
        item
        for item in weighted_closures.get("terms", []) or []
        if isinstance(item, Mapping)
    ]
    scattering_terms = [
        item
        for item in weighted_terms
        if str(item.get("domain") or "")
        in {"SurfaceScattering", "SurfaceTransmission"}
    ]
    requires_closure_composite = (
        any(
            str(item.get("domain") or "")
            in {"TransparentPassThrough", "Emission"}
            for item in weighted_terms
        )
        or len(scattering_terms) > 1
    )
    emission_mask_sources: set[tuple[str, str]] = set()

    def collect_weight_sources(expression: Any) -> None:
        if not isinstance(expression, Mapping):
            return
        source = expression.get("source")
        if isinstance(source, Mapping):
            node_id = str(source.get("nodeId") or source.get("node") or "")
            socket_id = str(
                source.get("socketId") or source.get("socket") or ""
            )
            if node_id and socket_id:
                emission_mask_sources.add((node_id, socket_id))
        for key in ("input", "inputs"):
            child = expression.get(key)
            if isinstance(child, list):
                for item in child:
                    collect_weight_sources(item)
            else:
                collect_weight_sources(child)

    def discover_emission_masks(closure: Any) -> None:
        if not isinstance(closure, Mapping):
            return
        first = closure.get("first")
        second = closure.get("second")
        if str(closure.get("kind") or "") == "Mix":
            domains = {
                str((first or {}).get("domain") or ""),
                str((second or {}).get("domain") or ""),
            }
            if "Emission" in domains and bool(
                domains
                & {
                    "SurfaceScattering",
                    "SurfaceTransmission",
                }
            ):
                collect_weight_sources(closure.get("factor"))
        discover_emission_masks(first)
        discover_emission_masks(second)

    discover_emission_masks(closure_graph.get("root"))

    def compile_source(
        record: dict[str, Any],
        source: Mapping[str, Any],
        *,
        code: str,
        usage: str | None = None,
        semantic: str = "",
        bake_static: bool = False,
    ) -> None:
        node_id = str(source.get("nodeId") or source.get("node") or "")
        socket_id = str(
            source.get("socketId") or source.get("socket") or ""
        )
        if not node_id or not socket_id:
            record["requiresBake"] = True
            return
        endpoint = {"node": node_id, "socket": socket_id}
        if compiler.requires_static_bake(endpoint):
            reusable = compiler.can_reusable_bake(endpoint)
            if not reusable and not compiler.allows_source_mesh_bake:
                record["requiresBake"] = True
                error = compiler._portable_mesh_bake_error(endpoint)
                message = str(error)
                diagnostic_code = message.split(":", 1)[0]
                diagnostic_key = (
                    diagnostic_code,
                    node_id,
                    socket_id,
                    message,
                )
                if diagnostic_key not in diagnostic_keys:
                    diagnostic_keys.add(diagnostic_key)
                    diagnostics.append(
                        {
                            "severity": "error",
                            "code": diagnostic_code,
                            "translationQuality": "Unsupported",
                            "nodeId": node_id,
                            "socketId": socket_id,
                            "message": message,
                        }
                    )
                return
            if not bake_static and not reusable:
                record["requiresBake"] = True
                return
            raw_value_type = str(record.get("valueType") or "Scalar")
            value_type = _VALUE_TYPES.get(
                raw_value_type.upper(),
                raw_value_type,
            )
            resolved_usage = usage or (
                "Color"
                if value_type in {"Color", "Float3"}
                else "Scalar"
            )
            record["expressionId"] = compiler.compile_baked(
                endpoint,
                value_type=value_type,
                usage=resolved_usage,
                coordinate_domain=(
                    compiler.static_dependency_domain(endpoint)
                    if reusable
                    else "MeshSurface"
                ),
                mesh_binding_required=not reusable,
            )
            record.pop("requiresBake", None)
            return
        try:
            record["expressionId"] = compiler.compile(
                endpoint,
                semantic=semantic,
            )
        except ValueError as exc:
            record["requiresBake"] = True
            message = str(exc)
            diagnostic_key = (code, node_id, socket_id, message)
            if diagnostic_key not in diagnostic_keys:
                diagnostic_keys.add(diagnostic_key)
                diagnostics.append(
                    {
                        "severity": "error",
                        "code": code,
                        "translationQuality": "Unsupported",
                        "nodeId": node_id,
                        "socketId": socket_id,
                        "message": message,
                    }
                )

    def bind_weight(expression: Any) -> None:
        if not isinstance(expression, dict):
            return
        kind = str(expression.get("kind") or "")
        if kind in {"Multiply", "Add"}:
            for item in expression.get("inputs", []) or []:
                bind_weight(item)
            return
        if kind in {"OneMinus", "Clamp", "ImplicitConversion"}:
            bind_weight(expression.get("input"))
            return
        if kind in {"Constant", "ConstantValue"}:
            return
        source = expression.get("source")
        if isinstance(source, Mapping):
            source_key = (
                str(source.get("nodeId") or source.get("node") or ""),
                str(
                    source.get("socketId")
                    or source.get("socket")
                    or ""
                ),
            )
            compile_source(
                expression,
                source,
                code="MIKU_CLOSURE_WEIGHT_EXPRESSION_UNSUPPORTED",
                semantic=(
                    "EmissionMask"
                    if source_key in emission_mask_sources
                    else ""
                ),
                bake_static=True,
            )
        else:
            expression["requiresBake"] = True

    def bind_parameters(parameters: Any) -> None:
        if not isinstance(parameters, dict):
            return
        for parameter_name, parameter in parameters.items():
            if (
                not isinstance(parameter, dict)
                or parameter.get("kind") != "ValueExpression"
                or parameter.get("expressionId")
            ):
                continue
            source = parameter.get("source")
            if isinstance(source, Mapping):
                normalized_parameter = _normalize_socket(parameter_name)
                parameter_semantic = {
                    "basecolor": "BaseColor",
                    "metallic": "Metalness",
                    "metalness": "Metalness",
                    "roughness": "Roughness",
                    "normal": "Normal",
                    "emissioncolor": "Emission",
                    "emission": "Emission",
                    "alpha": "Alpha",
                }.get(normalized_parameter, "")
                authoritative_expression = (
                    authoritative_channel_expressions.get(
                        parameter_semantic,
                        "",
                    )
                    if parameter_semantic
                    else ""
                )
                if (
                    authoritative_expression
                    and not requires_closure_composite
                ):
                    parameter["expressionId"] = authoritative_expression
                    parameter.pop("requiresBake", None)
                    continue
                compile_source(
                    parameter,
                    source,
                    code="MIKU_CLOSURE_PARAMETER_EXPRESSION_UNSUPPORTED",
                    usage=(
                        "Normal"
                        if normalized_parameter == "normal"
                        else None
                    ),
                    semantic=parameter_semantic,
                    # Closure parameters are authoritative consumers too. In
                    # Source Mesh mode every runtime-independent unsupported
                    # endpoint becomes a traceable expression-island sample,
                    # including Normal/Bump inputs. Portable modes retain the
                    # explicit requiresBake proof and fail before export.
                    bake_static=compiler.allows_source_mesh_bake,
                )
            else:
                parameter["requiresBake"] = True

    def bind_closure(closure: Any) -> None:
        if not isinstance(closure, dict):
            return
        bind_weight(closure.get("factor"))
        bind_weight(closure.get("localWeight"))
        bind_parameters(closure.get("parameters"))
        bind_closure(closure.get("first"))
        bind_closure(closure.get("second"))

    bind_closure(closure_graph.get("root"))
    for term in weighted_closures.get("terms", []) or []:
        if not isinstance(term, dict):
            continue
        bind_weight(term.get("finalWeight"))
        bind_parameters(term.get("parameters"))
    return diagnostics


def _finalize_static_image_bindings(
    expressions: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    """Assign one deterministic property to every explicitly packed image."""

    groups: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for expression in expressions:
        if str(expression.get("op") or "") != "Texture.SampleImage2D":
            continue
        params = (
            expression.get("params")
            if isinstance(expression.get("params"), dict)
            else {}
        )
        resource_id = str(params.get("resourceId") or "")
        if resource_id:
            groups[resource_id].append(expression)

    diagnostics: list[dict[str, Any]] = []
    for resource_id, samples in sorted(groups.items()):
        bindings = {
            (
                str((item.get("params") or {}).get("semantic") or ""),
                str((item.get("params") or {}).get("channel") or ""),
            )
            for item in samples
        }
        bindings.discard(("", ""))
        if len(bindings) <= 1:
            continue
        invalid = [
            item
            for item in samples
            if str((item.get("params") or {}).get("usage") or "") != "Scalar"
            or str((item.get("params") or {}).get("colorSpace") or "")
            != "Linear"
            or str((item.get("params") or {}).get("semantic") or "")
            not in _SCALAR_IMAGE_SEMANTICS
        ]
        if invalid:
            diagnostics.append(
                {
                    "severity": "error",
                    "code": "MIKU_PACKED_TEXTURE_COLOR_SPACE_CONFLICT",
                    "translationQuality": "Unsupported",
                    "resourceId": resource_id,
                    "message": (
                        "One physical image cannot mix color/normal sampling "
                        "with packed Linear scalar PBR channel bindings."
                    ),
                }
            )
            continue
        reference_name = (
            "_MIKU_Packed_"
            + hashlib.sha256(resource_id.encode("utf-8")).hexdigest()[:20]
        )
        channel_bindings = [
            {
                "semantic": binding_semantic,
                "channel": binding_channel,
            }
            for binding_semantic, binding_channel in sorted(bindings)
        ]
        for expression in samples:
            params = expression.get("params")
            if isinstance(params, dict):
                params["referenceName"] = reference_name
                params["packed"] = True
                params["channelBindings"] = channel_bindings
    return diagnostics


def build_material_ir(
    graph: Mapping[str, Any],
    *,
    source_blend_id: str = "",
    material_key: str = "",
    workflow_kind: str | None = None,
    fidelity_policy: str | FidelityPolicy = (
        FidelityPolicy.ALLOW_DECLARED_APPROXIMATION
    ),
    add_shader_energy_policy: str | AddShaderEnergyPolicy = (
        AddShaderEnergyPolicy.PRESERVE_BLENDER
    ),
    closure_budget: ClosureBudget | Mapping[str, Any] | None = None,
    conversion_mode: str = "Auto",
) -> dict[str, Any]:
    graph = graph or {}
    material_key = material_key or str((graph.get("material") or {}).get("name") or graph.get("materialName") or "Material")
    workflow_source = graph.get("workflow")
    workflow_source = (
        workflow_source if isinstance(workflow_source, Mapping) else {}
    )
    requested_workflow = normalize_workflow_kind(
        workflow_kind if workflow_kind is not None else workflow_source
    )
    if (
        requested_workflow
        in {"genshin_toon", "wuwa_toon", "hsr_toon", "endfield_toon"}
        and not bool(graph.get("_mikuFixedWorkflowSurrogate"))
    ):
        surrogate_workflow: dict[str, Any] = {"kind": requested_workflow}
        if requested_workflow in {
            "genshin_toon",
            "wuwa_toon",
            "hsr_toon",
        }:
            surrogate_workflow["part"] = normalize_workflow_part(
                workflow_source.get("part")
            )
        base_color_slot = (
            ((graph.get("standardPbrSemantic") or {}).get("slots") or {}).get(
                "BaseColor"
            )
            if isinstance(graph.get("standardPbrSemantic"), Mapping)
            else None
        )
        direct_base_color = (
            base_color_slot.get("default")
            if isinstance(base_color_slot, Mapping)
            and not isinstance(base_color_slot.get("source"), Mapping)
            else [1.0, 1.0, 1.0, 1.0]
        )
        if not (
            isinstance(direct_base_color, (list, tuple))
            and len(direct_base_color) >= 3
        ):
            direct_base_color = [1.0, 1.0, 1.0, 1.0]
        surrogate = {
            "_mikuFixedWorkflowSurrogate": True,
            "material": {"name": material_key},
            "workflow": surrogate_workflow,
            "nodes": [
                {"id": "miku-fixed-output", "op": "Output.Material"},
                {
                    "id": "miku-fixed-surface",
                    "op": "Shader.PrincipledBSDF",
                    "inputs": [
                        {
                            "id": "Base Color",
                            "name": "Base Color",
                            "valueType": "RGBA",
                            "default": list(direct_base_color),
                        }
                    ],
                },
            ],
            "edges": [
                {
                    "from": {
                        "node": "miku-fixed-surface",
                        "socket": "Closure",
                    },
                    "to": {
                        "node": "miku-fixed-output",
                        "socket": "Surface",
                    },
                }
            ],
            "standardPbrSemantic": {"slots": {}},
        }
        document = build_material_ir(
            surrogate,
            source_blend_id=source_blend_id,
            material_key=material_key,
            workflow_kind=requested_workflow,
            fidelity_policy=fidelity_policy,
            add_shader_energy_policy=add_shader_energy_policy,
            closure_budget=closure_budget,
            conversion_mode="Auto",
        )
        payload = {
            key: value
            for key, value in document.items()
            if key
            not in {
                "documentKind",
                "schemaVersion",
                "toolVersion",
                "id",
                "canonicalHash",
            }
        }
        payload["provenance"] = {
            "sourceRefs": [
                {"nodeId": str(node.get("id") or "")}
                for node in sorted(
                    (
                        node
                        for node in graph.get("nodes", []) or []
                        if isinstance(node, Mapping) and node.get("id")
                    ),
                    key=lambda item: str(item.get("id") or ""),
                )
            ]
        }
        payload["diagnostics"] = [
            {
                "severity": "warning",
                "code": "MIKU_FIXED_WORKFLOW_SOURCE_GRAPH_IGNORED",
                "translationQuality": "Approximate",
                "workflow": requested_workflow,
                "message": (
                    "The fixed shader workflow preserves exported images but "
                    "does not translate the Blender closure/value graph."
                ),
            },
            *(
                [
                    {
                        "severity": "info",
                        "code": (
                            "MIKU_FIXED_WORKFLOW_CONVERSION_MODE_IGNORED"
                        ),
                        "translationQuality": "Equivalent",
                        "mode": conversion_mode,
                        "message": (
                            "Fixed shader workflows always use the Native "
                            "texture-binding route and never schedule baking."
                        ),
                    }
                ]
                if conversion_mode != "Auto"
                else []
            ),
        ]
        return make_document(
            "miku-material-ir-2.0",
            payload,
            document_id=str(document["id"]),
        )
    nodes = {str(node.get("id")): node for node in graph.get("nodes", []) or [] if node.get("id")}
    reachable = _reachable_nodes(graph)
    expression_compiler = _RuntimeExpressionCompiler(
        graph,
        nodes,
        material_key,
        conversion_mode=conversion_mode,
        fidelity_policy=fidelity_policy,
    )
    regions: list[dict[str, Any]] = []
    closure_nodes = [node for node_id, node in nodes.items() if node_id in reachable and _node_op(node) in _CLOSURES]
    closure_ids = {str(node.get("id")) for node in closure_nodes}
    mesh_dependent = any(_node_op(nodes[node_id]) in _MESH_OPS for node_id in reachable)
    runtime_dependent = any(
        _node_op(nodes[node_id]) in _NON_BAKEABLE_RUNTIME_OPS
        for node_id in reachable
    )
    if not runtime_dependent:
        runtime_dependent = any(
            _node_op(nodes.get(str((edge.get("from") or {}).get("node") or ""), {}))
            == "Input.Geometry"
            and _normalize_socket((edge.get("from") or {}).get("socket"))
            in _GEOMETRY_RUNTIME_OUTPUTS
            for edge in graph.get("edges", []) or []
        )

    for node in sorted(closure_nodes, key=lambda item: str(item.get("id"))):
        op = _node_op(node)
        semantic = _CLOSURES[op]
        region_id = stable_uuid("miku-region", f"{material_key}:{semantic}:{node.get('id')}" )
        regions.append(
            {
                "id": region_id,
                "kind": semantic,
                "typedInputs": [
                    {"name": str(socket.get("id") or socket.get("name") or ""), "valueType": str(socket.get("valueType") or socket.get("type") or "unknown"), "space": str(socket.get("space") or "None"), "stage": "Fragment", "uniformity": "Varying"}
                    for socket in node.get("inputs", []) or []
                    if isinstance(socket, Mapping)
                ],
                "typedOutputs": [{"name": "Closure", "valueType": "Closure", "stage": "Fragment"}],
                "dependencyIds": sorted(closure_ids - {str(node.get("id"))}),
                "coordinateSpace": "Object" if mesh_dependent else "None",
                "stage": "Fragment",
                "uniformity": "Varying" if runtime_dependent else "Uniform",
                "dynamism": "Runtime" if runtime_dependent else "Static",
                "purity": "Impure" if runtime_dependent else "Pure",
                "deterministic": True,
                "sourceRegionId": region_id,
                "digest": _region_digest(material_key, [str(node.get("id"))], [semantic]),
            }
        )

    non_closure = [
        node
        for node_id, node in nodes.items()
        if node_id in reachable
        and node_id not in closure_ids
        and _node_op(node) not in {"Output.Material", "ShaderNodeOutputMaterial"}
    ]
    if non_closure:
        region_semantics = sorted({_node_op(node) for node in non_closure})
        region_id = stable_uuid("miku-region", f"{material_key}:opaque:{canonical_hash(region_semantics)}")
        regions.append(
            {
                "id": region_id,
                "kind": "OpaqueSemanticRegion",
                "typedInputs": [{"name": "Value", "valueType": "Value", "stage": "Fragment"}],
                "typedOutputs": [{"name": "Value", "valueType": "Value", "stage": "Fragment"}],
                "dependencyIds": [item["id"] for item in regions if item["kind"].endswith("Closure")],
                "coordinateSpace": "Object" if mesh_dependent else "None",
                "stage": "Fragment",
                "uniformity": "Varying" if runtime_dependent else "Uniform",
                "dynamism": "Runtime" if runtime_dependent else "Static",
                "purity": "Impure" if runtime_dependent else "Pure",
                "deterministic": True,
                "sourceRegionId": region_id,
                "sourceSemantics": region_semantics,
                "digest": _region_digest(material_key, [str(node.get("id")) for node in non_closure], region_semantics),
            }
        )

    semantic_slots = ((graph.get("standardPbrSemantic") or {}).get("slots") or {})
    surface_semantic = (
        graph.get("surfaceSemantic")
        if isinstance(graph.get("surfaceSemantic"), Mapping)
        else {}
    )
    required_channels = set(
        str(item)
        for item in surface_semantic.get("requiredChannels", []) or []
        if str(item)
    )
    if not surface_semantic:
        required_channels = {
            "BaseColor",
            "Roughness",
            "Metalness",
            "Normal",
            "Alpha",
        }
    channels = []
    fallback_region = regions[-1]["id"] if regions else stable_uuid("miku-region", f"{material_key}:empty")
    for semantic in _PBR_SEMANTICS:
        slot = semantic_slots.get(semantic) if isinstance(semantic_slots, Mapping) else None
        if semantic == "Height" and not isinstance(slot, Mapping):
            continue
        source = slot.get("source") if isinstance(slot, Mapping) and isinstance(slot.get("source"), Mapping) else None
        channel_default = slot.get("default") if isinstance(slot, Mapping) else None
        if source is None:
            channel_default = _canonical_channel_default(
                semantic,
                channel_default,
            )
        channel = {
            "semantic": semantic,
            "valueType": (
                "Float3"
                if semantic in {"Normal", "Displacement"}
                else (
                    "Color"
                    if semantic
                    in {"BaseColor", "Emission", "TransmissionColor"}
                    else "Scalar"
                )
            ),
            "stage": "Vertex" if semantic == "Displacement" else "Fragment",
            "regionId": fallback_region,
            "default": channel_default,
            "required": semantic in required_channels,
        }
        if source:
            try:
                if semantic == "Height" and expression_compiler.depends_on_runtime(source):
                    raise ValueError(
                        "MIKU_RUNTIME_INPUT_UNSUPPORTED:Height:"
                        f"{source.get('node') or ''}:{source.get('socket') or ''}"
                    )
                if expression_compiler.requires_static_bake(source):
                    reusable = (
                        semantic != "Displacement"
                        and expression_compiler.can_reusable_bake(source)
                    )
                    if reusable:
                        expression_id = expression_compiler.compile_baked(
                            source,
                            value_type=str(channel["valueType"]),
                            usage=(
                                "Normal"
                                if semantic == "Normal"
                                else (
                                    "Color"
                                    if semantic
                                    in {
                                        "BaseColor",
                                        "Emission",
                                        "TransmissionColor",
                                    }
                                    else "Scalar"
                                )
                            ),
                            coordinate_domain=(
                                expression_compiler.static_dependency_domain(
                                    source
                                )
                            ),
                            mesh_binding_required=False,
                        )
                        channel["value"] = {
                            "kind": "Expression",
                            "expressionId": expression_id,
                        }
                        channels.append(channel)
                        continue
                    if not expression_compiler.allows_source_mesh_bake:
                        raise expression_compiler._portable_mesh_bake_error(source)
                    source_op = _node_op(
                        expression_compiler.nodes.get(
                            str(source.get("node") or ""),
                            {},
                        )
                    )
                    uses_planned_height_channel = (
                        semantic == "Displacement"
                        and source_op == "Vector.Displacement"
                        and isinstance(graph.get("heightChannel"), Mapping)
                        and bool(graph.get("heightChannel"))
                    )
                    if uses_planned_height_channel:
                        expression_id = expression_compiler.compile(
                            source,
                            semantic=semantic,
                        )
                        channel["value"] = {
                            "kind": "Expression",
                            "expressionId": expression_id,
                        }
                    elif runtime_dependent and semantic != "Height":
                        source_node = expression_compiler.nodes.get(
                            str(source.get("node") or ""),
                            {},
                        )
                        source_metadata = (
                            source_node.get("source")
                            if isinstance(
                                source_node.get("source"),
                                Mapping,
                            )
                            else {}
                        )
                        if str(
                            source_metadata.get("blenderNodeName") or ""
                        ):
                            expression_id = expression_compiler.compile_baked(
                                source,
                                value_type=str(channel["valueType"]),
                                usage=(
                                    "Normal"
                                    if semantic == "Normal"
                                    else (
                                        "Color"
                                        if semantic
                                        in {
                                            "BaseColor",
                                            "Emission",
                                            "TransmissionColor",
                                        }
                                        else "Scalar"
                                    )
                                ),
                            )
                        else:
                            expression_id = expression_compiler.compile(
                                source,
                                semantic=semantic,
                            )
                        channel["value"] = {
                            "kind": "Expression",
                            "expressionId": expression_id,
                        }
                    else:
                        channel["requiresBake"] = True
                else:
                    expression_id = expression_compiler.compile(
                        source,
                        semantic=semantic,
                    )
                    channel["value"] = {
                        "kind": "Expression",
                        "expressionId": expression_id,
                    }
            except ValueError as exc:
                message = str(exc)
                diagnostic_code = (
                    message.split(":", 1)[0]
                    if message.startswith("MIKU_")
                    else "MIKU_RUNTIME_INPUT_UNSUPPORTED"
                )
                expression_compiler.diagnostics.append(
                    {
                        "severity": "error",
                        "code": diagnostic_code,
                        "translationQuality": "Unsupported",
                        "semantic": semantic,
                        "message": message,
                    }
                )
                if channel["required"]:
                    channel["default"] = None
        channels.append(channel)

    color_management_source = graph.get("colorManagement")
    color_management_source = (
        color_management_source
        if isinstance(color_management_source, Mapping)
        else {}
    )
    coefficients = color_management_source.get("luminanceCoefficients")
    color_management = ColorManagementContext(
        tuple(
            float(value)
            for value in (
                coefficients
                if isinstance(coefficients, (list, tuple))
                and len(coefficients) == 3
                else (0.2126, 0.7152, 0.0722)
            )
        ),
        str(
            color_management_source.get("configFingerprint")
            or "blender-5.2-bundled-ocio"
        ),
    )
    try:
        closure_graph, weighted_closures, closure_diagnostics = (
            build_weighted_closure_set(
                graph,
                color_management=color_management,
            )
        )
    except ValueError as error:
        if conversion_mode != "FullPBRBake":
            raise
        closure_graph, weighted_closures, closure_diagnostics = (
            build_weighted_closure_set(
                _source_mesh_closure_fallback_graph(graph),
                color_management=color_management,
            )
        )
        closure_diagnostics = [
            *closure_diagnostics,
            {
                "severity": "warning",
                "code": "MIKU_SOURCE_MESH_CLOSURE_FALLBACK",
                "translationQuality": "Baked",
                "message": (
                    "A malformed legacy closure branch was replaced only in "
                    "the descriptive IR; the Source Mesh Fidelity worker "
                    "still evaluates the original Blender material."
                ),
                "sourceError": str(error),
            },
        ]
    closure_expression_diagnostics = _bind_closure_value_expressions(
        closure_graph,
        weighted_closures,
        expression_compiler,
        {
            str(channel.get("semantic") or ""): str(
                (channel.get("value") or {}).get("expressionId") or ""
            )
            for channel in channels
            if isinstance(channel, Mapping)
            and isinstance(channel.get("value"), Mapping)
            and str((channel.get("value") or {}).get("kind") or "")
            == "Expression"
            and str((channel.get("value") or {}).get("expressionId") or "")
        },
    )
    static_image_diagnostics = _finalize_static_image_bindings(
        expression_compiler.expressions
    )
    hdr_emission_diagnostics = (
        [
            {
                "severity": "warning",
                "code": "MIKU_HDR_EMISSION_REQUIRES_URP_POST_PROCESSING",
                "translationQuality": "RequiresProjectSetup",
                "message": (
                    "HDR emission is preserved without shader-side whitening; "
                    "bright-to-white appearance requires URP HDR, tone mapping, "
                    "and Bloom project setup."
                ),
            }
        ]
        if any(
            str((item.get("params") or {}).get("semantic") or "")
            == "Emission"
            for item in expression_compiler.expressions
            if str(item.get("op") or "") == "Texture.SampleImage2D"
        )
        else []
    )

    if expression_compiler.expressions:
        expression_output_stages = {
            str(channel.get("stage") or "Fragment")
            for channel in channels
            if (channel.get("value") or {}).get("kind") == "Expression"
        }
        expression_region_stage = (
            next(iter(expression_output_stages))
            if len(expression_output_stages) == 1
            else "Both"
        )
        expression_node_ids = {
            str((item.get("source") or {}).get("nodeId") or "")
            for item in expression_compiler.expressions
            if isinstance(item.get("source"), Mapping)
        }
        expression_node_ids.discard("")
        expression_region_id = stable_uuid(
            "miku-region",
            f"{material_key}:runtime-expressions:"
            + canonical_hash(sorted(item["id"] for item in expression_compiler.expressions)),
        )
        regions.append(
            {
                "id": expression_region_id,
                "kind": "RuntimeExpressionRegion",
                "typedInputs": [],
                "typedOutputs": [
                    {
                        "name": channel["semantic"],
                        "valueType": channel["valueType"],
                        "stage": channel["stage"],
                    }
                    for channel in channels
                    if (channel.get("value") or {}).get("kind") == "Expression"
                ],
                "dependencyIds": [],
                "coordinateSpace": "World",
                "stage": expression_region_stage,
                "uniformity": "Varying",
                "dynamism": "Runtime",
                "purity": "Impure",
                "deterministic": True,
                "sourceRegionId": expression_region_id,
                "sourceSemantics": sorted(
                    {
                        _node_op(nodes[node_id])
                        for node_id in expression_node_ids
                        if node_id in nodes
                    }
                ),
                "expressionIds": sorted(
                    item["id"] for item in expression_compiler.expressions
                ),
                "digest": _region_digest(
                    material_key,
                    expression_node_ids,
                    (item["op"] for item in expression_compiler.expressions),
                ),
            }
        )
        for channel in channels:
            if (channel.get("value") or {}).get("kind") == "Expression":
                channel["regionId"] = expression_region_id

    workflow_source = graph.get("workflow")
    workflow_source = workflow_source if isinstance(workflow_source, Mapping) else {}
    workflow = {
        "kind": normalize_workflow_kind(
            workflow_kind if workflow_kind is not None else workflow_source
        )
    }
    if workflow["kind"] in {
        "genshin_toon",
        "wuwa_toon",
        "hsr_toon",
        "endfield_toon",
    }:
        workflow["part"] = normalize_workflow_part(workflow_source.get("part"))
    material_id = stable_uuid(
        "miku-material",
        f"{source_blend_id}:{material_key}",
    )
    fidelity = (
        fidelity_policy
        if isinstance(fidelity_policy, FidelityPolicy)
        else FidelityPolicy(str(fidelity_policy))
    )
    energy_policy = (
        add_shader_energy_policy
        if isinstance(add_shader_energy_policy, AddShaderEnergyPolicy)
        else AddShaderEnergyPolicy(str(add_shader_energy_policy))
    )
    if closure_budget is None:
        budget = ClosureBudget()
    elif isinstance(closure_budget, ClosureBudget):
        budget = closure_budget
    else:
        budget = ClosureBudget(
            max_lobes=int(closure_budget.get("maxLobes", 8)),
            max_specular_lobes=int(
                closure_budget.get("maxSpecularLobes", 4)
            ),
            max_transmission_lobes=int(
                closure_budget.get("maxTransmissionLobes", 2)
            ),
            max_refraction_samples=int(
                closure_budget.get("maxRefractionSamples", 2)
            ),
            max_distinct_normals=int(
                closure_budget.get("maxDistinctNormals", 4)
            ),
            max_dynamic_weights=int(
                closure_budget.get("maxDynamicWeights", 16)
            ),
            max_estimated_alu=int(
                closure_budget.get("maxEstimatedAlu", 512)
            ),
            max_texture_samples=int(
                closure_budget.get("maxTextureSamples", 32)
            ),
        )
    surface_model_plan = build_surface_model_plan(
        material_id,
        closure_graph,
        weighted_closures,
        fidelity_policy=fidelity,
        add_energy_policy=energy_policy,
        budget=budget,
    )
    surface_model_plan["rootClosure"] = {
        "closureId": closure_graph["rootClosureId"]
    }
    surface_model_plan["channelPlans"] = [
        {
            "semantic": channel["semantic"],
            "valueType": channel["valueType"],
            "stage": channel["stage"],
            "route": (
                "Native"
                if (channel.get("value") or {}).get("kind") == "Expression"
                else (
                    "MeshBake"
                    if channel.get("requiresBake")
                    else "Constant"
                )
            ),
        }
        for channel in channels
    ]
    surface_model_plan.setdefault("features", {})["vertexDisplacement"] = any(
        channel["semantic"] == "Displacement"
        and (channel.get("value") or {}).get("kind") == "Expression"
        for channel in channels
    )
    surface_model_plan["parameterPlans"] = _parameter_records(
        graph,
        material_key,
    )
    value_expressions = sorted(
        expression_compiler.expressions,
        key=lambda item: item["id"],
    )
    source_diagnostics = [
        item
        for item in list(graph.get("diagnostics", []) or [])
        if not (
            isinstance(item, Mapping)
            and str(item.get("code") or "")
            in {
                "MIKU_CLOSURE_FLATTENED_APPROXIMATE",
                *(
                    {"MIKU_CLOSURE_LOWERING_UNSUPPORTED"}
                    if surface_model_plan.get("kind")
                    != "UnsupportedSurface"
                    else set()
                ),
            }
        )
    ]
    payload = {
        "materialKey": material_key,
        "workflow": workflow,
        "displacementPolicy": str(
            graph.get("displacementPolicy") or "FOLLOW_BLENDER"
        ),
        "heightChannel": (
            dict(graph.get("heightChannel"))
            if isinstance(graph.get("heightChannel"), Mapping)
            else {}
        ),
        "source": {"sourceBlendId": source_blend_id, "materialName": material_key},
        "regions": sorted(regions, key=lambda item: item["id"]),
        "valueGraph": {
            "schema": "miku-value-graph-1.0",
            "expressions": value_expressions,
        },
        "expressions": value_expressions,
        "closureGraph": closure_graph,
        "weightedClosures": weighted_closures,
        "surfaceModelPlan": surface_model_plan,
        "channels": channels,
        "resources": list(graph.get("resources", []) or []) if isinstance(graph.get("resources"), list) else graph.get("resources", {}) or {},
        "parameters": _parameter_records(graph, material_key),
        "provenance": {"sourceRefs": [{"nodeId": node_id} for node_id in sorted(reachable)]},
        "diagnostics": [
            *source_diagnostics,
            *expression_compiler.diagnostics,
            *static_image_diagnostics,
            *hdr_emission_diagnostics,
            *closure_expression_diagnostics,
            *closure_diagnostics,
            *surface_model_plan.get("diagnostics", []),
        ],
    }
    if surface_semantic:
        surface_contract = {
            "schema": "miku-surface-1.0",
            "model": str(surface_semantic.get("model") or "StandardLit"),
            "renderMethod": str(
                surface_semantic.get("renderMethod") or "Opaque"
            ),
            "renderFace": str(surface_semantic.get("renderFace") or "Both"),
            "coverageChannel": str(
                surface_semantic.get("coverageChannel") or "Alpha"
            ),
        }
        if surface_contract["model"] == "DielectricScreenRefraction":
            surface_contract.update(
                {
                    "transmissionColorChannel": "TransmissionColor",
                    "transmissionWeightChannel": "TransmissionWeight",
                    "iorChannel": "IOR",
                    "thicknessChannel": "Thickness",
                    "roughnessChannel": "Roughness",
                    "normalChannel": "Normal",
                }
            )
        payload["surfaceContract"] = surface_contract
    return make_document(
        "miku-material-ir-2.0",
        payload,
        document_id=material_id,
    )
