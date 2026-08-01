"""License-neutral request/result contract for the external Blender bake worker."""

from __future__ import annotations

from typing import Any, Mapping

from .contracts import DocumentValidationError, make_document, validate_document


BLENDER_VERSION = "5.2.0 LTS"
BLENDER_COMMIT = "fbe6228777e7d9afefcd61a413844e790ae75db7"
BAKE_SETTINGS = {
    "blenderVersion": BLENDER_VERSION,
    "blenderCommit": BLENDER_COMMIT,
    "engine": "CYCLES",
    "device": "CPU",
    "resolution": 1024,
    "samples": 16,
    "margin": 16,
    "randomSeed": 0,
}


def make_bake_request(
    persistent_source_id: str,
    persistent_material_id: str,
    jobs: list[Mapping[str, Any]],
    *,
    source_material_name: str,
    source_snapshot: Mapping[str, Any],
    allow_appearance_approximation: bool = False,
    mesh_binding: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    if not persistent_source_id or not persistent_material_id:
        raise DocumentValidationError("MIKU_BAKE_IDENTITY_MISSING", "Persistent source and material IDs are required")
    if not jobs:
        raise DocumentValidationError("MIKU_BAKE_JOBS_MISSING", "At least one bake job is required")
    if not source_material_name or not isinstance(source_snapshot, Mapping):
        raise DocumentValidationError(
            "MIKU_BAKE_SOURCE_MISSING",
            "The Blender material selector and private source snapshot are required",
        )
    payload: dict[str, Any] = {
        "persistentSourceId": persistent_source_id,
        "persistentMaterialId": persistent_material_id,
        "sourceMaterialName": source_material_name,
        "sourceSnapshot": dict(source_snapshot),
        "allowAppearanceApproximation": bool(allow_appearance_approximation),
        "jobs": [dict(item) for item in jobs],
        "settings": dict(BAKE_SETTINGS),
    }
    if mesh_binding:
        payload["meshBinding"] = dict(mesh_binding)
    return make_document("miku-bake-request-1.0", payload)


def make_bake_result(
    request: Mapping[str, Any],
    resources: list[Mapping[str, Any]],
    *,
    status: str,
    diagnostics: list[Mapping[str, Any]] | None = None,
    mesh_binding: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    validate_document(request, "miku-bake-request-1.0")
    if status not in {"completed", "failed"}:
        raise DocumentValidationError("MIKU_BAKE_STATUS_INVALID", status)
    if status == "completed" and not resources:
        raise DocumentValidationError("MIKU_BAKE_RESOURCES_MISSING", "Completed bake results require resources")
    payload: dict[str, Any] = {
        "requestHash": request["canonicalHash"],
        "status": status,
        "resources": [dict(item) for item in resources],
        "diagnostics": list(diagnostics or []),
    }
    if mesh_binding:
        payload["meshBinding"] = dict(mesh_binding)
    return make_document("miku-bake-result-1.0", payload)


def validate_bake_result(result: Mapping[str, Any], request: Mapping[str, Any]) -> dict[str, Any]:
    value = validate_document(result, "miku-bake-result-1.0")
    if value.get("requestHash") != request.get("canonicalHash"):
        raise DocumentValidationError("MIKU_BAKE_REQUEST_HASH_MISMATCH", "Bake result belongs to another request")
    status = value.get("status")
    resources = value.get("resources")
    if status != "completed":
        diagnostics = value.get("diagnostics")
        first = (
            diagnostics[0]
            if isinstance(diagnostics, list)
            and diagnostics
            and isinstance(diagnostics[0], Mapping)
            else {}
        )
        raise DocumentValidationError(
            str(first.get("code") or "MIKU_BAKE_FAILED"),
            str(
                first.get("message")
                or "Bake result is not completed"
            ),
        )
    if not isinstance(resources, list) or not resources:
        raise DocumentValidationError("MIKU_BAKE_RESOURCES_MISSING", "Completed bake result has no resources")
    return value
