# SPDX-FileCopyrightText: 2026 Miku Project Authors
# SPDX-License-Identifier: GPL-2.0-or-later
"""Certified Blender-side executor for the Miku bake artifact protocol."""

from __future__ import annotations

import json
from pathlib import Path
from types import SimpleNamespace
from typing import Any

try:
    import bpy
except ImportError:  # Repository-side protocol tests run outside Blender.
    bpy = None

try:
    from .automatic_bake import (
        CHANNELS,
        bake_expression_islands,
        bake_material,
    )
    from .source_mesh_glb import export_source_mesh_glb
except ImportError:
    from automatic_bake import CHANNELS, bake_expression_islands, bake_material
    from source_mesh_glb import export_source_mesh_glb
try:
    from ..miku.bake_protocol import (
        make_bake_result,
        validate_bake_runtime_binding,
        validate_bake_request,
    )
    from ..miku.bundle import make_file_reference
    from ..miku.contracts import canonical_hash
    from ..miku_blender.versioning import (
        blender_build_hash,
        blender_version_string,
        require_blender_capabilities,
    )
except ImportError:
    # Repository test layout. The release ZIP always uses the private
    # extension-local protocol package above.
    from miku.bake_protocol import (
        make_bake_result,
        validate_bake_runtime_binding,
        validate_bake_request,
    )
    from miku.bundle import make_file_reference
    from miku.contracts import canonical_hash
    from miku_blender.versioning import (
        blender_build_hash,
        blender_version_string,
        require_blender_capabilities,
    )

_REGISTERED_CLASSES: list[type] = []


