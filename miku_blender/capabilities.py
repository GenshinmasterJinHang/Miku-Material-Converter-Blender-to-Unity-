"""Deterministic EEVEE capability classification for Blender snapshots."""

from __future__ import annotations

from collections import defaultdict
from collections.abc import Mapping
from typing import Any


NATIVE_OR_EQUIVALENT = "NativeOrEquivalent"
REQUIRES_SOURCE_MESH_FIDELITY = "RequiresSourceMeshFidelity"
CYCLES_ONLY = "CyclesOnly"

_CYCLES_ONLY_OPS = {
    "Shader.AnisotropicBSDF": "Blender 5.2 EEVEE does not support anisotropic BSDF semantics",
    "Shader.HairBSDF": "hair BSDF semantics require Cycles",
    "Shader.Holdout": "holdout surface semantics require Cycles",
    "Shader.SheenBSDF": "standalone sheen BSDF semantics require Cycles",
    "Shader.ToonBSDF": "Toon BSDF semantics require Cycles",
    "Vector.Bevel": "Bevel shader evaluation requires Cycles",
}

_NATIVE_OPS = {
    "Color.HueSaturationValue",
    "Color.Invert",
    "Color.Mix",
    "Color.Ramp",
    "Converter.SeparateColor",
    "Converter.SeparateXYZ",
    "Input.CameraData",
    "Input.Color",
    "Input.Fresnel",
    "Input.Geometry",
    "Input.LayerWeight",
    "Input.TextureCoordinate",
    "Input.Time",
    "Input.Value",
    "Math",
    "Math.Mix",
    "Output.Material",
    "Shader.Add",
    "Shader.DiffuseBSDF",
    "Shader.Emission",
    "Shader.GlassBSDF",
    "Shader.GlossyBSDF",
    "Shader.Mix",
    "Shader.PrincipledBSDF",
    "Shader.SubsurfaceScattering",
    "Shader.TranslucentBSDF",
    "Shader.TransparentBSDF",
    "Texture.Image",
    "Texture.Noise",
    "Utility.Reroute",
    "Vector.Bump",
    "Vector.Displacement",
    "Vector.Mapping",
    "Vector.NormalMap",
    "VectorMath",
}

_EEVEE_LIGHT_PATH_OUTPUTS = {"iscameraray", "isshadowray"}
_SOURCE_MESH_SURFACE_OPS = {
    "Shader.Add",
    "Shader.DiffuseBSDF",
    "Shader.GlassBSDF",
    "Shader.GlossyBSDF",
    "Shader.RefractionBSDF",
    "Shader.SubsurfaceScattering",
    "Shader.TranslucentBSDF",
}
_NATIVE_MATH_OPERATIONS = {
    "ABSOLUTE",
    "ADD",
    "COSINE",
    "DIVIDE",
    "GREATER_THAN",
    "LESS_THAN",
    "LOGARITHM",
    "MAXIMUM",
    "MINIMUM",
    "MODULO",
    "MULTIPLY",
    "MULTIPLY_ADD",
    "POWER",
    "SINE",
    "SUBTRACT",
}
_NATIVE_VECTOR_OPERATIONS = {
    "ABSOLUTE",
    "ADD",
    "DIVIDE",
    "DOT_PRODUCT",
    "LENGTH",
    "MULTIPLY",
    "NORMALIZE",
    "SCALE",
    "SUBTRACT",
}
_NATIVE_MIX_OPERATIONS = {
    "ADD",
    "DARKEN",
    "DIFFERENCE",
    "MIX",
    "MULTIPLY",
    "OVERLAY",
    "SCREEN",
    "SUBTRACT",
}


def _normalize(value: Any) -> str:
    return "".join(
        character
        for character in str(value or "").lower()
        if character.isalnum()
    )


def _active_output_ids(nodes: list[Mapping[str, Any]]) -> list[str]:
    outputs = [
        node
        for node in nodes
        if str(node.get("op") or "") == "Output.Material"
        and bool((node.get("params") or {}).get("isActiveOutput"))
    ]
    eevee = [
        str(node.get("id") or "")
        for node in outputs
        if str((node.get("params") or {}).get("target") or "ALL").upper()
        == "EEVEE"
    ]
    if eevee:
        return sorted(filter(None, eevee))
    return sorted(
        str(node.get("id") or "")
        for node in outputs
        if str((node.get("params") or {}).get("target") or "ALL").upper()
        == "ALL"
        and str(node.get("id") or "")
    )


