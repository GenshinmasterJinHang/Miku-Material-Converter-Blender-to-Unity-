"""Headless Miku exporter for the locked five-file metal corpus.

This driver is deliberately small: Blender is only used to read the public
``bpy`` data model and produce semantic IR/plan/source-map documents.  It
never serializes a Blender node list into the public IR and excludes ``.blend1``
recovery files by construction.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

import bpy


def _args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("output_root")
    parser.add_argument(
        "--mode", default="Auto", choices=["Auto", "NativeOnly", "ReusableBakeOnly"]
    )
    parser.add_argument("--source-id", default="")
    parser.add_argument("--material", action="append", default=[])
    parser.add_argument("--allow-appearance-approximation", action="store_true")
    return parser.parse_args(sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else [])


def _source_id() -> str:
    value = str(
        getattr(bpy.context.scene, "get", lambda *_: "")(
            "miku_source_id",
            "",
        )
        or ""
    )
    if not value:
        raise RuntimeError("MIKU_SOURCE_ID_REQUIRED: pass --source-id for certified batch export")
    return value


def main() -> int:
    args = _args()
    repo = Path(__file__).resolve().parents[1]
    if str(repo) not in sys.path:
        sys.path.insert(0, str(repo))
    from tools.miku_environment import (
        assert_bpy_version,
        validate_blender_executable,
    )

    validate_blender_executable(Path(bpy.app.binary_path))
    assert_bpy_version(bpy)
    import miku_blender

    source_id = args.source_id or _source_id()
    source_path = Path(str(getattr(bpy.data, "filepath", "")))
    blend_key = source_path.stem
    destination = Path(args.output_root).resolve() / blend_key
    destination.mkdir(parents=True, exist_ok=True)
    results = miku_blender.export_selected_materials(
        str(destination),
        mode=args.mode,
        source_blend_id=source_id,
        material_names=set(args.material) if args.material else None,
        allow_appearance_approximation=args.allow_appearance_approximation,
    )
    summary = {
        "schema": "miku-batch-result-1.0",
        "blend": str(source_path),
        "sourceBlendId": source_id,
        "materials": len(results),
        "materialNames": [item["materialKey"] for item in results],
        "completionMarker": "MIKU_CONVERSION_COMPLETE",
        "exitCode": 0,
    }
    (destination / "_miku-batch-result.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print("MIKU_CONVERSION_COMPLETE:" + json.dumps(summary, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