def execute_request(request_path: str, output_root: str) -> str:
    """Consume a request artifact and write the matching result artifact."""

    import bpy

    target = Path(output_root).resolve()
    request_file = Path(request_path).resolve()
    if request_file.parent != target:
        raise RuntimeError("MIKU_BAKE_REQUEST_PATH_INVALID")
    request = validate_bake_request(
        json.loads(request_file.read_text(encoding="utf-8"))
    )
    compatibility = _assert_request_blender(bpy, request)
    material_name = str(request.get("sourceMaterialName") or "")
    material = getattr(bpy.data, "materials", {}).get(material_name)
    if material is None:
        raise RuntimeError(f"MIKU_BAKE_SOURCE_MATERIAL_MISSING:{material_name}")
    settings_value = request["settings"]
    settings = SimpleNamespace(
        bake_resolution=str(settings_value["resolution"]),
        bake_samples=int(settings_value["samples"]),
        bake_margin=int(settings_value["margin"]),
        auto_generate_uv=False,
    )
    jobs = list(request.get("jobs") or [])
    channel_jobs = [
        item
        for item in jobs
        if isinstance(item, dict) and str(item.get("scope") or "") == "Channels"
    ]
    island_jobs = [
        item
        for item in jobs
        if isinstance(item, dict)
        and str(item.get("scope") or "") == "ExpressionIsland"
    ]
    reusable_island_jobs = [
        item
        for item in island_jobs
        if str(item.get("route") or "") == "ReusableBake"
        and item.get("meshBindingRequired") is False
        and str(item.get("coordinateDomain") or "") in {"Uniform", "UV0"}
    ]
    region_jobs = [
        item for item in jobs if item not in channel_jobs and item not in island_jobs
    ]
    material_jobs = [*channel_jobs, *region_jobs]
    channel_scoped = bool(channel_jobs) and not region_jobs
    channel_specs = None
    if material_jobs:
        requested_semantics: set[str] = set()
        explicit_semantics = False
        for job in material_jobs:
            semantics = job.get("semantics")
            if semantics is None:
                continue
            if not isinstance(semantics, list):
                raise RuntimeError("MIKU_BAKE_CHANNEL_SEMANTICS_INVALID")
            explicit_semantics = True
            requested_semantics.update(str(item) for item in semantics if str(item))
        if not explicit_semantics:
            requested_semantics = {item[0] for item in CHANNELS}
        known_semantics = {item[0] for item in CHANNELS}
        unknown_semantics = sorted(requested_semantics - known_semantics)
        if unknown_semantics:
            raise RuntimeError(
                "MIKU_BAKE_CHANNEL_SEMANTIC_UNSUPPORTED:"
                + ",".join(unknown_semantics)
            )
        channel_specs = tuple(
            item for item in CHANNELS if item[0] in requested_semantics
        )
        if not channel_specs:
            raise RuntimeError("MIKU_BAKE_CHANNEL_SEMANTICS_MISSING")
    if region_jobs and (channel_jobs or island_jobs):
        raise RuntimeError("MIKU_BAKE_JOB_SCOPE_CONFLICT")
    if reusable_island_jobs and (
        material_jobs or len(reusable_island_jobs) != len(island_jobs)
    ):
        raise RuntimeError("MIKU_BAKE_JOB_SCOPE_CONFLICT")
    cycles = getattr(getattr(bpy.context, "scene", None), "cycles", None)
    original_device = getattr(cycles, "device", None)
    original_seed = getattr(cycles, "seed", None)
    result = None
    island_result = None
    approximation_used = False
    try:
        if cycles is not None:
            cycles.device = "CPU"
            cycles.seed = 0
        if channel_jobs or region_jobs:
            result = bake_material(
                bpy.context,
                list(getattr(bpy.data, "objects", []) or []),
                material,
                dict(request["sourceSnapshot"]),
                str(target / f"{request['persistentMaterialId']}.miku-bake-source"),
                settings,
                allow_appearance_approximation=False,
                channel_specs=channel_specs,
                channel_scoped=channel_scoped,
            )
            if (
                result.get("status") == "skipped"
                and bool(request.get("allowAppearanceApproximation"))
            ):
                result = bake_material(
                    bpy.context,
                    list(getattr(bpy.data, "objects", []) or []),
                    material,
                    dict(request["sourceSnapshot"]),
                    str(
                        target
                        / f"{request['persistentMaterialId']}.miku-bake-source"
                    ),
                    settings,
                    allow_appearance_approximation=True,
                    channel_specs=channel_specs,
                    channel_scoped=channel_scoped,
                    parity_strategy="AppearanceSnapshot",
                )
                approximation_used = True
        if island_jobs:
            island_result = bake_expression_islands(
                bpy.context,
                list(getattr(bpy.data, "objects", []) or []),
                material,
                dict(request["sourceSnapshot"]),
                island_jobs,
                str(
                    target
                    / f"{request['persistentMaterialId']}.miku-bake-source"
                ),
                settings,
            )
    finally:
        if cycles is not None and original_device is not None:
            cycles.device = original_device
        if cycles is not None and original_seed is not None:
            cycles.seed = original_seed

    result_path = target / f"{request['persistentMaterialId']}.miku-bake-result.json"
    failed_results = [
        item
        for item in (result, island_result)
        if item is not None
        and item.get("status") not in {"completed", "reused"}
    ]
    if failed_results:
        runtime_dependencies = sorted(
            {
                str(item)
                for failed in failed_results
                for item in failed.get("nonBakeableDependencies") or []
                if str(item) in {
                    "ViewDirection",
                    "Camera",
                    "LightPath",
                    "Time",
                }
            }
        )
        diagnostics = []
        for failed in failed_results:
            failure_items = failed.get("failures") or [
                {
                    "message": (
                        failed.get("skipReason")
                        or failed.get("message")
                        or failed.get("status")
                    )
                }
            ]
            for item in failure_items:
                diagnostic_code = str(item.get("code") or "")
                if not diagnostic_code:
                    diagnostic_code = (
                        "MIKU_RUNTIME_INPUT_UNSUPPORTED"
                        if runtime_dependencies
                        else "MIKU_BAKE_EXECUTION_FAILED"
                    )
                diagnostics.append(
                    {
                        "severity": "error",
                        "code": diagnostic_code,
                        "expressionId": item.get("expressionId"),
                        "sourceNodeId": item.get("sourceNodeId"),
                        "sourceSocketId": item.get("sourceSocketId"),
                        "message": (
                            str(item.get("message") or item)
                            + (
                                " Runtime dependencies: "
                                + ", ".join(runtime_dependencies)
                                if runtime_dependencies
                                else ""
                            )
                        ),
                    }
                )
        failure = make_bake_result(
            request,
            [],
            status="failed",
            diagnostics=diagnostics,
        )
        _write_json(result_path, failure)
        raise RuntimeError(
            str(diagnostics[0]["code"]) + ": "
            + "; ".join(item["message"] for item in diagnostics)
        )

    portable_result = bool(reusable_island_jobs) and not material_jobs
    mesh_items = list(
        (
            ((result or {}).get("dependencies") or {}).get("targetMeshes")
            or ((island_result or {}).get("dependencies") or {}).get(
                "targetMeshes"
            )
            or []
        )
    )
    mesh_binding = None
    resources = []
    if not portable_result:
        mesh_binding = {
            "kind": "MeshFingerprintSet",
            "sha256": canonical_hash(mesh_items),
            "meshes": mesh_items,
            "coordinateConvention": "BlenderObjectToUnityObject",
            "normalConvention": "TangentOpenGLPositiveY",
        }
        source_mesh_path = (
            target
            / "SourceMesh"
            / f"{request['persistentMaterialId']}.glb"
        )
        source_mesh = export_source_mesh_glb(
            bpy.context,
            list(getattr(bpy.data, "objects", []) or []),
            material,
            source_mesh_path,
            mesh_items,
        )
        source_mesh_reference = make_file_reference(
            target,
            source_mesh_path,
            media_type="model/gltf-binary",
        )
        source_mesh_reference.update(
            {
                "id": (
                    f"miku_{request['persistentMaterialId']}_source_mesh"
                ),
                "kind": "SourceMesh",
                "semantic": "SourceMesh",
                "meshBinding": mesh_binding,
                "rendererBindings": source_mesh["rendererBindings"],
                "meshCount": source_mesh["meshCount"],
                "vertexCount": source_mesh["vertexCount"],
                "indexCount": source_mesh["indexCount"],
                "hasUv0": source_mesh["hasUv0"],
            }
        )
        resources.append(source_mesh_reference)
    for semantic, channel in sorted(
        (((result or {}).get("channels") or {}).items())
    ):
        relative = str(channel.get("relativePath") or "")
        path = target / Path(relative)
        media_type = str(
            channel.get("mediaType")
            or ("image/x-exr" if path.suffix.lower() == ".exr" else "image/png")
        )
        reference = make_file_reference(target, path, media_type=media_type)
        reference.update(
            {
                "id": (
                    f"miku_{request['persistentMaterialId']}_{semantic.lower()}"
                ),
                "semantic": semantic,
                "channel": str(channel.get("channel") or "RGBA"),
                "colorSpace": str(channel.get("colorSpace") or "Linear"),
                "width": int(result.get("resolution") or 1024),
                "height": int(result.get("resolution") or 1024),
                "channelCount": int(
                    channel.get("channelCount")
                    or (4 if semantic in {"BaseColor", "Emission"} else 1)
                ),
                "componentBytes": int(
                    channel.get("componentBytes")
                    or (2 if media_type == "image/x-exr" else 1)
                ),
                **({"meshBinding": mesh_binding} if mesh_binding else {}),
            }
        )
        if semantic == "Normal":
            reference["normalConvention"] = "TangentOpenGLPositiveY"
        resources.append(reference)
    for expression_id, island in sorted(
        (((island_result or {}).get("islands") or {}).items())
    ):
        relative = str(island.get("relativePath") or "")
        path = target / Path(relative)
        media_type = str(
            island.get("mediaType")
            or (
                "image/x-exr"
                if path.suffix.lower() == ".exr"
                else "image/png"
            )
        )
        reference = make_file_reference(
            target, path, media_type=media_type
        )
        usage = str(island.get("usage") or "Color")
        reference.update(
            {
                "id": str(island.get("resourceId") or expression_id),
                "semantic": "ExpressionIsland",
                "bindingKey": str(
                    island.get("referenceName") or expression_id
                ),
                "expressionId": expression_id,
                "usage": usage,
                "channel": str(island.get("channel") or "RGB"),
                "colorSpace": str(
                    island.get("colorSpace") or "Linear"
                ),
                "width": int(
                    (island_result or {}).get("resolution") or 1024
                ),
                "height": int(
                    (island_result or {}).get("resolution") or 1024
                ),
                "channelCount": int(
                    island.get("channelCount")
                    or (1 if usage == "Scalar" else 3)
                ),
                "componentBytes": int(
                    island.get("componentBytes")
                    or (2 if media_type == "image/x-exr" else 1)
                ),
                "coordinateDomain": str(
                    island.get("coordinateDomain") or "MeshSurface"
                ),
                "meshBindingRequired": bool(
                    island.get("meshBindingRequired", True)
                ),
                **(
                    {"meshBinding": mesh_binding}
                    if bool(island.get("meshBindingRequired", True))
                    and mesh_binding
                    else {}
                ),
            }
        )
        if usage == "Normal":
            reference["normalConvention"] = "TangentOpenGLPositiveY"
        resources.append(reference)
    completion_diagnostics = [
        {
            "severity": "info",
            "code": "MIKU_BAKE_COMPLETED",
            "translationQuality": "Baked",
            "message": (
                f"Generated {len(resources)} "
                + (
                    "portable UV0 expression-island resources without source "
                    "mesh binding."
                    if portable_result
                    else "mesh-bound semantic channel and expression-island "
                    "resources."
                )
            ),
        }
    ]
    if compatibility.diagnostic:
        completion_diagnostics.append(
            {
                "severity": "warning",
                "code": "MIKU_BLENDER_VERSION_UNVALIDATED",
                "translationQuality": "RequiresProjectSetup",
                "message": compatibility.diagnostic,
            }
        )
    if approximation_used:
        completion_diagnostics.append(
            {
                "severity": "warning",
                "code": "MIKU_APPEARANCE_SNAPSHOT_APPROXIMATION",
                "translationQuality": "Approximate",
                "message": (
                    "View/camera-dependent closure behavior was flattened at "
                    "the locked bake scene and may differ at runtime."
                ),
            }
        )
    protocol_result = make_bake_result(
        request,
        resources,
        status="completed",
        mesh_binding=mesh_binding,
        diagnostics=completion_diagnostics,
    )
    _write_json(result_path, protocol_result)
    return str(result_path)


