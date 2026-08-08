"""License-neutral request/result contract for the external Blender bake worker."""

from __future__ import annotations

import re
from typing import Any, Mapping

from .contracts import DocumentValidationError, make_document, validate_document


BLENDER_VERSION = "5.2.0 LTS"
BLENDER_COMMIT = "fbe6228777e7d9afefcd61a413844e790ae75db7"
CERTIFIED_RUNTIME_VERSION = "5.2.0"
MIN_RUNTIME_VERSION = (5, 0, 0)
MAX_RUNTIME_VERSION_EXCLUSIVE = (5, 3, 0)
SUPPORTED_BAKE_RESOLUTIONS = (512, 1024, 2048, 4096)
DEFAULT_BAKE_RESOLUTION = 1024
BAKE_REQUEST_KINDS = frozenset(
    {
        "miku-bake-request-1.0",
        "miku-bake-request-1.1",
        "miku-bake-request-1.2",
    }
)
BAKE_SETTINGS = {
    "blenderVersion": CERTIFIED_RUNTIME_VERSION,
    "blenderCommit": BLENDER_COMMIT,
    "engine": "CYCLES",
    "device": "CPU",
    "resolution": DEFAULT_BAKE_RESOLUTION,
    "samples": 16,
    "margin": 16,
    "randomSeed": 0,
}
LEGACY_BAKE_SETTINGS = {
    **BAKE_SETTINGS,
    "blenderVersion": BLENDER_VERSION,
}
_BUILD_HASH = re.compile(r"^[0-9a-f]{12,64}$")


def normalize_bake_blender_version(value: Any) -> str:
    text = str(value or "")
    match = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)", text)
    if match is None:
        raise DocumentValidationError(
            "MIKU_BAKE_BLENDER_VERSION_INVALID",
            "Blender version must be a numeric MAJOR.MINOR.PATCH value",
            "$.settings.blenderVersion",
        )
    version = tuple(int(part) for part in match.groups())
    if not MIN_RUNTIME_VERSION <= version < MAX_RUNTIME_VERSION_EXCLUSIVE:
        raise DocumentValidationError(
            "MIKU_BAKE_BLENDER_VERSION_UNSUPPORTED",
            "Supported Blender range is >=5.0.0 and <5.3.0, "
            f"got {text}",
            "$.settings.blenderVersion",
        )
    return text


def normalize_bake_blender_commit(value: Any) -> str:
    commit = str(value or "").strip().lower()
    if _BUILD_HASH.fullmatch(commit) is None:
        raise DocumentValidationError(
            "MIKU_BAKE_BLENDER_COMMIT_INVALID",
            "Blender build hash must contain 12 to 64 lowercase hexadecimal characters",
            "$.settings.blenderCommit",
        )
    return commit


def validate_bake_runtime_binding(
    request: Mapping[str, Any],
    actual_version: str,
    actual_commit: str,
) -> None:
    """Ensure a bake request was created for the executing Blender build."""

    kind = str(request.get("documentKind") or "")
    settings = request.get("settings")
    if not isinstance(settings, Mapping):
        raise ValueError("MIKU_BAKE_SETTINGS_INVALID")
    if kind in {"miku-bake-request-1.0", "miku-bake-request-1.1"}:
        commit_matches = (
            len(actual_commit) >= 12 and BLENDER_COMMIT.startswith(actual_commit)
        )
        if actual_version != CERTIFIED_RUNTIME_VERSION or not commit_matches:
            raise RuntimeError(
                "MIKU_UNCERTIFIED_BLENDER:"
                f" expected {BLENDER_VERSION} ({BLENDER_COMMIT}),"
                f" got {actual_version or '<unknown>'} "
                f"({actual_commit or '<unknown>'})"
            )
        return
    requested_version = str(settings.get("blenderVersion") or "")
    requested_commit = str(settings.get("blenderCommit") or "")
    if requested_version != actual_version:
        raise RuntimeError(
            "MIKU_BAKE_BLENDER_VERSION_MISMATCH:"
            f"request={requested_version}:actual={actual_version}"
        )
    if requested_commit != actual_commit:
        raise RuntimeError(
            "MIKU_BAKE_BLENDER_COMMIT_MISMATCH:"
            f"request={requested_commit}:actual={actual_commit or '<unknown>'}"
        )


def normalize_bake_resolution(value: Any) -> int:
    if isinstance(value, bool):
        value = None
    try:
        resolution = int(value)
    except (TypeError, ValueError) as exc:
        raise DocumentValidationError(
            "MIKU_BAKE_RESOLUTION_INVALID",
            "Bake resolution must be one of 512, 1024, 2048, or 4096",
            "$.settings.resolution",
        ) from exc
    if (isinstance(value, float) and value != resolution) or (
        resolution not in SUPPORTED_BAKE_RESOLUTIONS
    ):
        raise DocumentValidationError(
            "MIKU_BAKE_RESOLUTION_INVALID",
            "Bake resolution must be one of 512, 1024, 2048, or 4096",
            "$.settings.resolution",
        )
    return resolution


