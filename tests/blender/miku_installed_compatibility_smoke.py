"""Install the release ZIP and exercise its public Blender 5.0-5.2 path."""

from __future__ import annotations

import argparse
import hashlib
import importlib
import json
import sys
import tempfile
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from miku.contracts import canonical_json  # noqa: E402
from tools.miku_blender_installed_export import (  # noqa: E402
    install,
    verify_installed_extension,
)


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--extension-zip", type=Path, required=True)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    return parser.parse_args(
        sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    )


def _material(name: str, *, noise: bool):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    principled = tree.nodes.new("ShaderNodeBsdfPrincipled")
    principled.inputs["Base Color"].default_value = (0.2, 0.4, 0.8, 1.0)
    principled.inputs["Roughness"].default_value = 0.35
    if noise:
        texture = tree.nodes.new("ShaderNodeTexNoise")
        texture.inputs["Scale"].default_value = 3.0
        tree.links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material


def _tree_hashes(root: Path) -> dict[str, str]:
    return {
        path.relative_to(root).as_posix(): hashlib.sha256(
            path.read_bytes()
        ).hexdigest()
        for path in sorted(root.rglob("*"))
        if path.is_file()
    }


def _normalized_ir_hash(bundle_directory: Path) -> str:
    path = next(bundle_directory.glob("*.miku-ir.json"))
    value = json.loads(path.read_text(encoding="utf-8"))
    semantic = {
        key: value.get(key)
        for key in (
            "workflow",
            "channels",
            "expressions",
            "parameters",
            "surfaceContract",
            "surfaceModelPlan",
            "normalConvention",
            "displacementMethod",
            "displacementPolicy",
            "heightChannel",
        )
        if key in value
    }
    return hashlib.sha256(canonical_json(semantic).encode("utf-8")).hexdigest()


def main() -> int:
    args = arguments()
    actual = ".".join(str(part) for part in tuple(bpy.app.version))
    if actual != args.expected_version:
        raise RuntimeError(
            "MIKU_BLENDER_VERSION_MISMATCH:"
            f"expected={args.expected_version}:got={actual}"
        )
    bpy.ops.wm.read_factory_settings(use_empty=True)
    install(args.extension_zip)
    installed = verify_installed_extension(
        "bl_ext.user_default.miku_shader_converter",
        "miku_shader_converter",
        args.extension_zip,
        "3.0.0",
    )
    extension = importlib.import_module(
        "bl_ext.user_default.miku_shader_converter"
    )
    exporter = importlib.import_module(
        "bl_ext.user_default.miku_shader_converter.miku_blender"
    )
    extension.register()
    try:
        if not hasattr(bpy.context.scene, "miku_settings"):
            raise RuntimeError("MIKU_INSTALLED_EXTENSION_UI_REGISTRATION_FAILED")
        material = _material("Miku Matrix Standard PBR", noise=False)
        noisy = _material("Miku Matrix Bake Worker", noise=True)
        bpy.ops.mesh.primitive_plane_add(size=2.0)
        bpy.context.object.data.materials.append(noisy)
        with tempfile.TemporaryDirectory(
            prefix="miku-installed-compatibility-"
        ) as temporary:
            root = Path(temporary)
            targa = bpy.data.images.new(
                "Miku Matrix Targa",
                width=2,
                height=2,
                alpha=True,
            )
            targa.pixels = [0.2, 0.4, 0.8, 1.0] * 4
            targa.filepath_raw = str(root / "matrix.tga")
            targa.file_format = "TARGA"
            targa.save()
            transcoded = exporter._fixed_targa_png_bytes(
                targa,
                "miku-matrix-targa",
            )
            if not transcoded.startswith(b"\x89PNG\r\n\x1a\n"):
                raise RuntimeError("MIKU_BLENDER_MATRIX_TARGA_TRANSCODE_FAILED")
            common = {
                "source_blend_id": "miku-blender-matrix-source",
                "persistent_material_id": (
                    "8d8d3018-d289-510d-ad5f-6cecfcb23840"
                ),
                "mode": "Auto",
            }
            first = exporter.export_material_bundle(
                material,
                str(root / "first"),
                **common,
            )
            second = exporter.export_material_bundle(
                material,
                str(root / "second"),
                **common,
            )
            first_directory = Path(first["bundlePath"]).parent
            second_directory = Path(second["bundlePath"]).parent
            first_hashes = _tree_hashes(first_directory)
            second_hashes = _tree_hashes(second_directory)
            if first_hashes != second_hashes:
                raise RuntimeError("MIKU_BLENDER_MATRIX_NONDETERMINISTIC")
            baked = exporter.export_material_bundle(
                noisy,
                str(root / "baked"),
                source_blend_id="miku-blender-matrix-source",
                persistent_material_id=(
                    "ecf312fd-f823-5d8f-8ea8-ebca7570db6f"
                ),
                mode="FullPBRBake",
                bake_resolution=512,
            )
            baked_directory = Path(baked["bundlePath"]).parent
            if not list(baked_directory.glob("Baked/*")):
                raise RuntimeError("MIKU_BLENDER_MATRIX_BAKE_WORKER_EMPTY")
            evidence = {
                "schema": "miku-blender-compatibility-evidence-1.0",
                "blender": actual,
                "miku": "3.0.0",
                "archiveSha256": installed["archiveSha256"],
                "installedTreeSha256": installed["installedTreeSha256"],
                "normalizedIrSha256": _normalized_ir_hash(first_directory),
                "deterministicFileCount": len(first_hashes),
                "bakeWorkerArtifacts": len(
                    [path for path in baked_directory.rglob("*") if path.is_file()]
                ),
                "uiRegistered": True,
                "standardPbrExported": True,
                "bakeWorkerExecuted": True,
                "targaTranscodeBytes": len(transcoded),
            }
            args.evidence.parent.mkdir(parents=True, exist_ok=True)
            args.evidence.write_text(
                json.dumps(evidence, ensure_ascii=False, indent=2) + "\n",
                encoding="utf-8",
            )
    finally:
        extension.unregister()
    print("MIKU_INSTALLED_COMPATIBILITY_SMOKE_OK:" + actual)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
