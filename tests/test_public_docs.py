"""Regression checks for the public bilingual GitHub documentation."""

from __future__ import annotations

import hashlib
import json
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PUBLIC_DOCUMENTS = (
    ROOT / "README.md",
    ROOT / "docs/zh-CN/README.md",
    ROOT / "docs/manual.md",
    ROOT / "docs/zh-CN/manual.md",
)
UI_IMAGES = (
    "blender-standard-pbr-en.png",
    "unity-game-material-wizard-en.png",
)
CHARACTER_IMAGE_HASHES = {
    "preset-genshin-hu-tao.png":
        "a8534a28eafa8a65aefef727d6d5b48fcbfce2dafbcca93d6fe4966ff83961eb",
    "preset-hsr-bronya.png":
        "67689b87c6c87348f1b4d7a4650196f782bc1fb751712da45c7ce0dd05261e89",
    "preset-wuwa-phoebe.png":
        "1da7a8af11a1240086dd3b95d19655f84d4bbb5ddac383b16a0cc5526de40823",
    "preset-endfield-jierpeta.png":
        "2252af935e59f00c8ed13871989ab0a5662b96d9d3e96022144c56ed292fe530",
}
UI_IMAGE_HASHES = {
    "blender-standard-pbr-en.png":
        "24aa37f677bf9e51d1d35259b8e284e144c0b08ae040edc510a575609a48915c",
    "unity-game-material-wizard-en.png":
        "c82e3325b05626c1a8772b59aa6a099397baa4336ea07af697e7b6fc40e3f118",
}
UNITY_MENU_PATHS = (
    "Miku > Settings",
    "Miku > Game Toon > Materials > Create Material",
    "Miku > Game Toon > Materials > Apply Recommended Skin & Highlight Profile",
    "Miku > Game Toon > Textures > Import Audit",
    "Miku > Game Toon > Mesh > Smooth Normal Generator",
    "Miku > Game Toon > Rendering > Screen Rim Installer",
    "Miku > Game Toon > Rendering > Rebuild Anime Global Volume Profile",
    "Miku > Migration > Dry Run Selected MiGR Assets",
    "Miku > Migration > Upgrade Selected MiGR Assets",
)


def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _normalized(text: str) -> str:
    return " ".join(text.split())


