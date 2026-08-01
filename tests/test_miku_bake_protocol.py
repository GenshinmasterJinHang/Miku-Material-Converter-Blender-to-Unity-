import json
import unittest
import zipfile
from pathlib import Path
from tempfile import TemporaryDirectory

import jsonschema

from miku.bake_protocol import (
    BAKE_SETTINGS,
    make_bake_request,
    make_bake_result,
    validate_bake_result,
)


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
            (ROOT / "schema" / "miku-bake-request-1.0.schema.json").read_text(
                encoding="utf-8"
            )
        )
        jsonschema.validate(request, schema)
        self.assertEqual("5.2.0 LTS", BAKE_SETTINGS["blenderVersion"])
        self.assertEqual(
            "fbe6228777e7d9afefcd61a413844e790ae75db7",
            BAKE_SETTINGS["blenderCommit"],
        )
        self.assertEqual("CPU", BAKE_SETTINGS["device"])
        self.assertEqual(0, BAKE_SETTINGS["randomSeed"])

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
            "other-material-id",
            [{"jobId": "job-1", "route": "MeshBake"}],
            source_material_name="Material",
            source_snapshot={"material": {"name": "Material"}},
            allow_appearance_approximation=False,
        )
        with self.assertRaisesRegex(ValueError, "REQUEST_HASH_MISMATCH"):
            validate_bake_result(result, changed)

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
        package = ROOT / "dist" / "miku_shader_converter-1.0.1.zip"
        with zipfile.ZipFile(package) as archive:
            names = set(archive.namelist())
            manifest = archive.read("blender_manifest.toml").decode("utf-8")
            worker = archive.read("bake_worker/__init__.py").decode("utf-8")
        self.assertIn("miku_blender/__init__.py", names)
        self.assertIn("bake_worker/automatic_bake.py", names)
        self.assertIn("miku/bake_protocol.py", names)
        self.assertIn("LICENSE-MIT-ORIGIN.txt", names)
        self.assertIn("SPDX:GPL-3.0-or-later", manifest)
        self.assertIn("SPDX-License-Identifier: GPL-2.0-or-later", worker)
        self.assertIn("from ..miku.bake_protocol import", worker)
        self.assertNotIn("from .miku.bake_protocol import", worker)


if __name__ == "__main__":
    unittest.main()