def validate_bake_request(request: Mapping[str, Any]) -> dict[str, Any]:
    value = validate_document(request)
    kind = str(value.get("documentKind") or "")
    if kind not in BAKE_REQUEST_KINDS:
        raise DocumentValidationError(
            "MIKU_BAKE_REQUEST_SCHEMA_UNSUPPORTED",
            f"Unsupported bake request kind: {kind or '<missing>'}",
        )
    settings = value.get("settings")
    if not isinstance(settings, Mapping):
        raise DocumentValidationError(
            "MIKU_BAKE_SETTINGS_MISSING",
            "Bake request settings must be an object",
            "$.settings",
        )
    setting_keys = set(settings)
    expected_setting_keys = set(BAKE_SETTINGS)
    if setting_keys != expected_setting_keys:
        missing = sorted(expected_setting_keys - setting_keys)
        unexpected = sorted(setting_keys - expected_setting_keys)
        raise DocumentValidationError(
            "MIKU_BAKE_SETTINGS_INVALID",
            f"Missing settings: {missing}; unexpected settings: {unexpected}",
            "$.settings",
        )
    resolution = normalize_bake_resolution(settings.get("resolution"))
    if kind == "miku-bake-request-1.0" and resolution != DEFAULT_BAKE_RESOLUTION:
        raise DocumentValidationError(
            "MIKU_BAKE_RESOLUTION_INVALID",
            "Bake request 1.0 requires a 1024 resolution",
            "$.settings.resolution",
        )
    expected_settings = (
        LEGACY_BAKE_SETTINGS
        if kind in {"miku-bake-request-1.0", "miku-bake-request-1.1"}
        else BAKE_SETTINGS
    )
    for key, expected in expected_settings.items():
        if key in {"resolution", "blenderVersion", "blenderCommit"}:
            continue
        if settings.get(key) != expected:
            raise DocumentValidationError(
                "MIKU_BAKE_SETTING_INVALID",
                f"Certified bake setting {key} must be {expected!r}",
                f"$.settings.{key}",
            )
    if kind in {"miku-bake-request-1.0", "miku-bake-request-1.1"}:
        for key in ("blenderVersion", "blenderCommit"):
            expected = LEGACY_BAKE_SETTINGS[key]
            if settings.get(key) != expected:
                raise DocumentValidationError(
                    "MIKU_BAKE_SETTING_INVALID",
                    f"Frozen bake setting {key} must be {expected!r}",
                    f"$.settings.{key}",
                )
    else:
        normalize_bake_blender_version(settings.get("blenderVersion"))
        normalize_bake_blender_commit(settings.get("blenderCommit"))
    return value


def make_bake_request(
    persistent_source_id: str,
    persistent_material_id: str,
    jobs: list[Mapping[str, Any]],
    *,
    source_material_name: str,
    source_snapshot: Mapping[str, Any],
    allow_appearance_approximation: bool = False,
    mesh_binding: Mapping[str, Any] | None = None,
    resolution: int = DEFAULT_BAKE_RESOLUTION,
    blender_version: str = CERTIFIED_RUNTIME_VERSION,
    blender_commit: str = BLENDER_COMMIT,
) -> dict[str, Any]:
    if not persistent_source_id or not persistent_material_id:
        raise DocumentValidationError(
            "MIKU_BAKE_IDENTITY_MISSING", "Persistent source and material IDs are required"
        )
    if not jobs:
        raise DocumentValidationError("MIKU_BAKE_JOBS_MISSING", "At least one bake job is required")
    if not source_material_name or not isinstance(source_snapshot, Mapping):
        raise DocumentValidationError(
            "MIKU_BAKE_SOURCE_MISSING",
            "The Blender material selector and private source snapshot are required",
        )
    settings = dict(BAKE_SETTINGS)
    settings["resolution"] = normalize_bake_resolution(resolution)
    settings["blenderVersion"] = normalize_bake_blender_version(blender_version)
    settings["blenderCommit"] = normalize_bake_blender_commit(blender_commit)
    payload: dict[str, Any] = {
        "persistentSourceId": persistent_source_id,
        "persistentMaterialId": persistent_material_id,
        "sourceMaterialName": source_material_name,
        "sourceSnapshot": dict(source_snapshot),
        "allowAppearanceApproximation": bool(allow_appearance_approximation),
        "jobs": [dict(item) for item in jobs],
        "settings": settings,
    }
    if mesh_binding:
        payload["meshBinding"] = dict(mesh_binding)
    return make_document("miku-bake-request-1.2", payload)


def make_bake_result(
    request: Mapping[str, Any],
    resources: list[Mapping[str, Any]],
    *,
    status: str,
    diagnostics: list[Mapping[str, Any]] | None = None,
    mesh_binding: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    validate_bake_request(request)
    if status not in {"completed", "failed"}:
        raise DocumentValidationError("MIKU_BAKE_STATUS_INVALID", status)
    if status == "completed" and not resources:
        raise DocumentValidationError(
            "MIKU_BAKE_RESOURCES_MISSING", "Completed bake results require resources"
        )
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
    validate_bake_request(request)
    value = validate_document(result, "miku-bake-result-1.0")
    if value.get("requestHash") != request.get("canonicalHash"):
        raise DocumentValidationError(
            "MIKU_BAKE_REQUEST_HASH_MISMATCH", "Bake result belongs to another request"
        )
    status = value.get("status")
    resources = value.get("resources")
    if status != "completed":
        diagnostics = value.get("diagnostics")
        first = (
            diagnostics[0]
            if isinstance(diagnostics, list) and diagnostics and isinstance(diagnostics[0], Mapping)
            else {}
        )
        raise DocumentValidationError(
            str(first.get("code") or "MIKU_BAKE_FAILED"),
            str(first.get("message") or "Bake result is not completed"),
        )
    if not isinstance(resources, list) or not resources:
        raise DocumentValidationError(
            "MIKU_BAKE_RESOURCES_MISSING", "Completed bake result has no resources"
        )
    return value
