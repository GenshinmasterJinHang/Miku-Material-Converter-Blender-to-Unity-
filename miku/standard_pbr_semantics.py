"""Standard PBR semantic extraction for Miku 1.0 material graphs.

This module was reintroduced in Miku 1.0.1 to replace the simplified
``_principled_slots_from_snapshot`` closure pass that shipped with the
identity migration.  It preserves the legacy B2U behaviours:

* socket-aware semantic inference (Base Color / Metallic / Roughness / …)
* recursive passthrough traversal (Color.Ramp, Math, Mix, RGBToBW, …)
* ORM (Occlusion-Roughness-Metallic) packed-texture detection
* Bump vs. Normal Map distinction when both are wired
* AlphaMode inference (Opaque / Cutout / Blend) and Roughness→Smoothness flag
* loose-name texture recovery with reduced confidence

Public surface:

* :class:`StandardPbrSemanticExtractor`
* :func:`extract_standard_pbr_semantic`
* :class:`StandardPbrTextureSemantic`
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Any, Dict, Iterable, List, Optional, Sequence, Set, Tuple

from .standard_pbr_texture_semantics import (
    infer_channel_packing_config,
    infer_standard_pbr_texture_semantic,
)


class StandardPbrTextureSemantic(str, Enum):
    BaseColor = "BaseColor"
    AmbientOcclusion = "AmbientOcclusion"
    Metalness = "Metalness"
    Roughness = "Roughness"
    Bump = "Bump"
    Normal = "Normal"
    Height = "Height"
    Displacement = "Displacement"
    Reflection = "Reflection"
    Specular = "Specular"
    Glossiness = "Glossiness"
    Emission = "Emission"
    Alpha = "Alpha"
    Unknown = "Unknown"


CHANNEL_BY_SOCKET = {
    "Red": "R",
    "R": "R",
    "Green": "G",
    "G": "G",
    "Blue": "B",
    "B": "B",
    "Alpha": "A",
    "A": "A",
}


SOCKET_SEMANTICS = {
    "Base Color": StandardPbrTextureSemantic.BaseColor.value,
    "Metallic": StandardPbrTextureSemantic.Metalness.value,
    "Metalness": StandardPbrTextureSemantic.Metalness.value,
    "Roughness": StandardPbrTextureSemantic.Roughness.value,
    "Alpha": StandardPbrTextureSemantic.Alpha.value,
    "Emission Color": StandardPbrTextureSemantic.Emission.value,
    "Emission": StandardPbrTextureSemantic.Emission.value,
    "Specular IOR Level": StandardPbrTextureSemantic.Specular.value,
    "Specular Tint": StandardPbrTextureSemantic.Specular.value,
    "Specular": StandardPbrTextureSemantic.Specular.value,
    "IOR": StandardPbrTextureSemantic.Specular.value,
}


PASSTHROUGH_OPS = {
    "Color.Ramp",
    "Math.MapRange",
    "Math",
    "Color.Invert",
    "Color.RGBCurves",
    "Color.Mix",
    "Math.Mix",
    "Converter.RGBToBW",
    "Converter.FloatCurve",
    "Utility.Reroute",
}


@dataclass
class TraceResult:
    resource: str = ""
    texture: str = ""
    channel: str = ""
    node_id: str = ""
    node_name: str = ""
    confidence: float = 1.0
    socket_path: str = ""
    source: str = "socket"


class StandardPbrSemanticExtractor:
    def __init__(
        self,
        graph: Dict[str, Any],
        smoothness_source_priority: str = "Roughness",
        workflow_priority: str = "Metallic",
    ) -> None:
        self.graph = graph or {}
        self.nodes = {node.get("id"): node for node in self.graph.get("nodes", []) if node.get("id")}
        self.resources = self.graph.get("resources", {}) or {}
        self.incoming: Dict[Tuple[str, str], List[Dict[str, Any]]] = {}
        self.incoming_by_node: Dict[str, List[Dict[str, Any]]] = {}
        for edge in self.graph.get("edges", []) or []:
            target = edge.get("to") or {}
            source = edge.get("from") or {}
            if not target.get("node") or not target.get("socket") or not source.get("node"):
                continue
            self.incoming.setdefault((target["node"], target["socket"]), []).append(edge)
            self.incoming_by_node.setdefault(target["node"], []).append(edge)
        self.smoothness_source_priority = smoothness_source_priority or "Roughness"
        self.workflow_priority = workflow_priority or "Metallic"
        self.diagnostics: List[Dict[str, Any]] = []
        self.used_resources: Set[str] = set()

    def extract(self) -> Dict[str, Any]:
        material_name = (self.graph.get("material") or {}).get("name", "")
        result = {
            "workflow": "Metallic",
            "source": {
                "shader": "SemanticMaps",
                "blenderMaterial": material_name,
                "confidence": 0.0,
            },
            "slots": {},
            "packedTextures": {},
            "diagnostics": self.diagnostics,
        }

        output_node, output_socket = self._material_output()
        surface_node = self._surface_shader(output_node, output_socket)
        if surface_node is not None:
            result["source"]["shader"] = self._source_shader_name(surface_node)
            if surface_node.get("op") == "Shader.PrincipledBSDF":
                self._extract_principled(surface_node, result)
            else:
                self._extract_non_principled(surface_node, result)

        if output_node is not None:
            self._extract_output_displacement(output_node, result)

        self._extract_loose_textures(result)
        self._detect_packed_textures(result)
        self._resolve_conflicts(result)
        result["source"]["confidence"] = self._confidence(result)
        return result

    def _extract_principled(self, node: Dict[str, Any], result: Dict[str, Any]) -> None:
        for socket, semantic in SOCKET_SEMANTICS.items():
            traces = self._trace_socket(node["id"], socket)
            if not traces:
                self._add_scalar_default(node, socket, semantic, result)
                continue
            for trace in traces:
                self._add_slot(result, semantic, trace, node, socket)

        normal_traces = self._trace_socket(node["id"], "Normal")
        for trace in normal_traces:
            trace_node = self.nodes.get(trace.node_id)
            if trace_node and trace_node.get("op") == "Vector.Bump":
                self._add_bump_slot(result, trace, trace_node, node)
            elif trace_node and trace_node.get("op") == "Vector.NormalMap":
                self._add_normal_slot(result, trace, trace_node, node)
            else:
                self._add_slot(result, StandardPbrTextureSemantic.Normal.value, trace, node, "Normal")

    def _extract_non_principled(self, node: Dict[str, Any], result: Dict[str, Any]) -> None:
        op = node.get("op", "")
        if op == "Shader.DiffuseBSDF":
            for trace in self._trace_socket(node["id"], "Color"):
                self._add_slot(result, StandardPbrTextureSemantic.BaseColor.value, trace, node, "Color")
        elif op in {"Shader.GlossyBSDF", "Shader.SpecularBSDF"}:
            for trace in self._trace_socket(node["id"], "Color"):
                self._add_slot(result, StandardPbrTextureSemantic.Specular.value, trace, node, "Color")
        elif op == "Shader.Emission":
            for trace in self._trace_socket(node["id"], "Color"):
                self._add_slot(result, StandardPbrTextureSemantic.Emission.value, trace, node, "Color")
        elif op == "Shader.Mix":
            for edge in self.incoming_by_node.get(node["id"], []):
                from_node = self.nodes.get((edge.get("from") or {}).get("node"))
                if from_node is not None and from_node.get("op", "").startswith("Shader."):
                    self._extract_non_principled(from_node, result)

    def _extract_output_displacement(self, output_node: Dict[str, Any], result: Dict[str, Any]) -> None:
        for trace in self._trace_socket(output_node["id"], "Displacement"):
            trace_node = self.nodes.get(trace.node_id)
            if trace_node and trace_node.get("op") == "Vector.Displacement":
                self._add_displacement_slot(result, trace, trace_node, output_node)
            else:
                self._add_slot(result, StandardPbrTextureSemantic.Displacement.value, trace, output_node, "Displacement")

    def _trace_socket(self, node_id: str, socket: str) -> List[TraceResult]:
        edges = list(self.incoming.get((node_id, socket), []))
        if not edges:
            edges = [edge for edge in self.incoming_by_node.get(node_id, []) if self._socket_matches((edge.get("to") or {}).get("socket", ""), socket)]
        traces: List[TraceResult] = []
        for edge in edges:
            source = edge.get("from") or {}
            traces.extend(self._trace_from(source.get("node", ""), source.get("socket", ""), set(), ""))
        return traces

    def _trace_from(self, node_id: str, output_socket: str, seen: Set[Tuple[str, str]], channel: str) -> List[TraceResult]:
        key = (node_id, output_socket)
        if key in seen:
            return []
        seen.add(key)
        node = self.nodes.get(node_id)
        if node is None:
            return []
        op = node.get("op", "")
        params = node.get("params") or {}

        if op == "Texture.Image":
            resource = params.get("resource") or ""
            return [
                TraceResult(
                    resource=resource,
                    texture=self._resource_texture_path(resource),
                    channel=channel or self._default_channel_for_output(output_socket),
                    node_id=node_id,
                    node_name=(node.get("source") or {}).get("blenderNodeName", node_id),
                    confidence=1.0,
                    source="socket",
                )
            ]

        if op == "Input.AmbientOcclusion":
            return [TraceResult(node_id=node_id, node_name=(node.get("source") or {}).get("blenderNodeName", node_id), confidence=0.85, source="node")]

        if op == "Converter.SeparateColor":
            next_channel = CHANNEL_BY_SOCKET.get(output_socket, channel)
            return self._trace_first_inputs(node_id, ["Color", "Image", "Vector"], seen, next_channel)

        if op == "Vector.NormalMap":
            traces = self._trace_first_inputs(node_id, ["Color"], seen, channel or "RGB")
            for trace in traces:
                trace.node_id = node_id
            return traces

        if op == "Vector.Bump":
            traces = self._trace_first_inputs(node_id, ["Height"], seen, channel or "R")
            for trace in traces:
                trace.node_id = node_id
            return traces

        if op == "Vector.Displacement":
            traces = self._trace_first_inputs(node_id, ["Height"], seen, channel or "R")
            for trace in traces:
                trace.node_id = node_id
            return traces

        if op in PASSTHROUGH_OPS or op.startswith("Converter."):
            traces = self._trace_first_inputs(node_id, ["Color", "Value", "A", "B", "Factor", "Image", "Vector"], seen, channel)
            return traces

        return self._trace_any_input(node_id, seen, channel)

    def _trace_first_inputs(self, node_id: str, sockets: Sequence[str], seen: Set[Tuple[str, str]], channel: str) -> List[TraceResult]:
        for socket in sockets:
            traces = []
            for edge in self._incoming_edges(node_id, socket):
                source = edge.get("from") or {}
                traces.extend(self._trace_from(source.get("node", ""), source.get("socket", ""), seen, channel))
            if traces:
                return traces
        return []

    def _trace_any_input(self, node_id: str, seen: Set[Tuple[str, str]], channel: str) -> List[TraceResult]:
        traces: List[TraceResult] = []
        for edge in self.incoming_by_node.get(node_id, []):
            source = edge.get("from") or {}
            traces.extend(self._trace_from(source.get("node", ""), source.get("socket", ""), seen, channel))
        return traces

    def _incoming_edges(self, node_id: str, socket: str) -> List[Dict[str, Any]]:
        result = list(self.incoming.get((node_id, socket), []))
        if result:
            return result
        return [edge for edge in self.incoming_by_node.get(node_id, []) if self._socket_matches((edge.get("to") or {}).get("socket", ""), socket)]

    def _add_slot(
        self,
        result: Dict[str, Any],
        semantic: str,
        trace: TraceResult,
        target_node: Dict[str, Any],
        target_socket: str,
    ) -> None:
        if not trace.resource and semantic not in {StandardPbrTextureSemantic.AmbientOcclusion.value}:
            return
        slot = self._base_slot(semantic, trace, target_node, target_socket)
        self._apply_semantic_defaults(semantic, slot)
        self._put_slot(result, semantic, slot)
        if trace.resource:
            self.used_resources.add(trace.resource)
        self._diag(
            "info",
            "standard_pbr_socket_semantic",
            f"Image Texture connected to {self._node_label(target_node)}.{target_socket} exported as {semantic}.",
            node=self._node_label(target_node),
            data={"semantic": semantic, "resource": trace.resource, "socketPath": slot.get("socketPath", "")},
        )

    def _add_normal_slot(self, result: Dict[str, Any], trace: TraceResult, normal_node: Dict[str, Any], target_node: Dict[str, Any]) -> None:
        slot = self._base_slot(StandardPbrTextureSemantic.Normal.value, trace, target_node, "Normal")
        slot.update(
            {
                "strength": self._param_float(normal_node, "strength", 1.0),
                "space": self._normal_space((normal_node.get("params") or {}).get("space", "TANGENT")),
                "flipGreen": False,
            }
        )
        self._put_slot(result, StandardPbrTextureSemantic.Normal.value, slot)
        if trace.resource:
            self.used_resources.add(trace.resource)
        self._diag("info", "standard_pbr_socket_semantic", "Image Texture connected to Normal Map exported as Normal.", node=self._node_label(normal_node))

    def _add_bump_slot(self, result: Dict[str, Any], trace: TraceResult, bump_node: Dict[str, Any], target_node: Dict[str, Any]) -> None:
        slot = self._base_slot(StandardPbrTextureSemantic.Bump.value, trace, target_node, "Normal")
        slot.update(
            {
                "strength": self._param_float(bump_node, "strength", 0.1),
                "distance": self._param_float(bump_node, "distance", 1.0),
            }
        )
        self._put_slot(result, StandardPbrTextureSemantic.Bump.value, slot)
        if trace.resource:
            self.used_resources.add(trace.resource)
        self._diag(
            "warning",
            "standard_pbr_bump_approximation",
            "Blender Bump node approximated by shader height derivatives unless baked to normal.",
            node=self._node_label(bump_node),
        )

    def _add_displacement_slot(self, result: Dict[str, Any], trace: TraceResult, displacement_node: Dict[str, Any], output_node: Dict[str, Any]) -> None:
        slot = self._base_slot(StandardPbrTextureSemantic.Displacement.value, trace, output_node, "Displacement")
        slot.update(
            {
                "scale": self._param_float(displacement_node, "scale", 0.05),
                "midlevel": self._param_float(displacement_node, "midlevel", 0.5),
                "mode": "Parallax",
            }
        )
        self._put_slot(result, StandardPbrTextureSemantic.Displacement.value, slot)
        if trace.resource:
            self.used_resources.add(trace.resource)
        self._diag(
            "warning",
            "standard_pbr_displacement_parallax",
            "Blender Displacement mapped to parallax/POM by default; real geometry displacement requires explicit vertex displacement mode.",
            node=self._node_label(displacement_node),
        )

    def _add_scalar_default(self, node: Dict[str, Any], socket: str, semantic: str, result: Dict[str, Any]) -> None:
        default = self._input_default(node, socket)
        if default is None:
            return
        if semantic == StandardPbrTextureSemantic.BaseColor.value:
            slot = {"color": self._color(default), "channel": "RGBA", "colorSpace": "sRGB", "required": False, "scalar": None}
        elif semantic in {StandardPbrTextureSemantic.Metalness.value, StandardPbrTextureSemantic.Roughness.value, StandardPbrTextureSemantic.Glossiness.value}:
            slot = {"scalar": self._float(default, 0.0 if semantic == StandardPbrTextureSemantic.Metalness.value else 0.5), "channel": "R", "colorSpace": "Linear"}
            if semantic == StandardPbrTextureSemantic.Roughness.value:
                slot["convertToSmoothness"] = True
            if semantic == StandardPbrTextureSemantic.Glossiness.value:
                slot["isSmoothness"] = True
        elif semantic == StandardPbrTextureSemantic.Alpha.value:
            slot = {"scalar": self._float(default, 1.0), "channel": "R", "alphaMode": "Opaque", "cutoff": 0.5}
        else:
            return
        slot["source"] = "socket_default"
        slot["confidence"] = 0.6
        slot["socketPath"] = self._socket_path(node, socket)
        self._put_slot(result, semantic, slot, replace=False)

    def _extract_loose_textures(self, result: Dict[str, Any]) -> None:
        for node in self.graph.get("nodes", []) or []:
            if node.get("op") != "Texture.Image":
                continue
            resource = (node.get("params") or {}).get("resource") or ""
            if not resource or resource in self.used_resources:
                continue
            name = self._resource_name(resource)
            inferred = infer_standard_pbr_texture_semantic(name)
            semantic = inferred.get("semantic", "Unknown")
            if semantic == "Unknown":
                continue
            trace = TraceResult(
                resource=resource,
                texture=self._resource_texture_path(resource),
                channel=self._default_channel_for_semantic(semantic),
                node_id=node.get("id", ""),
                node_name=self._node_label(node),
                confidence=min(float(inferred.get("confidence", 0.45)), 0.55),
                source="loose_name",
            )
            slot = self._base_slot(semantic, trace, node, "")
            slot["source"] = "loose_name"
            self._apply_semantic_defaults(semantic, slot)
            self._put_slot(result, semantic, slot, replace=False)
            self.used_resources.add(resource)
            self._diag(
                "info",
                "standard_pbr_loose_semantic_texture",
                f"Loose semantic texture detected by name: {semantic}.",
                node=self._node_label(node),
                data={"semantic": semantic, "resource": resource, "matched": inferred.get("matched", "")},
            )

    def _detect_packed_textures(self, result: Dict[str, Any]) -> None:
        packed = result.setdefault("packedTextures", {})
        for semantic, slot in (result.get("slots") or {}).items():
            resource = slot.get("resource") or ""
            if not resource:
                continue
            config = infer_channel_packing_config(self._resource_name(resource))
            if config:
                packed[config["name"]] = {"texture": self._resource_texture_path(resource), **config}
        if self._has_slots(result, "Metalness", "AmbientOcclusion") or self._has_slots(result, "Roughness", "AmbientOcclusion"):
            packed.setdefault(
                "MaskMap",
                {
                    "texture": "",
                    "name": "MaskMap",
                    "r": "Metalness",
                    "g": "AmbientOcclusion",
                    "b": "Unused",
                    "a": "Smoothness",
                },
            )
            self._diag("info", "standard_pbr_maskmap_candidate", "Generated packed MaskMap: R=Metallic, G=AO, A=Smoothness.")

    def _resolve_conflicts(self, result: Dict[str, Any]) -> None:
        slots = result.get("slots") or {}
        if "Roughness" in slots and "Glossiness" in slots:
            priority = self.smoothness_source_priority if self.smoothness_source_priority in {"Roughness", "Glossiness"} else "Roughness"
            slots["smoothnessSourcePriority"] = priority
            self._diag("warning", "standard_pbr_smoothness_conflict", f"Both Roughness and Glossiness maps are present; using configured priority: {priority}.")
        if "Roughness" in slots:
            self._diag("info", "standard_pbr_roughness_to_smoothness", "Roughness converted to Smoothness using smoothness = 1 - roughness.")
        if "Glossiness" in slots:
            self._diag("info", "standard_pbr_glossiness_smoothness", "Glossiness used directly as Smoothness.")

        has_metal = "Metalness" in slots
        has_spec = "Specular" in slots
        if has_metal and has_spec:
            workflow = self.workflow_priority if self.workflow_priority in {"Metallic", "Specular"} else "Metallic"
            self._diag("warning", "standard_pbr_workflow_conflict", f"Both Metalness and Specular maps are present; using configured workflow: {workflow}.")
        elif has_spec:
            workflow = "Specular"
        elif has_metal:
            workflow = "Metallic"
        else:
            workflow = "Metallic"
        result["workflow"] = workflow

    def _base_slot(self, semantic: str, trace: TraceResult, target_node: Dict[str, Any], target_socket: str) -> Dict[str, Any]:
        return {
            "texture": trace.texture,
            "resource": trace.resource,
            "channel": trace.channel or self._default_channel_for_semantic(semantic),
            "socketPath": self._socket_path(target_node, target_socket),
            "source": trace.source,
            "confidence": trace.confidence,
            "required": False,
        }

    def _apply_semantic_defaults(self, semantic: str, slot: Dict[str, Any]) -> None:
        if semantic == "BaseColor":
            slot.setdefault("color", [1.0, 1.0, 1.0, 1.0])
            slot["channel"] = "RGBA"
            slot["colorSpace"] = "sRGB"
        elif semantic == "AmbientOcclusion":
            slot["channel"] = self._scalar_channel(slot.get("channel"))
            slot["colorSpace"] = "Linear"
            slot.setdefault("strength", 1.0)
        elif semantic == "Metalness":
            slot["channel"] = self._scalar_channel(slot.get("channel"))
            slot["colorSpace"] = "Linear"
            slot.setdefault("scalar", 1.0)
        elif semantic == "Roughness":
            slot["channel"] = self._scalar_channel(slot.get("channel"))
            slot["colorSpace"] = "Linear"
            slot.setdefault("scalar", 1.0)
            slot["convertToSmoothness"] = True
        elif semantic == "Glossiness":
            slot["channel"] = self._scalar_channel(slot.get("channel"))
            slot["colorSpace"] = "Linear"
            slot.setdefault("scalar", 1.0)
            slot["isSmoothness"] = True
        elif semantic == "Normal":
            slot["channel"] = "RGB"
            slot.setdefault("strength", 1.0)
            slot.setdefault("space", "Tangent")
            slot.setdefault("flipGreen", False)
        elif semantic == "Bump":
            slot["channel"] = self._scalar_channel(slot.get("channel"))
            slot.setdefault("strength", 0.1)
            slot.setdefault("distance", 1.0)
        elif semantic == "Height":
            slot["channel"] = self._scalar_channel(slot.get("channel"))
            slot.setdefault("scale", 0.05)
            slot.setdefault("bias", 0.0)
            slot["colorSpace"] = "Linear"
        elif semantic == "Displacement":
            slot["channel"] = self._scalar_channel(slot.get("channel"))
            slot.setdefault("scale", 0.05)
            slot.setdefault("midlevel", 0.5)
            slot.setdefault("mode", "Parallax")
            slot["colorSpace"] = "Linear"
        elif semantic == "Specular":
            if slot.get("channel") == "RGBA":
                slot["channel"] = "RGB"
            slot["colorSpace"] = "sRGB" if slot.get("channel") in {"RGB", "RGBA"} else "Linear"
            slot.setdefault("scalar", 0.5)
        elif semantic == "Reflection":
            if slot.get("channel") == "RGBA":
                slot["channel"] = "RGB"
            slot.setdefault("colorSpace", "sRGB")
            slot.setdefault("intensity", 1.0)
            self._diag("warning", "standard_pbr_reflection_mask", "Reflection 2D map interpreted as reflection intensity mask.")
        elif semantic == "Emission":
            if slot.get("channel") == "RGBA":
                slot["channel"] = "RGB"
            slot["colorSpace"] = "sRGB"
            slot.setdefault("strength", 1.0)
        elif semantic == "Alpha":
            slot["channel"] = self._scalar_channel(slot.get("channel"))
            slot.setdefault("alphaMode", "Opaque")
            slot.setdefault("cutoff", 0.5)
            slot["colorSpace"] = "Linear"

    def _put_slot(self, result: Dict[str, Any], semantic: str, slot: Dict[str, Any], replace: bool = True) -> None:
        slots = result.setdefault("slots", {})
        if semantic not in slots or replace or slot.get("confidence", 0) > slots[semantic].get("confidence", 0):
            slots[semantic] = slot

    def _material_output(self) -> Tuple[Optional[Dict[str, Any]], str]:
        surface = ((self.graph.get("entry") or {}).get("surface") or {})
        node = self.nodes.get(surface.get("node", ""))
        return node, surface.get("socket", "Surface")

    def _surface_shader(self, output_node: Optional[Dict[str, Any]], output_socket: str) -> Optional[Dict[str, Any]]:
        if output_node is None:
            return None
        for edge in self._incoming_edges(output_node["id"], output_socket or "Surface"):
            node = self.nodes.get((edge.get("from") or {}).get("node", ""))
            if node is not None:
                return node
        return None

    def _source_shader_name(self, node: Dict[str, Any]) -> str:
        op = node.get("op", "")
        if op == "Shader.PrincipledBSDF":
            return "PrincipledBSDF"
        if op == "Shader.Mix":
            return "MixedBSDF"
        return "SemanticMaps"

    def _resource_texture_path(self, resource: str) -> str:
        data = self.resources.get(resource) or {}
        return data.get("path") or data.get("uri") or data.get("exportFileName") or ""

    def _resource_name(self, resource: str) -> str:
        data = self.resources.get(resource) or {}
        return data.get("blenderImageName") or data.get("exportFileName") or data.get("name") or data.get("path") or resource

    def _socket_path(self, node: Dict[str, Any], socket: str) -> str:
        if not socket:
            return self._node_label(node)
        op = node.get("op", "")
        if op == "Output.Material":
            return f"MaterialOutput.{socket}"
        if op == "Shader.PrincipledBSDF":
            return f"MaterialOutput.Surface/PrincipledBSDF.{socket}"
        return f"{self._node_label(node)}.{socket}"

    def _node_label(self, node: Optional[Dict[str, Any]]) -> str:
        if not node:
            return ""
        return (node.get("source") or {}).get("blenderNodeName") or node.get("id", "")

    def _input_default(self, node: Dict[str, Any], socket: str) -> Any:
        for item in node.get("inputs") or []:
            if item.get("id") == socket or item.get("name") == socket:
                return item.get("defaultValue")
        return None

    def _param_float(self, node: Dict[str, Any], key: str, fallback: float) -> float:
        return self._float((node.get("params") or {}).get(key), fallback)

    @staticmethod
    def _float(value: Any, fallback: float) -> float:
        try:
            return float(value)
        except (TypeError, ValueError):
            return fallback

    @staticmethod
    def _color(value: Any) -> List[float]:
        if isinstance(value, list):
            result = [float(item) for item in value[:4]]
            while len(result) < 4:
                result.append(1.0)
            return result
        return [1.0, 1.0, 1.0, 1.0]

    @staticmethod
    def _normal_space(value: str) -> str:
        return "Tangent" if str(value or "").upper() == "TANGENT" else str(value or "Tangent").title()

    @staticmethod
    def _default_channel_for_output(socket: str) -> str:
        if socket == "Alpha":
            return "R"
        if socket == "Color":
            return "RGBA"
        return CHANNEL_BY_SOCKET.get(socket, "R")

    @staticmethod
    def _default_channel_for_semantic(semantic: str) -> str:
        if semantic in {"BaseColor"}:
            return "RGBA"
        if semantic in {"Specular", "Reflection", "Emission", "Normal"}:
            return "RGB"
        return "R"

    @staticmethod
    def _scalar_channel(channel: Any) -> str:
        value = str(channel or "").upper()
        return value if value in {"R", "G", "B", "A"} else "R"

    @staticmethod
    def _socket_matches(actual: str, requested: str) -> bool:
        return actual == requested or (actual or "").startswith((requested or "") + "_")

    @staticmethod
    def _has_slots(result: Dict[str, Any], *names: str) -> bool:
        slots = result.get("slots") or {}
        return all(name in slots for name in names)

    def _confidence(self, result: Dict[str, Any]) -> float:
        slots = result.get("slots") or {}
        if not slots:
            return 0.0
        values = [slot.get("confidence", 0.0) for slot in slots.values() if isinstance(slot, dict)]
        return round(sum(values) / max(1, len(values)), 3)

    def _diag(self, severity: str, code: str, message: str, node: str = "", data: Optional[Dict[str, Any]] = None) -> None:
        item: Dict[str, Any] = {"severity": severity, "code": code, "message": message}
        if node:
            item["node"] = node
        if data:
            item["data"] = data
        self.diagnostics.append(item)


def extract_standard_pbr_semantic(graph: Dict[str, Any]) -> Dict[str, Any]:
    return StandardPbrSemanticExtractor(graph).extract()
