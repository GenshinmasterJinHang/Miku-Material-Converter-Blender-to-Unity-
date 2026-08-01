import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "unity" / "Packages" / "com.miku.shaderconverter"
COPYRIGHT = "SPDX-FileCopyrightText: 2026 Miku Project Authors"
LICENSE = "SPDX-License-Identifier: MIT"


class MikuGameBackendLicensingTests(unittest.TestCase):
    def test_game_shader_and_hlsl_sources_are_first_party_mit(self):
        files = []
        for family in ("Genshin", "Wuwa", "HSR"):
            root = PACKAGE / "Runtime" / family
            files.extend(root.glob("*.shader"))
            files.extend(root.glob("*.hlsl"))
        self.assertTrue(files)
        for path in files:
            source = path.read_text(encoding="utf-8")
            self.assertIn(COPYRIGHT, source, path)
            self.assertIn(LICENSE, source, path)

    def test_public_package_has_no_retired_b2u_csharp_api(self):
        retired = [
            path
            for path in PACKAGE.rglob("*.cs")
            if path.name.startswith("B2U") or "B2U." in path.name
        ]
        self.assertEqual([], retired)
        importer_sources = "\n".join(
            path.read_text(encoding="utf-8")
            for path in (PACKAGE / "Editor").glob("*Importer.cs")
        )
        self.assertNotIn('"b2ubundle"', importer_sources)
        self.assertIn('[ScriptedImporter(1, "migrbundle")]', importer_sources)

    def test_provenance_contains_non_affiliation_and_no_asset_commitment(self):
        source = (
            ROOT / "docs" / "provenance" / "game-workflow-backends.md"
        ).read_text(encoding="utf-8")
        self.assertIn("2026-07-27", source)
        self.assertIn("original project work", source)
        self.assertIn("not affiliated", source)
        self.assertIn("No extracted game", source)


if __name__ == "__main__":
    unittest.main()
