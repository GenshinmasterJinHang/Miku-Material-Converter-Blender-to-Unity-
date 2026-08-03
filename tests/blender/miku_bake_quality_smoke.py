"""Execute one real 512px end-to-end Blender bake quality export."""

from __future__ import annotations

import importlib.util
import json
import shutil
import sys
import tempfile
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import miku_blender  # noqa: E402


def _load_canonical_bake_worker():
    package_root = ROOT / "extensions" / "miku_shader_converter" / "bake_worker"
    spec = importlib.util.spec_from_file_location(
        "_miku_bake_quality_smoke_worker",
        package_root / "__init__.py",
        submodule_search_locations=[str(package_root)],
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("MIKU_CANONICAL_BAKE_WORKER_LOAD_FAILED")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def _create_material_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.mesh.primitive_plane_add(size=2.0)
    obj = bpy.context.object
    material = bpy.data.materials.new("Miku Bake Quality")
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    principled = tree.nodes.new("ShaderNodeBsdfPrincipled")
    noise = tree.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 4.0
    tree.links.new(noise.outputs["Color"], principled.inputs["Base Color"])
    tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    obj.data.materials.append(material)
    return material


def assert_bake_quality() -> None:
    assert tuple(bpy.app.version) == (5, 2, 0), tuple(bpy.app.version)
    material = _create_material_scene()
    worker = _load_canonical_bake_worker()
    output_root = Path(tempfile.mkdtemp(prefix="miku-bake-quality-"))
    worker.register()
    try:
        result = miku_blender.export_material_bundle(
            material,
            str(output_root),
            source_blend_id="miku-bake-quality-source",
            persistent_material_id="miku-bake-quality-material",
            mode="FullPBRBake",
            bake_resolution=512,
        )
        bundle_path = Path(result["bundlePath"])
        bundle_directory = bundle_path.parent
        plan_path = next(bundle_directory.glob("*.miku-plan.json"))
        request_path = next(bundle_directory.glob("*.miku-bake-request.json"))
        plan = json.loads(plan_path.read_text(encoding="utf-8"))
        request = json.loads(request_path.read_text(encoding="utf-8"))
        assert request["documentKind"] == "miku-bake-request-1.1", request
        assert request["settings"]["resolution"] == 512, request["settings"]
        assert plan["bakeJobs"], plan
        assert all(job["resolution"] == 512 for job in plan["bakeJobs"]), plan["bakeJobs"]
        images = [
            item
            for item in result["bundle"]["resources"]
            if str(item.get("mediaType") or "").startswith("image/")
        ]
        assert images, result["bundle"]["resources"]
        assert all(item.get("width") == 512 and item.get("height") == 512 for item in images), (
            images
        )
    finally:
        worker.unregister()
        shutil.rmtree(output_root, ignore_errors=True)


if __name__ == "__main__":
    assert_bake_quality()
    print("MIKU_BAKE_QUALITY_SMOKE_OK")
