"""Repository identity and certified local-tool constraints for Miku 1.x."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any


BLENDER_ROOT = Path(r"C:\SteamLibrary\steamapps\common\Blender")
BLENDER_EXE = BLENDER_ROOT / "blender.exe"
BLENDER_VERSION = (5, 2, 0)

CANONICAL_SOURCE_ROOTS = (
    "miku",
    "miku_blender",
    "extensions/miku_shader_converter",
    "unity/Packages/com.miku.shaderconverter",
)
RETIRED_SOURCE_ROOTS = (
    "b2u_mvp",
    "b2u_mvp_blender",
    "addons/b2u_mvp_blender",
    "unity/Packages/com.b2u.shaderconverter",
)


def validate_source_boundary(root: Path) -> None:
    """Reject a wrong checkout or an accidental return to retired B2U sources."""

    root = root.resolve()
    missing = [
        relative
        for relative in CANONICAL_SOURCE_ROOTS
        if not (root / Path(relative)).is_dir()
    ]
    if missing:
        raise RuntimeError(
            "MIKU_CANONICAL_SOURCE_ROOT_MISSING:" + ",".join(missing)
        )

    extension_manifest = (
        root / "extensions/miku_shader_converter/blender_manifest.toml"
    ).read_text(encoding="utf-8")
    package = json.loads(
        (
            root / "unity/Packages/com.miku.shaderconverter/package.json"
        ).read_text(encoding="utf-8")
    )
    if 'id = "miku_shader_converter"' not in extension_manifest:
        raise RuntimeError("MIKU_EXTENSION_ID_MISMATCH:miku_shader_converter")
    if 'license = ["SPDX:GPL-3.0-or-later"]' not in extension_manifest:
        raise RuntimeError("MIKU_EXTENSION_LICENSE_MISMATCH")
    if package.get("name") != "com.miku.shaderconverter":
        raise RuntimeError("MIKU_UNITY_PACKAGE_ID_MISMATCH")

    retired_files = []
    for relative in RETIRED_SOURCE_ROOTS:
        candidate = root / Path(relative)
        if not candidate.exists():
            continue
        retired_files.extend(
            path.relative_to(root).as_posix()
            for path in candidate.rglob("*")
            if path.is_file() and "__pycache__" not in path.parts
        )
    if retired_files:
        raise RuntimeError(
            "MIKU_RETIRED_SOURCE_PRESENT:" + ",".join(sorted(retired_files))
        )


def validate_blender_executable(path: Path | str) -> Path:
    """Require the one repository-certified Blender executable."""

    candidate = Path(path).resolve()
    expected = BLENDER_EXE.resolve()
    if candidate != expected:
        raise RuntimeError(
            f"MIKU_BLENDER_EXECUTABLE_MISMATCH:expected={expected}:got={candidate}"
        )
    if not candidate.is_file():
        raise RuntimeError(f"MIKU_BLENDER_EXECUTABLE_MISSING:{candidate}")
    return candidate


def assert_bpy_version(bpy_module: Any) -> None:
    """Fail inside Blender before export/install when the exact version drifts."""

    actual = tuple(getattr(getattr(bpy_module, "app", None), "version", ()))
    if actual != BLENDER_VERSION:
        raise RuntimeError(
            "MIKU_BLENDER_VERSION_MISMATCH:"
            f"expected={BLENDER_VERSION}:got={actual or '<unknown>'}"
        )
