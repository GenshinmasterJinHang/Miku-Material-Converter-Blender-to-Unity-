"""Execute real 64px expression-island bakes on the locked corpus."""

from __future__ import annotations

import importlib.util
import os
import pathlib
import shutil
import sys
import tempfile
from types import SimpleNamespace

import bpy


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
MATERIAL_LIBRARY_ROOT = pathlib.Path(
    os.environ.get("MIKU_MATERIAL_LIBRARY_ROOT")
    or REPOSITORY_ROOT / "材质库"
)
CORPUS_PATH = (
    MATERIAL_LIBRARY_ROOT / "石头" / "彩色镀层" / "彩色镀层.blend"
)

if str(REPOSITORY_ROOT) not in sys.path:
    sys.path.insert(0, str(REPOSITORY_ROOT))

import miku_blender  # noqa: E402
from miku.planner import ConversionPlanner  # noqa: E402
from miku.semantic import build_material_ir  # noqa: E402


def _load_canonical_bake_worker():
    package_root = (
        REPOSITORY_ROOT
        / "extensions"
        / "miku_shader_converter"
        / "bake_worker"
    )
    spec = importlib.util.spec_from_file_location(
        "_miku_expression_island_smoke_bake_worker",
        package_root / "__init__.py",
        submodule_search_locations=[str(package_root)],
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("MIKU_CANONICAL_BAKE_WORKER_LOAD_FAILED")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


bake_expression_islands = _load_canonical_bake_worker().bake_expression_islands


def main() -> None:
    if tuple(bpy.app.version) != (5, 2, 0):
        raise RuntimeError("MIKU_BLENDER_VERSION_MISMATCH")
    bpy.ops.wm.open_mainfile(filepath=str(CORPUS_PATH))
    output_root = pathlib.Path(tempfile.mkdtemp(prefix="miku-island-smoke-"))
    try:
        for material_name in ("彩色镀层5", "彩色镀层8", "彩色镀层9"):
            material = bpy.data.materials[material_name]
            graph = miku_blender.snapshot_material(material)
            material_ir = build_material_ir(
                graph,
                source_blend_id="colorful-coating-smoke",
                material_key=material_name,
            )
            jobs = [
                item
                for item in ConversionPlanner().plan(material_ir)["bakeJobs"]
                if item.get("scope") == "ExpressionIsland"
            ]
            result = bake_expression_islands(
                bpy.context,
                list(bpy.data.objects),
                material,
                graph,
                jobs,
                str(output_root / "fixture.miku-bake-source"),
                SimpleNamespace(
                    bake_resolution="64",
                    bake_samples=1,
                    bake_margin=2,
                    auto_generate_uv=False,
                ),
            )
            if result["status"] != "completed":
                raise RuntimeError(str(result))
            if len(result["islands"]) != len(jobs):
                raise RuntimeError(
                    "MIKU_EXPRESSION_ISLAND_RESOURCE_COUNT_MISMATCH"
                )
            for island in result["islands"].values():
                path = output_root / island["relativePath"]
                if not path.is_file() or path.stat().st_size <= 0:
                    raise RuntimeError(
                        "MIKU_EXPRESSION_ISLAND_RESOURCE_MISSING"
                    )
        print("MIKU_EXPRESSION_ISLAND_BAKE_SMOKE_COMPLETE")
    finally:
        shutil.rmtree(output_root, ignore_errors=True)


if __name__ == "__main__":
    main()
