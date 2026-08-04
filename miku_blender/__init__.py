"""Blender 5.2 integration for Miku semantic material bundles.

The module imports bpy only when Blender loads the add-on.  The target-neutral
planner remains importable and testable in ordinary Python.
"""

from __future__ import annotations

import ast
import hashlib
import json
import math
import os
import re
import shutil
import tempfile
import unicodedata
import uuid
from pathlib import Path
from typing import Any, Mapping

try:
    import bpy
except ImportError:  # Ordinary Python unit tests do not provide Blender.
    bpy = None

from .capabilities import classify_eevee_graph
from .translations import TRANSLATIONS

try:
    from ..miku.bundle import (
        compute_sealed_digest,
        make_file_reference,
        validate_bundle_document,
        validate_portable_hybrid_resources,
    )
    from ..miku.bake_protocol import (
        DEFAULT_BAKE_RESOLUTION,
        normalize_bake_resolution,
    )
    from ..miku.contracts import make_document, validate_document
    from ..miku.fixed_workflows import (
        FIXED_TEXTURE_ROLES,
        FIXED_WORKFLOWS,
        allowed_texture_role,
        infer_filename_texture_role,
        normalize_texture_role,
        texture_role_color_space,
    )
    from ..miku.planner import (
        SOURCE_MESH_RESOLVABLE_DIAGNOSTIC_CODES,
        ConversionPlanner,
        default_target_profile,
    )
    from ..miku.semantic import (
        build_material_ir,
        build_source_map,
        normalize_workflow_kind,
        normalize_workflow_part,
    )
    from ..miku.standard_pbr_semantics import (
        extract_standard_pbr_semantic as _extract_legacy_standard_pbr_semantic,
    )
    from ..miku.time_driver import TimeDriverError, parse_affine_frame
except (ImportError, ValueError):
    from miku.bundle import (
        compute_sealed_digest,
        make_file_reference,
        validate_bundle_document,
        validate_portable_hybrid_resources,
    )
    from miku.bake_protocol import (
        DEFAULT_BAKE_RESOLUTION,
        normalize_bake_resolution,
    )
    from miku.contracts import make_document, validate_document
    from miku.fixed_workflows import (
        FIXED_TEXTURE_ROLES,
        FIXED_WORKFLOWS,
        allowed_texture_role,
        infer_filename_texture_role,
        normalize_texture_role,
        texture_role_color_space,
    )
    from miku.planner import (
        SOURCE_MESH_RESOLVABLE_DIAGNOSTIC_CODES,
        ConversionPlanner,
        default_target_profile,
    )
    from miku.semantic import (
        build_material_ir,
        build_source_map,
        normalize_workflow_kind,
        normalize_workflow_part,
    )
    from miku.standard_pbr_semantics import (
        extract_standard_pbr_semantic as _extract_legacy_standard_pbr_semantic,
    )
    from miku.time_driver import TimeDriverError, parse_affine_frame


bl_info = {
    "name": "Miku Semantic Material Converter",
    "author": "Miku contributors",
    "version": (2, 2, 8),
    "blender": (5, 2, 0),
    "location": "Shader Editor > Sidebar > Miku",
    "description": "Export Blender materials as target-neutral semantic regions and deterministic Unity bundles.",
    "category": "Import-Export",
}

WORKFLOW_ITEMS = (
    ("standard_pbr", "Standard PBR", "Editable URP Shader Graph"),
    ("genshin_toon", "Genshin Toon", "Miku Genshin-compatible backend"),
    ("wuwa_toon", "WuWa Toon", "Miku Wuthering Waves-compatible backend"),
    ("hsr_toon", "HSR Toon", "Miku HSR-compatible backend"),
    ("endfield_toon", "Endfield Toon", "Miku Endfield-compatible backend"),
)
BAKE_QUALITY_RESOLUTIONS = {
    "LOW_512": 512,
    "STANDARD_1024": 1024,
    "HIGH_2048": 2048,
    "ULTRA_4096": 4096,
}


def bake_resolution_for_quality(quality: Any) -> int:
    key = str(quality or "")
    if key not in BAKE_QUALITY_RESOLUTIONS:
        raise RuntimeError(f"MIKU_BAKE_QUALITY_INVALID:{key or '<missing>'}")
    return normalize_bake_resolution(BAKE_QUALITY_RESOLUTIONS[key])


def _translate_iface(message: str) -> str:
    if bpy is None:
        return message
    translations = getattr(getattr(bpy, "app", None), "translations", None)
    if translations is None:
        return message
    return str(translations.pgettext_iface(message))


def _translate_diagnostic(message: str) -> str:
    """Translate friendly exporter diagnostics while keeping their codes stable."""

    code, separator, detail = str(message).partition(":")
    templates = {
        "MIKU_TIME_INPUT_UNSUPPORTED": (
            "Time-dependent material outputs are not supported by the Blender "
            "exporter. Remove the time dependency and export again."
        ),
    }
    template = templates.get(code)
    if template is None:
        return _translate_iface(str(message))
    localized = _translate_iface(template)
    return f"{code}:{localized}" + (f" ({detail})" if separator and detail else "")


def _register_translations() -> None:
    global _TRANSLATIONS_REGISTERED
    if bpy is None or _TRANSLATIONS_REGISTERED:
        return
    try:
        bpy.app.translations.register(__name__, TRANSLATIONS)
    except ValueError:
        bpy.app.translations.unregister(__name__)
        bpy.app.translations.register(__name__, TRANSLATIONS)
    _TRANSLATIONS_REGISTERED = True


def _unregister_translations() -> None:
    global _TRANSLATIONS_REGISTERED
    if bpy is None or not _TRANSLATIONS_REGISTERED:
        return
    try:
        bpy.app.translations.unregister(__name__)
    except (RuntimeError, ValueError):
        pass
    _TRANSLATIONS_REGISTERED = False
GAME_WORKFLOWS = frozenset(
    {"genshin_toon", "wuwa_toon", "hsr_toon", "endfield_toon"}
)
_SOURCE_ID_PROPERTY = "miku_source_id"
_SOURCE_ORIGIN_PROPERTY = "_miku_source_identity_origin"
_MATERIAL_ID_PROPERTY = "miku_material_id"
_LEGACY_SOURCE_ID_PROPERTY = "migr_source_id"
_LEGACY_MATERIAL_ID_PROPERTY = "migr_material_id"
_WORKFLOW_MIKUATION_PROPERTY = "_miku_workflow_explicit_1_0"
_LEGACY_IDENTITY_REGISTRY = ".migr-identities.json"
_MAX_IDENTITY_DOCUMENT_BYTES = 16 * 1024 * 1024
_MAX_IDENTITY_SCAN_DIRECTORIES = 10_000
_INVALID_ASSET_CHARS = re.compile(r'[<>:"/\\|?*\x00-\x1f]')
_RESERVED_ASSET_NAMES = {
    "CON",
    "PRN",
    "AUX",
    "NUL",
    *(f"COM{index}" for index in range(1, 10)),
    *(f"LPT{index}" for index in range(1, 10)),
}
_REGISTERED_CLASSES: list[type] = []
_TRANSLATIONS_REGISTERED = False
_PENDING_WORKFLOW_MIKUATIONS: set[int] = set()
_SESSION_SOURCE_IDS: dict[int, tuple[Any, str]] = {}
_SESSION_MATERIAL_IDS: dict[int, tuple[Any, str]] = {}
_MIKU_TIME_SEMANTIC = "Input.Time"
_MIKU_TIME_SEMANTIC_VERSION = 1
_MIKU_TIME_CONTRACT = "miku_time_v1"
_SAFE_DRIVER_CALLS = frozenset(
    {
        "abs",
        "acos",
        "asin",
        "atan",
        "atan2",
        "ceil",
        "cos",
        "exp",
        "floor",
        "log",
        "max",
        "min",
        "pow",
        "round",
        "sin",
        "sqrt",
        "tan",
    }
)
_DRIVER_PATH = re.compile(
    r'^nodes\["(?P<node>(?:[^"\\]|\\.)+)"\]\.'
    r'(?P<direction>inputs|outputs)\[(?P<socket>\d+|".*")\]\.default_value$'
)


NODE_OPS = {
    "ShaderNodeOutputMaterial": "Output.Material",
    "ShaderNodeBsdfPrincipled": "Shader.PrincipledBSDF",
    "ShaderNodeBsdfDiffuse": "Shader.DiffuseBSDF",
    "ShaderNodeBsdfAnisotropic": "Shader.AnisotropicBSDF",
    "ShaderNodeBsdfGlossy": "Shader.GlossyBSDF",
    "ShaderNodeBsdfMetallic": "Shader.MetallicBSDF",
    "ShaderNodeBsdfTransparent": "Shader.TransparentBSDF",
    "ShaderNodeBsdfGlass": "Shader.GlassBSDF",
    "ShaderNodeBsdfRefraction": "Shader.RefractionBSDF",
    "ShaderNodeBsdfTranslucent": "Shader.TranslucentBSDF",
    "ShaderNodeSubsurfaceScattering": "Shader.SubsurfaceScattering",
    "ShaderNodeBsdfSheen": "Shader.SheenBSDF",
    "ShaderNodeBsdfHair": "Shader.HairBSDF",
    "ShaderNodeBsdfToon": "Shader.ToonBSDF",
    "ShaderNodeVolumePrincipled": "Shader.Volume",
    "ShaderNodeVolumeAbsorption": "Shader.Volume",
    "ShaderNodeVolumeScatter": "Shader.Volume",
    "ShaderNodeHoldout": "Shader.Holdout",
    "ShaderNodeShaderToRGB": "Shader.ToRGB",
    "ShaderNodeEmission": "Shader.Emission",
    "ShaderNodeMixShader": "Shader.Mix",
    "ShaderNodeAddShader": "Shader.Add",
    "ShaderNodeTexCoord": "Input.TextureCoordinate",
    "ShaderNodeNewGeometry": "Input.Geometry",
    "ShaderNodeAmbientOcclusion": "Input.AmbientOcclusion",
    "ShaderNodeLayerWeight": "Input.LayerWeight",
    "ShaderNodeFresnel": "Input.Fresnel",
    "ShaderNodeCameraData": "Input.CameraData",
    "ShaderNodeValue": "Input.Value",
    "ShaderNodeRGB": "Input.Color",
    "ShaderNodeUVMap": "Input.UVMap",
    "ShaderNodeWireframe": "Input.Wireframe",
    "ShaderNodeBump": "Vector.Bump",
    "ShaderNodeNormalMap": "Vector.NormalMap",
    "ShaderNodeBevel": "Vector.Bevel",
    "ShaderNodeDisplacement": "Vector.Displacement",
    "ShaderNodeLightPath": "Input.LightPath",
    "ShaderNodeTexImage": "Texture.Image",
    "ShaderNodeTexNoise": "Texture.Noise",
    "ShaderNodeTexVoronoi": "Texture.Voronoi",
    "ShaderNodeTexWave": "Texture.Wave",
    "ShaderNodeTexBrick": "Texture.Brick",
    "ShaderNodeTexChecker": "Texture.Checker",
    "ShaderNodeTexGradient": "Texture.Gradient",
    "ShaderNodeTexMagic": "Texture.Magic",
    "ShaderNodeTexWhiteNoise": "Texture.WhiteNoise",
    "ShaderNodeValToRGB": "Color.Ramp",
    "ShaderNodeHueSaturation": "Color.HueSaturationValue",
    "ShaderNodeMix": "Math.Mix",
    "ShaderNodeMixRGB": "Color.Mix",
    "ShaderNodeMath": "Math",
    "ShaderNodeVectorMath": "VectorMath",
    "ShaderNodeMapping": "Vector.Mapping",
    "ShaderNodeMapRange": "Math.MapRange",
    "ShaderNodeClamp": "Converter.Clamp",
    "ShaderNodeInvert": "Color.Invert",
    "ShaderNodeBrightContrast": "Color.BrightContrast",
    "ShaderNodeGamma": "Color.Gamma",
    "ShaderNodeRGBToBW": "Color.RGBToBW",
    "ShaderNodeRGBCurve": "Color.RGBCurve",
    "ShaderNodeSeparateColor": "Converter.SeparateColor",
    "ShaderNodeSeparateXYZ": "Converter.SeparateXYZ",
    "ShaderNodeCombineColor": "Converter.CombineColor",
    "ShaderNodeCombineRGB": "Converter.CombineColor",
    "ShaderNodeCombineXYZ": "Converter.CombineXYZ",
    "NodeReroute": "Utility.Reroute",
    "NodeGroupInput": "Interface.GroupInput",
    "NodeGroupOutput": "Interface.GroupOutput",
}


def _node_stable_id(node: Any, namespace: str = "") -> str:
    local_id = hashlib.sha256(
        f"{getattr(node, 'name', '')}|{getattr(node, 'bl_idname', '')}".encode(
            "utf-8"
        )
    ).hexdigest()[:20]
    if not namespace:
        return local_id
    return hashlib.sha256(f"{namespace}|{local_id}".encode("utf-8")).hexdigest()[:20]


def _id_value(owner: Any, name: str, default: Any = None) -> Any:
    getter = getattr(owner, "get", None)
    if callable(getter):
        try:
            return getter(name, default)
        except (AttributeError, ReferenceError, RuntimeError, TypeError):
            pass
    return getattr(owner, name, default)


def _time_contract() -> dict[str, Any]:
    scene = getattr(getattr(bpy, "context", None), "scene", None) if bpy else None
    render = getattr(scene, "render", None)
    fps = float(getattr(render, "fps", 24.0) or 24.0)
    fps_base = float(getattr(render, "fps_base", 1.0) or 1.0)
    source_fps = fps / fps_base if fps_base else fps
    return {
        "contract": _MIKU_TIME_CONTRACT,
        "sourceFps": source_fps,
        "frameStart": int(getattr(scene, "frame_start", 1) or 1),
    }


def _time_group_contract(node: Any) -> dict[str, Any] | None:
    if str(getattr(node, "bl_idname", "")) != "ShaderNodeGroup":
        return None
    group = getattr(node, "node_tree", None)
    if group is None:
        return None
    if str(_id_value(group, "miku.semantic", "")) != _MIKU_TIME_SEMANTIC:
        return None
    try:
        version = int(_id_value(group, "miku.semanticVersion", 0))
    except (TypeError, ValueError):
        version = 0
    if version != _MIKU_TIME_SEMANTIC_VERSION:
        raise RuntimeError(
            f"MIKU_TIME_CONTRACT_VERSION_UNSUPPORTED:{version}"
        )
    return {
        **_time_contract(),
        "contract": str(
            _id_value(group, "miku.contract", _MIKU_TIME_CONTRACT)
        ),
        "sourceFps": float(
            _id_value(group, "miku.sourceFps", _time_contract()["sourceFps"])
        ),
        "frameStart": int(
            _id_value(group, "miku.frameStart", _time_contract()["frameStart"])
        ),
    }


def _socket_semantics(
    op: str,
    socket_name: str,
    *,
    output: bool,
) -> dict[str, Any]:
    socket = _normalize_socket_name(socket_name)
    if output and op == "Input.CameraData":
        return {
            "viewvector": {
                "valueType": "Float3",
                "space": "View",
                "stage": "Fragment",
                "uniformity": "Varying",
            },
            "viewzdepth": {
                "valueType": "Scalar",
                "space": "View",
                "stage": "Fragment",
                "uniformity": "Varying",
            },
            "viewdistance": {
                "valueType": "Scalar",
                "space": "None",
                "stage": "Fragment",
                "uniformity": "Varying",
            },
        }.get(socket, {})
    if output and op == "Input.Geometry":
        if socket == "incoming":
            return {
                "valueType": "Float3",
                "space": "World",
                "stage": "Fragment",
                "uniformity": "Varying",
            }
        if socket == "backfacing":
            return {
                "valueType": "Scalar",
                "space": "None",
                "stage": "Fragment",
                "uniformity": "Varying",
            }
    if output and op in {"Input.Fresnel", "Input.LayerWeight"}:
        return {
            "valueType": "Scalar",
            "space": "None",
            "stage": "Fragment",
            "uniformity": "Varying",
        }
    if output and op == "Input.Time":
        return {
            "valueType": "Scalar",
            "space": "None",
            "stage": "Both",
            "uniformity": "Uniform",
        }
    return {}


def _normalize_socket_name(value: Any) -> str:
    return "".join(character for character in str(value or "").lower() if character.isalnum())


def _socket_value(socket: Any) -> Any:
    return _portable_value(getattr(socket, "default_value", None))


def _portable_value(value: Any, depth: int = 0) -> Any:
    if depth > 8:
        raise RuntimeError("MIKU_SOURCE_VALUE_NESTING_LIMIT")
    if value is None or isinstance(value, (str, bool, int)):
        return value
    if isinstance(value, float):
        if not math.isfinite(value):
            raise RuntimeError("MIKU_SOURCE_VALUE_NONFINITE")
        return value
    if isinstance(value, Mapping):
        return {str(key): _portable_value(item, depth + 1) for key, item in value.items()}
    try:
        return [_portable_value(item, depth + 1) for item in value]
    except TypeError:
        name = getattr(value, "name", None)
        if isinstance(name, str):
            return name
        raise RuntimeError("MIKU_SOURCE_VALUE_UNSUPPORTED:" + type(value).__name__)


def _snapshot_node(
    node: Any,
    namespace: str = "",
    *,
    group_path: tuple[str, ...] = ("Material",),
) -> dict[str, Any]:
    node_id = _node_stable_id(node, namespace)
    time_contract = _time_group_contract(node)
    blender_node_type = str(getattr(node, "bl_idname", ""))
    op = (
        _MIKU_TIME_SEMANTIC
        if time_contract is not None
        else NODE_OPS.get(
            blender_node_type,
            "Opaque.BlenderNode",
        )
    )
    if blender_node_type == "ShaderNodeBsdfAnisotropic":
        node_type = str(getattr(node, "type", "") or "")
        anisotropy = next(
            (
                socket
                for socket in getattr(node, "inputs", []) or []
                if "anisotropic"
                in _normalize_socket_name(getattr(socket, "name", ""))
            ),
            None,
        )
        anisotropy_value = (
            float(getattr(anisotropy, "default_value", 0.0) or 0.0)
            if anisotropy is not None
            else 0.0
        )
        if (
            node_type == "BSDF_GLOSSY"
            and not bool(getattr(anisotropy, "is_linked", False))
            and abs(anisotropy_value) <= 1.0e-8
        ):
            op = "Shader.GlossyBSDF"
    inputs = []
    for socket in getattr(node, "inputs", []) or []:
        socket_name = str(getattr(socket, "name", ""))
        inputs.append(
            {
                "id": str(getattr(socket, "identifier", "") or socket_name),
                "name": socket_name,
                "valueType": str(getattr(socket, "type", "UNKNOWN")),
                "default": _socket_value(socket),
                "enabled": bool(getattr(socket, "enabled", True)),
                "isUnavailable": bool(
                    getattr(socket, "is_unavailable", False)
                ),
                **_socket_semantics(op, socket_name, output=False),
            }
        )
    outputs = []
    for socket in getattr(node, "outputs", []) or []:
        socket_name = str(getattr(socket, "name", ""))
        outputs.append(
            {
                "id": str(getattr(socket, "identifier", "") or socket_name),
                "name": socket_name,
                "valueType": str(getattr(socket, "type", "UNKNOWN")),
                "default": _socket_value(socket),
                **_socket_semantics(op, socket_name, output=True),
            }
        )
    params = dict(time_contract or {})
    if op == "Output.Material":
        params["isActiveOutput"] = bool(
            getattr(node, "is_active_output", False)
        )
        params["target"] = str(getattr(node, "target", "ALL") or "ALL")
    for key in (
        "operation",
        "data_type",
        "dimensions",
        "normalize",
        "clamp",
        "blend_type",
        "distribution",
        "subsurface_method",
        "invert",
        "gradient_type",
        "wave_type",
        "bands_direction",
        "rings_direction",
        "wave_profile",
        "distance",
        "feature",
        "voronoi_dimensions",
        "uv_map",
    ):
        if hasattr(node, key):
            value = getattr(node, key)
            params[key] = _portable_value(getattr(value, "name", value))
    if op == "Texture.Noise":
        params["noiseDimensions"] = str(
            getattr(node, "noise_dimensions", "3D") or "3D"
        ).upper()
    if op == "Vector.Mapping":
        params["vectorType"] = str(
            getattr(node, "vector_type", "POINT") or "POINT"
        ).upper()
    if op == "Vector.NormalMap":
        params["space"] = str(
            getattr(node, "space", "TANGENT") or "TANGENT"
        ).upper()
        params["uvMap"] = str(getattr(node, "uv_map", "") or "")
    if op == "Vector.Displacement":
        params["space"] = str(
            getattr(node, "space", "OBJECT") or "OBJECT"
        ).upper()
    if op == "Converter.SeparateColor":
        params["mode"] = str(
            getattr(node, "mode", "RGB") or "RGB"
        ).upper()
    if op == "Texture.Image":
        image = getattr(node, "image", None)
        image_source = str(
            getattr(image, "source", "") or ""
        ).upper()
        image_format = str(
            getattr(image, "file_format", "") or ""
        ).upper()
        size = list(getattr(image, "size", ()) or ())
        color_settings = getattr(image, "colorspace_settings", None)
        color_space_name = str(
            getattr(color_settings, "name", "") or ""
        )
        params["image"] = {
            "resourceBaseId": hashlib.sha256(
                (
                    f"{getattr(image, 'name_full', '') or getattr(image, 'name', '')}|"
                    f"{image_source}|{image_format}|"
                    f"{int(size[0]) if len(size) > 0 else 0}x"
                    f"{int(size[1]) if len(size) > 1 else 0}"
                ).encode("utf-8")
            ).hexdigest(),
            "name": str(getattr(image, "name", "") or ""),
            "source": image_source,
            "fileFormat": image_format,
            "width": int(size[0]) if len(size) > 0 else 0,
            "height": int(size[1]) if len(size) > 1 else 0,
            "channels": int(getattr(image, "channels", 4) or 4),
            "colorSpaceName": color_space_name,
            "packed": bool(getattr(image, "packed_file", None)),
        }
        params["projection"] = str(
            getattr(node, "projection", "FLAT") or "FLAT"
        ).upper()
        params["interpolation"] = str(
            getattr(node, "interpolation", "Linear") or "Linear"
        ).upper()
        params["extension"] = str(
            getattr(node, "extension", "REPEAT") or "REPEAT"
        ).upper()
        params["mikuTextureRole"] = str(
            getattr(node, "miku_texture_role", "AUTO") or "AUTO"
        )
    if op == "Color.Ramp":
        ramp = getattr(node, "color_ramp", None)
        if ramp is not None:
            params["colorRamp"] = {
                "interpolation": str(getattr(ramp, "interpolation", "LINEAR")),
                "colorMode": str(getattr(ramp, "color_mode", "RGB")),
                "hueInterpolation": str(
                    getattr(ramp, "hue_interpolation", "NEAR")
                ),
                "elements": [
                    {
                        "position": float(getattr(element, "position", 0.0)),
                        "color": _portable_value(
                            getattr(element, "color", [0.0, 0.0, 0.0, 1.0])
                        ),
                    }
                    for element in getattr(ramp, "elements", []) or []
                ],
            }
    return {
        "id": node_id,
        "op": op,
        "inputs": inputs,
        "outputs": outputs,
        "params": params,
        "source": {
            "stableId": node_id,
            "displayName": str(getattr(node, "name", "")),
            "label": str(getattr(node, "label", "") or ""),
            "blenderNodeName": str(getattr(node, "name", "")),
            "blenderNodeType": blender_node_type,
            "blenderNodeKind": str(getattr(node, "type", "") or ""),
            "groupPath": list(group_path),
        },
    }


def _socket_identifier(socket: Any) -> str:
    return str(
        getattr(socket, "identifier", "")
        or getattr(socket, "name", "")
    )


