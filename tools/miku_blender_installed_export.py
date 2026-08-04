"""Blender 5.2 acceptance driver that installs release ZIPs before export."""

from __future__ import annotations

import argparse
import hashlib
import importlib
import json
import sys
import uuid
import zipfile
from pathlib import Path

import bpy


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--extension-zip", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--source-id", default="")
    parser.add_argument("--material", action="append", default=[])
    parser.add_argument("--direct-material", action="append", default=[])
    parser.add_argument("--mode", default="Auto")
    parser.add_argument(
        "--workflow",
        default="standard_pbr",
        choices=(
            "standard_pbr",
            "genshin_toon",
            "wuwa_toon",
            "hsr_toon",
        ),
    )
    parser.add_argument("--allow-appearance-approximation", action="store_true")
    return parser.parse_args(
        sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    )


def install(path: Path) -> None:
    result = bpy.ops.extensions.package_install_files(
        filepath=str(path.resolve()),
        repo="user_default",
        enable_on_install=True,
        overwrite=True,
    )
    if "FINISHED" not in set(result):
        raise RuntimeError(f"MIKU_EXTENSION_INSTALL_FAILED:{path.name}:{result}")


def verify_installed_extension(
    module_name: str,
    extension_id: str,
    archive_path: Path,
    expected_version: str,
) -> dict[str, str]:
    module = importlib.import_module(module_name)
    root = Path(module.__file__).resolve().parent
    normalized_root = root.as_posix()
    expected_suffix = f"/user_default/{extension_id}"
    if (
        module.__name__ != module_name
        or not normalized_root.endswith(expected_suffix)
    ):
        raise RuntimeError(
            "MIKU_INSTALLED_MODULE_PATH_MISMATCH:"
            f"expectedSuffix={expected_suffix}:got={normalized_root}"
        )
    manifest = (root / "blender_manifest.toml").read_text(encoding="utf-8")
    if f'version = "{expected_version}"' not in manifest:
        raise RuntimeError(
            f"MIKU_INSTALLED_EXTENSION_VERSION_MISMATCH:{extension_id}"
        )
    tree = hashlib.sha256()
    with zipfile.ZipFile(archive_path.resolve(), "r") as archive:
        for info in sorted(archive.infolist(), key=lambda item: item.filename):
            if info.is_dir():
                continue
            relative = info.filename.replace("\\", "/")
            installed = root / Path(relative)
            if not installed.is_file():
                raise RuntimeError(
                    f"MIKU_INSTALLED_EXTENSION_FILE_MISSING:{extension_id}:{relative}"
                )
            archive_bytes = archive.read(info)
            installed_bytes = installed.read_bytes()
            if installed_bytes != archive_bytes:
                raise RuntimeError(
                    f"MIKU_INSTALLED_EXTENSION_HASH_MISMATCH:{extension_id}:{relative}"
                )
            tree.update(relative.encode("utf-8"))
            tree.update(installed_bytes)
    return {
        "extensionId": extension_id,
        "version": expected_version,
        "moduleRoot": str(root),
        "archiveSha256": hashlib.sha256(
            archive_path.resolve().read_bytes()
        ).hexdigest(),
        "installedTreeSha256": tree.hexdigest(),
    }


def main() -> int:
    args = arguments()
    repo = Path(__file__).resolve().parents[1]
    if str(repo) not in sys.path:
        sys.path.insert(0, str(repo))
    from tools.miku_environment import (
        assert_bpy_version,
        validate_blender_executable,
    )

    validate_blender_executable(Path(bpy.app.binary_path))
    assert_bpy_version(bpy)
    install(args.extension_zip)
    installed = [
        verify_installed_extension(
            "bl_ext.user_default.miku_shader_converter",
            "miku_shader_converter",
            args.extension_zip,
            "2.2.8",
        )
    ]
    exporter = importlib.import_module(
        "bl_ext.user_default.miku_shader_converter.miku_blender"
    )
    results = exporter.export_selected_materials(
        str(args.output_root.resolve()),
        mode=args.mode,
        source_blend_id=args.source_id,
        material_names=set(args.material) if args.material else None,
        default_workflow=args.workflow,
        allow_appearance_approximation=args.allow_appearance_approximation,
    )
    for material_name in args.direct_material:
        if any(
            str(item.get("materialKey") or "") == material_name
            for item in results
        ):
            continue
        material = bpy.data.materials.get(material_name)
        if material is None:
            raise RuntimeError(
                f"MIKU_DIRECT_MATERIAL_MISSING:{material_name}"
            )
        results.append(
            exporter.export_material_bundle(
                material,
                str(args.output_root.resolve()),
                source_blend_id=(
                    args.source_id or "miku-installed-direct-export"
                ),
                persistent_material_id=str(
                    uuid.uuid5(
                        uuid.NAMESPACE_URL,
                        "miku-installed-direct:" + material_name,
                    )
                ),
                mode=args.mode,
                workflow_kind=args.workflow,
                allow_appearance_approximation=(
                    args.allow_appearance_approximation
                ),
            )
        )
    summary = {
        "schema": "miku-installed-extension-export-result-1.0",
        "materials": len(results),
        "bundlePaths": [item["bundlePath"] for item in results],
        "installedExtensions": installed,
    }
    print("MIKU_INSTALLED_EXPORT_COMPLETE:" + json.dumps(summary, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
