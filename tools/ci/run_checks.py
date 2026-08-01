#!/usr/bin/env python3
"""Run the Miku source, schema, package, and deterministic-build gates."""

from __future__ import annotations

import argparse
import ast
import json
import re
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from tools.miku_environment import validate_source_boundary

LEGACY_IDENTITY_PATTERN = re.compile(r"(?:MiGR|MIGR|MGIR|migr[-_.]|mgir[-_.])")
LEGACY_IDENTITY_ALLOWLIST = {
    Path("miku/legacy.py"),
    Path("miku/migrations.py"),
    Path("miku_blender/__init__.py"),
    Path("unity/Packages/com.miku.shaderconverter/Editor/MikuBundleImporter.cs"),
    Path("unity/Packages/com.miku.shaderconverter/Editor/MikuBundleScriptedImporter.cs"),
    Path("unity/Packages/com.miku.shaderconverter/Editor/MikuLegacyAssetMigration.cs"),
    Path("unity/Packages/com.miku.shaderconverter/Editor/MikuLegacyMgirImporter.cs"),
}


def run(command: list[str]) -> None:
    print("+", " ".join(command), flush=True)
    completed = subprocess.run(command, cwd=str(ROOT), check=False)
    if completed.returncode:
        raise SystemExit(completed.returncode)


def validate_python_sources() -> None:
    roots = ("miku", "miku_blender", "extensions", "tests", "tools")
    count = 0
    for root_name in roots:
        for path in sorted((ROOT / root_name).rglob("*.py")):
            if "__pycache__" in path.parts:
                continue
            ast.parse(path.read_text(encoding="utf-8-sig"), filename=str(path))
            count += 1
    if not count:
        raise RuntimeError("MIKU_PYTHON_SOURCE_EMPTY")
    print(f"parsed {count} Python files")


def validate_schemas() -> None:
    from jsonschema.validators import validator_for

    paths = sorted((ROOT / "schema").glob("miku-*.schema.json"))
    if not paths:
        raise RuntimeError("MIKU_SCHEMA_SET_EMPTY")
    for path in paths:
        schema = json.loads(path.read_text(encoding="utf-8"))
        validator_for(schema).check_schema(schema)
    print(f"validated {len(paths)} Miku schemas")

def validate_identity_boundary() -> None:
    retired_roots = (
        ROOT / "migr",
        ROOT / "migr_blender",
        ROOT / "extensions" / "migr_semantic_exporter",
        ROOT / "extensions" / "migr_gpl_bake_worker",
        ROOT / "unity" / "Packages" / "com.migr.shaderconverter",
    )
    present = [
        path.relative_to(ROOT).as_posix()
        for path in retired_roots
        if path.exists()
        and any(
            candidate.is_file()
            and candidate.suffix.lower() != ".pyc"
            and "__pycache__" not in candidate.parts
            for candidate in path.rglob("*")
        )
    ]
    if present:
        raise RuntimeError("MIKU_RETIRED_SOURCE_ROOT_PRESENT:" + ",".join(present))

    scan_roots = (
        ROOT / "miku",
        ROOT / "miku_blender",
        ROOT / "extensions" / "miku_shader_converter",
        ROOT / "unity" / "Packages" / "com.miku.shaderconverter" / "Editor",
        ROOT / "unity" / "Packages" / "com.miku.shaderconverter" / "Runtime",
    )
    suffixes = {".cs", ".hlsl", ".py", ".shader", ".toml", ".json"}
    violations: list[str] = []
    for scan_root in scan_roots:
        for path in sorted(scan_root.rglob("*")):
            if not path.is_file() or path.suffix.lower() not in suffixes:
                continue
            relative = path.relative_to(ROOT)
            if relative in LEGACY_IDENTITY_ALLOWLIST:
                continue
            text = path.read_text(encoding="utf-8-sig")
            match = LEGACY_IDENTITY_PATTERN.search(text)
            if match:
                line = text.count("\n", 0, match.start()) + 1
                violations.append(f"{relative.as_posix()}:{line}:{match.group(0)}")
    if violations:
        raise RuntimeError(
            "MIKU_ACTIVE_LEGACY_IDENTITY_FORBIDDEN:\n" + "\n".join(violations)
        )
    print("validated active implementation identity allowlist")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--profile",
        choices=("pr", "release"),
        default="pr",
        help="Validation policy profile. Both profiles run deterministic source gates.",
    )
    parser.parse_args()
    if sys.version_info < (3, 11):
        raise SystemExit(
            f"Miku validation requires Python 3.11+; found {sys.version.split()[0]}"
        )
    validate_source_boundary(ROOT)
    print("validated canonical Miku source boundary")
    validate_python_sources()
    validate_schemas()
    validate_identity_boundary()
    run([sys.executable, "-m", "unittest", "discover", "-s", "tests", "-p", "test_*.py"])
    run([sys.executable, "tools/miku_package_identity.py", "--check"])
    run([sys.executable, "tools/build_miku_blender_extensions.py"])
    run([sys.executable, "tools/build_miku_unity_package.py"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