def _snapshot_tree(
    tree: Any,
    *,
    namespace: str = "",
    group_path: tuple[str, ...] = ("Material",),
    ancestors: frozenset[int] = frozenset(),
) -> tuple[
    list[dict[str, Any]],
    list[dict[str, Any]],
    dict[str, list[dict[str, str]]],
    dict[str, dict[str, str]],
]:
    """Flatten ordinary node groups while retaining their interface wiring."""

    if tree is None:
        return [], [], {}, {}
    tree_identity = id(tree)
    if tree_identity in ancestors:
        raise RuntimeError("MIKU_NODE_GROUP_RECURSION_UNSUPPORTED")
    nested_ancestors = ancestors | {tree_identity}
    source_nodes = list(getattr(tree, "nodes", []) or [])
    nodes: list[dict[str, Any]] = []
    edges: list[dict[str, Any]] = []
    normal_nodes: dict[str, dict[str, Any]] = {}
    groups: dict[str, dict[str, Any]] = {}

    for node in source_nodes:
        name = str(getattr(node, "name", ""))
        node_type = str(getattr(node, "bl_idname", ""))
        if node_type in {"NodeGroupInput", "NodeGroupOutput"}:
            continue
        if (
            node_type == "ShaderNodeGroup"
            and _time_group_contract(node) is None
            and getattr(node, "node_tree", None) is not None
        ):
            instance_id = _node_stable_id(node, namespace)
            child_namespace = (
                f"{namespace}/{instance_id}" if namespace else instance_id
            )
            child_nodes, child_edges, child_inputs, child_outputs = (
                _snapshot_tree(
                    getattr(node, "node_tree", None),
                    namespace=child_namespace,
                    group_path=(*group_path, name),
                    ancestors=nested_ancestors,
                )
            )
            if child_nodes and child_outputs:
                nodes.extend(child_nodes)
                edges.extend(child_edges)
                groups[name] = {
                    "node": node,
                    "inputs": child_inputs,
                    "outputs": child_outputs,
                }
                continue
        snapshot = _snapshot_node(
            node,
            namespace,
            group_path=group_path,
        )
        nodes.append(snapshot)
        normal_nodes[name] = snapshot

    interface_inputs: dict[str, list[dict[str, str]]] = {}
    interface_outputs: dict[str, dict[str, str]] = {}

    def source_endpoint(node: Any, socket: Any) -> tuple[str, Any]:
        name = str(getattr(node, "name", ""))
        node_type = str(getattr(node, "bl_idname", ""))
        socket_id = _socket_identifier(socket)
        if node_type == "NodeGroupInput":
            return "interface", socket_id
        if name in groups:
            return "endpoint", groups[name]["outputs"].get(socket_id)
        snapshot = normal_nodes.get(name)
        if snapshot is None:
            return "missing", None
        return "endpoint", {"node": snapshot["id"], "socket": socket_id}

    def target_endpoints(node: Any, socket: Any) -> tuple[str, Any]:
        name = str(getattr(node, "name", ""))
        node_type = str(getattr(node, "bl_idname", ""))
        socket_id = _socket_identifier(socket)
        if node_type == "NodeGroupOutput":
            return "interface", socket_id
        if name in groups:
            return "endpoints", list(groups[name]["inputs"].get(socket_id, []))
        snapshot = normal_nodes.get(name)
        if snapshot is None:
            return "missing", []
        return "endpoints", [{"node": snapshot["id"], "socket": socket_id}]

    linked_group_inputs: dict[str, set[str]] = {
        name: set() for name in groups
    }
    for link in getattr(tree, "links", []) or []:
        from_node = getattr(link, "from_node", None)
        to_node = getattr(link, "to_node", None)
        from_socket = getattr(link, "from_socket", None)
        to_socket = getattr(link, "to_socket", None)
        to_name = str(getattr(to_node, "name", ""))
        if to_name in groups:
            linked_group_inputs[to_name].add(_socket_identifier(to_socket))
        source_kind, source = source_endpoint(from_node, from_socket)
        target_kind, targets = target_endpoints(to_node, to_socket)
        if source_kind == "missing" or target_kind == "missing":
            continue
        if source_kind == "interface":
            if target_kind == "endpoints":
                interface_inputs.setdefault(str(source), []).extend(targets)
            continue
        if source is None:
            continue
        if target_kind == "interface":
            interface_outputs[str(targets)] = source
            continue
        for target in targets:
            edges.append({"from": dict(source), "to": dict(target)})

    nodes_by_id = {str(node["id"]): node for node in nodes}
    for group_name, group in groups.items():
        linked = linked_group_inputs[group_name]
        for socket in getattr(group["node"], "inputs", []) or []:
            socket_id = _socket_identifier(socket)
            if socket_id in linked:
                continue
            default = _socket_value(socket)
            for endpoint in group["inputs"].get(socket_id, []):
                target_node = nodes_by_id.get(str(endpoint.get("node") or ""))
                if target_node is None:
                    continue
                target_socket = _normalize_socket_name(endpoint.get("socket"))
                for record in target_node.get("inputs", []) or []:
                    if _normalize_socket_name(
                        record.get("id") or record.get("name")
                    ) == target_socket:
                        record["default"] = default

    for socket_id, targets in interface_inputs.items():
        deduplicated = {
            (str(item["node"]), str(item["socket"])): item
            for item in targets
        }
        interface_inputs[socket_id] = [
            deduplicated[key] for key in sorted(deduplicated)
        ]
    return nodes, edges, interface_inputs, interface_outputs


def _snapshot_input(
    node: Mapping[str, Any],
    incoming: Mapping[tuple[str, str], dict[str, str]],
    socket_names: tuple[str, ...],
    fallback: Any,
    value_type: str | None = None,
) -> dict[str, Any]:
    node_id = str(node.get("id") or "")
    records = [
        item
        for item in node.get("inputs", []) or []
        if isinstance(item, Mapping)
        and bool(item.get("enabled", True))
        and not bool(item.get("isUnavailable", False))
    ]
    normalized_types = {
        "COLOR": "Color",
        "RGBA": "Color",
        "FLOAT": "Scalar",
        "VALUE": "Scalar",
        "VECTOR": "Float3",
    }

    def matching_records(*, identifier: bool) -> list[Mapping[str, Any]]:
        matches: list[Mapping[str, Any]] = []
        for name in socket_names:
            normalized_name = _normalize_socket_name(name)
            for item in records:
                candidate = item.get("id") if identifier else item.get("name")
                if _normalize_socket_name(candidate) != normalized_name:
                    continue
                if value_type is not None:
                    raw_type = str(item.get("valueType") or "")
                    resolved_type = normalized_types.get(
                        raw_type.upper(),
                        raw_type,
                    )
                    if resolved_type != value_type:
                        continue
                if item not in matches:
                    matches.append(item)
        return matches

    candidates = matching_records(identifier=True)
    if not candidates:
        candidates = matching_records(identifier=False)
    if len(candidates) > 1:
        raise RuntimeError(
            "MIKU_SOCKET_AMBIGUOUS:"
            f"{node_id}:{'/'.join(socket_names)}:{value_type or 'Any'}"
        )
    record = candidates[0] if candidates else None
    if record is None:
        return {"default": fallback}
    socket_id = str(record.get("id") or record.get("name") or "")
    source = incoming.get((node_id, _normalize_socket_name(socket_id)))
    if source is not None:
        return {"default": None, "source": dict(source)}
    value = record.get("default")
    return {"default": fallback if value is None else value}


def _principled_slots_from_snapshot(
    nodes: list[dict[str, Any]],
    edges: list[dict[str, Any]],
    *,
    displacement_method: str = "BUMP",
    displacement_policy: str = "FOLLOW_BLENDER",
) -> tuple[
    dict[str, dict[str, Any]],
    dict[str, Any],
    list[dict[str, Any]],
]:
    """Flatten the active surface closure into editable Standard PBR slots."""

    by_id = {str(node.get("id") or ""): node for node in nodes}
    incoming: dict[tuple[str, str], dict[str, str]] = {}
    for edge in edges:
        source = edge.get("from") or {}
        target = edge.get("to") or {}
        incoming[
            (
                str(target.get("node") or ""),
                _normalize_socket_name(target.get("socket")),
            )
        ] = {
            "node": str(source.get("node") or ""),
            "socket": str(source.get("socket") or ""),
        }
    outputs = [
        node for node in nodes if node.get("op") == "Output.Material"
    ]
    output = min(
        outputs,
        key=lambda node: (
            not bool((node.get("params") or {}).get("isActiveOutput")),
            str((node.get("params") or {}).get("target") or "ALL")
            not in {"EEVEE", "ALL"},
            str((node.get("params") or {}).get("target") or "ALL") != "EEVEE",
            str(node.get("id") or ""),
        ),
        default=None,
    )
    if output is None:
        return {}, {}, []
    surface = incoming.get((str(output["id"]), "surface"))
    if surface is None:
        return {}, {}, []
    displacement = incoming.get((str(output["id"]), "displacement"))
    diagnostics: list[dict[str, Any]] = []
    fallbacks = {
        "BaseColor": [0.8, 0.8, 0.8, 1.0],
        "Metalness": 0.0,
        "Roughness": 0.5,
        "Normal": [0.0, 0.0, 1.0],
        "Emission": [0.0, 0.0, 0.0, 1.0],
        "Alpha": 1.0,
        "AmbientOcclusion": 1.0,
        "TransmissionColor": [1.0, 1.0, 1.0, 1.0],
        "TransmissionWeight": 0.0,
        "IOR": 1.5,
        "Thickness": 0.1,
    }
    socket_names = {
        "BaseColor": ("Base Color",),
        "Metalness": ("Metallic",),
        "Roughness": ("Roughness",),
        "Normal": ("Normal",),
        "Emission": ("Emission Color", "Emission"),
        "Alpha": ("Alpha",),
    }
    socket_types = {
        "BaseColor": "Color",
        "Metalness": "Scalar",
        "Roughness": "Scalar",
        "Normal": "Float3",
        "Emission": "Color",
        "Alpha": "Scalar",
    }

    def synthetic_expression(
        *,
        owner: Mapping[str, Any],
        semantic: str,
        op: str,
        inputs: tuple[tuple[str, dict[str, Any], Any, str], ...],
        value_type: str,
        suffix: str,
        params: Mapping[str, Any] | None = None,
    ) -> dict[str, Any]:
        owner_id = str(owner.get("id") or "")
        synthetic_id = hashlib.sha256(
            f"{owner_id}|{suffix}|{semantic}".encode("utf-8")
        ).hexdigest()[:20]
        synthetic = {
            "id": synthetic_id,
            "op": op,
            "inputs": [
                {
                    "id": socket_id,
                    "name": socket_id,
                    "valueType": input_type,
                    "default": slot.get("default", fallback),
                }
                for socket_id, slot, fallback, input_type in inputs
            ],
            "outputs": [
                {
                    "id": "Result",
                    "name": "Result",
                    "valueType": value_type,
                }
            ],
            "params": {"semantic": semantic, **dict(params or {})},
            "source": {
                "stableId": owner_id,
                "displayName": f"Miku {semantic} {suffix}",
            },
        }
        nodes.append(synthetic)
        by_id[synthetic_id] = synthetic
        for socket_id, slot, _fallback, _input_type in inputs:
            source = slot.get("source")
            if source is not None:
                edge = {
                    "from": dict(source),
                    "to": {"node": synthetic_id, "socket": socket_id},
                }
                edges.append(edge)
                incoming[
                    (synthetic_id, _normalize_socket_name(socket_id))
                ] = dict(source)
        return {
            "default": None,
            "source": {"node": synthetic_id, "socket": "Result"},
        }

    def closure_warning(node: Mapping[str, Any], reason: str) -> None:
        diagnostics.append(
            {
                "severity": "warning",
                "code": "MIKU_CLOSURE_FLATTENED_APPROXIMATE",
                "translationQuality": "Approximate",
                "nodeId": str(node.get("id") or ""),
                "message": reason,
            }
        )

    def emission_with_strength(
        owner: Mapping[str, Any],
        color: dict[str, Any],
        strength: dict[str, Any],
    ) -> dict[str, Any]:
        if color.get("source") is None and strength.get("source") is None:
            strength_default = strength.get("default")
            strength_value = float(
                1.0 if strength_default is None else strength_default
            )
            color_value = color.get("default")
            if not isinstance(color_value, (list, tuple)):
                color_value = fallbacks["Emission"]
            return {
                "default": [
                    float(component) * strength_value
                    for component in color_value
                ]
            }
        strength_default = strength.get("default")
        if (
            strength.get("source") is not None
            or float(1.0 if strength_default is None else strength_default)
            != 1.0
        ):
            return synthetic_expression(
                owner=owner,
                semantic="Emission",
                op="Math.Multiply",
                inputs=(
                    ("A", color, fallbacks["Emission"], "Color"),
                    ("B", strength, 1.0, "Scalar"),
                ),
                value_type="Color",
                suffix="emission-strength",
            )
        return color

    def extract_ao_from_base_color(
        base_color: dict[str, Any],
    ) -> tuple[dict[str, Any], dict[str, Any] | None]:
        """Recognize the explicit Blender PBR BaseColor × AO topology."""

        source = base_color.get("source")
        if not isinstance(source, Mapping):
            return base_color, None
        mix_node = by_id.get(str(source.get("node") or ""))
        if not isinstance(mix_node, Mapping):
            return base_color, None
        if str(mix_node.get("op") or "") not in {
            "Math.Mix",
            "Color.Mix",
        }:
            return base_color, None
        mix_params = (
            mix_node.get("params")
            if isinstance(mix_node.get("params"), Mapping)
            else {}
        )
        if str(mix_params.get("blend_type") or "MIX").upper() != "MULTIPLY":
            return base_color, None
        base = _snapshot_input(
            mix_node,
            incoming,
            ("A", "Color1", "Color 1"),
            fallbacks["BaseColor"],
            "Color",
        )
        ao_color = _snapshot_input(
            mix_node,
            incoming,
            ("B", "Color2", "Color 2"),
            [1.0, 1.0, 1.0, 1.0],
            "Color",
        )
        factor = _snapshot_input(
            mix_node,
            incoming,
            ("Factor", "Fac"),
            1.0,
            "Scalar",
        )
        ao_default = ao_color.get("default")
        if isinstance(ao_default, (list, tuple)):
            ao_color = {
                **ao_color,
                "default": (
                    float(ao_default[0])
                    if ao_default
                    else 1.0
                ),
            }
        ao = synthetic_expression(
            owner=mix_node,
            semantic="AmbientOcclusion",
            op="Math.Mix",
            inputs=(
                ("Factor", factor, 1.0, "Scalar"),
                ("A", {"default": 1.0}, 1.0, "Scalar"),
                ("B", ao_color, 1.0, "Scalar"),
            ),
            value_type="Scalar",
            suffix="basecolor-ao-multiply",
        )
        diagnostics.append(
            {
                "severity": "info",
                "code": "MIKU_AO_BASECOLOR_MULTIPLY_RECOGNIZED",
                "translationQuality": "Equivalent",
                "nodeId": str(mix_node.get("id") or ""),
                "message": (
                    "Recognized Base Color multiplied by Ambient Occlusion; "
                    "Unity will apply AO once in the Base Color path."
                ),
            }
        )
        return base, ao

    def closure_state(
        endpoint: Mapping[str, str],
        visiting: frozenset[str] = frozenset(),
    ) -> dict[str, Any]:
        node_id = str(endpoint.get("node") or "")
        if node_id in visiting:
            raise RuntimeError("MIKU_CLOSURE_GRAPH_CYCLE")
        node = by_id.get(node_id)
        if node is None:
            return {}
        op = str(node.get("op") or "")
        if op == "Shader.PrincipledBSDF":
            slots = {
                semantic: _snapshot_input(
                    node,
                    incoming,
                    names,
                    fallbacks[semantic],
                    socket_types[semantic],
                )
                for semantic, names in socket_names.items()
            }
            emission_strength = _snapshot_input(
                node,
                incoming,
                ("Emission Strength",),
                1.0,
                "Scalar",
            )
            slots["Emission"] = emission_with_strength(
                node,
                slots["Emission"],
                emission_strength,
            )
            (
                slots["BaseColor"],
                authored_ao,
            ) = extract_ao_from_base_color(slots["BaseColor"])
            slots["AmbientOcclusion"] = (
                authored_ao
                if authored_ao is not None
                else {"default": fallbacks["AmbientOcclusion"]}
            )
            slots["TransmissionColor"] = {
                "default": fallbacks["TransmissionColor"]
            }
            slots["TransmissionWeight"] = {
                "default": fallbacks["TransmissionWeight"]
            }
            slots["IOR"] = _snapshot_input(
                node, incoming, ("IOR",), fallbacks["IOR"]
            )
            slots["Thickness"] = {"default": fallbacks["Thickness"]}
            return {
                "slots": slots,
                "model": "StandardLit",
                "coverage": slots["Alpha"],
                "usesTransparency": (
                    slots["Alpha"].get("source") is not None
                    or float(slots["Alpha"].get("default") or 0.0) < 1.0
                ),
                "requiredChannels": {
                    "BaseColor",
                    "Metalness",
                    "Roughness",
                    "Normal",
                    "Alpha",
                    *(
                        {"AmbientOcclusion"}
                        if authored_ao is not None
                        else set()
                    ),
                },
            }
        if op == "Shader.DiffuseBSDF":
            closure_warning(
                node,
                "Flattened Blender Diffuse BSDF to URP Lit diffuse channels; "
                "Diffuse Roughness is approximated by the Lit roughness model.",
            )
            slots = {
                "BaseColor": _snapshot_input(
                    node, incoming, ("Color",), fallbacks["BaseColor"]
                ),
                "Metalness": {"default": 0.0},
                "Roughness": _snapshot_input(
                    node, incoming, ("Roughness",), fallbacks["Roughness"]
                ),
                "Normal": _snapshot_input(
                    node, incoming, ("Normal",), fallbacks["Normal"]
                ),
                "Emission": {"default": fallbacks["Emission"]},
                "Alpha": {"default": fallbacks["Alpha"]},
                "AmbientOcclusion": {
                    "default": fallbacks["AmbientOcclusion"]
                },
            }
            return {
                "slots": slots,
                "model": "StandardLit",
                "coverage": slots["Alpha"],
                "usesTransparency": False,
                "requiredChannels": {
                    "BaseColor",
                    "Roughness",
                    "Normal",
                    "Alpha",
                },
            }
        if op == "Shader.AnisotropicBSDF":
            anisotropy = _snapshot_input(
                node, incoming, ("Anisotropy",), 0.0
            )
            if anisotropy.get("source") is not None or abs(
                float(anisotropy.get("default") or 0.0)
            ) > 1.0e-8:
                raise RuntimeError(
                    "MIKU_CLOSURE_LOWERING_UNSUPPORTED:"
                    f"{node_id}:nonzero-anisotropy"
                )
            closure_warning(
                node,
                "Flattened zero-anisotropy Glossy/Anisotropic BSDF to "
                "metallic URP Lit channels; Beckmann is approximated by GGX.",
            )
            slots = {
                "BaseColor": _snapshot_input(
                    node, incoming, ("Color",), fallbacks["BaseColor"]
                ),
                "Metalness": {"default": 1.0},
                "Roughness": _snapshot_input(
                    node, incoming, ("Roughness",), fallbacks["Roughness"]
                ),
                "Normal": _snapshot_input(
                    node, incoming, ("Normal",), fallbacks["Normal"]
                ),
                "Emission": {"default": fallbacks["Emission"]},
                "Alpha": _snapshot_input(
                    node, incoming, ("Alpha",), fallbacks["Alpha"]
                ),
                "AmbientOcclusion": {
                    "default": fallbacks["AmbientOcclusion"]
                },
            }
            return {
                "slots": slots,
                "model": "StandardLit",
                "coverage": slots["Alpha"],
                "usesTransparency": (
                    slots["Alpha"].get("source") is not None
                    or float(slots["Alpha"].get("default") or 0.0) < 1.0
                ),
                "requiredChannels": {
                    "BaseColor",
                    "Roughness",
                    "Normal",
                    "Alpha",
                },
            }
        if op == "Shader.TransparentBSDF":
            color = _snapshot_input(
                node, incoming, ("Color",), [1.0, 1.0, 1.0, 1.0]
            )
            if color.get("source") is not None or color.get("default") is not None:
                default_color = color.get("default")
                if (
                    default_color is not None
                    and list(default_color)[:3] != [1.0, 1.0, 1.0]
                ):
                    diagnostics.append(
                        {
                            "severity": "warning",
                            "code": "MIKU_COLORED_TRANSPARENCY_APPROXIMATE",
                            "translationQuality": "Approximate",
                            "nodeId": node_id,
                            "message": (
                                "Colored Transparent BSDF background tint cannot "
                                "be represented exactly by URP coverage."
                            ),
                        }
                    )
            slots = {
                "BaseColor": {"default": [0.0, 0.0, 0.0, 1.0]},
                "Metalness": {"default": 0.0},
                "Roughness": {"default": 1.0},
                "Normal": {"default": fallbacks["Normal"]},
                "Emission": {"default": fallbacks["Emission"]},
                "Alpha": {"default": 0.0},
                "AmbientOcclusion": {"default": 1.0},
                "TransmissionColor": color,
                "TransmissionWeight": {"default": 0.0},
                "IOR": {"default": fallbacks["IOR"]},
                "Thickness": {"default": fallbacks["Thickness"]},
            }
            return {
                "slots": slots,
                "model": "Transparent",
                "coverage": slots["Alpha"],
                "usesTransparency": True,
                "requiredChannels": {"Alpha"},
            }
        if op == "Shader.GlassBSDF":
            thin_film = _snapshot_input(
                node, incoming, ("Thin Film Thickness",), 0.0
            )
            if thin_film.get("source") is not None or abs(
                float(thin_film.get("default") or 0.0)
            ) > 1.0e-8:
                raise RuntimeError(
                    "MIKU_CLOSURE_LOWERING_UNSUPPORTED:"
                    f"{node_id}:glass-thin-film"
                )
            slots = {
                "BaseColor": {"default": [0.0, 0.0, 0.0, 1.0]},
                "Metalness": {"default": 0.0},
                "Roughness": _snapshot_input(
                    node, incoming, ("Roughness",), 0.0
                ),
                "Normal": _snapshot_input(
                    node, incoming, ("Normal",), fallbacks["Normal"]
                ),
                "Emission": {"default": fallbacks["Emission"]},
                "Alpha": {"default": 1.0},
                "AmbientOcclusion": {"default": 1.0},
                "TransmissionColor": _snapshot_input(
                    node,
                    incoming,
                    ("Color",),
                    fallbacks["TransmissionColor"],
                ),
                "TransmissionWeight": _snapshot_input(
                    node, incoming, ("Weight",), 1.0
                ),
                "IOR": _snapshot_input(
                    node, incoming, ("IOR",), fallbacks["IOR"]
                ),
                "Thickness": {"default": fallbacks["Thickness"]},
            }
            closure_warning(
                node,
                "Glass BSDF is lowered to single-sample URP screen refraction "
                "and reflection-probe Fresnel.",
            )
            return {
                "slots": slots,
                "model": "DielectricScreenRefraction",
                "coverage": slots["Alpha"],
                "usesTransparency": True,
                "requiredChannels": {
                    "TransmissionColor",
                    "TransmissionWeight",
                    "IOR",
                    "Roughness",
                    "Normal",
                    "Alpha",
                },
            }
        if op == "Shader.Emission":
            color = _snapshot_input(
                node, incoming, ("Color",), fallbacks["Emission"]
            )
            strength = _snapshot_input(
                node, incoming, ("Strength",), 1.0, "Scalar"
            )
            emission = emission_with_strength(node, color, strength)
            closure_warning(
                node,
                "Flattened Blender Emission closure to the independent URP "
                "Lit Emission channel.",
            )
            slots = {
                "BaseColor": {"default": [0.0, 0.0, 0.0, 1.0]},
                "Metalness": {"default": 0.0},
                "Roughness": {"default": 1.0},
                "Normal": {"default": fallbacks["Normal"]},
                "Emission": emission,
                "Alpha": {"default": fallbacks["Alpha"]},
                "AmbientOcclusion": {
                    "default": fallbacks["AmbientOcclusion"]
                },
            }
            return {
                "slots": slots,
                "model": "StandardLit",
                "coverage": slots["Alpha"],
                "usesTransparency": False,
                "requiredChannels": {"Emission", "Alpha"},
            }
        if op not in {"Shader.Mix", "Shader.Add"}:
            return {}
        first = incoming.get((node_id, "shader"))
        second = incoming.get((node_id, "shader001"))
        if first is None or second is None:
            return {}
        next_visiting = visiting | {node_id}
        first_state = closure_state(first, next_visiting)
        second_state = closure_state(second, next_visiting)
        if not first_state or not second_state:
            return {}
        if op == "Shader.Add":
            factor = {"default": 1.0}
        else:
            factor = _snapshot_input(
                node, incoming, ("Factor", "Fac"), 0.5
            )
        first_model = str(first_state["model"])
        second_model = str(second_state["model"])
        first_op = str(
            by_id.get(str(first.get("node") or ""), {}).get("op") or ""
        )
        second_op = str(
            by_id.get(str(second.get("node") or ""), {}).get("op") or ""
        )
        if (
            (first_op == "Shader.Emission")
            != (second_op == "Shader.Emission")
            and first_model == "StandardLit"
            and second_model == "StandardLit"
        ):
            visible = (
                second_state
                if first_op == "Shader.Emission"
                else first_state
            )
            diagnostics.append(
                {
                    "severity": "info",
                    "code": "MIKU_EMISSION_CLOSURE_TOPOLOGY_PRESERVED",
                    "translationQuality": "Equivalent",
                    "nodeId": node_id,
                    "message": (
                        "Preserved the Principled surface channels while the "
                        + (
                            "Mix Shader factor controls the Emission closure."
                            if op == "Shader.Mix"
                            else "Add Shader keeps the Emission closure additive."
                        )
                    ),
                }
            )
            return {
                "slots": dict(visible["slots"]),
                "model": visible["model"],
                "coverage": visible["coverage"],
                "usesTransparency": bool(
                    visible["usesTransparency"]
                ),
                "requiredChannels": set(
                    visible["requiredChannels"]
                ),
            }
        if op == "Shader.Mix" and (
            first_model == "Transparent" or second_model == "Transparent"
        ):
            if first_model == second_model:
                visible = first_state
            elif first_model == "Transparent":
                visible = second_state
            else:
                visible = first_state
            coverage = synthetic_expression(
                owner=node,
                semantic="Alpha",
                op="Math.Mix",
                inputs=(
                    (
                        "Factor",
                        factor,
                        0.5,
                        "Scalar",
                    ),
                    (
                        "A",
                        first_state["coverage"],
                        0.0,
                        "Scalar",
                    ),
                    (
                        "B",
                        second_state["coverage"],
                        0.0,
                        "Scalar",
                    ),
                ),
                value_type="Scalar",
                suffix="closure-coverage",
            )
            slots = dict(visible["slots"])
            slots["Alpha"] = coverage
            closure_warning(
                node,
                "Lowered Transparent BSDF as independent surface coverage; "
                "visible surface and emission parameters are not pre-multiplied.",
            )
            return {
                "slots": slots,
                "model": visible["model"],
                "coverage": coverage,
                "usesTransparency": True,
                "requiredChannels": set(visible["requiredChannels"]) | {"Alpha"},
            }
        if first_model != second_model:
            raise RuntimeError(
                "MIKU_CLOSURE_LOWERING_UNSUPPORTED:"
                f"{node_id}:mixed-models={first_model},{second_model}"
            )
        closure_warning(
            node,
            "Flattened a recursive same-model closure mix to per-channel "
            "interpolation; this preserves editability but is not a physical "
            "closure blend.",
        )
        mixed: dict[str, dict[str, Any]] = {}
        for semantic, fallback in fallbacks.items():
            left = first_state["slots"].get(semantic, {"default": fallback})
            right = second_state["slots"].get(semantic, {"default": fallback})
            if left == right:
                mixed[semantic] = left
                continue
            value_type = (
                "Color"
                if semantic in {"BaseColor", "Normal", "Emission"}
                else "Scalar"
            )
            mixed[semantic] = synthetic_expression(
                owner=node,
                semantic=semantic,
                op="Color.Mix" if value_type == "Color" else "Math.Mix",
                inputs=(
                    ("Factor", factor, 0.5, "Scalar"),
                    ("A", left, fallback, value_type),
                    ("B", right, fallback, value_type),
                ),
                value_type=value_type,
                suffix="closure-mix",
            )
        return {
            "slots": mixed,
            "model": first_model,
            "coverage": mixed["Alpha"],
            "usesTransparency": bool(first_state["usesTransparency"])
            or bool(second_state["usesTransparency"]),
            "requiredChannels": set(first_state["requiredChannels"])
            | set(second_state["requiredChannels"]),
        }

    try:
        state = closure_state(surface)
        if not state:
            return {}, {}, diagnostics
        normalized_displacement_policy = str(
            displacement_policy or "FOLLOW_BLENDER"
        ).upper()
        if normalized_displacement_policy not in {
            "FOLLOW_BLENDER",
            "ALWAYS_VERTEX",
            "MAP_ONLY",
        }:
            raise RuntimeError(
                "MIKU_DISPLACEMENT_POLICY_UNSUPPORTED:"
                f"{normalized_displacement_policy}"
            )
        normalized_displacement_method = str(
            displacement_method or "BUMP"
        ).upper()
        active_node_ids: set[str] = set()
        pending_node_ids = [str(surface.get("node") or "")]
        if displacement is not None:
            pending_node_ids.append(str(displacement.get("node") or ""))
        while pending_node_ids:
            active_node_id = pending_node_ids.pop()
            if not active_node_id or active_node_id in active_node_ids:
                continue
            active_node_ids.add(active_node_id)
            active_node = by_id.get(active_node_id) or {}
            for input_socket in active_node.get("inputs", []) or []:
                if not isinstance(input_socket, Mapping):
                    continue
                input_id = str(
                    input_socket.get("id")
                    or input_socket.get("name")
                    or ""
                )
                input_source = incoming.get(
                    (active_node_id, _normalize_socket_name(input_id))
                )
                if input_source is not None:
                    pending_node_ids.append(
                        str(input_source.get("node") or "")
                    )

        height_candidates: list[dict[str, Any]] = []
        if normalized_displacement_policy in {"ALWAYS_VERTEX", "MAP_ONLY"}:
            for candidate_id in sorted(active_node_ids):
                candidate_node = by_id.get(candidate_id) or {}
                candidate_op = str(candidate_node.get("op") or "")
                if candidate_op not in {"Vector.Bump", "Vector.Displacement"}:
                    continue
                raw_height = _snapshot_input(
                    candidate_node,
                    incoming,
                    ("Height",),
                    0.0,
                    "Scalar",
                )
                candidate = {
                    "node": candidate_node,
                    "kind": "Bump" if candidate_op == "Vector.Bump" else "Displacement",
                    "height": raw_height,
                    "midlevel": 0.5,
                    "scale": None,
                }
                if candidate_op == "Vector.Bump":
                    strength = _snapshot_input(
                        candidate_node,
                        incoming,
                        ("Strength",),
                        1.0,
                        "Scalar",
                    )
                    distance = _snapshot_input(
                        candidate_node,
                        incoming,
                        ("Distance",),
                        1.0,
                        "Scalar",
                    )
                    if (
                        strength.get("source") is None
                        and distance.get("source") is None
                    ):
                        strength_value = float(strength.get("default", 1.0))
                        distance_value = float(distance.get("default", 1.0))
                        scale_value = strength_value * distance_value
                        if bool((candidate_node.get("params") or {}).get("invert")):
                            scale_value = -scale_value
                        if math.isfinite(scale_value):
                            candidate["scale"] = scale_value
                    if candidate["scale"] is None:
                        diagnostics.append(
                            {
                                "severity": "warning",
                                "code": "MIKU_BUMP_VERTEX_PROMOTION_PARAMETER_UNSUPPORTED",
                                "translationQuality": "Unsupported",
                                "nodeId": candidate_id,
                                "message": (
                                    "Dynamic or non-finite Bump Strength/Distance "
                                    "cannot be promoted to vertex displacement."
                                ),
                            }
                        )
                else:
                    midlevel = _snapshot_input(
                        candidate_node,
                        incoming,
                        ("Midlevel",),
                        0.5,
                        "Scalar",
                    )
                    scale = _snapshot_input(
                        candidate_node,
                        incoming,
                        ("Scale",),
                        1.0,
                        "Scalar",
                    )
                    if midlevel.get("source") is None and scale.get("source") is None:
                        midlevel_value = float(midlevel.get("default", 0.5))
                        scale_value = float(scale.get("default", 1.0))
                        if math.isfinite(midlevel_value) and math.isfinite(scale_value):
                            candidate["midlevel"] = midlevel_value
                            candidate["scale"] = scale_value
                height_candidates.append(candidate)
        elif (
            displacement is not None
            and normalized_displacement_method in {"DISPLACEMENT", "BOTH"}
        ):
            displacement_node = by_id.get(str(displacement.get("node") or "")) or {}
            if str(displacement_node.get("op") or "") == "Vector.Displacement":
                follow_midlevel = _snapshot_input(
                    displacement_node,
                    incoming,
                    ("Midlevel",),
                    0.5,
                    "Scalar",
                )
                follow_scale = _snapshot_input(
                    displacement_node,
                    incoming,
                    ("Scale",),
                    1.0,
                    "Scalar",
                )
                height_candidates.append(
                    {
                        "node": displacement_node,
                        "kind": "Displacement",
                        "height": _snapshot_input(
                            displacement_node,
                            incoming,
                            ("Height",),
                            0.0,
                            "Scalar",
                        ),
                        "midlevel": (
                            float(follow_midlevel.get("default", 0.5))
                            if follow_midlevel.get("source") is None
                            else 0.5
                        ),
                        "scale": (
                            float(follow_scale.get("default", 1.0))
                            if follow_scale.get("source") is None
                            else None
                        ),
                    }
                )

        height_by_source: dict[str, dict[str, Any]] = {}
        for candidate in height_candidates:
            raw_height = candidate["height"]
            raw_source = raw_height.get("source")
            source_key = (
                f"{raw_source.get('node')}:{raw_source.get('socket')}"
                if isinstance(raw_source, Mapping)
                else "constant:" + repr(raw_height.get("default"))
            )
            height_by_source.setdefault(source_key, candidate)
        selected_height = None
        if len(height_by_source) > 1:
            diagnostics.append(
                {
                    "severity": "warning",
                    "code": "MIKU_MULTIPLE_HEIGHT_SOURCES_NOT_COMBINED",
                    "translationQuality": "Unsupported",
                    "sourceCount": len(height_by_source),
                    "message": (
                        "Multiple active Height sources cannot be safely combined; "
                        "no shared Height map or vertex displacement was emitted."
                    ),
                }
            )
        elif height_by_source:
            selected_height = next(iter(height_by_source.values()))
            state["slots"]["Height"] = dict(selected_height["height"])
            state["requiredChannels"].add("Height")
            state["heightChannel"] = {
                "policy": normalized_displacement_policy,
                "sourceKind": selected_height["kind"],
                "source": dict(selected_height["height"].get("source") or {}),
                "midlevel": float(selected_height.get("midlevel", 0.5)),
                "scale": selected_height.get("scale"),
                "format": "OpenEXRHalf",
                "channel": "R",
                "colorSpace": "Linear",
            }
        if displacement is not None:
            displacement_node = by_id.get(
                str(displacement.get("node") or "")
            )
            if displacement_node is None or str(
                displacement_node.get("op") or ""
            ) != "Vector.Displacement":
                raise RuntimeError(
                    "MIKU_DISPLACEMENT_NODE_REQUIRED"
                )
            displacement_params = (
                displacement_node.get("params")
                if isinstance(displacement_node.get("params"), Mapping)
                else {}
            )
            if str(
                displacement_params.get("space") or "OBJECT"
            ).upper() != "OBJECT":
                raise RuntimeError(
                    "MIKU_DISPLACEMENT_SPACE_UNSUPPORTED:"
                    f"{displacement_params.get('space') or '<missing>'}"
                )
            displacement_id = str(displacement_node.get("id") or "")
            if incoming.get((displacement_id, "normal")) is not None:
                raise RuntimeError(
                    "MIKU_DISPLACEMENT_NORMAL_INPUT_UNSUPPORTED"
                )
            height = _snapshot_input(
                displacement_node,
                incoming,
                ("Height",),
                0.0,
                "Scalar",
            )
            midlevel = _snapshot_input(
                displacement_node,
                incoming,
                ("Midlevel",),
                0.5,
                "Scalar",
            )
            scale = _snapshot_input(
                displacement_node,
                incoming,
                ("Scale",),
                1.0,
                "Scalar",
            )
            if midlevel.get("source") is not None:
                raise RuntimeError(
                    "MIKU_DISPLACEMENT_MIDLEVEL_DYNAMIC_UNSUPPORTED"
                )
            if scale.get("source") is not None:
                raise RuntimeError(
                    "MIKU_DISPLACEMENT_SCALE_DYNAMIC_UNSUPPORTED"
                )
            midlevel_value = float(midlevel.get("default", 0.5))
            scale_value = float(scale.get("default", 1.0))
            if not math.isfinite(midlevel_value) or not math.isfinite(
                scale_value
            ):
                raise RuntimeError(
                    "MIKU_DISPLACEMENT_PARAMETER_NONFINITE"
                )
            if normalized_displacement_method in {"BUMP", "BOTH"}:
                state["slots"]["Normal"] = synthetic_expression(
                    owner=displacement_node,
                    semantic="Normal",
                    op="Vector.DisplacementBump",
                    inputs=(
                        ("Height", height, 0.0, "Scalar"),
                        (
                            "Normal",
                            state["slots"].get(
                                "Normal",
                                {"default": fallbacks["Normal"]},
                            ),
                            fallbacks["Normal"],
                            "Float3",
                        ),
                    ),
                    value_type="Float3",
                    suffix="material-output-bump",
                    params={
                        "midlevel": midlevel_value,
                        "scale": scale_value,
                    },
                )
                state["requiredChannels"].add("Normal")
            if (
                normalized_displacement_policy != "MAP_ONLY"
                and (
                    normalized_displacement_policy == "ALWAYS_VERTEX"
                    or normalized_displacement_method in {"DISPLACEMENT", "BOTH"}
                )
                and selected_height is not None
                and selected_height.get("scale") is not None
            ):
                state["slots"]["Displacement"] = {
                    "default": None,
                    "source": dict(displacement),
                }
                state["requiredChannels"].add("Displacement")
                diagnostics.append(
                    {
                        "severity": "warning",
                        "code": (
                            "MIKU_VERTEX_DISPLACEMENT_REQUIRES_"
                            "SUBDIVIDED_MESH"
                        ),
                        "translationQuality": "RequiresProjectSetup",
                        "nodeId": str(
                            displacement_node.get("id") or ""
                        ),
                        "message": (
                            "Unity vertex displacement requires a mesh "
                            "with sufficient vertex subdivision."
                        ),
                    }
                )
            if normalized_displacement_method not in {
                "BUMP",
                "DISPLACEMENT",
                "BOTH",
            }:
                raise RuntimeError(
                    "MIKU_DISPLACEMENT_METHOD_UNSUPPORTED:"
                    f"{normalized_displacement_method}"
                )
        if (
            displacement is None
            and normalized_displacement_policy == "ALWAYS_VERTEX"
            and selected_height is not None
            and selected_height.get("scale") is not None
        ):
            owner = selected_height["node"]
            state["slots"]["Displacement"] = synthetic_expression(
                owner=owner,
                semantic="Displacement",
                op="Vector.Displacement",
                inputs=(
                    ("Height", selected_height["height"], 0.0, "Scalar"),
                    ("Midlevel", {"default": selected_height["midlevel"]}, 0.5, "Scalar"),
                    ("Scale", {"default": selected_height["scale"]}, 1.0, "Scalar"),
                ),
                value_type="Float3",
                suffix="bump-vertex-promotion",
                params={"space": "OBJECT"},
            )
            state["requiredChannels"].add("Displacement")
            diagnostics.append(
                {
                    "severity": "warning",
                    "code": "MIKU_VERTEX_DISPLACEMENT_REQUIRES_SUBDIVIDED_MESH",
                    "translationQuality": "RequiresProjectSetup",
                    "nodeId": str(owner.get("id") or ""),
                    "message": (
                        "Unity vertex displacement requires a mesh with "
                        "sufficient vertex subdivision."
                    ),
                }
            )
        contract = {
            "model": (
                "StandardLit"
                if state["model"] == "Transparent"
                else state["model"]
            ),
            "coverageChannel": "Alpha",
            "requiredChannels": sorted(state["requiredChannels"]),
            "usesTransparency": bool(state["usesTransparency"]),
        }
        if isinstance(state.get("heightChannel"), Mapping):
            contract["heightChannel"] = dict(state["heightChannel"])
        return state["slots"], contract, diagnostics
    except RuntimeError as exc:
        text = str(exc)
        code = text.split(":", 1)[0]
        diagnostics.append(
            {
                "severity": "error",
                "code": code,
                "message": text,
            }
        )
        return {}, {}, diagnostics


