"""Blender runtime smoke test for the Miku current-material frontend."""

from __future__ import annotations

import sys
import tempfile
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import miku_blender

if tuple(bpy.app.version) != (5, 2, 0):
    raise RuntimeError(
        f"MIKU_BLENDER_VERSION_MISMATCH:expected=(5, 2, 0):got={bpy.app.version}"
    )


def main() -> None:
    miku_blender.register()

    mesh = bpy.data.meshes.new("MikuFrontendSmokeMesh")
    obj = bpy.data.objects.new("MikuFrontendSmokeObject", mesh)
    bpy.context.scene.collection.objects.link(obj)
    first = bpy.data.materials.new("MikuFrontendFirst")
    active = bpy.data.materials.new("MikuFrontendActive")
    mesh.materials.append(first)
    mesh.materials.append(active)
    obj.active_material_index = 1
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)

    bpy.context.scene.miku_settings.default_workflow = "hsr_toon"
    active.miku_workflow = "inherit"
    active.miku_workflow_part = "Hair"

    selected, diagnostic = miku_blender._active_material_slot_state(bpy.context)
    assert selected is active, diagnostic
    assert (
        miku_blender._migrate_material_workflow(
            active,
            bpy.context.scene.miku_settings,
        )
        == "hsr_toon"
    )
    bpy.context.scene.miku_settings.default_workflow = "standard_pbr"
    assert (
        miku_blender._migrate_material_workflow(
            active,
            bpy.context.scene.miku_settings,
        )
        == "hsr_toon"
    )

    captured = {}
    original_export = miku_blender.export_material_bundle

    def fake_export(material, output_root, **kwargs):
        captured["material"] = material
        captured["outputRoot"] = output_root
        captured.update(kwargs)
        return {"materialKey": material.name}

    miku_blender.export_material_bundle = fake_export
    try:
        with tempfile.TemporaryDirectory() as temporary:
            result = miku_blender.export_current_material(
                bpy.context,
                temporary,
            )
            assert not (Path(temporary) / ".migr-identities.json").exists()
            duplicate = active.copy()
            identities, warnings = miku_blender._ensure_material_identities(
                list(bpy.data.materials),
                captured["source_blend_id"],
                Path(temporary),
                required_materials=[duplicate],
            )
            assert identities[duplicate.as_pointer()] != captured["persistent_material_id"]
            assert any(
                "MIKU_MATERIAL_ID_DUPLICATE_REPAIRED" in warning
                for warning in warnings
            )
    finally:
        miku_blender.export_material_bundle = original_export

    assert result["materialKey"] == active.name
    assert captured["material"] is active
    assert captured["workflow_kind"] == "hsr_toon"
    assert captured["workflow_part"] == "Hair"
    assert captured["persistent_material_id"]
    assert active.get("miku_material_id") == captured["persistent_material_id"]
    assert bpy.context.scene.get("miku_source_id")
    previous_source_id = bpy.context.scene["miku_source_id"]
    forked = miku_blender.fork_source_identity(
        bpy.data,
        current_scene=bpy.context.scene,
    )
    assert forked["persistentSourceId"] != previous_source_id
    assert forked["materialCount"] == len(bpy.data.materials)
    print("MIKU_CURRENT_MATERIAL_FRONTEND_SMOKE_OK")


if __name__ == "__main__":
    main()
