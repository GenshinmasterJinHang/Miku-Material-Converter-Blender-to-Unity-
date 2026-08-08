#!/usr/bin/env python3
"""Run the public Blender 5.0-5.2 smoke suite against an exact runtime."""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
from datetime import UTC, datetime
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SCRIPTS = {
    "miku_bake_quality_smoke.py": "MIKU_BAKE_QUALITY_SMOKE_OK",
    "miku_current_material_frontend_smoke.py": (
        "MIKU_CURRENT_MATERIAL_FRONTEND_SMOKE_OK"
    ),
    "miku_fixed_workflow_textures_smoke.py": (
        "MIKU_FIXED_WORKFLOW_TEXTURES_SMOKE_OK"
    ),
    "miku_height_bake_smoke.py": "MIKU_HEIGHT_BAKE_SMOKE_OK",
    "miku_portable_hybrid_smoke.py": "MIKU_PORTABLE_HYBRID_SMOKE_COMPLETE",
    "miku_runtime_inputs_smoke.py": "MIKU_RUNTIME_INPUTS_SMOKE_COMPLETE",
    "miku_static_pbr_textures_smoke.py": "MIKU_STATIC_PBR_TEXTURES_SMOKE_OK",
    "miku_ui_localization_smoke.py": "MIKU_UI_LOCALIZATION_SMOKE_OK",
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--blender", required=True, type=Path)
    parser.add_argument(
        "--expected-version",
        required=True,
        help="Exact Blender version required from the executable, for example 5.2.0.",
    )
    parser.add_argument(
        "--script",
        action="append",
        dest="scripts",
        help="Run only this tests/blender script; may be repeated.",
    )
    parser.add_argument(
        "--evidence",
        type=Path,
        help="Optional JSON evidence path; a sibling .log is also written.",
    )
    args = parser.parse_args()
    blender = args.blender.resolve()
    if not blender.is_file():
        raise SystemExit(f"MIKU_BLENDER_EXECUTABLE_MISSING:{blender}")
    try:
        expected = tuple(int(part) for part in args.expected_version.split("."))
    except ValueError as exc:
        raise SystemExit(
            f"MIKU_BLENDER_EXPECTED_VERSION_INVALID:{args.expected_version}"
        ) from exc
    if len(expected) != 3 or not (5, 0, 0) <= expected < (5, 3, 0):
        raise SystemExit(
            f"MIKU_BLENDER_EXPECTED_VERSION_UNSUPPORTED:{args.expected_version}"
        )
    version_expression = (
        "import bpy; actual=tuple(bpy.app.version); "
        f"expected={expected!r}; "
        "assert actual == expected, "
        "f'MIKU_BLENDER_VERSION_MISMATCH:expected={expected}:got={actual}'; "
        "print(f'MIKU_BLENDER_VERSION_OK:{actual}')"
    )
    version_command = [
        str(blender),
        "--background",
        "--factory-startup",
        "--python-expr",
        version_expression,
    ]
    print("+", " ".join(version_command), flush=True)
    version_check = subprocess.run(
        version_command,
        cwd=ROOT,
        check=False,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
    )
    print(version_check.stdout, end="")
    if (
        version_check.returncode
        or "MIKU_BLENDER_VERSION_OK:" not in version_check.stdout
        or "Traceback (most recent call last)" in version_check.stdout
    ):
        raise SystemExit(
            "Blender version preflight failed: "
            f"expected {args.expected_version} (exit {version_check.returncode})"
        )
    scripts = tuple(args.scripts or DEFAULT_SCRIPTS)
    captured_output = [version_check.stdout]
    for name in scripts:
        script = (ROOT / "tests" / "blender" / name).resolve()
        if not script.is_file() or ROOT not in script.parents:
            raise SystemExit(f"MIKU_BLENDER_SMOKE_SCRIPT_INVALID:{name}")
        command = [
            str(blender),
            "--background",
            "--factory-startup",
            "--python",
            str(script),
        ]
        print("+", " ".join(command), flush=True)
        completed = subprocess.run(
            command,
            cwd=ROOT,
            check=False,
            text=True,
            encoding="utf-8",
            errors="replace",
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
        )
        print(completed.stdout, end="")
        captured_output.append(completed.stdout)
        marker = DEFAULT_SCRIPTS.get(name)
        if (
            completed.returncode
            or "Traceback (most recent call last)" in completed.stdout
            or marker is not None and marker not in completed.stdout
        ):
            raise SystemExit(
                f"Blender smoke failed: {script.name} (exit {completed.returncode})"
            )
    if args.evidence:
        evidence = args.evidence.resolve()
        evidence.parent.mkdir(parents=True, exist_ok=True)
        payload = {
            "schema": "miku-blender-headless-evidence-1.0",
            "blender": args.expected_version,
            "executableSha256": hashlib.sha256(blender.read_bytes()).hexdigest(),
            "scripts": list(scripts),
            "passed": len(scripts),
            "failed": 0,
            "completedUtc": datetime.now(UTC).isoformat(),
        }
        evidence.write_text(
            json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        evidence.with_suffix(".log").write_text(
            "\n".join(captured_output),
            encoding="utf-8",
        )
    print(f"Blender smoke suite passed: {len(scripts)} scripts")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