def classify_eevee_graph(graph: Mapping[str, Any]) -> dict[str, Any]:
    """Classify only nodes reachable from the active EEVEE/ALL output."""

    nodes = [
        node
        for node in graph.get("nodes", []) or []
        if isinstance(node, Mapping)
    ]
    by_id = {
        str(node.get("id") or ""): node
        for node in nodes
        if str(node.get("id") or "")
    }
    incoming: dict[str, list[tuple[str, str]]] = defaultdict(list)
    used_outputs: dict[str, set[str]] = defaultdict(set)
    for edge in graph.get("edges", []) or []:
        if not isinstance(edge, Mapping):
            continue
        source = (
            edge.get("from")
            if isinstance(edge.get("from"), Mapping)
            else {}
        )
        target = (
            edge.get("to") if isinstance(edge.get("to"), Mapping) else {}
        )
        source_id = str(source.get("node") or "")
        target_id = str(target.get("node") or "")
        if source_id and target_id:
            incoming[target_id].append(
                (source_id, str(source.get("socket") or ""))
            )

    active_outputs = _active_output_ids(nodes)
    active: set[str] = set()
    pending = list(reversed(active_outputs))
    while pending:
        node_id = pending.pop()
        if not node_id or node_id in active:
            continue
        active.add(node_id)
        for source_id, source_socket in incoming.get(node_id, []):
            used_outputs[source_id].add(_normalize(source_socket))
            pending.append(source_id)

    evidence: list[dict[str, Any]] = []
    node_qualities: dict[str, str] = {}
    for node_id in sorted(active):
        node = by_id.get(node_id, {})
        op = str(node.get("op") or "Opaque.BlenderNode")
        source = (
            node.get("source")
            if isinstance(node.get("source"), Mapping)
            else {}
        )
        quality = NATIVE_OR_EQUIVALENT
        reason = ""
        params = (
            node.get("params")
            if isinstance(node.get("params"), Mapping)
            else {}
        )
        outputs = used_outputs.get(node_id, set())
        if op in _CYCLES_ONLY_OPS:
            quality = CYCLES_ONLY
            reason = _CYCLES_ONLY_OPS[op]
        elif op == "Input.LightPath":
            unsupported = sorted(
                outputs - _EEVEE_LIGHT_PATH_OUTPUTS
            )
            if unsupported:
                quality = CYCLES_ONLY
                reason = "unsupported EEVEE Light Path output: " + ",".join(
                    unsupported
                )
        elif op in _SOURCE_MESH_SURFACE_OPS:
            quality = REQUIRES_SOURCE_MESH_FIDELITY
            reason = (
                "the active closure composition requires a source-mesh PBR "
                "projection"
            )
        elif op == "Input.TextureCoordinate" and (
            outputs - {"object"}
        ):
            quality = REQUIRES_SOURCE_MESH_FIDELITY
            reason = (
                "the active texture-coordinate output is bound to source "
                "mesh evaluation"
            )
        elif op == "Texture.Noise" and (
            outputs - {"fac", "factor"}
        ):
            quality = REQUIRES_SOURCE_MESH_FIDELITY
            reason = (
                "the active Noise output is not in the native Factor subset"
            )
        elif op == "Math" and str(
            params.get("operation") or ""
        ).upper() not in _NATIVE_MATH_OPERATIONS:
            quality = REQUIRES_SOURCE_MESH_FIDELITY
            reason = "the Math operation is outside the native subset"
        elif op == "VectorMath" and str(
            params.get("operation") or ""
        ).upper() not in _NATIVE_VECTOR_OPERATIONS:
            quality = REQUIRES_SOURCE_MESH_FIDELITY
            reason = "the Vector Math operation is outside the native subset"
        elif op in {"Math.Mix", "Color.Mix"} and str(
            params.get("blend_type") or "MIX"
        ).upper() not in _NATIVE_MIX_OPERATIONS:
            quality = REQUIRES_SOURCE_MESH_FIDELITY
            reason = "the Mix blend mode is outside the native subset"
        elif op not in _NATIVE_OPS:
            quality = REQUIRES_SOURCE_MESH_FIDELITY
            reason = (
                "the active EEVEE branch requires deterministic source-mesh "
                "evaluation"
            )
        node_qualities[node_id] = quality
        if quality != NATIVE_OR_EQUIVALENT:
            evidence.append(
                {
                    "nodeId": node_id,
                    "op": op,
                    "blenderNodeType": str(
                        source.get("blenderNodeType") or ""
                    ),
                    "displayName": str(source.get("displayName") or ""),
                    "quality": quality,
                    "reason": reason,
                }
            )

    if not active_outputs:
        overall = REQUIRES_SOURCE_MESH_FIDELITY
        evidence.append(
            {
                "nodeId": "",
                "op": "Output.Material",
                "blenderNodeType": "ShaderNodeOutputMaterial",
                "displayName": "",
                "quality": overall,
                "reason": "no active EEVEE or ALL material output",
            }
        )
    elif CYCLES_ONLY in node_qualities.values():
        overall = CYCLES_ONLY
    elif REQUIRES_SOURCE_MESH_FIDELITY in node_qualities.values():
        overall = REQUIRES_SOURCE_MESH_FIDELITY
    else:
        overall = NATIVE_OR_EQUIVALENT

    return {
        "quality": overall,
        "activeOutputIds": active_outputs,
        "activeNodeIds": sorted(active),
        "usedOutputs": {
            node_id: sorted(outputs)
            for node_id, outputs in sorted(used_outputs.items())
            if node_id in active
        },
        "evidence": evidence,
    }
