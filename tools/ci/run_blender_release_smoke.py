#!/usr/bin/env python3
"""Run the installed-ZIP Blender compatibility smoke in isolated user data."""

from __future__ import annotations

import argparse
import os
import subprocess
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--blender", type=Path, required=True)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--extension-zip", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    args = parser.parse_args()
    blender = args.blender.resolve()
    extension_zip = args.extension_zip.resolve()
    evidence = args.evidence.resolve()
    for code, path in (
        ("MIKU_BLENDER_EXECUTABLE_MISSING", blender),
        ("MIKU_EXTENSION_ARCHIVE_MISSING", extension_zip),
    ):
        if not path.is_file():
            raise SystemExit(f"{code}:{path}")
    script = ROOT / "tests/blender/miku_installed_compatibility_smoke.py"
    evidence.parent.mkdir(parents=True, exist_ok=True)
    evidence.unlink(missing_ok=True)
    log_path = evidence.with_suffix(".log")
    log_path.unlink(missing_ok=True)
    with tempfile.TemporaryDirectory(
        prefix="miku-blender-user-resources-"
    ) as resources:
        environment = os.environ.copy()
        environment["BLENDER_USER_RESOURCES"] = resources
        command = [
            str(blender),
            "--background",
            "--factory-startup",
            "--python",
            str(script),
            "--",
            "--extension-zip",
            str(extension_zip),
            "--expected-version",
            args.expected_version,
            "--evidence",
            str(evidence),
        ]
        print("+", " ".join(command), flush=True)
        completed = subprocess.run(
            command,
            cwd=ROOT,
            env=environment,
            text=True,
            encoding="utf-8",
            errors="replace",
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            check=False,
        )
    log_path.write_text(completed.stdout, encoding="utf-8")
    print(completed.stdout, end="")
    success_marker = (
        "MIKU_INSTALLED_COMPATIBILITY_SMOKE_OK:" + args.expected_version
    )
    if (
        completed.returncode
        or "Traceback (most recent call last):" in completed.stdout
        or success_marker not in completed.stdout
        or not evidence.is_file()
    ):
        raise SystemExit(
            "Blender installed release smoke failed: "
            f"{args.expected_version} (exit {completed.returncode}); {log_path}"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
