"""Bake one real raw Height channel as a Linear half-float EXR."""

from __future__ import annotations

import importlib.util
import pathlib
import shutil
import sys
import tempfile
from types import SimpleNamespace

import bpy


ROOT = pathlib.Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import miku_blender  # noqa: E402
from miku_blender.versioning import require_blender_capabilities  # noqa: E402
from miku.planner import ConversionPlanner  # noqa: E402
from miku.semantic import build_material_ir  # noqa: E402


def load_worker():
    package_root = (
        ROOT / "extensions" / "miku_shader_converter" / "bake_worker"
    )
    spec = importlib.util.spec_from_file_location(
        "_miku_height_bake_smoke_worker",
        package_root / "__init__.py",
        submodule_search_locations=[str(package_root)],
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("MIKU_CANONICAL_BAKE_WORKER_LOAD_FAILED")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def main() -> None:
    require_blender_capabilities(bpy)
    miku_blender.register()
    material = bpy.data.materials.new("Miku Raw Height Bake")
    material.use_nodes = True
    material.miku_displacement_policy = "ALWAYS_VERTEX"
    material.displacement_method = "BUMP"
    tree = material.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    principled = tree.nodes.new("ShaderNodeBsdfPrincipled")
    bump = tree.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.25
    bump.inputs["Distance"].default_value = 0.2
    voronoi = tree.nodes.new("ShaderNodeTexVoronoi")
    coordinates = tree.nodes.new("ShaderNodeTexCoord")
    tree.links.new(coordinates.outputs["Generated"], voronoi.inputs["Vector"])
    tree.links.new(voronoi.outputs["Distance"], bump.inputs["Height"])
    tree.links.new(bump.outputs["Normal"], principled.inputs["Normal"])
    tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])

    bpy.ops.mesh.primitive_grid_add(x_subdivisions=8, y_subdivisions=8)
    mesh_object = bpy.context.active_object
    mesh_object.name = "MikuRawHeightGrid"
    mesh_object.data.materials.append(material)

    graph = miku_blender.snapshot_material(material)
    ir = build_material_ir(
        graph,
        source_blend_id="height-bake-smoke",
        material_key=material.name,
        conversion_mode="AllowMeshBake",
    )
    plan = ConversionPlanner().plan(ir, mode="AllowMeshBake")
    height_jobs = [
        item
        for item in plan["bakeJobs"]
        if "Height" in item.get("semantics", [])
    ]
    if len(height_jobs) != 1:
        raise RuntimeError(
            "MIKU_HEIGHT_BAKE_JOB_COUNT_INVALID:" + repr(height_jobs)
        )
    if abs(float(ir["heightChannel"]["scale"]) - 0.05) > 1.0e-6:
        raise RuntimeError("MIKU_BUMP_HEIGHT_SCALE_NOT_PRESERVED")

    worker = load_worker()
    output_root = pathlib.Path(tempfile.mkdtemp(prefix="miku-height-bake-"))
    try:
        settings = SimpleNamespace(
            bake_resolution="32",
            bake_samples=1,
            bake_margin=2,
            auto_generate_uv=False,
        )
        kwargs = dict(
            context=bpy.context,
            objects=[mesh_object],
            material=material,
            graph=graph,
            miku_path=str(output_root / "fixture.miku-bake-source"),
            settings=settings,
            allow_appearance_approximation=False,
            channel_specs=(
                ("Height", "Height", "R", "Linear"),
            ),
            channel_scoped=True,
        )
        first = worker.bake_material(**kwargs)
        if first["status"] != "completed":
            raise RuntimeError("MIKU_HEIGHT_BAKE_FAILED:" + repr(first))
        height = first["channels"]["Height"]
        if (
            height["mediaType"] != "image/x-exr"
            or height["componentBytes"] != 2
            or height["channel"] != "R"
            or height["colorSpace"] != "Linear"
        ):
            raise RuntimeError("MIKU_HEIGHT_EXR_CONTRACT_INVALID:" + repr(height))
        height_path = output_root / height["relativePath"]
        first_bytes = height_path.read_bytes()
        image = bpy.data.images.load(str(height_path), check_existing=False)
        try:
            if not image.is_float:
                raise RuntimeError("MIKU_HEIGHT_EXR_NOT_FLOAT")
            values = list(image.pixels)[0::4]
            if not values or min(values) < -1.0e-5 or max(values) <= 0.05:
                raise RuntimeError(
                    "MIKU_HEIGHT_RAW_RANGE_INVALID:"
                    f"{min(values) if values else None}:"
                    f"{max(values) if values else None}"
                )
        finally:
            bpy.data.images.remove(image)
        second = worker.bake_material(**kwargs)
        if second["status"] != "reused":
            raise RuntimeError("MIKU_HEIGHT_CACHE_NOT_REUSED:" + repr(second))
        if height_path.read_bytes() != first_bytes:
            raise RuntimeError("MIKU_HEIGHT_BAKE_NOT_BYTE_STABLE")
        print("MIKU_HEIGHT_BAKE_SMOKE_OK")
    finally:
        shutil.rmtree(output_root, ignore_errors=True)


if __name__ == "__main__":
    main()
