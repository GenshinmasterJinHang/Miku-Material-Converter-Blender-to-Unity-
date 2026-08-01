"""Blender 5.2 smoke coverage for mixed and direct Glass BSDF surfaces."""

from __future__ import annotations

import importlib.util
import os
import tempfile
import sys
import uuid
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import miku_blender  # noqa: E402


def _load_canonical_bake_worker():
    package_root = (
        ROOT / "extensions" / "miku_shader_converter" / "bake_worker"
    )
    spec = importlib.util.spec_from_file_location(
        "_miku_glass_smoke_bake_worker",
        package_root / "__init__.py",
        submodule_search_locations=[str(package_root)],
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("MIKU_CANONICAL_BAKE_WORKER_LOAD_FAILED")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


bake_worker = _load_canonical_bake_worker()
from miku.planner import ConversionPlanner  # noqa: E402
from miku.semantic import build_material_ir  # noqa: E402


MATERIAL_LIBRARY_ROOT = Path(
    os.environ.get("MIKU_MATERIAL_LIBRARY_ROOT") or ROOT / "材质库"
)
SOURCE = MATERIAL_LIBRARY_ROOT / "玻璃" / "玻璃.blend"


def material_ir(material: bpy.types.Material, key: str) -> dict:
    return build_material_ir(
        miku_blender.snapshot_material(material),
        source_blend_id="glass-surface-corpus",
        material_key=key,
    )


def assert_dielectric(document: dict) -> None:
    assert (
        document["surfaceContract"]["model"]
        == "DielectricScreenRefraction"
    ), document["surfaceContract"]
    channels = {
        item["semantic"]: item for item in document["channels"]
    }
    assert channels["TransmissionWeight"]["default"] == 1.0, channels[
        "TransmissionWeight"
    ]
    assert channels["IOR"]["default"] == 1.5, channels["IOR"]
    errors = [
        item
        for item in document.get("diagnostics", [])
        if str(item.get("severity") or "").lower() == "error"
    ]
    assert not errors, errors
    plan = ConversionPlanner().plan(document)
    assert not any(
        item["route"] == "Unsupported" for item in plan["regions"]
    ), plan


def connect_direct_glass(material: bpy.types.Material) -> None:
    tree = material.node_tree
    output = next(
        node
        for node in tree.nodes
        if node.bl_idname == "ShaderNodeOutputMaterial"
        and node.is_active_output
    )
    glass = next(
        node
        for node in tree.nodes
        if node.bl_idname == "ShaderNodeBsdfGlass"
    )
    surface = output.inputs["Surface"]
    for link in tuple(surface.links):
        tree.links.remove(link)
    tree.links.new(glass.outputs[0], surface)


def assert_glass_surface() -> None:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    assert tuple(bpy.app.version) == (5, 2, 0), tuple(bpy.app.version)
    material = bpy.data.materials["Material"]

    mixed = material_ir(material, "GlassFacingMix")
    assert_dielectric(mixed)
    assert any(
        item["kind"] == "SurfaceMix" for item in mixed["regions"]
    ), mixed["regions"]

    bake_worker.register()
    try:
        with tempfile.TemporaryDirectory(
            prefix="miku-glass-surface-1.2.1-"
        ) as output:
            mixed_export = miku_blender.export_material_bundle(
                material,
                output,
                source_blend_id="glass-surface-mixed",
                persistent_material_id=str(
                    uuid.uuid5(
                        uuid.NAMESPACE_URL,
                        "miku-glass-surface:mixed",
                    )
                ),
                allow_appearance_approximation=False,
            )
            assert Path(mixed_export["bundlePath"]).is_file(), mixed_export

            connect_direct_glass(material)
            direct = material_ir(material, "DirectGlass")
            assert_dielectric(direct)
            glass_region = next(
                item
                for item in direct["regions"]
                if item["kind"] == "GlassClosure"
            )
            region_plan = next(
                item
                for item in ConversionPlanner().plan(direct)["regions"]
                if item["regionId"] == glass_region["id"]
            )
            assert region_plan["route"] == "Native", region_plan
            assert region_plan["fidelity"] == "Approximate", region_plan
            direct_export = miku_blender.export_material_bundle(
                material,
                output,
                source_blend_id="glass-surface-direct",
                persistent_material_id=str(
                    uuid.uuid5(
                        uuid.NAMESPACE_URL,
                        "miku-glass-surface:direct",
                    )
                ),
                allow_appearance_approximation=False,
            )
            assert Path(direct_export["bundlePath"]).is_file(), direct_export
    finally:
        bake_worker.unregister()


if __name__ == "__main__":
    assert_glass_surface()
    print("MIKU_GLASS_SURFACE_SMOKE_OK")