def snapshot_material(
    material: Any,
    *,
    workflow_kind: str = "standard_pbr",
    workflow_part: str = "Body",
) -> dict[str, Any]:
    tree = getattr(material, "node_tree", None)
    source_nodes = list(getattr(tree, "nodes", []) or [])
    nodes, edges, _, _ = _snapshot_tree(tree)
    displacement_method = str(
        getattr(material, "displacement_method", "BUMP") or "BUMP"
    )
    displacement_policy = str(
        getattr(material, "miku_displacement_policy", "FOLLOW_BLENDER")
        or "FOLLOW_BLENDER"
    )
    semantic_slots, surface_semantic, closure_diagnostics = (
        _principled_slots_from_snapshot(
            nodes,
            edges,
            displacement_method=displacement_method,
            displacement_policy=displacement_policy,
        )
    )
    if not semantic_slots:
        semantic_slots = _principled_defaults(material)
    workflow = {"kind": normalize_workflow_kind(workflow_kind)}
    if workflow["kind"] in {
        "genshin_toon",
        "wuwa_toon",
        "hsr_toon",
        "endfield_toon",
    }:
        workflow["part"] = normalize_workflow_part(workflow_part)
    parameters, driver_diagnostics = _snapshot_root_drivers(tree, source_nodes, nodes)
    color_management = _color_management_snapshot()
    source_render_method = str(
        getattr(material, "surface_render_method", "OPAQUE") or "OPAQUE"
    ).upper()
    uses_transparency = bool(surface_semantic.get("usesTransparency"))
    render_method = (
        {
            "BLENDED": "AlphaBlend",
            "DITHERED": "Dithered",
        }.get(source_render_method, "Opaque")
        if uses_transparency
        else "Opaque"
    )
    alpha_slot = semantic_slots.get("Alpha", {})
    alpha_linked = isinstance(alpha_slot, Mapping) and isinstance(
        alpha_slot.get("source"), Mapping
    )
    if (
        surface_semantic
        and render_method == "Opaque"
        and (
            alpha_linked
            or (
                isinstance(alpha_slot, Mapping)
                and alpha_slot.get("default") != 1.0
            )
        )
    ):
        render_method = "AlphaBlend"
        closure_diagnostics.append(
            {
                "severity": "warning",
                "code": "MIKU_ALPHA_LINKED_OPAQUE_AUTO_BLEND",
                "translationQuality": "Equivalent",
                "message": (
                    "A linked or non-opaque Principled Alpha input changed the "
                    "Miku surface contract from Opaque to AlphaBlend."
                ),
            }
        )
    if surface_semantic:
        surface_semantic.update(
            {
                "renderMethod": render_method,
                "renderFace": (
                    "Front"
                    if bool(getattr(material, "use_backface_culling", False))
                    else "Both"
                ),
                "sourceRenderMethod": source_render_method,
            }
        )
        surface_semantic.pop("usesTransparency", None)
    height_channel = (
        dict(surface_semantic.pop("heightChannel"))
        if isinstance(surface_semantic.get("heightChannel"), Mapping)
        else {}
    )
    legacy_resources, legacy_entry, semantic_slots, standard_pbr_metadata = (
        _augment_standard_pbr_semantics(
            nodes,
            edges,
            semantic_slots,
            source_nodes,
        )
    )
    standard_pbr_semantic = {
        "workflow": str(standard_pbr_metadata.get("workflow") or "Metallic"),
        "source": dict(
            standard_pbr_metadata.get("source")
            if isinstance(standard_pbr_metadata.get("source"), Mapping)
            else {}
        ),
        "slots": semantic_slots,
        "packedTextures": dict(
            standard_pbr_metadata.get("packedTextures")
            if isinstance(standard_pbr_metadata.get("packedTextures"), Mapping)
            else {}
        ),
        "diagnostics": list(standard_pbr_metadata.get("diagnostics") or []),
    }
    if not standard_pbr_semantic["source"].get("blenderMaterial"):
        standard_pbr_semantic["source"]["blenderMaterial"] = str(
            getattr(material, "name", "Material")
        )
    if not standard_pbr_semantic["source"].get("shader"):
        standard_pbr_semantic["source"]["shader"] = "SemanticMaps"
    standard_pbr_semantic["source"].setdefault(
        "confidence",
        max(
            (
                float(slot.get("confidence", 0.0))
                for slot in semantic_slots.values()
                if isinstance(slot, Mapping)
            ),
            default=0.0,
        ),
    )
    if legacy_entry:
        standard_pbr_semantic["entry"] = legacy_entry
    snapshot = {
        "material": {"name": str(getattr(material, "name", "Material"))},
        "workflow": workflow,
        "nodes": nodes,
        "edges": edges,
        "resources": legacy_resources,
        "parameters": parameters,
        "standardPbrSemantic": standard_pbr_semantic,
        "surfaceSemantic": surface_semantic,
        "colorManagement": color_management,
        "normalConvention": str(
            getattr(
                material,
                "miku_normal_convention",
                "TangentOpenGLPositiveY",
            )
            or "TangentOpenGLPositiveY"
        ),
        "displacementMethod": displacement_method,
        "displacementPolicy": displacement_policy,
        "heightChannel": height_channel,
        "diagnostics": [*driver_diagnostics, *closure_diagnostics],
    }
    snapshot["eeveeCapability"] = classify_eevee_graph(snapshot)
    return snapshot


def _color_management_snapshot() -> dict[str, Any]:
    coefficients = [0.2126, 0.7152, 0.0722]
    fingerprint_source = "blender-5.2-bundled-ocio"
    try:
        import PyOpenColorIO as ocio

        config = ocio.GetCurrentConfig()
        reported = list(config.getDefaultLumaCoefs())
        if len(reported) == 3 and all(math.isfinite(float(v)) for v in reported):
            coefficients = [float(value) for value in reported]
        cache_id = (
            str(config.getCacheID())
            if hasattr(config, "getCacheID")
            else ""
        )
        fingerprint_source = cache_id or (
            f"{getattr(config, 'getName', lambda: '')()}:{coefficients}"
        )
    except (ImportError, AttributeError, RuntimeError, TypeError, ValueError):
        pass
    return {
        "luminanceCoefficients": coefficients,
        "configFingerprint": hashlib.sha256(
            fingerprint_source.encode("utf-8")
        ).hexdigest(),
        "conversionAlgorithmVersion": "blender-5.2-implicit-v1",
    }


def _action_fcurves(action: Any) -> list[Any]:
    """Read both legacy and layered Blender Action storage without mutation."""

    direct = getattr(action, "fcurves", None)
    if direct is not None:
        try:
            return list(direct)
        except TypeError:
            pass
    result = []
    for layer in getattr(action, "layers", []) or []:
        for strip in getattr(layer, "strips", []) or []:
            for bag in getattr(strip, "channelbags", []) or []:
                result.extend(list(getattr(bag, "fcurves", []) or []))
    return result


def _driver_target(
    tree: Any,
    source_nodes: list[Any],
    data_path: str,
) -> tuple[Any, str, int, Any] | None:
    match = _DRIVER_PATH.match(str(data_path or ""))
    if not match:
        return None
    node_name = (
        match.group("node")
        .replace(r"\\", "\\")
        .replace(r"\"", '"')
    )
    node = next(
        (
            item
            for item in source_nodes
            if str(getattr(item, "name", "")) == node_name
        ),
        None,
    )
    if node is None:
        return None
    direction = match.group("direction")
    sockets = list(getattr(node, direction, []) or [])
    socket_token = match.group("socket")
    if socket_token.startswith('"'):
        try:
            socket_name = json.loads(socket_token)
        except json.JSONDecodeError:
            return None
        index = next(
            (
                item_index
                for item_index, item in enumerate(sockets)
                if str(getattr(item, "name", "")) == socket_name
            ),
            -1,
        )
    else:
        index = int(socket_token)
    if index < 0 or index >= len(sockets):
        return None
    return node, direction, index, sockets[index]


