"""Export the complete locked corpus through the installed 1.1.1 extensions."""

from __future__ import annotations

import importlib
import atexit
import hashlib
import json
import os
import pathlib
import shutil
import tempfile

import bpy


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
OUTPUT_ROOT = pathlib.Path(tempfile.gettempdir()) / (
    "miku-colorful-coating-" + str(os.getpid())
)
atexit.register(shutil.rmtree, OUTPUT_ROOT, ignore_errors=True)
SOURCE_ID = (
    "b02d5d317af2787023a71993d90ceaceb2066917637338fefd95157f9abd7942"
)
MATERIALS = {
    "Dots Stroke",
    *(f"彩色镀层{index}" for index in range(1, 11)),
    "星点光",
    "河岩",
    "玻璃雾岩",
}


def _hash_tree(root: pathlib.Path) -> dict[str, str]:
    return {
        path.relative_to(root).as_posix(): hashlib.sha256(
            path.read_bytes()
        ).hexdigest()
        for path in sorted(root.rglob("*"))
        if path.is_file()
    }


def _export(exporter):
    return exporter.export_selected_materials(
        str(OUTPUT_ROOT),
        mode="Auto",
        source_blend_id=SOURCE_ID,
        material_names=MATERIALS,
        default_workflow="standard_pbr",
        allow_appearance_approximation=False,
    )


def main() -> None:
    if tuple(bpy.app.version) != (5, 2, 0):
        raise RuntimeError("MIKU_BLENDER_VERSION_MISMATCH")
    exporter = importlib.import_module(
        "bl_ext.user_default.miku_shader_converter.miku_blender"
    )
    exporter.register()
    results = _export(exporter)
    if len(results) != 14:
        raise RuntimeError(
            f"MIKU_COLORFUL_COATING_MATERIAL_COUNT:{len(results)}"
        )
    summaries = []
    for result in results:
        bundle_path = pathlib.Path(result["bundlePath"])
        bundle = json.loads(bundle_path.read_text(encoding="utf-8"))
        ir_path = bundle_path.parent / bundle["ir"]["relativePath"]
        plan_path = bundle_path.parent / bundle["plan"]["relativePath"]
        material_ir = json.loads(ir_path.read_text(encoding="utf-8"))
        plan = json.loads(plan_path.read_text(encoding="utf-8"))
        errors = [
            item
            for item in plan.get("diagnostics", [])
            if str(item.get("severity") or "").lower() == "error"
        ]
        if errors:
            raise RuntimeError(
                f"MIKU_COLORFUL_COATING_PLAN_ERROR:"
                f"{result['materialKey']}:{errors}"
            )
        if any(
            item.get("code") == "MIKU_REQUIRED_CHANNEL_UNRESOLVED"
            for item in plan.get("diagnostics", [])
        ):
            raise RuntimeError("MIKU_REQUIRED_CHANNEL_UNRESOLVED")
        summaries.append(
            {
                "material": result["materialKey"],
                "bundle": str(bundle_path),
                "resources": len(material_ir.get("resources", [])),
                "jobs": [
                    item.get("scope", "Region")
                    for item in plan.get("bakeJobs", [])
                ],
                "targetProfileHash": bundle["targetProfileHash"],
            }
        )
    first_hashes = _hash_tree(OUTPUT_ROOT)
    repeated_results = _export(exporter)
    if len(repeated_results) != 14:
        raise RuntimeError(
            "MIKU_COLORFUL_COATING_REPEAT_MATERIAL_COUNT:"
            f"{len(repeated_results)}"
        )
    second_hashes = _hash_tree(OUTPUT_ROOT)
    if first_hashes != second_hashes:
        changed = sorted(
            set(first_hashes) ^ set(second_hashes)
            | {
                path
                for path in set(first_hashes) & set(second_hashes)
                if first_hashes[path] != second_hashes[path]
            }
        )
        raise RuntimeError(
            "MIKU_COLORFUL_COATING_NONDETERMINISTIC:"
            + json.dumps(changed, ensure_ascii=False)
        )
    print(
        "MIKU_INSTALLED_COLORFUL_COATING_EXPORT_COMPLETE:"
        + json.dumps(
            {
                "materials": summaries,
                "deterministicFiles": len(second_hashes),
            },
            ensure_ascii=False,
            sort_keys=True,
        )
    )


if __name__ == "__main__":
    main()
