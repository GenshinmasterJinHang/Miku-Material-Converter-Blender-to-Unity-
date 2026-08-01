from __future__ import annotations

import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

from tools.miku_environment import (
    BLENDER_EXE,
    BLENDER_ROOT,
    assert_bpy_version,
    validate_blender_executable,
    validate_source_boundary,
)


ROOT = Path(__file__).resolve().parents[1]


class MikuEnvironmentTests(unittest.TestCase):
    def test_canonical_checkout_and_ids_pass(self):
        validate_source_boundary(ROOT)

    def test_fixed_blender_path_is_the_steam_52_install(self):
        self.assertEqual(
            Path(r"C:\SteamLibrary\steamapps\common\Blender"),
            BLENDER_ROOT,
        )
        self.assertEqual(BLENDER_ROOT / "blender.exe", BLENDER_EXE)
        self.assertEqual(BLENDER_EXE.resolve(), validate_blender_executable(BLENDER_EXE))

    def test_wrong_blender_path_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            wrong = Path(temporary) / "blender.exe"
            wrong.touch()
            with self.assertRaises(RuntimeError):
                validate_blender_executable(wrong)

    def test_bpy_version_must_be_exactly_520(self):
        assert_bpy_version(SimpleNamespace(app=SimpleNamespace(version=(5, 2, 0))))
        with self.assertRaises(RuntimeError):
            assert_bpy_version(
                SimpleNamespace(app=SimpleNamespace(version=(5, 1, 0)))
            )


if __name__ == "__main__":
    unittest.main()
