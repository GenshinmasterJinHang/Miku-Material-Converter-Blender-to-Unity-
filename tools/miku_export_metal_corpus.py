"""Run and certify the locked five-file, 73-material Blender corpus export."""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from miku.bundle import sha256_file, validate_bundle_document
from miku.contracts import canonical_hash, validate_document
from miku.planner import default_target_profile
import jsonschema


IDENTITIES = ROOT / "docs" / "provenance" / "miku-metal-corpus-identities.json"


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument(
        "--output-root",
        type=Path,
        default=ROOT / "outputs" / "metal_library",
    )
    parser.add_argument(
        "--blender",
        type=Path,
        default=Path(r"C:\SteamLibrary\steamapps\common\Blender\blender.exe"),
    )
    parser.add_argument("--blend", action="append", default=[])
    parser.add_argument("--material", action="append", default=[])
    parser.add_argument("--allow-appearance-approximation", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = _arguments()
    provenance = json.loads(IDENTITIES.read_text(encoding="utf-8"))
    selected = [
        item
        for item in provenance["sources"]
        if not args.blend or item["blendName"] in set(args.blend)
    ]
    if not selected:
        raise RuntimeError("MIKU_CORPUS_FILTER_EMPTY")
    if args.material and len(selected) != 1:
        raise RuntimeError("MIKU_MATERIAL_FILTER_REQUIRES_ONE_BLEND")
    args.output_root.mkdir(parents=True, exist_ok=True)
    for source in selected:
        blend = (args.source_root / Path(source["relativePath"])).resolve()
        if not blend.is_file():
            raise FileNotFoundError(blend)
        if sha256_file(blend) != source["sourceSha256"]:
            raise RuntimeError("MIKU_CORPUS_SOURCE_HASH_MISMATCH:" + source["blendName"])
        command = [
            str(args.blender),
            "--background",
            str(blend),
            "--python-exit-code",
            "13",
            "--python",
            str(ROOT / "tools" / "miku_blender_batch.py"),
            "--",
            str(args.output_root),
            "--source-id",
            source["persistentSourceId"],
        ]
        for material in args.material:
            command.extend(["--material", material])
        if args.allow_appearance_approximation:
            command.append("--allow-appearance-approximation")
        subprocess.run(command, cwd=ROOT, check=True)
        batch_result_path = args.output_root / source["blendName"] / "_miku-batch-result.json"
        if not batch_result_path.is_file():
            raise RuntimeError("MIKU_BATCH_RESULT_MISSING:" + source["blendName"])
        batch_result = json.loads(batch_result_path.read_text(encoding="utf-8"))
        expected_count = (
            len(args.material)
            if args.material
            else int(source["expectedMaterials"])
        )
        if (
            batch_result.get("completionMarker") != "MIKU_CONVERSION_COMPLETE"
            or batch_result.get("exitCode") != 0
            or int(batch_result.get("materials") or -1) != expected_count
        ):
            raise RuntimeError("MIKU_BATCH_RESULT_INVALID:" + source["blendName"])

    summary = certify_corpus(
        args.output_root,
        selected,
        require_complete=not args.blend and not args.material,
    )
    destination = args.output_root / (
        "_miku-corpus-complete.json"
        if str(summary["status"]).startswith("completed")
        else "_miku-corpus-partial.json"
    )
    destination.write_text(
        json.dumps(summary, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print("MIKU_CORPUS_CERTIFIED:" + json.dumps(summary, ensure_ascii=False))
    return 0


def certify_corpus(
    output_root: Path,
    selected_sources: list[dict[str, Any]],
    *,
    require_complete: bool,
) -> dict[str, Any]:
    expected = {item["blendName"]: int(item["expectedMaterials"]) for item in selected_sources}
    bundles = sorted(output_root.rglob("*.mikubundle"))
    records = []
    material_ids = set()
    actual_counts: dict[str, int] = {}
    approximation_count = 0
    expected_profile_hash = default_target_profile()["canonicalHash"]
    for bundle_path in bundles:
        blend_name = bundle_path.parents[1].name
        if blend_name not in expected:
            continue
        bundle = validate_bundle_document(
            json.loads(bundle_path.read_text(encoding="utf-8"))
        )
        material_id = str(bundle["persistentMaterialId"])
        if material_id in material_ids:
            raise RuntimeError(f"MIKU_CORPUS_MATERIAL_ID_DUPLICATE:{material_id}")
        material_ids.add(material_id)
        actual_counts[blend_name] = actual_counts.get(blend_name, 0) + 1
        root = bundle_path.parent
        for role in ("ir", "plan", "manifest", "sourceMap"):
            _verify_reference(root, bundle[role])
            document = json.loads(
                (root / bundle[role]["relativePath"]).read_text(encoding="utf-8")
            )
            validate_document(document)
            _validate_schema(document)
        manifest = json.loads(
            (root / bundle["manifest"]["relativePath"]).read_text(encoding="utf-8")
        )
        if (
            bundle.get("targetProfileHash") != expected_profile_hash
            or manifest.get("targetProfileHash") != expected_profile_hash
        ):
            raise RuntimeError(
                "MIKU_CORPUS_TARGET_PROFILE_MISMATCH:" + str(bundle_path)
            )
        if manifest["completion"]["status"] != "completed":
            raise RuntimeError("MIKU_CORPUS_MANIFEST_INCOMPLETE:" + str(bundle_path))
        if any(
            item.get("translationQuality") == "Approximate"
            for item in manifest.get("diagnostics") or []
            if isinstance(item, dict)
        ):
            approximation_count += 1
        artifact_ids = {
            item["id"] for item in manifest["completion"].get("artifacts") or []
        }
        resource_ids = {item["id"] for item in bundle.get("resources") or []}
        if not resource_ids or artifact_ids != resource_ids:
            raise RuntimeError("MIKU_CORPUS_ARTIFACT_SET_MISMATCH:" + str(bundle_path))
        for resource in bundle["resources"]:
            _verify_reference(root, resource)
            _verify_image_header(root / resource["relativePath"], resource)
        _validate_schema(bundle)
        records.append(
            {
                "blendName": blend_name,
                "persistentMaterialId": material_id,
                "bundleHash": bundle["canonicalHash"],
            }
        )
    if require_complete:
        if actual_counts != expected or len(records) != 73:
            raise RuntimeError(
                "MIKU_CORPUS_COUNT_MISMATCH:"
                + json.dumps(actual_counts, ensure_ascii=False, sort_keys=True)
            )
    elif any(actual_counts.get(name, 0) > count for name, count in expected.items()):
        raise RuntimeError("MIKU_CORPUS_PARTIAL_COUNT_OVERFLOW")
    if not records:
        raise RuntimeError("MIKU_CORPUS_EMPTY")
    records.sort(key=lambda item: (item["blendName"], item["persistentMaterialId"]))
    payload = {
        "schema": "miku-corpus-completion-1.0",
        "status": (
            "completed-with-approximations"
            if require_complete and approximation_count
            else ("completed" if require_complete else "partial")
        ),
        "materialCount": len(records),
        "approximationCount": approximation_count,
        "categoryCounts": actual_counts,
        "persistentMaterialIdsHash": canonical_hash(
            [item["persistentMaterialId"] for item in records]
        ),
        "bundleSetHash": canonical_hash(
            [item["bundleHash"] for item in records]
        ),
    }
    payload["assetTreeHash"] = _asset_tree_hash(output_root)
    return payload


def _verify_reference(root: Path, reference: dict[str, Any]) -> None:
    path = root / reference["relativePath"]
    if (
        not path.is_file()
        or path.stat().st_size != reference["byteLength"]
        or sha256_file(path) != reference["sha256"]
    ):
        raise RuntimeError("MIKU_CORPUS_ARTIFACT_MISMATCH:" + str(path))


def _asset_tree_hash(root: Path) -> str:
    digest = hashlib.sha256()
    for path in sorted(
        (
            item
            for item in root.rglob("*")
            if item.is_file() and not item.name.startswith("_miku-corpus-")
        ),
        key=lambda item: item.relative_to(root).as_posix(),
    ):
        relative = path.relative_to(root).as_posix()
        digest.update(relative.encode("utf-8"))
        digest.update(bytes.fromhex(sha256_file(path)))
    return digest.hexdigest()


def _validate_schema(document: dict[str, Any]) -> None:
    schema_name = str(document["documentKind"]) + ".schema.json"
    schema_path = ROOT / "schema" / schema_name
    if schema_path.is_file():
        jsonschema.validate(
            document,
            json.loads(schema_path.read_text(encoding="utf-8")),
        )


def _verify_image_header(path: Path, resource: dict[str, Any]) -> None:
    media_type = resource["mediaType"]
    if media_type == "image/png":
        header = path.read_bytes()[:24]
        if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
            raise RuntimeError("MIKU_PNG_HEADER_INVALID:" + str(path))
        width = int.from_bytes(header[16:20], "big")
        height = int.from_bytes(header[20:24], "big")
    elif media_type == "image/x-exr":
        width, height, pixel_types = _read_exr_header(path)
        if any(pixel_type != 1 for pixel_type in pixel_types):
            raise RuntimeError("MIKU_EXR_NOT_HALF_FLOAT:" + str(path))
    else:
        raise RuntimeError("MIKU_IMAGE_MEDIA_TYPE_INVALID:" + str(path))
    if width != resource["width"] or height != resource["height"]:
        raise RuntimeError("MIKU_IMAGE_DIMENSION_MISMATCH:" + str(path))


def _read_exr_header(path: Path) -> tuple[int, int, list[int]]:
    import struct

    with path.open("rb") as stream:
        if stream.read(4) != b"\x76\x2f\x31\x01":
            raise RuntimeError("MIKU_EXR_HEADER_INVALID:" + str(path))
        stream.read(4)
        attributes = {}
        while True:
            name = _read_zero_string(stream)
            if not name:
                break
            kind = _read_zero_string(stream)
            size = struct.unpack("<I", stream.read(4))[0]
            attributes[name] = (kind, stream.read(size))
    window = attributes.get("dataWindow")
    channels = attributes.get("channels")
    if window is None or channels is None:
        raise RuntimeError("MIKU_EXR_ATTRIBUTE_MISSING:" + str(path))
    x_min, y_min, x_max, y_max = struct.unpack("<iiii", window[1])
    data = channels[1]
    offset = 0
    pixel_types = []
    while offset < len(data) and data[offset] != 0:
        end = data.index(0, offset)
        offset = end + 1
        pixel_types.append(struct.unpack_from("<i", data, offset)[0])
        offset += 16
    return x_max - x_min + 1, y_max - y_min + 1, pixel_types


def _read_zero_string(stream: Any) -> str:
    value = bytearray()
    while True:
        byte = stream.read(1)
        if byte == b"\0":
            return value.decode("ascii")
        if not byte:
            raise RuntimeError("MIKU_EXR_HEADER_TRUNCATED")
        value.extend(byte)


if __name__ == "__main__":
    raise SystemExit(main())
