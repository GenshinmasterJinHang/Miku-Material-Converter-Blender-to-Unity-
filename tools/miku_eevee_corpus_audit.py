"""Audit and optionally export a Blender 5.2 EEVEE material corpus.

Run with the repository-pinned Blender executable:

    blender.exe --background --factory-startup \
      --python tools/miku_eevee_corpus_audit.py -- \
      --library-root <directory> --report <report.json>

The report contains only paths relative to ``--library-root``.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import sys
import traceback
from collections import Counter
from pathlib import Path
from typing import Any

import bpy


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
if str(REPOSITORY_ROOT) not in sys.path:
    sys.path.insert(0, str(REPOSITORY_ROOT))

import miku_blender  # noqa: E402
from miku.bundle import (  # noqa: E402
    normalize_relative_path,
    sha256_file,
    validate_bundle_document,
)
from miku.contracts import (  # noqa: E402
    canonical_hash,
    validate_document,
)
from miku.planner import (  # noqa: E402
    SOURCE_MESH_RESOLVABLE_DIAGNOSTIC_CODES,
    ConversionPlanner,
    default_target_profile,
)
from miku.semantic import build_material_ir  # noqa: E402
from miku_blender.capabilities import (  # noqa: E402
    CYCLES_ONLY,
    REQUIRES_SOURCE_MESH_FIDELITY,
)


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--library-root", required=True)
    parser.add_argument("--report", required=True)
    parser.add_argument("--export-root")
    parser.add_argument("--only-blend")
    parser.add_argument("--only-material")
    parser.add_argument("--expected-bound", type=int)
    parser.add_argument("--expected-unbound", type=int)
    parser.add_argument("--expected-supported", type=int)
    parser.add_argument(
        "--cycles-policy",
        choices=("active", "label", "active-or-label"),
        default="active",
    )
    parser.add_argument("--sample-limit", type=int, default=8)
    parser.add_argument(
        "--resume-existing",
        action="store_true",
        help=(
            "Reuse only bundles whose identity, current IR/plan hashes, and "
            "artifact hashes all verify; mismatches are exported again."
        ),
    )
    parser.add_argument("--require-complete", action="store_true")
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(arguments)


def _stable_identity(*parts: str) -> str:
    return hashlib.sha256("|".join(parts).encode("utf-8")).hexdigest()


def _material_ir_resume_hash(material_ir: dict[str, Any]) -> str:
    """Hash the semantic IR fields that export-time resource binding cannot edit."""

    return canonical_hash(
        {
            key: material_ir.get(key)
            for key in (
                "documentKind",
                "schemaVersion",
                "toolVersion",
                "materialKey",
                "workflow",
                "closureGraph",
                "weightedClosures",
                "surfaceModelPlan",
                "surfaceContract",
            )
        }
    )


def _resume_finalization_valid(material_ir: dict[str, Any]) -> bool:
    """Reject an older bundle whose baked PBR projection was not finalized."""

    surface_plan = material_ir.get("surfaceModelPlan") or {}
    return not (
        str(surface_plan.get("kind") or "") == "CustomMultiLobe"
        and miku_blender._has_source_mesh_pbr_projection(material_ir)
    )


def _cycles_labelled(material: str, object_names: list[str]) -> bool:
    values = [material, *object_names]
    return any("cycles" in str(value or "").casefold() for value in values)


def _scope(
    *,
    bound: bool,
    active_cycles_only: bool,
    cycles_labelled: bool,
    cycles_policy: str,
) -> tuple[str, list[str]]:
    if not bound:
        return "audit-only-unbound", ["material-is-not-bound-to-an-object"]
    exclude_active = active_cycles_only and cycles_policy in {
        "active",
        "active-or-label",
    }
    exclude_label = cycles_labelled and cycles_policy in {
        "label",
        "active-or-label",
    }
    reasons = []
    if exclude_active:
        reasons.append("active-eevee-chain-is-cycles-only")
    if exclude_label:
        reasons.append("material-or-object-name-contains-cycles")
    if not reasons:
        return "supported", []
    if len(reasons) == 2:
        return "excluded-cycles-active-and-label", reasons
    if exclude_active:
        return "excluded-cycles-active", reasons
    return "excluded-cycles-label", reasons


def _material_bindings() -> dict[str, list[Any]]:
    bindings: dict[str, list[Any]] = {}
    for obj in sorted(bpy.data.objects, key=lambda item: item.name_full):
        for slot in getattr(obj, "material_slots", ()) or ():
            material = getattr(slot, "material", None)
            if material is None:
                continue
            bindings.setdefault(material.name_full, []).append(obj)
    for material_name in bindings:
        bindings[material_name].sort(key=lambda item: item.name_full)
    return bindings


def _register_canonical_bake_worker() -> Any:
    package_root = (
        REPOSITORY_ROOT
        / "extensions"
        / "miku_shader_converter"
        / "bake_worker"
    )
    module_path = package_root / "__init__.py"
    spec = importlib.util.spec_from_file_location(
        "_miku_corpus_bake_worker",
        module_path,
        submodule_search_locations=[str(package_root)],
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("MIKU_CANONICAL_BAKE_WORKER_LOAD_FAILED")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    module.register()
    return module


def _activate(obj: Any) -> None:
    if obj is None:
        return
    if bpy.context.mode != "OBJECT" and bpy.ops.object.mode_set.poll():
        bpy.ops.object.mode_set(mode="OBJECT")
    if bpy.ops.object.select_all.poll():
        bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def _existing_bundle_index(export_root: Path) -> dict[str, Path]:
    index: dict[str, Path] = {}
    for bundle_path in sorted(export_root.rglob("*.mikubundle")):
        if bundle_path.is_symlink() or not bundle_path.is_file():
            continue
        try:
            document = json.loads(bundle_path.read_text(encoding="utf-8"))
        except (OSError, UnicodeError, json.JSONDecodeError):
            continue
        material_id = str(document.get("persistentMaterialId") or "")
        if not material_id:
            continue
        if material_id in index:
            raise RuntimeError(
                "MIKU_CORPUS_RESUME_IDENTITY_DUPLICATE:"
                f"{material_id}:{index[material_id]}:{bundle_path}"
            )
        index[material_id] = bundle_path.resolve()
    return index


def _verified_artifact_path(
    bundle_root: Path,
    reference: dict[str, Any],
) -> Path:
    relative = normalize_relative_path(str(reference.get("relativePath") or ""))
    path = (bundle_root / Path(relative)).resolve()
    try:
        path.relative_to(bundle_root)
    except ValueError as error:
        raise RuntimeError("MIKU_CORPUS_RESUME_PATH_ESCAPE") from error
    if path.is_symlink() or not path.is_file():
        raise RuntimeError("MIKU_CORPUS_RESUME_ARTIFACT_MISSING:" + relative)
    if path.stat().st_size != int(reference.get("byteLength") or -1):
        raise RuntimeError("MIKU_CORPUS_RESUME_LENGTH_MISMATCH:" + relative)
    if sha256_file(path) != str(reference.get("sha256") or ""):
        raise RuntimeError("MIKU_CORPUS_RESUME_HASH_MISMATCH:" + relative)
    return path


def _export_record_from_bundle(
    bundle_path: Path,
    export_root: Path,
    *,
    mode: str,
    reused_existing: bool,
) -> dict[str, Any]:
    bundle_document = json.loads(bundle_path.read_text(encoding="utf-8"))
    return {
        "status": "success",
        "mode": mode,
        "reusedExisting": reused_existing,
        "bundleRelativePath": bundle_path.relative_to(
            export_root.resolve()
        ).as_posix(),
        "bundleSha256": sha256_file(bundle_path),
        "sealedDigest": str(bundle_document.get("sealedDigest") or ""),
        "resourceHashes": [
            {
                "relativePath": str(resource.get("relativePath") or ""),
                "semantic": str(resource.get("semantic") or ""),
                "sha256": str(resource.get("sha256") or ""),
                "byteLength": int(resource.get("byteLength") or 0),
            }
            for resource in sorted(
                bundle_document.get("resources") or [],
                key=lambda item: (
                    str(item.get("relativePath") or ""),
                    str(item.get("semantic") or ""),
                ),
            )
            if isinstance(resource, dict)
        ],
    }


def _try_resume_export(
    bundle_path: Path | None,
    export_root: Path,
    *,
    source_id: str,
    material_id: str,
    material_key: str,
    mode: str,
    material_ir_resume_hash: str,
    plan_hash: str,
) -> dict[str, Any] | None:
    if bundle_path is None or not bundle_path.is_file():
        return None
    try:
        bundle = validate_bundle_document(
            json.loads(bundle_path.read_text(encoding="utf-8"))
        )
        if (
            bundle.get("persistentSourceId") != source_id
            or bundle.get("persistentMaterialId") != material_id
            or bundle.get("materialKey") != material_key
            or bundle.get("toolVersion") != "1.0.1"
            or bundle.get("targetProfileHash")
            != default_target_profile()["canonicalHash"]
        ):
            return None
        bundle_root = bundle_path.parent.resolve()
        documents: dict[str, dict[str, Any]] = {}
        for role in ("ir", "plan", "manifest", "sourceMap"):
            reference = bundle.get(role)
            if not isinstance(reference, dict):
                return None
            artifact_path = _verified_artifact_path(bundle_root, reference)
            document = json.loads(artifact_path.read_text(encoding="utf-8"))
            validate_document(document)
            documents[role] = document
        for reference in bundle.get("resources") or []:
            if not isinstance(reference, dict):
                return None
            _verified_artifact_path(bundle_root, reference)
        if (
            _material_ir_resume_hash(documents["ir"])
            != material_ir_resume_hash
            or canonical_hash(documents["plan"]) != plan_hash
            or str(documents["plan"].get("mode") or "") != mode
            or not _resume_finalization_valid(documents["ir"])
        ):
            return None
        return _export_record_from_bundle(
            bundle_path,
            export_root,
            mode=mode,
            reused_existing=True,
        )
    except (
        OSError,
        UnicodeError,
        ValueError,
        TypeError,
        KeyError,
        json.JSONDecodeError,
    ):
        return None


def _features(
    relative_blend: str,
    material: str,
    snapshot: dict[str, Any],
) -> list[str]:
    capability = snapshot["eeveeCapability"]
    active = set(capability.get("activeNodeIds") or [])
    features = {
        "file:" + relative_blend,
        "quality:" + str(capability.get("quality") or ""),
    }
    for node in snapshot.get("nodes", []) or []:
        if str(node.get("id") or "") not in active:
            continue
        features.add("op:" + str(node.get("op") or ""))
        params = node.get("params") or {}
        operation = str(params.get("operation") or "")
        blend_type = str(params.get("blend_type") or "")
        if operation:
            features.add("operation:" + operation)
        if blend_type:
            features.add("blend:" + blend_type)
    surface = snapshot.get("surfaceSemantic") or {}
    features.add("surface:" + str(surface.get("model") or "Unresolved"))
    return sorted(features)


def _select_samples(
    records: list[dict[str, Any]],
    limit: int,
) -> list[dict[str, str]]:
    if limit <= 0:
        return []
    candidates = [
        record
        for record in records
        if record.get("scopeStatus") == "supported"
        and record.get("export", {}).get("status") in {"not-requested", "success"}
    ]
    candidates.sort(
        key=lambda record: (
            str(record.get("blend") or ""),
            str(record.get("material") or ""),
            str((record.get("objects") or [""])[0]),
        )
    )

    def semantic_features(record: dict[str, Any]) -> set[str]:
        return {
            str(feature)
            for feature in record.get("features", [])
            if not str(feature).startswith("file:")
        }

    uncovered = {
        feature
        for record in candidates
        for feature in semantic_features(record)
    }
    selected: list[dict[str, str]] = []
    remaining = list(candidates)

    def choose(predicate: Any) -> None:
        matching = [record for record in remaining if predicate(record)]
        if not matching or len(selected) >= limit:
            return
        matching.sort(
            key=lambda record: (
                -len(semantic_features(record) & uncovered),
                str(record.get("blend") or ""),
                str(record.get("material") or ""),
                str((record.get("objects") or [""])[0]),
            )
        )
        append(matching[0])

    def append(record: dict[str, Any]) -> None:
        remaining.remove(record)
        uncovered.difference_update(semantic_features(record))
        selected.append(
            {
                "blend": str(record.get("blend") or ""),
                "material": str(record.get("material") or ""),
                "object": str((record.get("objects") or [""])[0]),
                "recommendedMode": str(record.get("recommendedMode") or ""),
                "bundlePath": str(
                    record.get("export", {}).get("bundleRelativePath") or ""
                ),
            }
        )

    risk_buckets = (
        lambda record: record.get("capability", {}).get("quality")
        == "NativeOrEquivalent",
        lambda record: record.get("recommendedMode") == "FullPBRBake",
        lambda record: "surface:DielectricScreenRefraction"
        in semantic_features(record),
        lambda record: "op:Input.LightPath" in semantic_features(record),
        lambda record: "op:Texture.Image" in semantic_features(record),
        lambda record: "op:Texture.Magic" in semantic_features(record),
        lambda record: "op:Texture.Brick" in semantic_features(record),
    )
    for predicate in risk_buckets:
        choose(predicate)

    while uncovered and remaining and len(selected) < limit:
        remaining.sort(
            key=lambda record: (
                -len(semantic_features(record) & uncovered),
                str(record.get("blend") or ""),
                str(record.get("material") or ""),
                str((record.get("objects") or [""])[0]),
            )
        )
        record = remaining.pop(0)
        covered = semantic_features(record) & uncovered
        if not covered:
            break
        uncovered -= covered
        selected.append(
            {
                "blend": str(record.get("blend") or ""),
                "material": str(record.get("material") or ""),
                "object": str((record.get("objects") or [""])[0]),
                "recommendedMode": str(record.get("recommendedMode") or ""),
                "bundlePath": str(
                    record.get("export", {}).get("bundleRelativePath") or ""
                ),
            }
        )
    return selected


def _audit_file(
    blend_path: Path,
    library_root: Path,
    export_root: Path | None,
    only_material: str | None,
    cycles_policy: str,
    existing_bundles: dict[str, Path] | None,
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    relative_blend = blend_path.relative_to(library_root).as_posix()
    bpy.ops.wm.open_mainfile(filepath=str(blend_path), load_ui=False)
    bindings = _material_bindings()
    records: list[dict[str, Any]] = []
    failures: list[dict[str, Any]] = []
    for material in sorted(bpy.data.materials, key=lambda item: item.name_full):
        if not bool(getattr(material, "use_nodes", False)):
            continue
        if only_material and material.name_full != only_material:
            continue
        objects = bindings.get(material.name_full, [])
        object_names = [obj.name_full for obj in objects]
        snapshot = miku_blender.snapshot_material(material)
        capability = snapshot["eeveeCapability"]
        scope_status, exclusion_reasons = _scope(
            bound=bool(objects),
            active_cycles_only=(
                capability.get("quality") == CYCLES_ONLY
            ),
            cycles_labelled=_cycles_labelled(
                material.name_full,
                object_names,
            ),
            cycles_policy=cycles_policy,
        )
        initial_mode = (
            "AllowMeshBake"
            if capability.get("quality")
            == REQUIRES_SOURCE_MESH_FIDELITY
            else "Auto"
        )
        record: dict[str, Any] = {
            "blend": relative_blend,
            "material": material.name_full,
            "bound": bool(objects),
            "objects": object_names,
            "capability": capability,
            "scopeStatus": scope_status,
            "exclusionReasons": exclusion_reasons,
            "recommendedMode": (
                initial_mode if scope_status == "supported" else ""
            ),
            "features": _features(
                relative_blend,
                material.name_full,
                snapshot,
            ),
            "snapshotHash": canonical_hash(snapshot),
            "plan": {
                "status": (
                    "not-requested"
                    if scope_status == "supported"
                    else scope_status
                )
            },
            "export": {
                "status": (
                    "not-requested"
                    if scope_status == "supported"
                    else scope_status
                )
            },
        }
        mode = initial_mode
        if scope_status == "supported":
            try:
                material_key = str(
                    (snapshot.get("material") or {}).get("name")
                    or material.name_full
                )
                def compile_plan(
                    selected_mode: str,
                ) -> tuple[dict[str, Any], dict[str, Any], list[dict[str, Any]]]:
                    built_ir = build_material_ir(
                        snapshot,
                        source_blend_id=_stable_identity(relative_blend),
                        material_key=material_key,
                        conversion_mode=selected_mode,
                    )
                    built_plan = ConversionPlanner().plan(
                        built_ir,
                        target_profile=default_target_profile(),
                        mode=selected_mode,
                    )
                    built_errors = [
                        item
                        for item in built_plan.get("diagnostics", []) or []
                        if isinstance(item, dict)
                        and str(item.get("severity") or "").lower()
                        == "error"
                    ]
                    return built_ir, built_plan, built_errors

                resolved_error_codes: list[str] = []
                resolved_diagnostics: list[dict[str, Any]] = []
                resolution_path: list[str] = []
                try:
                    ir, plan, errors = compile_plan(mode)
                except ValueError as error:
                    if (
                        mode != "AllowMeshBake"
                        or not str(error).startswith(
                            "MIKU_CLOSURE_INPUT_MISSING:"
                        )
                    ):
                        raise
                    resolved_error_codes.append(
                        "MIKU_CLOSURE_INPUT_MISSING"
                    )
                    resolved_diagnostics.append(
                        {
                            "severity": "error",
                            "code": "MIKU_CLOSURE_INPUT_MISSING",
                            "message": str(error),
                            "sourceNode": next(
                                (
                                    {
                                        "id": str(node.get("id") or ""),
                                        "op": str(node.get("op") or ""),
                                        "source": dict(node.get("source") or {}),
                                        "inputs": [
                                            dict(item)
                                            for item in node.get("inputs") or []
                                            if isinstance(item, dict)
                                        ],
                                    }
                                    for node in snapshot.get("nodes") or []
                                    if isinstance(node, dict)
                                    and str(node.get("id") or "")
                                    in str(error)
                                ),
                                {},
                            ),
                        }
                    )
                    resolution_path.append(
                        "AllowMeshBake->FullPBRBake"
                    )
                    mode = "FullPBRBake"
                    ir, plan, errors = compile_plan(mode)
                error_codes = {
                    str(item.get("code") or "") for item in errors
                }
                if (
                    mode == "Auto"
                    and errors
                    and error_codes.issubset(
                        SOURCE_MESH_RESOLVABLE_DIAGNOSTIC_CODES
                    )
                    and "MIKU_SOURCE_MESH_FIDELITY_REQUIRED"
                    in error_codes
                ):
                    resolved_error_codes = sorted(
                        code for code in error_codes if code
                    )
                    resolved_diagnostics = [
                        dict(item) for item in errors
                    ]
                    capability = {
                        **capability,
                        "quality": REQUIRES_SOURCE_MESH_FIDELITY,
                        "evidence": [
                            *list(capability.get("evidence") or []),
                            {
                                "nodeId": "",
                                "op": "Semantic.SurfaceModel",
                                "blenderNodeType": "",
                                "displayName": "",
                                "quality": (
                                    REQUIRES_SOURCE_MESH_FIDELITY
                                ),
                                "reason": (
                                    "native semantic compilation requested an "
                                    "explicit source-mesh PBR projection"
                                ),
                            },
                        ],
                    }
                    record["capability"] = capability
                    resolution_path.append("Auto->AllowMeshBake")
                    mode = "AllowMeshBake"
                    try:
                        ir, plan, errors = compile_plan(mode)
                    except ValueError as error:
                        if not str(error).startswith(
                            "MIKU_CLOSURE_INPUT_MISSING:"
                        ):
                            raise
                        errors = [
                            {
                                "severity": "error",
                                "code": "MIKU_FULL_PBR_BAKE_REQUIRED",
                                "message": str(error),
                            }
                        ]
                    error_codes = {
                        str(item.get("code") or "") for item in errors
                    }
                unresolved_required_channels = sorted(
                    str(channel.get("semantic") or "")
                    for channel in ir.get("channels") or []
                    if isinstance(channel, dict)
                    and bool(channel.get("required"))
                    and channel.get("value") is None
                    and channel.get("default") is None
                    and not bool(channel.get("requiresBake"))
                    and str(channel.get("semantic") or "")
                )
                if (
                    mode == "AllowMeshBake"
                    and not errors
                    and unresolved_required_channels
                    and str(plan.get("surfaceModel") or "")
                    not in {"CustomMultiLobe", "RefractiveGlass"}
                ):
                    resolved_error_codes.append(
                        "MIKU_REQUIRED_CHANNEL_UNRESOLVED"
                    )
                    resolved_diagnostics.append(
                        {
                            "severity": "error",
                            "code": "MIKU_REQUIRED_CHANNEL_UNRESOLVED",
                            "surfaceModel": str(
                                plan.get("surfaceModel") or ""
                            ),
                            "semantics": unresolved_required_channels,
                            "message": (
                                "Source Mesh Fidelity left required channels "
                                "unresolved: "
                                + ", ".join(unresolved_required_channels)
                            ),
                        }
                    )
                    resolution_path.append(
                        "AllowMeshBake->FullPBRBake"
                    )
                    mode = "FullPBRBake"
                    ir, plan, errors = compile_plan(mode)
                    error_codes = {
                        str(item.get("code") or "") for item in errors
                    }
                if (
                    mode == "AllowMeshBake"
                    and errors
                    and error_codes.issubset(
                        SOURCE_MESH_RESOLVABLE_DIAGNOSTIC_CODES
                    )
                ):
                    resolved_error_codes = sorted(
                        {
                            *resolved_error_codes,
                            *(code for code in error_codes if code),
                        }
                    )
                    resolved_diagnostics.extend(dict(item) for item in errors)
                    resolution_path.append("AllowMeshBake->FullPBRBake")
                    mode = "FullPBRBake"
                    ir, plan, errors = compile_plan(mode)
                record["recommendedMode"] = mode
                record["plan"] = {
                    "status": "failed" if errors else "success",
                    "mode": mode,
                    "materialIrHash": canonical_hash(ir),
                    "materialIrResumeHash": _material_ir_resume_hash(ir),
                    "planHash": canonical_hash(plan),
                    "surfaceModel": str(plan.get("surfaceModel") or ""),
                    "bakeJobCount": len(plan.get("bakeJobs") or []),
                    "bakeJobs": [
                        dict(item)
                        for item in plan.get("bakeJobs") or []
                        if isinstance(item, dict)
                    ],
                    "bakeJobSources": [
                        {
                            "id": str(node.get("id") or ""),
                            "op": str(node.get("op") or ""),
                            "source": dict(node.get("source") or {}),
                        }
                        for node in snapshot.get("nodes") or []
                        if isinstance(node, dict)
                        and str(node.get("id") or "")
                        in {
                            str(job.get("sourceNodeId") or "")
                            for job in plan.get("bakeJobs") or []
                            if isinstance(job, dict)
                        }
                    ],
                    "requiredUnresolvedChannels": sorted(
                        str(channel.get("semantic") or "")
                        for channel in ir.get("channels") or []
                        if isinstance(channel, dict)
                        and bool(channel.get("required"))
                        and channel.get("value") is None
                        and channel.get("default") is None
                        and not bool(channel.get("requiresBake"))
                    ),
                    "errorCodes": sorted(
                        {
                            str(item.get("code") or "")
                            for item in errors
                            if str(item.get("code") or "")
                        }
                    ),
                    "resolvedBySourceMeshFidelity": (
                        resolved_error_codes
                    ),
                    "resolvedDiagnostics": resolved_diagnostics,
                    "resolutionPath": resolution_path,
                }
            except Exception as error:
                record["plan"] = {
                    "status": "failed",
                    "mode": mode,
                    "error": str(error),
                }
        if export_root is not None and scope_status == "supported":
            if record["plan"].get("status") == "success":
                try:
                    source_id = _stable_identity(relative_blend)
                    material_id = _stable_identity(
                        relative_blend,
                        material.name_full,
                        objects[0].name_full,
                    )
                    material_key = str(
                        (snapshot.get("material") or {}).get("name")
                        or material.name_full
                    )
                    resumed = _try_resume_export(
                        (
                            existing_bundles.get(material_id)
                            if existing_bundles is not None
                            else None
                        ),
                        export_root,
                        source_id=source_id,
                        material_id=material_id,
                        material_key=material_key,
                        mode=mode,
                        material_ir_resume_hash=str(
                            record["plan"]["materialIrResumeHash"]
                        ),
                        plan_hash=str(record["plan"]["planHash"]),
                    )
                    if resumed is not None:
                        record["export"] = resumed
                    else:
                        _activate(objects[0])
                        result = miku_blender.export_material_bundle(
                            material,
                            str(export_root),
                            source_blend_id=source_id,
                            persistent_material_id=material_id,
                            mode=mode,
                        )
                        bundle_path = Path(result["bundlePath"]).resolve()
                        record["export"] = _export_record_from_bundle(
                            bundle_path,
                            export_root,
                            mode=mode,
                            reused_existing=False,
                        )
                except Exception as error:  # Report every material independently.
                    message = str(error.getBaseException()) if hasattr(
                        error, "getBaseException"
                    ) else str(error)
                    record["export"] = {
                        "status": "failed",
                        "mode": mode,
                        "error": message,
                    }
                    failures.append(
                        {
                            "blend": relative_blend,
                            "material": material.name_full,
                            "error": message,
                            "traceback": traceback.format_exc(limit=8),
                        }
                    )
            else:
                record["export"] = {
                    "status": "blocked-by-plan",
                    "mode": mode,
                }
        records.append(record)
    return records, failures


def main() -> int:
    arguments = _arguments()
    if tuple(bpy.app.version) != (5, 2, 0):
        raise RuntimeError(
            "MIKU_BLENDER_VERSION_MISMATCH:"
            f"expected=5.2.0:got={bpy.app.version_string}"
        )
    library_root = Path(arguments.library_root).resolve()
    report_path = Path(arguments.report).resolve()
    export_root = (
        Path(arguments.export_root).resolve()
        if arguments.export_root
        else None
    )
    if not library_root.is_dir():
        raise RuntimeError("MIKU_CORPUS_ROOT_MISSING")
    if arguments.resume_existing and export_root is None:
        raise RuntimeError("MIKU_CORPUS_RESUME_REQUIRES_EXPORT_ROOT")
    existing_bundles: dict[str, Path] | None = None
    if export_root is not None:
        export_root.mkdir(parents=True, exist_ok=True)
        _register_canonical_bake_worker()
        if arguments.resume_existing:
            existing_bundles = _existing_bundle_index(export_root)

    records: list[dict[str, Any]] = []
    failures: list[dict[str, Any]] = []
    blend_files = sorted(
        path
        for path in library_root.rglob("*.blend")
        if path.is_file()
    )
    if arguments.only_blend:
        requested = (library_root / arguments.only_blend).resolve()
        if requested not in blend_files:
            raise RuntimeError("MIKU_CORPUS_BLEND_FILTER_MISSING")
        blend_files = [requested]
    for blend_path in blend_files:
        file_records, file_failures = _audit_file(
            blend_path,
            library_root,
            export_root,
            arguments.only_material,
            arguments.cycles_policy,
            existing_bundles,
        )
        records.extend(file_records)
        failures.extend(file_failures)

    bound = sum(bool(record["bound"]) for record in records)
    unbound = len(records) - bound
    quality_counts = Counter(
        str(record["capability"].get("quality") or "") for record in records
    )
    scope_counts = Counter(str(record["scopeStatus"]) for record in records)
    supported_records = [
        record
        for record in records
        if record["scopeStatus"] == "supported"
    ]
    supported = len(supported_records)
    excluded = sum(
        status.startswith("excluded-cycles")
        for status in (str(record["scopeStatus"]) for record in records)
    )
    recommended_mode_counts = Counter(
        str(record.get("recommendedMode") or "")
        for record in supported_records
    )
    hard_failures = [
        record
        for record in supported_records
        if (
            record["plan"].get("status") != "success"
            or (
                export_root is not None
                and record["export"].get("status") != "success"
            )
        )
    ]
    report = {
        "documentKind": "miku-eevee-corpus-audit-1.0",
        "blenderVersion": list(bpy.app.version),
        "files": len(blend_files),
        "materials": len(records),
        "boundMaterials": bound,
        "unboundMaterials": unbound,
        "cyclesPolicy": arguments.cycles_policy,
        "supportedMaterials": supported,
        "excludedMaterials": excluded,
        "qualityCounts": dict(sorted(quality_counts.items())),
        "scopeCounts": dict(sorted(scope_counts.items())),
        "recommendedModeCounts": dict(
            sorted(recommended_mode_counts.items())
        ),
        "hardFailureCount": len(hard_failures),
        "records": records,
        "sampleSelection": _select_samples(
            records,
            arguments.sample_limit,
        ),
        "failures": failures,
    }
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(
        json.dumps(
            report,
            ensure_ascii=False,
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )

    if (
        arguments.expected_bound is not None
        and bound != arguments.expected_bound
    ):
        raise RuntimeError(
            f"MIKU_CORPUS_BOUND_COUNT_MISMATCH:{bound}:"
            f"expected={arguments.expected_bound}"
        )
    if (
        arguments.expected_unbound is not None
        and unbound != arguments.expected_unbound
    ):
        raise RuntimeError(
            f"MIKU_CORPUS_UNBOUND_COUNT_MISMATCH:{unbound}:"
            f"expected={arguments.expected_unbound}"
        )
    if (
        arguments.expected_supported is not None
        and supported != arguments.expected_supported
    ):
        raise RuntimeError(
            f"MIKU_CORPUS_SUPPORTED_COUNT_MISMATCH:{supported}:"
            f"expected={arguments.expected_supported}"
        )
    if hard_failures and (
        arguments.require_complete or export_root is not None
    ):
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
