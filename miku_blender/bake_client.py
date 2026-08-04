"""MIT-side client for the artifact-only GPL bake worker boundary."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Mapping

try:
    from ..miku.bake_protocol import (
        DEFAULT_BAKE_RESOLUTION,
        make_bake_request,
        validate_bake_result,
    )
except ImportError:
    from miku.bake_protocol import (
        DEFAULT_BAKE_RESOLUTION,
        make_bake_request,
        validate_bake_result,
    )


def execute_bake(
    graph: Mapping[str, Any],
    plan: Mapping[str, Any],
    target: Path,
    *,
    material_name: str,
    persistent_source_id: str,
    persistent_material_id: str,
    allow_appearance_approximation: bool = False,
    bake_resolution: int = DEFAULT_BAKE_RESOLUTION,
) -> tuple[dict[str, Any], dict[str, Any]]:
    """Exchange only versioned JSON artifacts with the separately licensed worker."""

    jobs = list(plan.get("bakeJobs") or [])
    request = make_bake_request(
        persistent_source_id,
        persistent_material_id,
        jobs,
        source_material_name=material_name,
        source_snapshot=graph,
        allow_appearance_approximation=allow_appearance_approximation,
        resolution=bake_resolution,
    )
    request_path = target / f"{persistent_material_id}.miku-bake-request.json"
    _write_json(request_path, request)
    _invoke_gpl_worker(request_path, target)
    expected_result = (target / f"{persistent_material_id}.miku-bake-result.json").resolve()
    if not expected_result.is_file():
        raise RuntimeError("MIKU_BAKE_RESULT_PATH_INVALID")
    result = json.loads(expected_result.read_text(encoding="utf-8"))
    return request, validate_bake_result(result, request)


def _invoke_gpl_worker(request_path: Path, target: Path) -> None:
    """Invoke the separately licensed extension through Blender's operator ABI."""

    try:
        import bpy
    except ImportError as exc:  # pragma: no cover - Blender-only boundary
        raise RuntimeError("MIKU_GPL_BAKE_WORKER_REQUIRES_BLENDER") from exc
    namespace = getattr(getattr(bpy, "ops", None), "miku_gpl", None)
    operator = getattr(namespace, "execute_bake_request", None)
    if operator is None:
        raise RuntimeError(
            "MIKU_GPL_BAKE_WORKER_REQUIRED: install and enable the "
            "miku_gpl_bake_worker Blender Extension"
        )
    outcome = operator(
        request_path=str(request_path),
        output_root=str(target),
    )
    if "FINISHED" not in set(outcome or ()):
        result_path = target / (
            request_path.name.removesuffix(".miku-bake-request.json") + ".miku-bake-result.json"
        )
        if result_path.is_file():
            try:
                failed = json.loads(result_path.read_text(encoding="utf-8"))
                diagnostics = failed.get("diagnostics")
                first = (
                    diagnostics[0]
                    if isinstance(diagnostics, list)
                    and diagnostics
                    and isinstance(diagnostics[0], Mapping)
                    else {}
                )
                if first:
                    raise RuntimeError(
                        f"{first.get('code') or 'MIKU_BAKE_FAILED'}:"
                        f"{first.get('message') or 'Bake failed'}"
                    )
            except RuntimeError:
                raise
            except (OSError, ValueError, TypeError):
                pass
        raise RuntimeError("MIKU_GPL_BAKE_WORKER_FAILED")


def _write_json(path: Path, value: Mapping[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(
        json.dumps(value, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    temporary.replace(path)
