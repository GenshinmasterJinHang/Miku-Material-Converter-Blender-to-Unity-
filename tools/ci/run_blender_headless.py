#!/usr/bin/env python3
"""Run the public, self-contained Blender 5.2 smoke suite."""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SCRIPTS = (
    "miku_bake_quality_smoke.py",
    "miku_current_material_frontend_smoke.py",
    "miku_fixed_workflow_textures_smoke.py",
    "miku_height_bake_smoke.py",
    "miku_portable_hybrid_smoke.py",
    "miku_runtime_inputs_smoke.py",
    "miku_static_pbr_textures_smoke.py",
    "miku_ui_localization_smoke.py",
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--blender", required=True, type=Path)
    parser.add_argument(
        "--script",
        action="append",
        dest="scripts",
        help="Run only this tests/blender script; may be repeated.",
    )
    args = parser.parse_args()
    blender = args.blender.resolve()
    if not blender.is_file():
        raise SystemExit(f"MIKU_BLENDER_EXECUTABLE_MISSING:{blender}")
    scripts = tuple(args.scripts or DEFAULT_SCRIPTS)
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
        completed = subprocess.run(command, cwd=ROOT, check=False)
        if completed.returncode:
            raise SystemExit(
                f"Blender smoke failed: {script.name} (exit {completed.returncode})"
            )
    print(f"Blender smoke suite passed: {len(scripts)} scripts")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