def _assert_request_blender(bpy: Any, request: dict[str, Any]):
    compatibility = require_blender_capabilities(bpy)
    actual_version = blender_version_string(bpy)
    actual_commit = blender_build_hash(bpy)
    validate_bake_runtime_binding(request, actual_version, actual_commit)
    return compatibility


def _write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(
        json.dumps(value, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    temporary.replace(path)


def register() -> None:
    """Register the artifact-protocol operator consumed by the MIT extension."""

    global _REGISTERED_CLASSES
    if bpy is None:
        return

    if _REGISTERED_CLASSES:
        return

    class MIKU_GPL_OT_execute_bake_request(bpy.types.Operator):
        bl_idname = "miku_gpl.execute_bake_request"
        bl_label = "Execute Miku Bake Request"
        bl_options = {"INTERNAL"}

        request_path: bpy.props.StringProperty(subtype="FILE_PATH")
        output_root: bpy.props.StringProperty(subtype="DIR_PATH")

        def execute(self, context):  # noqa: N802
            try:
                result_path = execute_request(
                    self.request_path,
                    self.output_root,
                )
            except Exception as exc:
                self.report({"ERROR"}, str(exc))
                return {"CANCELLED"}
            context.window_manager["miku_gpl_last_result"] = result_path
            return {"FINISHED"}

    _REGISTERED_CLASSES = [MIKU_GPL_OT_execute_bake_request]
    for cls in _REGISTERED_CLASSES:
        bpy.utils.register_class(cls)


def unregister() -> None:
    """Unregister the artifact-protocol operator."""

    global _REGISTERED_CLASSES
    if bpy is None:
        return
    for cls in reversed(_REGISTERED_CLASSES):
        try:
            bpy.utils.unregister_class(cls)
        except RuntimeError:
            pass
    _REGISTERED_CLASSES = []
