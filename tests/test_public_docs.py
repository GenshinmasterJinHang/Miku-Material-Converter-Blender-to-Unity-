"""Regression checks for the public bilingual GitHub documentation."""

from __future__ import annotations

import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


class PublicDocumentationTests(unittest.TestCase):
    def test_bilingual_entry_points_and_version(self) -> None:
        readme = (ROOT / "README.md").read_text(encoding="utf-8")
        chinese = (ROOT / "docs/zh-CN/README.md").read_text(encoding="utf-8")
        manual = (ROOT / "docs/manual.md").read_text(encoding="utf-8")
        chinese_manual = (ROOT / "docs/zh-CN/manual.md").read_text(encoding="utf-8")
        for document in (readme, chinese, manual, chinese_manual):
            self.assertIn("2.2.8", document)
            self.assertNotIn("0.11.0", document)
            self.assertNotIn("B2U", document)
        self.assertIn("docs/zh-CN/README.md", readme)
        self.assertIn("docs/manual.md", readme)
        self.assertIn("Standard PBR", readme)
        self.assertIn("standard_pbr", manual)

    def test_public_readme_links_and_images_exist(self) -> None:
        readme = (ROOT / "README.md").read_text(encoding="utf-8")
        for target in re.findall(r"!\[[^\]]*\]\(([^)]+)\)", readme):
            if target.startswith(("http://", "https://")):
                continue
            self.assertTrue((ROOT / target).is_file(), target)
        for target in re.findall(r"\[[^\]]+\]\(([^)]+)\)", readme):
            if target.startswith(("http://", "https://", "#")):
                continue
            self.assertTrue((ROOT / target).is_file(), target)

    def test_release_asset_names_and_workflow_menu_are_documented(self) -> None:
        release = (ROOT / "docs/release/miku-2.2.8.md").read_text(encoding="utf-8")
        self.assertIn("miku_shader_converter-2.2.8.zip", release)
        self.assertIn("com.miku.shaderconverter-2.2.8.tgz", release)
        self.assertIn("SHA256SUMS.txt", release)
        self.assertIn("Miku > Game Toon > Materials > Create Material", release)
        self.assertNotIn("Create Material Template", release)


if __name__ == "__main__":
    unittest.main()