class PublicDocumentationTests(unittest.TestCase):
    def test_bilingual_entry_points_version_and_active_branding(self) -> None:
        readme, chinese, manual, chinese_manual = map(_read, PUBLIC_DOCUMENTS)
        for document in (readme, chinese, manual, chinese_manual):
            self.assertIn("2.2.12", document)
            self.assertNotIn("0.11.0", document)
            self.assertNotIn("B2U", document)
        self.assertIn("docs/zh-CN/README.md", readme)
        self.assertIn("docs/manual.md", readme)
        self.assertIn("Standard PBR", readme)
        self.assertIn("standard_pbr", manual)
        self.assertIn("standard_pbr", chinese_manual)
        self.assertNotIn("MiGR", readme)
        self.assertNotIn("MiGR", chinese)

    def test_relative_links_and_images_exist_in_every_public_document(self) -> None:
        link_pattern = re.compile(r"!?\[[^\]]*\]\(([^)]+)\)")
        for document_path in PUBLIC_DOCUMENTS:
            for raw_target in link_pattern.findall(_read(document_path)):
                target = raw_target.strip()
                if target.startswith(("http://", "https://", "mailto:", "#")):
                    continue
                target = target.split("#", 1)[0]
                self.assertTrue(
                    (document_path.parent / target).is_file(),
                    f"{document_path.relative_to(ROOT)} -> {raw_target}",
                )

    def test_real_ui_and_character_images_are_referenced(self) -> None:
        readme, chinese, manual, chinese_manual = map(_read, PUBLIC_DOCUMENTS)
        for image in UI_IMAGES[:1]:
            for document in (readme, chinese, manual, chinese_manual):
                self.assertIn(image, document)
        for document in (manual, chinese_manual):
            self.assertIn(UI_IMAGES[1], document)
            for image in CHARACTER_IMAGE_HASHES:
                self.assertIn(image, document)

        self.assertFalse(
            (ROOT / "docs/images/blender-standard-pbr-zh-cn.png").exists()
        )
        self.assertFalse(
            (ROOT / "docs/images/unity-game-material-wizard-zh-cn.png").exists()
        )
        self.assertFalse(
            (ROOT / "tools/docs/render_documentation_screenshots.py").exists()
        )

    def test_documentation_images_match_approved_source_hashes(self) -> None:
        for filename, expected in {**UI_IMAGE_HASHES,
                                   **CHARACTER_IMAGE_HASHES}.items():
            self.assertEqual(
                expected,
                _sha256(ROOT / "docs/images" / filename),
                filename,
            )

    def test_four_shader_families_and_22_parts_are_documented(self) -> None:
        readme, chinese, manual, chinese_manual = map(_read, PUBLIC_DOCUMENTS)
        for document in (readme, chinese, manual, chinese_manual):
            self.assertIn("Shader/HLSL", document)
            for workflow in ("Genshin", "HSR", "Wuwa", "Endfield"):
                self.assertIn(workflow, document)
        self.assertIn("22 valid material parts", readme)
        self.assertIn("22 valid material parts", manual)
        self.assertIn("22 个有效材质部位", chinese)
        self.assertIn("22 个有效材质部位", chinese_manual)
        for character in ("Hu Tao", "Bronya", "Phoebe", "洁尔佩塔"):
            self.assertIn(character, manual)
        for character in ("胡桃", "布洛妮娅", "菲比", "洁尔佩塔"):
            self.assertIn(character, chinese_manual)

    def test_public_unity_tools_are_in_both_manuals(self) -> None:
        manual = _normalized(_read(ROOT / "docs/manual.md"))
        chinese_manual = _normalized(_read(ROOT / "docs/zh-CN/manual.md"))
        for menu_path in UNITY_MENU_PATHS:
            self.assertIn(menu_path, manual)
            self.assertIn(menu_path, chinese_manual)
        for inspector in (
            "Miku Material Inspector",
            "Mesh Binding Description Inspector",
            "Toon Material Recipe Inspector",
        ):
            self.assertIn(inspector, manual)

    def test_code_and_character_image_license_boundaries_are_explicit(self) -> None:
        readme, chinese, manual, chinese_manual = map(_read, PUBLIC_DOCUMENTS)
        provenance = _read(ROOT / "docs/provenance/documentation-images.md")
        notices = _read(ROOT / "THIRD_PARTY_NOTICES.md")
        self.assertIn("MIT License", readme)
        self.assertIn("MIT License", chinese)
        self.assertIn("Commercial use is prohibited", manual)
        self.assertIn("禁止用于任何商业用途", chinese_manual)
        self.assertIn("excluded from Miku's MIT", provenance)
        self.assertIn("commercial use is prohibited", _normalized(notices))
        for filename in CHARACTER_IMAGE_HASHES:
            self.assertIn(filename, provenance)
        package = json.loads(
            _read(ROOT / "unity/Packages/com.miku.shaderconverter/package.json")
        )
        self.assertEqual("MIT", package["license"])
        self.assertIn("MIT License", _read(ROOT / "LICENSE"))

    def test_release_asset_names_and_workflow_menu_are_documented(self) -> None:
        release = _read(ROOT / "docs/release/miku-2.2.9.md")
        self.assertIn("miku_shader_converter-2.2.9.zip", release)
        self.assertIn("com.miku.shaderconverter-2.2.9.tgz", release)
        self.assertIn("SHA256SUMS.txt", release)
        self.assertIn("Miku > Game Toon > Materials > Create Material", release)
        self.assertNotIn("Create Material Template", release)


if __name__ == "__main__":
    unittest.main()