def _unsafe_driver_expression(expression: str) -> bool:
    try:
        tree = ast.parse(str(expression or ""), mode="eval")
    except SyntaxError:
        return True
    for node in ast.walk(tree):
        if isinstance(
            node,
            (
                ast.Attribute,
                ast.Subscript,
                ast.Lambda,
                ast.ListComp,
                ast.SetComp,
                ast.DictComp,
                ast.GeneratorExp,
                ast.NamedExpr,
            ),
        ):
            return True
        if isinstance(node, ast.Name) and node.id.startswith("_"):
            return True
        if isinstance(node, ast.Call):
            if (
                not isinstance(node.func, ast.Name)
                or node.func.id not in _SAFE_DRIVER_CALLS
            ):
                return True
    return False


def _driver_has_object_target(driver: Any) -> bool:
    for variable in getattr(driver, "variables", []) or []:
        for target in getattr(variable, "targets", []) or []:
            if str(getattr(target, "id_type", "") or "") == "OBJECT":
                return True
            identifier = getattr(target, "id", None)
            if identifier is not None and type(identifier).__name__ == "Object":
                return True
    return False


def _snapshot_root_drivers(
    tree: Any,
    source_nodes: list[Any],
    snapshots: list[dict[str, Any]],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    if tree is None:
        return [], []
    animation_data = getattr(tree, "animation_data", None)
    action = getattr(animation_data, "action", None)
    fcurves = list(getattr(animation_data, "drivers", []) or [])
    if action is not None:
        fcurves.extend(_action_fcurves(action))
    if not fcurves:
        return [], []
    by_name = {
        str(getattr(node, "name", "")): snapshot
        for node, snapshot in zip(source_nodes, snapshots)
    }
    parameters: list[dict[str, Any]] = []
    diagnostics: list[dict[str, Any]] = []
    contract = _time_contract()
    seen_fcurves: set[tuple[str, int]] = set()
    for fcurve in fcurves:
        fcurve_key = (
            str(getattr(fcurve, "data_path", "")),
            int(getattr(fcurve, "array_index", 0) or 0),
        )
        if fcurve_key in seen_fcurves:
            continue
        seen_fcurves.add(fcurve_key)
        driver = getattr(fcurve, "driver", None)
        if driver is None:
            continue
        target = _driver_target(
            tree,
            source_nodes,
            str(getattr(fcurve, "data_path", "")),
        )
        if target is None:
            continue
        node, direction, socket_index, socket = target
        snapshot = by_name[str(getattr(node, "name", ""))]
        socket_snapshot = snapshot[direction][socket_index]
        driver_type = str(getattr(driver, "type", "SCRIPTED") or "SCRIPTED")
        expression = str(getattr(driver, "expression", "") or "")
        is_scalar = str(getattr(socket, "type", "")) in {"VALUE", "BOOLEAN"}
        if not is_scalar:
            socket_snapshot["driver"] = {
                "kind": "Unsupported",
                "reason": "non-scalar driver target",
            }
            continue
        if _driver_has_object_target(driver):
            socket_snapshot["driver"] = {
                "kind": "Unsupported",
                "reason": "object-targeted driver",
            }
            continue
        if driver_type == "SCRIPTED":
            try:
                affine = parse_affine_frame(expression)
            except TimeDriverError:
                affine = None
            if affine is not None:
                socket_snapshot["driver"] = {
                    "kind": "TimeAffine",
                    "scale": affine.scale,
                    "offset": affine.offset,
                    "timeContract": contract,
                }
                continue
        if _unsafe_driver_expression(expression):
            socket_snapshot["driver"] = {
                "kind": "Unsupported",
                "reason": "unsafe driver expression",
            }
            continue
        node_id = str(snapshot["id"])
        socket_id = str(socket_snapshot.get("id") or socket_snapshot.get("name"))
        parameter_id = "miku-driver-" + hashlib.sha256(
            f"{node_id}:{direction}:{socket_id}".encode("utf-8")
        ).hexdigest()[:24]
        socket_snapshot["driver"] = {
            "kind": "Externalized",
            "parameterId": parameter_id,
        }
        parameters.append(
            {
                "id": parameter_id,
                "semantic": "TimeDriver",
                "displayName": (
                    f"{getattr(node, 'name', 'Node')} / "
                    f"{getattr(socket, 'name', 'Value')}"
                ),
                "default": _socket_value(socket),
                "mutability": "Live",
                "scope": "PerMaterial",
                "updateAction": "None",
                "runtimeEditable": True,
                "nodeId": node_id,
                "socketId": socket_id,
            }
        )
        diagnostics.append(
            {
                "severity": "warning",
                "code": "MIKU_TIME_DRIVER_EXTERNALIZED",
                "translationQuality": "RequiresRuntimeSupport",
                "nodeId": node_id,
                "socketId": socket_id,
                "message": (
                    "A non-affine scalar driver was externalized as a stable "
                    "material parameter; animate it from Animator or Timeline."
                ),
            }
        )
    return (
        sorted(parameters, key=lambda item: item["id"]),
        diagnostics,
    )


def _write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    os.replace(temporary, path)


def _write_bytes(path: Path, value: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_bytes(value)
    os.replace(temporary, path)


def _shader_nodes_by_stable_id(
    tree: Any,
    *,
    namespace: str = "",
    ancestors: frozenset[int] = frozenset(),
) -> dict[str, Any]:
    """Return live Blender nodes using the same identities as ``_snapshot_tree``."""

    if tree is None:
        return {}
    tree_identity = id(tree)
    if tree_identity in ancestors:
        raise RuntimeError("MIKU_NODE_GROUP_RECURSION_UNSUPPORTED")
    nested_ancestors = ancestors | {tree_identity}
    result: dict[str, Any] = {}
    for node in getattr(tree, "nodes", []) or []:
        node_type = str(getattr(node, "bl_idname", ""))
        if node_type in {"NodeGroupInput", "NodeGroupOutput"}:
            continue
        if (
            node_type == "ShaderNodeGroup"
            and _time_group_contract(node) is None
            and getattr(node, "node_tree", None) is not None
        ):
            instance_id = _node_stable_id(node, namespace)
            child_namespace = (
                f"{namespace}/{instance_id}" if namespace else instance_id
            )
            result.update(
                _shader_nodes_by_stable_id(
                    getattr(node, "node_tree", None),
                    namespace=child_namespace,
                    ancestors=nested_ancestors,
                )
            )
            continue
        result[_node_stable_id(node, namespace)] = node
    return result


def _static_image_bytes(image: Any, node_id: str) -> bytes:
    packed_file = getattr(image, "packed_file", None)
    if packed_file is not None:
        data = bytes(getattr(packed_file, "data", b""))
        if not data:
            raise RuntimeError(f"MIKU_PACKED_IMAGE_EMPTY:{node_id}")
        return data
    if bpy is None:
        raise RuntimeError("MIKU_BLENDER_RUNTIME_REQUIRED")
    source_path = Path(
        bpy.path.abspath(
            str(getattr(image, "filepath", "") or ""),
            library=getattr(image, "library", None),
        )
    )
    if not source_path.is_file():
        raise RuntimeError(f"MIKU_IMAGE_FILE_MISSING:{node_id}")
    return source_path.read_bytes()


def _collect_static_image_resources(
    material: Any,
    ir: Mapping[str, Any],
    target: Path,
) -> list[dict[str, Any]]:
    """Seal native static image samples without retaining user machine paths."""

    nodes = _shader_nodes_by_stable_id(getattr(material, "node_tree", None))
    format_contracts = {
        "PNG": (".png", "image/png", 1),
        "JPEG": (".jpg", "image/jpeg", 1),
        "OPEN_EXR": (".exr", "image/x-exr", 4),
    }
    resources: dict[str, dict[str, Any]] = {}
    resource_bindings: dict[str, set[tuple[str, str]]] = {}
    resource_references: dict[str, set[str]] = {}
    semantic_bindings: dict[str, str] = {}
    for expression in ir.get("expressions", []) or []:
        if (
            not isinstance(expression, Mapping)
            or str(expression.get("op") or "") != "Texture.SampleImage2D"
        ):
            continue
        params = (
            expression.get("params")
            if isinstance(expression.get("params"), Mapping)
            else {}
        )
        source = (
            expression.get("source")
            if isinstance(expression.get("source"), Mapping)
            else {}
        )
        resource_id = str(params.get("resourceId") or "")
        node_id = str(source.get("nodeId") or "")
        semantic = str(params.get("semantic") or "")
        if not resource_id or not node_id or not semantic:
            raise RuntimeError("MIKU_STATIC_IMAGE_EXPRESSION_INVALID")
        previous = semantic_bindings.get(semantic)
        if previous is not None and previous != resource_id:
            raise RuntimeError(
                f"MIKU_STATIC_IMAGE_BINDING_CONFLICT:{semantic}"
            )
        semantic_bindings[semantic] = resource_id
        resource_bindings.setdefault(resource_id, set()).add(
            (
                semantic,
                str(params.get("channel") or "R"),
            )
        )
        resource_references.setdefault(resource_id, set()).add(
            str(params.get("referenceName") or "")
        )
        if resource_id in resources:
            resource = resources[resource_id]
            for key, fallback in (
                ("colorSpace", "Linear"),
                ("uvSet", "UV0"),
                ("projection", "FLAT"),
                ("interpolation", "LINEAR"),
                ("extension", "REPEAT"),
            ):
                if str(resource.get(key) or fallback) != str(
                    params.get(key) or fallback
                ):
                    raise RuntimeError(
                        f"MIKU_STATIC_IMAGE_SAMPLER_CONFLICT:{resource_id}:{key}"
                    )
            continue
        node = nodes.get(node_id)
        image = getattr(node, "image", None) if node is not None else None
        if image is None:
            raise RuntimeError(f"MIKU_IMAGE_DATABLOCK_MISSING:{node_id}")
        image_format = str(
            getattr(image, "file_format", "") or ""
        ).upper()
        contract = format_contracts.get(image_format)
        if contract is None:
            raise RuntimeError(
                f"MIKU_IMAGE_FORMAT_UNSUPPORTED:{node_id}:"
                f"{image_format or '<missing>'}"
            )
        extension, media_type, component_bytes = contract
        data = _static_image_bytes(image, node_id)
        relative_path = Path("Textures") / f"{resource_id}{extension}"
        destination = target / relative_path
        _write_bytes(destination, data)
        resource = make_file_reference(
            target,
            destination,
            media_type=media_type,
        )
        image_size = list(getattr(image, "size", ()) or ())
        resource.update(
            {
                "id": resource_id,
                "semantic": semantic,
                "bindingKey": semantic,
                "usage": str(params.get("usage") or "Scalar"),
                "channel": str(params.get("channel") or "R"),
                "colorSpace": str(params.get("colorSpace") or "Linear"),
                "width": int(image_size[0]) if len(image_size) > 0 else 0,
                "height": int(image_size[1]) if len(image_size) > 1 else 0,
                "channelCount": int(
                    getattr(image, "channels", 4) or 4
                ),
                "componentBytes": component_bytes,
                "uvSet": str(params.get("uvSet") or "UV0"),
                "projection": str(params.get("projection") or "FLAT"),
                "interpolation": str(
                    params.get("interpolation") or "LINEAR"
                ),
                "extension": str(params.get("extension") or "REPEAT"),
                **(
                    {
                        "normalConvention": str(
                            params.get("normalConvention")
                            or "TangentOpenGLPositiveY"
                        )
                    }
                    if semantic == "Normal"
                    else {}
                ),
            }
        )
        resources[resource_id] = resource
    scalar_semantics = {
        "Metalness",
        "Roughness",
        "AmbientOcclusion",
        "Height",
        "Alpha",
        "EmissionMask",
    }
    for resource_id, resource in resources.items():
        bindings = sorted(resource_bindings.get(resource_id, set()))
        references = {
            item
            for item in resource_references.get(resource_id, set())
            if item
        }
        if len(bindings) <= 1:
            continue
        if (
            any(semantic not in scalar_semantics for semantic, _ in bindings)
            or str(resource.get("usage") or "") != "Scalar"
            or str(resource.get("colorSpace") or "") != "Linear"
        ):
            raise RuntimeError(
                f"MIKU_PACKED_TEXTURE_COLOR_SPACE_CONFLICT:{resource_id}"
            )
        if len(references) != 1:
            raise RuntimeError(
                f"MIKU_PACKED_TEXTURE_REFERENCE_CONFLICT:{resource_id}"
            )
        first_semantic, first_channel = bindings[0]
        resource["semantic"] = first_semantic
        resource["channel"] = first_channel
        resource["bindingKey"] = next(iter(references))
        resource["channelBindings"] = [
            {
                "semantic": semantic,
                "channel": channel,
            }
            for semantic, channel in bindings
        ]
    return [resources[key] for key in sorted(resources)]


def _fixed_image_resource_id(image: Any) -> str:
    size = list(getattr(image, "size", ()) or ())
    value = (
        f"{getattr(image, 'name_full', '') or getattr(image, 'name', '')}|"
        f"{getattr(image, 'source', '')}|{getattr(image, 'file_format', '')}|"
        f"{int(size[0]) if len(size) > 0 else 0}x"
        f"{int(size[1]) if len(size) > 1 else 0}"
    )
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def _fixed_targa_png_bytes(image: Any, node_id: str) -> bytes:
    """Encode a TARGA image as PNG without mutating the source datablock."""

    if bpy is None:
        raise RuntimeError(f"MIKU_IMAGE_TRANSCODE_UNAVAILABLE:{node_id}")
    try:
        import imbuf
        from io import BytesIO
    except ImportError as error:
        raise RuntimeError(
            f"MIKU_IMAGE_TRANSCODE_UNAVAILABLE:{node_id}"
        ) from error
    buffer = None
    try:
        buffer = imbuf.load_from_buffer(_static_image_bytes(image, node_id))
        buffer.file_type = "PNG"
        buffer.compress = 15
        destination = BytesIO()
        imbuf.write_to_buffer(buffer, destination)
        data = destination.getvalue()
        if not data:
            raise RuntimeError(
                f"MIKU_IMAGE_TRANSCODE_OUTPUT_MISSING:{node_id}"
            )
        return data
    except (OSError, RuntimeError) as error:
        raise RuntimeError(
            f"MIKU_IMAGE_TRANSCODE_FAILED:{node_id}:TARGA:PNG:{error}"
        ) from error
    finally:
        if buffer is not None:
            buffer.free()


def _fixed_node_role_candidate(
    node: Any,
    image: Any,
    workflow_kind: str,
    workflow_part: str = "Body",
) -> tuple[int, str]:
    explicit = str(
        getattr(node, "miku_texture_role", "AUTO") or "AUTO"
    )
    role = normalize_texture_role(explicit) if explicit != "AUTO" else ""
    if role and allowed_texture_role(workflow_kind, role):
        return 0, role
    label = str(getattr(node, "label", "") or "").strip()
    if label.casefold().startswith("miku:"):
        role = normalize_texture_role(label)
        if role and allowed_texture_role(workflow_kind, role):
            return 1, role
    role = normalize_texture_role(label)
    if role and allowed_texture_role(workflow_kind, role):
        return 2, role
    for value in (
        str(getattr(image, "name", "") or ""),
        str(getattr(image, "filepath", "") or ""),
    ):
        role = (
            _infer_wuwa_eye_filename_texture_role(value)
            if workflow_kind == "wuwa_toon" and workflow_part == "Eye"
            else ""
        ) or infer_filename_texture_role(value)
        if role in {
            "EyeHET",
            "EyeHDMF",
            "EyeUpperHighlight",
            "EyeLowerHighlight",
            "EyeEG",
        } and not (
            workflow_kind == "wuwa_toon" and workflow_part == "Eye"
        ):
            role = ""
        if role and allowed_texture_role(workflow_kind, role):
            return 3, role
    return 99, ""


def _infer_wuwa_eye_filename_texture_role(value: str) -> str:
    """Recognize complete Wuwa Eye filenames only inside the Eye workflow."""

    stem = os.path.splitext(os.path.basename(value or ""))[0]
    normalized = re.sub(r"[\s.-]+", "_", stem.casefold()).strip("_")
    if re.search(r"(?:^|_)eye_het$", normalized):
        return "EyeHET"
    if re.search(r"(?:^|_)hdmf(?:\d+)?_em$", normalized):
        return "EyeHDMF"
    if normalized == "t_highlight_1" or normalized.endswith(
        "_t_highlight_1"
    ):
        return "EyeUpperHighlight"
    if normalized == "bottomhighlight_1" or normalized.endswith(
        "_bottomhighlight_1"
    ):
        return "EyeLowerHighlight"
    compact = re.sub(r"[^0-9a-z]+", "", normalized)
    if compact.endswith("eg") and (
        compact.endswith("eyeeg")
        or "eyesecondhighlight" in compact
        or "eyessecondhighlight" in compact
        or "eyesecondheightlight" in compact
        or "eyessecondheightlight" in compact
    ):
        return "EyeEG"
    return ""


def _mix_selected_input_socket(
    node: Mapping[str, Any],
    node_edges: list[Mapping[str, Any]],
) -> str:
    """Return the sole effective Mix input for an unlinked exact 0/1 factor."""

    op = str(node.get("op") or "")
    if op not in {"Math.Mix", "Color.Mix", "Shader.Mix"}:
        return ""
    params = node.get("params") or {}
    if op != "Shader.Mix" and str(
        params.get("blend_type") or "MIX"
    ).upper() != "MIX":
        return ""
    inputs = [
        item
        for item in node.get("inputs", []) or []
        if isinstance(item, Mapping)
        and bool(item.get("enabled", True))
        and not bool(item.get("isUnavailable", False))
    ]
    factor = next(
        (
            item
            for item in inputs
            if _normalize_socket_name(item.get("name")) in {"factor", "fac"}
            and str(item.get("valueType") or "").upper() in {"FLOAT", "VALUE"}
        ),
        None,
    )
    if factor is None:
        return ""
    factor_socket = _normalize_socket_name(
        factor.get("id") or factor.get("name")
    )
    if any(
        _normalize_socket_name((edge.get("to") or {}).get("socket"))
        == factor_socket
        for edge in node_edges
    ):
        return ""
    value = factor.get("default")
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        return ""
    selected_index = 0 if abs(float(value)) <= 1.0e-8 else (
        1 if abs(float(value) - 1.0) <= 1.0e-8 else -1
    )
    if selected_index < 0:
        return ""
    if op == "Shader.Mix":
        candidates = [
            item
            for item in inputs
            if _normalize_socket_name(item.get("name")) == "shader"
        ]
    else:
        expected = "a" if selected_index == 0 else "b"
        candidates = [
            item
            for item in inputs
            if _normalize_socket_name(item.get("name")) == expected
        ]
    if op == "Shader.Mix":
        if len(candidates) != 2:
            return ""
        selected = candidates[selected_index]
    else:
        if len(candidates) != 1:
            return ""
        selected = candidates[0]
    return _normalize_socket_name(selected.get("id") or selected.get("name"))


def _active_surface_node_ids(graph: Mapping[str, Any]) -> set[str]:
    """Return nodes reachable upstream from the active material Surface."""

    graph_nodes = {
        str(node.get("id") or ""): node
        for node in graph.get("nodes", []) or []
        if isinstance(node, Mapping) and node.get("id")
    }
    outputs = [
        node
        for node in graph_nodes.values()
        if str(node.get("op") or "") == "Output.Material"
    ]
    active_outputs = [
        node
        for node in outputs
        if bool((node.get("params") or {}).get("isActiveOutput"))
    ] or outputs
    output_ids = {str(node["id"]) for node in active_outputs}
    incoming_edges: dict[str, list[Mapping[str, Any]]] = {}
    roots: set[str] = set()
    for edge in graph.get("edges", []) or []:
        if not isinstance(edge, Mapping):
            continue
        source = edge.get("from") or {}
        target = edge.get("to") or {}
        source_id = str(source.get("node") or "")
        target_id = str(target.get("node") or "")
        if not source_id or not target_id:
            continue
        incoming_edges.setdefault(target_id, []).append(edge)
        if (
            target_id in output_ids
            and _normalize_socket_name(target.get("socket")) == "surface"
        ):
            roots.add(source_id)
    reachable = set(output_ids)
    pending = list(sorted(roots))
    while pending:
        node_id = pending.pop()
        if node_id in reachable:
            continue
        reachable.add(node_id)
        node_edges = incoming_edges.get(node_id, [])
        selected_socket = _mix_selected_input_socket(
            graph_nodes.get(node_id, {}),
            node_edges,
        )
        sources = {
            str((edge.get("from") or {}).get("node") or "")
            for edge in node_edges
            if not selected_socket
            or _normalize_socket_name((edge.get("to") or {}).get("socket"))
            == selected_socket
        }
        pending.extend(sorted((sources - {""}) - reachable))
    return reachable


def _input_default(
    node: Mapping[str, Any],
    name: str,
) -> Any:
    normalized = _normalize_socket_name(name)
    matches = [
        item
        for item in node.get("inputs", []) or []
        if isinstance(item, Mapping)
        and bool(item.get("enabled", True))
        and not bool(item.get("isUnavailable", False))
        and _normalize_socket_name(item.get("name")) == normalized
    ]
    return matches[0].get("default") if len(matches) == 1 else None


def _fixed_image_uv_transform(
    graph: Mapping[str, Any],
    image_node_id: str,
) -> tuple[dict[str, Any] | None, str]:
    """Lower static UV0 Point Mapping into a deterministic affine matrix."""

    nodes = {
        str(node.get("id") or ""): node
        for node in graph.get("nodes", []) or []
        if isinstance(node, Mapping) and node.get("id")
    }
    incoming: dict[tuple[str, str], Mapping[str, Any]] = {}
    for edge in graph.get("edges", []) or []:
        if not isinstance(edge, Mapping):
            continue
        target = edge.get("to") or {}
        incoming[
            (
                str(target.get("node") or ""),
                _normalize_socket_name(target.get("socket")),
            )
        ] = edge
    image_vector = incoming.get((image_node_id, "vector"))
    if image_vector is None:
        return None, "Image Vector is not connected to UV0."
    source = image_vector.get("from") or {}
    source_id = str(source.get("node") or "")
    source_socket = _normalize_socket_name(source.get("socket"))
    source_node = nodes.get(source_id)
    if source_node is None:
        return None, "Image Vector source is missing."
    if str(source_node.get("op") or "") in {
        "Input.TextureCoordinate",
        "Input.UVMap",
    }:
        if (
            str(source_node.get("op") or "") == "Input.TextureCoordinate"
            and source_socket != "uv"
        ):
            return None, "Only the Texture Coordinate UV output is supported."
        uv_map = str((source_node.get("params") or {}).get("uv_map") or "")
        if str(source_node.get("op") or "") == "Input.UVMap" and uv_map not in {
            "",
            "UVMap",
        }:
            return None, "Only the active UV0 map is supported."
        return {
            "coordinateSpace": "UV0",
            "operation": "Affine2D",
            "matrix": [1.0, 0.0, 0.0, 0.0, 1.0, 0.0],
        }, ""
    if str(source_node.get("op") or "") != "Vector.Mapping":
        return None, "Image Vector must use a Point Mapping node."
    if str((source_node.get("params") or {}).get("vectorType") or "") != "POINT":
        return None, "Only Point Mapping is supported."
    for socket_name in ("Location", "Rotation", "Scale"):
        if (source_id, _normalize_socket_name(socket_name)) in incoming:
            return None, f"Animated or linked Mapping {socket_name} is unsupported."
    mapping_vector = incoming.get((source_id, "vector"))
    if mapping_vector is None:
        return None, "Mapping Vector is not connected to UV0."
    vector_source = mapping_vector.get("from") or {}
    vector_node = nodes.get(str(vector_source.get("node") or ""))
    vector_socket = _normalize_socket_name(vector_source.get("socket"))
    if vector_node is None or str(vector_node.get("op") or "") not in {
        "Input.TextureCoordinate",
        "Input.UVMap",
    }:
        return None, "Point Mapping source must be UV0."
    if (
        str(vector_node.get("op") or "") == "Input.TextureCoordinate"
        and vector_socket != "uv"
    ):
        return None, "Point Mapping source must use Texture Coordinate UV."
    uv_map = str((vector_node.get("params") or {}).get("uv_map") or "")
    if str(vector_node.get("op") or "") == "Input.UVMap" and uv_map not in {
        "",
        "UVMap",
    }:
        return None, "Only the active UV0 map is supported."
    location = _input_default(source_node, "Location")
    rotation = _input_default(source_node, "Rotation")
    scale = _input_default(source_node, "Scale")
    if not all(
        isinstance(value, (list, tuple)) and len(value) >= 3
        for value in (location, rotation, scale)
    ):
        return None, "Point Mapping defaults are incomplete."
    values = [float(item) for value in (location, rotation, scale) for item in value[:3]]
    if not all(math.isfinite(item) for item in values):
        return None, "Point Mapping contains a non-finite value."
    lx, ly, _ = (float(item) for item in location[:3])
    rx, ry, rz = (float(item) for item in rotation[:3])
    sx, sy, _ = (float(item) for item in scale[:3])
    cx, sxr = math.cos(rx), math.sin(rx)
    cy, syr = math.cos(ry), math.sin(ry)
    cz, szr = math.cos(rz), math.sin(rz)
    r00 = cz * cy
    r01 = cz * syr * sxr - szr * cx
    r10 = szr * cy
    r11 = szr * syr * sxr + cz * cx
    matrix = [r00 * sx, r01 * sy, lx, r10 * sx, r11 * sy, ly]
    if not all(math.isfinite(item) for item in matrix):
        return None, "Point Mapping produced a non-finite affine matrix."
    return {
        "coordinateSpace": "UV0",
        "operation": "Affine2D",
        "matrix": matrix,
    }, ""


def _wuwa_stocking_id_node_ids(
    graph: Mapping[str, Any],
    active_node_ids: set[str],
) -> set[str]:
    """Recognize the authored linear ID -> Greater Than 0.5 mask chain."""

    nodes = {
        str(node.get("id") or ""): node
        for node in graph.get("nodes", []) or []
        if isinstance(node, Mapping) and node.get("id")
    }
    incoming: dict[str, list[Mapping[str, Any]]] = {}
    for edge in graph.get("edges", []) or []:
        if not isinstance(edge, Mapping):
            continue
        target_id = str((edge.get("to") or {}).get("node") or "")
        if target_id:
            incoming.setdefault(target_id, []).append(edge)
    result: set[str] = set()

    def texture_source_id(source_id: str) -> str:
        source_node = nodes.get(source_id)
        if source_node is None:
            return ""
        if str(source_node.get("op") or "") == "Texture.Image":
            return source_id
        if str(source_node.get("op") or "") != "Converter.SeparateColor":
            return ""
        for source_edge in incoming.get(source_id, []):
            target = source_edge.get("to") or {}
            if _normalize_socket_name(target.get("socket")) != "color":
                continue
            candidate_id = str(
                (source_edge.get("from") or {}).get("node") or ""
            )
            candidate = nodes.get(candidate_id)
            if (
                candidate is not None
                and str(candidate.get("op") or "") == "Texture.Image"
            ):
                return candidate_id
        return ""

    for node_id in sorted(active_node_ids):
        node = nodes.get(node_id)
        if node is None or str(node.get("op") or "") != "Math":
            continue
        operation = str(
            (node.get("params") or {}).get("operation") or ""
        ).upper()
        if operation != "GREATER_THAN":
            continue
        node_edges = incoming.get(node_id, [])
        for edge in node_edges:
            source = edge.get("from") or {}
            target = edge.get("to") or {}
            source_id = str(source.get("node") or "")
            texture_id = texture_source_id(source_id)
            if not texture_id:
                continue
            linked_socket = _normalize_socket_name(target.get("socket"))
            unlinked_defaults = [
                item.get("default")
                for item in node.get("inputs", []) or []
                if isinstance(item, Mapping)
                and _normalize_socket_name(item.get("id")) != linked_socket
                and isinstance(item.get("default"), (int, float))
            ]
            if any(
                abs(float(value) - 0.5) <= 1.0e-6
                for value in unlinked_defaults
            ):
                result.add(texture_id)
    return result


def _same_object_eye_het_candidates(
    material: Any,
) -> list[tuple[Any, str]]:
    """Find unique HET images authored by sibling materials on the same mesh."""

    if bpy is None:
        return []
    siblings: dict[int, Any] = {}
    for obj in getattr(bpy.data, "objects", []) or []:
        data = getattr(obj, "data", None)
        materials = list(getattr(data, "materials", []) or [])
        if not any(candidate is material for candidate in materials):
            continue
        for candidate in materials:
            if candidate is not None and candidate is not material:
                siblings[id(candidate)] = candidate
    result: dict[int, tuple[Any, str]] = {}
    for sibling in siblings.values():
        for node_id, node in sorted(
            _shader_nodes_by_stable_id(
                getattr(sibling, "node_tree", None)
            ).items()
        ):
            if str(getattr(node, "bl_idname", "")) != "ShaderNodeTexImage":
                continue
            image = getattr(node, "image", None)
            if image is None:
                continue
            _, role = _fixed_node_role_candidate(
                node,
                image,
                "wuwa_toon",
                "Eye",
            )
            if role == "EyeHET":
                result[id(image)] = (image, node_id)
    return [result[key] for key in sorted(result)]


def _collect_fixed_workflow_image_resources(
    material: Any,
    target: Path,
    workflow_kind: str,
    graph: Mapping[str, Any],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    """Seal fixed-workflow images independently of closure translation."""

    nodes = _shader_nodes_by_stable_id(getattr(material, "node_tree", None))
    active_node_ids = _active_surface_node_ids(graph)
    workflow = graph.get("workflow") or {}
    workflow_part = str(workflow.get("part") or "Body")
    stocking_id_node_ids = (
        _wuwa_stocking_id_node_ids(graph, active_node_ids)
        if workflow_kind == "wuwa_toon" and workflow_part == "Body"
        else set()
    )
    images: dict[int, dict[str, Any]] = {}
    diagnostics: list[dict[str, Any]] = []
    for node_id, node in sorted(nodes.items()):
        if str(getattr(node, "bl_idname", "")) != "ShaderNodeTexImage":
            continue
        image = getattr(node, "image", None)
        if image is None:
            diagnostics.append(
                {
                    "severity": "warning",
                    "code": "MIKU_FIXED_TEXTURE_NOT_EXPORTABLE",
                    "translationQuality": "Approximate",
                    "nodeId": node_id,
                    "message": "Image Texture node has no image datablock.",
                }
            )
            continue
        key = id(image)
        record = images.setdefault(
            key,
            {
                "image": image,
                "resourceId": _fixed_image_resource_id(image),
                "nodes": [],
                "candidates": [],
                "active": False,
            },
        )
        record["nodes"].append(node_id)
        active = node_id in active_node_ids
        record["active"] = bool(record["active"] or active)
        rank, role = _fixed_node_role_candidate(
            node,
            image,
            workflow_kind,
            workflow_part,
        )
        if role:
            record["candidates"].append((rank, role, node_id, active))
        if node_id in stocking_id_node_ids:
            record["candidates"].extend(
                (
                    (2, "IDMap", node_id, True),
                    (2, "StockingsMap", node_id, True),
                )
            )

    local_eye_het = any(
        role == "EyeHET"
        for record in images.values()
        for _, role, _, _ in record["candidates"]
    )
    if (
        workflow_kind == "wuwa_toon"
        and workflow_part == "Eye"
        and not local_eye_het
    ):
        inherited = _same_object_eye_het_candidates(material)
        if len(inherited) == 1:
            image, node_id = inherited[0]
            key = id(image)
            record = images.setdefault(
                key,
                {
                    "image": image,
                    "resourceId": _fixed_image_resource_id(image),
                    "nodes": [],
                    "candidates": [],
                    "active": False,
                },
            )
            record["nodes"].append(node_id)
            record["candidates"].append((5, "EyeHET", node_id, False))
            diagnostics.append(
                {
                    "severity": "info",
                    "code": "MIKU_WUWA_EYE_HET_INHERITED",
                    "translationQuality": "Equivalent",
                    "nodeId": node_id,
                    "message": (
                        "Inherited the unique EyeHET image from another "
                        "material on the same mesh."
                    ),
                }
            )
        elif len(inherited) > 1:
            diagnostics.append(
                {
                    "severity": "warning",
                    "code": "MIKU_FIXED_TEXTURE_ROLE_AMBIGUOUS",
                    "translationQuality": "Approximate",
                    "role": "EyeHET",
                    "message": (
                        "Multiple sibling EyeHET images were found; none was "
                        "inherited."
                    ),
                }
            )

    active_base_claims = [
        record
        for record in images.values()
        if any(
            role == "BaseMap" and active
            for _, role, _, active in record["candidates"]
        )
    ]
    if not active_base_claims:
        active_unassigned = [
            record
            for record in images.values()
            if record["active"] and not record["candidates"]
        ]
        if len(active_unassigned) == 1:
            record = active_unassigned[0]
            node_id = str(record["nodes"][0])
            record["candidates"].append((4, "BaseMap", node_id, True))

    role_claims: dict[
        str,
        list[tuple[tuple[int, int], bool, dict[str, Any], str]],
    ] = {}
    active_preferred_roles = {
        "BaseMap",
        "EmissionMap",
        "EyeHET",
        "EyeHDMF",
        "EyeUpperHighlight",
        "EyeLowerHighlight",
        "EyeEG",
    }
    for record in images.values():
        best_by_role: dict[str, tuple[tuple[int, int], bool, str]] = {}
        for rank, role, node_id, active in record["candidates"]:
            authority = (
                (rank, 0)
                if rank <= 1 or role not in active_preferred_roles
                else (rank if active else rank + 100, rank)
            )
            candidate = (authority, active, str(node_id))
            if role not in best_by_role or candidate[0] < best_by_role[role][0]:
                best_by_role[role] = candidate
        for role, (authority, active, node_id) in best_by_role.items():
            role_claims.setdefault(role, []).append(
                (authority, active, record, node_id)
            )

    bindings: dict[str, set[str]] = {
        str(record["resourceId"]): set() for record in images.values()
    }
    binding_transforms: dict[tuple[str, str], dict[str, Any]] = {}
    for role, claims in sorted(role_claims.items()):
        best_rank = min(rank for rank, _, _, _ in claims)
        winners = {
            str(record["resourceId"]): (record, node_id)
            for rank, _, record, node_id in claims
            if rank == best_rank
        }
        if len(winners) != 1:
            diagnostics.append(
                {
                    "severity": "warning",
                    "code": "MIKU_FIXED_TEXTURE_ROLE_AMBIGUOUS",
                    "translationQuality": "Approximate",
                    "role": role,
                    "message": (
                        "Multiple equally authoritative images claim this "
                        "fixed-workflow texture role; none was bound."
                    ),
                }
            )
            continue
        resource_id = next(iter(winners))
        _, winner_node_id = winners[resource_id]
        if role in {
            "EyeUpperHighlight",
            "EyeLowerHighlight",
            "EyeEG",
        }:
            transform, reason = _fixed_image_uv_transform(
                graph,
                winner_node_id,
            )
            if transform is None:
                diagnostics.append(
                    {
                        "severity": "warning",
                        "code": "MIKU_WUWA_EYE_UV_MAPPING_UNSUPPORTED",
                        "translationQuality": "Approximate",
                        "role": role,
                        "nodeId": winner_node_id,
                        "message": reason,
                    }
                )
                continue
            binding_transforms[(resource_id, role)] = transform
        bindings[resource_id].add(role)
        if role in {"BaseMap", "EmissionMap"}:
            winner_is_active = any(
                rank == best_rank
                and active
                and str(record["resourceId"]) == resource_id
                for rank, active, record, _ in claims
            )
            ignored_inactive = any(
                not active and str(record["resourceId"]) != resource_id
                for _, active, record, _ in claims
            )
            if winner_is_active and ignored_inactive:
                diagnostics.append(
                    {
                        "severity": "warning",
                        "code": "MIKU_FIXED_TEXTURE_INACTIVE_PRIMARY_IGNORED",
                        "translationQuality": "Equivalent",
                        "role": role,
                        "message": (
                            "An inactive image claimed this primary role; "
                            "the active material Surface chain was used."
                        ),
                    }
                )

    format_contracts = {
        "PNG": (".png", "image/png", 1),
        "JPEG": (".jpg", "image/jpeg", 1),
        "OPEN_EXR": (".exr", "image/x-exr", 4),
        "TARGA": (".png", "image/png", 1),
    }
    resources: list[dict[str, Any]] = []
    for record in sorted(
        images.values(), key=lambda item: str(item["resourceId"])
    ):
        image = record["image"]
        resource_id = str(record["resourceId"])
        roles = sorted(bindings[resource_id])
        color_spaces = {
            texture_role_color_space([role]) for role in roles
        }
        if len(color_spaces) > 1 or (
            "NormalMap" in roles and len(roles) > 1
        ):
            diagnostics.append(
                {
                    "severity": "warning",
                    "code": "MIKU_FIXED_TEXTURE_USAGE_CONFLICT",
                    "translationQuality": "Approximate",
                    "resourceId": resource_id,
                    "message": (
                        "One image claimed incompatible color/data roles; it "
                        "was imported without material bindings."
                    ),
                }
            )
            roles = []
        image_format = str(
            getattr(image, "file_format", "") or ""
        ).upper()
        contract = format_contracts.get(image_format)
        try:
            if contract is None:
                raise RuntimeError(
                    "MIKU_IMAGE_FORMAT_UNSUPPORTED:"
                    f"{image_format or '<missing>'}"
                )
            node_id = str((record.get("nodes") or [resource_id])[0])
            data = (
                _fixed_targa_png_bytes(image, node_id)
                if image_format == "TARGA"
                else _static_image_bytes(image, node_id)
            )
        except (OSError, RuntimeError) as error:
            diagnostics.append(
                {
                    "severity": "warning",
                    "code": "MIKU_FIXED_TEXTURE_NOT_EXPORTABLE",
                    "translationQuality": "Approximate",
                    "resourceId": resource_id,
                    "message": str(error),
                }
            )
            continue
        extension, media_type, component_bytes = contract
        relative_path = Path("Textures") / f"{resource_id}{extension}"
        destination = target / relative_path
        _write_bytes(destination, data)
        resource = make_file_reference(
            target,
            destination,
            media_type=media_type,
        )
        size = list(getattr(image, "size", ()) or ())
        color_space = texture_role_color_space(roles)
        resource.update(
            {
                "id": resource_id,
                "semantic": "FixedWorkflowTexture",
                "bindingKey": "FixedTexture_" + resource_id[:20],
                "materialBindings": [
                    {
                        "role": role,
                        **(
                            {
                                "uvTransform": binding_transforms[
                                    (resource_id, role)
                                ]
                            }
                            if (resource_id, role) in binding_transforms
                            else {}
                        ),
                    }
                    for role in roles
                ],
                "usage": "Normal" if roles == ["NormalMap"] else (
                    "Scalar" if color_space == "Linear" else "Color"
                ),
                "channel": "RGB",
                "colorSpace": color_space,
                "width": int(size[0]) if len(size) > 0 else 0,
                "height": int(size[1]) if len(size) > 1 else 0,
                "channelCount": int(getattr(image, "channels", 4) or 4),
                "componentBytes": component_bytes,
                "uvSet": "UV0",
                "projection": "FLAT",
                "interpolation": "LINEAR",
                "extension": "REPEAT",
                **(
                    {"normalConvention": "TangentOpenGLPositiveY"}
                    if roles == ["NormalMap"]
                    else {}
                ),
            }
        )
        resources.append(resource)
        if not roles:
            diagnostics.append(
                {
                    "severity": "warning",
                    "code": "MIKU_FIXED_TEXTURE_UNASSIGNED",
                    "translationQuality": "Approximate",
                    "resourceId": resource_id,
                    "message": (
                        "The image was exported but has no recognized material "
                        "texture role."
                    ),
                }
            )
    return resources, diagnostics


def export_material_bundle(
    material: Any,
    output_root: str,
    *,
    source_blend_id: str,
    persistent_material_id: str,
    mode: str = "Auto",
    workflow_kind: str = "standard_pbr",
    workflow_part: str = "Body",
    allow_appearance_approximation: bool = False,
    fidelity_policy: str = "AllowDeclaredApproximation",
    add_shader_energy_policy: str = "PreserveBlender",
    bake_resolution: int = DEFAULT_BAKE_RESOLUTION,
) -> dict[str, Any]:
    if not source_blend_id or not persistent_material_id:
        raise RuntimeError("MIKU_PERSISTENT_ID_REQUIRED")
    bake_resolution = normalize_bake_resolution(bake_resolution)
    workflow_kind = normalize_workflow_kind(workflow_kind)
    graph, material_key, ir = _prepare_material_export(
        material,
        source_blend_id=source_blend_id,
        material_key=str(getattr(material, "name", "Material") or "Material"),
        workflow_kind=workflow_kind,
        fidelity_policy=fidelity_policy,
        add_shader_energy_policy=add_shader_energy_policy,
        conversion_mode=mode,
        workflow_part=workflow_part,
    )
    _assert_no_export_time_inputs(ir)
    root = Path(output_root).resolve()
    root.mkdir(parents=True, exist_ok=True)
    asset_name = _safe_asset_name(material_key)
    target = _resolve_bundle_directory(
        root,
        asset_name,
        source_blend_id,
        persistent_material_id,
    )
    staging = Path(
        tempfile.mkdtemp(
            prefix=f".{asset_name}.miku-stage-",
            dir=str(root),
        )
    )
    try:
        result = _export_material_bundle_to_directory(
            material,
            staging,
            source_blend_id=source_blend_id,
            persistent_material_id=persistent_material_id,
            mode=mode,
            workflow_kind=workflow_kind,
            workflow_part=workflow_part,
            allow_appearance_approximation=allow_appearance_approximation,
            fidelity_policy=fidelity_policy,
            add_shader_energy_policy=add_shader_energy_policy,
            bake_resolution=bake_resolution,
            graph=graph,
            ir=ir,
            material_key=material_key,
        )
        # Re-resolve after the potentially long bake so another exporter cannot
        # silently claim the candidate directory while this bundle is staged.
        target = _resolve_bundle_directory(
            root,
            asset_name,
            source_blend_id,
            persistent_material_id,
        )
        _commit_material_directory(staging, target)
        result["bundlePath"] = str(target / f"{asset_name}.mikubundle")
        return result
    except Exception:
        if staging.exists():
            shutil.rmtree(staging, ignore_errors=True)
        raise


def _prepare_material_export(
    material: Any,
    *,
    source_blend_id: str,
    material_key: str,
    workflow_kind: str,
    fidelity_policy: str,
    add_shader_energy_policy: str,
    conversion_mode: str,
    workflow_part: str,
) -> tuple[dict[str, Any], str, dict[str, Any]]:
    """Snapshot and lower a material before any export filesystem mutation."""

    graph = snapshot_material(
        material,
        workflow_kind=workflow_kind,
        workflow_part=workflow_part,
    )
    material_key = str((graph.get("material") or {}).get("name") or material_key)
    try:
        ir = build_material_ir(
            graph,
            source_blend_id=source_blend_id,
            material_key=material_key,
            workflow_kind=workflow_kind,
            fidelity_policy=fidelity_policy,
            add_shader_energy_policy=add_shader_energy_policy,
            conversion_mode=conversion_mode,
        )
    except ValueError as error:
        if conversion_mode == "AllowMeshBake" and str(error).startswith(
            "MIKU_CLOSURE_INPUT_MISSING:"
        ):
            raise RuntimeError(
                "MIKU_FULL_PBR_BAKE_REQUIRED:Source Mesh Fidelity cannot "
                "safely lower this legacy closure graph. Select Full PBR "
                "Bake for this material."
            ) from error
        raise
    return graph, material_key, ir


def _assert_no_export_time_inputs(ir: Mapping[str, Any]) -> None:
    """Reject reachable time expressions before creating export artifacts."""

    operations = sorted(
        {
            str(expression.get("op") or "")
            for expression in ir.get("expressions", []) or []
            if isinstance(expression, Mapping)
            and str(expression.get("op") or "").startswith("Input.Time.")
        }
    )
    if operations:
        raise RuntimeError(
            "MIKU_TIME_INPUT_UNSUPPORTED:" + ",".join(operations)
        )


def _export_material_bundle_to_directory(
    material: Any,
    target: Path,
    *,
    source_blend_id: str,
    persistent_material_id: str,
    mode: str,
    workflow_kind: str,
    workflow_part: str,
    allow_appearance_approximation: bool,
    fidelity_policy: str,
    add_shader_energy_policy: str,
    bake_resolution: int,
    graph: Mapping[str, Any] | None = None,
    ir: dict[str, Any] | None = None,
    material_key: str | None = None,
) -> dict[str, Any]:
    workflow_kind = normalize_workflow_kind(workflow_kind)
    if graph is None or ir is None or material_key is None:
        graph, material_key, ir = _prepare_material_export(
            material,
            source_blend_id=source_blend_id,
            material_key=str(getattr(material, "name", "Material") or "Material"),
            workflow_kind=workflow_kind,
            fidelity_policy=fidelity_policy,
            add_shader_energy_policy=add_shader_energy_policy,
            conversion_mode=mode,
            workflow_part=workflow_part,
        )
    asset_name = _safe_asset_name(material_key)
    target.mkdir(parents=True, exist_ok=True)
    profile = default_target_profile()
    if workflow_kind in FIXED_WORKFLOWS:
        direct_resources, fixed_diagnostics = (
            _collect_fixed_workflow_image_resources(
                material,
                target,
                workflow_kind,
                graph,
            )
        )
        ir["diagnostics"] = [
            *list(ir.get("diagnostics") or []),
            *fixed_diagnostics,
        ]
        ir = _rebuild_document(ir)
    else:
        direct_resources = _collect_static_image_resources(
            material,
            ir,
            target,
        )
    source_map = build_source_map(graph, source_blend_id=source_blend_id, material_key=material_key)
    source_map["source"]["persistentMaterialId"] = persistent_material_id
    source_map = _rebuild_document(source_map)
    plan = ConversionPlanner().plan(ir, target_profile=profile, mode=mode)
    plan_errors = [
        item
        for item in plan.get("diagnostics", []) or []
        if isinstance(item, Mapping)
        and str(item.get("severity") or "").lower() == "error"
    ]
    if plan_errors:
        error_codes = {
            str(item.get("code") or "")
            for item in plan_errors
            if str(item.get("code") or "")
        }
        if (
            mode == "AllowMeshBake"
            and error_codes
            and "MIKU_FULL_PBR_BAKE_REQUIRED" not in error_codes
            and error_codes.issubset(
                SOURCE_MESH_RESOLVABLE_DIAGNOSTIC_CODES
            )
        ):
            raise RuntimeError(
                "MIKU_FULL_PBR_BAKE_REQUIRED:Source Mesh Fidelity cannot "
                "safely lower the complete static surface. Select Full PBR "
                "Bake for this material."
            )
        first = plan_errors[0]
        code = str(first.get("code") or "MIKU_CONVERSION_FAILED")
        message = str(
            first.get("message") or "Conversion planning failed"
        )
        raise RuntimeError(
            message
            if message == code or message.startswith(code + ":")
            else f"{code}:{message}"
        )
    if (
        allow_appearance_approximation
        and not plan.get("bakeJobs")
        and _has_unresolved_required_channels(ir)
    ):
        plan = _force_appearance_snapshot_plan(plan, ir)
    plan = _apply_bake_resolution_to_plan(plan, bake_resolution)
    resources: list[dict[str, Any]] = list(direct_resources)
    bake_result = None
    if plan.get("bakeJobs"):
        from .bake_client import execute_bake

        _, bake_result = execute_bake(
            graph,
            plan,
            target,
            material_name=str(getattr(material, "name", material_key)),
            persistent_source_id=source_blend_id,
            persistent_material_id=persistent_material_id,
            allow_appearance_approximation=allow_appearance_approximation,
            bake_resolution=bake_resolution,
        )
        resources.extend(
            dict(item) for item in bake_result.get("resources") or []
        )
    validate_portable_hybrid_resources(mode, resources)
    full_pbr_bake = any(
        str(job.get("route") or "") == "FullPBRBake"
        for job in plan.get("bakeJobs", []) or []
        if isinstance(job, Mapping)
    )
    source_mesh_pbr_projection = _has_source_mesh_pbr_projection(ir)
    if workflow_kind not in FIXED_WORKFLOWS:
        try:
            ir = _apply_channel_values(
                ir,
                resources,
                validate_baked_resources=not full_pbr_bake,
                authoritative_bake=full_pbr_bake,
            )
        except RuntimeError as error:
            if mode == "AllowMeshBake" and str(error).startswith(
                "MIKU_REQUIRED_CHANNEL_UNRESOLVED:"
            ):
                raise RuntimeError(
                    "MIKU_FULL_PBR_BAKE_REQUIRED:Source Mesh Fidelity left a "
                    "required PBR channel unresolved. Select Full PBR Bake "
                    "for this material."
                ) from error
            raise
    if full_pbr_bake:
        ir = _apply_full_pbr_surface_model(ir)
    elif source_mesh_pbr_projection:
        ir = _apply_source_mesh_pbr_surface_model(ir)
    ir_path = target / f"{asset_name}.miku-ir.json"
    plan_path = target / f"{asset_name}.miku-plan.json"
    source_map_path = target / f"{asset_name}.miku-source-map.json"
    _write_json(ir_path, ir)
    _write_json(plan_path, plan)
    _write_json(source_map_path, source_map)
    manifest_payload = {
        "materialKey": material_key,
        "persistentSourceId": source_blend_id,
        "persistentMaterialId": persistent_material_id,
        "irHash": ir["canonicalHash"],
        "planHash": plan["canonicalHash"],
        "targetProfileHash": profile["canonicalHash"],
        "surfaceModel": ir.get("surfaceModelPlan") or {},
        "closureGraph": ir.get("closureGraph") or {},
        "weightedClosures": ir.get("weightedClosures") or {},
        "regions": plan.get("regions", []),
        "bakeJobs": plan.get("bakeJobs", []),
        "executor": "miku_blender.closure-aware-2+gpl-bake-protocol-1.0",
        "completion": {
            "status": "completed",
            "exitCode": 0,
            "marker": "MIKU_CONVERSION_COMPLETE",
            "artifacts": [
                {
                    "id": item["id"],
                    "relativePath": item["relativePath"],
                    "sha256": item["sha256"],
                    "byteLength": item["byteLength"],
                }
                for item in resources
            ],
        },
        "diagnostics": list(plan.get("diagnostics", []))
        + list((bake_result or {}).get("diagnostics") or []),
    }

    manifest = make_document("miku-conversion-manifest-1.0", manifest_payload)
    manifest_path = target / f"{asset_name}.miku-manifest.json"
    _write_json(manifest_path, manifest)
    bundle_payload = {
        "materialKey": material_key,
        "sourceName": material_key,
        "persistentSourceId": source_blend_id,
        "persistentMaterialId": persistent_material_id,
        "targetProfileHash": profile["canonicalHash"],
        "ir": make_file_reference(target, ir_path, media_type="application/json"),
        "plan": make_file_reference(target, plan_path, media_type="application/json"),
        "manifest": make_file_reference(target, manifest_path, media_type="application/json"),
        "sourceMap": {
            **make_file_reference(target, source_map_path, media_type="application/json"),
            "editorOnly": True,
        },
        "resources": resources,
    }
    bundle_payload["sealedDigest"] = compute_sealed_digest(bundle_payload)
    bundle_kind = "miku-bundle-1.0"
    bundle = make_document(bundle_kind, bundle_payload)
    validate_document(ir, "miku-material-ir-2.0")
    validate_document(plan, "miku-conversion-plan-1.0")
    validate_document(source_map, "miku-blender-source-map-1.0")
    validate_document(manifest, "miku-conversion-manifest-1.0")
    validate_bundle_document(bundle)
    bundle_path = target / f"{asset_name}.mikubundle"
    _write_json(bundle_path, bundle)
    return {
        "materialKey": material_key,
        "workflow": workflow_kind,
        "bundle": bundle,
        "manifest": manifest,
        "bundleFileName": bundle_path.name,
    }


def _safe_asset_name(value: str) -> str:
    normalized = unicodedata.normalize("NFC", str(value or "Material")).strip()
    normalized = _INVALID_ASSET_CHARS.sub("_", normalized).rstrip(" .")
    if not normalized:
        normalized = "Material"
    if normalized.upper() in _RESERVED_ASSET_NAMES:
        normalized = "_" + normalized
    if len(normalized) > 120:
        digest = hashlib.sha256(normalized.encode("utf-8")).hexdigest()[:12]
        normalized = normalized[:107].rstrip(" .") + "-" + digest
    return normalized


def _short_identity(value: str) -> str:
    compact = re.sub(r"[^0-9A-Za-z]", "", str(value))
    if len(compact) >= 12:
        return compact[:12].lower()
    return hashlib.sha256(str(value).encode("utf-8")).hexdigest()[:12]


def _read_bundle_identity(path: Path) -> tuple[str, str] | None:
    try:
        if path.stat().st_size > _MAX_IDENTITY_DOCUMENT_BYTES:
            return None
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError):
        return None
    if document.get("documentKind") not in {
        "miku-bundle-1.0",
        "migr-bundle-1.0",
        "migr-bundle-2.0",
        "migr-bundle-2.1",
        "migr-bundle-2.2",
    }:
        return None
    source_id = str(document.get("persistentSourceId") or "").strip()
    material_id = str(document.get("persistentMaterialId") or "").strip()
    if not source_id or not material_id:
        return None
    return source_id, material_id


def _directory_bundle_identities(directory: Path) -> list[tuple[str, str]]:
    identities: set[tuple[str, str]] = set()
    try:
        bundle_paths = sorted(
            (
                *directory.glob("*.mikubundle"),
                *directory.glob("*.migrbundle"),
            ),
            key=lambda item: item.name,
        )
    except OSError:
        return []
    for bundle_path in bundle_paths:
        if bundle_path.is_symlink() or not bundle_path.is_file():
            continue
        identity = _read_bundle_identity(bundle_path)
        if identity is not None:
            identities.add(identity)
    return sorted(identities)


def _resolve_bundle_directory(
    root: Path,
    asset_name: str,
    source_id: str,
    material_id: str,
) -> Path:
    """Reuse a bundle directory by identity or allocate a collision-safe name."""

    expected = (source_id, material_id)
    matches: list[Path] = []
    try:
        children = sorted(root.iterdir(), key=lambda item: item.name)
    except OSError as exc:
        raise RuntimeError(f"MIKU_OUTPUT_ROOT_UNREADABLE:{root}:{exc}") from exc
    if len(children) > _MAX_IDENTITY_SCAN_DIRECTORIES:
        raise RuntimeError(
            f"MIKU_OUTPUT_DIRECTORY_LIMIT:{len(children)}:"
            f"max={_MAX_IDENTITY_SCAN_DIRECTORIES}"
        )
    for child in children:
        if child.name.startswith(".") or child.is_symlink() or not child.is_dir():
            continue
        if expected in _directory_bundle_identities(child):
            matches.append(child)
    if len(matches) > 1:
        paths = "|".join(str(path) for path in matches)
        raise RuntimeError(
            "MIKU_OUTPUT_IDENTITY_DUPLICATE:"
            f"sourceId={source_id}:materialId={material_id}:directories={paths}"
        )
    if matches:
        return matches[0]

    target = root / f"{asset_name}__{_short_identity(material_id)}"
    if not target.exists():
        return target
    identities = _directory_bundle_identities(target) if target.is_dir() else []
    if identities:
        actual_source_id, actual_material_id = identities[0]
    else:
        actual_source_id, actual_material_id = "<unowned>", "<unowned>"
    raise RuntimeError(
        "MIKU_OUTPUT_IDENTITY_CONFLICT:"
        f"directory={target}:"
        f"requestedSourceId={source_id}:requestedMaterialId={material_id}:"
        f"existingSourceId={actual_source_id}:existingMaterialId={actual_material_id}"
    )


def _commit_material_directory(staging: Path, target: Path) -> None:
    backup = target.with_name(f".{target.name}.miku-backup-{uuid.uuid4().hex}")
    moved_existing = False
    try:
        if target.exists():
            os.replace(target, backup)
            moved_existing = True
        os.replace(staging, target)
    except Exception:
        if moved_existing and target.exists():
            shutil.rmtree(target, ignore_errors=True)
        if moved_existing and backup.exists():
            os.replace(backup, target)
        raise
    if backup.exists():
        shutil.rmtree(backup)


def export_selected_materials(
    output_root: str,
    *,
    mode: str = "Auto",
    source_blend_id: str = "",
    material_names: set[str] | None = None,
    default_workflow: str = "standard_pbr",
    allow_appearance_approximation: bool = False,
    fidelity_policy: str = "AllowDeclaredApproximation",
    add_shader_energy_policy: str = "PreserveBlender",
    bake_resolution: int = DEFAULT_BAKE_RESOLUTION,
) -> list[dict[str, Any]]:
    bake_resolution = normalize_bake_resolution(bake_resolution)
    try:
        import bpy
    except ImportError as exc:  # pragma: no cover - only used outside Blender
        raise RuntimeError("miku_blender export requires Blender 5.2") from exc
    identity_storage = _source_identity_storage(
        bpy.data,
        getattr(getattr(bpy, "context", None), "scene", None),
    )
    temporary_source_identity = False
    if source_blend_id:
        if not str(_id_property_get(identity_storage, _SOURCE_ID_PROPERTY, "") or "").strip():
            if not _try_id_property_set(identity_storage, _SOURCE_ID_PROPERTY, source_blend_id):
                _session_identity_set(
                    _SESSION_SOURCE_IDS,
                    identity_storage,
                    source_blend_id,
                )
                temporary_source_identity = True
    else:
        source_blend_id, temporary_source_identity = _ensure_persistent_source_id(
            bpy.data,
            storage=identity_storage,
        )
    assigned = []
    seen = set()
    for obj in getattr(bpy.data, "objects", []) or []:
        for slot in getattr(obj, "material_slots", []) or []:
            material = getattr(slot, "material", None)
            if (
                material is not None
                and material.name not in seen
                and (material_names is None or material.name in material_names)
            ):
                seen.add(material.name)
                assigned.append(material)
    if material_names is not None:
        for material in getattr(bpy.data, "materials", []) or []:
            if (
                material.name in material_names
                and material.name not in seen
            ):
                seen.add(material.name)
                assigned.append(material)
        missing = sorted(material_names - {material.name for material in assigned})
        if missing:
            raise RuntimeError("MIKU_MATERIAL_FILTER_NOT_FOUND:" + ",".join(missing))
    all_materials = _data_materials(bpy.data, assigned)
    identities, identity_warnings = _ensure_material_identities(
        all_materials,
        source_blend_id,
        Path(output_root),
        required_materials=assigned,
    )
    identity_warnings = [
        *_source_identity_warnings(
            bpy.data,
            identity_storage,
            source_blend_id,
        ),
        *identity_warnings,
    ]
    default_workflow = normalize_workflow_kind(default_workflow)
    results = []
    for material in sorted(assigned, key=lambda item: item.name):
        result = export_material_bundle(
            material,
            output_root,
            source_blend_id=source_blend_id,
            persistent_material_id=identities[_owner_session_key(material)],
            mode=mode,
            workflow_kind=_resolved_material_workflow(material, default_workflow),
            workflow_part=normalize_workflow_part(getattr(material, "miku_workflow_part", "Body")),
            allow_appearance_approximation=allow_appearance_approximation,
            fidelity_policy=fidelity_policy,
            add_shader_energy_policy=add_shader_energy_policy,
            bake_resolution=bake_resolution,
        )
        result["sourceIdentityTemporary"] = temporary_source_identity
        result["identityWarnings"] = list(identity_warnings)
        results.append(result)
    return results


def _active_material_slot_state(context: Any) -> tuple[Any | None, str | None]:
    """Return only the active object's active-slot material and a UI diagnostic."""

    obj = getattr(context, "object", None)
    if obj is None:
        return None, "No active object. Select an object with a material."
    slots = getattr(obj, "material_slots", None)
    if slots is None or len(slots) == 0:
        return None, "The active object has no material slots."
    try:
        index = int(getattr(obj, "active_material_index", -1))
    except (TypeError, ValueError):
        index = -1
    if index < 0 or index >= len(slots):
        return None, "The active material slot index is invalid."
    material = getattr(slots[index], "material", None)
    if material is None:
        return None, "The active material slot is empty."

    space = getattr(context, "space_data", None)
    shader_type = getattr(space, "shader_type", None)
    if shader_type not in (None, "OBJECT"):
        return None, "Set the Shader Editor context to Object materials."
    if bool(getattr(space, "pin", False)):
        editor_material = getattr(space, "id", None)
        if editor_material is None:
            return None, "The pinned Shader Editor has no active material."
        if editor_material is not material:
            return None, (
                "The Shader Editor is pinned to a different material. "
                "Unpin it or activate the matching material slot."
            )
    return material, None


def _normalized_blend_identity_path(filepath: str) -> str:
    path = Path(filepath).expanduser().resolve(strict=False)
    normalized = os.path.normcase(path.as_posix())
    return unicodedata.normalize("NFC", normalized)


def _source_identity_warnings(
    data: Any,
    storage: Any,
    source_id: str,
) -> list[str]:
    filepath = str(getattr(data, "filepath", "") or "").strip()
    if not filepath:
        return []
    current_origin = _normalized_blend_identity_path(filepath)
    stored_origin = str(
        _id_property_get(storage, _SOURCE_ORIGIN_PROPERTY, "") or ""
    ).strip()
    if not stored_origin:
        _try_id_property_set(storage, _SOURCE_ORIGIN_PROPERTY, current_origin)
        return []
    if stored_origin == current_origin:
        return []
    return [
        "MIKU_SOURCE_ID_COPY_DETECTED:"
        f"sourceId={source_id}:"
        f"originFingerprint={hashlib.sha256(stored_origin.encode('utf-8')).hexdigest()[:12]}:"
        f"currentFingerprint={hashlib.sha256(current_origin.encode('utf-8')).hexdigest()[:12]}:"
        "use Fork Source Identity if this copy is an independent source"
    ]


def _ensure_persistent_source_id(
    data: Any,
    storage: Any | None = None,
) -> tuple[str, bool]:
    """Return a stable hidden source identity and whether it is session-only."""

    storage = data if storage is None else storage
    _copy_legacy_id_property(
        storage,
        _LEGACY_SOURCE_ID_PROPERTY,
        _SOURCE_ID_PROPERTY,
    )
    if data is not storage:
        _copy_legacy_id_property(
            data,
            _LEGACY_SOURCE_ID_PROPERTY,
            _SOURCE_ID_PROPERTY,
        )
    filepath = str(getattr(data, "filepath", "") or "").strip()
    existing = str(
        _id_property_get(storage, _SOURCE_ID_PROPERTY, "")
        or _id_property_get(data, _SOURCE_ID_PROPERTY, "")
        or _session_identity_get(_SESSION_SOURCE_IDS, storage)
        or ""
    ).strip()
    if existing:
        persisted = bool(
            _id_property_get(storage, _SOURCE_ID_PROPERTY, "")
            or _id_property_get(data, _SOURCE_ID_PROPERTY, "")
        )
        return existing, not bool(filepath) or not persisted

    if filepath:
        identity_name = "miku://blend/" + _normalized_blend_identity_path(filepath)
        source_id = str(uuid.uuid5(uuid.NAMESPACE_URL, identity_name))
        temporary = False
    else:
        source_id = str(uuid.uuid4())
        temporary = True
    if not _try_id_property_set(storage, _SOURCE_ID_PROPERTY, source_id):
        _session_identity_set(_SESSION_SOURCE_IDS, storage, source_id)
        temporary = True
    return source_id, temporary


def _id_property_get(owner: Any, name: str, default: Any = None) -> Any:
    try:
        return getattr(owner, "get")(name, default)
    except (AttributeError, TypeError):
        return getattr(owner, name, default)


def _id_property_set(owner: Any, name: str, value: Any) -> None:
    try:
        owner[name] = value
    except (AttributeError, TypeError):
        setattr(owner, name, value)


def _try_id_property_set(owner: Any, name: str, value: Any) -> bool:
    try:
        _id_property_set(owner, name, value)
        return True
    except (AttributeError, ReferenceError, RuntimeError, TypeError):
        return False


def _copy_legacy_id_property(
    owner: Any,
    legacy_name: str,
    current_name: str,
) -> bool:
    """Copy one legacy MiGR ID property once without deleting the source."""

    if owner is None or _id_property_get(owner, current_name, ""):
        return False
    value = _id_property_get(owner, legacy_name, "")
    if value in (None, ""):
        return False
    return _try_id_property_set(owner, current_name, value)


def _owner_session_key(owner: Any) -> int:
    try:
        return int(owner.as_pointer())
    except (AttributeError, ReferenceError, RuntimeError, TypeError, ValueError):
        return id(owner)


def _session_identity_get(
    cache: dict[int, tuple[Any, str]],
    owner: Any,
) -> str:
    entry = cache.get(_owner_session_key(owner))
    return "" if entry is None else entry[1]


def _session_identity_set(
    cache: dict[int, tuple[Any, str]],
    owner: Any,
    value: str,
) -> None:
    # Retain the owner with the value so Python cannot reuse a fallback id()
    # for a different read-only data-block during the same Blender session.
    cache[_owner_session_key(owner)] = (owner, value)


def _session_identity_pop(
    cache: dict[int, tuple[Any, str]],
    owner: Any,
) -> None:
    cache.pop(_owner_session_key(owner), None)


def _source_identity_storage(data: Any, current_scene: Any) -> Any:
    """Choose one blend-wide persistent store while retaining legacy scene IDs."""

    scenes = getattr(data, "scenes", None)
    try:
        candidates = list(scenes or ())
    except TypeError:
        candidates = []
    for scene in candidates:
        if _id_property_get(scene, _SOURCE_ID_PROPERTY, ""):
            return scene
    if candidates:
        return candidates[0]
    return current_scene


def _data_materials(data: Any, required: list[Any] | tuple[Any, ...] = ()) -> list[Any]:
    try:
        materials = list(getattr(data, "materials", ()) or ())
    except (AttributeError, ReferenceError, RuntimeError, TypeError):
        materials = []
    known = {_owner_session_key(material) for material in materials}
    for material in required:
        key = _owner_session_key(material)
        if key not in known:
            materials.append(material)
            known.add(key)
    return materials


def _load_legacy_identity_registry(
    root: Path,
) -> tuple[dict[str, Any] | None, list[str]]:
    registry_path = root.resolve() / _LEGACY_IDENTITY_REGISTRY
    if not registry_path.is_file():
        return None, []
    try:
        if registry_path.stat().st_size > _MAX_IDENTITY_DOCUMENT_BYTES:
            raise ValueError("file exceeds identity document limit")
        registry = json.loads(registry_path.read_text(encoding="utf-8"))
        if not isinstance(registry, dict):
            raise ValueError("root must be an object")
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as exc:
        return None, [f"MIKU_LEGACY_IDENTITY_REGISTRY_INVALID:{registry_path}:{exc}"]
    return registry, []


def _read_legacy_material_identities(
    root: Path,
    source_id: str,
) -> tuple[dict[str, str], list[str]]:
    registry_path = root.resolve() / _LEGACY_IDENTITY_REGISTRY
    registry, warnings = _load_legacy_identity_registry(root)
    if registry is None:
        return {}, warnings

    registry_source_id = str(registry.get("persistentSourceId") or "").strip()
    if registry_source_id != source_id:
        return {}, [
            "MIKU_LEGACY_IDENTITY_SOURCE_MISMATCH:"
            f"{registry_path}:registrySourceId={registry_source_id or '<missing>'}:"
            f"currentSourceId={source_id}"
        ]
    raw_materials = registry.get("materials")
    if not isinstance(raw_materials, dict):
        return {}, [
            f"MIKU_LEGACY_IDENTITY_REGISTRY_INVALID:{registry_path}:materials must be an object"
        ]
    identities = {
        str(name): str(material_id).strip()
        for name, material_id in raw_materials.items()
        if str(name) and str(material_id).strip()
    }
    return identities, []


def _ensure_material_identities(
    materials: list[Any],
    source_id: str,
    output_root: Path,
    *,
    required_materials: list[Any] | tuple[Any, ...] | None = None,
) -> tuple[dict[int, str], list[str]]:
    """Persist Material IDs, migrate legacy IDs, and repair copied duplicates."""

    legacy, warnings = _read_legacy_material_identities(output_root, source_id)
    unique: dict[int, Any] = {}
    for material in materials:
        unique.setdefault(_owner_session_key(material), material)
    ordered = sorted(
        unique.values(),
        key=lambda item: (
            str(getattr(item, "name", "")).casefold(),
            str(getattr(item, "name", "")),
            _owner_session_key(item),
        ),
    )
    required_keys = (
        set(unique)
        if required_materials is None
        else {_owner_session_key(material) for material in required_materials}
    )
    identities: dict[int, str] = {}
    identity_owners: dict[str, Any] = {}
    for material in ordered:
        key = _owner_session_key(material)
        name = str(getattr(material, "name", "Material") or "Material")
        _copy_legacy_id_property(
            material,
            _LEGACY_MATERIAL_ID_PROPERTY,
            _MATERIAL_ID_PROPERTY,
        )
        for legacy_name, current_name in (
            ("migr_workflow", "miku_workflow"),
            ("migr_workflow_kind", "miku_workflow_kind"),
            ("migr_workflow_part", "miku_workflow_part"),
            ("migr_normal_convention", "miku_normal_convention"),
        ):
            _copy_legacy_id_property(
                material,
                legacy_name,
                current_name,
            )
        material_id = str(
            _id_property_get(material, _MATERIAL_ID_PROPERTY, "")
            or _session_identity_get(_SESSION_MATERIAL_IDS, material)
            or ""
        ).strip()
        if not material_id and key not in required_keys:
            continue
        migrated = False
        if not material_id:
            material_id = legacy.get(name, "") or str(uuid.uuid4())
            migrated = name in legacy
            if not _try_id_property_set(material, _MATERIAL_ID_PROPERTY, material_id):
                _session_identity_set(_SESSION_MATERIAL_IDS, material, material_id)
                warnings.append(
                    f"MIKU_MATERIAL_ID_SESSION_ONLY:{name}:materialId={material_id}"
                )
            elif migrated:
                warnings.append(
                    f"MIKU_LEGACY_MATERIAL_ID_MIKUATED:{name}:materialId={material_id}"
                )

        if material_id in identity_owners:
            previous_id = material_id
            material_id = str(uuid.uuid4())
            if not _try_id_property_set(material, _MATERIAL_ID_PROPERTY, material_id):
                _session_identity_set(_SESSION_MATERIAL_IDS, material, material_id)
            warnings.append(
                "MIKU_MATERIAL_ID_DUPLICATE_REPAIRED:"
                f"{name}:duplicateOf={getattr(identity_owners[previous_id], 'name', 'Material')}:"
                f"oldMaterialId={previous_id}:newMaterialId={material_id}"
            )
        identities[key] = material_id
        identity_owners[material_id] = material
    return identities, warnings


def migrate_legacy_identities(
    data: Any,
    output_root: str | Path,
    *,
    current_scene: Any | None = None,
) -> dict[str, Any]:
    """Explicitly adopt one legacy registry into Blender custom properties."""

    root = Path(output_root)
    registry_path = root.resolve() / _LEGACY_IDENTITY_REGISTRY
    registry, warnings = _load_legacy_identity_registry(root)
    if registry is None:
        if not warnings:
            warnings.append(f"MIKU_LEGACY_IDENTITY_REGISTRY_NOT_FOUND:{registry_path}")
        return {
            "persistentSourceId": "",
            "materialCount": 0,
            "sourceIdentityTemporary": False,
            "identityWarnings": warnings,
        }
    registry_source_id = str(registry.get("persistentSourceId") or "").strip()
    raw_materials = registry.get("materials")
    if not registry_source_id or not isinstance(raw_materials, dict):
        warnings.append(
            f"MIKU_LEGACY_IDENTITY_REGISTRY_INVALID:{registry_path}:"
            "source ID and materials are required"
        )
        return {
            "persistentSourceId": "",
            "materialCount": 0,
            "sourceIdentityTemporary": False,
            "identityWarnings": warnings,
        }

    storage = _source_identity_storage(data, current_scene)
    stored_source_id = str(
        _id_property_get(storage, _SOURCE_ID_PROPERTY, "") or ""
    ).strip()
    existing_source_id = str(
        stored_source_id
        or _session_identity_get(_SESSION_SOURCE_IDS, storage)
        or ""
    ).strip()
    if existing_source_id and existing_source_id != registry_source_id:
        warnings.append(
            "MIKU_LEGACY_IDENTITY_SOURCE_MISMATCH:"
            f"{registry_path}:registrySourceId={registry_source_id}:"
            f"currentSourceId={existing_source_id}"
        )
        return {
            "persistentSourceId": existing_source_id,
            "materialCount": 0,
            "sourceIdentityTemporary": False,
            "identityWarnings": warnings,
        }

    source_persisted = bool(stored_source_id)
    if not source_persisted:
        source_persisted = _try_id_property_set(
            storage,
            _SOURCE_ID_PROPERTY,
            registry_source_id,
        )
    if not source_persisted:
        _session_identity_set(_SESSION_SOURCE_IDS, storage, registry_source_id)
        warnings.append(
            f"MIKU_SOURCE_ID_SESSION_ONLY:sourceId={registry_source_id}"
        )
    materials = _data_materials(data)
    eligible = [
        material
        for material in materials
        if str(
            raw_materials.get(
                str(getattr(material, "name", "Material") or "Material"),
                "",
            )
        ).strip()
        and not str(
            _id_property_get(material, _MATERIAL_ID_PROPERTY, "")
            or _session_identity_get(_SESSION_MATERIAL_IDS, material)
            or ""
        ).strip()
    ]
    _, material_warnings = _ensure_material_identities(
        materials,
        registry_source_id,
        root,
        required_materials=eligible,
    )
    warnings.extend(material_warnings)
    warnings.extend(
        _source_identity_warnings(data, storage, registry_source_id)
    )
    return {
        "persistentSourceId": registry_source_id,
        "materialCount": len(eligible),
        "sourceIdentityTemporary": not source_persisted
        or not bool(str(getattr(data, "filepath", "") or "").strip()),
        "identityWarnings": warnings,
    }


def fork_source_identity(
    data: Any,
    *,
    current_scene: Any | None = None,
) -> dict[str, Any]:
    """Explicitly make a copied blend an independent Miku identity source."""

    storage = _source_identity_storage(data, current_scene)
    source_id = str(uuid.uuid4())
    source_persisted = _try_id_property_set(storage, _SOURCE_ID_PROPERTY, source_id)
    if source_persisted:
        _session_identity_pop(_SESSION_SOURCE_IDS, storage)
    else:
        _session_identity_set(_SESSION_SOURCE_IDS, storage, source_id)
    filepath = str(getattr(data, "filepath", "") or "").strip()
    if filepath:
        _try_id_property_set(
            storage,
            _SOURCE_ORIGIN_PROPERTY,
            _normalized_blend_identity_path(filepath),
        )

    warnings: list[str] = []
    materials = _data_materials(data)
    for material in materials:
        material_id = str(uuid.uuid4())
        key = _owner_session_key(material)
        if _try_id_property_set(material, _MATERIAL_ID_PROPERTY, material_id):
            _session_identity_pop(_SESSION_MATERIAL_IDS, material)
        else:
            _session_identity_set(_SESSION_MATERIAL_IDS, material, material_id)
            warnings.append(
                "MIKU_MATERIAL_ID_SESSION_ONLY:"
                f"{getattr(material, 'name', 'Material')}:materialId={material_id}"
            )
    if not source_persisted:
        warnings.append(f"MIKU_SOURCE_ID_SESSION_ONLY:sourceId={source_id}")
    return {
        "persistentSourceId": source_id,
        "materialCount": len(materials),
        "sourceIdentityTemporary": not source_persisted
        or not bool(str(getattr(data, "filepath", "") or "").strip()),
        "identityWarnings": warnings,
    }


def _safe_workflow_kind(value: Any, fallback: str = "standard_pbr") -> str:
    raw = str(value or fallback)
    if raw.strip().casefold() == "generic_toon":
        raise ValueError("MIKU_WORKFLOW_RETIRED:generic_toon")
    try:
        return normalize_workflow_kind(raw)
    except (TypeError, ValueError):
        return fallback


def _material_workflow_preview(material: Any, scene_settings: Any) -> str:
    if bool(_id_property_get(material, _WORKFLOW_MIKUATION_PROPERTY, False)):
        return _safe_workflow_kind(getattr(material, "miku_workflow_kind", "standard_pbr"))
    legacy = str(getattr(material, "miku_workflow", "inherit") or "inherit")
    if legacy.lower() == "inherit":
        legacy = getattr(scene_settings, "default_workflow", "standard_pbr")
    return _safe_workflow_kind(legacy)


def _migrate_material_workflow(material: Any, scene_settings: Any) -> str:
    """Resolve the legacy inherit setting once and persist an explicit workflow."""

    if bool(_id_property_get(material, _WORKFLOW_MIKUATION_PROPERTY, False)):
        workflow = _safe_workflow_kind(getattr(material, "miku_workflow_kind", "standard_pbr"))
        if getattr(material, "miku_workflow_kind", None) != workflow:
            material.miku_workflow_kind = workflow
        return workflow

    workflow = _material_workflow_preview(material, scene_settings)
    material.miku_workflow_kind = workflow
    _id_property_set(material, _WORKFLOW_MIKUATION_PROPERTY, True)
    return workflow


def _queue_material_workflow_migration(
    material: Any,
    scene_settings: Any,
) -> None:
    """Defer ID writes because Blender forbids them during Panel.draw()."""

    if bpy is None or bool(_id_property_get(material, _WORKFLOW_MIKUATION_PROPERTY, False)):
        return
    try:
        key = int(material.as_pointer())
    except (AttributeError, ReferenceError, TypeError):
        key = id(material)
    if key in _PENDING_WORKFLOW_MIKUATIONS:
        return
    _PENDING_WORKFLOW_MIKUATIONS.add(key)

    def migrate_after_draw() -> None:
        try:
            _migrate_material_workflow(material, scene_settings)
        except (AttributeError, ReferenceError, RuntimeError, TypeError, ValueError) as exc:
            material_name = str(getattr(material, "name", "<unknown>"))
            print(f"MIKU_WORKFLOW_MIKUATION_FAILED:{material_name}:{exc}")
        finally:
            _PENDING_WORKFLOW_MIKUATIONS.discard(key)
        window_manager = getattr(
            getattr(bpy, "context", None),
            "window_manager",
            None,
        )
        for window in getattr(window_manager, "windows", ()):
            for area in window.screen.areas:
                if area.type == "NODE_EDITOR":
                    area.tag_redraw()
        return None

    bpy.app.timers.register(migrate_after_draw, first_interval=0.0)


def export_current_material(
    context: Any,
    output_root: str,
    *,
    mode: str = "Auto",
    allow_appearance_approximation: bool = False,
    fidelity_policy: str = "AllowDeclaredApproximation",
    add_shader_energy_policy: str = "PreserveBlender",
    bake_resolution: int = DEFAULT_BAKE_RESOLUTION,
    data: Any | None = None,
) -> dict[str, Any]:
    """Export exactly the active object's active-slot material."""

    material, diagnostic = _active_material_slot_state(context)
    if material is None:
        raise RuntimeError("MIKU_ACTIVE_MATERIAL_REQUIRED: " + str(diagnostic))
    if data is None:
        if bpy is None:
            raise RuntimeError("miku_blender export requires Blender 5.2")
        data = bpy.data

    settings = getattr(getattr(context, "scene", None), "miku_settings", None)
    if settings is None:
        raise RuntimeError("MIKU_SCENE_SETTINGS_REQUIRED")
    source_id, temporary_identity = _ensure_persistent_source_id(
        data,
        storage=_source_identity_storage(
            data,
            getattr(context, "scene", None),
        ),
    )
    # The Blender-facing export contract is intentionally Standard PBR only.
    # The lower-level export_material_bundle API still accepts explicit legacy
    # workflow values for scripts and historical fixture compatibility.
    workflow_kind = "standard_pbr"
    workflow_part = "Body"
    identities, identity_warnings = _ensure_material_identities(
        _data_materials(data, [material]),
        source_id,
        Path(output_root),
        required_materials=[material],
    )
    identity_warnings = [
        *_source_identity_warnings(
            data,
            _source_identity_storage(data, getattr(context, "scene", None)),
            source_id,
        ),
        *identity_warnings,
    ]
    result = export_material_bundle(
        material,
        output_root,
        source_blend_id=source_id,
        persistent_material_id=identities[_owner_session_key(material)],
        mode=mode,
        workflow_kind=workflow_kind,
        workflow_part=workflow_part,
        allow_appearance_approximation=allow_appearance_approximation,
        fidelity_policy=fidelity_policy,
        add_shader_energy_policy=add_shader_energy_policy,
        bake_resolution=bake_resolution,
    )
    result["sourceIdentityTemporary"] = temporary_identity
    result["identityWarnings"] = identity_warnings
    return result


def _resolved_material_workflow(material: Any, scene_default: str) -> str:
    if bool(_id_property_get(material, _WORKFLOW_MIKUATION_PROPERTY, False)):
        return _safe_workflow_kind(getattr(material, "miku_workflow_kind", "standard_pbr"))
    override = str(getattr(material, "miku_workflow", "inherit") or "inherit")
    return normalize_workflow_kind(scene_default if override.lower() == "inherit" else override)


def _augment_standard_pbr_semantics(
    nodes: list[dict[str, Any]],
    edges: list[dict[str, Any]],
    initial_slots: dict[str, dict[str, Any]],
    source_nodes: list[Any] | None = None,
) -> tuple[
    dict[str, dict[str, Any]],
    dict[str, Any],
    dict[str, dict[str, Any]],
    dict[str, Any],
]:
    """Re-run the legacy Standard PBR semantic extractor over the snapshot.

    The simplified ``_principled_slots_from_snapshot`` pass only handles
    a handful of direct BSDFs.  For programmable-node graphs (e.g. wood
    materials mixing colour ramps, math nodes, and Principled inputs),
    the snapshot is augmented with the richer legacy extractor so the
    Unity importer receives the full Standard PBR slot set (BaseColor,
    Metallic, Roughness, Normal, ORM packed textures, …) instead of an
    empty ``slots`` map.

    Returns ``(resources, entry, merged_slots, metadata)``.  ``entry`` may be empty
    when the graph has no active Material Output; in that case the legacy
    extractor cannot run and ``merged_slots`` is just ``initial_slots``.
    """

    metadata: dict[str, Any] = {
        "workflow": "Metallic",
        "source": {
            "shader": "SemanticMaps",
            "blenderMaterial": "",
            "confidence": 0.0,
        },
        "packedTextures": {},
        "diagnostics": [],
    }

    # 1. Build ``entry`` for the legacy extractor: it expects
    #    ``entry.surface`` to point at the active Material Output node
    #    and its ``Surface`` input socket identifier.
    entry: dict[str, Any] = {}
    outputs = [
        node for node in nodes if str(node.get("op") or "") == "Output.Material"
    ]
    if outputs:
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
        surface_socket = next(
            (
                sock
                for sock in output.get("inputs", []) or []
                if str(sock.get("id") or sock.get("name") or "").lower()
                in {"surface", "bsdf"}
            ),
            None,
        )
        if surface_socket is not None:
            socket_id = str(
                surface_socket.get("id") or surface_socket.get("name") or "Surface"
            )
            entry["surface"] = {"node": str(output.get("id") or ""), "socket": socket_id}

    # 2. Build a minimal ``resources`` table that maps every image
    #    resource ID referenced by a Texture.Image node to the
    #    underlying image path.  The legacy extractor only consumes
    #    ``resource.path`` / ``resource.uri`` / ``resource.exportFileName``;
    #    the values below are sufficient for slot population.
    resources: dict[str, dict[str, Any]] = {}
    source_lookup = {id(node): node for node in (source_nodes or [])}
    for node in nodes:
        if str(node.get("op") or "") != "Texture.Image":
            continue
        params = node.get("params") or {}
        image = source_lookup.get(params.get("sourcePointer", id(None)), None)
        # Determine the file path / resource identity.  The Miku 1.0
        # snapshot stores the metadata in ``params.image`` as a dict;
        # older B2U snapshots use ``params.filepath`` / ``params.image``
        # (string).  Fall back to live source nodes when present.
        filepath = ""
        image_dict = params.get("image")
        if isinstance(image_dict, Mapping):
            filepath = str(
                image_dict.get("filepath")
                or image_dict.get("path")
                or image_dict.get("name")
                or ""
            )
        elif isinstance(image_dict, str):
            filepath = image_dict
        if not filepath:
            for key in ("filepath", "imagePath", "path", "uri"):
                value = params.get(key)
                if value:
                    filepath = str(value)
                    break
        if not filepath and image is not None:
            try:
                filepath = str(
                    getattr(image, "filepath", "")
                    or getattr(image, "name", "")
                )
            except Exception:
                filepath = ""
        if not filepath:
            filepath = str(node.get("id") or "")
        resource_id = str(
            params.get("resource")
            or (isinstance(image_dict, Mapping) and image_dict.get("resourceBaseId"))
            or f"image::{filepath}"
        )
        resources[resource_id] = {
            "id": resource_id,
            "path": filepath,
            "uri": filepath,
            "exportFileName": filepath.rsplit("/", 1)[-1],
            "blenderImageName": filepath.rsplit("/", 1)[-1],
            "name": filepath.rsplit("/", 1)[-1],
        }
        # Make sure the node carries a ``resource`` reference so the
        # legacy extractor can resolve ``trace.resource``.
        params.setdefault("resource", resource_id)
        if not isinstance(params.get("image"), Mapping):
            params["image"] = filepath
        node["params"] = params

    if not entry:
        return resources, entry, dict(initial_slots), metadata

    # 3. Run the legacy extractor and merge its slots with the
    #    initial closure-derived slots.  Legacy slots win on a per-key
    #    basis when their confidence is greater than the closure
    #    slot's confidence; otherwise the closure slot is kept.
    #
    #    The legacy helper expects socket defaults under
    #    ``defaultValue``; the Miku 1.0 snapshot uses ``default``.
    #    Mirror the legacy field name for the duration of the call.
    graph_nodes: list[dict[str, Any]] = []
    for node in nodes:
        cloned = dict(node)
        cloned_inputs: list[dict[str, Any]] = []
        for sock in node.get("inputs") or []:
            if not isinstance(sock, Mapping):
                cloned_inputs.append(sock)
                continue
            cloned_sock = dict(sock)
            if "defaultValue" not in cloned_sock and "default" in cloned_sock:
                cloned_sock["defaultValue"] = cloned_sock["default"]
            cloned_inputs.append(cloned_sock)
        cloned["inputs"] = cloned_inputs
        graph_nodes.append(cloned)
    graph: dict[str, Any] = {
        "material": (nodes and {"name": ""}) or {"name": ""},
        "nodes": graph_nodes,
        "edges": edges,
        "resources": resources,
        "entry": entry,
    }
    try:
        result = _extract_legacy_standard_pbr_semantic(graph)
    except Exception as exc:
        metadata["diagnostics"].append(
            {
                "severity": "error",
                "code": "MIKU_STANDARD_PBR_SEMANTIC_EXTRACTION_FAILED",
                "translationQuality": "Unsupported",
                "message": (
                    "Standard PBR semantic extraction failed; preserving the "
                    f"closure-derived slots. {type(exc).__name__}: {exc}"
                ),
            }
        )
        return resources, entry, dict(initial_slots), metadata

    legacy_slots = result.get("slots") or {}
    merged: dict[str, dict[str, Any]] = dict(initial_slots)
    result_source = result.get("source")
    metadata = {
        "workflow": result.get("workflow") or "Metallic",
        "source": dict(result_source) if isinstance(result_source, Mapping) else {},
        "packedTextures": dict(result.get("packedTextures") or {}),
        "diagnostics": list(result.get("diagnostics") or []),
    }
    for semantic, slot in legacy_slots.items():
        existing = merged.get(semantic)
        existing_confidence = float((existing or {}).get("confidence", 0.0))
        new_confidence = float(slot.get("confidence", 0.0))
        if existing is None or new_confidence >= existing_confidence:
            if existing is None:
                merged[semantic] = dict(slot)
                if not isinstance(slot.get("source"), Mapping):
                    provenance = slot.get("source")
                    merged[semantic].pop("source", None)
                    if provenance:
                        merged[semantic]["sourceProvenance"] = provenance
                continue

            # The legacy extractor records provenance as strings such as
            # ``socket`` and ``loose_name``.  The compiler, however, needs the
            # snapshot endpoint mapping emitted by the closure flattener.  A
            # string must therefore never replace a real ``{node, socket}``
            # mapping, otherwise the texture becomes a constant/default in IR
            # and Unity receives no material texture to bind.
            combined = dict(existing)
            combined.update(
                key_value
                for key_value in slot.items()
                if key_value[0] != "source" and key_value[1] is not None
            )
            existing_source = existing.get("source")
            candidate_source = slot.get("source")
            if isinstance(existing_source, Mapping):
                combined["source"] = dict(existing_source)
                if candidate_source and not isinstance(candidate_source, Mapping):
                    combined["sourceProvenance"] = candidate_source
            elif isinstance(candidate_source, Mapping):
                combined["source"] = dict(candidate_source)
            else:
                combined.pop("source", None)
                if candidate_source:
                    combined["sourceProvenance"] = candidate_source
            merged[semantic] = combined
    return resources, entry, merged, metadata


def _principled_defaults(material: Any) -> dict[str, dict[str, Any]]:
    tree = getattr(material, "node_tree", None)
    nodes = list(getattr(tree, "nodes", []) or [])
    principled = next(
        (node for node in nodes if getattr(node, "bl_idname", "") == "ShaderNodeBsdfPrincipled"),
        None,
    )
    if principled is None:
        return {}
    mapping = {
        "BaseColor": ("Base Color", [0.8, 0.8, 0.8, 1.0]),
        "Metalness": ("Metallic", 0.0),
        "Roughness": ("Roughness", 0.5),
        "Normal": ("Normal", [0.0, 0.0, 1.0]),
        "Emission": ("Emission Color", [0.0, 0.0, 0.0, 1.0]),
        "Alpha": ("Alpha", 1.0),
    }
    slots: dict[str, dict[str, Any]] = {}
    for semantic, (socket_name, fallback) in mapping.items():
        socket = next(
            (
                item
                for item in getattr(principled, "inputs", []) or []
                if getattr(item, "name", "") == socket_name
            ),
            None,
        )
        if socket is None and semantic == "Emission":
            socket = next(
                (
                    item
                    for item in getattr(principled, "inputs", []) or []
                    if getattr(item, "name", "") == "Emission"
                ),
                None,
            )
        if socket is None:
            slots[semantic] = {"default": fallback}
            continue
        links = list(getattr(socket, "links", []) or [])
        if bool(getattr(socket, "is_linked", False)) and len(links) == 1:
            link = links[0]
            from_node = getattr(link, "from_node", None)
            from_socket = getattr(link, "from_socket", None)
            slots[semantic] = {
                "default": None,
                "source": {
                    "node": _node_stable_id(from_node),
                    "socket": str(
                        getattr(from_socket, "identifier", "")
                        or getattr(from_socket, "name", "")
                    ),
                },
            }
            continue
        value = _socket_value(socket)
        slots[semantic] = {"default": fallback if value is None else value}
    return slots


def _apply_channel_values(
    ir: Mapping[str, Any],
    resources: list[Mapping[str, Any]],
    *,
    validate_baked_resources: bool = True,
    authoritative_bake: bool = False,
) -> dict[str, Any]:
    by_semantic = {str(item.get("semantic")): item for item in resources}
    surface_plan = (
        ir.get("surfaceModelPlan")
        if isinstance(ir.get("surfaceModelPlan"), Mapping)
        else {}
    )
    surface_kind = str(surface_plan.get("kind") or "")
    mesh_bake_semantics = {
        str(item.get("semantic") or "")
        for item in surface_plan.get("channelPlans", []) or []
        if isinstance(item, Mapping)
        and str(item.get("route") or "") == "MeshBake"
    }
    closure_composite_defaults = (
        {
            "BaseColor": [0.8, 0.8, 0.8, 1.0],
            "Metalness": 0.0,
            "Roughness": 0.5,
            "Normal": [0.0, 0.0, 1.0],
            "Emission": [0.0, 0.0, 0.0, 1.0],
            "Alpha": 1.0,
        }
        if surface_kind in {"CustomMultiLobe", "RefractiveGlass"}
        else {}
    )
    neutralized_composite_channels: list[str] = []
    resource_ids = {
        str(item.get("id") or "")
        for item in resources
        if str(item.get("id") or "")
    }
    for expression in ir.get("expressions", []) or []:
        if (
            isinstance(expression, Mapping)
            and str(expression.get("op") or "")
            in {"Texture.SampleBaked2D", "Texture.SampleImage2D"}
        ):
            params = (
                expression.get("params")
                if isinstance(expression.get("params"), Mapping)
                else {}
            )
            resource_id = str(params.get("resourceId") or "")
            is_baked = (
                str(expression.get("op") or "")
                == "Texture.SampleBaked2D"
            )
            if (
                (not resource_id or resource_id not in resource_ids)
                and (validate_baked_resources or not is_baked)
            ):
                diagnostic_code = (
                    "MIKU_STATIC_EXPRESSION_ISLAND_BAKE_FAILED"
                    if str(expression.get("op") or "")
                    == "Texture.SampleBaked2D"
                    else "MIKU_STATIC_IMAGE_RESOURCE_MISSING"
                )
                source = (
                    expression.get("source")
                    if isinstance(expression.get("source"), Mapping)
                    else {}
                )
                raise RuntimeError(
                    diagnostic_code + ":"
                    f"{source.get('nodeId') or ''}:"
                    f"{source.get('socketId') or ''}:"
                    f"missing resource {resource_id or '<empty>'}"
                )
    channels = []
    for original in ir.get("channels", []) or []:
        channel = dict(original)
        semantic = str(channel.get("semantic") or "")
        resource = by_semantic.get(semantic)
        if resource is not None and (
            authoritative_bake or semantic in mesh_bake_semantics
        ):
            channel["value"] = {"kind": "TextureResource", "resourceId": resource["id"]}
            channel.pop("requiresBake", None)
        elif isinstance(channel.get("value"), Mapping):
            pass
        elif resource is not None:
            channel["value"] = {"kind": "TextureResource", "resourceId": resource["id"]}
            channel.pop("requiresBake", None)
        elif channel.get("default") is not None:
            channel["value"] = {"kind": "Constant", "value": channel["default"]}
            channel.pop("requiresBake", None)
        elif semantic in closure_composite_defaults:
            channel["value"] = {
                "kind": "Constant",
                "value": closure_composite_defaults[semantic],
            }
            channel.pop("requiresBake", None)
            neutralized_composite_channels.append(semantic)
        elif channel.get("required"):
            raise RuntimeError(f"MIKU_REQUIRED_CHANNEL_UNRESOLVED:{semantic}")
        channels.append(channel)
    payload = {
        key: value
        for key, value in ir.items()
        if key not in {"documentKind", "schemaVersion", "toolVersion", "id", "canonicalHash"}
    }
    payload["channels"] = channels
    payload["resources"] = [dict(item) for item in resources]
    if neutralized_composite_channels:
        payload["diagnostics"] = [
            *list(payload.get("diagnostics") or []),
            {
                "severity": "info",
                "code": "MIKU_CLOSURE_COMPOSITE_CHANNEL_NEUTRAL",
                "translationQuality": "Exact",
                "channels": sorted(
                    set(neutralized_composite_channels)
                ),
                "message": (
                    "Unused top-level Standard PBR channels were assigned "
                    "neutral contract values; weighted closure parameters "
                    "remain authoritative."
                ),
            },
        ]
    return make_document(
        str(ir["documentKind"]),
        payload,
        document_id=str(ir["id"]),
    )


def _apply_full_pbr_surface_model(
    ir: Mapping[str, Any],
) -> dict[str, Any]:
    """Project a completed Full PBR bake onto a supported surface contract."""

    return _apply_baked_pbr_surface_model(
        ir,
        all_channels_baked=True,
    )


def _apply_source_mesh_pbr_surface_model(
    ir: Mapping[str, Any],
) -> dict[str, Any]:
    """Lower an explicit source-mesh PBR projection to a standard surface."""

    return _apply_baked_pbr_surface_model(
        ir,
        all_channels_baked=False,
    )


def _apply_baked_pbr_surface_model(
    ir: Mapping[str, Any],
    *,
    all_channels_baked: bool,
) -> dict[str, Any]:
    """Select a supported PBR surface while preserving bake provenance."""

    payload = {
        key: value
        for key, value in ir.items()
        if key not in {
            "documentKind",
            "schemaVersion",
            "toolVersion",
            "id",
            "canonicalHash",
        }
    }
    original_plan = (
        dict(ir.get("surfaceModelPlan") or {})
        if isinstance(ir.get("surfaceModelPlan"), Mapping)
        else {}
    )
    render_state = (
        dict(original_plan.get("renderStatePlan") or {})
        if isinstance(original_plan.get("renderStatePlan"), Mapping)
        else {}
    )
    if bool(render_state.get("alphaClip")):
        surface_kind = "CutoutPBR"
    elif str(render_state.get("surfaceType") or "").lower() == "transparent":
        surface_kind = "TransparentLit"
    else:
        surface_kind = "OpaquePBR"
    channel_plans = (
        [
                {
                    "semantic": str(channel.get("semantic") or ""),
                    "valueType": str(channel.get("valueType") or ""),
                    "stage": str(channel.get("stage") or "Fragment"),
                    "route": "MeshBake",
                }
                for channel in payload.get("channels", []) or []
                if isinstance(channel, Mapping)
        ]
        if all_channels_baked
        else [
            dict(item)
            for item in original_plan.get("channelPlans", []) or []
            if isinstance(item, Mapping)
        ]
    )
    original_plan.update(
        {
            "kind": surface_kind,
            "fidelity": "Baked",
            "channelPlans": channel_plans,
        }
    )
    approximations = [
        dict(item)
        for item in original_plan.get("approximations", []) or []
        if isinstance(item, Mapping)
    ]
    if not any(
        str(item.get("kind") or "")
        == "SourceMeshFidelityPbrProjection"
        for item in approximations
    ):
        approximations.append(
            {
                "kind": "SourceMeshFidelityPbrProjection",
                "algorithmVersion": "miku-source-mesh-pbr-1",
                "errorBound": (
                    "Lighting-independent Blender PBR channels are bound to the "
                    "source mesh topology and UV layout."
                ),
            }
        )
    original_plan["approximations"] = approximations
    original_plan["diagnostics"] = [
        dict(item)
        for item in original_plan.get("diagnostics", []) or []
        if isinstance(item, Mapping)
        and str(item.get("code") or "")
        != "MIKU_CUSTOM_LIGHTING_SSAO_UNAVAILABLE"
    ]
    payload["surfaceModelPlan"] = original_plan
    payload["diagnostics"] = [
        (
            {
                **dict(item),
                "severity": "info",
                "translationQuality": "Baked",
                "resolvedBy": "SourceMeshFidelity",
            }
            if isinstance(item, Mapping)
            and str(item.get("severity") or "").lower() == "error"
            and str(item.get("code") or "")
            in SOURCE_MESH_RESOLVABLE_DIAGNOSTIC_CODES
            else dict(item)
        )
        for item in payload.get("diagnostics", []) or []
        if isinstance(item, Mapping)
    ]
    return make_document(
        str(ir["documentKind"]),
        payload,
        document_id=str(ir["id"]),
    )


def _has_source_mesh_pbr_projection(ir: Mapping[str, Any]) -> bool:
    plan = (
        ir.get("surfaceModelPlan")
        if isinstance(ir.get("surfaceModelPlan"), Mapping)
        else {}
    )
    has_projection_marker = any(
        isinstance(item, Mapping)
        and str(item.get("kind") or "")
        == "SourceMeshFidelityPbrProjection"
        for item in plan.get("approximations", []) or []
    )
    routes = {
        str(item.get("semantic") or ""): str(item.get("route") or "")
        for item in plan.get("channelPlans", []) or []
        if isinstance(item, Mapping)
    }
    has_mesh_baked_channel = any(
        route == "MeshBake" for route in routes.values()
    )
    if has_projection_marker:
        return has_mesh_baked_channel
    render_state = (
        plan.get("renderStatePlan")
        if isinstance(plan.get("renderStatePlan"), Mapping)
        else {}
    )
    return (
        str(plan.get("kind") or "") == "CustomMultiLobe"
        and str(render_state.get("surfaceType") or "Opaque").lower()
        == "opaque"
        and all(
            routes.get(semantic) == "MeshBake"
            for semantic in (
                "BaseColor",
                "Metalness",
                "Roughness",
                "Normal",
            )
        )
    )


def _rebuild_document(document: Mapping[str, Any]) -> dict[str, Any]:
    payload = {
        key: value
        for key, value in document.items()
        if key not in {"documentKind", "schemaVersion", "toolVersion", "id", "canonicalHash"}
    }
    return make_document(str(document["documentKind"]), payload, document_id=str(document["id"]))


def _apply_bake_resolution_to_plan(
    plan: Mapping[str, Any],
    bake_resolution: int,
) -> dict[str, Any]:
    resolution = normalize_bake_resolution(bake_resolution)
    jobs = list(plan.get("bakeJobs") or [])
    if not jobs:
        return dict(plan)
    updated = dict(plan)
    updated["bakeJobs"] = [
        {**dict(job), "resolution": resolution}
        if isinstance(job, Mapping)
        else job
        for job in jobs
    ]
    return _rebuild_document(updated)


def _has_unresolved_required_channels(ir: Mapping[str, Any]) -> bool:
    return any(
        bool(channel.get("required"))
        and channel.get("default") is None
        and not isinstance(channel.get("value"), Mapping)
        for channel in ir.get("channels", []) or []
    )


def _force_appearance_snapshot_plan(
    plan: Mapping[str, Any],
    ir: Mapping[str, Any],
) -> dict[str, Any]:
    payload = {
        key: value
        for key, value in plan.items()
        if key not in {"documentKind", "schemaVersion", "toolVersion", "id", "canonicalHash"}
    }
    region_id = str(
        next(
            (
                channel.get("regionId")
                for channel in ir.get("channels", []) or []
                if channel.get("regionId")
            ),
            ir.get("id"),
        )
    )
    payload["regions"] = [
        {
            **dict(region),
            "route": "AppearanceSnapshot",
            "fidelity": "Approximate",
            "backend": "BlenderCyclesAppearanceSnapshotExecutor",
        }
        for region in plan.get("regions", []) or []
    ]
    payload["bakeJobs"] = [
        {
            "jobId": "appearance-" + str(ir.get("id")),
            "regionId": region_id,
            "route": "AppearanceSnapshot",
            "resolution": 1024,
            "supersampling": 2,
            "padding": 16,
            "samples": 16,
            "randomSeed": 0,
            "sourceRegionId": region_id,
        }
    ]
    payload["diagnostics"] = [
        *list(plan.get("diagnostics") or []),
        {
            "severity": "warning",
            "code": "MIKU_APPEARANCE_SNAPSHOT_EXPLICIT",
            "translationQuality": "Approximate",
            "message": (
                "The caller explicitly authorized a view-dependent appearance "
                "snapshot because required semantic channels had no static value."
            ),
        },
    ]
    return make_document(
        "miku-conversion-plan-1.0",
        payload,
        document_id=str(plan["id"]),
    )


def _assert_blender_52() -> None:
    if bpy is None:
        return
    actual = tuple(getattr(bpy.app, "version", ()))
    if actual != (5, 2, 0):
        raise RuntimeError(
            "MIKU_BLENDER_VERSION_MISMATCH:"
            f"expected=(5, 2, 0):got={actual or '<unknown>'}"
        )


def create_miku_time_node(material: Any) -> Any:
    """Create the versioned Miku Time group in a material root node tree."""

    _assert_blender_52()
    if bpy is None:
        raise RuntimeError("MIKU_BLENDER_REQUIRED")
    tree = getattr(material, "node_tree", None)
    if tree is None:
        raise RuntimeError("MIKU_MATERIAL_NODE_TREE_REQUIRED")
    contract = _time_contract()
    group_name = "Miku Time v1"
    group = bpy.data.node_groups.new(group_name, "ShaderNodeTree")
    group["miku.semantic"] = _MIKU_TIME_SEMANTIC
    group["miku.semanticVersion"] = _MIKU_TIME_SEMANTIC_VERSION
    group["miku.contract"] = _MIKU_TIME_CONTRACT
    group["miku.sourceFps"] = contract["sourceFps"]
    group["miku.frameStart"] = contract["frameStart"]
    for name in ("Seconds", "Frame", "Sine", "Cosine"):
        group.interface.new_socket(
            name=name,
            in_out="OUTPUT",
            socket_type="NodeSocketFloat",
        )
    output = group.nodes.new("NodeGroupOutput")
    output.name = "Miku Time Outputs"
    output.location = (320.0, 0.0)
    fps = float(contract["sourceFps"])
    frame_start = int(contract["frameStart"])
    expressions = {
        "Seconds": f"(frame - {frame_start}) / {fps:.17g}",
        "Frame": "frame",
        "Sine": f"sin((frame - {frame_start}) / {fps:.17g})",
        "Cosine": f"cos((frame - {frame_start}) / {fps:.17g})",
    }
    for index, (name, expression) in enumerate(expressions.items()):
        value = group.nodes.new("ShaderNodeValue")
        value.name = f"Miku {name}"
        value.label = name
        value.location = (-180.0, 180.0 - index * 120.0)
        curve = value.outputs[0].driver_add("default_value")
        curve.driver.type = "SCRIPTED"
        curve.driver.expression = expression
        group.links.new(value.outputs[0], output.inputs[name])
    node = tree.nodes.new("ShaderNodeGroup")
    node.node_tree = group
    node.name = group_name
    node.label = "Miku Time"
    return node


def register() -> None:
    global _REGISTERED_CLASSES
    if bpy is None:
        return
    _assert_blender_52()

    if _REGISTERED_CLASSES:
        return

    _register_translations()

    class MIKU_PG_scene_settings(bpy.types.PropertyGroup):
        output_root: bpy.props.StringProperty(
            name="Output Folder",
            subtype="DIR_PATH",
            default="//Generated/Miku",
        )
        # Hidden compatibility data for files saved by the first Miku 1.0 UI.
        source_id: bpy.props.StringProperty(
            name="Persistent Source ID",
            description="Stable project/source identifier written into every Miku bundle",
            default="",
        )
        default_workflow: bpy.props.EnumProperty(
            name="Default Workflow",
            items=WORKFLOW_ITEMS,
            default="standard_pbr",
        )
        mode: bpy.props.EnumProperty(
            name="Conversion Mode",
            items=(
                (
                    "Auto",
                    "Auto",
                    "Portable native conversion; fail before a mesh-bound bake",
                ),
                ("NativeOnly", "Native Only", "Reject regions that require baking"),
                (
                    "PreferNative",
                    "Portable Hybrid (Prefer Native)",
                    "Keep View/Camera/Time live and allow only reusable UV0 bakes; never export a source mesh",
                ),
                (
                    "ReusableBakeOnly",
                    "Reusable Bake Only",
                    "Allow mesh-independent reusable bakes only",
                ),
                (
                    "AllowMeshBake",
                    "Source Mesh Fidelity",
                    "Bake against and export the evaluated source mesh",
                ),
                (
                    "FullPBRBake",
                    "Full PBR Bake (Source Mesh)",
                    "Bake the complete source-mesh-bound PBR channel set; runtime View/Camera/Time inputs are rejected",
                ),
                (
                    "AppearanceSnapshot",
                    "Appearance Snapshot",
                    "Explicit fixed-view approximation",
                ),
            ),
            default="Auto",
        )
        fidelity_policy: bpy.props.EnumProperty(
            name="Fidelity Policy",
            items=(
                (
                    "AllowDeclaredApproximation",
                    "Allow Declared Approximation",
                    "Allow registered approximations with structured diagnostics",
                ),
                (
                    "Strict",
                    "Strict Fidelity",
                    "Reject every surface plan that requires an approximation",
                ),
            ),
            default="AllowDeclaredApproximation",
        )
        add_shader_energy_policy: bpy.props.EnumProperty(
            name="Add Shader Energy",
            items=(
                (
                    "PreserveBlender",
                    "Preserve Blender",
                    "Copy parent closure weight to both Add Shader branches",
                ),
                (
                    "EnergyConservingApproximation",
                    "Energy-Conserving Approximation",
                    "Explicitly normalize additive closure energy",
                ),
                (
                    "ClampForRealtimeSafety",
                    "Clamp for Realtime Safety",
                    "Explicitly clamp additive energy and emit a high warning",
                ),
            ),
            default="PreserveBlender",
        )
        show_advanced: bpy.props.BoolProperty(
            name="Advanced",
            description="Show advanced conversion settings",
            default=False,
        )
        bake_texture_quality: bpy.props.EnumProperty(
            name="Bake Texture Quality",
            description=(
                "Resolution for generated 2D bake textures; this setting is "
                "used only when conversion schedules a bake"
            ),
            items=(
                (
                    "LOW_512",
                    "Low (512 × 512)",
                    "Faster baking with lower texture detail",
                ),
                (
                    "STANDARD_1024",
                    "Standard (1024 × 1024)",
                    "Balanced default bake texture resolution",
                ),
                (
                    "HIGH_2048",
                    "High (2048 × 2048)",
                    "Higher texture detail with increased bake time and memory use",
                ),
                (
                    "ULTRA_4096",
                    "Ultra (4096 × 4096)",
                    "Maximum texture detail with significantly increased bake time and memory use",
                ),
            ),
            default="STANDARD_1024",
        )

    class MIKU_OT_export_materials(bpy.types.Operator):
        bl_idname = "miku.export_materials"
        bl_label = "Export Current Material"
        bl_description = "Export only the material in the active material slot"
        bl_options = {"REGISTER"}

        @classmethod
        def poll(cls, context):  # noqa: N802
            material, diagnostic = _active_material_slot_state(context)
            if material is None and hasattr(cls, "poll_message_set"):
                cls.poll_message_set(_translate_iface(str(diagnostic)))
            return material is not None

        def execute(self, context):  # noqa: N802
            settings = context.scene.miku_settings
            try:
                result = export_current_material(
                    context,
                    bpy.path.abspath(settings.output_root),
                    mode=settings.mode,
                    allow_appearance_approximation=(settings.mode == "AppearanceSnapshot"),
                    fidelity_policy=settings.fidelity_policy,
                    add_shader_energy_policy=settings.add_shader_energy_policy,
                    bake_resolution=bake_resolution_for_quality(
                        settings.bake_texture_quality
                    ),
                )
            except Exception as exc:
                self.report({"ERROR"}, _translate_diagnostic(str(exc)))
                return {"CANCELLED"}
            if result.get("sourceIdentityTemporary"):
                self.report(
                    {"WARNING"},
                    _translate_iface(
                        "The source identity is session-only. Save the blend and ensure it is writable."
                    ),
                )
            for warning in result.get("identityWarnings", ()):
                self.report({"WARNING"}, str(warning))
            self.report(
                {"INFO"},
                _translate_iface("Exported current material: {material}").format(
                    material=result["materialKey"]
                ),
            )
            return {"FINISHED"}

    class MIKU_OT_add_time_node(bpy.types.Operator):
        bl_idname = "miku.add_time_node"
        bl_label = "Add Miku Time Node"
        bl_description = (
            "Add a versioned Time node whose Seconds, Frame, Sine and Cosine "
            "outputs remain dynamic in Unity Shader Graph"
        )
        bl_options = {"INTERNAL", "UNDO"}

        @classmethod
        def poll(cls, context):  # noqa: N802
            material, diagnostic = _active_material_slot_state(context)
            if material is None and hasattr(cls, "poll_message_set"):
                cls.poll_message_set(_translate_iface(str(diagnostic)))
            return material is not None

        def execute(self, context):  # noqa: N802
            material, diagnostic = _active_material_slot_state(context)
            if material is None:
                self.report({"ERROR"}, str(diagnostic))
                return {"CANCELLED"}
            try:
                create_miku_time_node(material)
            except Exception as exc:
                self.report({"ERROR"}, str(exc))
                return {"CANCELLED"}
            self.report({"INFO"}, _translate_iface("Added Miku Time v1"))
            return {"FINISHED"}

    class MIKU_OT_fork_source_identity(bpy.types.Operator):
        bl_idname = "miku.fork_source_identity"
        bl_label = "Fork Source Identity"
        bl_description = (
            "Declare this blend as an independent source; future Unity assets "
            "will receive new stable GUIDs"
        )
        bl_options = {"INTERNAL", "UNDO"}

        def invoke(self, context, event):  # noqa: N802
            return context.window_manager.invoke_confirm(self, event)

        def execute(self, context):  # noqa: N802
            result = fork_source_identity(
                bpy.data,
                current_scene=getattr(context, "scene", None),
            )
            for warning in result.get("identityWarnings", ()):
                self.report({"WARNING"}, str(warning))
            self.report(
                {"INFO"},
                _translate_iface(
                    "Forked Miku source identity for {count} material(s). Save the blend to persist it."
                ).format(count=result["materialCount"]),
            )
            return {"FINISHED"}

    class MIKU_OT_migrate_legacy_identities(bpy.types.Operator):
        bl_idname = "miku.migrate_legacy_identities"
        bl_label = "Migrate Legacy Identities"
        bl_description = (
            "Copy matching IDs from the legacy .migr-identities.json into "
            "this blend without modifying the registry"
        )
        bl_options = {"INTERNAL", "UNDO"}

        def execute(self, context):  # noqa: N802
            settings = context.scene.miku_settings
            result = migrate_legacy_identities(
                bpy.data,
                bpy.path.abspath(settings.output_root),
                current_scene=getattr(context, "scene", None),
            )
            for warning in result.get("identityWarnings", ()):
                self.report({"WARNING"}, str(warning))
            if not result.get("persistentSourceId"):
                return {"CANCELLED"}
            self.report(
                {"INFO"},
                _translate_iface(
                    "Migrated legacy source identity and {count} material identity value(s)."
                ).format(count=result["materialCount"]),
            )
            return {"FINISHED"}

    class MIKU_PT_export_panel(bpy.types.Panel):
        bl_label = "Miku"
        bl_space_type = "NODE_EDITOR"
        bl_region_type = "UI"
        bl_category = "Miku"

        @classmethod
        def poll(cls, context):  # noqa: N802
            return getattr(context.space_data, "tree_type", "") == "ShaderNodeTree"

        def draw(self, context):
            layout = self.layout
            settings = context.scene.miku_settings
            layout.prop(settings, "output_root")
            material, diagnostic = _active_material_slot_state(context)
            if material is not None:
                layout.label(
                    text=_translate_iface("Material: {material}").format(
                        material=getattr(material, "name", "Material")
                    ),
                    icon="MATERIAL",
                )
                layout.label(
                    text=_translate_iface("Standard PBR"),
                    icon="SHADING_RENDERED",
                )
                layout.prop(
                    material,
                    "miku_normal_convention",
                    text=_translate_iface("Normal Map"),
                )
                layout.prop(
                    material,
                    "miku_displacement_policy",
                    text=_translate_iface("Displacement"),
                )
            else:
                message = layout.box()
                message.label(
                    text=_translate_iface(str(diagnostic)),
                    icon="ERROR",
                    translate=False,
                )

            advanced = layout.box()
            header = advanced.row()
            header.prop(
                settings,
                "show_advanced",
                text="Advanced",
                icon=("DISCLOSURE_TRI_DOWN" if settings.show_advanced else "DISCLOSURE_TRI_RIGHT"),
                emboss=False,
            )
            if settings.show_advanced:
                advanced.prop(settings, "mode")
                advanced.prop(settings, "fidelity_policy")
                advanced.prop(settings, "add_shader_energy_policy")
                advanced.prop(settings, "bake_texture_quality")
                advanced.label(
                    text="Used only when conversion schedules a bake.",
                    icon="INFO",
                )
                advanced.operator(
                    MIKU_OT_fork_source_identity.bl_idname,
                    icon="DUPLICATE",
                )

            action = layout.row()
            action.enabled = material is not None
            action.operator(MIKU_OT_export_materials.bl_idname)

    _REGISTERED_CLASSES = [
        MIKU_PG_scene_settings,
        MIKU_OT_export_materials,
        MIKU_OT_add_time_node,
        MIKU_OT_migrate_legacy_identities,
        MIKU_OT_fork_source_identity,
        MIKU_PT_export_panel,
    ]
    for cls in _REGISTERED_CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Scene.miku_settings = bpy.props.PointerProperty(type=MIKU_PG_scene_settings)
    bpy.types.Material.miku_workflow = bpy.props.EnumProperty(
        name="Miku Workflow",
        description="Legacy workflow retained only for Miku 1.0 migration",
        items=(("inherit", "Inherit Scene", "Use the scene default workflow"),) + WORKFLOW_ITEMS,
        default="inherit",
    )
    bpy.types.Material.miku_workflow_kind = bpy.props.EnumProperty(
        name="Workflow",
        description="Select the Miku generation workflow for this material",
        items=WORKFLOW_ITEMS,
        default="standard_pbr",
    )
    bpy.types.Material.miku_workflow_part = bpy.props.EnumProperty(
        name="Game Part",
        items=(
            ("Body", "Body", "Body or accessory shader"),
            ("Hair", "Hair", "Hair shader"),
            ("Face", "Face", "Face shader"),
            ("Eye", "Eye", "Eye shader"),
            ("Effect", "Effect", "Effect shader when supported"),
            ("Skin", "Skin", "Skin shader when supported"),
            ("Mouth", "Mouth", "Mouth or teeth shader when supported"),
            ("Overlay", "Overlay", "Eyelash, eye-shadow, or expression overlay"),
            ("HairShadow", "Hair Shadow", "Texture-driven hair shadow overlay"),
        ),
        default="Body",
    )
    bpy.types.Material.miku_normal_convention = bpy.props.EnumProperty(
        name="Normal Map Convention",
        description=(
            "Explicit tangent-space normal convention for this material; "
            "Miku never infers it from filenames"
        ),
        items=(
            (
                "TangentOpenGLPositiveY",
                "OpenGL (+Y)",
                "Green channel stores positive tangent-space Y",
            ),
            (
                "TangentDirectXNegativeY",
                "DirectX (-Y)",
                "Green channel stores negative tangent-space Y",
            ),
        ),
        default="TangentOpenGLPositiveY",
    )
    bpy.types.Material.miku_displacement_policy = bpy.props.EnumProperty(
        name="Miku Displacement Policy",
        description=(
            "Choose whether Miku follows Blender displacement, promotes a "
            "safe Bump height to vertex displacement, or exports only Height"
        ),
        items=(
            (
                "FOLLOW_BLENDER",
                "Follow Blender",
                "Preserve Blender's displacement method",
            ),
            (
                "ALWAYS_VERTEX",
                "Always Vertex",
                "Export Height and connect safe finite sources to Vertex Position",
            ),
            (
                "MAP_ONLY",
                "Map Only",
                "Export Height and controls without connecting Vertex Position",
            ),
        ),
        default="FOLLOW_BLENDER",
    )
    bpy.types.ShaderNodeTexImage.miku_texture_role = bpy.props.EnumProperty(
        name="Miku Texture Role",
        description=(
            "Explicit fixed-workflow material texture role; Auto uses only "
            "strict node/image/file aliases"
        ),
        items=(
            ("AUTO", "Auto", "Use strict controlled aliases"),
            *tuple((role, role, role) for role in FIXED_TEXTURE_ROLES),
        ),
        default="AUTO",
    )


def unregister() -> None:
    global _REGISTERED_CLASSES
    if bpy is None:
        return
    for owner, attribute in (
        (bpy.types.ShaderNodeTexImage, "miku_texture_role"),
        (bpy.types.Material, "miku_displacement_policy"),
        (bpy.types.Material, "miku_normal_convention"),
        (bpy.types.Material, "miku_workflow_part"),
        (bpy.types.Material, "miku_workflow_kind"),
        (bpy.types.Material, "miku_workflow"),
        (bpy.types.Scene, "miku_settings"),
    ):
        if hasattr(owner, attribute):
            delattr(owner, attribute)
    for cls in reversed(_REGISTERED_CLASSES):
        try:
            bpy.utils.unregister_class(cls)
        except RuntimeError:
            pass
    _REGISTERED_CLASSES = []
    _unregister_translations()
