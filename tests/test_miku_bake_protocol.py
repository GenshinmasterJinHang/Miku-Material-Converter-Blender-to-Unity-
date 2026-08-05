import json
import sys
import unittest
import zipfile
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace
from unittest.mock import patch

import jsonschema

from miku.bake_protocol import (
    BAKE_SETTINGS,
    DEFAULT_BAKE_RESOLUTION,
    SUPPORTED_BAKE_RESOLUTIONS,
    make_bake_request,
    make_bake_result,
    normalize_bake_blender_commit,
    normalize_bake_blender_version,
    normalize_bake_resolution,
    validate_bake_runtime_binding,
    validate_bake_request,
    validate_bake_result,
)
from miku.contracts import make_document
from miku_blender.bake_client import execute_bake


ROOT = Path(__file__).resolve().parents[1]


class MikuBakeProtocolTests(unittest.TestCase):
    def _request(self):
        return make_bake_request(
            "source-id",
            "material-id",
            [{"jobId": "job-1", "route": "MeshBake"}],
            source_material_name="Material",
            source_snapshot={"material": {"name": "Material"}},
            allow_appearance_approximation=False,
        )

    def test_certified_request_is_schema_valid_and_locked(self):
        request = self._request()
        schema = json.loads(
            (ROOT / "schema" / "miku-bake-request-1.2.schema.json").read_text(
                encoding="utf-8"
            )
        )
        jsonschema.validate(request, schema)
        self.assertEqual("miku-bake-request-1.2", request["documentKind"])
        self.assertEqual(DEFAULT_BAKE_RESOLUTION, request["settings"]["resolution"])
        self.assertEqual("5.2.0", BAKE_SETTINGS["blenderVersion"])
        self.assertEqual(
            "fbe6228777e7d9afefcd61a413844e790ae75db7",
            BAKE_SETTINGS["blenderCommit"],
        )
        self.assertEqual("CPU", BAKE_SETTINGS["device"])
        self.assertEqual(0, BAKE_SETTINGS["randomSeed"])

    def test_supported_resolutions_are_schema_valid_and_deterministic(self):
        schema = json.loads(
            (ROOT / "schema" / "miku-bake-request-1.2.schema.json").read_text(
                encoding="utf-8"
            )
        )
        hashes = set()
        for resolution in SUPPORTED_BAKE_RESOLUTIONS:
            with self.subTest(resolution=resolution):
                request = make_bake_request(
                    "source-id",
                    "material-id",
                    [{"jobId": "job-1", "route": "MeshBake"}],
                    source_material_name="Material",
                    source_snapshot={"material": {"name": "Material"}},
                    resolution=resolution,
                )
                jsonschema.validate(request, schema)
                self.assertEqual(resolution, request["settings"]["resolution"])
                self.assertEqual(
                    request,
                    make_bake_request(
                        "source-id",
                        "material-id",
                        [{"jobId": "job-1", "route": "MeshBake"}],
                        source_material_name="Material",
                        source_snapshot={"material": {"name": "Material"}},
                        resolution=resolution,
                    ),
                )
                hashes.add(request["canonicalHash"])
        self.assertEqual(len(SUPPORTED_BAKE_RESOLUTIONS), len(hashes))

    def test_invalid_resolution_is_rejected(self):
        for resolution in (0, 256, 8192, "large", None):
            with self.subTest(resolution=resolution):
                with self.assertRaisesRegex(ValueError, "MIKU_BAKE_RESOLUTION_INVALID"):
                    normalize_bake_resolution(resolution)

    def test_bake_runtime_version_is_major_validated(self):
        for version in ("5.0.0", "5.0.19", "5.1.7", "5.2.0", "5.2.1"):
            with self.subTest(version=version):
                self.assertEqual(version, normalize_bake_blender_version(version))
        for version in ("4.5.8", "6.0.0", "5.2"):
            with self.subTest(version=version):
                with self.assertRaisesRegex(
                    ValueError,
                    "MIKU_BAKE_BLENDER_VERSION",
                ):
                    normalize_bake_blender_version(version)
        self.assertEqual("abcdef123456", normalize_bake_blender_commit("ABCDEF123456"))
        with self.assertRaisesRegex(ValueError, "MIKU_BAKE_BLENDER_COMMIT_INVALID"):
            normalize_bake_blender_commit("not-a-build-hash")

    def test_worker_contract_accepts_frozen_request_1_0(self):
        current = self._request()
        payload = {
            key: value
            for key, value in current.items()
            if key
            not in {
                "documentKind",
                "schemaVersion",
                "toolVersion",
                "id",
                "canonicalHash",
            }
        }
        payload["settings"] = {
            **payload["settings"],
            "blenderVersion": "5.2.0 LTS",
        }
        legacy = make_document("miku-bake-request-1.0", payload)
        self.assertEqual(legacy, validate_bake_request(legacy))
        validate_bake_runtime_binding(
            legacy,
            "5.2.0",
            "fbe6228777e7d9afefcd61a413844e790ae75db7",
        )
        with self.assertRaisesRegex(RuntimeError, "MIKU_UNCERTIFIED_BLENDER"):
            validate_bake_runtime_binding(
                legacy,
                "5.1.0",
                "fbe6228777e7d9afefcd61a413844e790ae75db7",
            )

    def test_request_1_2_is_bound_to_exact_runtime_build(self):
        request = make_bake_request(
            "source-id",
            "material-id",
            [{"jobId": "job-1", "route": "MeshBake"}],
            source_material_name="Material",
            source_snapshot={"material": {"name": "Material"}},
            blender_version="5.1.4",
            blender_commit="abcdef1234567890",
        )
        validate_bake_runtime_binding(request, "5.1.4", "abcdef1234567890")
        with self.assertRaisesRegex(
            RuntimeError,
            "MIKU_BAKE_BLENDER_VERSION_MISMATCH",
        ):
            validate_bake_runtime_binding(request, "5.1.5", "abcdef1234567890")
        with self.assertRaisesRegex(
            RuntimeError,
            "MIKU_BAKE_BLENDER_COMMIT_MISMATCH",
        ):
            validate_bake_runtime_binding(request, "5.1.4", "abcdef1234567891")

    def test_result_is_bound_to_exact_request_hash(self):
        request = self._request()
        resource = {
            "id": "resource",
            "relativePath": "Baked/base.exr",
            "sha256": "0" * 64,
            "byteLength": 16,
            "mediaType": "image/x-exr",
            "semantic": "BaseColor",
            "channel": "RGBA",
            "colorSpace": "Linear",
            "width": 1024,
            "height": 1024,
            "channelCount": 4,
            "componentBytes": 2,
        }
        result = make_bake_result(request, [resource], status="completed")
        self.assertEqual(result, validate_bake_result(result, request))
        changed = make_bake_request(
            "source-id",
            "material-id",
            [{"jobId": "job-1", "route": "MeshBake"}],
            source_material_name="Material",
            source_snapshot={"material": {"name": "Material"}},
            allow_appearance_approximation=False,
            resolution=2048,
        )
        with self.assertRaisesRegex(ValueError, "REQUEST_HASH_MISMATCH"):
            validate_bake_result(result, changed)

    def test_bake_client_threads_selected_resolution_into_request(self):
        captured = {}

        def write_result(request_path, target):
            request = json.loads(Path(request_path).read_text(encoding="utf-8"))
            captured.update(request)
            resolution = request["settings"]["resolution"]
            resource = {
                "id": "resource",
                "relativePath": "Baked/base.exr",
                "sha256": "0" * 64,
                "byteLength": 16,
                "mediaType": "image/x-exr",
                "semantic": "BaseColor",
                "channel": "RGBA",
                "colorSpace": "Linear",
                "width": resolution,
                "height": resolution,
                "channelCount": 4,
                "componentBytes": 2,
            }
            result = make_bake_result(request, [resource], status="completed")
            result_path = Path(target) / "material-id.miku-bake-result.json"
            result_path.write_text(json.dumps(result), encoding="utf-8")

        with TemporaryDirectory() as temporary:
            fake_bpy = SimpleNamespace(
                app=SimpleNamespace(
                    version=(5, 2, 0),
                    build_hash=(
                        b"fbe6228777e7d9afefcd61a413844e790ae75db7"
                    ),
                )
            )
            with patch.dict(sys.modules, {"bpy": fake_bpy}), patch(
                    "miku_blender.bake_client._invoke_gpl_worker",
                    side_effect=write_result):
                request, result = execute_bake(
                    {"material": {"name": "Material"}},
                    {"bakeJobs": [{"jobId": "job-1", "route": "MeshBake"}]},
                    Path(temporary),
                    material_name="Material",
                    persistent_source_id="source-id",
                    persistent_material_id="material-id",
                    bake_resolution=2048,
                )
        self.assertEqual(2048, captured["settings"]["resolution"])
        self.assertEqual(request["canonicalHash"], result["requestHash"])
        self.assertEqual(2048, result["resources"][0]["width"])

    def test_gpl_worker_is_not_inside_mit_core_or_unity_package(self):
        self.assertFalse((ROOT / "miku_blender" / "gpl_bake_bridge.py").exists())
        forbidden = []
        for root in (
            ROOT / "miku",
            ROOT / "miku_blender",
            ROOT / "unity" / "Packages" / "com.miku.shaderconverter",
        ):
            for path in root.rglob("*"):
                if path.is_file() and path.suffix.lower() in {".py", ".cs"}:
                    text = path.read_text(encoding="utf-8", errors="replace")
                    if "def bake_material(" in text:
                        forbidden.append(path.relative_to(ROOT).as_posix())
        self.assertEqual([], forbidden)

    def test_unified_gpl_extension_release_is_deterministic_and_complete(self):
        from tools.build_miku_blender_extensions import build

        with TemporaryDirectory():
            first = build().read_bytes()
            second = build().read_bytes()
        self.assertEqual(first, second)
        package = ROOT / "dist" / "miku_shader_converter-2.2.11.zip"
        with zipfile.ZipFile(package) as archive:
            names = set(archive.namelist())
            manifest = archive.read("blender_manifest.toml").decode("utf-8")
            worker = archive.read("bake_worker/__init__.py").decode("utf-8")
        self.assertIn("miku_blender/__init__.py", names)
        self.assertIn("miku_blender/translations.py", names)
        self.assertIn("bake_worker/automatic_bake.py", names)
        self.assertIn("miku/bake_protocol.py", names)
        self.assertIn("LICENSE-MIT-ORIGIN.txt", names)
        self.assertIn("SPDX:GPL-3.0-or-later", manifest)
        self.assertIn("SPDX-License-Identifier: GPL-2.0-or-later", worker)
        self.assertIn("from ..miku.bake_protocol import", worker)
        self.assertIn("validate_bake_request", worker)
        self.assertNotIn("from .miku.bake_protocol import", worker)


if __name__ == "__main__":
    unittest.main()
