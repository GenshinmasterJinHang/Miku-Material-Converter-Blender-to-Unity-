"""Blender runtime smoke for the installed Miku shared-output implementation."""

from __future__ import annotations

import importlib
import json
import tempfile
from pathlib import Path

import bpy

if tuple(bpy.app.version) != (5, 2, 0):
    raise RuntimeError(
        f"MIKU_BLENDER_VERSION_MISMATCH:expected=(5, 2, 0):got={bpy.app.version}"
    )


def main() -> None:
    extension = importlib.import_module(
        "bl_ext.user_default.miku_shader_converter"
    )
    assert callable(extension.register)
    assert callable(extension.unregister)
    exporter = importlib.import_module(
        "bl_ext.user_default.miku_shader_converter.miku_blender"
    )
    exporter.register()

    material = bpy.data.materials.new("Shared Rock")
    original_directory_export = exporter._export_material_bundle_to_directory

    def write_identity_bundle(
        source_material,
        target,
        *,
        source_blend_id,
        persistent_material_id,
        **_kwargs,
    ):
        asset_name = exporter._safe_asset_name(source_material.name)
        target.mkdir(parents=True, exist_ok=True)
        bundle_path = target / f"{asset_name}.mikubundle"
        bundle_path.write_text(
            json.dumps(
                {
                    "documentKind": "miku-bundle-1.0",
                    "persistentSourceId": source_blend_id,
                    "persistentMaterialId": persistent_material_id,
                }
            ),
            encoding="utf-8",
        )
        return {
            "materialKey": source_material.name,
            "bundleFileName": bundle_path.name,
        }

    exporter._export_material_bundle_to_directory = write_identity_bundle
    with tempfile.TemporaryDirectory() as temporary:
        try:
            root = Path(temporary)
            first = exporter.export_material_bundle(
                material,
                str(root),
                source_blend_id="source-a",
                persistent_material_id="11111111-1111-4111-8111-111111111111",
            )
            second = exporter.export_material_bundle(
                material,
                str(root),
                source_blend_id="source-b",
                persistent_material_id="22222222-2222-4222-8222-222222222222",
            )
            first_directory = Path(first["bundlePath"]).parent
            second_directory = Path(second["bundlePath"]).parent
            assert first_directory.name == "Shared Rock__111111111111"
            assert second_directory.name == "Shared Rock__222222222222"
            assert first_directory != second_directory
            assert not (root / ".migr-identities.json").exists()

            material.name = "Renamed Rock"
            renamed = exporter.export_material_bundle(
                material,
                str(root),
                source_blend_id="source-a",
                persistent_material_id="11111111-1111-4111-8111-111111111111",
            )
            assert Path(renamed["bundlePath"]).parent == first_directory
            bundle = json.loads(Path(renamed["bundlePath"]).read_text(encoding="utf-8"))
            assert bundle["persistentSourceId"] == "source-a"
            assert bundle["persistentMaterialId"] == (
                "11111111-1111-4111-8111-111111111111"
            )
        finally:
            exporter._export_material_bundle_to_directory = original_directory_export

    print("MIKU_INSTALLED_EXTENSION_IDENTITY_SMOKE_OK")


if __name__ == "__main__":
    main()
